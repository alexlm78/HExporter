# 07 — Testing Strategy

## 1. Levels

| Level | Scope | Tools |
|-------|---------|--------------|
| Unit | CSV/XLSX writers, validation, quoting, formatting, factory | xUnit, FluentAssertions |
| Integration | `OracleRecordReader` against real Oracle | Testcontainers.Oracle (gvenzl/oracle-free) |
| End-to-end | Full CLI: SQL → file, content verification | CLI process + assertions on file |
| Performance / memory | High volume, flat memory | BenchmarkDotNet + working set measurement |

## 2. Unit — key cases

**CSV:**
- RFC 4180 quoting: value with delimiter, with quotes (`"` → `""`), with line break.
- NULL → empty cell.
- Dates/numbers with fixed `CultureInfo` (independent of host locale).
- Encoding with/without BOM.
- `--no-headers` does not write a header row.

**XLSX:**
- Headers present/absent.
- Types: number, date, text, NULL correct in cells.
- `RowLimitStrategy=Fail` aborts when exceeding 1,048,576.

**Reader (with a double/fake):** correct schema, `ReadAsync` advances, `IsDBNull`.

## 3. Integration (real Oracle)

- Spin up an Oracle Free container, seed a table with a known dataset.
- Verify: mapped schema, bind variables, Oracle types (NUMBER, DATE, TIMESTAMP, VARCHAR2, CLOB) → CLR.
- Verify CLOB streaming with `SequentialAccess`.

## 4. Memory test (mandatory — validates the project's goal)

Goal: demonstrate **O(1)** memory relative to rows.

Procedure:
1. Generate a synthetic dataset of **10M+ rows** (via `CONNECT BY LEVEL` or a seeded table).
2. Run export to CSV and to XLSX.
3. Sample working set / GC heap during the run (dotnet-counters).
4. **Acceptance criterion:** stable memory; no monotonic growth with row count; no `OutOfMemoryException`.

```sql
-- Synthetic dataset generator for testing
SELECT LEVEL id,
       SYSDATE - LEVEL fecha,
       DBMS_RANDOM.VALUE(1,10000) monto,
       'cliente_' || LEVEL nombre
FROM dual CONNECT BY LEVEL <= 10000000;
```

## 5. Performance (BenchmarkDotNet)

- Measure rows/sec per format while varying `FetchSizeBytes` and `FlushEveryRows`.
- Compare boxed vs. typed accessors (justifies the optimization in [04](./04-streaming-strategy.md) §5).
- Record a baseline to detect regressions in CI.

## 6. CI

- `dotnet test` (unit) on every PR.
- Integration with Testcontainers in a nightly pipeline (slower).
- Memory test as a manual/nightly job with trend reporting.
