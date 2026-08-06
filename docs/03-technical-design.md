# 03 — Detailed Technical Design

## 1. Solution structure

```
HExporter.sln
├─ src/
│  ├─ HExporter.Core/            # Ports, models, contracts. No external dependencies.
│  │   ├─ Abstractions/
│  │   │   ├─ IRecordReader.cs
│  │   │   ├─ IExportWriter.cs
│  │   │   └─ IExportWriterFactory.cs
│  │   ├─ Models/
│  │   │   ├─ ColumnSchema.cs
│  │   │   ├─ ExportRequest.cs
│  │   │   ├─ ExportResult.cs
│  │   │   ├─ ExportFormat.cs      # enum: Csv, Xlsx
│  │   │   └─ ReportProfile.cs
│  │   └─ Progress/IProgressSink.cs
│  ├─ HExporter.Application/      # ExportService, validation, profile loading
│  │   ├─ ExportService.cs
│  │   ├─ ReportProfileLoader.cs
│  │   └─ Validation/ExportRequestValidator.cs
│  ├─ HExporter.Infrastructure/   # Oracle adapter
│  │   ├─ Oracle/OracleRecordReader.cs
│  │   ├─ Oracle/OracleConnectionFactory.cs
│  │   └─ Oracle/OracleOptions.cs
│  ├─ HExporter.Export/           # CSV/XLSX writers
│  │   ├─ Csv/CsvExportWriter.cs
│  │   ├─ Csv/CsvOptions.cs
│  │   ├─ Xlsx/XlsxExportWriter.cs
│  │   ├─ Xlsx/XlsxOptions.cs
│  │   └─ ExportWriterFactory.cs
│  └─ HExporter.Cli/             # Entry point, System.CommandLine, host DI, Serilog
│      ├─ Program.cs
│      └─ Commands/ExportCommand.cs
└─ tests/
   ├─ HExporter.UnitTests/
   └─ HExporter.IntegrationTests/   # Oracle via Testcontainers
```

## 2. Contracts (ports)

### 2.1 `ColumnSchema`

```csharp
public sealed record ColumnSchema(int Ordinal, string Name, Type ClrType, string DbTypeName);
```

### 2.2 `IRecordReader`

Forward-only. Wraps `OracleDataReader` without exposing driver details.

```csharp
public interface IRecordReader : IAsyncDisposable
{
    IReadOnlyList<ColumnSchema> Schema { get; }

    /// Advances to the next row. False when there are no more.
    ValueTask<bool> ReadAsync(CancellationToken ct);

    /// Value of the column in the current row (boxed, or use GetValue for typed access).
    object? GetValue(int ordinal);
    bool IsDBNull(int ordinal);
}
```

> **Performance note:** `GetValue` returns `object?` (boxing). For extremely high-volume numeric columns, typed accessors can be added (`GetInt64`, `GetDecimal`, `GetString`) for writers to use and avoid boxing. See [04-streaming-strategy.md](./04-streaming-strategy.md) §5.

### 2.3 `IExportWriter`

```csharp
public interface IExportWriter : IAsyncDisposable
{
    /// Writes headers / initializes the sheet. Receives the reader's schema.
    ValueTask BeginAsync(IReadOnlyList<ColumnSchema> schema, CancellationToken ct);

    /// Writes a row reading from the current reader. Must not retain references.
    void WriteRow(IRecordReader row);

    /// Forces the buffer to flush to the underlying stream.
    ValueTask FlushAsync(CancellationToken ct);

    /// Closes format-specific structures (XLSX footer, final flush).
    ValueTask EndAsync(CancellationToken ct);
}
```

### 2.4 `IExportWriterFactory`

```csharp
public interface IExportWriterFactory
{
    IExportWriter Create(ExportFormat format, Stream destination, ExportOptions options);
}
```

## 3. Application models

```csharp
public enum ExportFormat { Csv, Xlsx }

public sealed record ExportRequest(
    string Sql,                                   // or table name resolved to SELECT *
    IReadOnlyDictionary<string, object?> Binds,   // bind variables
    ExportFormat Format,
    string DestinationPath,
    ExportOptions Options);

public sealed record ExportResult(long RowCount, long BytesWritten, TimeSpan Elapsed);
```

`ExportOptions` groups `CsvOptions` and `XlsxOptions` plus common settings (encoding, `IncludeHeaders`, `FlushEveryRows`, `DateFormat`, `NumberFormat`, `CultureName`).

## 4. `OracleRecordReader` (read core)

Responsibilities:
1. Create a connection with `OracleConnectionFactory` (pooling on).
2. Create the `OracleCommand`, set `FetchSize` (bytes) — key for streaming (see §04).
3. Execute `ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct)`.
4. Project `GetColumnSchema()` into `IReadOnlyList<ColumnSchema>`.

