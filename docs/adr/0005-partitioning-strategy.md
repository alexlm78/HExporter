# ADR-0005 — Estrategia de particionado para volúmenes masivos

**Estado:** Diferido (v2) · **Fecha:** 2026-07-13

## Contexto

Dos motivos para particionar la salida en varios archivos/hojas:
1. **Límite duro de XLSX:** 1.048.576 filas por hoja.
2. **Ergonomía/entrega:** archivos muy grandes son difíciles de abrir/transferir; puede convenir trocear (por tamaño, por nº de filas o por clave).

## Decisión (v1)

- v1 soporta particionado **solo a nivel de hoja XLSX** (`RowLimitStrategy=NewSheet`, opcional) y por defecto `Fail` al exceder el límite.
- Particionado multi-archivo (`reporte_0001.csv/xlsx`, `_0002`, …) se **difiere a v2**.

## Diseño propuesto (v2)

- `PartitionStrategy`: `None` | `ByRowCount(n)` | `BySizeBytes(n)` | `ByKeyColumn(col)`.
- El `ExportService` rota el `Stream`/writer al cruzar el umbral, manteniendo el streaming.
- Nomenclatura con índice cero-padded + manifiesto opcional (`manifest.json` con nº de partes, filas por parte, checksums).

## Consecuencias

- ✅ v1 se mantiene simple y enfocada en el objetivo de memoria.
- ➖ Reportes XLSX > 1M filas deben usar CSV en v1 (documentado en [04](../04-streaming-strategy.md) §6).
