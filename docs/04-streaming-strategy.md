# 04 — Estrategia de Streaming Memoria-Segura (núcleo)

Este es el documento crítico: explica **por qué** el diseño no colapsa la memoria y **qué** hay que hacer/no hacer.

## 1. Principio central

El pipeline mantiene, en todo momento, **una sola fila viva** más un **buffer de salida acotado**:

```
Oracle (server-side cursor)
   → FetchSize buffer (driver, N bytes)      ← acotado
   → 1 fila en CLR (la actual)               ← O(1)
   → StreamWriter/MiniExcel buffer           ← acotado
   → FileStream buffer                        ← acotado
   → Disco
```

El uso de memoria es **O(ancho_de_fila + buffers)**, NO **O(número_de_filas)**. Da igual si el reporte tiene 1.000 o 1.000.000.000 de filas.

## 2. Reglas duras (lo que NUNCA se hace)

1. ❌ **No** `DataTable` / `DataSet` / `reader.Load()`.
2. ❌ **No** `ToList()` / `ToArray()` sobre el resultado ni buffers de filas que crezcan sin límite.
3. ❌ **No** librerías XLSX que construyan el workbook completo en memoria (ClosedXML, EPPlus en modo normal). Ver ADR-0003.
4. ❌ **No** `string` gigantes concatenados (`StringBuilder` de todo el CSV). Se escribe al `Stream`.
5. ❌ **No** cargar LOBs completos si se pueden transmitir (`InitialLOBFetchSize = -1`).

## 3. Lado Oracle: leer server-side

- `OracleDataReader` es **forward-only** y trae filas por lotes según `FetchSize`.
- **`FetchSize`** (en bytes) controla cuánto trae el driver por ida a la red. Valor típico: **256 KB – 1 MB**.
  - Muy bajo → muchas idas a la red, lento.
  - Muy alto → más RAM por lote y más latencia de primera fila. **No** cargues todo.
- `CommandBehavior.SequentialAccess`: permite procesar columnas en orden y hacer streaming de LOBs sin bufferizarlos completos.
- El cursor vive en el servidor Oracle; el cliente solo mantiene el lote actual.

Cálculo orientativo: `FetchSize` ÷ `bytes_por_fila` ≈ filas por lote. Ej.: 1 MB ÷ 200 B ≈ ~5.000 filas por ida de red, liberadas al avanzar.

## 4. Lado escritura: buffer + flush periódico

- El `StreamWriter`/MiniExcel acumulan en un buffer pequeño y lo vuelcan al `FileStream`.
- **Flush cada N filas** (`FlushEveryRows`, def. 10.000) para que el buffer no crezca y para durabilidad parcial.
- `FileStream` con `bufferSize` razonable (p. ej. 64–128 KB) y `useAsync: true`.
- Considerar `FileOptions.SequentialScan` para I/O secuencial.

## 5. Evitar asignaciones por fila (opcional, alto volumen)

Cuando el volumen lo justifique:
- Accesores tipados en `IRecordReader` (`GetInt64`, `GetDecimal`, `GetString`) → evitan boxing de `object`.
- Reusar un `char[]`/`Span<char>` buffer para formatear números/fechas (`ISpanFormattable.TryFormat`) en el writer CSV.
- Evitar `string.Format`/interpolación por celda en el hot path.

Estas optimizaciones son **incrementales** y no cambian la arquitectura; medir antes (ver [07](./07-testing-strategy.md)).

## 6. Límite de filas de XLSX y particionado

XLSX permite máx. **1.048.576 filas/hoja**. Estrategias (config `XlsxOptions.RowLimitStrategy`):

| Estrategia | Comportamiento |
|-----------|----------------|
| `Fail` (def.) | Aborta si se excede el límite. Seguro por defecto. |
| `NewSheet` | Al llegar al límite, crea `Datos_2`, `Datos_3`, … en el mismo archivo. |
| `NewFile` | Genera `reporte_0001.xlsx`, `reporte_0002.xlsx`, … (v2, ADR-0005). |

Para volúmenes que superen holgadamente el límite, **CSV es la elección natural** (sin límite de filas).

## 7. Presión de memoria y verificación

- Configurar `<ServerGarbageCollection>true</ServerGarbageCollection>` para batch de alto throughput; evaluar `<ConcurrentGarbageCollection>`.
- Prueba de humo obligatoria: exportar dataset sintético de **≥ 10M filas** y verificar que el working set se mantiene plano (ver [07](./07-testing-strategy.md) §4).
- Métrica de aceptación: memoria estable e independiente del nº de filas.

## 8. Backpressure

El bombeo es síncrono respecto a la escritura: si el disco es más lento que la DB, el `while` se bloquea en `FlushAsync`/`WriteRow`, lo que naturalmente frena la lectura (el driver deja de pedir el siguiente lote). No se necesita cola intermedia. Si en el futuro se paraleliza lectura/escritura, usar un `Channel<T>` **acotado** (`BoundedChannelOptions` con capacidad fija) para preservar el backpressure.
