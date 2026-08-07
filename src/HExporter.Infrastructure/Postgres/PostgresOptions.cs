namespace HExporter.Infrastructure.Postgres;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    /// <summary>Cadena de conexión. Resolver desde env/secret — nunca hardcodear.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>0 = sin límite (reportes largos).</summary>
    public int CommandTimeoutSeconds { get; set; } = 0;

    /// <summary>Reintentos ante fallo transitorio de apertura de conexión (Polly). 0 = sin reintentos.</summary>
    public int ConnectRetryAttempts { get; set; } = 3;

    /// <summary>Base del backoff exponencial entre reintentos de conexión.</summary>
    public double ConnectRetryBaseDelaySeconds { get; set; } = 2.0;
}
