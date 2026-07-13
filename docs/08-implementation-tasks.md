# 08 — Backlog de Implementación

Épicas → historias → tareas. Estimación en puntos (S=1, M=3, L=5, XL=8). Marcar `[ ]` / `[x]`.

Prioridad de entrega: **E1 → E2 → E3 → E4** forman el MVP (CSV funcional end-to-end). E5–E8 endurecen y completan.

---

## E1 — Andamiaje de solución y CI  (M)

- [x] **T1.1** Crear solución y proyectos: `Core`, `Application`, `Infrastructure`, `Export`, `Cli`, `UnitTests`, `IntegrationTests`. (S)
- [x] **T1.2** Configurar `Directory.Build.props`: .NET 10 (LTS vigente; ver ADR-0001), `Nullable=enable`, `TreatWarningsAsErrors`. (S)
- [x] **T1.3** Referencias entre proyectos según regla de dependencias (ADR-0001). (S)
- [x] **T1.4** Pipeline CI: `dotnet build` + `dotnet test` en PR. → `.github/workflows/ci.yml` (build Release + unit tests), verificado local. Repo aún no inicializado como `.git`; workflow correrá al hacer push a GitHub. (M)
- [x] **T1.5** `Host` genérico + DI + carga de `appsettings.json` + `IOptions<T>`. (M)

## E2 — Puertos y modelos (Core)  (M)

- [x] **T2.1** `ColumnSchema`, `ExportFormat`, `ExportRequest`, `ExportResult`, `ExportOptions`. (S)
- [x] **T2.2** Interfaz `IRecordReader` (+ accesores tipados opcionales). (S)
- [x] **T2.3** Interfaz `IExportWriter` e `IExportWriterFactory`. (S)
- [x] **T2.4** `IProgressSink` y modelo de progreso. (S)
- [x] **T2.5** `ReportProfile` (modelo del perfil declarativo). (S)

## E3 — Lectura Oracle en streaming (Infrastructure)  (L)

- [x] **T3.1** `OracleOptions` + binding desde config. (S)
- [x] **T3.2** `OracleConnectionFactory` con pooling. (M)
- [x] **T3.3** `OracleRecordReader.OpenAsync`: comando, `FetchSize`, `SequentialAccess`, bind vars. (L)
- [x] **T3.4** Mapeo `GetColumnSchema()` → `ColumnSchema` (tipos Oracle→CLR). (M)
- [x] **T3.5** Streaming de LOB (`InitialLOBFetchSize=-1`). (M)
- [x] **T3.6** Pruebas de integración con Testcontainers.Oracle. → `tests/HExporter.IntegrationTests` (`OracleFixture` + `OracleRecordReaderTests`), contenedor `gvenzl/oracle-free:slim-faststart` vía podman (`DOCKER_HOST`/`TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` ya apuntan a la VM de podman). Cubre: schema/valores end-to-end, bind variables, y `FetchSizeBytes` chico forzando varios round-trips sin perder/duplicar filas. Dos hallazgos no obvios documentados como comentarios en `OracleFixture.cs`: (1) la wait strategy por defecto de la librería (buscar mensaje en logs) choca con podman — Docker.DotNet lanza "Invalid chunk header" leyendo logs por su socket; se reemplaza por espera de puerto + retry de conexión real; (2) no usar `.WithDatabase(...)`: gvenzl/oracle-free ya crea el PDB "FREEPDB1" por defecto, pedir crear uno con el mismo nombre dispara `ORA-65012` y aborta el arranque — el connection string se arma a mano contra ese PDB en vez de `GetConnectionString()` (que asume el servicio "XE" de oracle-xe). Además, la VM de podman necesitó subir de 2GiB a 6GiB de RAM (`podman machine set --memory 6144`) — Oracle Free no arranca de forma confiable con menos. (L)

## E4 — Writer CSV (Export)  (L)

- [x] **T4.1** `CsvOptions` (delimitador, headers, BOM, cultura, formatos). (S)
- [x] **T4.2** `CsvExportWriter`: `StreamWriter` + buffer, headers, `WriteRow`, flush. (L)
- [x] **T4.3** Quoting/escaping RFC 4180. (M)
- [x] **T4.4** Formateo de fechas/números con `CultureInfo` fijo; NULL→vacío. (M)
- [x] **T4.5** Unit tests de quoting, tipos, encoding, headers. → `tests/HExporter.UnitTests/CsvExportWriterTests.cs` (6 casos). (M)

## E5 — Writer XLSX (Export)  (L)

- [x] **T5.1** Integrar `MiniExcel`; `XlsxOptions` (sheet, headers, RowLimitStrategy). (M)
- [x] **T5.2** Adaptar `IRecordReader` → fuente perezosa (IDataReader/IEnumerable). → puente push→pull vía `BlockingCollection` acotada. (L)
- [x] **T5.3** `XlsxExportWriter` streaming (Begin/WriteRow/End). (L)
- [x] **T5.4** Enforcement del límite 1.048.576 filas + estrategia `Fail`/`NewSheet`. → `Fail` verificado (probe 10M XLSX aborta correctamente); `NewSheet` implementado en `XlsxExportWriter` explotando el modo multi-hoja lazy de MiniExcel (`IDictionary<string,object>` custom, streaming, ver docs/04-streaming-strategy.md §6). 2 tests nuevos en `XlsxExportWriterTests` (rollover multi-hoja + `Fail` sigue lanzando). (M)
- [x] **T5.5** Unit tests de tipos/celdas/headers/límite. → `tests/HExporter.UnitTests/XlsxExportWriterTests.cs` (roundtrip real). (M)

