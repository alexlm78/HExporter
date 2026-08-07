This file is a merged representation of a subset of the codebase, containing files not matching ignore patterns, combined into a single document by Repomix.

# File Summary

## Purpose
This file contains a packed representation of a subset of the repository's contents that is considered the most important context.
It is designed to be easily consumable by AI systems for analysis, code review,
or other automated processes.

## File Format
The content is organized as follows:
1. This summary section
2. Repository information
3. Directory structure
4. Repository files (if enabled)
5. Multiple file entries, each consisting of:
  a. A header with the file path (## File: path/to/file)
  b. The full contents of the file in a code block

## Usage Guidelines
- This file should be treated as read-only. Any changes should be made to the
  original repository files, not this packed version.
- When processing this file, use the file path to distinguish
  between different files in the repository.
- Be aware that this file may contain sensitive information. Handle it with
  the same level of security as you would the original repository.

## Notes
- Some files may have been excluded based on .gitignore rules and Repomix's configuration
- Binary files are not included in this packed representation. Please refer to the Repository Structure section for a complete list of file paths, including binary files
- Files matching these patterns are excluded: graphify-out
- Files matching patterns in .gitignore are excluded
- Files matching default ignore patterns are excluded
- Files are sorted by Git change count (files with more changes are at the bottom)

# Directory Structure
````
[HExporter]/
  .github/
    workflows/
      ci.yml
      release.yml
  docs/
    adr/
      0001-dotnet8-solution-structure.md
      0002-oracle-managed-streaming.md
      0003-xlsx-miniexcel.md
      0004-csv-manual-writer.md
      0005-partitioning-strategy.md
      0006-postgresql-engine-support.md
      README.md
    01-vision-scope.md
    02-architecture.md
    03-technical-design.md
    04-streaming-strategy.md
    05-configuration.md
    06-nfr-ops.md
    07-testing-strategy.md
    08-implementation-tasks.md
    09-tuning.md
    README.md
    STATUS.md
  reports/
    ventas.json
  scripts/
    seed_10m.sql
  src/
    HExporter.Application/
      Validation/
        ExportRequestValidator.cs
        ExportSecurityOptions.cs
      DependencyInjection.cs
      ExportService.cs
      HExporter.Application.csproj
      ReportProfileLoader.cs
    HExporter.Cli/
      appsettings.json
      ConsoleProgressSink.cs
      HExporter.Cli.csproj
      Program.cs
    HExporter.Core/
      Abstractions/
        IExportWriter.cs
        IExportWriterFactory.cs
        IProgressSink.cs
        IRecordReader.cs
        IRecordReaderFactory.cs
      Models/
        ColumnSchema.cs
        ExportFormat.cs
        ExportOptions.cs
        ExportRequest.cs
        ExportResult.cs
        ReportProfile.cs
      HExporter.Core.csproj
    HExporter.Export/
      Csv/
        CsvExportWriter.cs
      Xlsx/
        XlsxExportWriter.cs
      DependencyInjection.cs
      ExportWriterFactory.cs
      HExporter.Export.csproj
    HExporter.Infrastructure/
      Common/
        ConnectionRetryPolicyFactory.cs
      Oracle/
        OracleConnectionFactory.cs
        OracleOptions.cs
        OracleRecordReader.cs
        OracleRecordReaderFactory.cs
      Postgres/
        PostgresConnectionFactory.cs
        PostgresOptions.cs
        PostgresRecordReader.cs
        PostgresRecordReaderFactory.cs
      DatabaseEngine.cs
      DatabaseEngineResolver.cs
      DependencyInjection.cs
      HExporter.Infrastructure.csproj
  tests/
    HExporter.IntegrationTests/
      HExporter.IntegrationTests.csproj
      OracleFixture.cs
      OracleRecordReaderTests.cs
    HExporter.UnitTests/
      CsvExportWriterTests.cs
      DatabaseEngineResolverTests.cs
      ExportRequestValidatorTests.cs
      FakeRecordReader.cs
      HExporter.UnitTests.csproj
      OracleConnectionFactoryRetryTests.cs
      PostgresConnectionFactoryRetryTests.cs
      XlsxExportWriterTests.cs
  tools/
    HExporter.Benchmarks/
      ExportThroughputBenchmarks.cs
      HExporter.Benchmarks.csproj
      Program.cs
    HExporter.MemProbe/
      HExporter.MemProbe.csproj
      Program.cs
      README.md
      SyntheticRecordReader.cs
  .dockerignore
  .gitignore
  CLAUDE.md
  Directory.Build.props
  Dockerfile
  env.example
  HExporter.slnx
  README.md
````

# Files

## File: HExporter/.github/workflows/ci.yml
````yaml
name: CI

on:
  pull_request:
    branches: [ main ]
  push:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore HExporter.slnx

      - name: Build (warnings as errors)
        run: dotnet build HExporter.slnx --configuration Release --no-restore

      - name: Unit tests
        run: dotnet test tests/HExporter.UnitTests --configuration Release --no-build --logger "console;verbosity=normal"
````

## File: HExporter/.github/workflows/release.yml
````yaml
name: Release

on:
  push:
    tags:
      - 'v*.*.*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Tag a publicar (ej. v1.0.0). Solo aplica a runs manuales.'
        required: true
        default: 'v0.0.0-dev'

permissions:
  contents: write

jobs:
  build:
    name: Publish ${{ matrix.rid }}
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - rid: linux-x64
            os_name: linux
            arch: x86_64
            ext: tar.gz
          - rid: linux-arm64
            os_name: linux
            arch: arm64
            ext: tar.gz
          - rid: win-x64
            os_name: windows
            arch: x86_64
            ext: zip
          - rid: win-arm64
            os_name: windows
            arch: arm64
            ext: zip
          - rid: osx-x64
            os_name: macos
            arch: x86_64
            ext: tar.gz
          - rid: osx-arm64
            os_name: macos
            arch: arm64
            ext: tar.gz

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Version from tag
        id: version
        run: |
          TAG="${{ github.event_name == 'workflow_dispatch' && github.event.inputs.version || github.ref_name }}"
          echo "tag=$TAG" >> "$GITHUB_OUTPUT"
          echo "version=${TAG#v}" >> "$GITHUB_OUTPUT"

      - name: Restore
        run: dotnet restore HExporter.slnx

      - name: Publish (self-contained single-file, ${{ matrix.rid }})
        run: |
          dotnet publish src/HExporter.Cli \
            -c Release \
            -r ${{ matrix.rid }} \
            -p:PublishSingleFile=true \
            -p:Version=${{ steps.version.outputs.version }} \
            --self-contained true \
            -o publish/${{ matrix.rid }}

      - name: Package (tar.gz)
        if: matrix.ext == 'tar.gz'
        working-directory: publish/${{ matrix.rid }}
        run: tar -czf ../../hexporter-${{ steps.version.outputs.version }}-${{ matrix.os_name }}-${{ matrix.arch }}.tar.gz .

      - name: Package (zip)
        if: matrix.ext == 'zip'
        working-directory: publish/${{ matrix.rid }}
        run: zip -r ../../hexporter-${{ steps.version.outputs.version }}-${{ matrix.os_name }}-${{ matrix.arch }}.zip .

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: hexporter-${{ matrix.rid }}
          path: hexporter-${{ steps.version.outputs.version }}-${{ matrix.os_name }}-${{ matrix.arch }}.${{ matrix.ext }}
          retention-days: 7

  release:
    name: Create GitHub Release
    needs: build
    runs-on: ubuntu-latest
    steps:
      - name: Resolve tag
        id: tag
        run: |
          TAG="${{ github.event_name == 'workflow_dispatch' && github.event.inputs.version || github.ref_name }}"
          echo "tag=$TAG" >> "$GITHUB_OUTPUT"

      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: dist
          merge-multiple: true

      - name: Create release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: ${{ steps.tag.outputs.tag }}
          files: dist/*
          generate_release_notes: true
          draft: false
          prerelease: false
````

## File: HExporter/docs/adr/0001-dotnet8-solution-structure.md
````markdown
# ADR-0001 — .NET 10 and solution structure

**Status:** Accepted · **Date:** 2026-07-13
**Update:** The scaffold was generated on **.NET 10 (LTS)** — it's the current LTS (Nov 2025) and the only targeting pack available on the host (SDK 10.0.301). The design is identical for net8.0 if needed.

## Context

A batch export tool is required: cross-platform, with efficient streaming and a long support cycle.

## Decision

- **.NET 8 (LTS)**, C# 12, `Nullable=enable`.
- Layered structure (light clean architecture): `Core` (ports/models, no dependencies) ← `Application` ← `Infrastructure`/`Export` ← `Cli`.
- `Core` defines interfaces (`IRecordReader`, `IExportWriter`); Oracle and the writers are adapters.

## Consequences

- ✅ High testability (mockable ports), extensible by format without touching the core.
- ✅ LTS support until Nov 2026 (evaluate migration to .NET 10 LTS).
- ➖ Some project ceremony for a small tool; accepted for maintainability.

## Rejected alternatives

- Single monolithic project: simpler at first, worse separation and testability.
- .NET Framework 4.8: not cross-platform, no modern `Span`/async improvements.
````

## File: HExporter/docs/adr/0002-oracle-managed-streaming.md
````markdown
# ADR-0002 — Managed Oracle driver and server-side streaming reading

**Status:** Accepted · **Date:** 2026-07-13

## Context

The core requirement is to read huge results without loading them into memory. The choice of driver and read mode determines whether this is possible.

## Decision

- Use **`Oracle.ManagedDataAccess.Core`** (100% managed, no native Oracle client → cross-platform, simple deployment).
- Read with a **forward-only `OracleDataReader`** via `ExecuteReaderAsync(CommandBehavior.SequentialAccess)`.
- Tune **`FetchSize`** (bytes per network batch, ~256KB–1MB), **do not** load the full result.
- **Forbidden**: `DataTable`/`DataSet`/`Load()`.
- LOBs with `InitialLOBFetchSize=-1` to stream rather than buffer.

## Consequences

- ✅ Server-side cursor: the client only holds the current batch → O(1) memory in rows.
- ✅ No dependency on Oracle Instant Client.
- ➖ `FetchSize` requires tuning based on row shape; documented in [04](../04-streaming-strategy.md) and validated in E9.

## Rejected alternatives

- Non-managed ODP.NET: requires a native client, complicates deployment/containers.
- Dapper `Query<T>` without `buffered:false`: buffers everything by default. (With `buffered:false` it would be viable, but we prefer direct control of the reader.)
````

## File: HExporter/docs/adr/0003-xlsx-miniexcel.md
````markdown
# ADR-0003 — MiniExcel for streaming XLSX

**Status:** Accepted · **Date:** 2026-07-13

## Context

XLSX is a ZIP of XML. Most .NET libraries build the full document in memory before saving it, which breaks the bounded-memory requirement for large reports.

## Decision

Use **MiniExcel** for XLSX. It writes row by row to the output `Stream` from a lazy source (`IDataReader`/`IEnumerable`), without materializing the workbook in RAM.

## Consequences

- ✅ True streaming → bounded memory in XLSX too.
- ✅ Simple API, few dependencies.
- ➖ Fewer formatting features (advanced styles, formulas) than ClosedXML/EPPlus. Acceptable: the goal is exporting data, not layout.
- ➖ XLSX format limit: 1,048,576 rows/sheet → mitigation in ADR-0005 / [04](../04-streaming-strategy.md) §6.

## Rejected alternatives

- **ClosedXML:** builds the full workbook in memory → OOM with large volumes. Rejected.
- **EPPlus:** in normal mode buffers in memory; commercial license (Polyform) since v5. Rejected.
- **OpenXML SDK (`OpenXmlWriter`):** allows true streaming and is free, but has a low-level API (verbose, error-prone). Fallback alternative if MiniExcel isn't enough; documented as plan B.
````

## File: HExporter/docs/adr/0004-csv-manual-writer.md
````markdown
# ADR-0004 — Manual CSV writing vs. CsvHelper

**Status:** Accepted · **Date:** 2026-07-13

## Context

CSV is the primary format for massive volumes (no row limit). We need incremental writing, control over quoting/encoding, and maximum performance in the hot path.

## Decision

Write CSV with a **direct `StreamWriter`** + custom RFC 4180 quoting logic, in `CsvExportWriter`. CsvHelper is not adopted for writing.

## Consequences

- ✅ Full control of the hot path: no reflection, no per-object mapping, minimal allocations (enables `ISpanFormattable.TryFormat`).
- ✅ Zero extra dependency on the hottest path.
- ➖ We must implement and test the quoting/escaping ourselves (covered in E4/tests).

## Rejected alternatives

- **CsvHelper:** excellent for object↔CSV mapping, but geared toward typed records; unnecessary overhead for a raw-row `IRecordReader` pipeline. Kept as an option if complex mapping is needed.

## Note

The quoting logic is small and stable; the risk of implementing it ourselves is low compared to the performance and control benefits.
````

## File: HExporter/docs/adr/0005-partitioning-strategy.md
````markdown
# ADR-0005 — Partitioning strategy for massive volumes

**Status:** Deferred (v2) · **Date:** 2026-07-13

## Context

Two reasons to partition the output into multiple files/sheets:
1. **Hard XLSX limit:** 1,048,576 rows per sheet.
2. **Ergonomics/delivery:** very large files are hard to open/transfer; it may be worth splitting them (by size, row count, or key).

## Decision (v1)

- v1 supports partitioning **only at the XLSX sheet level** (`RowLimitStrategy=NewSheet`, optional) and defaults to `Fail` when the limit is exceeded.
- Multi-file partitioning (`reporte_0001.csv/xlsx`, `_0002`, …) is **deferred to v2**.

## Proposed design (v2)

- `PartitionStrategy`: `None` | `ByRowCount(n)` | `BySizeBytes(n)` | `ByKeyColumn(col)`.
- `ExportService` rotates the `Stream`/writer when crossing the threshold, preserving streaming.
- Zero-padded index naming + optional manifest (`manifest.json` with part count, rows per part, checksums).

## Consequences

- ✅ v1 stays simple and focused on the memory goal.
- ➖ XLSX reports > 1M rows must use CSV in v1 (documented in [04](../04-streaming-strategy.md) §6).
````

## File: HExporter/docs/adr/0006-postgresql-engine-support.md
````markdown
# ADR-0006 — Multi-engine support: PostgreSQL alongside Oracle

**Status:** Accepted · **Date:** 2026-08-06

## Context

HExporter was Oracle-only. A second engine was requested — PostgreSQL — selectable per run, defaulting to Oracle, without breaking the O(1)-memory streaming guarantee ([04](../04-streaming-strategy.md)) or the port/adapter boundary ([0001](./0001-dotnet8-solution-structure.md)).

## Decision

- **New adapter, same port.** `HExporter.Infrastructure/Postgres/` mirrors the Oracle adapter 1:1: `PostgresOptions`, `PostgresConnectionFactory`, `PostgresRecordReader`, `PostgresRecordReaderFactory`. Both implement `IRecordReader`/`IRecordReaderFactory` from `HExporter.Core` — **Core stays engine-agnostic**, no changes to its abstractions or to `ExportService`.
- **Driver:** `Npgsql` (100% managed, no native client — same rationale as ADR-0002 for Oracle). Npgsql's binary protocol already streams row-by-row without buffering the full resultset; `CommandBehavior.SequentialAccess` is still requested for parity and to avoid buffering large column values.
- **Engine selection happens once, at the composition root** (`Program.cs`, before `IHost` is built) via `DatabaseEngineResolver.Resolve(cliValue, configuredValue)`. Only **one** `IRecordReaderFactory` implementation is registered per run — there is no per-row or per-request dispatch, no `if (engine == ...)` branching inside `ExportService` or the writers.
- **Selection source:** new `--db-engine oracle|postgres` CLI flag, `Database:Engine` config key (bindable via `HEXPORTER_Database__Engine` env var / `.env` / `appsettings.json`). Precedence follows the existing project-wide rule ([05](../05-configuration.md) §2): CLI > real env var > `.env` > `appsettings.json`. Default: `Oracle` (backward compatible — no config change needed for existing users).
- **Fail fast on invalid input:** an unrecognized `--db-engine`/`Database:Engine` value throws `ArgumentException` immediately (exit code 1) listing valid values. A missing `Postgres:ConnectionString` when the engine is Postgres throws the same way Oracle does today.
- **Shared retry policy.** Both connection factories now build their Polly retry pipeline through `Infrastructure/Common/ConnectionRetryPolicyFactory` (generic over the driver's exception type) instead of duplicating the pipeline-construction code — extracted while adding the second provider, since the logic was identical.
- **Table-name validation** (`ExportRequestValidator.IsValidTableName`, used by `--table`) is **not** engine-specific: it keeps the single Oracle-shaped identifier regex for both engines. It's a superset of valid PostgreSQL unquoted identifiers (accepts `$`/`#`, which Postgres doesn't produce but also doesn't reject as input to `SELECT * FROM <table>`), so it remains a safe anti-injection gate without needing a second regex.

## Consequences

- ✅ Adding a third engine later follows the same recipe: new `Infrastructure/<Engine>/` adapter + one `DatabaseEngine` enum value + one switch arm in `DependencyInjection.AddHExporterDatabase`. No Core/Application/Export changes.
- ✅ No runtime cost for Oracle-only users: the Postgres adapter is only instantiated (never even touched) when `Database:Engine=Postgres`.
- ➖ `Postgres` has no `FetchSizeBytes`-equivalent tunable (unlike Oracle) — Npgsql doesn't expose a batch-size knob the same way; its streaming behavior isn't user-tunable in v1.
- ➖ Bind-variable placeholder syntax in `--sql`/`--sql-file`/`report.json` is engine-native (`:name` for Oracle, `@name` for Npgsql) — HExporter does not rewrite SQL, so a query written for one engine isn't portable to the other without edits.

## Rejected alternatives

- **Provider-agnostic abstraction (e.g., Dapper across both drivers):** would still require a buffered/non-buffered decision per provider and adds an indirection layer for no streaming benefit — `IRecordReader` already *is* that abstraction at the right level.
- **Runtime per-request engine switching:** rejected — a single CLI invocation exports from one database; adding request-level engine dispatch would complicate `ExportService` for a capability nobody asked for.
````

## File: HExporter/docs/adr/README.md
````markdown
# Architecture Decision Records (ADR)

Record of significant architecture decisions. Format: context → decision → consequences.

| ADR | Title | Status |
|-----|--------|--------|
| [0001](./0001-dotnet8-solution-structure.md) | .NET 8 and solution structure (light clean architecture) | Accepted |
| [0002](./0002-oracle-managed-streaming.md) | Managed Oracle driver and server-side streaming reading | Accepted |
| [0003](./0003-xlsx-miniexcel.md) | MiniExcel for streaming XLSX | Accepted |
| [0004](./0004-csv-manual-writer.md) | Manual CSV writing vs. CsvHelper | Accepted |
| [0005](./0005-partitioning-strategy.md) | Partitioning strategy for massive volumes | Deferred (v2) |
| [0006](./0006-postgresql-engine-support.md) | Multi-engine support: PostgreSQL alongside Oracle | Accepted |
````

## File: HExporter/docs/01-vision-scope.md
````markdown
# 01 — Vision and Scope

## 1. Problem

Current reports are generated by loading the entire result of an Oracle query into memory (e.g. `DataTable`, object lists, or Excel libraries that build the whole workbook in RAM). With high volumes (millions of rows) this causes:

- Memory usage proportional to report size → **OutOfMemoryException**.
- Long garbage collector (GC) pauses → the application "freezes".
- Intermittent failures that are hard to reproduce, depending on the day's data size.

## 2. Vision

A tool that exports any Oracle query/table to CSV or XLSX with **constant memory**, suitable for running on modest servers, in scheduled batch jobs, or invoked on demand.

## 3. Scope (In scope)

- Export the result of a **SQL query** or **table/view** to **CSV** and **XLSX**.
- **End-to-end streaming**: server-side reading + incremental writing to disk/stream.
- Query parameters (bind variables) to filter the report.
- Format configuration: CSV delimiter, encoding, headers, sheet name, date/number formats.
- Execution via **CLI** with parameters and via **report definition file** (reusable profile).
- Cooperative cancellation, progress reporting, and structured logging.
- Error handling and connection retries.

## 4. Out of scope — v1

- Complex transformations / joins that should be resolved outside SQL (delegated to SQL).
- Chart generation or advanced conditional formatting in XLSX.
- Graphical interface (GUI/web). CLI only in v1 (extensible to Worker/API later).
- Writing to destination databases (files only).
- Automatic multi-file partitioning (v2 candidate, see ADR-0005).

## 5. Actors

| Actor | Description |
|-------|-------------|
| **Operator / Analyst** | Runs the export with parameters from the CLI. |
| **Scheduler** (cron / Task Scheduler / Airflow) | Invokes the CLI on a schedule. |
| **DBA** | Provides credentials, `FetchSize` tuning, and validates queries. |

## 6. Main use cases

1. **UC-01 Export table to CSV.** The operator specifies table/query, CSV format, and destination path; the system produces the file via streaming.
2. **UC-02 Export parameterized query to XLSX.** Same as UC-01 but with bind variables and XLSX output.
3. **UC-03 Run report by profile.** The operator specifies a profile (`report.json`) that defines the query, format, and options.
4. **UC-04 Cancel a running export.** `Ctrl+C` cancels cooperatively; the partial file is marked/deleted per policy.

## 7. Success criteria

- Export **10M rows × 20 columns** with stable **process memory < 300 MB**.
- Target throughput ≥ **50k rows/sec** for CSV (network/DB dependent) — see [06-nfr-ops.md](./06-nfr-ops.md).
- Zero `OutOfMemoryException` regardless of result size.
````

## File: HExporter/docs/02-architecture.md
````markdown
# 02 — Architecture

## 1. Principles

1. **Streaming first.** Data flows row by row from the source database (Oracle or PostgreSQL) to the file. The full set is never materialized.
2. **Bounded memory.** RAM usage depends on row width and buffer size, not on the number of rows.
3. **Separation of concerns.** Reading (Oracle/PostgreSQL), formatting (CSV/XLSX), and orchestration are independent and testable.
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
|   + HExporter.Export       PostgresRecordReader,              |
|                             CsvExportWriter, XlsxExportWriter) |
+-------------------------------------------------------------+
```

Dependency rule: outer layers depend on inner ones. `Core` depends on nothing (it defines the ports/interfaces). Oracle, PostgreSQL, and the writers are **adapters** that implement those ports. Only one `IRecordReader` implementation is wired per run, selected by `Database:Engine` ([ADR-0006](./adr/0006-postgresql-engine-support.md)).

## 3. Components

| Component | Responsibility |
|------------|-----------------|
| `IRecordReader` | Forward-only read port. Exposes column metadata + row-by-row iteration. |
| `OracleRecordReader` | Oracle adapter: opens the connection, executes the command with `CommandBehavior.SequentialAccess`, tunes `FetchSize`, wraps `OracleDataReader`. |
| `PostgresRecordReader` | PostgreSQL adapter: opens the connection, executes the command with `CommandBehavior.SequentialAccess`, wraps `NpgsqlDataReader` (Npgsql streams row-by-row natively). |
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
````

## File: HExporter/docs/03-technical-design.md
````markdown
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
````

## File: HExporter/docs/04-streaming-strategy.md
````markdown
# 04 — Memory-Safe Streaming Strategy (core)

This is the critical document: it explains **why** the design does not blow up memory and **what** to do / not do.

## 1. Central principle

The pipeline maintains, at all times, **a single live row** plus a **bounded output buffer**:

```
Oracle (server-side cursor)
   → FetchSize buffer (driver, N bytes)      ← bounded
   → 1 row in the CLR (the current one)      ← O(1)
   → StreamWriter/MiniExcel buffer           ← bounded
   → FileStream buffer                        ← bounded
   → Disk
```

Memory usage is **O(row_width + buffers)**, NOT **O(number_of_rows)**. It doesn't matter whether the report has 1,000 or 1,000,000,000 rows.

## 2. Hard rules (what is NEVER done)

1. ❌ **No** `DataTable` / `DataSet` / `reader.Load()`.
2. ❌ **No** `ToList()` / `ToArray()` on the result, nor row buffers that grow unbounded.
3. ❌ **No** XLSX libraries that build the full workbook in memory (ClosedXML, EPPlus in normal mode). See ADR-0003.
4. ❌ **No** giant concatenated `string`s (`StringBuilder` for the entire CSV). It is written to the `Stream`.
5. ❌ **No** loading complete LOBs when they can be streamed (`InitialLOBFetchSize = -1`).

## 3. Oracle side: reading server-side

- `OracleDataReader` is **forward-only** and fetches rows in batches according to `FetchSize`.
- **`FetchSize`** (in bytes) controls how much the driver fetches per network round trip. Typical value: **256 KB – 1 MB**.
  - Too low → many network round trips, slow.
  - Too high → more RAM per batch and higher first-row latency. **Do not** load everything.
- `CommandBehavior.SequentialAccess`: allows processing columns in order and streaming LOBs without fully buffering them.
- The cursor lives on the Oracle server; the client only holds the current batch.

Rough calculation: `FetchSize` ÷ `bytes_per_row` ≈ rows per batch. E.g.: 1 MB ÷ 200 B ≈ ~5,000 rows per network round trip, released as it advances.

## 4. Write side: buffer + periodic flush

- `StreamWriter`/MiniExcel accumulate into a small buffer and flush it to the `FileStream`.
- **Flush every N rows** (`FlushEveryRows`, default 10,000) to prevent the buffer from growing and for partial durability.
- `FileStream` with a reasonable `bufferSize` (e.g. 64–128 KB) and `useAsync: true`.
- Consider `FileOptions.SequentialScan` for sequential I/O.

## 5. Avoiding per-row allocations (optional, high volume)

When volume justifies it:
- Typed accessors on `IRecordReader` (`GetInt64`, `GetDecimal`, `GetString`) → avoid boxing of `object`.
- Reuse a `char[]`/`Span<char>` buffer to format numbers/dates (`ISpanFormattable.TryFormat`) in the CSV writer.
- Avoid `string.Format`/interpolation per cell in the hot path.

These optimizations are **incremental** and do not change the architecture; measure before applying them (see [07](./07-testing-strategy.md)).

## 6. XLSX row limit and partitioning

XLSX allows a max. of **1,048,576 rows/sheet**. Strategies (config `XlsxOptions.RowLimitStrategy`):

| Strategy | Behavior |
|-----------|----------------|
| `Fail` (default) | Aborts if the limit is exceeded. Safe by default. |
| `NewSheet` | On reaching the limit, creates `Datos_2`, `Datos_3`, … in the same file. |
| `NewFile` | Generates `reporte_0001.xlsx`, `reporte_0002.xlsx`, … (v2, ADR-0005). |

For volumes that greatly exceed the limit, **CSV is the natural choice** (no row limit).

`NewSheet` is implemented in `XlsxExportWriter` without breaking streaming: MiniExcel decides on multi-sheet output if the `value` passed to `SaveAs` implements `IDictionary<string, object>`, and `GetSheets()` internally only calls `GetEnumerator()` (never `.Count`/`.Keys`/indexer). This is exploited with a "fake" `IDictionary<string, object>` whose only real member is the enumerator, fed by a `BlockingCollection<KeyValuePair<string, object>>` (one sheet at a time, capacity 1) — each sheet is in turn another `BlockingCollection` of rows. On reaching `MaxRowsPerSheet`, the current sheet's row queue is closed and the next sheet is added to the outer queue. Memory remains bounded by the queue capacities, not by the number of sheets/rows.

## 7. Memory pressure and verification

- Configure `<ServerGarbageCollection>true</ServerGarbageCollection>` for high-throughput batch jobs; evaluate `<ConcurrentGarbageCollection>`.
- Mandatory smoke test: export a synthetic dataset of **≥ 10M rows** and verify the working set stays flat (see [07](./07-testing-strategy.md) §4).
- Acceptance metric: memory stable and independent of the number of rows.

## 8. Backpressure

The pump is synchronous with respect to writing: if the disk is slower than the DB, the `while` loop blocks on `FlushAsync`/`WriteRow`, which naturally throttles reading (the driver stops requesting the next batch). No intermediate queue is needed. If reading/writing are parallelized in the future, use a **bounded** `Channel<T>` (`BoundedChannelOptions` with a fixed capacity) to preserve backpressure.
````

## File: HExporter/docs/05-configuration.md
````markdown
# 05 — Configuration and CLI

## 1. `appsettings.json`

```json
{
  "Database": {
    "Engine": "Oracle"
  },
  "Oracle": {
    "ConnectionStringName": "Reporting",
    "FetchSizeBytes": 1048576,
    "CommandTimeoutSeconds": 0,
    "BindByName": true,
    "ConnectRetryAttempts": 3,
    "ConnectRetryBaseDelaySeconds": 2.0
  },
  "Postgres": {
    "ConnectionString": "",
    "CommandTimeoutSeconds": 0,
    "ConnectRetryAttempts": 3,
    "ConnectRetryBaseDelaySeconds": 2.0
  },
  "Export": {
    "DefaultEncoding": "utf-8",
    "FlushEveryRows": 10000,
    "FileBufferBytes": 131072
  },
  "Csv": {
    "Delimiter": ",",
    "IncludeHeaders": true,
    "WriteBom": false,
    "QuoteMode": "Minimal",
    "DateFormat": "yyyy-MM-dd HH:mm:ss",
    "Culture": "en-US"
  },
  "Xlsx": {
    "SheetName": "Datos",
    "IncludeHeaders": true,
    "RowLimitStrategy": "Fail"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/hexporter-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

`Oracle.ConnectRetryAttempts` / `Postgres.ConnectRetryAttempts` (Polly): retries with exponential backoff on a transient failure while **opening** the connection (listener down, network). `0` = disabled. Does not retry on cancellation (Ctrl+C) or configuration errors (empty connection string).

### 1.1 Database engine selection

`Database:Engine` selects the active adapter: `Oracle` (default) or `Postgres`. Only one `IRecordReaderFactory` is wired per run — resolved once at startup, before the DI container is built. Precedence (highest wins): `--db-engine` CLI flag > real process env var (`HEXPORTER_Database__Engine`) > `[dot]env` file > `appsettings.json`. An unrecognized value throws immediately (exit code 1) listing the valid options. When `Postgres` is selected, `Oracle:*` settings are simply unused (and vice versa) — no cross-validation between sections.

## 2. Secrets / credentials

**Never** hardcode the connection string or commit a `[dot]env` file with real credentials (see `env.example` at the repo root, a template without secrets). Precedence order (highest wins):

1. **CLI** — one-off run parameters (`--sql`, `--table`, `--bind`, `--format`, `--out`, etc.).
2. **Real process environment variables** — `HEXPORTER_Oracle__ConnectionString`, `HEXPORTER_Oracle__FetchSizeBytes`, etc. (`HEXPORTER_` prefix, `__` as hierarchical separator).
3. **`[dot]env` file** — loaded by `HExporter.Cli` at startup (`DotNetEnv`) if present in the current directory, or at the path given by `--env-file <path>`. If a variable already exists in the process's real environment, the file **does not override it** (this preserves precedence order 2 > 3).
4. **`appsettings.json`** — non-sensitive defaults (`FetchSizeBytes`, `CommandTimeoutSeconds`, etc.).

`--env-file` with a nonexistent path is an **argument error** (exit code 1); without the option, the file is optional — its absence does not fail, it simply contributes no values.

In production, prefer **Oracle Wallet** (external authentication, no plaintext password) injected via real environment variables from the orchestrator (Kubernetes Secret, Vault, etc.), not a `[dot]env` file on disk. See [06-nfr-ops.md](./06-nfr-ops.md) §Security.

## 3. Report profile (`report.json`)

Reusable declarative definition of a report:

```json
{
  "name": "ventas_mensuales",
  "sql": "SELECT id, fecha, monto, cliente FROM ventas WHERE fecha BETWEEN :desde AND :hasta",
  "binds": { "desde": "2026-01-01", "hasta": "2026-01-31" },
  "format": "xlsx",
  "csv": { "delimiter": ";" },
  "xlsx": { "sheetName": "Ventas" }
}
```

`binds` can be overridden on the CLI (`--bind desde=2026-02-01`).

## 4. CLI interface

```
hexporter export [options]

Options:
  --sql <text>             SELECT query to export. (mutually exclusive with --table and --profile)
  --table <owner.table>    Exports the full table/view (SELECT *).
  --profile <path>         Path to a report.json.
  --format <csv|xlsx>      Output format. (default: csv)
  --out <path>             Destination file. '-' = stdout (csv only).
  --bind <k=v>             Bind variable (repeatable). E.g.: --bind desde=2026-01-01
  --delimiter <char>       CSV delimiter. (default: ,)
  --no-headers             Skips the header row.
  --encoding <name>        utf-8 | utf-8-bom | latin1. (default: utf-8)
  --flush-every <n>        Rows between flushes. (default: 10000)
  --fetch-size <bytes>     Oracle driver FetchSize. (default: 1048576)
  --sheet <name>           XLSX sheet name. (default: Data)
  --env-file <path>        Alternate [dot]env file (default: [dot]env in current directory, optional).
  --db-engine <name>       oracle | postgres. (default: oracle; see §1.1)
  -v, --verbose            Verbose logging.

Exit codes:
  0  Success
  1  Validation / argument error
  2  Oracle connection / SQL error
  3  Write / I/O error
  130 Cancelled by the user (Ctrl+C)
```

### Examples

```bash
# Full table to CSV
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv

# Parameterized query to XLSX
hexporter export \
  --sql "SELECT * FROM ventas WHERE fecha >= :d" \
  --bind d=2026-01-01 --format xlsx --out ventas.xlsx --sheet Ventas

# By profile, overriding a bind
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28

# To stdout, piped with gzip
hexporter export --table LOGS --format csv --out - | gzip > logs.csv.gz
```
````

## File: HExporter/docs/06-nfr-ops.md
````markdown
# 06 — Non-Functional Requirements and Operations

## 1. Performance

| Metric | Target |
|---------|----------|
| Process memory (working set) | < 300 MB, **stable** and independent of row count |
| CSV throughput | ≥ 50,000 rows/sec (network/DB/disk dependent) |
| XLSX throughput | ≥ 20,000 rows/sec (format overhead) |
| Time to first row (TTFB) | < 2 s with default `FetchSize` |

Tuning levers: `FetchSizeBytes`, `FlushEveryRows`, `FileBufferBytes`, server GC, output disk (SSD/local vs. network mount).

## 2. Reliability

- **Cooperative cancellation** via `CancellationToken` (Ctrl+C → `PosixSignalRegistration`/`Console.CancelKeyPress`).
- **Partial file policy:** write to `destino.tmp` and atomically rename on completion (`File.Move`); on failure/cancellation, delete the `.tmp` file. Prevents delivering truncated reports as valid.
- **Connection retries:** retry policy (Polly) only on connection **opening**; once streaming has started, a disconnection aborts and the full report must be retried (not resumable in v1).
- **Timeouts:** `CommandTimeout = 0` (no limit) for long reports, configurable; connection timeout is bounded.

## 3. Security

- **Credentials:** never in plain text or in logs. Prefer **Oracle Wallet** / external authentication in production. Secrets via environment variables or Key Vault.
- **SQL Injection:** **always** use bind variables for parameters. `--table` is validated against an `owner.object` pattern (identifiers) and quoted with an app-side `DBMS_ASSERT` equivalent; never concatenate user values into SQL.
- **Least privilege:** the Oracle account must have only `SELECT` on the required objects.
- **PII / sensitive data:** reports may contain personal data. Define output file permissions (0600), a controlled write location, and a retention/deletion policy. Do not log row values.
- **Path traversal:** validate/normalize `--out` against an allowed base directory when running in a multi-user context. → `ExportSecurity:AllowedOutputDirectory` (`HEXPORTER_ExportSecurity__AllowedOutputDirectory`); undefined = no restriction (normal single-user CLI usage).

## 4. Observability

- **Structured logging (Serilog):** export start/end, row count, bytes, duration, throughput, exit code. **Never** row content.
- **Progress:** reported every `FlushEveryRows` to the console (`stderr`, to avoid polluting `stdout` when `--out -`).
- **Metrics (optional):** expose counters (rows/sec, memory) via `System.Diagnostics.Metrics` / OpenTelemetry if integrated with a collector.
- **Correlation:** `ExportId` (GUID) per run in all logs.

## 5. Portability / Deployment

- Cross-platform (.NET 8): Linux/Windows/macOS. `Oracle.ManagedDataAccess.Core` is 100% managed (no native Oracle client).
- Packaging: framework-dependent (requires .NET 8 runtime) or self-contained single-file for hosts without a runtime.
- Container: base image `mcr.microsoft.com/dotnet/runtime:8.0`.

## 6. Maintainability

- Ports/adapters → adding a format = new `IExportWriter` class + factory registration.
- Centralized, typed configuration (`IOptions<T>`).
- Target test coverage ≥ 80% in `Core`/`Application`.

## 7. Known limits (v1)

- Not resumable after a disconnect (the report is re-run).
- XLSX limited to 1,048,576 rows/sheet (mitigation in [04](./04-streaming-strategy.md) §6).
- No partition parallelization (v2).
````

## File: HExporter/docs/07-testing-strategy.md
````markdown
# 07 — Testing Strategy

## 1. Levels

| Level | Scope | Tools |
|-------|---------|--------------|
| Unit | CSV/XLSX writers, validation, quoting, formatting, factory | xUnit, FluentAssertions |
| Integration | `OracleRecordReader` against real Oracle | Testcontainers.Oracle (gvenzl/oracle-free) |
| End-to-end | Full CLI: SQL → file, content verification | CLI process + assertions on file |
| Performance / memory | High volume, flat memory | BenchmarkDotNet + working set measurement |

## 2. Unit — key cases

**CSV:**
- RFC 4180 quoting: value with delimiter, with quotes (`"` → `""`), with line break.
- NULL → empty cell.
- Dates/numbers with fixed `CultureInfo` (independent of host locale).
- Encoding with/without BOM.
- `--no-headers` does not write a header row.

**XLSX:**
- Headers present/absent.
- Types: number, date, text, NULL correct in cells.
- `RowLimitStrategy=Fail` aborts when exceeding 1,048,576.

**Reader (with a double/fake):** correct schema, `ReadAsync` advances, `IsDBNull`.

## 3. Integration (real Oracle)

- Spin up an Oracle Free container, seed a table with a known dataset.
- Verify: mapped schema, bind variables, Oracle types (NUMBER, DATE, TIMESTAMP, VARCHAR2, CLOB) → CLR.
- Verify CLOB streaming with `SequentialAccess`.

## 4. Memory test (mandatory — validates the project's goal)

Goal: demonstrate **O(1)** memory relative to rows.

Procedure:
1. Generate a synthetic dataset of **10M+ rows** (via `CONNECT BY LEVEL` or a seeded table).
2. Run export to CSV and to XLSX.
3. Sample working set / GC heap during the run (dotnet-counters).
4. **Acceptance criterion:** stable memory; no monotonic growth with row count; no `OutOfMemoryException`.

```sql
-- Synthetic dataset generator for testing
SELECT LEVEL id,
       SYSDATE - LEVEL fecha,
       DBMS_RANDOM.VALUE(1,10000) monto,
       'cliente_' || LEVEL nombre
FROM dual CONNECT BY LEVEL <= 10000000;
```

## 5. Performance (BenchmarkDotNet)

- Measure rows/sec per format while varying `FetchSizeBytes` and `FlushEveryRows`.
- Compare boxed vs. typed accessors (justifies the optimization in [04](./04-streaming-strategy.md) §5).
- Record a baseline to detect regressions in CI.

## 6. CI

- `dotnet test` (unit) on every PR.
- Integration with Testcontainers in a nightly pipeline (slower).
- Memory test as a manual/nightly job with trend reporting.
````

## File: HExporter/docs/08-implementation-tasks.md
````markdown
# 08 — Implementation Backlog

Epics → stories → tasks. Estimated in points (S=1, M=3, L=5, XL=8). Mark `[ ]` / `[x]`.

Delivery priority: **E1 → E2 → E3 → E4** form the MVP (CSV functional end-to-end). E5–E8 harden and complete it.

---

## E1 — Solution scaffolding and CI  (M)

- [x] **T1.1** Create solution and projects: `Core`, `Application`, `Infrastructure`, `Export`, `Cli`, `UnitTests`, `IntegrationTests`. (S)
- [x] **T1.2** Configure `Directory.Build.props`: .NET 10 (current LTS; see ADR-0001), `Nullable=enable`, `TreatWarningsAsErrors`. (S)
- [x] **T1.3** Project references per the dependency rule (ADR-0001). (S)
- [x] **T1.4** CI pipeline: `dotnet build` + `dotnet test` on PR. → `.github/workflows/ci.yml` (Release build + unit tests), verified locally. Repo not yet initialized as `.git`; the workflow will run on push to GitHub. (M)
- [x] **T1.5** Generic `Host` + DI + `appsettings.json` loading + `IOptions<T>`. (M)

## E2 — Ports and models (Core)  (M)

- [x] **T2.1** `ColumnSchema`, `ExportFormat`, `ExportRequest`, `ExportResult`, `ExportOptions`. (S)
- [x] **T2.2** `IRecordReader` interface (+ optional typed accessors). (S)
- [x] **T2.3** `IExportWriter` and `IExportWriterFactory` interfaces. (S)
- [x] **T2.4** `IProgressSink` and progress model. (S)
- [x] **T2.5** `ReportProfile` (declarative profile model). (S)

## E3 — Streaming Oracle reading (Infrastructure)  (L)

- [x] **T3.1** `OracleOptions` + config binding. (S)
- [x] **T3.2** `OracleConnectionFactory` with pooling. (M)
- [x] **T3.3** `OracleRecordReader.OpenAsync`: command, `FetchSize`, `SequentialAccess`, bind vars. (L)
- [x] **T3.4** `GetColumnSchema()` → `ColumnSchema` mapping (Oracle→CLR types). (M)
- [x] **T3.5** LOB streaming (`InitialLOBFetchSize=-1`). (M)
- [x] **T3.6** Integration tests with Testcontainers.Oracle. → `tests/HExporter.IntegrationTests` (`OracleFixture` + `OracleRecordReaderTests`), `gvenzl/oracle-free:slim-faststart` container via podman (`DOCKER_HOST`/`TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` already point to the podman VM). Covers: schema/values end-to-end, bind variables, and a small `FetchSizeBytes` forcing several round trips without losing/duplicating rows. Two non-obvious findings documented as comments in `OracleFixture.cs`: (1) the library's default wait strategy (searching for a message in the logs) clashes with podman — Docker.DotNet throws "Invalid chunk header" reading logs through its socket; replaced with a port-wait + real connection retry; (2) don't use `.WithDatabase(...)`: gvenzl/oracle-free already creates the "FREEPDB1" PDB by default, requesting one with the same name triggers `ORA-65012` and aborts startup — the connection string is built by hand against that PDB instead of `GetConnectionString()` (which assumes the "XE" service from oracle-xe). Also, the podman VM needed to go from 2GiB to 6GiB of RAM (`podman machine set --memory 6144`) — Oracle Free does not start reliably with less. (L)

## E4 — CSV Writer (Export)  (L)

- [x] **T4.1** `CsvOptions` (delimiter, headers, BOM, culture, formats). (S)
- [x] **T4.2** `CsvExportWriter`: `StreamWriter` + buffer, headers, `WriteRow`, flush. (L)
- [x] **T4.3** RFC 4180 quoting/escaping. (M)
- [x] **T4.4** Date/number formatting with fixed `CultureInfo`; NULL→empty. (M)
- [x] **T4.5** Unit tests for quoting, types, encoding, headers. → `tests/HExporter.UnitTests/CsvExportWriterTests.cs` (6 cases). (M)

## E5 — XLSX Writer (Export)  (L)

- [x] **T5.1** Integrate `MiniExcel`; `XlsxOptions` (sheet, headers, RowLimitStrategy). (M)
- [x] **T5.2** Adapt `IRecordReader` → lazy source (IDataReader/IEnumerable). → push→pull bridge via a bounded `BlockingCollection`. (L)
- [x] **T5.3** Streaming `XlsxExportWriter` (Begin/WriteRow/End). (L)
- [x] **T5.4** Enforce the 1,048,576-row limit + `Fail`/`NewSheet` strategy. → `Fail` verified (10M XLSX probe aborts correctly); `NewSheet` implemented in `XlsxExportWriter` exploiting MiniExcel's lazy multi-sheet mode (custom `IDictionary<string,object>`, streaming, see docs/04-streaming-strategy.md §6). 2 new tests in `XlsxExportWriterTests` (multi-sheet rollover + `Fail` still throws). (M)
- [x] **T5.5** Unit tests for types/cells/headers/limit. → `tests/HExporter.UnitTests/XlsxExportWriterTests.cs` (real roundtrip). (M)

## E6 — Orchestration (Application)  (M)

- [x] **T6.1** `ExportWriterFactory` (resolves writer by format). (S)
- [x] **T6.2** `ExportService.ExecuteAsync`: pumping, periodic flush, progress. (L)
- [x] **T6.3** Write to `.tmp` + atomic rename; cleanup of partial file on failure/cancellation. (M)
- [x] **T6.4** `ReportProfileLoader` (load/merge profile + bind overrides). (M)
- [x] **T6.5** `ExportRequestValidator` (SQL vs table vs profile; validate `--table`). → anti-injection regex verified against an injection attempt. (M)

## E7 — CLI (Cli)  (M)

- [x] **T7.1** `System.CommandLine`: `export` command with all options ([05](./05-configuration.md) §4). (L)
- [x] **T7.2** Map args → `ExportRequest`; precedence CLI > real env > `[dot]env` > `appsettings.json`. → see [05-configuration.md](./05-configuration.md) §2 and `--env-file`. (M)
- [x] **T7.3** Cancellation (Ctrl+C) → `CancellationToken`. (S)
- [x] **T7.4** Exit codes (0/1/2/3/130) and progress output to `stderr`. → verified: validation=1, connection=2, missing `--env-file`=1. (S)
- [x] **T7.5** `--out -` support (stdout, CSV only). (S)

## E8 — Observability, security and hardening  (M)

- [x] **T8.1** Serilog: console + rolling file; correlation `ExportId`. (M)
- [x] **T8.2** Export metrics logging (rows, bytes, throughput); NEVER row data. (S)
- [x] **T8.3** Secret resolution: real env > `[dot]env` file (`DotNetEnv`, optional, configurable `--env-file`) > `appsettings.json`. Secret-free `env.example` template; `.env` in `.gitignore`. Wallet/Key Vault for prod still pending. (M)
- [x] **T8.4** Connection-open retries (Polly). → `OracleConnectionFactory` with `ResiliencePipeline` (exponential backoff), configurable `Oracle:ConnectRetryAttempts`/`ConnectRetryBaseDelaySeconds`, `0` disables it. 2 unit tests (`OracleConnectionFactoryRetryTests.cs`). (M)
- [x] **T8.5** Anti-injection validation for `--table` / path traversal for `--out`. → `ExportRequestValidator.IsValidTableName` (identifier regex) + optional `ExportSecurity:AllowedOutputDirectory` (defense in depth, `Path.GetFullPath` + containment check); 5 unit tests in `ExportRequestValidatorTests.cs`. (M)

## E9 — Volume and performance testing  (M)  *(acceptance gate)*

- [x] **T9.1** Synthetic 10M+ row dataset generator. → `tools/HExporter.MemProbe` (`SyntheticRecordReader`) + `scripts/seed_10m.sql` (real Oracle). (S)
- [x] **T9.2** Flat memory test — **project acceptance criterion**. → MemProbe samples working set/GC. **Verified: 10M CSV, peak WS ~126 MB, flat memory. PASS.** (M)
- [x] **T9.3** Benchmarks (BenchmarkDotNet) varying FetchSize/FlushEvery. → `tools/HExporter.Benchmarks` (`ExportThroughputBenchmarks`, synthetic reader + real writers, no Oracle); varies `FlushEveryRows`/`FileBufferBytes`. `FetchSizeBytes` requires real Oracle — remains a guideline without its own benchmark (not an environment blocker: T3.6 confirmed podman does run real Oracle here). (M)
- [x] **T9.4** Document recommended tuning values. → `docs/09-tuning.md`. (S)

## E10 — Packaging and delivery  (S)

- [x] **T10.1** Framework-dependent + self-contained single-file publishing. → `dotnet publish` commands documented in README; `HExporter.Cli.csproj` with `SelfContained`/`IncludeNativeLibrariesForSelfExtract` conditioned on `PublishSingleFile=true`. No trimming (`Oracle.ManagedDataAccess.Core` is not trim-safe). (M)
- [x] **T10.2** Dockerfile (net10.0 runtime — corrected from "8.0" to the project's actual version). → multi-stage sdk→runtime, framework-dependent, `.dockerignore`. Verified with `podman build -t hexporter:test .` (successful build) and `podman run --rm hexporter:test --help` (entrypoint responds correctly). (S)
- [x] **T10.3** Usage README + examples. → "Packaging and distribution" section (publish/Docker). (S)

---

## Critical path (MVP CSV)

```
T1.1 → T1.5 → T2.* → T3.3 → T4.2 → T6.2 → T7.1 → (functional CSV export)
```

XLSX (E5) and hardening (E8/E9) continue in parallel after the MVP.

## Definition of "Done" (DoD)

- Code with nullable enabled, no warnings.
- Unit tests green; coverage ≥ 80% in Core/Application.
- No regression in the memory test (T9.2).
- Documentation/ADR updated if a decision changed.
````

## File: HExporter/docs/09-tuning.md
````markdown
# 09 — Tuning

Recommended values for `ExportOptions` and the benchmarks backing them (T9.3/T9.4).

## How to run the benchmarks

```bash
dotnet run -c Release --project tools/HExporter.Benchmarks -- --filter '*'
```

`tools/HExporter.Benchmarks/ExportThroughputBenchmarks.cs` measures the full pipeline
(`ExportService.ExecuteAsync`) with `SyntheticRecordReader` (200,000 rows, no Oracle)
→ real CSV/XLSX → disk. Varies `FlushEveryRows` (1,000/10,000/100,000) and
`FileBufferBytes` (64 KB/1 MB).

## Results (Apple M1 Pro, macOS, .NET 10, Release, 200,000 rows)

| Format | FlushEveryRows | FileBufferBytes | Mean  | Allocated memory |
|---------|----------------|------------------|--------|-------------------|
| CSV     | 1,000          | 64 KB            | 156 ms | 59 MB             |
| CSV     | 10,000         | 64 KB            | 107 ms | 59 MB             |
| CSV     | 100,000        | 64 KB            | 106 ms | 59 MB             |
| CSV     | 10,000         | 1 MB             | 114 ms | 65 MB             |
| CSV     | 100,000        | 1 MB             | 103 ms | 65 MB             |
| XLSX    | 1,000          | 64 KB            | 606 ms | 417 MB            |
| XLSX    | 10,000         | 64 KB            | 584 ms | 416 MB            |
| XLSX    | 100,000        | 1 MB             | 582 ms | 418 MB            |

(Full table of 12 combinations in the BenchmarkDotNet output; the omitted values
fall within the same noise range.)

## Conclusions

1. **`FlushEveryRows` has no observable effect on throughput at this volume.** CSV's
   `StreamWriter` already buffers internally (`FileBufferBytes`); the periodic
   `FlushAsync` only forces an early flush to disk — at 200k rows its cost is lost
   in the noise. Its real value is bounding how much could be lost on a disconnect
   (memory/time since the last flush), not speeding up the export. The default
   `10_000` is reasonable; no need to tune it unless a different trade-off is wanted
   between "how much is lost on disconnect" and flush syscall overhead.
   `FlushAsync` in `XlsxExportWriter` is a no-op (`ValueTask.CompletedTask`) —
   `FlushEveryRows` has **no effect at all** on XLSX; it only triggers `progress.Report`.
2. **`FileBufferBytes` has a marginal effect in this range (64 KB vs 1 MB).** Raising
   the `StreamWriter`/`FileStream` buffer reduces write syscalls, but at this volume
   the OS already smooths this out via the page cache. The default `128 * 1024` (128 KB) is
   fine; only raise it when exporting very large files (hundreds of millions of
   rows) over high-latency storage (network/NFS).
3. **XLSX is ~5x slower and ~7x more memory-costly than CSV for the same
   volume** (per row: OOXML generation — styles, shared strings, zip
   compression — via MiniExcel, not the streaming bridge in `XlsxExportWriter`). Prefer
   **CSV** when the consumer accepts it; use XLSX only when the output format
   requires it. This is not an O(1) memory problem — XLSX memory usage
   remains flat with respect to row count (see `docs/04-streaming-strategy.md`),
   it just has a higher per-row constant.

## Out of scope for this benchmark: `Oracle:FetchSizeBytes`

`FetchSizeBytes` (`OracleRecordReader`, see `docs/05-configuration.md`) only has
an effect with a real Oracle listener (it bounds the network batch per cursor round trip).
It cannot be measured with the synthetic reader. Without an Oracle instance available in this
environment (same blocker as T3.6 — Testcontainers requires Docker, unavailable here),
it remains a configuration guideline without its own benchmark:

- Default `1 MiB` is a reasonable starting point (balance between network
  round trips and fetch buffer memory).
- High-latency networks to the listener → raising `FetchSizeBytes` reduces round trips.
- Very wide rows (many columns / LOBs) → lower `FetchSizeBytes` if per-batch
  latency becomes noticeable, since each batch must complete before the
  first row is released to the pipeline.
- Revisit with `tools/HExporter.MemProbe` + real Oracle once an environment with
  Docker/Testcontainers is available (pending in T3.6).
````

## File: HExporter/docs/README.md
````markdown
# HExporter — Architecture Documentation

Large-volume report exporter from **Oracle** tables to **CSV** or **XLSX** files, writing **directly from the database to the file via streaming**, without materializing the full result in memory.

## Design goal

Process reports with millions of rows with **constant and bounded** memory usage (independent of the result size), preventing the machine from freezing or crashing due to memory pressure (OOM / GC pauses).

## Document index

| # | Document | Content |
|---|-----------|-----------|
| 00 | [README.md](./README.md) | This index |
| 01 | [01-vision-scope.md](./01-vision-scope.md) | Vision, scope, actors, use cases |
| 02 | [02-architecture.md](./02-architecture.md) | Architecture, layers, components, flow |
| 03 | [03-technical-design.md](./03-technical-design.md) | Detailed technical design, interfaces, classes |
| 04 | [04-streaming-strategy.md](./04-streaming-strategy.md) | Memory-safe streaming strategy (core) |
| 05 | [05-configuration.md](./05-configuration.md) | Configuration, `appsettings`, secrets, CLI |
| 06 | [06-nfr-ops.md](./06-nfr-ops.md) | Non-functional requirements, security, observability |
| 07 | [07-testing-strategy.md](./07-testing-strategy.md) | Testing strategy |
| 08 | [08-implementation-tasks.md](./08-implementation-tasks.md) | Backlog: epics, stories and tasks |
| — | [adr/](./adr/) | Architecture Decision Records |

## Technology stack (summary)

- **Runtime:** .NET 10 (LTS), C# latest _(the generated scaffold targets `net10.0`; the installed SDK is 10.0.301)_
- **Oracle driver:** `Oracle.ManagedDataAccess.Core`
- **CSV:** direct `StreamWriter` (row-by-row writing)
- **XLSX:** `MiniExcel` (true streaming, low memory)
- **CLI:** `System.CommandLine`
- **Logging:** `Serilog`
- **DI/Host:** `Microsoft.Extensions.Hosting`

## Golden rule

> Never load the full result into memory. The pipeline is `OracleDataReader` (forward-only, server-side) → per-row transformation → output `Stream` with a bounded buffer and periodic `flush`. Memory is O(1) relative to the number of rows.
````

## File: HExporter/docs/STATUS.md
````markdown
# STATUS — project resume snapshot

> Generated 2026-07-14. Read this before touching code if you're a new agent joining the session.
> Architecture/rules context: `CLAUDE.md` (root). Full backlog: `docs/08-implementation-tasks.md`.

## What HExporter is

Exports large reports from Oracle to CSV/XLSX via streaming, O(1) memory relative to row count. See `CLAUDE.md` for the golden rule (do not break: no `DataTable`, `ToList()`, full workbook in memory, etc.).

## Backlog status

**All 10 epics (E1–E10) in `docs/08-implementation-tasks.md` are complete** — every task marked `[x]`. The CSV MVP, XLSX, CLI, observability/security, volume testing (MemProbe PASS, 10M rows ~126MB peak WS) and packaging (publish + Docker) are done and verified. No backlog remains pending from that document.

## Build / test / verification

- Build: `dotnet build` (or `dotnet build HExporter.slnx`) — **0 warnings, 0 errors** (`TreatWarningsAsErrors=true`, non-negotiable).
- Unit tests: `dotnet test tests/HExporter.UnitTests` — **16/16 passed** (last run: 2026-07-14).
- Integration tests (real Oracle via Testcontainers + podman): `dotnet test tests/HExporter.IntegrationTests` — **3/3 passed** (~8.8s). Requires podman running (`DOCKER_HOST`/`TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` pointing at the podman VM, already configured in this environment). VM needs ≥6GiB RAM (`podman machine set --memory 6144`) — Oracle Free doesn't start reliably with 2GiB.
- Docker: `podman build -t hexporter:test .` + `podman run --rm hexporter:test --help` — verified working.

## Recent work (this session, outside the original backlog)

1. **T3.6 completed** (`tests/HExporter.IntegrationTests/OracleFixture.cs` + `OracleRecordReaderTests.cs`): integration tests against real Oracle (`gvenzl/oracle-free`). Two gotchas documented as comments in `OracleFixture.cs`:
   - Testcontainers.Oracle's default wait strategy (log-based) clashes with podman → replaced with a port-wait + real connection retry.
   - `.WithDatabase("FREEPDB1")` collides with the PDB that gvenzl/oracle-free already creates by default (`ORA-65012`) → connection string built by hand against the real PDB instead of using the library's `GetConnectionString()`.
2. **`--sql-file` in the CLI** (`src/HExporter.Cli/Program.cs`): allows passing a `.sql` file instead of `--table`/`--sql` inline, for long queries. Source priority: `--profile` > `--table` > `--sql-file` > `--sql`. Passing `--sql` and `--sql-file` together = error (exit 1).
3. **Optimization of `kardex-gnc.sql`** (repo root, not part of HExporter's source code — it's a business query that gets exported *with* HExporter): the original query had an O(n²) pattern (2 correlated subqueries with `GROUP BY` per row for `INICIAL`/`SALDO_FINAL`) + duplicated subqueries in a `CASE` (up to 4x the same lookup per row). Rewritten using window functions (`SUM() OVER (...RANGE...)`) and `LEFT JOIN`s with a `DOC_TYPE` guard. Original backed up in `kardex-gnc.original.sql` for result comparison. **Still pending real verification against Oracle** (row count + `INICIAL`/`SALDO_FINAL` totals matching between both versions) — could not be run here due to lack of access to that specific database. Recommended index (not applied, it's DDL on the user's Oracle):
   ```sql
   CREATE INDEX idx_delta_prism_kardex
     ON REPORTUSER.DELTA_PRISM (ITEM_SID, STORE_NO, REVERSION, CREATED_DATETIME);
   ```

## Git status

Current branch, not pushed to `origin/main` beyond what's already synced. Working tree has 2 untracked files (`kardex-gnc.sql`, `kardex-gnc.original.sql`) — these are the user's business artifacts, not part of the project's source code; decide whether they go into `.gitignore` or get committed separately.

Latest commits (most recent first):
```
1b9efbe feat(cli): add --sql-file option to load export queries from a file
d8ef9b7 test(integration): add Oracle integration tests via Testcontainers
cfbbe98 feat(packaging): add publish, Dockerfile and docs for distribution (E10)
c834ada feat(tools): add BenchmarkDotNet suite and tuning doc
9172b9f feat(export): implement NewSheet strategy for XLSX row-limit overflow
e5c1e76 feat(infra): add Polly retry policy for Oracle connection open
9da5ec0 feat: scaffold HExporter streaming export pipeline with CI and hardening
```

## Working convention with the user (important for the next agent)

- **Never commit without explicit permission.** Only draft conventional-commit messages (English) for the user to apply.
- Keep the build green (0 warnings) and unit tests passing at every step.
- Update `docs/08-implementation-tasks.md` (checkboxes + notes) if anything from the original backlog is touched.
- Architecture changes → review/update the relevant ADR in `docs/adr/`.

## What's left / possible next steps

- Verify the rewritten `kardex-gnc.sql` against the user's real Oracle (could not be done this session).
- Decide the fate of `kardex-gnc*.sql` in the repo (gitignore vs. `reports/` folder vs. commit).
- The formal backlog (`docs/08-implementation-tasks.md`) has no pending items — any new work is ad-hoc (like the CLI flag and the query) and not pre-planned in that document.
````

## File: HExporter/reports/ventas.json
````json
{
  "name": "ventas_mensuales",
  "sql": "SELECT id, fecha, monto, cliente FROM ventas WHERE fecha BETWEEN :desde AND :hasta",
  "binds": { "desde": "2026-01-01", "hasta": "2026-01-31" },
  "format": "xlsx",
  "xlsx": { "sheetName": "Ventas" }
}
````

## File: HExporter/scripts/seed_10m.sql
````sql
-- seed_10m.sql — dataset sintético para prueba de volumen/memoria contra Oracle real.
-- Ver docs/07-testing-strategy.md §4.
-- Uso: ejecutar en SQL*Plus / SQLcl con la cuenta de pruebas.

-- 1) Tabla destino
CREATE TABLE hexporter_stress (
    id       NUMBER        NOT NULL,
    fecha    DATE          NOT NULL,
    monto    NUMBER(12,2)  NOT NULL,
    cliente  VARCHAR2(64)  NOT NULL
);

-- 2) Sembrar 10M filas por lotes de 1M (evita un solo INSERT gigante / undo enorme).
--    CONNECT BY LEVEL genera las filas del lote; el offset desplaza el id.
BEGIN
    FOR lote IN 0 .. 9 LOOP
        INSERT /*+ APPEND */ INTO hexporter_stress (id, fecha, monto, cliente)
        SELECT lote * 1000000 + LEVEL,
               DATE '2000-01-01' + MOD(LEVEL, 3650),
               ROUND(DBMS_RANDOM.VALUE(1, 10000), 2),
               'cliente_' || (lote * 1000000 + LEVEL)
        FROM   dual
        CONNECT BY LEVEL <= 1000000;
        COMMIT;
    END LOOP;
END;
/

-- 3) Estadísticas (mejora los planes durante el export)
BEGIN
    DBMS_STATS.GATHER_TABLE_STATS(USER, 'HEXPORTER_STRESS');
