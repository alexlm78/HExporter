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
