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
