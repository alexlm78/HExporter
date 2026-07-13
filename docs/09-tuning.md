# 09 — Tuning

Valores recomendados para `ExportOptions` y benchmarks que los respaldan (T9.3/T9.4).

## Cómo correr los benchmarks

```bash
dotnet run -c Release --project tools/HExporter.Benchmarks -- --filter '*'
```

`tools/HExporter.Benchmarks/ExportThroughputBenchmarks.cs` mide el pipeline completo
(`ExportService.ExecuteAsync`) con `SyntheticRecordReader` (200.000 filas, sin Oracle)
→ CSV/XLSX real → disco. Varía `FlushEveryRows` (1.000/10.000/100.000) y
`FileBufferBytes` (64 KB/1 MB).

## Resultados (Apple M1 Pro, macOS, .NET 10, Release, 200.000 filas)

| Formato | FlushEveryRows | FileBufferBytes | Media  | Memoria asignada |
|---------|----------------|------------------|--------|-------------------|
| CSV     | 1.000          | 64 KB            | 156 ms | 59 MB             |
| CSV     | 10.000         | 64 KB            | 107 ms | 59 MB             |
| CSV     | 100.000        | 64 KB            | 106 ms | 59 MB             |
| CSV     | 10.000         | 1 MB             | 114 ms | 65 MB             |
| CSV     | 100.000        | 1 MB             | 103 ms | 65 MB             |
| XLSX    | 1.000          | 64 KB            | 606 ms | 417 MB            |
| XLSX    | 10.000         | 64 KB            | 584 ms | 416 MB            |
| XLSX    | 100.000        | 1 MB             | 582 ms | 418 MB            |

(Tabla completa de 12 combinaciones en la salida de BenchmarkDotNet; los valores
omitidos están dentro del mismo rango de ruido.)

## Conclusiones

1. **`FlushEveryRows` no afecta el throughput observable en este volumen.** El
   `StreamWriter` de CSV ya bufferiza internamente (`FileBufferBytes`); el `FlushAsync`
   periódico solo fuerza el vaciado a disco antes de tiempo — a 200k filas su costo
   se pierde en el ruido. Su valor real es acotar cuánto se puede perder ante un
   corte (memoria/tiempo desde el último flush), no acelerar el export. Default
   `10_000` es razonable; no hace falta tunearlo salvo que se quiera un compromiso
   distinto entre "cuánto se pierde si corta" y overhead de syscalls de flush.
   `FlushAsync` en `XlsxExportWriter` es un no-op (`ValueTask.CompletedTask`) —
   `FlushEveryRows` **no tiene ningún efecto** en XLSX; solo dispara `progress.Report`.
2. **`FileBufferBytes` tiene efecto marginal en este rango (64 KB vs 1 MB).** Subir
   el buffer del `StreamWriter`/`FileStream` reduce syscalls de escritura pero a
   este volumen el SO ya amortigua vía page cache. Default `128 * 1024` (128 KB) está
   bien; solo subirlo si se exportan archivos muy grandes (cientos de millones de
   filas) sobre almacenamiento con latencia alta (red/NFS).
3. **XLSX es ~5x más lento y ~7x más costoso en memoria que CSV para el mismo
   volumen** (por fila: generación de OOXML — estilos, shared strings, compresión
   zip — vía MiniExcel, no por el puente streaming de `XlsxExportWriter`). Preferir
   **CSV** cuando el consumidor lo acepte; usar XLSX solo cuando el formato de
   salida lo exija. Esto no es un problema de memoria O(1) — el consumo de XLSX
   sigue siendo plano respecto al nº de filas (ver `docs/04-streaming-strategy.md`),
   solo tiene una constante por fila más alta.

## Fuera de alcance de este benchmark: `Oracle:FetchSizeBytes`

`FetchSizeBytes` (`OracleRecordReader`, ver `docs/05-configuration.md`) solo tiene
efecto con un listener Oracle real (acota el lote de red por round-trip del cursor).
No se puede medir con el reader sintético. Sin una instancia Oracle disponible en este
entorno (mismo bloqueo que T3.6 — Testcontainers requiere Docker, no disponible aquí),
queda como guía de configuración sin benchmark propio:

- Default `1 MiB` es un punto de partida razonable (balance entre round-trips de red
  y memoria del buffer de fetch).
- Redes de alta latencia hacia el listener → subir `FetchSizeBytes` reduce round-trips.
- Filas muy anchas (muchas columnas / LOBs) → bajar `FetchSizeBytes` si la latencia
  por lote se vuelve visible, ya que cada lote debe completarse antes de liberar la
  primera fila al pipeline.
- Revisar con `tools/HExporter.MemProbe` + Oracle real cuando haya un entorno con
  Docker/Testcontainers disponible (pendiente en T3.6).
