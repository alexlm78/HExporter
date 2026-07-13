namespace HExporter.Application.Validation;

public sealed class ExportSecurityOptions
{
    public const string SectionName = "ExportSecurity";

    /// <summary>
    /// Si se define, `--out` debe resolver dentro de este directorio (defensa en profundidad
    /// contra path traversal cuando el ejecutable corre en contexto multiusuario/servicio).
    /// Null (por defecto) = sin restricción, para uso normal de CLI de un solo usuario.
    /// Ver docs/06-nfr-ops.md §Seguridad.
    /// </summary>
    public string? AllowedOutputDirectory { get; init; }
}
