using FluentAssertions;
using HExporter.Core.Models;
using HExporter.Infrastructure.Oracle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HExporter.IntegrationTests;

/// <summary>
/// T3.6: OracleRecordReader contra un Oracle real (Testcontainers). Verifica el
/// contrato forward-only, bind variables y que un FetchSize pequeño (varios round-trips
/// de red) no pierde ni duplica filas — la razón de ser de streaming por lotes acotados.
/// </summary>
[Collection(OracleCollection.Name)]
public sealed class OracleRecordReaderTests(OracleFixture fixture)
{
    private OracleConnectionFactory CreateFactory(long fetchSizeBytes = 1024 * 1024)
    {
        var options = new OracleOptions
        {
            ConnectionString = fixture.ConnectionString,
            FetchSizeBytes = fetchSizeBytes,
            ConnectRetryAttempts = 0
        };
        return new OracleConnectionFactory(Options.Create(options), NullLogger<OracleConnectionFactory>.Instance);
    }

    // Nombre de tabla único (< 30 chars, límite de identificador Oracle sin comillas).
    private static string UniqueTable(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();

    [Fact]
    public async Task Reads_rows_streaming_from_real_oracle()
    {
        var factory = CreateFactory();
        string table = UniqueTable("HX_BASIC");

        await using (var conn = await factory.OpenAsync(default))
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE TABLE {table} (id NUMBER, nombre VARCHAR2(50), monto NUMBER(10,2))";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = $"INSERT INTO {table} VALUES (1, 'ana', 10.5)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO {table} VALUES (2, 'beto', 20.75)";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var reader = await OracleRecordReader.OpenAsync(
            factory, $"SELECT id, nombre, monto FROM {table} ORDER BY id", ExportRequest.NoBinds, default);

        reader.Schema.Should().HaveCount(3);
        reader.Schema[0].Name.Should().Be("ID");
        reader.Schema[1].Name.Should().Be("NOMBRE");
        reader.Schema[2].Name.Should().Be("MONTO");

        var rows = new List<(decimal Id, string Nombre, decimal Monto)>();
        while (await reader.ReadAsync(default))
        {
            rows.Add((
                Convert.ToDecimal(reader.GetValue(0)),
                (string)reader.GetValue(1)!,
                Convert.ToDecimal(reader.GetValue(2))));
        }

        rows.Should().HaveCount(2);
        rows[0].Should().Be((1m, "ana", 10.5m));
        rows[1].Should().Be((2m, "beto", 20.75m));
    }

    [Fact]
    public async Task Bind_variables_filter_rows_via_bind_by_name()
    {
        var factory = CreateFactory();
        string table = UniqueTable("HX_BIND");

        await using (var conn = await factory.OpenAsync(default))
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE TABLE {table} (id NUMBER, fecha DATE)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO {table} VALUES (1, DATE '2025-01-01')";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO {table} VALUES (2, DATE '2026-06-01')";
            await cmd.ExecuteNonQueryAsync();
        }

        var binds = new Dictionary<string, object?> { ["desde"] = new DateTime(2026, 1, 1) };
        await using var reader = await OracleRecordReader.OpenAsync(
            factory, $"SELECT id FROM {table} WHERE fecha >= :desde ORDER BY id", binds, default);

        var ids = new List<decimal>();
        while (await reader.ReadAsync(default))
            ids.Add(Convert.ToDecimal(reader.GetValue(0)));

        ids.Should().Equal(2m);
    }

    [Fact]
    public async Task Small_fetch_size_forces_multiple_round_trips_without_losing_rows()
    {
        var factory = CreateFactory(fetchSizeBytes: 2048); // fuerza varios lotes de red
        string table = UniqueTable("HX_FETCH");
        const int expectedRows = 3000;

        await using (var conn = await factory.OpenAsync(default))
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE TABLE {table} (id NUMBER, nombre VARCHAR2(50))";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText =
                $"INSERT INTO {table} (id, nombre) " +
                $"SELECT LEVEL, 'cliente_' || LEVEL FROM dual CONNECT BY LEVEL <= {expectedRows}";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var reader = await OracleRecordReader.OpenAsync(
            factory, $"SELECT id, nombre FROM {table} ORDER BY id", ExportRequest.NoBinds, default);

        long count = 0;
        decimal lastId = 0;
        while (await reader.ReadAsync(default))
        {
            count++;
            lastId = Convert.ToDecimal(reader.GetValue(0));
        }

        count.Should().Be(expectedRows);
        lastId.Should().Be(expectedRows);
    }
}
