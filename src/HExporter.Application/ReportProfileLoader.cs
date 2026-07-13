using System.Text.Json;
using HExporter.Core.Models;

namespace HExporter.Application;

public sealed class ReportProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<ReportProfile> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Perfil de reporte no encontrado: {path}");
        await using var fs = File.OpenRead(path);
        var profile = await JsonSerializer.DeserializeAsync<ReportProfile>(fs, JsonOptions, ct)
                      ?? throw new InvalidOperationException($"Perfil inválido: {path}");
        return profile;
    }
}
