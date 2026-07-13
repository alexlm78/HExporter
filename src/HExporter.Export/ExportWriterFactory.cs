using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using HExporter.Export.Csv;
using HExporter.Export.Xlsx;

namespace HExporter.Export;

public sealed class ExportWriterFactory : IExportWriterFactory
{
    public IExportWriter Create(ExportFormat format, Stream destination, ExportOptions options) => format switch
    {
        ExportFormat.Csv => new CsvExportWriter(destination, options),
        ExportFormat.Xlsx => new XlsxExportWriter(destination, options),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Formato no soportado.")
    };
}
