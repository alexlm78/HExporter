# 05 — Configuration and CLI

## 1. `appsettings.json`

```json
{
  "Database": {
    "Engine": "Oracle"
  },
  "Oracle": {
    "ConnectionStringName": "Reporting",
    "FetchSizeBytes": 1048576,
    "CommandTimeoutSeconds": 0,
    "BindByName": true,
    "ConnectRetryAttempts": 3,
    "ConnectRetryBaseDelaySeconds": 2.0
  },
  "Postgres": {
    "ConnectionString": "",
    "CommandTimeoutSeconds": 0,
    "ConnectRetryAttempts": 3,
    "ConnectRetryBaseDelaySeconds": 2.0
  },
  "Export": {
    "DefaultEncoding": "utf-8",
    "FlushEveryRows": 10000,
    "FileBufferBytes": 131072
  },
  "Csv": {
    "Delimiter": ",",
    "IncludeHeaders": true,
    "WriteBom": false,
    "QuoteMode": "Minimal",
    "DateFormat": "yyyy-MM-dd HH:mm:ss",
    "Culture": "en-US"
  },
  "Xlsx": {
    "SheetName": "Datos",
    "IncludeHeaders": true,
    "RowLimitStrategy": "Fail"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/hexporter-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

`Oracle.ConnectRetryAttempts` / `Postgres.ConnectRetryAttempts` (Polly): retries with exponential backoff on a transient failure while **opening** the connection (listener down, network). `0` = disabled. Does not retry on cancellation (Ctrl+C) or configuration errors (empty connection string).

### 1.1 Database engine selection

`Database:Engine` selects the active adapter: `Oracle` (default) or `Postgres`. Only one `IRecordReaderFactory` is wired per run — resolved once at startup, before the DI container is built. Precedence (highest wins): `--db-engine` CLI flag > real process env var (`HEXPORTER_Database__Engine`) > `[dot]env` file > `appsettings.json`. An unrecognized value throws immediately (exit code 1) listing the valid options. When `Postgres` is selected, `Oracle:*` settings are simply unused (and vice versa) — no cross-validation between sections.

## 2. Secrets / credentials

**Never** hardcode the connection string or commit a `[dot]env` file with real credentials (see `env.example` at the repo root, a template without secrets). Precedence order (highest wins):

1. **CLI** — one-off run parameters (`--sql`, `--table`, `--bind`, `--format`, `--out`, etc.).
2. **Real process environment variables** — `HEXPORTER_Oracle__ConnectionString`, `HEXPORTER_Oracle__FetchSizeBytes`, etc. (`HEXPORTER_` prefix, `__` as hierarchical separator).
3. **`[dot]env` file** — loaded by `HExporter.Cli` at startup (`DotNetEnv`) if present in the current directory, or at the path given by `--env-file <path>`. If a variable already exists in the process's real environment, the file **does not override it** (this preserves precedence order 2 > 3).
4. **`appsettings.json`** — non-sensitive defaults (`FetchSizeBytes`, `CommandTimeoutSeconds`, etc.).

`--env-file` with a nonexistent path is an **argument error** (exit code 1); without the option, the file is optional — its absence does not fail, it simply contributes no values.

In production, prefer **Oracle Wallet** (external authentication, no plaintext password) injected via real environment variables from the orchestrator (Kubernetes Secret, Vault, etc.), not a `[dot]env` file on disk. See [06-nfr-ops.md](./06-nfr-ops.md) §Security.

## 3. Report profile (`report.json`)

Reusable declarative definition of a report:

```json
{
  "name": "ventas_mensuales",
  "sql": "SELECT id, fecha, monto, cliente FROM ventas WHERE fecha BETWEEN :desde AND :hasta",
  "binds": { "desde": "2026-01-01", "hasta": "2026-01-31" },
  "format": "xlsx",
  "csv": { "delimiter": ";" },
  "xlsx": { "sheetName": "Ventas" }
}
```

`binds` can be overridden on the CLI (`--bind desde=2026-02-01`).

## 4. CLI interface

```
hexporter export [options]

Options:
  --sql <text>             SELECT query to export. (mutually exclusive with --table and --profile)
  --table <owner.table>    Exports the full table/view (SELECT *).
  --profile <path>         Path to a report.json.
  --format <csv|xlsx>      Output format. (default: csv)
  --out <path>             Destination file. '-' = stdout (csv only).
  --bind <k=v>             Bind variable (repeatable). E.g.: --bind desde=2026-01-01
  --delimiter <char>       CSV delimiter. (default: ,)
  --no-headers             Skips the header row.
  --encoding <name>        utf-8 | utf-8-bom | latin1. (default: utf-8)
  --flush-every <n>        Rows between flushes. (default: 10000)
  --fetch-size <bytes>     Oracle driver FetchSize. (default: 1048576)
  --sheet <name>           XLSX sheet name. (default: Data)
  --env-file <path>        Alternate [dot]env file (default: [dot]env in current directory, optional).
  --db-engine <name>       oracle | postgres. (default: oracle; see §1.1)
  -v, --verbose            Verbose logging.

Exit codes:
  0  Success
  1  Validation / argument error
  2  Oracle connection / SQL error
  3  Write / I/O error
  130 Cancelled by the user (Ctrl+C)
```

### Examples

```bash
# Full table to CSV
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv

# Parameterized query to XLSX
hexporter export \
  --sql "SELECT * FROM ventas WHERE fecha >= :d" \
  --bind d=2026-01-01 --format xlsx --out ventas.xlsx --sheet Ventas

# By profile, overriding a bind
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28

# To stdout, piped with gzip
hexporter export --table LOGS --format csv --out - | gzip > logs.csv.gz
```
