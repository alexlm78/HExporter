namespace HExporter.Infrastructure;

/// <summary>Resuelve el motor de base de datos activo. Precedencia: CLI > configuración
/// (env var real / [dot]env / appsettings.json, en ese orden, ya fusionada por IConfiguration).
/// Default: Oracle.</summary>
public static class DatabaseEngineResolver
{
    public const string ConfigKey = "Database:Engine";

    public static DatabaseEngine Resolve(string? cliValue, string? configuredValue)
    {
        string? raw = !string.IsNullOrWhiteSpace(cliValue) ? cliValue : configuredValue;
        if (string.IsNullOrWhiteSpace(raw))
            return DatabaseEngine.Oracle;

        return raw.Trim().ToLowerInvariant() switch
        {
            "oracle" => DatabaseEngine.Oracle,
            "postgres" or "postgresql" or "pg" => DatabaseEngine.Postgres,
            _ => throw new ArgumentException(
                $"Unsupported database engine '{raw}'. Valid values: oracle, postgres.")
        };
    }
}