END;
/

-- 4) Verificación
SELECT COUNT(*) AS filas FROM hexporter_stress;

-- Limpieza (cuando termine la prueba):
-- DROP TABLE hexporter_stress PURGE;
````

## File: HExporter/src/HExporter.Application/Validation/ExportRequestValidator.cs
````csharp
using System.Text.RegularExpressions;
using HExporter.Core.Models;
using Microsoft.Extensions.Options;

namespace HExporter.Application.Validation;

public sealed partial class ExportRequestValidator(IOptions<ExportSecurityOptions> securityOptions)
{
    // owner.objeto — identificadores Oracle válidos. Anti-injection para --table.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,29}(\.[A-Za-z][A-Za-z0-9_$#]{0,29})?$")]
    private static partial Regex TableNameRegex();

    public void Validate(ExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sql))
            throw new ArgumentException("The SQL query is required.");
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
            throw new ArgumentException("The destination path is required.");
        if (request.Format == ExportFormat.Xlsx && request.DestinationPath == "-")
            throw new ArgumentException("XLSX does not support stdout output ('-'). Use CSV.");
        if (request.DestinationPath.Contains('\0'))
            throw new ArgumentException("Invalid destination path.");

        ValidateOutputBoundary(request.DestinationPath);
    }

    /// <summary>Path traversal: si hay un directorio base configurado (ExportSecurity:AllowedOutputDirectory),
    /// --out debe resolver dentro de él. Ver docs/06-nfr-ops.md §Seguridad.</summary>
    private void ValidateOutputBoundary(string destinationPath)
    {
        string? allowedDir = securityOptions.Value.AllowedOutputDirectory;
        if (allowedDir is null || destinationPath == "-")
            return;

        string baseDir = Path.GetFullPath(allowedDir);
        string fullPath = Path.GetFullPath(destinationPath, baseDir);
        string baseDirWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar) ? baseDir : baseDir + Path.DirectorySeparatorChar;

        if (fullPath != baseDir && !fullPath.StartsWith(baseDirWithSep, StringComparison.Ordinal))
            throw new ArgumentException($"The destination path must be inside {allowedDir}.");
    }

    /// <summary>Valida un identificador de tabla/vista antes de construir SELECT *.</summary>
    public static bool IsValidTableName(string name) => TableNameRegex().IsMatch(name);
}
````

