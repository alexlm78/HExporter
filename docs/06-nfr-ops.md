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
