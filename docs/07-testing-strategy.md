# 07 — Estrategia de Pruebas

## 1. Niveles

| Nivel | Alcance | Herramientas |
|-------|---------|--------------|
| Unitarias | Writers CSV/XLSX, validación, quoting, formateo, factory | xUnit, FluentAssertions |
| Integración | `OracleRecordReader` contra Oracle real | Testcontainers.Oracle (gvenzl/oracle-free) |
| End-to-end | CLI completa: SQL → archivo, verificación de contenido | Proceso CLI + aserciones sobre archivo |
| Rendimiento / memoria | Volumen alto, memoria plana | BenchmarkDotNet + medición de working set |

## 2. Unitarias — casos clave

**CSV:**
- Quoting RFC 4180: valor con delimitador, con comillas (`"` → `""`), con salto de línea.
- NULL → celda vacía.
- Fechas/números con `CultureInfo` fijo (no depende del locale del host).
- Encoding con/sin BOM.
- `--no-headers` no escribe encabezado.

**XLSX:**
- Encabezados presentes/ausentes.
- Tipos: número, fecha, texto, NULL correctos en celdas.
- `RowLimitStrategy=Fail` aborta al exceder 1.048.576.

**Reader (con doble/fake):** schema correcto, `ReadAsync` avanza, `IsDBNull`.

## 3. Integración (Oracle real)

- Levantar contenedor Oracle Free, sembrar tabla con dataset conocido.
- Verificar: schema mapeado, bind variables, tipos Oracle (NUMBER, DATE, TIMESTAMP, VARCHAR2, CLOB) → CLR.
- Verificar streaming de CLOB con `SequentialAccess`.

## 4. Prueba de memoria (obligatoria — valida el objetivo del proyecto)

Objetivo: demostrar memoria **O(1)** respecto a filas.

Procedimiento:
1. Generar dataset sintético de **10M+ filas** (vía `CONNECT BY LEVEL` o tabla sembrada).
2. Ejecutar export a CSV y a XLSX.
3. Muestrear working set / GC heap durante la corrida (dotnet-counters).
4. **Criterio de aceptación:** memoria estable; sin crecimiento monotónico con el nº de filas; sin `OutOfMemoryException`.

```sql
-- Generador de dataset sintético para pruebas
SELECT LEVEL id,
       SYSDATE - LEVEL fecha,
       DBMS_RANDOM.VALUE(1,10000) monto,
       'cliente_' || LEVEL nombre
FROM dual CONNECT BY LEVEL <= 10000000;
```

## 5. Rendimiento (BenchmarkDotNet)

- Medir filas/seg por formato variando `FetchSizeBytes` y `FlushEveryRows`.
- Comparar accesores boxed vs. tipados (justifica optimización de [04](./04-streaming-strategy.md) §5).
- Registrar baseline para detectar regresiones en CI.

## 6. CI

- `dotnet test` (unitarias) en cada PR.
- Integración con Testcontainers en pipeline nocturno (más lento).
- Prueba de memoria como job manual/nocturno con reporte de tendencia.