## File: HExporter/src/HExporter.Application/Validation/ExportSecurityOptions.cs
````csharp
namespace HExporter.Application.Validation;

public sealed class ExportSecurityOptions
{
    public const string SectionName = "ExportSecurity";

    /// <summary>
    /// Si se define, `--out` debe resolver dentro de este directorio (defensa en profundidad
    /// contra path traversal cuando el ejecutable corre en contexto multiusuario/servicio).
    /// Null (por defecto) = sin restricción, para uso normal de CLI de un solo usuario.
    /// Ver docs/06-nfr-ops.md §Seguridad.
    /// </summary>
    public string? AllowedOutputDirectory { get; init; }
}
````

## File: HExporter/src/HExporter.Application/DependencyInjection.cs
````csharp
using HExporter.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HExporter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHExporterApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ExportSecurityOptions>(config.GetSection(ExportSecurityOptions.SectionName));
        services.AddSingleton<ExportRequestValidator>();
        services.AddSingleton<ReportProfileLoader>();
        services.AddSingleton<ExportService>();
        return services;
    }
}
````

## File: HExporter/src/HExporter.Application/ExportService.cs
````csharp
using System.Diagnostics;
using HExporter.Application.Validation;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using Microsoft.Extensions.Logging;

namespace HExporter.Application;

