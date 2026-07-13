using HExporter.Core.Models;

namespace HExporter.Core.Abstractions;

public interface IExportWriterFactory
{
    /// <summary>Crea el writer del formato indicado, escribiendo al stream destino.</summary>
    IExportWriter Create(ExportFormat format, Stream destination, ExportOptions options);
}
