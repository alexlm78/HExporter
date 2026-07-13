# 06 — Requisitos No Funcionales y Operación

## 1. Rendimiento

| Métrica | Objetivo |
|---------|----------|
| Memoria de proceso (working set) | < 300 MB, **estable** e independiente del nº de filas |
| Throughput CSV | ≥ 50.000 filas/seg (sujeto a red/DB/disco) |
| Throughput XLSX | ≥ 20.000 filas/seg (overhead de formato) |
| Tiempo a primera fila (TTFB) | < 2 s con `FetchSize` por defecto |

Palancas de tuning: `FetchSizeBytes`, `FlushEveryRows`, `FileBufferBytes`, GC de servidor, disco de salida (SSD/local vs. montaje de red).

## 2. Confiabilidad

- **Cancelación cooperativa** vía `CancellationToken` (Ctrl+C → `PosixSignalRegistration`/`Console.CancelKeyPress`).
- **Política de archivo parcial:** escribir a `destino.tmp` y renombrar atómicamente al terminar (`File.Move`); ante fallo/cancelación, borrar el `.tmp`. Evita entregar reportes truncados como válidos.
- **Reintentos de conexión:** política de reintentos (Polly) solo en la **apertura** de conexión; una vez iniciado el streaming, un corte aborta y se reintenta el reporte completo (no es reanudable en v1).
- **Timeouts:** `CommandTimeout = 0` (sin límite) para reportes largos, configurable; timeout de conexión sí acotado.

## 3. Seguridad

- **Credenciales:** nunca en texto plano ni en logs. Preferir **Oracle Wallet** / autenticación externa en producción. Secretos vía variables de entorno o Key Vault.
- **SQL Injection:** usar **siempre bind variables** para parámetros. `--table` se valida contra un patrón `owner.objeto` (identificadores) y se cita con `DBMS_ASSERT`-equivalente del lado app; nunca concatenar valores de usuario en el SQL.
- **Least privilege:** la cuenta Oracle debe tener solo `SELECT` sobre los objetos requeridos.
- **PII / datos sensibles:** los reportes pueden contener datos personales. Definir permisos de archivo de salida (0600), ubicación de escritura controlada, y política de retención/borrado. No loguear valores de filas.
- **Path traversal:** validar/normalizar `--out` contra un directorio base permitido si corre en contexto multiusuario. → `ExportSecurity:AllowedOutputDirectory` (`HEXPORTER_ExportSecurity__AllowedOutputDirectory`); sin definir = sin restricción (uso normal de CLI de un solo usuario).

## 4. Observabilidad

- **Logging estructurado (Serilog):** inicio/fin de exportación, nº de filas, bytes, duración, throughput, código de salida. **Nunca** el contenido de las filas.
- **Progreso:** reporte cada `FlushEveryRows` a consola (`stderr` para no contaminar `stdout` cuando `--out -`).
- **Métricas (opcional):** exponer contadores (filas/seg, memoria) vía `System.Diagnostics.Metrics` / OpenTelemetry si se integra a un colector.
- **Correlación:** `ExportId` (GUID) por ejecución en todos los logs.

## 5. Portabilidad / Despliegue

- Cross-platform (.NET 8): Linux/Windows/macOS. `Oracle.ManagedDataAccess.Core` es 100% managed (sin cliente Oracle nativo).
- Empaquetado: framework-dependent (requiere runtime .NET 8) o self-contained single-file para hosts sin runtime.
- Contenedor: imagen base `mcr.microsoft.com/dotnet/runtime:8.0`.

## 6. Mantenibilidad

- Puertos/adaptadores → agregar formato = nueva clase `IExportWriter` + registro en factory.
- Config centralizada y tipada (`IOptions<T>`).
- Cobertura de pruebas objetivo ≥ 80% en `Core`/`Application`.

## 7. Límites conocidos (v1)

- No reanudable tras corte (se re-ejecuta el reporte).
- XLSX limitado a 1.048.576 filas/hoja (mitigación en [04](./04-streaming-strategy.md) §6).
- Sin paralelización de particiones (v2).