/// <summary>
/// Orquesta la exportación: reader forward-only → writer incremental.
/// Bombea UNA fila viva a la vez. Flush periódico. Escribe a .tmp y renombra atómico.
/// </summary>
public sealed class ExportService
{
    private readonly IRecordReaderFactory _readerFactory;
    private readonly IExportWriterFactory _writerFactory;
    private readonly ExportRequestValidator _validator;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        IRecordReaderFactory readerFactory,
        IExportWriterFactory writerFactory,
        ExportRequestValidator validator,
        ILogger<ExportService> logger)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ExportResult> ExecuteAsync(
        ExportRequest request, IProgressSink? progress, CancellationToken ct)
    {
        _validator.Validate(request);
        progress ??= NullProgressSink.Instance;

        bool toStdout = request.DestinationPath == "-";
        string finalPath = request.DestinationPath;
        string writePath = toStdout ? finalPath : finalPath + ".tmp";
        var exportId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();
        long rows = 0;
        long bytes = 0;

        _logger.LogInformation("Export {ExportId} started. Format={Format} Destination={Dest}",
            exportId, request.Format, finalPath);

        Stream destination = toStdout
            ? Console.OpenStandardOutput()
            : new FileStream(writePath, FileMode.Create, FileAccess.Write, FileShare.None,
                request.Options.FileBufferBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);

        try
        {
            await using var reader = await _readerFactory.OpenAsync(request, ct);
            await using (var writer = _writerFactory.Create(request.Format, destination, request.Options))
            {
                await writer.BeginAsync(reader.Schema, ct);
                while (await reader.ReadAsync(ct))
                {
                    writer.WriteRow(reader);
                    if (++rows % request.Options.FlushEveryRows == 0)
                    {
                        await writer.FlushAsync(ct);
                        progress.Report(rows);
                    }
                }
                await writer.EndAsync(ct);
            }

            bytes = destination.CanSeek ? destination.Length : 0;
        }
        catch (Exception ex)
        {
            await destination.DisposeAsync();
            if (!toStdout) TryDeletePartial(writePath);
            if (ex is OperationCanceledException)
                _logger.LogWarning("Export {ExportId} cancelled after {Rows} rows.", exportId, rows);
            else
                _logger.LogError(ex, "Export {ExportId} failed after {Rows} rows.", exportId, rows);
            throw;
        }

        await destination.DisposeAsync();

        if (!toStdout)
        {
            File.Move(writePath, finalPath, overwrite: true); // atomic rename
            bytes = new FileInfo(finalPath).Length;
        }

        sw.Stop();
        var result = new ExportResult(rows, bytes, sw.Elapsed);
        _logger.LogInformation(
            "Export {ExportId} completed. Rows={Rows} Bytes={Bytes} Duration={Elapsed} ({Rps:N0} rows/s)",
            exportId, result.RowCount, result.BytesWritten, result.Elapsed, result.RowsPerSecond);
        return result;
    }

    private void TryDeletePartial(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete partial file {Path}", path); }
    }
}
````

## File: HExporter/src/HExporter.Application/HExporter.Application.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HExporter.Core\HExporter.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.9" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

## File: HExporter/src/HExporter.Application/ReportProfileLoader.cs
````csharp
using System.Text.Json;
using HExporter.Core.Models;

namespace HExporter.Application;

public sealed class ReportProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<ReportProfile> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Report profile not found: {path}");
        await using var fs = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<ReportProfile>(fs, JsonOptions, ct)
                      ?? throw new InvalidOperationException($"Invalid profile: {path}");
        return profile;
    }
}
````

## File: HExporter/src/HExporter.Cli/appsettings.json
````json
{
  "Database": {
    "Engine": "Oracle"
  },
  "Oracle": {
    "ConnectionString": "",
    "FetchSizeBytes": 1048576,
    "CommandTimeoutSeconds": 0,
    "BindByName": true,
    "ConnectRetryAttempts": 3,
    "ConnectRetryBaseDelaySeconds": 2.0
  },
  "Postgres": {
    "ConnectionString": "",
    "CommandTimeoutSeconds": 0,
    "ConnectRetryAttempts": 3,
    "ConnectRetryBaseDelaySeconds": 2.0
  },
  "ExportSecurity": {
    "AllowedOutputDirectory": null
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": { "path": "logs/hexporter-.log", "rollingInterval": "Day" }
      }
    ]
  }
}
````

## File: HExporter/src/HExporter.Cli/ConsoleProgressSink.cs
````csharp
using HExporter.Core.Abstractions;

namespace HExporter.Cli;

/// <summary>Reporta progreso a stderr (no contamina stdout cuando --out -).</summary>
public sealed class ConsoleProgressSink : IProgressSink
{
    public void Report(long rowsWritten)
        => Console.Error.Write($"\r  {rowsWritten:N0} rows...");
}
````

## File: HExporter/src/HExporter.Cli/HExporter.Cli.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HExporter.Application\HExporter.Application.csproj" />
    <ProjectReference Include="..\HExporter.Infrastructure\HExporter.Infrastructure.csproj" />
    <ProjectReference Include="..\HExporter.Export\HExporter.Export.csproj" />
    <ProjectReference Include="..\HExporter.Core\HExporter.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="DotNetEnv" Version="3.2.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.9" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="10.0.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.1.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
    <PackageReference Include="System.CommandLine" Version="3.0.0-preview.5.26302.115" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>HExporter.Cli</RootNamespace>
    <AssemblyName>hexporter</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <None Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <!-- Solo aplica con `dotnet publish -p:PublishSingleFile=true -r <RID>` (T10.1).
       Sin PublishTrimmed: Oracle.ManagedDataAccess.Core usa reflection y no es trim-safe. -->
  <PropertyGroup Condition="'$(PublishSingleFile)' == 'true'">
    <SelfContained>true</SelfContained>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>

</Project>
````

## File: HExporter/src/HExporter.Cli/Program.cs
````csharp
using System.CommandLine;
using HExporter.Application;
using HExporter.Cli;
using HExporter.Core.Models;
using HExporter.Export;
using HExporter.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using DotNetEnv;

