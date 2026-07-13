# CLAUDE.md

Guía para agentes (Claude Code) que trabajen en este repositorio.

## Qué es

HExporter exporta reportes de gran volumen desde **Oracle** a **CSV** o **XLSX** mediante **streaming end-to-end**, con memoria **O(1) respecto al nº de filas**. Regla central del proyecto: nunca materializar el resultado completo en memoria.

## Regla de oro (NO romper)

Pipeline: `OracleDataReader` forward-only → **una fila viva** a la vez → writer con buffer acotado + flush periódico → `Stream`. El consumo de RAM depende del ancho de fila y los buffers, **no** del número de filas.

Prohibido (ver `docs/04-streaming-strategy.md`):
- `DataTable` / `DataSet` / `reader.Load()`.
- `ToList()` / `ToArray()` sobre el resultado, o cualquier colección de filas que crezca sin límite.
- Librerías XLSX que arman el workbook completo en memoria (ClosedXML, EPPlus). Se usa **MiniExcel** (streaming).
- Concatenar todo el CSV en un `string`/`StringBuilder`. Se escribe al `Stream`.
- Bufferizar LOBs completos (usar `InitialLOBFetchSize = -1`).

Si una tarea parece requerir romper esto, **parar y preguntar** — probablemente hay otra forma.

## Arquitectura

Clean architecture ligera, puertos y adaptadores. Regla de dependencias: capas externas dependen de internas; `Core` no depende de nadie.

```
Cli → Application → Core ← Infrastructure (Oracle)
                     Core ← Export (Csv/Xlsx)
```

| Proyecto | Rol | Depende de |
|----------|-----|-----------|
| `src/HExporter.Core` | Puertos (`IRecordReader`, `IExportWriter`, `IExportWriterFactory`, `IRecordReaderFactory`, `IProgressSink`) + modelos. **Sin dependencias externas.** | — |
| `src/HExporter.Application` | `ExportService` (orquesta el bombeo), validación, carga de perfiles, DI. | Core |
| `src/HExporter.Infrastructure` | Adaptador Oracle (`OracleRecordReader`, factories). | Core |
| `src/HExporter.Export` | Writers `CsvExportWriter`, `XlsxExportWriter` (MiniExcel), factory. | Core |
| `src/HExporter.Cli` | Entry point, `System.CommandLine`, host DI, Serilog. | todas |
| `tools/HExporter.MemProbe` | Prueba de memoria/volumen (reader sintético, sin DB). | Core, Application, Export |
| `tests/HExporter.UnitTests` | Writers, quoting, validación. | Core, Application, Export |
| `tests/HExporter.IntegrationTests` | Oracle real (Testcontainers). | Core, Infrastructure |

El flujo de exportación vive en `ExportService.ExecuteAsync` (`src/HExporter.Application/ExportService.cs`) — es el corazón; leerlo antes de tocar el pipeline.

## Comandos

```bash
dotnet build                                   # compila la solución (warnings = errores)
dotnet test tests/HExporter.UnitTests          # pruebas unitarias
dotnet run --project src/HExporter.Cli -- --help

# Prueba de memoria (criterio de aceptación del proyecto)
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv
```

CLI de ejemplo:
```bash
hexporter export --table VENTAS.PEDIDOS --format csv --out pedidos.csv
hexporter export --sql "SELECT * FROM ventas WHERE fecha >= :d" --bind d=2026-01-01 --format xlsx --out ventas.xlsx
hexporter export --profile reports/ventas.json --bind hasta=2026-02-28
```

## Convenciones de código

- **.NET 10 (LTS)**, C# `latest`, definido en `Directory.Build.props` (no repetir `TargetFramework` en cada csproj).
- `Nullable=enable`, `ImplicitUsings=enable`, **`TreatWarningsAsErrors=true`** — el build falla con warnings. Mantenerlo verde.
- Async con `CancellationToken` propagado en toda ruta de I/O. Nada de `.Result`/`.Wait()`.
- Nombres de tipos/miembros en inglés; comentarios y mensajes al usuario en español (patrón vigente en el repo). Seguir el estilo del archivo circundante.
- Cultura **fija** (`CultureInfo` explícito) al formatear fechas/números — nunca el locale del host.

## Rendimiento (hot path = por fila)

`WriteRow` corre por cada fila. Evitar en ese camino: asignaciones innecesarias, `string.Format`/interpolación por celda, boxing evitable. Optimizaciones (accesores tipados, `ISpanFormattable.TryFormat`) son incrementales — **medir con MemProbe/BenchmarkDotNet antes**. No cambian la arquitectura.

## Seguridad (no negociable)

- **Credenciales:** nunca hardcodear ni loguear. Resolver por env (`HEXPORTER_Oracle__ConnectionString`), User Secrets o Oracle Wallet. Ver `docs/06-nfr-ops.md`.
- **SQL injection:** parámetros siempre por **bind variables**. `--table` se valida contra regex de identificador (`ExportRequestValidator.IsValidTableName`); nunca concatenar valores de usuario en SQL.
- **Logs:** jamás el contenido de las filas (posible PII). Sí métricas (filas, bytes, duración) + `ExportId` de correlación.
- **Archivo parcial:** se escribe a `destino.tmp` y se renombra atómico al terminar; en fallo/cancelación se borra. No entregar reportes truncados como válidos.

## Límites conocidos

- XLSX: máx **1.048.576 filas/hoja**. `RowLimitStrategy=Fail` (def.) aborta; `NewSheet` reparte. Volúmenes mayores → usar CSV. Multi-archivo diferido a v2 (`docs/adr/0005`).
- v1 no es reanudable tras corte: se re-ejecuta el reporte.

## Documentación

Diseño completo en `docs/` (00–08 + `docs/adr/`). Antes de un cambio de arquitectura, leer el ADR pertinente y **actualizarlo/añadir uno** si la decisión cambia. `docs/04-streaming-strategy.md` es lectura obligada antes de tocar el pipeline.

## Definición de "Hecho"

Build verde (sin warnings) · unit tests verdes · sin regresión de la prueba de memoria (MemProbe PASS) · docs/ADR actualizados si cambió una decisión.
