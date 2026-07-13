namespace HExporter.Infrastructure.Oracle;

public sealed class OracleOptions
{
    public const string SectionName = "Oracle";

    /// <summary>Cadena de conexión. Resolver desde env/secret/Wallet — nunca hardcodear.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Bytes por lote de red. ~256KB–1MB. NO es el resultado completo.</summary>
    public long FetchSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>0 = sin límite (reportes largos).</summary>
    public int CommandTimeoutSeconds { get; set; } = 0;

    public bool BindByName { get; set; } = true;

    /// <summary>Reintentos ante fallo transitorio de apertura de conexión (Polly). 0 = sin reintentos.</summary>
    public int ConnectRetryAttempts { get; set; } = 3;

    /// <summary>Base del backoff exponencial entre reintentos de conexión.</summary>
    public double ConnectRetryBaseDelaySeconds { get; set; } = 2.0;
}
