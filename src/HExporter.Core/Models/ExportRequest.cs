namespace HExporter.Core.Models;

/// <summary>Petición de exportación resuelta (SQL final + binds + destino).</summary>
public sealed record ExportRequest(
    string Sql,
    IReadOnlyDictionary<string, object?> Binds,
    ExportFormat Format,
    string DestinationPath,
    ExportOptions Options)
{
    public static IReadOnlyDictionary<string, object?> NoBinds { get; } =
        new Dictionary<string, object?>();
}
