using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.UnitTests;

/// <summary>Reader en memoria para pruebas (no toca Oracle).</summary>
public sealed class FakeRecordReader : IRecordReader
{
    private readonly object?[][] _rows;
    private int _index = -1;

    public IReadOnlyList<ColumnSchema> Schema { get; }

    public FakeRecordReader(IReadOnlyList<ColumnSchema> schema, object?[][] rows)
    {
        Schema = schema;
        _rows = rows;
    }

    public ValueTask<bool> ReadAsync(CancellationToken ct) => new(++_index < _rows.Length);
    public object? GetValue(int ordinal) => _rows[_index][ordinal];
    public bool IsDBNull(int ordinal) => _rows[_index][ordinal] is null;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
