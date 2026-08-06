# HExporter — Architecture Documentation

Large-volume report exporter from **Oracle** tables to **CSV** or **XLSX** files, writing **directly from the database to the file via streaming**, without materializing the full result in memory.

## Design goal

Process reports with millions of rows with **constant and bounded** memory usage (independent of the result size), preventing the machine from freezing or crashing due to memory pressure (OOM / GC pauses).

## Document index

| # | Document | Content |
|---|-----------|-----------|
| 00 | [README.md](./README.md) | This index |
| 01 | [01-vision-scope.md](./01-vision-scope.md) | Vision, scope, actors, use cases |
| 02 | [02-architecture.md](./02-architecture.md) | Architecture, layers, components, flow |
| 03 | [03-technical-design.md](./03-technical-design.md) | Detailed technical design, interfaces, classes |
| 04 | [04-streaming-strategy.md](./04-streaming-strategy.md) | Memory-safe streaming strategy (core) |
| 05 | [05-configuration.md](./05-configuration.md) | Configuration, `appsettings`, secrets, CLI |
| 06 | [06-nfr-ops.md](./06-nfr-ops.md) | Non-functional requirements, security, observability |
| 07 | [07-testing-strategy.md](./07-testing-strategy.md) | Testing strategy |
| 08 | [08-implementation-tasks.md](./08-implementation-tasks.md) | Backlog: epics, stories and tasks |
| — | [adr/](./adr/) | Architecture Decision Records |

## Technology stack (summary)

- **Runtime:** .NET 10 (LTS), C# latest _(the generated scaffold targets `net10.0`; the installed SDK is 10.0.301)_
- **Oracle driver:** `Oracle.ManagedDataAccess.Core`
- **CSV:** direct `StreamWriter` (row-by-row writing)
- **XLSX:** `MiniExcel` (true streaming, low memory)
- **CLI:** `System.CommandLine`
- **Logging:** `Serilog`
- **DI/Host:** `Microsoft.Extensions.Hosting`

## Golden rule

> Never load the full result into memory. The pipeline is `OracleDataReader` (forward-only, server-side) → per-row transformation → output `Stream` with a bounded buffer and periodic `flush`. Memory is O(1) relative to the number of rows.
