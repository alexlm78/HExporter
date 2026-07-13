# 05 — Configuración y CLI

## 1. `appsettings.json`

```json
{
  "Oracle": {
    "ConnectionStringName": "Reporting",
    "FetchSizeBytes": 1048576,
    "CommandTimeoutSeconds": 0,
    "BindByName": true,
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

`Oracle.ConnectRetryAttempts` (Polly): reintentos con backoff exponencial ante fallo transitorio al **abrir** la conexión (listener caído, red). `0` = deshabilitado. No reintenta cancelación (Ctrl+C) ni errores de configuración (connection string vacío).

## 2. Secretos / credenciales

**Nunca** hardcodear la cadena de conexión ni commitear un archivo `[dot]env` con credenciales reales (ver `env.example` en la raíz del repo, plantilla sin secretos). Orden de precedencia (mayor gana):

1. **CLI** — parámetros puntuales de la ejecución (`--sql`, `--table`, `--bind`, `--format`, `--out`, etc.).
2. **Variables de entorno reales del proceso** — `HEXPORTER_Oracle__ConnectionString`, `HEXPORTER_Oracle__FetchSizeBytes`, etc. (prefijo `HEXPORTER_`, `__` como separador jerárquico).
3. **Archivo `[dot]env`** — cargado por `HExporter.Cli` al iniciar (`DotNetEnv`) si existe en el directorio actual, o en la ruta indicada por `--env-file <ruta>`. Si una variable ya existe en el entorno real del proceso, el archivo **no la sobreescribe** (así se cumple el orden 2 > 3).
4. **`appsettings.json`** — valores por defecto no sensibles (`FetchSizeBytes`, `CommandTimeoutSeconds`, etc.).

`--env-file` con ruta inexistente es un **error de argumentos** (exit code 1); sin la opción, el archivo es opcional — su ausencia no falla, simplemente no aporta valores.

En producción, preferir **Oracle Wallet** (autenticación externa, sin password en texto) inyectado vía variables de entorno reales del orquestador (Kubernetes Secret, Vault, etc.), no un archivo `[dot]env` en disco. Ver [06-nfr-ops.md](./06-nfr-ops.md) §Seguridad.

## 3. Perfil de reporte (`report.json`)

Definición declarativa reutilizable de un reporte:

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

Los `binds` pueden sobreescribirse en la CLI (`--bind desde=2026-02-01`).

## 4. Interfaz CLI

```
hexporter export [opciones]

Opciones:
  --sql <texto>            Consulta SELECT a exportar. (excluyente con --table y --profile)
  --table <owner.tabla>    Exporta la tabla/vista completa (SELECT *).
  --profile <ruta>         Ruta a un report.json.
  --format <csv|xlsx>      Formato de salida. (def: csv)
  --out <ruta>             Archivo destino. '-' = stdout (solo csv).
  --bind <k=v>             Bind variable (repetible). Ej: --bind desde=2026-01-01
  --delimiter <char>       Delimitador CSV. (def: ,)
  --no-headers             Omite fila de encabezados.
  --encoding <nombre>      utf-8 | utf-8-bom | latin1. (def: utf-8)
  --flush-every <n>        Filas entre flushes. (def: 10000)
  --fetch-size <bytes>     FetchSize del driver Oracle. (def: 1048576)
  --sheet <nombre>         Nombre de hoja XLSX. (def: Datos)
  --env-file <ruta>        Archivo [dot]env alternativo (def: [dot]env en el directorio actual, opcional).
  -v, --verbose            Log detallado.

Códigos de salida:
  0  Éxito
  1  Error de validación / argumentos
  2  Error de conexión / SQL Oracle
  3  Error de escritura / I/O
  130 Cancelado por el usuario (Ctrl+C)
```

### Ejemplos

```bash
# Tabla completa a CSV
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv

# Consulta parametrizada a XLSX
hexporter export \
  --sql "SELECT * FROM ventas WHERE fecha >= :d" \
  --bind d=2026-01-01 --format xlsx --out ventas.xlsx --sheet Ventas

# Por perfil, sobreescribiendo un bind
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28

# A stdout, encadenado con gzip
hexporter export --table LOGS --format csv --out - | gzip > logs.csv.gz
```
