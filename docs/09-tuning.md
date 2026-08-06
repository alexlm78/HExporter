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
