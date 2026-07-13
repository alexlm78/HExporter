# 02 — Arquitectura

## 1. Principios

1. **Streaming primero.** El dato fluye fila por fila desde Oracle al archivo. Nunca se materializa el conjunto completo.
2. **Memoria acotada.** El uso de RAM depende del tamaño de fila y del buffer, no del número de filas.
3. **Separación de responsabilidades.** Lectura (Oracle), formateo (CSV/XLSX) y orquestación son independientes y testeables.
4. **Extensible por formato.** Agregar un formato nuevo = implementar una interfaz, sin tocar el resto.
5. **Fail-safe.** Cancelación cooperativa, límites de recursos, y limpieza de archivos parciales.

## 2. Vista de capas (Clean Architecture ligera)

```
+-------------------------------------------------------------+
|  HExporter.Cli            (System.CommandLine, host, DI)     |  Presentación
+-------------------------------------------------------------+
|  HExporter.Application    (orquestación, ExportService,      |  Aplicación
|                            perfiles, validación)             |
+-------------------------------------------------------------+
|  HExporter.Core           (abstracciones, modelos, puertos:  |  Dominio
|                            IRecordReader, IExportWriter)      |
+-------------------------------------------------------------+
|  HExporter.Infrastructure (OracleRecordReader,               |  Infraestructura
|   + HExporter.Export       CsvExportWriter, XlsxExportWriter) |
+-------------------------------------------------------------+
```

Regla de dependencias: las capas externas dependen de las internas. `Core` no depende de nadie (define los puertos/interfaces). Oracle y los writers son **adaptadores** que implementan esos puertos.

## 3. Componentes

| Componente | Responsabilidad |
|------------|-----------------|
| `IRecordReader` | Puerto de lectura forward-only. Expone metadatos de columnas + iteración por fila. |
| `OracleRecordReader` | Adaptador Oracle: abre conexión, ejecuta comando con `CommandBehavior.SequentialAccess`, tunea `FetchSize`, envuelve `OracleDataReader`. |
| `IExportWriter` | Puerto de escritura. Recibe el schema y consume filas de forma incremental. |
| `CsvExportWriter` | Escribe CSV fila a fila con `StreamWriter` + buffer; maneja quoting/escaping. |
| `XlsxExportWriter` | Escribe XLSX en streaming con `MiniExcel` (no arma el workbook en RAM). |
| `ExportService` | Orquesta: obtiene reader, resuelve writer por formato, bombea filas, reporta progreso, maneja cancelación y errores. |
| `ReportProfile` | Definición declarativa de un reporte (consulta, formato, opciones). |
| `ExportWriterFactory` | Resuelve el `IExportWriter` según formato solicitado. |

## 4. Flujo de una exportación (secuencia)

```
CLI → ExportService.ExecuteAsync(request, ct)
  ├─ Validar request / cargar ReportProfile
  ├─ reader = OracleRecordReader.OpenAsync(sql, binds, ct)   // conexión + FetchSize
  ├─ schema = reader.GetSchema()                             // nombres/tipos de columnas
  ├─ writer = factory.Create(format, destinationStream)
  ├─ await writer.BeginAsync(schema, ct)                     // encabezados / hoja
  ├─ while (await reader.ReadAsync(ct))                      // forward-only
  │     writer.WriteRow(reader.CurrentRow)                   // sin acumular
  │     if (++n % FlushEvery == 0) writer.Flush(); progress.Report(n)
  ├─ await writer.EndAsync(ct)                               // cerrar/flush final
  └─ return ExportResult(rows=n, bytes, elapsed)
```

Puntos clave:
- El `while` procesa **una fila viva a la vez**. No hay lista/colección que crezca.
- `Flush` periódico empuja el buffer al `Stream` de salida y evita que crezca.
- Todo respeta un `CancellationToken`.

## 5. Modelo de despliegue

- **Ejecutable de consola** self-contained o framework-dependent (`dotnet HExporter.Cli.dll ...`).
- Corre en el mismo host o en un worker. Sin estado entre ejecuciones.
- Salida a: ruta de disco local, montaje de red, o `stdout` (para pipe).

## 6. Decisiones de arquitectura

Ver [adr/](./adr/):
- ADR-0001: .NET 8 + estructura de solución
- ADR-0002: Driver Oracle managed y streaming server-side
- ADR-0003: MiniExcel para XLSX en streaming
- ADR-0004: Escritura CSV manual vs CsvHelper
- ADR-0005: Estrategia de particionado (diferida a v2)
