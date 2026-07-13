# HExporter

Exportador de reportes de gran volumen desde **Oracle** a **CSV** o **XLSX**, escribiendo **directamente de la base al archivo mediante streaming**, sin cargar el resultado en memoria.

Diseñado para reportes de millones de filas con un consumo de memoria **constante y acotado**, independiente del tamaño del resultado — evita que el equipo se congele o colapse por presión de memoria (OOM / pausas de GC).

## Por qué

Generar reportes cargando todo el resultado en memoria (`DataTable`, listas, o librerías de Excel que arman el workbook completo) hace que la RAM crezca con el tamaño del reporte → `OutOfMemoryException` y congelamientos con volúmenes altos. HExporter transmite **una fila a la vez** desde el cursor de Oracle hasta el archivo, con memoria O(1) respecto al nº de filas.

## Cómo funciona

```
Oracle (cursor server-side)
  → FetchSize (lote de red acotado)
  → 1 fila viva en el proceso
  → writer con buffer + flush periódico
  → archivo (CSV / XLSX)
```

Nunca se materializa el conjunto completo. Detalle en [`docs/04-streaming-strategy.md`](./docs/04-streaming-strategy.md).

## Rendimiento verificado

Prueba de memoria con 10M filas sintéticas (Apple Silicon, Server GC):

| Formato | Filas | Archivo | Peak memoria | Throughput |
|---------|-------|---------|--------------|-----------|
| CSV | 10.000.000 | 491 MB | **~126 MB** | ~3.9M filas/s |
| XLSX | 1.000.000 | 35 MB | **~152 MB** | ~0.4M filas/s |

La memoria **no crece** con el nº de filas. Reproducir: ver [MemProbe](#prueba-de-memoria).

## Stack

- **.NET 10** (LTS), C#
- `Oracle.ManagedDataAccess.Core` — driver 100% managed, sin cliente nativo (cross-platform)
- `MiniExcel` — XLSX en streaming
- `System.CommandLine` — CLI · `Serilog` — logging · `Microsoft.Extensions.Hosting` — DI/config

## Requisitos

- SDK de .NET 10
- Acceso a una base Oracle (cuenta con `SELECT` sobre los objetos a exportar)

## Compilar y probar

```bash
dotnet build
dotnet test tests/HExporter.UnitTests
```

## Uso

```bash
# Tabla completa a CSV
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv

# Consulta parametrizada a XLSX (bind variables)
hexporter export \
  --sql "SELECT * FROM ventas WHERE fecha >= :d" \
  --bind d=2026-01-01 --format xlsx --out ventas.xlsx --sheet Ventas

# Por perfil declarativo, sobreescribiendo un bind
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28

# A stdout, encadenado con gzip (solo CSV)
hexporter export --table LOGS --format csv --out - | gzip > logs.csv.gz
```

Ejecutando desde el código fuente:
```bash
dotnet run --project src/HExporter.Cli -- --help
```

### Opciones principales

| Opción | Descripción |
|--------|-------------|
| `--sql` / `--table` / `--profile` | Origen (consulta, tabla, o perfil `report.json`) |
| `--format csv\|xlsx` | Formato de salida (def. csv) |
| `--out <ruta>` | Destino (`-` = stdout, solo CSV) |
| `--bind k=v` | Bind variable (repetible) |
| `--delimiter` | Delimitador CSV (def. `,`) |
| `--sheet` | Nombre de hoja XLSX (def. `Datos`) |
| `--flush-every` | Filas entre flushes (def. 10.000) |
| `--no-headers` | Omite encabezados |

Referencia completa: [`docs/05-configuration.md`](./docs/05-configuration.md).

## Configuración

Cadena de conexión y opciones vía `appsettings.json` o variables de entorno. **Nunca** hardcodear credenciales — usar env, User Secrets u Oracle Wallet:

```bash
export HEXPORTER_Oracle__ConnectionString="User Id=rpt;Password=***;Data Source=..."
```

## Perfil de reporte (`report.json`)

```json
{
  "name": "ventas_mensuales",
  "sql": "SELECT id, fecha, monto, cliente FROM ventas WHERE fecha BETWEEN :desde AND :hasta",
  "binds": { "desde": "2026-01-01", "hasta": "2026-01-31" },
  "format": "xlsx",
  "xlsx": { "sheetName": "Ventas" }
}
```

## Prueba de memoria

Valida el objetivo del proyecto (memoria plana) sin necesidad de Oracle — usa un generador de filas sintéticas:

```bash
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv
```

Para la ruta Oracle real: sembrar con [`scripts/seed_10m.sql`](./scripts/seed_10m.sql) y exportar la tabla `HEXPORTER_STRESS`. Detalle en [`tools/HExporter.MemProbe/README.md`](./tools/HExporter.MemProbe/README.md).

## Estructura del repositorio

```
src/
  HExporter.Core            puertos + modelos (sin dependencias)
  HExporter.Application     ExportService (orquestación), validación, perfiles
  HExporter.Infrastructure  adaptador Oracle (lectura streaming)
  HExporter.Export          writers CSV / XLSX
  HExporter.Cli             CLI (hexporter)
tools/HExporter.MemProbe    prueba de memoria / volumen
tests/                      unitarias + integración (Testcontainers Oracle)
docs/                       diseño de arquitectura (00–08 + ADRs)
scripts/                    seed SQL para pruebas
```

## Empaquetado y distribución

**Framework-dependent** (requiere .NET 10 runtime instalado en destino):

```bash
dotnet publish src/HExporter.Cli -c Release -o ./publish
```

**Self-contained single-file** (no requiere .NET instalado; incluye el runtime):

```bash
dotnet publish src/HExporter.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -o ./publish
# RIDs alternativos: win-x64, osx-arm64, osx-x64, linux-arm64
```

Sin `PublishTrimmed`: `Oracle.ManagedDataAccess.Core` usa reflection extensivamente y
no es trim-safe (recortarlo puede romper la carga del driver en runtime).

**Docker** (imagen framework-dependent, runtime `mcr.microsoft.com/dotnet/runtime:10.0`):

```bash
docker build -t hexporter .
docker run --rm \
  -e HEXPORTER_Oracle__ConnectionString="user/pass@host:1521/service" \
  -v "$(pwd)/out:/out" \
  hexporter export --table VENTAS.PEDIDOS --format csv --out /out/pedidos.csv
```

Credenciales solo por variable de entorno (nunca hardcodeadas ni en la imagen, ver
`docs/06-nfr-ops.md`). El volumen `/out` recibe el archivo exportado.

## Límites conocidos

- XLSX: máx **1.048.576 filas por hoja** (límite del formato). Para volúmenes mayores usar CSV, o `RowLimitStrategy=NewSheet`.
- v1 no reanudable tras un corte de conexión (se re-ejecuta el reporte).

## Documentación

Diseño completo en [`docs/`](./docs/README.md): visión, arquitectura, diseño técnico, estrategia de streaming, configuración, NFR/seguridad, pruebas, backlog y ADRs. Lineamientos para contribuir en [`CLAUDE.md`](./CLAUDE.md).
