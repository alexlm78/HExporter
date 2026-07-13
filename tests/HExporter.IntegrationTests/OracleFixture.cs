using DotNet.Testcontainers.Builders;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

namespace HExporter.IntegrationTests;

/// <summary>
/// Un solo contenedor Oracle (gvenzl/oracle-free) compartido por toda la colección de
/// tests — arrancarlo es lento (~30-60s), no vale la pena por test. Ver docs/07-testing-strategy.md.
///
/// La wait strategy por defecto de Testcontainers.Oracle (buscar mensaje en logs) choca con
/// podman: Docker.DotNet lanza "Invalid chunk header encountered" al leer logs vía el socket
/// de podman (incompatibilidad conocida en el streaming de logs, no un bug de esta app). Se
/// reemplaza por espera de puerto + retry de conexión real, que evita ese endpoint de logs.
///
/// No se usa `.WithDatabase(...)`: gvenzl/oracle-free ya crea por defecto el PDB "FREEPDB1";
/// pedirle explícitamente crear uno con ese mismo nombre dispara "ORA-65012: ya existe" y
/// el contenedor aborta el arranque. El servicio por defecto de la librería (Testcontainers.Oracle
/// usa "XE", pensado para oracle-xe) tampoco sirve aquí, así que el connection string de conexión
/// se arma a mano contra el PDB real ("FREEPDB1") en vez de usar `_container.GetConnectionString()`.
/// </summary>
public sealed class OracleFixture : IAsyncLifetime
{
    private OracleContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _container = new OracleBuilder("gvenzl/oracle-free:slim-faststart")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1521))
            .Build();
        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(1521);
        var descriptor =
            $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME=FREEPDB1)))";
        // Pooling=false + Connection Timeout corto: cada intento falla rápido en vez de
        // esperar el timeout por defecto (~15s) del connection pool de ODP.NET.
        ConnectionString = $"User Id=oracle;Password=oracle;Data Source={descriptor};Pooling=false;Connection Timeout=5";
        var probeConnectionString = ConnectionString;

        var deadline = DateTime.UtcNow.AddSeconds(300);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var conn = new OracleConnection(probeConnectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM dual";
                await cmd.ExecuteScalarAsync();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        throw new TimeoutException("Oracle no respondió a tiempo tras iniciar el contenedor.", lastError);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class OracleCollection : ICollectionFixture<OracleFixture>
{
    public const string Name = "Oracle";
}
