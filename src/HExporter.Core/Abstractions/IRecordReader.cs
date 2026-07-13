using HExporter.Core.Models;

namespace HExporter.Core.Abstractions;

/// <summary>
/// Puerto de lectura forward-only. Envuelve el reader del proveedor sin exponerlo.
/// Mantiene UNA fila viva a la vez — nunca materializa el resultado completo.
/// </summary>
public interface IRecordReader : IAsyncDisposable
{
    IReadOnlyList<ColumnSchema> Schema { get; }

    /// <summary>Avanza a la siguiente fila. False cuando no hay más.</summary>
    ValueTask<bool> ReadAsync(CancellationToken ct);

    /// <summary>Valor de la columna en la fila actual (null si DBNull).</summary>
    object? GetValue(int ordinal);

    bool IsDBNull(int ordinal);
}
