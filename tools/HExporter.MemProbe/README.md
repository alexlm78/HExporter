# HExporter.MemProbe — Prueba de memoria / volumen

Valida el objetivo del proyecto: **memoria O(1) respecto al nº de filas** (docs/07 §4).
Genera filas sintéticas al vuelo (`SyntheticRecordReader`, sin Oracle) y las exporta con el `ExportService` **real**, muestreando working-set y GC heap durante la corrida.

## Uso

```bash
# 10M filas a CSV (por defecto)
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 10000000 --format csv --out /tmp/probe.csv

# 1M filas a XLSX (bajo el límite de 1.048.576/hoja)
dotnet run -c Release --project tools/HExporter.MemProbe -- --rows 1000000 --format xlsx --out /tmp/probe.xlsx
```

Args: `--rows N` · `--format csv|xlsx` · `--out ruta`.

## Criterio de aceptación

`PASS` si el **peak working set < 500 MB** independientemente del nº de filas.
El probe imprime: filas, bytes, duración, filas/s, peak working set, peak GC heap, colecciones GC gen0/1/2.

## Resultados de referencia (Apple Silicon, Server GC)

| Caso | Filas | Archivo | Peak WS | filas/s |
|------|-------|---------|---------|---------|
| CSV  | 10.000.000 | 491 MB | ~126 MB | ~3.9M |
| XLSX | 1.000.000  | 35 MB  | ~152 MB | ~0.4M |
| XLSX | 10.000.000 | — | aborta en 1.048.576 (límite de hoja, por diseño) | — |

La memoria **no crece** con el nº de filas: es la prueba del pipeline de streaming.

## Ruta Oracle real

Para probar contra Oracle (no sintético): sembrar con `scripts/seed_10m.sql` y exportar con la CLI:

```bash
hexporter export --table HEXPORTER_STRESS --format csv --out /tmp/stress.csv
```
