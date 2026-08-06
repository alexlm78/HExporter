# 02 — Architecture

## 1. Principles

1. **Streaming first.** Data flows row by row from Oracle to the file. The full set is never materialized.
2. **Bounded memory.** RAM usage depends on row width and buffer size, not on the number of rows.
3. **Separation of concerns.** Reading (Oracle), formatting (CSV/XLSX), and orchestration are independent and testable.
4. **Extensible by format.** Adding a new format = implement an interface, without touching the rest.
5. **Fail-safe.** Cooperative cancellation, resource limits, and cleanup of partial files.

## 2. Layer view (light clean architecture)

```
+-------------------------------------------------------------+
|  HExporter.Cli            (System.CommandLine, host, DI)     |  Presentation
+-------------------------------------------------------------+
|  HExporter.Application    (orchestration, ExportService,     |  Application
|                            profiles, validation)             |
+-------------------------------------------------------------+
|  HExporter.Core           (abstractions, models, ports:      |  Domain
|                            IRecordReader, IExportWriter)      |
+-------------------------------------------------------------+
|  HExporter.Infrastructure (OracleRecordReader,               |  Infrastructure
|   + HExporter.Export       CsvExportWriter, XlsxExportWriter) |
+-------------------------------------------------------------+
```

Dependency rule: outer layers depend on inner ones. `Core` depends on nothing (it defines the ports/interfaces). Oracle and the writers are **adapters** that implement those ports.

## 3. Components

| Component | Responsibility |
|------------|-----------------|
| `IRecordReader` | Forward-only read port. Exposes column metadata + row-by-row iteration. |
| `OracleRecordReader` | Oracle adapter: opens the connection, executes the command with `CommandBehavior.SequentialAccess`, tunes `FetchSize`, wraps `OracleDataReader`. |
| `IExportWriter` | Write port. Receives the schema and consumes rows incrementally. |
| `CsvExportWriter` | Writes CSV row by row with `StreamWriter` + buffer; handles quoting/escaping. |
| `XlsxExportWriter` | Writes XLSX in streaming mode with `MiniExcel` (does not build the workbook in RAM). |
| `ExportService` | Orchestrates: gets the reader, resolves the writer by format, pumps rows, reports progress, handles cancellation and errors. |
| `ReportProfile` | Declarative definition of a report (query, format, options). |
| `ExportWriterFactory` | Resolves the `IExportWriter` based on the requested format. |

## 4. Export flow (sequence)

```
CLI → ExportService.ExecuteAsync(request, ct)
  ├─ Validate request / load ReportProfile
  ├─ reader = OracleRecordReader.OpenAsync(sql, binds, ct)   // connection + FetchSize
  ├─ schema = reader.GetSchema()                             // column names/types
  ├─ writer = factory.Create(format, destinationStream)
  ├─ await writer.BeginAsync(schema, ct)                     // headers / sheet
  ├─ while (await reader.ReadAsync(ct))                      // forward-only
  │     writer.WriteRow(reader.CurrentRow)                   // no accumulation
  │     if (++n % FlushEvery == 0) writer.Flush(); progress.Report(n)
  ├─ await writer.EndAsync(ct)                               // close / final flush
  └─ return ExportResult(rows=n, bytes, elapsed)
```

Key points:
- The `while` loop processes **one live row at a time**. There is no growing list/collection.
- Periodic `Flush` pushes the buffer to the output `Stream` and keeps it from growing.
- Everything respects a `CancellationToken`.

## 5. Deployment model

- **Console executable**, self-contained or framework-dependent (`dotnet HExporter.Cli.dll ...`).
- Runs on the same host or in a worker. No state between runs.
- Output to: local disk path, network mount, or `stdout` (for piping).

## 6. Architecture decisions

See [adr/](./adr/):
- ADR-0001: .NET 8 + solution structure
- ADR-0002: Managed Oracle driver and server-side streaming
- ADR-0003: MiniExcel for streaming XLSX
- ADR-0004: Manual CSV writing vs. CsvHelper
- ADR-0005: Partitioning strategy (deferred to v2)