Sketch:

```csharp
public sealed class OracleRecordReader : IRecordReader
{
    private readonly OracleConnection _conn;
    private readonly OracleCommand _cmd;
    private readonly OracleDataReader _reader;
    public IReadOnlyList<ColumnSchema> Schema { get; }

    private OracleRecordReader(OracleConnection c, OracleCommand cmd, OracleDataReader r)
    { _conn = c; _cmd = cmd; _reader = r; Schema = BuildSchema(r); }

    public static async Task<OracleRecordReader> OpenAsync(
        OracleConnectionFactory factory, string sql,
        IReadOnlyDictionary<string, object?> binds, OracleOptions opt, CancellationToken ct)
    {
        var conn = await factory.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.FetchSize = opt.FetchSizeBytes;      // e.g. 1 MB. NOT the whole result.
        cmd.InitialLOBFetchSize = -1;            // stream LOBs if applicable
        foreach (var (k, v) in binds)
            cmd.Parameters.Add(new OracleParameter(k, v ?? DBNull.Value));
        cmd.BindByName = true;
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, ct);
        return new OracleRecordReader(conn, cmd, reader);
    }

    public ValueTask<bool> ReadAsync(CancellationToken ct) => new(_reader.ReadAsync(ct));
    public object? GetValue(int i) => _reader.IsDBNull(i) ? null : _reader.GetValue(i);
    public bool IsDBNull(int i) => _reader.IsDBNull(i);

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _cmd.DisposeAsync();
        await _conn.DisposeAsync();   // returns to the pool
    }
}
```

## 5. `CsvExportWriter`

- Wraps the destination `Stream` in a `StreamWriter` with configurable `bufferSize` and encoding (UTF-8 with/without BOM).
- Headers in `BeginAsync`.
- `WriteRow` iterates over the columns, applies RFC 4180 quoting (quotes if the value contains the delimiter, a quote character, or a line break; escapes `"` → `""`).
- Formats dates/numbers with a fixed `CultureInfo` (avoids locale surprises).
- `FlushAsync` → `StreamWriter.FlushAsync`.

## 6. `XlsxExportWriter` (MiniExcel)

MiniExcel writes XLSX in streaming mode, accepting an `IDataReader`/`IEnumerable`. Two approaches:

- **A (recommended):** adapt `IRecordReader` → `IDataReader` and pass it to `MiniExcel.SaveAsByIdataReader`/`SaveAs`, which writes row by row to the `Stream` without building the full OpenXML tree.
- **B (fine control):** expose a lazy `IEnumerable<IDictionary<string,object>>` (`yield return` per row) to `MiniExcel.SaveAs`. The `IEnumerable`'s laziness preserves streaming.

Format constraint: XLSX has a limit of **1,048,576 rows per sheet**. For larger results → sheet/file partitioning policy (see ADR-0005 and [04](./04-streaming-strategy.md) §6).

## 7. `ExportService`

```csharp
public async Task<ExportResult> ExecuteAsync(ExportRequest req, CancellationToken ct)
{
    _validator.Validate(req);
    var sw = Stopwatch.StartNew();
    await using var reader = await _readerFactory.OpenAsync(req, ct);

    await using var dest = _fs.CreateWrite(req.DestinationPath);   // FileStream buffer
    await using var writer = _writerFactory.Create(req.Format, dest, req.Options);

    await writer.BeginAsync(reader.Schema, ct);
    long n = 0;
    try
    {
        while (await reader.ReadAsync(ct))
        {
            writer.WriteRow(reader);
            if (++n % req.Options.FlushEveryRows == 0)
            {
                await writer.FlushAsync(ct);
                _progress.Report(n);
            }
        }
        await writer.EndAsync(ct);
    }
    catch (OperationCanceledException)
    {
        await _fs.DeletePartialAsync(req.DestinationPath);   // cleanup policy
        throw;
    }
    return new ExportResult(n, dest.Length, sw.Elapsed);
}
```

## 8. NuGet dependencies

| Package | Use |
|---------|-----|
| `Oracle.ManagedDataAccess.Core` | Managed Oracle driver |
| `MiniExcel` | Streaming XLSX |
| `System.CommandLine` | CLI |
| `Serilog` + `Serilog.Sinks.Console` / `.File` | Structured logging |
| `Microsoft.Extensions.Hosting` | Host, DI, config |
| `FluentValidation` (optional) | Request/profile validation |
| `xUnit`, `FluentAssertions`, `Testcontainers.Oracle` | Testing |
