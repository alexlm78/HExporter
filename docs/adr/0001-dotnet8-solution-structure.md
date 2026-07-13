# ADR-0001 — .NET 10 y estructura de solución

**Estado:** Aceptado · **Fecha:** 2026-07-13
**Actualización:** El scaffold se generó sobre **.NET 10 (LTS)** — es el LTS vigente (nov-2025) y el único targeting pack disponible en el host (SDK 10.0.301). El diseño es idéntico para net8.0 si se requiere.

## Contexto

Se requiere una herramienta de exportación batch, cross-platform, con streaming eficiente y ciclo de soporte largo.

## Decisión

- **.NET 8 (LTS)**, C# 12, `Nullable=enable`.
- Estructura por capas (clean architecture ligera): `Core` (puertos/modelos, sin dependencias) ← `Application` ← `Infrastructure`/`Export` ← `Cli`.
- `Core` define interfaces (`IRecordReader`, `IExportWriter`); Oracle y los writers son adaptadores.

## Consecuencias

- ✅ Testabilidad alta (puertos mockeables), extensible por formato sin tocar el núcleo.
- ✅ Soporte LTS hasta nov-2026 (evaluar migración a .NET 10 LTS).
- ➖ Algo de ceremonia de proyectos para un tool pequeño; se acepta por mantenibilidad.

## Alternativas descartadas

- Proyecto único monolítico: más simple al inicio, peor separación y testabilidad.
- .NET Framework 4.8: no cross-platform, sin mejoras de `Span`/async modernas.
