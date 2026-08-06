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