## E6 — Orquestación (Application)  (M)

- [x] **T6.1** `ExportWriterFactory` (resuelve writer por formato). (S)
- [x] **T6.2** `ExportService.ExecuteAsync`: bombeo, flush periódico, progreso. (L)
- [x] **T6.3** Escritura a `.tmp` + rename atómico; limpieza de parcial en fallo/cancelación. (M)
- [x] **T6.4** `ReportProfileLoader` (cargar/mergear perfil + overrides de binds). (M)
- [x] **T6.5** `ExportRequestValidator` (SQL vs table vs profile; validar `--table`). → regex anti-injection verificada contra intento de inyección. (M)

## E7 — CLI (Cli)  (M)

- [x] **T7.1** `System.CommandLine`: comando `export` con todas las opciones ([05](./05-configuration.md) §4). (L)
- [x] **T7.2** Mapear args → `ExportRequest`; precedencia CLI > env real > `[dot]env` > `appsettings.json`. → ver [05-configuration.md](./05-configuration.md) §2 y `--env-file`. (M)
- [x] **T7.3** Cancelación (Ctrl+C) → `CancellationToken`. (S)
- [x] **T7.4** Códigos de salida (0/1/2/3/130) y salida de progreso a `stderr`. → verificado: validación=1, conexión=2, `--env-file` ausente=1. (S)
- [x] **T7.5** Soporte `--out -` (stdout, solo CSV). (S)

## E8 — Observabilidad, seguridad y hardening  (M)

- [x] **T8.1** Serilog: consola + archivo rolling; `ExportId` de correlación. (M)
- [x] **T8.2** Logging de métricas de exportación (filas, bytes, throughput); NUNCA datos de filas. (S)
- [x] **T8.3** Resolución de secretos: env reales > archivo `[dot]env` (`DotNetEnv`, opcional, `--env-file` configurable) > `appsettings.json`. Plantilla `env.example` sin secretos; `.env` en `.gitignore`. Pendiente Wallet/Key Vault para prod. (M)
- [x] **T8.4** Reintentos de apertura de conexión (Polly). → `OracleConnectionFactory` con `ResiliencePipeline` (backoff exponencial), `Oracle:ConnectRetryAttempts`/`ConnectRetryBaseDelaySeconds` configurables, `0` deshabilita. 2 unit tests (`OracleConnectionFactoryRetryTests.cs`). (M)
- [x] **T8.5** Validación anti-injection de `--table` / path traversal de `--out`. → `ExportRequestValidator.IsValidTableName` (regex identificador) + `ExportSecurity:AllowedOutputDirectory` opcional (defensa en profundidad, `Path.GetFullPath` + containment check); 5 unit tests en `ExportRequestValidatorTests.cs`. (M)

## E9 — Pruebas de volumen y rendimiento  (M)  *(gate de aceptación)*

- [x] **T9.1** Generador de dataset sintético 10M+ filas. → `tools/HExporter.MemProbe` (`SyntheticRecordReader`) + `scripts/seed_10m.sql` (Oracle real). (S)
- [x] **T9.2** Prueba de memoria plana — **criterio de aceptación del proyecto**. → MemProbe muestrea working-set/GC. **Verificado: 10M CSV, peak WS ~126 MB, memoria plana. PASS.** (M)
- [x] **T9.3** Benchmarks (BenchmarkDotNet) variando FetchSize/FlushEvery. → `tools/HExporter.Benchmarks` (`ExportThroughputBenchmarks`, reader sintético + writers reales, sin Oracle); varía `FlushEveryRows`/`FileBufferBytes`. `FetchSizeBytes` requiere Oracle real — queda como guía sin benchmark propio (no por bloqueo de entorno: T3.6 confirmó que podman sí corre Oracle real aquí). (M)
- [x] **T9.4** Documentar valores de tuning recomendados. → `docs/09-tuning.md`. (S)

## E10 — Empaquetado y entrega  (S)

- [x] **T10.1** Publicación framework-dependent + self-contained single-file. → comandos `dotnet publish` documentados en README; `HExporter.Cli.csproj` con `SelfContained`/`IncludeNativeLibrariesForSelfExtract` condicionados a `PublishSingleFile=true`. Sin trimming (`Oracle.ManagedDataAccess.Core` no es trim-safe). (M)
- [x] **T10.2** Dockerfile (runtime net10.0 — corregido de "8.0" a la versión real del proyecto). → multi-stage sdk→runtime, framework-dependent, `.dockerignore`. Verificado con `podman build -t hexporter:test .` (build exitoso) y `podman run --rm hexporter:test --help` (entrypoint responde correctamente). (S)
- [x] **T10.3** README de uso + ejemplos. → sección "Empaquetado y distribución" (publish/Docker). (S)

---

## Ruta crítica (MVP CSV)

```
T1.1 → T1.5 → T2.* → T3.3 → T4.2 → T6.2 → T7.1 → (export CSV funcional)
```

XLSX (E5) y hardening (E8/E9) siguen en paralelo tras el MVP.

## Definición de "Hecho" (DoD)

- Código con nullable enabled, sin warnings.
- Pruebas unitarias verdes; cobertura ≥ 80% en Core/Application.
- Sin regresión de la prueba de memoria (T9.2).
- Documentación/ADR actualizados si cambió una decisión.
