using System.Collections.Concurrent;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using MiniExcelLibs;

namespace HExporter.Export.Xlsx;

/// <summary>
/// Escribe XLSX en streaming con MiniExcel. MiniExcel consume (pull) un IEnumerable
/// perezoso; el patrón IExportWriter empuja (push). Se puentea con una cola acotada
/// (BlockingCollection) + tarea consumidora: preserva streaming y backpressure.
/// Memoria acotada por la capacidad de la cola.
/// </summary>
public sealed class XlsxExportWriter : IExportWriter
{
    private const int QueueCapacity = 2048;

    private readonly Stream _destination;
    private readonly XlsxOptions _xlsx;
    private readonly bool _includeHeaders;
    private readonly BlockingCollection<IDictionary<string, object?>> _queue = new(QueueCapacity);

    private IReadOnlyList<ColumnSchema> _schema = Array.Empty<ColumnSchema>();
    private Task? _consumer;
    private long _rowCount;
    private volatile Exception? _failure;

    public XlsxExportWriter(Stream destination, ExportOptions options)
    {
        _destination = destination;
        _xlsx = options.Xlsx;
        _includeHeaders = options.IncludeHeaders;
    }

    public ValueTask BeginAsync(IReadOnlyList<ColumnSchema> schema, CancellationToken ct)
    {
        _schema = schema;
        _consumer = Task.Run(() =>
        {
            try
            {
                _destination.SaveAs(
                    _queue.GetConsumingEnumerable(),
                    printHeader: _includeHeaders,
                    sheetName: _xlsx.SheetName,
                    excelType: ExcelType.XLSX);
            }
            catch (Exception ex)
            {
                _failure = ex;
                // Drenar para desbloquear a productores que estén en Add().
                foreach (var _ in _queue.GetConsumingEnumerable()) { }
            }
        }, ct);
        return ValueTask.CompletedTask;
    }

    public void WriteRow(IRecordReader row)
    {
        if (_failure is not null) throw new InvalidOperationException("Fallo del writer XLSX.", _failure);

        if (++_rowCount > XlsxOptions.MaxRowsPerSheet &&
            _xlsx.RowLimitStrategy == XlsxRowLimitStrategy.Fail)
        {
            throw new InvalidOperationException(
                $"El resultado supera el límite de {XlsxOptions.MaxRowsPerSheet:N0} filas por hoja XLSX. " +
                "Use CSV o configure RowLimitStrategy=NewSheet. Ver docs/04-streaming-strategy.md §6.");
        }

        var dict = new Dictionary<string, object?>(_schema.Count);
        for (int i = 0; i < _schema.Count; i++)
            dict[_schema[i].Name] = row.IsDBNull(i) ? null : row.GetValue(i);
        _queue.Add(dict);
    }

    public ValueTask FlushAsync(CancellationToken ct) => ValueTask.CompletedTask;

    public async ValueTask EndAsync(CancellationToken ct)
    {
        _queue.CompleteAdding();
        if (_consumer is not null) await _consumer;
        if (_failure is not null)
            throw new InvalidOperationException("Fallo al escribir XLSX.", _failure);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_queue.IsAddingCompleted) _queue.CompleteAdding();
        if (_consumer is not null)
        {
            try { await _consumer; } catch { /* ya reportado en EndAsync */ }
        }
        _queue.Dispose();
    }
}
