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
            throw new ArgumentException("The SQL query is required.");
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
            throw new ArgumentException("The destination path is required.");
        if (request.Format == ExportFormat.Xlsx && request.DestinationPath == "-")
            throw new ArgumentException("XLSX does not support stdout output ('-'). Use CSV.");
        if (request.DestinationPath.Contains('\0'))
            throw new ArgumentException("Invalid destination path.");

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
            throw new ArgumentException($"The destination path must be inside {allowedDir}.");
    }

    /// <summary>Valida un identificador de tabla/vista antes de construir SELECT *.</summary>
    public static bool IsValidTableName(string name) => TableNameRegex().IsMatch(name);
}
