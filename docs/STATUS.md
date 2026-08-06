# STATUS — project resume snapshot

> Generated 2026-07-14. Read this before touching code if you're a new agent joining the session.
> Architecture/rules context: `CLAUDE.md` (root). Full backlog: `docs/08-implementation-tasks.md`.

## What HExporter is

Exports large reports from Oracle to CSV/XLSX via streaming, O(1) memory relative to row count. See `CLAUDE.md` for the golden rule (do not break: no `DataTable`, `ToList()`, full workbook in memory, etc.).

## Backlog status

**All 10 epics (E1–E10) in `docs/08-implementation-tasks.md` are complete** — every task marked `[x]`. The CSV MVP, XLSX, CLI, observability/security, volume testing (MemProbe PASS, 10M rows ~126MB peak WS) and packaging (publish + Docker) are done and verified. No backlog remains pending from that document.

## Build / test / verification

- Build: `dotnet build` (or `dotnet build HExporter.slnx`) — **0 warnings, 0 errors** (`TreatWarningsAsErrors=true`, non-negotiable).
- Unit tests: `dotnet test tests/HExporter.UnitTests` — **16/16 passed** (last run: 2026-07-14).
- Integration tests (real Oracle via Testcontainers + podman): `dotnet test tests/HExporter.IntegrationTests` — **3/3 passed** (~8.8s). Requires podman running (`DOCKER_HOST`/`TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` pointing at the podman VM, already configured in this environment). VM needs ≥6GiB RAM (`podman machine set --memory 6144`) — Oracle Free doesn't start reliably with 2GiB.
- Docker: `podman build -t hexporter:test .` + `podman run --rm hexporter:test --help` — verified working.

## Recent work (this session, outside the original backlog)

1. **T3.6 completed** (`tests/HExporter.IntegrationTests/OracleFixture.cs` + `OracleRecordReaderTests.cs`): integration tests against real Oracle (`gvenzl/oracle-free`). Two gotchas documented as comments in `OracleFixture.cs`:
   - Testcontainers.Oracle's default wait strategy (log-based) clashes with podman → replaced with a port-wait + real connection retry.
   - `.WithDatabase("FREEPDB1")` collides with the PDB that gvenzl/oracle-free already creates by default (`ORA-65012`) → connection string built by hand against the real PDB instead of using the library's `GetConnectionString()`.
2. **`--sql-file` in the CLI** (`src/HExporter.Cli/Program.cs`): allows passing a `.sql` file instead of `--table`/`--sql` inline, for long queries. Source priority: `--profile` > `--table` > `--sql-file` > `--sql`. Passing `--sql` and `--sql-file` together = error (exit 1).
3. **Optimization of `kardex-gnc.sql`** (repo root, not part of HExporter's source code — it's a business query that gets exported *with* HExporter): the original query had an O(n²) pattern (2 correlated subqueries with `GROUP BY` per row for `INICIAL`/`SALDO_FINAL`) + duplicated subqueries in a `CASE` (up to 4x the same lookup per row). Rewritten using window functions (`SUM() OVER (...RANGE...)`) and `LEFT JOIN`s with a `DOC_TYPE` guard. Original backed up in `kardex-gnc.original.sql` for result comparison. **Still pending real verification against Oracle** (row count + `INICIAL`/`SALDO_FINAL` totals matching between both versions) — could not be run here due to lack of access to that specific database. Recommended index (not applied, it's DDL on the user's Oracle):
   ```sql
   CREATE INDEX idx_delta_prism_kardex
     ON REPORTUSER.DELTA_PRISM (ITEM_SID, STORE_NO, REVERSION, CREATED_DATETIME);
   ```

## Git status

Current branch, not pushed to `origin/main` beyond what's already synced. Working tree has 2 untracked files (`kardex-gnc.sql`, `kardex-gnc.original.sql`) — these are the user's business artifacts, not part of the project's source code; decide whether they go into `.gitignore` or get committed separately.

Latest commits (most recent first):
```
1b9efbe feat(cli): add --sql-file option to load export queries from a file
d8ef9b7 test(integration): add Oracle integration tests via Testcontainers
cfbbe98 feat(packaging): add publish, Dockerfile and docs for distribution (E10)
c834ada feat(tools): add BenchmarkDotNet suite and tuning doc
9172b9f feat(export): implement NewSheet strategy for XLSX row-limit overflow
e5c1e76 feat(infra): add Polly retry policy for Oracle connection open
9da5ec0 feat: scaffold HExporter streaming export pipeline with CI and hardening
```

## Working convention with the user (important for the next agent)

- **Never commit without explicit permission.** Only draft conventional-commit messages (English) for the user to apply.
- Keep the build green (0 warnings) and unit tests passing at every step.
- Update `docs/08-implementation-tasks.md` (checkboxes + notes) if anything from the original backlog is touched.
- Architecture changes → review/update the relevant ADR in `docs/adr/`.

## What's left / possible next steps

- Verify the rewritten `kardex-gnc.sql` against the user's real Oracle (could not be done this session).
- Decide the fate of `kardex-gnc*.sql` in the repo (gitignore vs. `reports/` folder vs. commit).
- The formal backlog (`docs/08-implementation-tasks.md`) has no pending items — any new work is ad-hoc (like the CLI flag and the query) and not pre-planned in that document.
