namespace HExporter.Core.Models;

/// <summary>Definición declarativa reutilizable de un reporte (report.json).</summary>
public sealed class ReportProfile
{
    public string Name { get; init; } = "";
    public string Sql { get; init; } = "";
    public Dictionary<string, object?> Binds { get; init; } = new();
    public ExportFormat Format { get; init; } = ExportFormat.Csv;
    public CsvOptions? Csv { get; init; }
    public XlsxOptions? Xlsx { get; init; }
}
