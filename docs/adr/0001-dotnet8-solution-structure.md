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
