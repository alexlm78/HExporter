# ADR-0002 — Driver Oracle managed y lectura server-side streaming

**Estado:** Aceptado · **Fecha:** 2026-07-13

## Contexto

El requisito central es leer resultados enormes sin cargarlos en memoria. La elección del driver y del modo de lectura determina si esto es posible.

## Decisión

- Usar **`Oracle.ManagedDataAccess.Core`** (100% managed, sin cliente Oracle nativo → cross-platform, despliegue simple).
- Leer con **`OracleDataReader` forward-only** vía `ExecuteReaderAsync(CommandBehavior.SequentialAccess)`.
- Tunear **`FetchSize`** (bytes por lote de red, ~256KB–1MB), **no** cargar el resultado completo.
- **Prohibido** `DataTable`/`DataSet`/`Load()`.
- LOBs con `InitialLOBFetchSize=-1` para transmitir, no bufferizar.

## Consecuencias

- ✅ Cursor server-side: el cliente mantiene solo el lote actual → memoria O(1) en filas.
- ✅ Sin dependencia de Oracle Instant Client.
- ➖ `FetchSize` requiere tuning por forma de fila; documentado en [04](../04-streaming-strategy.md) y validado en E9.

## Alternativas descartadas

- ODP.NET no-managed: requiere cliente nativo, complica despliegue/containers.
- Dapper `Query<T>` sin `buffered:false`: bufferiza todo por defecto. (Con `buffered:false` sería viable pero preferimos control directo del reader.)