// ---- CLI options ----
var sqlOpt = new Option<string?>("--sql") { Description = "SELECT query to export." };
var sqlFileOpt = new Option<string?>("--sql-file") { Description = "Path to a .sql file with the query to export." };
var tableOpt = new Option<string?>("--table") { Description = "Table/view to export (SELECT *)." };
var profileOpt = new Option<string?>("--profile") { Description = "Path to a report.json." };
var formatOpt = new Option<ExportFormat>("--format") { Description = "csv | xlsx.", DefaultValueFactory = _ => ExportFormat.Csv };
var outOpt = new Option<string?>("--out") { Description = "Destination file ('-' = stdout, CSV only)." };
var bindOpt = new Option<string[]>("--bind") { Description = "Bind variable k=v (repeatable).", AllowMultipleArgumentsPerToken = true };
var delimiterOpt = new Option<string>("--delimiter") { Description = "CSV delimiter.", DefaultValueFactory = _ => "," };
var noHeadersOpt = new Option<bool>("--no-headers") { Description = "Omit headers." };
var sheetOpt = new Option<string>("--sheet") { Description = "XLSX sheet name.", DefaultValueFactory = _ => "Data" };
var flushOpt = new Option<int>("--flush-every") { Description = "Rows between flushes.", DefaultValueFactory = _ => 10_000 };
var envFileOpt = new Option<string?>("--env-file") { Description = "Path to an alternate .env file (default: .env in the current directory)." };
var dbEngineOpt = new Option<string?>("--db-engine") { Description = "oracle | postgres. Overrides HEXPORTER_Database__Engine / appsettings.json (default: oracle)." };

var root = new RootCommand("HExporter — streams Oracle/PostgreSQL tables/queries to CSV/XLSX.");
foreach (var o in new Option[] { sqlOpt, sqlFileOpt, tableOpt, profileOpt, formatOpt, outOpt, bindOpt, delimiterOpt, noHeadersOpt, sheetOpt, flushOpt, envFileOpt, dbEngineOpt })
    root.Options.Add(o);

if (args.Length == 0) args = ["--help"];

root.SetAction(async (parse, ct) =>
{
    try
    {
        var host = BuildHost(parse.GetValue(envFileOpt), parse.GetValue(dbEngineOpt));
        var loader = host.Services.GetRequiredService<ReportProfileLoader>();
        var exporter = host.Services.GetRequiredService<ExportService>();

        string? sql = parse.GetValue(sqlOpt);
        string? sqlFile = parse.GetValue(sqlFileOpt);
        string? table = parse.GetValue(tableOpt);
        string? profilePath = parse.GetValue(profileOpt);
        var format = parse.GetValue(formatOpt);
        var binds = ParseBinds(parse.GetValue(bindOpt) ?? Array.Empty<string>());

        // Resolve source -> final SQL
        if (profilePath is not null)
        {
            var profile = await loader.LoadAsync(profilePath, ct);
            sql ??= profile.Sql;
            format = profile.Format;
            foreach (var (k, v) in profile.Binds)
                binds.TryAdd(k, v);
        }
        else if (table is not null)
        {
            if (!HExporter.Application.Validation.ExportRequestValidator.IsValidTableName(table))
            {
                Console.Error.WriteLine($"Invalid table name: {table}");
                return 1;
            }
            sql = $"SELECT * FROM {table}";
        }
        else if (sqlFile is not null)
        {
            if (sql is not null)
            {
                Console.Error.WriteLine("Use --sql or --sql-file, not both.");
                return 1;
            }
            if (!File.Exists(sqlFile))
            {
                Console.Error.WriteLine($"--sql-file file not found: {sqlFile}");
                return 1;
            }
            sql = await File.ReadAllTextAsync(sqlFile, ct);
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            Console.Error.WriteLine("Specify --sql, --sql-file, --table, or --profile.");
            return 1;
        }

        string outPath = parse.GetValue(outOpt) ?? $"export.{(format == ExportFormat.Xlsx ? "xlsx" : "csv")}";

        var options = new ExportOptions
        {
            IncludeHeaders = !parse.GetValue(noHeadersOpt),
            FlushEveryRows = parse.GetValue(flushOpt),
            Csv = new CsvOptions { Delimiter = parse.GetValue(delimiterOpt) ?? "," },
            Xlsx = new XlsxOptions { SheetName = parse.GetValue(sheetOpt) ?? "Data" }
        };

        var request = new ExportRequest(sql!, binds, format, outPath, options);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var result = await exporter.ExecuteAsync(request, new ConsoleProgressSink(), cts.Token);
        Console.Error.WriteLine();
        Console.Error.WriteLine($"OK: {result.RowCount:N0} rows, {result.BytesWritten:N0} bytes, {result.Elapsed}.");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("\nCancelled.");
        return 130;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nError: {ex.Message}");
        return ex is ArgumentException or FileNotFoundException ? 1 : 2;
    }
    finally
    {
        Log.CloseAndFlush();
    }
});

return await root.Parse(args).InvokeAsync();

// ---- Helpers ----
static IHost BuildHost(string? envFilePath, string? dbEngineOverride)
{
    // Precedence (lowest to highest): appsettings.json < .env < real env vars < CLI.
    // DotNetEnv does not overwrite variables already present in the process: if HEXPORTER_... already exists, it wins.
    LoadDotEnv(envFilePath);

    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.AddEnvironmentVariables("HEXPORTER_");

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    var engine = DatabaseEngineResolver.Resolve(
        dbEngineOverride, builder.Configuration[DatabaseEngineResolver.ConfigKey]);
    builder.Services.AddHExporterDatabase(builder.Configuration, engine);
    builder.Services.AddHExporterWriters();
    builder.Services.AddHExporterApplication(builder.Configuration);
    return builder.Build();
}

static void LoadDotEnv(string? envFilePath)
{
    string path = envFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(path))
    {
        if (envFilePath is not null)
            throw new FileNotFoundException($".env file not found: {envFilePath}");
        return; // .env is optional by default — configuration can come solely from real env vars.
    }
    Env.Load(path); // does not overwrite variables already present in the process
}

static Dictionary<string, object?> ParseBinds(string[] pairs)
{
    var d = new Dictionary<string, object?>();
    foreach (var p in pairs)
    {
        int eq = p.IndexOf('=');
        if (eq <= 0) throw new ArgumentException($"Invalid bind (expected k=v): {p}");
        d[p[..eq]] = p[(eq + 1)..];
    }
    return d;
}
````

## File: HExporter/src/HExporter.Core/Abstractions/IExportWriter.cs
````csharp
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
````

## File: HExporter/src/HExporter.Core/Abstractions/IExportWriterFactory.cs
````csharp
using HExporter.Core.Models;

namespace HExporter.Core.Abstractions;

public interface IExportWriterFactory
{
    /// <summary>Crea el writer del formato indicado, escribiendo al stream destino.</summary>
    IExportWriter Create(ExportFormat format, Stream destination, ExportOptions options);
}
````

## File: HExporter/src/HExporter.Core/Abstractions/IProgressSink.cs
````csharp
namespace HExporter.Core.Abstractions;

/// <summary>Recibe reportes de progreso (nº de filas escritas hasta el momento).</summary>
public interface IProgressSink
{
    void Report(long rowsWritten);
}

/// <summary>Sink nulo por defecto.</summary>
public sealed class NullProgressSink : IProgressSink
{
    public static readonly NullProgressSink Instance = new();
    public void Report(long rowsWritten) { }
}
````

## File: HExporter/src/HExporter.Core/Abstractions/IRecordReader.cs
````csharp
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
````

## File: HExporter/src/HExporter.Core/Abstractions/IRecordReaderFactory.cs
````csharp
using HExporter.Core.Models;

namespace HExporter.Core.Abstractions;

public interface IRecordReaderFactory
{
    /// <summary>Abre un reader forward-only para la petición (conexión + comando + FetchSize).</summary>
    Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct);
}
````

## File: HExporter/src/HExporter.Core/Models/ColumnSchema.cs
````csharp
namespace HExporter.Core.Models;

/// <summary>Describe una columna del resultado. Inmutable.</summary>
public sealed record ColumnSchema(int Ordinal, string Name, Type ClrType, string DbTypeName);
````

## File: HExporter/src/HExporter.Core/Models/ExportFormat.cs
````csharp
namespace HExporter.Core.Models;

public enum ExportFormat
{
    Csv,
    Xlsx
}
````

## File: HExporter/src/HExporter.Core/Models/ExportOptions.cs
````csharp
using System.Globalization;
using System.Text;

namespace HExporter.Core.Models;

/// <summary>Opciones comunes + específicas por formato. Ver docs/05-configuration.md.</summary>
public sealed class ExportOptions
{
    public bool IncludeHeaders { get; init; } = true;
    public int FlushEveryRows { get; init; } = 10_000;
    public int FileBufferBytes { get; init; } = 128 * 1024;
    public string CultureName { get; init; } = "en-US";
    public string DateFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    public CsvOptions Csv { get; init; } = new();
    public XlsxOptions Xlsx { get; init; } = new();

    public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);
}

public enum EncodingKind { Utf8, Utf8Bom, Latin1 }

public sealed class CsvOptions
{
    public string Delimiter { get; init; } = ",";
    public EncodingKind Encoding { get; init; } = EncodingKind.Utf8;

    public Encoding ResolveEncoding() => Encoding switch
    {
        EncodingKind.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        EncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        EncodingKind.Latin1 => System.Text.Encoding.Latin1,
        _ => new UTF8Encoding(false)
    };
}

public enum XlsxRowLimitStrategy { Fail, NewSheet }

public sealed class XlsxOptions
{
    /// <summary>Límite duro del formato XLSX: 1.048.576 filas por hoja.</summary>
    public const int MaxRowsPerSheet = 1_048_576;

    public string SheetName { get; init; } = "Datos";
    public XlsxRowLimitStrategy RowLimitStrategy { get; init; } = XlsxRowLimitStrategy.Fail;
}
````

## File: HExporter/src/HExporter.Core/Models/ExportRequest.cs
````csharp
namespace HExporter.Core.Models;

/// <summary>Petición de exportación resuelta (SQL final + binds + destino).</summary>
public sealed record ExportRequest(
    string Sql,
    IReadOnlyDictionary<string, object?> Binds,
    ExportFormat Format,
    string DestinationPath,
    ExportOptions Options)
{
    public static IReadOnlyDictionary<string, object?> NoBinds { get; } =
        new Dictionary<string, object?>();
}
````

## File: HExporter/src/HExporter.Core/Models/ExportResult.cs
````csharp
namespace HExporter.Core.Models;

public sealed record ExportResult(long RowCount, long BytesWritten, TimeSpan Elapsed)
{
    public double RowsPerSecond => Elapsed.TotalSeconds > 0 ? RowCount / Elapsed.TotalSeconds : 0;
}
````

## File: HExporter/src/HExporter.Core/Models/ReportProfile.cs
````csharp
namespace HExporter.Core.Models;

/// <summary>Definición declarativa reutilizable de un reporte (report.json).</summary>
public sealed class ReportProfile
{
    public string Name { get; init; } = "";
    public string Sql { get; init; } = "";
    public Dictionary<string, object?> Binds { get; init; } = new();
    public ExportFormat Format { get; init; } = ExportFormat.Csv;
    public CsvOptions? Csv { get; init; }
    public XlsxOptions? Xlsx { get; init; }
}
````

## File: HExporter/src/HExporter.Core/HExporter.Core.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

## File: HExporter/src/HExporter.Export/Csv/CsvExportWriter.cs
````csharp
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
````

## File: HExporter/src/HExporter.Export/Xlsx/XlsxExportWriter.cs
````csharp
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
        if (_failure is not null) throw new InvalidOperationException("XLSX writer failure.", _failure);

        if (_rowsInCurrentSheet >= MaxRowsPerSheetOverride)
        {
            if (_xlsx.RowLimitStrategy == XlsxRowLimitStrategy.Fail)
            {
                throw new InvalidOperationException(
                    $"The result exceeds the {XlsxOptions.MaxRowsPerSheet:N0} row-per-XLSX-sheet limit. " +
                    "Use CSV or set RowLimitStrategy=NewSheet. See docs/04-streaming-strategy.md §6.");
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
            throw new InvalidOperationException("Failed to write XLSX.", _failure);
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
````

## File: HExporter/src/HExporter.Export/DependencyInjection.cs
````csharp
using HExporter.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HExporter.Export;

public static class DependencyInjection
{
    public static IServiceCollection AddHExporterWriters(this IServiceCollection services)
    {
        services.AddSingleton<IExportWriterFactory, ExportWriterFactory>();
        return services;
    }
}
````

## File: HExporter/src/HExporter.Export/ExportWriterFactory.cs
````csharp
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using HExporter.Export.Csv;
using HExporter.Export.Xlsx;

namespace HExporter.Export;

public sealed class ExportWriterFactory : IExportWriterFactory
{
    public IExportWriter Create(ExportFormat format, Stream destination, ExportOptions options) => format switch
    {
        ExportFormat.Csv => new CsvExportWriter(destination, options),
        ExportFormat.Xlsx => new XlsxExportWriter(destination, options),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported format.")
    };
}
````

## File: HExporter/src/HExporter.Export/HExporter.Export.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HExporter.Core\HExporter.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="HExporter.UnitTests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />
    <PackageReference Include="MiniExcel" Version="1.45.0" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

## File: HExporter/src/HExporter.Infrastructure/Common/ConnectionRetryPolicyFactory.cs
````csharp
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace HExporter.Infrastructure.Common;

/// <summary>Reintentos con backoff exponencial ante fallo transitorio al abrir una conexión.
/// ShouldHandle solo captura TException (la excepción propia del driver) — no reintenta
/// OperationCanceledException (cancelación explícita) ni errores de configuración.</summary>
internal static class ConnectionRetryPolicyFactory
{
    public static ResiliencePipeline Build<TException>(
        int maxAttempts, double baseDelaySeconds, ILogger logger, string providerName)
        where TException : Exception
    {
        if (maxAttempts <= 0)
            return ResiliencePipeline.Empty;

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<TException>(),
                MaxRetryAttempts = maxAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(baseDelaySeconds),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying {Provider} connection (attempt {Attempt}/{Max}) after {Delay}",
                        providerName, args.AttemptNumber + 1, maxAttempts, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }
}
````

## File: HExporter/src/HExporter.Infrastructure/Oracle/OracleConnectionFactory.cs
````csharp
using HExporter.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Polly;

namespace HExporter.Infrastructure.Oracle;

public sealed class OracleConnectionFactory
{
    private readonly OracleOptions _options;
    private readonly ResiliencePipeline _retryPipeline;

    public OracleConnectionFactory(IOptions<OracleOptions> options, ILogger<OracleConnectionFactory> logger)
    {
        _options = options.Value;
        _retryPipeline = ConnectionRetryPolicyFactory.Build<OracleException>(
            _options.ConnectRetryAttempts, _options.ConnectRetryBaseDelaySeconds, logger, "Oracle");
    }

    public OracleOptions Options => _options;

    public async Task<OracleConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Oracle:ConnectionString not configured. See docs/05-configuration.md §2.");

        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var conn = new OracleConnection(_options.ConnectionString);
            await conn.OpenAsync(token);
            return conn;
        }, ct);
    }
}
````

## File: HExporter/src/HExporter.Infrastructure/Oracle/OracleOptions.cs
````csharp
namespace HExporter.Infrastructure.Oracle;

public sealed class OracleOptions
{
    public const string SectionName = "Oracle";

    /// <summary>Cadena de conexión. Resolver desde env/secret/Wallet — nunca hardcodear.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Bytes por lote de red. ~256KB–1MB. NO es el resultado completo.</summary>
    public long FetchSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>0 = sin límite (reportes largos).</summary>
    public int CommandTimeoutSeconds { get; set; } = 0;

    public bool BindByName { get; set; } = true;

    /// <summary>Reintentos ante fallo transitorio de apertura de conexión (Polly). 0 = sin reintentos.</summary>
    public int ConnectRetryAttempts { get; set; } = 3;

    /// <summary>Base del backoff exponencial entre reintentos de conexión.</summary>
    public double ConnectRetryBaseDelaySeconds { get; set; } = 2.0;
}
````

## File: HExporter/src/HExporter.Infrastructure/Oracle/OracleRecordReader.cs
````csharp
using System.Data;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace HExporter.Infrastructure.Oracle;

/// <summary>
/// Adaptador Oracle. Cursor server-side forward-only. FetchSize acota el lote de red.
/// SequentialAccess permite streaming de columnas/LOB sin bufferizarlos completos.
/// </summary>
public sealed class OracleRecordReader : IRecordReader
{
    private readonly OracleConnection _conn;
    private readonly OracleCommand _cmd;
    private readonly OracleDataReader _reader;

    public IReadOnlyList<ColumnSchema> Schema { get; }

    private OracleRecordReader(OracleConnection conn, OracleCommand cmd, OracleDataReader reader)
    {
        _conn = conn;
        _cmd = cmd;
        _reader = reader;
        Schema = BuildSchema(reader);
    }

    public static async Task<OracleRecordReader> OpenAsync(
        OracleConnectionFactory factory,
        string sql,
        IReadOnlyDictionary<string, object?> binds,
        CancellationToken ct)
    {
        var opt = factory.Options;
        var conn = await factory.OpenAsync(ct);
        try
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = opt.CommandTimeoutSeconds;
            cmd.FetchSize = opt.FetchSizeBytes;      // clave: lote acotado, no todo el resultado
            cmd.InitialLOBFetchSize = -1;            // stream de LOBs
            cmd.BindByName = opt.BindByName;
            foreach (var (k, v) in binds)
                cmd.Parameters.Add(new OracleParameter(k, v ?? DBNull.Value));

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess, ct);
            return new OracleRecordReader(conn, cmd, reader);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static IReadOnlyList<ColumnSchema> BuildSchema(OracleDataReader reader)
    {
        var cols = new ColumnSchema[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
            cols[i] = new ColumnSchema(i, reader.GetName(i), reader.GetFieldType(i), reader.GetDataTypeName(i));
        return cols;
    }

    public ValueTask<bool> ReadAsync(CancellationToken ct) => new(_reader.ReadAsync(ct));

    public object? GetValue(int ordinal) => _reader.IsDBNull(ordinal) ? null : _reader.GetValue(ordinal);

    public bool IsDBNull(int ordinal) => _reader.IsDBNull(ordinal);

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _cmd.DisposeAsync();
        await _conn.DisposeAsync(); // devuelve al pool
    }
}
````

## File: HExporter/src/HExporter.Infrastructure/Oracle/OracleRecordReaderFactory.cs
````csharp
using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.Infrastructure.Oracle;

public sealed class OracleRecordReaderFactory : IRecordReaderFactory
{
    private readonly OracleConnectionFactory _connectionFactory;

    public OracleRecordReaderFactory(OracleConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct)
        => await OracleRecordReader.OpenAsync(_connectionFactory, request.Sql, request.Binds, ct);
}
````

## File: HExporter/src/HExporter.Infrastructure/Postgres/PostgresConnectionFactory.cs
````csharp
using HExporter.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;

namespace HExporter.Infrastructure.Postgres;

