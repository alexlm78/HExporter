using HExporter.Core.Models;

namespace HExporter.Core.Abstractions;

/// <summary>
/// Puerto de escritura incremental. Consume filas del reader de a una,
/// sin retener referencias. Escribe al Stream de salida por buffer acotado.
/// </summary>
public interface IExportWriter : IAsyncDisposable
{
    /// <summary>Inicializa el formato (encabezados / hoja) a partir del schema.</summary>
    ValueTask BeginAsync(IReadOnlyList<ColumnSchema> schema, CancellationToken ct);

    /// <summary>Escribe la fila actual del reader. No debe retener el reader.</summary>
    void WriteRow(IRecordReader row);

    /// <summary>Vacía el buffer al stream subyacente.</summary>
    ValueTask FlushAsync(CancellationToken ct);

    /// <summary>Cierra estructuras del formato y hace flush final.</summary>
    ValueTask EndAsync(CancellationToken ct);
}
