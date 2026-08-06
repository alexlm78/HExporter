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