public sealed class PostgresConnectionFactory
{
    private readonly PostgresOptions _options;
    private readonly ResiliencePipeline _retryPipeline;

    public PostgresConnectionFactory(IOptions<PostgresOptions> options, ILogger<PostgresConnectionFactory> logger)
    {
        _options = options.Value;
        _retryPipeline = ConnectionRetryPolicyFactory.Build<NpgsqlException>(
            _options.ConnectRetryAttempts, _options.ConnectRetryBaseDelaySeconds, logger, "PostgreSQL");
    }

    public PostgresOptions Options => _options;

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Postgres:ConnectionString not configured. See docs/05-configuration.md §2.");

        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var conn = new NpgsqlConnection(_options.ConnectionString);
            await conn.OpenAsync(token);
            return conn;
        }, ct);
    }
}
````

## File: HExporter/src/HExporter.Infrastructure/Postgres/PostgresOptions.cs
````csharp
namespace HExporter.Infrastructure.Postgres;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    /// <summary>Cadena de conexión. Resolver desde env/secret — nunca hardcodear.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>0 = sin límite (reportes largos).</summary>
    public int CommandTimeoutSeconds { get; set; } = 0;

    /// <summary>Reintentos ante fallo transitorio de apertura de conexión (Polly). 0 = sin reintentos.</summary>
    public int ConnectRetryAttempts { get; set; } = 3;

    /// <summary>Base del backoff exponencial entre reintentos de conexión.</summary>
    public double ConnectRetryBaseDelaySeconds { get; set; } = 2.0;
}
````

## File: HExporter/src/HExporter.Infrastructure/Postgres/PostgresRecordReader.cs
````csharp
using System.Data;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using Npgsql;

namespace HExporter.Infrastructure.Postgres;

/// <summary>
/// Adaptador PostgreSQL. El protocolo binario de Npgsql ya transmite fila a fila (no bufferiza
/// el resultset completo); CommandBehavior.SequentialAccess evita además bufferizar columnas
/// grandes (bytea/text) al leerlas.
/// </summary>
public sealed class PostgresRecordReader : IRecordReader
{
    private readonly NpgsqlConnection _conn;
    private readonly NpgsqlCommand _cmd;
    private readonly NpgsqlDataReader _reader;

    public IReadOnlyList<ColumnSchema> Schema { get; }

    private PostgresRecordReader(NpgsqlConnection conn, NpgsqlCommand cmd, NpgsqlDataReader reader)
    {
        _conn = conn;
        _cmd = cmd;
        _reader = reader;
        Schema = BuildSchema(reader);
    }

    public static async Task<PostgresRecordReader> OpenAsync(
        PostgresConnectionFactory factory,
        string sql,
        IReadOnlyDictionary<string, object?> binds,
        CancellationToken ct)
    {
        var opt = factory.Options;
        var conn = await factory.OpenAsync(ct);
        try
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = opt.CommandTimeoutSeconds;
            foreach (var (k, v) in binds)
                cmd.Parameters.Add(new NpgsqlParameter(k, v ?? DBNull.Value));

            var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            return new PostgresRecordReader(conn, cmd, reader);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static IReadOnlyList<ColumnSchema> BuildSchema(NpgsqlDataReader reader)
    {
        var cols = new ColumnSchema[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
            cols[i] = new ColumnSchema(i, reader.GetName(i), reader.GetFieldType(i), reader.GetDataTypeName(i));
        return cols;
    }

    public ValueTask<bool> ReadAsync(CancellationToken ct) => new(_reader.ReadAsync(ct));

    public object? GetValue(int ordinal) => _reader.IsDBNull(ordinal) ? null : _reader.GetValue(ordinal);

    public bool IsDBNull(int ordinal) => _reader.IsDBNull(ordinal);

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _cmd.DisposeAsync();
        await _conn.DisposeAsync(); // devuelve al pool
    }
}
````

## File: HExporter/src/HExporter.Infrastructure/Postgres/PostgresRecordReaderFactory.cs
````csharp
using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.Infrastructure.Postgres;

public sealed class PostgresRecordReaderFactory : IRecordReaderFactory
{
    private readonly PostgresConnectionFactory _connectionFactory;

    public PostgresRecordReaderFactory(PostgresConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct)
        => await PostgresRecordReader.OpenAsync(_connectionFactory, request.Sql, request.Binds, ct);
}
````

## File: HExporter/src/HExporter.Infrastructure/DatabaseEngine.cs
````csharp
namespace HExporter.Infrastructure;

public enum DatabaseEngine
{
    Oracle,
    Postgres
}
````

## File: HExporter/src/HExporter.Infrastructure/DatabaseEngineResolver.cs
````csharp
namespace HExporter.Infrastructure;

/// <summary>Resuelve el motor de base de datos activo. Precedencia: CLI > configuración
/// (env var real / [dot]env / appsettings.json, en ese orden, ya fusionada por IConfiguration).
/// Default: Oracle.</summary>
public static class DatabaseEngineResolver
{
    public const string ConfigKey = "Database:Engine";

    public static DatabaseEngine Resolve(string? cliValue, string? configuredValue)
    {
        string? raw = !string.IsNullOrWhiteSpace(cliValue) ? cliValue : configuredValue;
        if (string.IsNullOrWhiteSpace(raw))
            return DatabaseEngine.Oracle;

        return raw.Trim().ToLowerInvariant() switch
        {
            "oracle" => DatabaseEngine.Oracle,
            "postgres" or "postgresql" or "pg" => DatabaseEngine.Postgres,
            _ => throw new ArgumentException(
                $"Unsupported database engine '{raw}'. Valid values: oracle, postgres.")
        };
    }
}
````

## File: HExporter/src/HExporter.Infrastructure/DependencyInjection.cs
````csharp
using HExporter.Core.Abstractions;
using HExporter.Infrastructure.Oracle;
using HExporter.Infrastructure.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HExporter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHExporterOracle(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OracleOptions>(config.GetSection(OracleOptions.SectionName));
        services.AddSingleton<OracleConnectionFactory>();
        services.AddSingleton<IRecordReaderFactory, OracleRecordReaderFactory>();
        return services;
    }

    public static IServiceCollection AddHExporterPostgres(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PostgresOptions>(config.GetSection(PostgresOptions.SectionName));
        services.AddSingleton<PostgresConnectionFactory>();
        services.AddSingleton<IRecordReaderFactory, PostgresRecordReaderFactory>();
        return services;
    }

    /// <summary>Registra el adaptador de base de datos correspondiente al motor seleccionado
    /// (ver <see cref="DatabaseEngineResolver"/>). Un único <see cref="IRecordReaderFactory"/>
    /// queda registrado por ejecución — no hay despacho en tiempo de fila.</summary>
    public static IServiceCollection AddHExporterDatabase(
        this IServiceCollection services, IConfiguration config, DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.Oracle => services.AddHExporterOracle(config),
        DatabaseEngine.Postgres => services.AddHExporterPostgres(config),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported database engine.")
    };
}
````

## File: HExporter/src/HExporter.Infrastructure/HExporter.Infrastructure.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\HExporter.Core\HExporter.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.9" />
    <PackageReference Include="Npgsql" Version="10.0.3" />
    <PackageReference Include="Oracle.ManagedDataAccess.Core" Version="23.26.200" />
    <PackageReference Include="Polly.Core" Version="8.5.0" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

## File: HExporter/tests/HExporter.IntegrationTests/HExporter.IntegrationTests.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.9" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Testcontainers.Oracle" Version="4.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HExporter.Core\HExporter.Core.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Infrastructure\HExporter.Infrastructure.csproj" />
  </ItemGroup>

</Project>
````

## File: HExporter/tests/HExporter.IntegrationTests/OracleFixture.cs
````csharp
using DotNet.Testcontainers.Builders;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

namespace HExporter.IntegrationTests;

/// <summary>
/// Un solo contenedor Oracle (gvenzl/oracle-free) compartido por toda la colección de
/// tests — arrancarlo es lento (~30-60s), no vale la pena por test. Ver docs/07-testing-strategy.md.
///
/// La wait strategy por defecto de Testcontainers.Oracle (buscar mensaje en logs) choca con
/// podman: Docker.DotNet lanza "Invalid chunk header encountered" al leer logs vía el socket
/// de podman (incompatibilidad conocida en el streaming de logs, no un bug de esta app). Se
/// reemplaza por espera de puerto + retry de conexión real, que evita ese endpoint de logs.
///
/// No se usa `.WithDatabase(...)`: gvenzl/oracle-free ya crea por defecto el PDB "FREEPDB1";
/// pedirle explícitamente crear uno con ese mismo nombre dispara "ORA-65012: ya existe" y
/// el contenedor aborta el arranque. El servicio por defecto de la librería (Testcontainers.Oracle
/// usa "XE", pensado para oracle-xe) tampoco sirve aquí, así que el connection string de conexión
/// se arma a mano contra el PDB real ("FREEPDB1") en vez de usar `_container.GetConnectionString()`.
/// </summary>
public sealed class OracleFixture : IAsyncLifetime
{
    private OracleContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _container = new OracleBuilder("gvenzl/oracle-free:slim-faststart")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1521))
            .Build();
        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(1521);
        var descriptor =
            $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME=FREEPDB1)))";
        // Pooling=false + Connection Timeout corto: cada intento falla rápido en vez de
        // esperar el timeout por defecto (~15s) del connection pool de ODP.NET.
        ConnectionString = $"User Id=oracle;Password=oracle;Data Source={descriptor};Pooling=false;Connection Timeout=5";
        var probeConnectionString = ConnectionString;

        var deadline = DateTime.UtcNow.AddSeconds(300);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var conn = new OracleConnection(probeConnectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM dual";
                await cmd.ExecuteScalarAsync();
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        throw new TimeoutException("Oracle no respondió a tiempo tras iniciar el contenedor.", lastError);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class OracleCollection : ICollectionFixture<OracleFixture>
{
    public const string Name = "Oracle";
}
````

## File: HExporter/tests/HExporter.IntegrationTests/OracleRecordReaderTests.cs
````csharp
using FluentAssertions;
using HExporter.Core.Models;
using HExporter.Infrastructure.Oracle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HExporter.IntegrationTests;

/// <summary>
/// T3.6: OracleRecordReader contra un Oracle real (Testcontainers). Verifica el
/// contrato forward-only, bind variables y que un FetchSize pequeño (varios round-trips
/// de red) no pierde ni duplica filas — la razón de ser de streaming por lotes acotados.
/// </summary>
[Collection(OracleCollection.Name)]
public sealed class OracleRecordReaderTests(OracleFixture fixture)
{
    private OracleConnectionFactory CreateFactory(long fetchSizeBytes = 1024 * 1024)
    {
        var options = new OracleOptions
        {
            ConnectionString = fixture.ConnectionString,
            FetchSizeBytes = fetchSizeBytes,
            ConnectRetryAttempts = 0
        };
        return new OracleConnectionFactory(Options.Create(options), NullLogger<OracleConnectionFactory>.Instance);
    }

    // Nombre de tabla único (< 30 chars, límite de identificador Oracle sin comillas).
    private static string UniqueTable(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();

    [Fact]
    public async Task Reads_rows_streaming_from_real_oracle()
    {
        var factory = CreateFactory();
        string table = UniqueTable("HX_BASIC");

        await using (var conn = await factory.OpenAsync(default))
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE TABLE {table} (id NUMBER, nombre VARCHAR2(50), monto NUMBER(10,2))";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = $"INSERT INTO {table} VALUES (1, 'ana', 10.5)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO {table} VALUES (2, 'beto', 20.75)";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var reader = await OracleRecordReader.OpenAsync(
            factory, $"SELECT id, nombre, monto FROM {table} ORDER BY id", ExportRequest.NoBinds, default);

        reader.Schema.Should().HaveCount(3);
        reader.Schema[0].Name.Should().Be("ID");
        reader.Schema[1].Name.Should().Be("NOMBRE");
        reader.Schema[2].Name.Should().Be("MONTO");

        var rows = new List<(decimal Id, string Nombre, decimal Monto)>();
        while (await reader.ReadAsync(default))
        {
            rows.Add((
                Convert.ToDecimal(reader.GetValue(0)),
                (string)reader.GetValue(1)!,
                Convert.ToDecimal(reader.GetValue(2))));
        }

        rows.Should().HaveCount(2);
        rows[0].Should().Be((1m, "ana", 10.5m));
        rows[1].Should().Be((2m, "beto", 20.75m));
    }

    [Fact]
    public async Task Bind_variables_filter_rows_via_bind_by_name()
    {
        var factory = CreateFactory();
        string table = UniqueTable("HX_BIND");

        await using (var conn = await factory.OpenAsync(default))
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE TABLE {table} (id NUMBER, fecha DATE)";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO {table} VALUES (1, DATE '2025-01-01')";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO {table} VALUES (2, DATE '2026-06-01')";
            await cmd.ExecuteNonQueryAsync();
        }

        var binds = new Dictionary<string, object?> { ["desde"] = new DateTime(2026, 1, 1) };
        await using var reader = await OracleRecordReader.OpenAsync(
            factory, $"SELECT id FROM {table} WHERE fecha >= :desde ORDER BY id", binds, default);

        var ids = new List<decimal>();
        while (await reader.ReadAsync(default))
            ids.Add(Convert.ToDecimal(reader.GetValue(0)));

        ids.Should().Equal(2m);
    }

    [Fact]
    public async Task Small_fetch_size_forces_multiple_round_trips_without_losing_rows()
    {
        var factory = CreateFactory(fetchSizeBytes: 2048); // fuerza varios lotes de red
        string table = UniqueTable("HX_FETCH");
        const int expectedRows = 3000;

        await using (var conn = await factory.OpenAsync(default))
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE TABLE {table} (id NUMBER, nombre VARCHAR2(50))";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText =
                $"INSERT INTO {table} (id, nombre) " +
                $"SELECT LEVEL, 'cliente_' || LEVEL FROM dual CONNECT BY LEVEL <= {expectedRows}";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var reader = await OracleRecordReader.OpenAsync(
            factory, $"SELECT id, nombre FROM {table} ORDER BY id", ExportRequest.NoBinds, default);

        long count = 0;
        decimal lastId = 0;
        while (await reader.ReadAsync(default))
        {
            count++;
            lastId = Convert.ToDecimal(reader.GetValue(0));
        }

        count.Should().Be(expectedRows);
        lastId.Should().Be(expectedRows);
    }
}
````

## File: HExporter/tests/HExporter.UnitTests/CsvExportWriterTests.cs
````csharp
using System.Text;
using FluentAssertions;
using HExporter.Core.Models;
using HExporter.Export.Csv;

namespace HExporter.UnitTests;

public class CsvExportWriterTests
{
    private static ColumnSchema[] Schema(params string[] names)
        => names.Select((n, i) => new ColumnSchema(i, n, typeof(object), "OBJ")).ToArray();

