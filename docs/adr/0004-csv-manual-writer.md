# ADR-0004 — Escritura CSV manual vs. CsvHelper

**Estado:** Aceptado · **Fecha:** 2026-07-13

## Contexto

CSV es el formato principal para volúmenes masivos (sin límite de filas). Necesitamos escritura incremental, control de quoting/encoding y máximo rendimiento en el hot path.

## Decisión

Escribir CSV con **`StreamWriter` directo** + lógica propia de quoting RFC 4180, en `CsvExportWriter`. No se adopta CsvHelper para la escritura.

## Consecuencias

- ✅ Control total del hot path: sin reflexión, sin mapeos por objeto, mínimas asignaciones (permite `ISpanFormattable.TryFormat`).
- ✅ Cero dependencia extra para el camino más caliente.
- ➖ Debemos implementar y testear el quoting/escaping nosotros (cubierto en E4/tests).

## Alternativas descartadas

- **CsvHelper:** excelente para mapeo objeto↔CSV, pero orientado a records tipados; overhead innecesario para un pipeline por `IRecordReader` fila-cruda. Se mantiene como opción si se requiere mapeo complejo.

## Nota

La lógica de quoting es pequeña y estable; el riesgo de implementarla es bajo frente al beneficio de rendimiento y control.
