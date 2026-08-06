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
