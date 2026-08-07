# HExporter

Large-volume report exporter from **Oracle** or **PostgreSQL** to **CSV** or **XLSX**, writing **directly from the database to the file via streaming**, without loading the result into memory.

Designed for reports with millions of rows with **constant and bounded** memory usage, independent of the result size — prevents the machine from freezing or crashing due to memory pressure (OOM / GC pauses).

## Why

Generating reports by loading the entire result into memory (`DataTable`, lists, or Excel libraries that build the whole workbook in RAM) makes RAM grow with the report size → `OutOfMemoryException` and freezes with high volumes. HExporter streams **one row at a time** from the database cursor to the file, with O(1) memory relative to the number of rows.

## How it works

```
Oracle / PostgreSQL (server-side cursor, bounded network batch)
  → 1 live row in the process
  → writer with buffer + periodic flush
  → file (CSV / XLSX)
```

The full result set is never materialized. Details in [`docs/04-streaming-strategy.md`](./docs/04-streaming-strategy.md).

## Verified performance

Memory test with 10M synthetic rows (Apple Silicon, Server GC):

| Format | Rows | File | Peak memory | Throughput |
|---------|-------|---------|--------------|-----------|
| CSV | 10,000,000 | 491 MB | **~126 MB** | ~3.9M rows/s |
| XLSX | 1,000,000 | 35 MB | **~152 MB** | ~0.4M rows/s |

Memory **does not grow** with the number of rows. To reproduce: see [MemProbe](#memory-test).

## Stack

- **.NET 10** (LTS), C#
- `Oracle.ManagedDataAccess.Core` — 100% managed driver, no native client (cross-platform)
- `Npgsql` — 100% managed PostgreSQL driver
- `MiniExcel` — streaming XLSX
- `System.CommandLine` — CLI · `Serilog` — logging · `Microsoft.Extensions.Hosting` — DI/config

## Requirements

- .NET 10 SDK
- Access to an Oracle or PostgreSQL database (account with `SELECT` on the objects to export)

## Build and test

```bash
dotnet build
dotnet test tests/HExporter.UnitTests
```

## Usage

```bash
# Full table to CSV
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv

# Parameterized query to XLSX (bind variables)
hexporter export \
  --sql "SELECT * FROM ventas WHERE fecha >= :d" \
  --bind d=2026-01-01 --format xlsx --out ventas.xlsx --sheet Ventas

# By declarative profile, overriding a bind
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28

# To stdout, piped with gzip (CSV only)
hexporter export --table LOGS --format csv --out - | gzip > logs.csv.gz
```

Running from source:
```bash
dotnet run --project src/HExporter.Cli -- --help
```

### Options

| Option | Description |
|--------|-------------|
| `--sql <sql>` | SELECT query to export. |
| `--sql-file <path>` | Path to a `.sql` file with the query to export. |
| `--table <table>` | Table/view to export (`SELECT *`). |
| `--profile <path>` | Path to a `report.json`. |
| `--format csv\|xlsx` | Output format (default `csv`). |
| `--out <path>` | Destination file (`-` = stdout, CSV only). |
| `--bind k=v` | Bind variable (repeatable). |
| `--delimiter <char>` | CSV delimiter (default `,`). |
| `--no-headers` | Omit headers. |
| `--sheet <name>` | XLSX sheet name (default `Data`). |
| `--flush-every <n>` | Rows between flushes (default `10000`). |
| `--env-file <path>` | Path to an alternate `.env` file (default: `.env` in the current directory). |
| `--db-engine oracle\|postgres` | Database engine. Overrides `HEXPORTER_Database__Engine` / `appsettings.json` (default `oracle`). |

One of `--sql`, `--sql-file`, `--table`, or `--profile` is required. Full reference: [`docs/05-configuration.md`](./docs/05-configuration.md).

## Configuration

Connection string and options via `appsettings.json` or environment variables. **Never** hardcode credentials — use env vars, User Secrets, or Oracle Wallet:

```bash
export HEXPORTER_Oracle__ConnectionString="User Id=rpt;Password=***;Data Source=..."
```

### Database engine

Default is **Oracle**. Select PostgreSQL via `--db-engine postgres`, `HEXPORTER_Database__Engine=postgres` (env var or `.env`), or `Database:Engine` in `appsettings.json` — precedence: CLI > real env var > `.env` > `appsettings.json`.

```bash
export HEXPORTER_Database__Engine=postgres
export HEXPORTER_Postgres__ConnectionString="Host=host;Port=5432;Username=rpt;Password=***;Database=reporting"
hexporter --table public.orders --format csv --out orders.csv
```

An unrecognized `--db-engine` value fails fast with exit code 1 and the list of valid values (`oracle`, `postgres`).

## Report profile (`report.json`)

```json
{
  "name": "ventas_mensuales",
  "sql": "SELECT id, fecha, monto, cliente FROM ventas WHERE fecha BETWEEN :desde AND :hasta",
  "binds": { "desde": "2026-01-01", "hasta": "2026-01-31" },
  "format": "xlsx",
  "xlsx": { "sheetName": "Ventas" }
}
```

## Memory test

Validates the project's core goal (flat memory) without needing Oracle — uses a synthetic row generator:

```bash
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv
```

For the real Oracle path: seed with [`scripts/seed_10m.sql`](./scripts/seed_10m.sql) and export the `HEXPORTER_STRESS` table. Details in [`tools/HExporter.MemProbe/README.md`](./tools/HExporter.MemProbe/README.md).

## Repository structure

```
src/
  HExporter.Core            ports + models (no dependencies)
  HExporter.Application     ExportService (orchestration), validation, profiles
  HExporter.Infrastructure  Oracle / PostgreSQL adapters (streaming reader)
  HExporter.Export          CSV / XLSX writers
  HExporter.Cli             CLI (hexporter)
tools/HExporter.MemProbe    memory / volume test
tests/                      unit + integration (Testcontainers Oracle)
docs/                       architecture design (00–08 + ADRs)
scripts/                    seed SQL for tests
```

## Packaging and distribution

**Framework-dependent** (requires .NET 10 runtime installed on the target):

```bash
dotnet publish src/HExporter.Cli -c Release -o ./publish
```

**Self-contained single-file** (does not require .NET installed; includes the runtime):

```bash
dotnet publish src/HExporter.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -o ./publish
# Alternative RIDs: win-x64, osx-arm64, osx-x64, linux-arm64
```

Without `PublishTrimmed`: `Oracle.ManagedDataAccess.Core` uses reflection extensively and
is not trim-safe (trimming it can break driver loading at runtime).

**Docker** (framework-dependent image, `mcr.microsoft.com/dotnet/runtime:10.0` runtime):

```bash
docker build -t hexporter .
docker run --rm \
  -e HEXPORTER_Oracle__ConnectionString="user/pass@host:1521/service" \
  -v "$(pwd)/out:/out" \
  hexporter export --table VENTAS.PEDIDOS --format csv --out /out/pedidos.csv
```

Credentials only via environment variable (never hardcoded or baked into the image, see
`docs/06-nfr-ops.md`). The `/out` volume receives the exported file.

## Known limits

- XLSX: max **1,048,576 rows per sheet** (format limit). For larger volumes use CSV, or `RowLimitStrategy=NewSheet`.
- v1 is not resumable after a connection drop (the report is re-run).

## Documentation

Full design in [`docs/`](./docs/README.md): vision, architecture, technical design, streaming strategy, configuration, NFR/security, testing, backlog, and ADRs. Contribution guidelines in [`CLAUDE.md`](./CLAUDE.md).
