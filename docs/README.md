# HExporter — Documentación de Arquitectura

Exportador de reportes de gran volumen desde tablas **Oracle** hacia archivos **CSV** o **XLSX**, escribiendo **directamente de la base al archivo mediante streaming**, sin materializar el resultado completo en memoria.

## Objetivo de diseño

Procesar reportes de millones de filas con un consumo de memoria **constante y acotado** (independiente del tamaño del resultado), evitando que el equipo se congele o colapse por presión de memoria (OOM / GC pauses).

## Índice de documentos

| # | Documento | Contenido |
|---|-----------|-----------|
| 00 | [README.md](./README.md) | Este índice |
| 01 | [01-vision-scope.md](./01-vision-scope.md) | Visión, alcance, actores, casos de uso |
| 02 | [02-architecture.md](./02-architecture.md) | Arquitectura, capas, componentes, flujo |
| 03 | [03-technical-design.md](./03-technical-design.md) | Diseño técnico detallado, interfaces, clases |
| 04 | [04-streaming-strategy.md](./04-streaming-strategy.md) | Estrategia de streaming memoria-segura (núcleo) |
| 05 | [05-configuration.md](./05-configuration.md) | Configuración, `appsettings`, secretos, CLI |
| 06 | [06-nfr-ops.md](./06-nfr-ops.md) | Requisitos no funcionales, seguridad, observabilidad |
| 07 | [07-testing-strategy.md](./07-testing-strategy.md) | Estrategia de pruebas |
| 08 | [08-implementation-tasks.md](./08-implementation-tasks.md) | Backlog: épicas, historias y tareas |
| — | [adr/](./adr/) | Architecture Decision Records |

## Stack tecnológico (resumen)

- **Runtime:** .NET 10 (LTS), C# latest _(el scaffold generado apunta a `net10.0`; el SDK instalado es 10.0.301)_
- **Driver Oracle:** `Oracle.ManagedDataAccess.Core`
- **CSV:** `StreamWriter` directo (escritura fila a fila)
- **XLSX:** `MiniExcel` (streaming real, baja memoria)
- **CLI:** `System.CommandLine`
- **Logging:** `Serilog`
- **DI/Host:** `Microsoft.Extensions.Hosting`

## Regla de oro

> Nunca cargar el resultado completo en memoria. El pipeline es `OracleDataReader` (forward-only, server-side) → transformación por fila → `Stream` de salida con buffer acotado y `flush` periódico. Memoria O(1) respecto al número de filas.
