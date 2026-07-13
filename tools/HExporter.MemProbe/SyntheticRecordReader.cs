using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.MemProbe;

/// <summary>
/// Genera N filas sintéticas al vuelo, sin DB y sin acumular estado.
/// Memoria O(1): solo mantiene el índice actual. Prueba la memoria del pipeline
/// de escritura de forma aislada (sin Oracle). Ver docs/07-testing-strategy.md §4.
/// </summary>
public sealed class SyntheticRecordReader : IRecordReader
{
    private static readonly ColumnSchema[] Cols =
    {
        new(0, "ID",     typeof(long),     "NUMBER"),
        new(1, "FECHA",  typeof(DateTime), "DATE"),
        new(2, "MONTO",  typeof(decimal),  "NUMBER"),
        new(3, "CLIENTE", typeof(string),  "VARCHAR2")
    };

    private static readonly DateTime Base = new(2000, 1, 1);

    private readonly long _rows;
    private long _index = -1;

    public SyntheticRecordReader(long rows) => _rows = rows;

    public IReadOnlyList<ColumnSchema> Schema => Cols;

    public ValueTask<bool> ReadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<bool>(++_index < _rows);
    }

    public object? GetValue(int ordinal) => ordinal switch
    {
        0 => _index,
        1 => Base.AddSeconds(_index % 315_360_000), // ~10 años de rango
        2 => decimal.Round((decimal)((_index * 7919 % 1_000_000) / 100.0), 2),
        3 => "cliente_" + _index,
        _ => null
    };

    public bool IsDBNull(int ordinal) => false;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class SyntheticReaderFactory : IRecordReaderFactory
{
    private readonly long _rows;
    public SyntheticReaderFactory(long rows) => _rows = rows;

    public Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct)
        => Task.FromResult<IRecordReader>(new SyntheticRecordReader(_rows));
}
