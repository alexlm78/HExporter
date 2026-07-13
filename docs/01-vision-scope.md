# 01 — Visión y Alcance

## 1. Problema

Los reportes actuales se generan cargando el resultado completo de una consulta Oracle en memoria (p. ej. `DataTable`, listas de objetos o librerías de Excel que arman el workbook completo en RAM). Con volúmenes altos (millones de filas) esto provoca:

- Consumo de memoria proporcional al tamaño del reporte → **OutOfMemoryException**.
- Pausas largas del recolector de basura (GC) → la aplicación "se congela".
- Fallas intermitentes difíciles de reproducir según el tamaño del dato del día.

## 2. Visión

Una herramienta que exporta cualquier consulta/tabla Oracle a CSV o XLSX con **memoria constante**, apta para correr en servidores modestos, en batch programado o invocada bajo demanda.

## 3. Alcance (In scope)

- Exportar el resultado de una **consulta SQL** o **tabla/vista** a **CSV** y **XLSX**.
- **Streaming end-to-end**: lectura server-side + escritura incremental a disco/stream.
- Parámetros de consulta (bind variables) para filtrar el reporte.
- Configuración de formato: delimitador CSV, encoding, encabezados, nombre de hoja, formato de fechas/números.
- Ejecución vía **CLI** con parámetros y vía **archivo de definición de reporte** (perfil reutilizable).
- Cancelación cooperativa, reporte de progreso y logging estructurado.
- Manejo de errores y reintentos de conexión.

## 4. Fuera de alcance (Out of scope) — v1

- Transformaciones complejas / joins que deban resolverse fuera de SQL (se delega al SQL).
- Generación de gráficos o formato condicional avanzado en XLSX.
- Interfaz gráfica (GUI/web). Solo CLI en v1 (extensible a Worker/API después).
- Escritura hacia bases destino (solo archivos).
- Particionado automático multi-archivo (candidato v2, ver ADR-0005).

## 5. Actores

| Actor | Descripción |
|-------|-------------|
| **Operador / Analista** | Ejecuta la exportación con parámetros desde la CLI. |
| **Scheduler** (cron / Task Scheduler / Airflow) | Invoca la CLI de forma programada. |
| **DBA** | Provee credenciales, tuning de `FetchSize`, y valida consultas. |

## 6. Casos de uso principales

1. **UC-01 Exportar tabla a CSV.** El operador indica tabla/consulta, formato CSV y ruta destino; el sistema produce el archivo por streaming.
2. **UC-02 Exportar consulta parametrizada a XLSX.** Igual que UC-01 pero con bind variables y salida XLSX.
3. **UC-03 Ejecutar reporte por perfil.** El operador indica un perfil (`report.json`) que define consulta, formato y opciones.
4. **UC-04 Cancelar exportación en curso.** `Ctrl+C` cancela de forma cooperativa; el archivo parcial se marca/elimina según política.

## 7. Criterios de éxito

- Exportar **10M filas × 20 columnas** con **memoria de proceso < 300 MB** estable.
- Throughput objetivo ≥ **50k filas/seg** en CSV (dependiente de red/DB) — ver [06-nfr-ops.md](./06-nfr-ops.md).
- Cero `OutOfMemoryException` sin importar el tamaño del resultado.
