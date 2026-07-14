# STATUS — snapshot para retomar el proyecto

> Generado 2026-07-14. Lee esto antes de tocar código si eres un agente nuevo entrando a la sesión.
> Contexto de arquitectura/reglas: `CLAUDE.md` (raíz). Backlog completo: `docs/08-implementation-tasks.md`.

## Qué es HExporter

Exporta reportes grandes desde Oracle a CSV/XLSX en streaming, memoria O(1) respecto a nº de filas. Ver `CLAUDE.md` para la regla de oro (no romper: nada de `DataTable`, `ToList()`, workbook completo en memoria, etc.).

## Estado del backlog

**Las 10 épicas (E1–E10) de `docs/08-implementation-tasks.md` están completas** — todas las tareas marcadas `[x]`. El MVP CSV, XLSX, CLI, observabilidad/seguridad, pruebas de volumen (MemProbe PASS, 10M filas ~126MB peak WS) y empaquetado (publish + Docker) están hechos y verificados. No queda backlog pendiente de ese documento.

## Build / test / verificación

- Build: `dotnet build` (o `dotnet build HExporter.slnx`) — **0 warnings, 0 errors** (`TreatWarningsAsErrors=true`, no negociable).
- Unit tests: `dotnet test tests/HExporter.UnitTests` — **16/16 passed** (última corrida: 2026-07-14).
- Integration tests (Oracle real vía Testcontainers + podman): `dotnet test tests/HExporter.IntegrationTests` — **3/3 passed** (~8.8s). Requiere podman corriendo (`DOCKER_HOST`/`TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` apuntando a la VM podman, ya configurado en este entorno). VM necesita ≥6GiB RAM (`podman machine set --memory 6144`) — con 2GiB Oracle Free no arranca confiable.
- Docker: `podman build -t hexporter:test .` + `podman run --rm hexporter:test --help` — verificado funcionando.

## Trabajo reciente (esta sesión, fuera del backlog original)

1. **T3.6 completado** (`tests/HExporter.IntegrationTests/OracleFixture.cs` + `OracleRecordReaderTests.cs`): tests de integración contra Oracle real (`gvenzl/oracle-free`). Dos gotchas documentados como comentarios en `OracleFixture.cs`:
   - Wait strategy por defecto de Testcontainers.Oracle (basada en logs) choca con podman → reemplazada por espera de puerto + retry de conexión real.
   - `.WithDatabase("FREEPDB1")` colisiona con el PDB que gvenzl/oracle-free ya crea por defecto (`ORA-65012`) → connection string armado a mano contra el PDB real en vez de usar `GetConnectionString()` de la librería.
2. **`--sql-file` en el CLI** (`src/HExporter.Cli/Program.cs`): permite pasar un archivo `.sql` en vez de `--table`/`--sql` inline, para queries largas. Prioridad de fuente: `--profile` > `--table` > `--sql-file` > `--sql`. Pasar `--sql` y `--sql-file` juntos = error (exit 1).
3. **Optimización de `kardex-gnc.sql`** (raíz del repo, no forma parte del código fuente de HExporter — es una query de negocio que se exporta *con* HExporter): la query original tenía patrón O(n²) (2 subqueries correlacionados con `GROUP BY` por fila para `INICIAL`/`SALDO_FINAL`) + subqueries duplicados en `CASE` (hasta 4x el mismo lookup por fila). Reescrita usando funciones de ventana (`SUM() OVER (...RANGE...)`) y `LEFT JOIN`s con guard de `DOC_TYPE`. Original respaldado en `kardex-gnc.original.sql` para comparar resultados. **Pendiente de verificación real contra Oracle** (conteo de filas + totales `INICIAL`/`SALDO_FINAL` iguales entre ambas versiones) — no se pudo correr aquí por falta de acceso a esa base específica. Índice recomendado (no aplicado, es DDL sobre su Oracle):
   ```sql
   CREATE INDEX idx_delta_prism_kardex
     ON REPORTUSER.DELTA_PRISM (ITEM_SID, STORE_NO, REVERSION, CREATED_DATETIME);
   ```

## Estado de git

Rama actual, no pusheada a `origin/main` más allá de lo ya sincronizado. Working tree con 2 archivos sin trackear (`kardex-gnc.sql`, `kardex-gnc.original.sql`) — son artefactos de negocio del usuario, no parte del código fuente del proyecto; decidir si van a `.gitignore` o se commitean aparte.

Últimos commits (más reciente primero):
```
1b9efbe feat(cli): add --sql-file option to load export queries from a file
d8ef9b7 test(integration): add Oracle integration tests via Testcontainers
cfbbe98 feat(packaging): add publish, Dockerfile and docs for distribution (E10)
c834ada feat(tools): add BenchmarkDotNet suite and tuning doc
9172b9f feat(export): implement NewSheet strategy for XLSX row-limit overflow
e5c1e76 feat(infra): add Polly retry policy for Oracle connection open
9da5ec0 feat: scaffold HExporter streaming export pipeline with CI and hardening
```

## Convención de trabajo con el usuario (importante para el próximo agente)

- **Nunca commitear sin permiso explícito.** Solo redactar mensajes conventional commit (inglés) para que el usuario los aplique.
- Mantener build verde (0 warnings) y unit tests pasando en cada paso.
- Actualizar `docs/08-implementation-tasks.md` (checkboxes + notas) si se toca algo del backlog original.
- Cambios de arquitectura → revisar/actualizar el ADR pertinente en `docs/adr/`.

## Qué falta / próximos pasos posibles

- Verificar `kardex-gnc.sql` reescrita contra Oracle real del usuario (no se pudo hacer en esta sesión).
- Decidir destino de `kardex-gnc*.sql` en el repo (gitignore vs. carpeta `reports/` vs. commit).
- Backlog formal (`docs/08-implementation-tasks.md`) no tiene items pendientes — cualquier trabajo nuevo es ad-hoc (como el CLI flag y la query) y no está pre-planeado en ese documento.
