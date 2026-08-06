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