    private static async Task<string> WriteAsync(ColumnSchema[] schema, object?[][] rows, ExportOptions opt)
    {
        using var ms = new MemoryStream();
        await using (var w = new CsvExportWriter(ms, opt))
        {
            await w.BeginAsync(schema, default);
            var reader = new FakeRecordReader(schema, rows);
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
            await w.EndAsync(default);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public async Task Writes_headers_and_rows()
    {
        var csv = await WriteAsync(Schema("id", "name"),
            new object?[][] { new object?[] { 1, "ana" }, new object?[] { 2, "beto" } },
            new ExportOptions());
        csv.Should().Be("id,name\n1,ana\n2,beto\n");
    }

    [Fact]
    public async Task Null_becomes_empty_cell()
    {
        var csv = await WriteAsync(Schema("a", "b"),
            new object?[][] { new object?[] { null, "x" } }, new ExportOptions());
        csv.Should().Be("a,b\n,x\n");
    }

    [Fact]
    public async Task Quotes_values_with_delimiter_quote_or_newline()
    {
        var csv = await WriteAsync(Schema("v"),
            new object?[][]
            {
                new object?[] { "a,b" },
                new object?[] { "he said \"hi\"" },
                new object?[] { "line1\nline2" }
            },
            new ExportOptions());
        csv.Should().Be("v\n\"a,b\"\n\"he said \"\"hi\"\"\"\n\"line1\nline2\"\n");
    }

    [Fact]
    public async Task Uses_fixed_culture_for_numbers_and_dates()
    {
        var opt = new ExportOptions { CultureName = "en-US", DateFormat = "yyyy-MM-dd" };
        var csv = await WriteAsync(Schema("n", "d"),
            new object?[][] { new object?[] { 1234.5m, new DateTime(2026, 1, 31) } }, opt);
        csv.Should().Be("n,d\n1234.5,2026-01-31\n");
    }

    [Fact]
    public async Task No_headers_option_omits_header_row()
    {
        var csv = await WriteAsync(Schema("id"),
            new object?[][] { new object?[] { 7 } },
            new ExportOptions { IncludeHeaders = false });
        csv.Should().Be("7\n");
    }

    [Fact]
    public async Task Custom_delimiter()
    {
        var opt = new ExportOptions { Csv = new CsvOptions { Delimiter = ";" } };
        var csv = await WriteAsync(Schema("a", "b"),
            new object?[][] { new object?[] { 1, 2 } }, opt);
        csv.Should().Be("a;b\n1;2\n");
    }
}
````

## File: HExporter/tests/HExporter.UnitTests/DatabaseEngineResolverTests.cs
````csharp
using FluentAssertions;
using HExporter.Infrastructure;

namespace HExporter.UnitTests;

public class DatabaseEngineResolverTests
{
    [Fact]
    public void Defaults_to_oracle_when_nothing_set()
        => DatabaseEngineResolver.Resolve(null, null).Should().Be(DatabaseEngine.Oracle);

    [Theory]
    [InlineData("postgres")]
    [InlineData("POSTGRES")]
    [InlineData("postgresql")]
    [InlineData("pg")]
    public void Cli_value_selects_postgres(string cliValue)
        => DatabaseEngineResolver.Resolve(cliValue, null).Should().Be(DatabaseEngine.Postgres);

    [Fact]
    public void Config_value_used_when_cli_not_given()
        => DatabaseEngineResolver.Resolve(null, "postgres").Should().Be(DatabaseEngine.Postgres);

    [Fact]
    public void Cli_value_takes_precedence_over_config()
        => DatabaseEngineResolver.Resolve("oracle", "postgres").Should().Be(DatabaseEngine.Oracle);

    [Fact]
    public void Unknown_value_throws_with_valid_options_listed()
    {
        var act = () => DatabaseEngineResolver.Resolve("mysql", null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*mysql*oracle*postgres*");
    }
}
````

## File: HExporter/tests/HExporter.UnitTests/ExportRequestValidatorTests.cs
````csharp
using FluentAssertions;
using HExporter.Application.Validation;
using HExporter.Core.Models;
using Microsoft.Extensions.Options;

namespace HExporter.UnitTests;

public class ExportRequestValidatorTests
{
    private static ExportRequest Request(string destinationPath) =>
        new("SELECT 1", ExportRequest.NoBinds, ExportFormat.Csv, destinationPath, new ExportOptions());

    [Fact]
    public void No_boundary_configured_allows_any_path()
    {
        var validator = new ExportRequestValidator(Options.Create(new ExportSecurityOptions()));

        var act = () => validator.Validate(Request("../../otro_directorio/salida.csv"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Rejects_path_traversal_outside_allowed_directory()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("../../otro_directorio/salida.csv"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_absolute_path_outside_allowed_directory()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("/otro_directorio/salida.csv"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Allows_path_inside_allowed_directory()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("reporte.csv"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Stdout_output_bypasses_boundary_check()
    {
        var options = Options.Create(new ExportSecurityOptions { AllowedOutputDirectory = "/data/exports" });
        var validator = new ExportRequestValidator(options);

        var act = () => validator.Validate(Request("-"));

        act.Should().NotThrow();
    }
}
````

## File: HExporter/tests/HExporter.UnitTests/FakeRecordReader.cs
````csharp
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
````

## File: HExporter/tests/HExporter.UnitTests/HExporter.UnitTests.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="MiniExcel" Version="1.45.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\HExporter.Core\HExporter.Core.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Application\HExporter.Application.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Export\HExporter.Export.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Infrastructure\HExporter.Infrastructure.csproj" />
  </ItemGroup>

</Project>
````

## File: HExporter/tests/HExporter.UnitTests/OracleConnectionFactoryRetryTests.cs
````csharp
using FluentAssertions;
using HExporter.Infrastructure.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace HExporter.UnitTests;

public class OracleConnectionFactoryRetryTests
{
    private sealed class CountingLogger : ILogger<OracleConnectionFactory>
    {
        public int WarningCount { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) WarningCount++;
        }
    }

    [Fact]
    public async Task Retries_configured_number_of_times_on_transient_connect_failure()
    {
        var options = Options.Create(new OracleOptions
        {
            ConnectionString = "Data Source=###malformed###;User Id=x;Password=x",
            ConnectRetryAttempts = 2,
            ConnectRetryBaseDelaySeconds = 0.01
        });
        var logger = new CountingLogger();
        var factory = new OracleConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OracleException>();
        logger.WarningCount.Should().Be(2);
    }

    [Fact]
    public async Task Does_not_retry_when_disabled()
    {
        var options = Options.Create(new OracleOptions
        {
            ConnectionString = "Data Source=###malformed###;User Id=x;Password=x",
            ConnectRetryAttempts = 0
        });
        var logger = new CountingLogger();
        var factory = new OracleConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OracleException>();
        logger.WarningCount.Should().Be(0);
    }
}
````

## File: HExporter/tests/HExporter.UnitTests/PostgresConnectionFactoryRetryTests.cs
````csharp
using FluentAssertions;
using HExporter.Infrastructure.Postgres;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HExporter.UnitTests;

public class PostgresConnectionFactoryRetryTests
{
    private sealed class CountingLogger : ILogger<PostgresConnectionFactory>
    {
        public int WarningCount { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) WarningCount++;
        }
    }

    // Puerto reservado (TCP no asignable) => rechazo de conexión inmediato, sin espera de red real.
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1";

    [Fact]
    public async Task Retries_configured_number_of_times_on_transient_connect_failure()
    {
        var options = Options.Create(new PostgresOptions
        {
            ConnectionString = UnreachableConnectionString,
            ConnectRetryAttempts = 2,
            ConnectRetryBaseDelaySeconds = 0.01
        });
        var logger = new CountingLogger();
        var factory = new PostgresConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NpgsqlException>();
        logger.WarningCount.Should().Be(2);
    }

    [Fact]
    public async Task Does_not_retry_when_disabled()
    {
        var options = Options.Create(new PostgresOptions
        {
            ConnectionString = UnreachableConnectionString,
            ConnectRetryAttempts = 0
        });
        var logger = new CountingLogger();
        var factory = new PostgresConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NpgsqlException>();
        logger.WarningCount.Should().Be(0);
    }

    [Fact]
    public async Task Throws_when_connection_string_missing()
    {
        var options = Options.Create(new PostgresOptions { ConnectionString = "" });
        var factory = new PostgresConnectionFactory(options, new CountingLogger());

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
````

## File: HExporter/tests/HExporter.UnitTests/XlsxExportWriterTests.cs
````csharp
using FluentAssertions;
using HExporter.Core.Models;
using HExporter.Export.Xlsx;
using MiniExcelLibs;

namespace HExporter.UnitTests;

public class XlsxExportWriterTests
{
    private static ColumnSchema[] Schema(params string[] names)
        => names.Select((n, i) => new ColumnSchema(i, n, typeof(object), "OBJ")).ToArray();

    [Fact]
    public async Task Writes_streaming_xlsx_with_headers_and_rows()
    {
        var schema = Schema("id", "name");
        var rows = new object?[][] { new object?[] { 1, "ana" }, new object?[] { 2, "beto" } };

        using var ms = new MemoryStream();
        await using (var w = new XlsxExportWriter(ms, new ExportOptions()))
        {
            await w.BeginAsync(schema, default);
            var reader = new FakeRecordReader(schema, rows);
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
            await w.EndAsync(default);
        }

        ms.Position = 0;
        var read = ms.Query(useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();
        read.Should().HaveCount(2);
        read[0]["id"].Should().Be(1d);
        read[0]["name"].Should().Be("ana");
        read[1]["name"].Should().Be("beto");
    }

    [Fact]
    public async Task NewSheet_strategy_rolls_over_when_row_limit_exceeded()
    {
        var schema = Schema("id");
        var options = new ExportOptions
        {
            Xlsx = new XlsxOptions { SheetName = "Datos", RowLimitStrategy = XlsxRowLimitStrategy.NewSheet }
        };

        using var ms = new MemoryStream();
        await using (var w = new XlsxExportWriter(ms, options) { MaxRowsPerSheetOverride = 3 })
        {
            await w.BeginAsync(schema, default);
            var rows = Enumerable.Range(1, 7).Select(i => new object?[] { i }).ToArray();
            var reader = new FakeRecordReader(schema, rows);
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
            await w.EndAsync(default);
        }

        ms.Position = 0;
        var sheetNames = ms.GetSheetNames();
        sheetNames.Should().BeEquivalentTo(["Datos", "Datos_2", "Datos_3"]);

        var sheet1 = ms.Query(useHeaderRow: true, sheetName: "Datos").Cast<IDictionary<string, object>>().ToList();
        var sheet2 = ms.Query(useHeaderRow: true, sheetName: "Datos_2").Cast<IDictionary<string, object>>().ToList();
        var sheet3 = ms.Query(useHeaderRow: true, sheetName: "Datos_3").Cast<IDictionary<string, object>>().ToList();

        sheet1.Should().HaveCount(3);
        sheet2.Should().HaveCount(3);
        sheet3.Should().HaveCount(1);
    }

    [Fact]
    public async Task Fail_strategy_still_throws_when_row_limit_exceeded()
    {
        var schema = Schema("id");
        var options = new ExportOptions
        {
            Xlsx = new XlsxOptions { RowLimitStrategy = XlsxRowLimitStrategy.Fail }
        };

        using var ms = new MemoryStream();
        await using var w = new XlsxExportWriter(ms, options) { MaxRowsPerSheetOverride = 2 };
        await w.BeginAsync(schema, default);
        var rows = Enumerable.Range(1, 3).Select(i => new object?[] { i }).ToArray();
        var reader = new FakeRecordReader(schema, rows);

        var act = async () =>
        {
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
````

## File: HExporter/tools/HExporter.Benchmarks/ExportThroughputBenchmarks.cs
````csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using HExporter.Application;
using HExporter.Application.Validation;
using HExporter.Core.Models;
using HExporter.Export;
using HExporter.MemProbe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HExporter.Benchmarks;

/// <summary>
/// Mide throughput del pipeline de exportación (reader sintético → writer real → disco)
/// variando FlushEveryRows y FileBufferBytes (T9.3). Sin Oracle: FetchSizeBytes solo
/// importa con un listener real y no se puede medir aquí — ver docs/09-tuning.md.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class ExportThroughputBenchmarks
{
    private const long Rows = 200_000;

    private ExportService _service = null!;
    private string _outDir = null!;

    [Params(1_000, 10_000, 100_000)]
    public int FlushEveryRows { get; set; }

    [Params(65536, 1048576)]
    public int FileBufferBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _outDir = Directory.CreateTempSubdirectory("hexporter-bench").FullName;
        var readerFactory = new SyntheticReaderFactory(Rows);
        var writerFactory = new ExportWriterFactory();
        var validator = new ExportRequestValidator(Options.Create(new ExportSecurityOptions()));
        _service = new ExportService(readerFactory, writerFactory, validator, NullLogger<ExportService>.Instance);
    }

    [Benchmark]
    public async Task Csv()
    {
        string path = Path.Combine(_outDir, $"bench_{Guid.NewGuid():N}.csv");
        var options = new ExportOptions { FlushEveryRows = FlushEveryRows, FileBufferBytes = FileBufferBytes };
        var request = new ExportRequest("SELECT 1", ExportRequest.NoBinds, ExportFormat.Csv, path, options);
        await _service.ExecuteAsync(request, progress: null, CancellationToken.None);
        File.Delete(path);
    }

    [Benchmark]
    public async Task Xlsx()
    {
        string path = Path.Combine(_outDir, $"bench_{Guid.NewGuid():N}.xlsx");
        var options = new ExportOptions { FlushEveryRows = FlushEveryRows, FileBufferBytes = FileBufferBytes };
        var request = new ExportRequest("SELECT 1", ExportRequest.NoBinds, ExportFormat.Xlsx, path, options);
        await _service.ExecuteAsync(request, progress: null, CancellationToken.None);
        File.Delete(path);
    }

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_outDir, recursive: true);
}
````

## File: HExporter/tools/HExporter.Benchmarks/HExporter.Benchmarks.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\HExporter.Core\HExporter.Core.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Application\HExporter.Application.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Export\HExporter.Export.csproj" />
    <ProjectReference Include="..\HExporter.MemProbe\HExporter.MemProbe.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.14.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
````

## File: HExporter/tools/HExporter.Benchmarks/Program.cs
````csharp
using BenchmarkDotNet.Running;
using HExporter.Benchmarks;

BenchmarkRunner.Run<ExportThroughputBenchmarks>(args: args);
````

## File: HExporter/tools/HExporter.MemProbe/HExporter.MemProbe.csproj
````
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\HExporter.Core\HExporter.Core.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Application\HExporter.Application.csproj" />
    <ProjectReference Include="..\..\src\HExporter.Export\HExporter.Export.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.9" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
  </PropertyGroup>

</Project>
````

## File: HExporter/tools/HExporter.MemProbe/Program.cs
````csharp
using System.Diagnostics;
using HExporter.Application;
using HExporter.Application.Validation;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using HExporter.Export;
using HExporter.MemProbe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// ---- Args ----
//   --rows N        nº de filas sintéticas (def. 10_000_000)
//   --format csv|xlsx (def. csv)
//   --out path      (def. probe.<ext>)
long rows = 10_000_000;
var format = ExportFormat.Csv;
string? outPath = null;

for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--rows": rows = long.Parse(args[++i]); break;
        case "--format": format = Enum.Parse<ExportFormat>(args[++i], ignoreCase: true); break;
        case "--out": outPath = args[++i]; break;
    }
}
outPath ??= $"probe.{(format == ExportFormat.Xlsx ? "xlsx" : "csv")}";

Console.WriteLine($"MemProbe: {rows:N0} filas -> {format} -> {outPath}");
Console.WriteLine($"GC modo servidor: {System.Runtime.GCSettings.IsServerGC}");

// ---- Servicio real (sin Oracle: reader sintético) ----
var service = new ExportService(
    new SyntheticReaderFactory(rows),
    new ExportWriterFactory(),
    new ExportRequestValidator(Options.Create(new ExportSecurityOptions())),
    NullLogger<ExportService>.Instance);

var options = new ExportOptions { FlushEveryRows = 50_000 };
var request = new ExportRequest("synthetic", ExportRequest.NoBinds, format, outPath, options);

// ---- Muestreo de memoria en background ----
long peakWorkingSet = 0;
long peakManagedHeap = 0;
using var stop = new CancellationTokenSource();
var sampler = Task.Run(async () =>
{
    var proc = Process.GetCurrentProcess();
    while (!stop.IsCancellationRequested)
    {
        proc.Refresh();
        peakWorkingSet = Math.Max(peakWorkingSet, proc.WorkingSet64);
        peakManagedHeap = Math.Max(peakManagedHeap, GC.GetTotalMemory(forceFullCollection: false));
        try { await Task.Delay(250, stop.Token); } catch (OperationCanceledException) { break; }
    }
});

var progress = new ConsoleProgress();
var sw = Stopwatch.StartNew();
var result = await service.ExecuteAsync(request, progress, CancellationToken.None);
sw.Stop();

stop.Cancel();
await sampler;

Console.WriteLine();
Console.WriteLine("==== Resultado ====");
Console.WriteLine($"Filas escritas : {result.RowCount:N0}");
Console.WriteLine($"Bytes archivo  : {result.BytesWritten:N0} ({result.BytesWritten / 1024.0 / 1024:N1} MB)");
Console.WriteLine($"Duración       : {result.Elapsed} ({result.RowsPerSecond:N0} filas/s)");
Console.WriteLine($"Peak WorkingSet: {peakWorkingSet / 1024.0 / 1024:N1} MB");
Console.WriteLine($"Peak GC heap   : {peakManagedHeap / 1024.0 / 1024:N1} MB");
Console.WriteLine($"GC gen0/1/2    : {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
Console.WriteLine();
Console.WriteLine(peakWorkingSet < 500L * 1024 * 1024
    ? "PASS: working set < 500 MB (memoria O(1) respecto a filas)."
    : "REVISAR: working set >= 500 MB. Ver docs/04-streaming-strategy.md.");

sealed file class ConsoleProgress : IProgressSink
{
    public void Report(long rowsWritten) => Console.Write($"\r  {rowsWritten:N0} filas...");
}
````

## File: HExporter/tools/HExporter.MemProbe/README.md
````markdown
# HExporter.MemProbe — Prueba de memoria / volumen

Valida el objetivo del proyecto: **memoria O(1) respecto al nº de filas** (docs/07 §4).
Genera filas sintéticas al vuelo (`SyntheticRecordReader`, sin Oracle) y las exporta con el `ExportService` **real**, muestreando working-set y GC heap durante la corrida.

## Uso

```bash
# 10M filas a CSV (por defecto)
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv

# 1M filas a XLSX (bajo el límite de 1.048.576/hoja)
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 1000000 --format xlsx --out /tmp/probe.xlsx
```

Args: `--rows N` · `--format csv|xlsx` · `--out ruta`.

## Criterio de aceptación

`PASS` si el **peak working set < 500 MB** independientemente del nº de filas.
El probe imprime: filas, bytes, duración, filas/s, peak working set, peak GC heap, colecciones GC gen0/1/2.

## Resultados de referencia (Apple Silicon, Server GC)

| Caso | Filas | Archivo | Peak WS | filas/s |
|------|-------|---------|---------|---------|
| CSV  | 10.000.000 | 491 MB | ~126 MB | ~3.9M |
| XLSX | 1.000.000  | 35 MB  | ~152 MB | ~0.4M |
| XLSX | 10.000.000 | — | aborta en 1.048.576 (límite de hoja, por diseño) | — |

La memoria **no crece** con el nº de filas: es la prueba del pipeline de streaming.

## Ruta Oracle real

Para probar contra Oracle (no sintético): sembrar con `scripts/seed_10m.sql` y exportar con la CLI:

```bash
hexporter export --table HEXPORTER_STRESS --format csv --out /tmp/stress.csv
```
````

## File: HExporter/tools/HExporter.MemProbe/SyntheticRecordReader.cs
````csharp
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
````

## File: HExporter/.dockerignore
````
**/bin/
**/obj/
.git/
.tokensave/
BenchmarkDotNet.Artifacts/
*.env*
````

## File: HExporter/.gitignore
````
### Csharp ###
## Ignore Visual Studio temporary files, build results, and
## files generated by popular Visual Studio add-ons.
##
## Get latest from https://github.com/github/gitignore/blob/main/VisualStudio.gitignore

# User-specific files
*.rsuser
*.suo
*.user
*.userosscache
*.sln.docstates

# User-specific files (MonoDevelop/Xamarin Studio)
*.userprefs

# Mono auto generated files
mono_crash.*

# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
[Rr]eleases/
x64/
x86/
[Ww][Ii][Nn]32/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
bld/
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/

# Visual Studio 2015/2017 cache/options directory
.vs/
# Uncomment if you have tasks that create the project's static files in wwwroot
#wwwroot/

# Visual Studio 2017 auto generated files
Generated\ Files/

# MSTest test Results
[Tt]est[Rr]esult*/
[Bb]uild[Ll]og.*

# NUnit
*.VisualState.xml
TestResult.xml
nunit-*.xml

# Build Results of an ATL Project
[Dd]ebugPS/
[Rr]eleasePS/
dlldata.c

# Benchmark Results
BenchmarkDotNet.Artifacts/

# .NET Core
project.lock.json
project.fragment.lock.json
artifacts/

# ASP.NET Scaffolding
ScaffoldingReadMe.txt

# StyleCop
StyleCopReport.xml

# Files built by Visual Studio
*_i.c
*_p.c
*_h.h
*.ilk
*.meta
*.obj
*.iobj
*.pch
*.pdb
*.ipdb
*.pgc
*.pgd
*.rsp
*.sbr
*.tlb
*.tli
*.tlh
*.tmp
*.tmp_proj
*_wpftmp.csproj
*.log
*.tlog
*.vspscc
*.vssscc
.builds
*.pidb
*.svclog
*.scc

# Chutzpah Test files
_Chutzpah*

# Visual C++ cache files
ipch/
*.aps
*.ncb
*.opendb
*.opensdf
*.sdf
*.cachefile
*.VC.db
*.VC.VC.opendb

# Visual Studio profiler
*.psess
*.vsp
*.vspx
*.sap

# Visual Studio Trace Files
*.e2e

# TFS 2012 Local Workspace
$tf/

# Guidance Automation Toolkit
*.gpState

# ReSharper is a .NET coding add-in
_ReSharper*/
*.[Rr]e[Ss]harper
*.DotSettings.user

# TeamCity is a build add-in
_TeamCity*

# DotCover is a Code Coverage Tool
*.dotCover

# AxoCover is a Code Coverage Tool
.axoCover/*
!.axoCover/settings.json

# Coverlet is a free, cross platform Code Coverage Tool
coverage*.json
coverage*.xml
coverage*.info

# Visual Studio code coverage results
*.coverage
*.coveragexml

# NCrunch
_NCrunch_*
.*crunch*.local.xml
nCrunchTemp_*

# MightyMoose
*.mm.*
AutoTest.Net/

# Web workbench (sass)
.sass-cache/

# Installshield output folder
[Ee]xpress/

# DocProject is a documentation generator add-in
DocProject/buildhelp/
DocProject/Help/*.HxT
DocProject/Help/*.HxC
DocProject/Help/*.hhc
DocProject/Help/*.hhk
DocProject/Help/*.hhp
DocProject/Help/Html2
DocProject/Help/html

# Click-Once directory
publish/

# Publish Web Output
*.[Pp]ublish.xml
*.azurePubxml
# Note: Comment the next line if you want to checkin your web deploy settings,
# but database connection strings (with potential passwords) will be unencrypted
*.pubxml
*.publishproj

# Microsoft Azure Web App publish settings. Comment the next line if you want to
# checkin your Azure Web App publish settings, but sensitive information contained
# in these scripts will be unencrypted
PublishScripts/

# NuGet Packages
*.nupkg
# NuGet Symbol Packages
*.snupkg
# The packages folder can be ignored because of Package Restore
**/[Pp]ackages/*
# except build/, which is used as an MSBuild target.
!**/[Pp]ackages/build/
# Uncomment if necessary however generally it will be regenerated when needed
#!**/[Pp]ackages/repositories.config
# NuGet v3's project.json files produces more ignorable files
*.nuget.props
*.nuget.targets

# Microsoft Azure Build Output
csx/
*.build.csdef

# Microsoft Azure Emulator
ecf/
rcf/

# Windows Store app package directories and files
AppPackages/
BundleArtifacts/
Package.StoreAssociation.xml
_pkginfo.txt
*.appx
*.appxbundle
*.appxupload

# Visual Studio cache files
# files ending in .cache can be ignored
*.[Cc]ache
# but keep track of directories ending in .cache
!?*.[Cc]ache/

# Others
ClientBin/
~$*
*~
*.dbmdl
*.dbproj.schemaview
*.jfm
*.pfx
*.publishsettings
orleans.codegen.cs

# Including strong name files can present a security risk
# (https://github.com/github/gitignore/pull/2483#issue-259490424)
#*.snk

# Since there are multiple workflows, uncomment next line to ignore bower_components
# (https://github.com/github/gitignore/pull/1529#issuecomment-104372622)
#bower_components/

# RIA/Silverlight projects
Generated_Code/

# Backup & report files from converting an old project file
# to a newer Visual Studio version. Backup files are not needed,
# because we have git ;-)
_UpgradeReport_Files/
Backup*/
UpgradeLog*.XML
UpgradeLog*.htm
ServiceFabricBackup/
*.rptproj.bak

# SQL Server files
*.mdf
*.ldf
*.ndf

# Business Intelligence projects
*.rdl.data
*.bim.layout
*.bim_*.settings
*.rptproj.rsuser
*- [Bb]ackup.rdl
*- [Bb]ackup ([0-9]).rdl
*- [Bb]ackup ([0-9][0-9]).rdl

# Microsoft Fakes
FakesAssemblies/

# GhostDoc plugin setting file
*.GhostDoc.xml

# Node.js Tools for Visual Studio
.ntvs_analysis.dat
node_modules/

# Visual Studio 6 build log
*.plg

# Visual Studio 6 workspace options file
*.opt

# Visual Studio 6 auto-generated workspace file (contains which files were open etc.)
*.vbw

# Visual Studio 6 auto-generated project file (contains which files were open etc.)
*.vbp

# Visual Studio 6 workspace and project file (working project files containing files to include in project)
*.dsw
*.dsp

# Visual Studio 6 technical files

# Visual Studio LightSwitch build output
**/*.HTMLClient/GeneratedArtifacts
**/*.DesktopClient/GeneratedArtifacts
**/*.DesktopClient/ModelManifest.xml
**/*.Server/GeneratedArtifacts
**/*.Server/ModelManifest.xml
_Pvt_Extensions

# Paket dependency manager
.paket/paket.exe
paket-files/

# FAKE - F# Make
.fake/

# CodeRush personal settings
.cr/personal

# Python Tools for Visual Studio (PTVS)
__pycache__/
*.pyc

# Cake - Uncomment if you are using it
# tools/**
# !tools/packages.config

# Tabs Studio
*.tss

# Telerik's JustMock configuration file
*.jmconfig

# BizTalk build output
*.btp.cs
*.btm.cs
*.odx.cs
*.xsd.cs

# OpenCover UI analysis results
OpenCover/

# Azure Stream Analytics local run output
ASALocalRun/

# MSBuild Binary and Structured Log
*.binlog

# NVidia Nsight GPU debugger configuration file
*.nvuser

# MFractors (Xamarin productivity tool) working folder
.mfractor/

# Local History for Visual Studio
.localhistory/

# Visual Studio History (VSHistory) files
.vshistory/

# BeatPulse healthcheck temp database
healthchecksdb

# Backup folder for Package Reference Convert tool in Visual Studio 2017
MigrationBackup/

# Ionide (cross platform F# VS Code tools) working folder
.ionide/

# Fody - auto-generated XML schema
FodyWeavers.xsd

# VS Code files for those working on multiple tools
.vscode/*
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json
*.code-workspace

# Local History for Visual Studio Code
.history/

# Windows Installer files from build outputs
*.cab
*.msi
*.msix
*.msm
*.msp

# JetBrains Rider
*.sln.iml

### dotenv ###
.env

### DotnetCore ###
# .NET Core build folders
bin/
obj/

# Common node modules locations
/node_modules
/wwwroot/node_modules

### Git ###
# Created by git for backups. To disable backups in Git:
# $ git config --global mergetool.keepBackup false
*.orig

# Created by git when using merge tools for conflicts
*.BACKUP.*
*.BASE.*
*.LOCAL.*
*.REMOTE.*
*_BACKUP_*.txt
*_BASE_*.txt
*_LOCAL_*.txt
*_REMOTE_*.txt

### Linux ###

# temporary files which can be created if a process still has a handle open of a deleted file
.fuse_hidden*

# KDE directory preferences
.directory

# Linux trash folder which might appear on any partition or disk
.Trash-*

# .nfs files are created when an open file is removed but is still being accessed
.nfs*

### macOS ###
# General
.DS_Store
.AppleDouble
.LSOverride

# Icon must end with two \r
Icon


# Thumbnails
._*

# Files that might appear in the root of a volume
.DocumentRevisions-V100
.fseventsd
.Spotlight-V100
.TemporaryItems
.Trashes
.VolumeIcon.icns
.com.apple.timemachine.donotpresent

# Directories potentially created on remote AFP share
.AppleDB
.AppleDesktop
Network Trash Folder
Temporary Items
.apdisk

### macOS Patch ###
# iCloud generated files
*.icloud

### Windows ###
# Windows thumbnail cache files
Thumbs.db
Thumbs.db:encryptable
ehthumbs.db
ehthumbs_vista.db

# Dump file
*.stackdump

# Folder config file
[Dd]esktop.ini

# Recycle Bin used on file shares
$RECYCLE.BIN/

# Windows Installer files

# Windows shortcuts
*.lnk

# tokensave MCP tool (local code-graph cache/db)
.tokensave/
````

## File: HExporter/CLAUDE.md
````markdown
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
````

## File: HExporter/Directory.Build.props
````
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
</Project>
````

## File: HExporter/Dockerfile
````dockerfile
# Build framework-dependent (Oracle.ManagedDataAccess.Core no es trim-safe; sin single-file aquí).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/HExporter.Cli/HExporter.Cli.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# Credenciales por env var (nunca en la imagen): HEXPORTER_Oracle__ConnectionString.
# Montar un volumen en /out para recibir el archivo exportado (--out /out/archivo.csv).
VOLUME ["/out"]

ENTRYPOINT ["dotnet", "hexporter.dll"]
````

## File: HExporter/env.example
````
# Copy to `.env` and fill in. NEVER commit `.env` with real credentials.
# Convention: HEXPORTER_<Section>__<Key> (double underscore = hierarchical config separator).
# See docs/05-configuration.md §2 and docs/06-nfr-ops.md §Security.

# Database engine: oracle | postgres. Default: oracle. One-off override with --db-engine.
HEXPORTER_Database__Engine=oracle

# Oracle connection string. Prefer Oracle Wallet in production (no plaintext password).
HEXPORTER_Oracle__ConnectionString=User Id=rpt;Password=CHANGEME;Data Source=host:1521/service

# Bytes per network batch of the driver (~256KB-1MB). Not the full result — see docs/04-streaming-strategy.md.
HEXPORTER_Oracle__FetchSizeBytes=1048576

# 0 = no limit (long-running reports).
HEXPORTER_Oracle__CommandTimeoutSeconds=0

HEXPORTER_Oracle__BindByName=true

# PostgreSQL connection string (only if HEXPORTER_Database__Engine=postgres).
HEXPORTER_Postgres__ConnectionString=Host=host;Port=5432;Username=rpt;Password=CHANGEME;Database=reporting

# 0 = no limit (long-running reports).
HEXPORTER_Postgres__CommandTimeoutSeconds=0
````

## File: HExporter/HExporter.slnx
````
<Solution>
  <Folder Name="/src/">
    <Project Path="src/HExporter.Application/HExporter.Application.csproj" />
    <Project Path="src/HExporter.Cli/HExporter.Cli.csproj" />
    <Project Path="src/HExporter.Core/HExporter.Core.csproj" />
    <Project Path="src/HExporter.Export/HExporter.Export.csproj" />
    <Project Path="src/HExporter.Infrastructure/HExporter.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/HExporter.IntegrationTests/HExporter.IntegrationTests.csproj" />
    <Project Path="tests/HExporter.UnitTests/HExporter.UnitTests.csproj" />
  </Folder>
  <Folder Name="/tools/">
    <Project Path="tools/HExporter.Benchmarks/HExporter.Benchmarks.csproj" />
    <Project Path="tools/HExporter.MemProbe/HExporter.MemProbe.csproj" />
  </Folder>
</Solution>
````

## File: HExporter/README.md
````markdown
# HExporter

Large-volume report exporter from **Oracle** or **PostgreSQL** to **CSV** or **XLSX**, writing **directly from the database to the file via streaming**, without loading the result into memory.

Designed for reports with millions of rows with **constant and bounded** memory usage, independent of the result size — prevents the machine from freezing or crashing due to memory pressure (OOM / GC pauses).

## Why

Generating reports by loading the entire result into memory (`DataTable`, lists, or Excel libraries that build the whole workbook in RAM) makes RAM grow with the report size → `OutOfMemoryException` and freezes with high volumes. HExporter streams **one row at a time** from the database cursor to the file, with O(1) memory relative to the number of rows.

## How it works

```
Oracle / PostgreSQL (server-side cursor, bounded network batch)
  → 1 live row in the process
  → writer with buffer + periodic flush
  → file (CSV / XLSX)
```

The full result set is never materialized. Details in [`docs/04-streaming-strategy.md`](./docs/04-streaming-strategy.md).

## Verified performance

Memory test with 10M synthetic rows (Apple Silicon, Server GC):

| Format | Rows | File | Peak memory | Throughput |
|---------|-------|---------|--------------|-----------|
| CSV | 10,000,000 | 491 MB | **~126 MB** | ~3.9M rows/s |
| XLSX | 1,000,000 | 35 MB | **~152 MB** | ~0.4M rows/s |

Memory **does not grow** with the number of rows. To reproduce: see [MemProbe](#memory-test).

## Stack

- **.NET 10** (LTS), C#
- `Oracle.ManagedDataAccess.Core` — 100% managed driver, no native client (cross-platform)
- `Npgsql` — 100% managed PostgreSQL driver
- `MiniExcel` — streaming XLSX
- `System.CommandLine` — CLI · `Serilog` — logging · `Microsoft.Extensions.Hosting` — DI/config

## Requirements

- .NET 10 SDK
- Access to an Oracle or PostgreSQL database (account with `SELECT` on the objects to export)

## Build and test

```bash
dotnet build
dotnet test tests/HExporter.UnitTests
```

## Usage

```bash
# Full table to CSV
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv

# Parameterized query to XLSX (bind variables)
hexporter export \
  --sql "SELECT * FROM ventas WHERE fecha >= :d" \
  --bind d=2026-01-01 --format xlsx --out ventas.xlsx --sheet Ventas

# By declarative profile, overriding a bind
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28

# To stdout, piped with gzip (CSV only)
hexporter export --table LOGS --format csv --out - | gzip > logs.csv.gz
```

Running from source:
```bash
dotnet run --project src/HExporter.Cli -- --help
```

### Options

| Option | Description |
|--------|-------------|
| `--sql <sql>` | SELECT query to export. |
| `--sql-file <path>` | Path to a `.sql` file with the query to export. |
| `--table <table>` | Table/view to export (`SELECT *`). |
| `--profile <path>` | Path to a `report.json`. |
| `--format csv\|xlsx` | Output format (default `csv`). |
| `--out <path>` | Destination file (`-` = stdout, CSV only). |
| `--bind k=v` | Bind variable (repeatable). |
| `--delimiter <char>` | CSV delimiter (default `,`). |
| `--no-headers` | Omit headers. |
| `--sheet <name>` | XLSX sheet name (default `Data`). |
| `--flush-every <n>` | Rows between flushes (default `10000`). |
| `--env-file <path>` | Path to an alternate `.env` file (default: `.env` in the current directory). |
| `--db-engine oracle\|postgres` | Database engine. Overrides `HEXPORTER_Database__Engine` / `appsettings.json` (default `oracle`). |

One of `--sql`, `--sql-file`, `--table`, or `--profile` is required. Full reference: [`docs/05-configuration.md`](./docs/05-configuration.md).

## Configuration

Connection string and options via `appsettings.json` or environment variables. **Never** hardcode credentials — use env vars, User Secrets, or Oracle Wallet:

```bash
export HEXPORTER_Oracle__ConnectionString="User Id=rpt;Password=***;Data Source=..."
```

### Database engine

Default is **Oracle**. Select PostgreSQL via `--db-engine postgres`, `HEXPORTER_Database__Engine=postgres` (env var or `.env`), or `Database:Engine` in `appsettings.json` — precedence: CLI > real env var > `.env` > `appsettings.json`.

```bash
export HEXPORTER_Database__Engine=postgres
export HEXPORTER_Postgres__ConnectionString="Host=host;Port=5432;Username=rpt;Password=***;Database=reporting"
hexporter --table public.orders --format csv --out orders.csv
```

An unrecognized `--db-engine` value fails fast with exit code 1 and the list of valid values (`oracle`, `postgres`).

## Report profile (`report.json`)

```json
{
  "name": "ventas_mensuales",
  "sql": "SELECT id, fecha, monto, cliente FROM ventas WHERE fecha BETWEEN :desde AND :hasta",
  "binds": { "desde": "2026-01-01", "hasta": "2026-01-31" },
  "format": "xlsx",
  "xlsx": { "sheetName": "Ventas" }
}
```

## Memory test

Validates the project's core goal (flat memory) without needing Oracle — uses a synthetic row generator:

```bash
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv
```

For the real Oracle path: seed with [`scripts/seed_10m.sql`](./scripts/seed_10m.sql) and export the `HEXPORTER_STRESS` table. Details in [`tools/HExporter.MemProbe/README.md`](./tools/HExporter.MemProbe/README.md).

## Repository structure

```
src/
  HExporter.Core            ports + models (no dependencies)
  HExporter.Application     ExportService (orchestration), validation, profiles
  HExporter.Infrastructure  Oracle / PostgreSQL adapters (streaming reader)
  HExporter.Export          CSV / XLSX writers
  HExporter.Cli             CLI (hexporter)
tools/HExporter.MemProbe    memory / volume test
tests/                      unit + integration (Testcontainers Oracle)
docs/                       architecture design (00–08 + ADRs)
scripts/                    seed SQL for tests
```

## Packaging and distribution

**Framework-dependent** (requires .NET 10 runtime installed on the target):

```bash
dotnet publish src/HExporter.Cli -c Release -o ./publish
```

**Self-contained single-file** (does not require .NET installed; includes the runtime):

```bash
dotnet publish src/HExporter.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -o ./publish
# Alternative RIDs: win-x64, osx-arm64, osx-x64, linux-arm64
```

Without `PublishTrimmed`: `Oracle.ManagedDataAccess.Core` uses reflection extensively and
is not trim-safe (trimming it can break driver loading at runtime).

**Docker** (framework-dependent image, `mcr.microsoft.com/dotnet/runtime:10.0` runtime):

```bash
docker build -t hexporter .
docker run --rm \
  -e HEXPORTER_Oracle__ConnectionString="user/pass@host:1521/service" \
  -v "$(pwd)/out:/out" \
  hexporter export --table VENTAS.PEDIDOS --format csv --out /out/pedidos.csv
```

Credentials only via environment variable (never hardcoded or baked into the image, see
`docs/06-nfr-ops.md`). The `/out` volume receives the exported file.

## Known limits

- XLSX: max **1,048,576 rows per sheet** (format limit). For larger volumes use CSV, or `RowLimitStrategy=NewSheet`.
- v1 is not resumable after a connection drop (the report is re-run).

## Documentation

Full design in [`docs/`](./docs/README.md): vision, architecture, technical design, streaming strategy, configuration, NFR/security, testing, backlog, and ADRs. Contribution guidelines in [`CLAUDE.md`](./CLAUDE.md).
````
