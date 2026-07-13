using HExporter.Core.Models;

namespace HExporter.Core.Abstractions;

public interface IRecordReaderFactory
{
    /// <summary>Abre un reader forward-only para la petición (conexión + comando + FetchSize).</summary>
    Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct);
}
