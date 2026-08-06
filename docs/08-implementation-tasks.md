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
