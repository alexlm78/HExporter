# ADR-0003 — MiniExcel para XLSX en streaming

**Estado:** Aceptado · **Fecha:** 2026-07-13

## Contexto

XLSX es un ZIP de XML. La mayoría de librerías .NET construyen el documento completo en memoria antes de guardarlo, lo que rompe el requisito de memoria acotada con reportes grandes.

## Decisión

Usar **MiniExcel** para XLSX. Escribe fila a fila hacia el `Stream` de salida a partir de una fuente perezosa (`IDataReader`/`IEnumerable`), sin materializar el workbook en RAM.

## Consecuencias

- ✅ Streaming real → memoria acotada también en XLSX.
- ✅ API simple, pocas dependencias.
- ➖ Menos features de formato (estilos avanzados, fórmulas) que ClosedXML/EPPlus. Aceptable: el objetivo es exportar datos, no maquetar.
- ➖ Límite de formato XLSX: 1.048.576 filas/hoja → mitigación en ADR-0005 / [04](../04-streaming-strategy.md) §6.

## Alternativas descartadas

- **ClosedXML:** arma el workbook completo en memoria → OOM con volúmenes grandes. Descartado.
- **EPPlus:** en modo normal bufferiza en memoria; licencia comercial (Polyform) desde v5. Descartado.
- **OpenXML SDK (`OpenXmlWriter`):** permite streaming real y es gratuito, pero API de bajo nivel (verbosa, propensa a errores). Alternativa de respaldo si MiniExcel no alcanza; documentado como plan B.
