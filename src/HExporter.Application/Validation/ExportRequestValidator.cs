using System.Text.RegularExpressions;
using HExporter.Core.Models;
using Microsoft.Extensions.Options;

namespace HExporter.Application.Validation;

public sealed partial class ExportRequestValidator(IOptions<ExportSecurityOptions> securityOptions)
{
    // owner.objeto — identificadores Oracle válidos. Anti-injection para --table.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_$#]{0,29}(\.[A-Za-z][A-Za-z0-9_$#]{0,29})?$")]
    private static partial Regex TableNameRegex();

    public void Validate(ExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sql))
            throw new ArgumentException("La consulta SQL es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
            throw new ArgumentException("La ruta de destino es obligatoria.");
        if (request.Format == ExportFormat.Xlsx && request.DestinationPath == "-")
            throw new ArgumentException("XLSX no soporta salida a stdout ('-'). Use CSV.");
        if (request.DestinationPath.Contains('\0'))
            throw new ArgumentException("Ruta de destino inválida.");

        ValidateOutputBoundary(request.DestinationPath);
    }

    /// <summary>Path traversal: si hay un directorio base configurado (ExportSecurity:AllowedOutputDirectory),
    /// --out debe resolver dentro de él. Ver docs/06-nfr-ops.md §Seguridad.</summary>
    private void ValidateOutputBoundary(string destinationPath)
    {
        string? allowedDir = securityOptions.Value.AllowedOutputDirectory;
        if (allowedDir is null || destinationPath == "-")
            return;

        string baseDir = Path.GetFullPath(allowedDir);
        string fullPath = Path.GetFullPath(destinationPath, baseDir);
        string baseDirWithSep = baseDir.EndsWith(Path.DirectorySeparatorChar) ? baseDir : baseDir + Path.DirectorySeparatorChar;

        if (fullPath != baseDir && !fullPath.StartsWith(baseDirWithSep, StringComparison.Ordinal))
            throw new ArgumentException($"La ruta de destino debe estar dentro de {allowedDir}.");
    }

    /// <summary>Valida un identificador de tabla/vista antes de construir SELECT *.</summary>
    public static bool IsValidTableName(string name) => TableNameRegex().IsMatch(name);
}
