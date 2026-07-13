using System.Collections;
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
///
/// RowLimitStrategy=NewSheet: MiniExcel detecta multi-hoja cuando el `value` pasado a
/// SaveAs implementa IDictionary&lt;string, object&gt; (ver GetSheets() en
/// ExcelOpenXmlSheetWriter, que solo llama GetEnumerator() — nunca .Count/.Keys/indexer).
/// <see cref="SheetSource"/> explota eso: es un IDictionary "de mentira" cuyo enumerador
/// bloquea/produce hojas a medida que el productor las genera, preservando streaming.
/// </summary>
public sealed class XlsxExportWriter : IExportWriter
{
    private const int QueueCapacity = 2048;

    private readonly Stream _destination;
    private readonly XlsxOptions _xlsx;
    private readonly bool _includeHeaders;
    private readonly BlockingCollection<KeyValuePair<string, object>> _sheets = new(boundedCapacity: 1);

    private BlockingCollection<IDictionary<string, object?>> _currentRows =
        new(QueueCapacity);

    private IReadOnlyList<ColumnSchema> _schema = Array.Empty<ColumnSchema>();
    private Task? _consumer;
    private long _rowsInCurrentSheet;
    private int _sheetIndex = 1;
    private volatile Exception? _failure;

    /// <summary>Límite real de filas/hoja (const público). Ajustable solo desde tests
    /// (InternalsVisibleTo) para ejercitar RowLimitStrategy.NewSheet sin escribir 1M+ filas.</summary>
    internal long MaxRowsPerSheetOverride { get; init; } = XlsxOptions.MaxRowsPerSheet;

    public XlsxExportWriter(Stream destination, ExportOptions options)
    {
        _destination = destination;
        _xlsx = options.Xlsx;
        _includeHeaders = options.IncludeHeaders;
    }

    public ValueTask BeginAsync(IReadOnlyList<ColumnSchema> schema, CancellationToken ct)
    {
        _schema = schema;
        _sheets.Add(new KeyValuePair<string, object>(_xlsx.SheetName, _currentRows.GetConsumingEnumerable()));

        _consumer = Task.Run(() =>
        {
            try
            {
                _destination.SaveAs(
                    new SheetSource(_sheets),
                    printHeader: _includeHeaders,
                    excelType: ExcelType.XLSX);
            }
            catch (Exception ex)
            {
                _failure = ex;
                DrainOnFailure();
            }
        }, ct);
        return ValueTask.CompletedTask;
    }

    public void WriteRow(IRecordReader row)
    {
        if (_failure is not null) throw new InvalidOperationException("Fallo del writer XLSX.", _failure);

        if (_rowsInCurrentSheet >= MaxRowsPerSheetOverride)
        {
            if (_xlsx.RowLimitStrategy == XlsxRowLimitStrategy.Fail)
            {
                throw new InvalidOperationException(
                    $"El resultado supera el límite de {XlsxOptions.MaxRowsPerSheet:N0} filas por hoja XLSX. " +
                    "Use CSV o configure RowLimitStrategy=NewSheet. Ver docs/04-streaming-strategy.md §6.");
            }

            RollToNextSheet();
        }

        var dict = new Dictionary<string, object?>(_schema.Count);
        for (int i = 0; i < _schema.Count; i++)
            dict[_schema[i].Name] = row.IsDBNull(i) ? null : row.GetValue(i);

        _currentRows.Add(dict);
        _rowsInCurrentSheet++;
    }

    // Cierra la hoja actual y abre la siguiente en la cola de hojas (RowLimitStrategy.NewSheet).
    private void RollToNextSheet()
    {
        _currentRows.CompleteAdding();
        _sheetIndex++;
        _currentRows = new BlockingCollection<IDictionary<string, object?>>(QueueCapacity);
        _rowsInCurrentSheet = 0;
        _sheets.Add(new KeyValuePair<string, object>($"{_xlsx.SheetName}_{_sheetIndex}", _currentRows.GetConsumingEnumerable()));
    }

    public ValueTask FlushAsync(CancellationToken ct) => ValueTask.CompletedTask;

    public async ValueTask EndAsync(CancellationToken ct)
    {
        _currentRows.CompleteAdding();
        _sheets.CompleteAdding();
        if (_consumer is not null) await _consumer;
        if (_failure is not null)
            throw new InvalidOperationException("Fallo al escribir XLSX.", _failure);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_currentRows.IsAddingCompleted) _currentRows.CompleteAdding();
        if (!_sheets.IsAddingCompleted) _sheets.CompleteAdding();
        if (_consumer is not null)
        {
            try { await _consumer; } catch { /* ya reportado en EndAsync */ }
        }
        _currentRows.Dispose();
        _sheets.Dispose();
    }

    // Desbloquea cualquier Add() del productor (a lo sumo uno, en _sheets o en la hoja
    // actual) drenando ambas colas en tareas de fondo hasta que Dispose las complete.
    private void DrainOnFailure()
    {
        _ = Task.Run(() => { foreach (var _ in _sheets.GetConsumingEnumerable()) { } });
        _ = Task.Run(() => { foreach (var _ in _currentRows.GetConsumingEnumerable()) { } });
    }

    /// <summary>IDictionary "de mentira": solo implementa GetEnumerator (lo único que
    /// ExcelOpenXmlSheetWriter.GetSheets() invoca). El resto de los miembros nunca se
    /// llaman en el camino de escritura y lanzan si alguien los usa por error.</summary>
    private sealed class SheetSource(BlockingCollection<KeyValuePair<string, object>> source)
        : IDictionary<string, object>
    {
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() =>
            source.GetConsumingEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public object this[string key]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public ICollection<string> Keys => throw new NotSupportedException();
        public ICollection<object> Values => throw new NotSupportedException();
        public int Count => throw new NotSupportedException();
        public bool IsReadOnly => true;
        public void Add(string key, object value) => throw new NotSupportedException();
        public void Add(KeyValuePair<string, object> item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(KeyValuePair<string, object> item) => throw new NotSupportedException();
        public bool ContainsKey(string key) => throw new NotSupportedException();
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => throw new NotSupportedException();
        public bool Remove(string key) => throw new NotSupportedException();
        public bool Remove(KeyValuePair<string, object> item) => throw new NotSupportedException();
        public bool TryGetValue(string key, out object value) => throw new NotSupportedException();
    }
}
