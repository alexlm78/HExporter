# CLAUDE.md

Guide for agents (Claude Code) working in this repository.

## What it is

HExporter exports large-volume reports from **Oracle** or **PostgreSQL** to **CSV** or **XLSX** via **end-to-end streaming**, with memory **O(1) relative to row count**. Core project rule: never materialize the full result in memory.

## Golden rule (DO NOT break)

Pipeline: forward-only reader (`OracleDataReader` / `NpgsqlDataReader`, via the `IRecordReader` port) → **one live row** at a time → writer with bounded buffer + periodic flush → `Stream`. RAM consumption depends on row width and buffers, **not** on row count.

Forbidden (see `docs/04-streaming-strategy.md`):
- `DataTable` / `DataSet` / `reader.Load()`.
- `ToList()` / `ToArray()` on the result, or any row collection that grows unbounded.
- XLSX libraries that build the whole workbook in memory (ClosedXML, EPPlus). Use **MiniExcel** (streaming).
- Concatenating the whole CSV into a `string`/`StringBuilder`. Write to the `Stream` instead.
- Buffering full LOBs (use `InitialLOBFetchSize = -1`).

If a task seems to require breaking this, **stop and ask** — there's probably another way.

## Architecture

Lightweight clean architecture, ports and adapters. Dependency rule: outer layers depend on inner ones; `Core` depends on nothing.

```
Cli → Application → Core ← Infrastructure (Oracle/PostgreSQL)
                     Core ← Export (Csv/Xlsx)
```

| Project | Role | Depends on |
|----------|-----|-----------|
| `src/HExporter.Core` | Ports (`IRecordReader`, `IExportWriter`, `IExportWriterFactory`, `IRecordReaderFactory`, `IProgressSink`) + models. **No external dependencies.** | — |
| `src/HExporter.Application` | `ExportService` (orchestrates the pump), validation, profile loading, DI. | Core |
| `src/HExporter.Infrastructure` | Oracle/PostgreSQL adapters (`OracleRecordReader`, `PostgresRecordReader`, factories, `DatabaseEngineResolver`). Only one `IRecordReaderFactory` is registered per run, based on `Database:Engine` (default Oracle). | Core |
| `src/HExporter.Export` | Writers `CsvExportWriter`, `XlsxExportWriter` (MiniExcel), factory. | Core |
| `src/HExporter.Cli` | Entry point, `System.CommandLine`, host DI, Serilog. | all |
| `tools/HExporter.MemProbe` | Memory/volume test (synthetic reader, no DB). | Core, Application, Export |
| `tests/HExporter.UnitTests` | Writers, quoting, validation. | Core, Application, Export |
| `tests/HExporter.IntegrationTests` | Real Oracle (Testcontainers). | Core, Infrastructure |

The export flow lives in `ExportService.ExecuteAsync` (`src/HExporter.Application/ExportService.cs`) — it's the core; read it before touching the pipeline.

## Commands

```bash
dotnet build                                   # builds the solution (warnings = errors)
dotnet test tests/HExporter.UnitTests          # unit tests
dotnet run --project src/HExporter.Cli -- --help

# Memory test (project acceptance criterion)
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv
```

Example CLI:
```bash
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv
hexporter export --sql "SELECT * FROM ventas WHERE fecha >= :d" --bind d=2026-01-01 --format xlsx --out ventas.xlsx
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28
```

## Code conventions

- **.NET 10 (LTS)**, C# `latest`, defined in `Directory.Build.props` (don't repeat `TargetFramework` in each csproj).
- `Nullable=enable`, `ImplicitUsings=enable`, **`TreatWarningsAsErrors=true`** — the build fails on warnings. Keep it green.
- Async with `CancellationToken` propagated through every I/O path. No `.Result`/`.Wait()`.
- Type/member names in English. Code comments: Spanish (existing convention in most of the repo — follow the surrounding file's style). User-facing output (CLI help, console/log messages, exceptions) is English.
- **Fixed culture** (explicit `CultureInfo`) when formatting dates/numbers — never the host locale.

## Performance (hot path = per row)

`WriteRow` runs for every row. Avoid on that path: unnecessary allocations, `string.Format`/interpolation per cell, avoidable boxing. Optimizations (typed accessors, `ISpanFormattable.TryFormat`) are incremental — **measure with MemProbe/BenchmarkDotNet first**. They don't change the architecture.

## Security (non-negotiable)

- **Credentials:** never hardcode or log them. Resolve via env (`HEXPORTER_Oracle__ConnectionString`), User Secrets, or Oracle Wallet. See `docs/06-nfr-ops.md`.
- **SQL injection:** parameters always via **bind variables**. `--table` is validated against an identifier regex (`ExportRequestValidator.IsValidTableName`); never concatenate user values into SQL.
- **Logs:** never row content (possible PII). Metrics yes (rows, bytes, duration) + correlation `ExportId`.
- **Partial file:** written to `destination.tmp` and atomically renamed on completion; deleted on failure/cancellation. Never deliver truncated reports as valid.

## Known limits

- XLSX: max **1,048,576 rows/sheet**. `RowLimitStrategy=Fail` (default) aborts; `NewSheet` splits. Larger volumes → use CSV. Multi-file support deferred to v2 (`docs/adr/0005`).
- v1 is not resumable after an interruption: the report must be re-run.

## Documentation

Full design in `docs/` (00–08 + `docs/adr/`). Before an architecture change, read the relevant ADR and **update it/add one** if the decision changes. `docs/04-streaming-strategy.md` is required reading before touching the pipeline.

## Definition of "Done"

Green build (no warnings) · unit tests green · no regression in the memory test (MemProbe PASS) · docs/ADR updated if a decision changed.
