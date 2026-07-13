using System.Globalization;
using System.Text;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.Export.Csv;

/// <summary>
/// Escribe CSV fila a fila con StreamWriter directo. Quoting RFC 4180.
/// No acumula filas — memoria acotada por el buffer del StreamWriter.
/// </summary>
public sealed class CsvExportWriter : IExportWriter
{
    private readonly StreamWriter _writer;
    private readonly string _delimiter;
    private readonly char _delimiterChar;
    private readonly bool _includeHeaders;
    private readonly CultureInfo _culture;
    private readonly string _dateFormat;
    private IReadOnlyList<ColumnSchema> _schema = Array.Empty<ColumnSchema>();

    public CsvExportWriter(Stream destination, ExportOptions options)
    {
        _delimiter = options.Csv.Delimiter;
        _delimiterChar = _delimiter.Length == 1 ? _delimiter[0] : '\0';
        _includeHeaders = options.IncludeHeaders;
        _culture = options.Culture;
        _dateFormat = options.DateFormat;
        _writer = new StreamWriter(destination, options.Csv.ResolveEncoding(), options.FileBufferBytes)
        {
            AutoFlush = false
        };
    }

    public async ValueTask BeginAsync(IReadOnlyList<ColumnSchema> schema, CancellationToken ct)
    {
        _schema = schema;
        if (!_includeHeaders) return;
        for (int i = 0; i < schema.Count; i++)
        {
            if (i > 0) _writer.Write(_delimiter);
            WriteField(schema[i].Name);
        }
        _writer.Write('\n');
        await Task.CompletedTask;
    }

    public void WriteRow(IRecordReader row)
    {
        for (int i = 0; i < _schema.Count; i++)
        {
            if (i > 0) _writer.Write(_delimiter);
            if (row.IsDBNull(i)) continue; // NULL -> celda vacía
            WriteValue(row.GetValue(i));
        }
        _writer.Write('\n');
    }

    private void WriteValue(object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string s:
                WriteField(s);
                return;
            case DateTime dt:
                WriteField(dt.ToString(_dateFormat, _culture));
                return;
            case bool b:
                _writer.Write(b ? "true" : "false");
                return;
            case IFormattable f:
                // números/decimales con cultura fija; no requieren quoting
                _writer.Write(f.ToString(null, _culture));
                return;
            default:
                WriteField(value.ToString() ?? string.Empty);
                return;
        }
    }

    /// <summary>Escribe un campo aplicando quoting RFC 4180 solo si hace falta.</summary>
    private void WriteField(string s)
    {
        bool needsQuote = s.IndexOf('"') >= 0
                          || s.IndexOf('\n') >= 0
                          || s.IndexOf('\r') >= 0
                          || (_delimiterChar != '\0' ? s.IndexOf(_delimiterChar) >= 0 : s.Contains(_delimiter, StringComparison.Ordinal));
        if (!needsQuote)
        {
            _writer.Write(s);
            return;
        }
        _writer.Write('"');
        foreach (char c in s)
        {
            if (c == '"') _writer.Write('"'); // escape: " -> ""
            _writer.Write(c);
        }
        _writer.Write('"');
    }

    public async ValueTask FlushAsync(CancellationToken ct) => await _writer.FlushAsync(ct);

    public async ValueTask EndAsync(CancellationToken ct) => await _writer.FlushAsync(ct);

    public async ValueTask DisposeAsync() => await _writer.DisposeAsync();
}
