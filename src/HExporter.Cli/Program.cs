using System.CommandLine;
using HExporter.Application;
using HExporter.Cli;
using HExporter.Core.Models;
using HExporter.Export;
using HExporter.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using DotNetEnv;

// ---- Opciones CLI ----
var sqlOpt = new Option<string?>("--sql") { Description = "Consulta SELECT a exportar." };
var sqlFileOpt = new Option<string?>("--sql-file") { Description = "Ruta a un archivo .sql con la consulta a exportar." };
var tableOpt = new Option<string?>("--table") { Description = "Tabla/vista a exportar (SELECT *)." };
var profileOpt = new Option<string?>("--profile") { Description = "Ruta a un report.json." };
var formatOpt = new Option<ExportFormat>("--format") { Description = "csv | xlsx.", DefaultValueFactory = _ => ExportFormat.Csv };
var outOpt = new Option<string?>("--out") { Description = "Archivo destino ('-' = stdout, solo csv)." };
var bindOpt = new Option<string[]>("--bind") { Description = "Bind variable k=v (repetible).", AllowMultipleArgumentsPerToken = true };
var delimiterOpt = new Option<string>("--delimiter") { Description = "Delimitador CSV.", DefaultValueFactory = _ => "," };
var noHeadersOpt = new Option<bool>("--no-headers") { Description = "Omite encabezados." };
var sheetOpt = new Option<string>("--sheet") { Description = "Nombre de hoja XLSX.", DefaultValueFactory = _ => "Datos" };
var flushOpt = new Option<int>("--flush-every") { Description = "Filas entre flushes.", DefaultValueFactory = _ => 10_000 };
var envFileOpt = new Option<string?>("--env-file") { Description = "Ruta a un archivo .env alternativo (def. .env en el directorio actual)." };

var root = new RootCommand("HExporter — exporta tablas/consultas Oracle a CSV/XLSX por streaming.");
foreach (var o in new Option[] { sqlOpt, sqlFileOpt, tableOpt, profileOpt, formatOpt, outOpt, bindOpt, delimiterOpt, noHeadersOpt, sheetOpt, flushOpt, envFileOpt })
    root.Options.Add(o);

root.SetAction(async (parse, ct) =>
{
    try
    {
        var host = BuildHost(parse.GetValue(envFileOpt));
        var loader = host.Services.GetRequiredService<ReportProfileLoader>();
        var exporter = host.Services.GetRequiredService<ExportService>();

        string? sql = parse.GetValue(sqlOpt);
        string? sqlFile = parse.GetValue(sqlFileOpt);
        string? table = parse.GetValue(tableOpt);
        string? profilePath = parse.GetValue(profileOpt);
        var format = parse.GetValue(formatOpt);
        var binds = ParseBinds(parse.GetValue(bindOpt) ?? Array.Empty<string>());

        // Resolver origen -> SQL final
        if (profilePath is not null)
        {
            var profile = await loader.LoadAsync(profilePath, ct);
            sql ??= profile.Sql;
            format = profile.Format;
            foreach (var (k, v) in profile.Binds)
                binds.TryAdd(k, v);
        }
        else if (table is not null)
        {
            if (!HExporter.Application.Validation.ExportRequestValidator.IsValidTableName(table))
            {
                Console.Error.WriteLine($"Nombre de tabla inválido: {table}");
                return 1;
            }
            sql = $"SELECT * FROM {table}";
        }
        else if (sqlFile is not null)
        {
            if (sql is not null)
            {
                Console.Error.WriteLine("Use --sql o --sql-file, no ambos.");
                return 1;
            }
            if (!File.Exists(sqlFile))
            {
                Console.Error.WriteLine($"Archivo --sql-file no encontrado: {sqlFile}");
                return 1;
            }
            sql = await File.ReadAllTextAsync(sqlFile, ct);
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            Console.Error.WriteLine("Indique --sql, --sql-file, --table o --profile.");
            return 1;
        }

        string outPath = parse.GetValue(outOpt) ?? $"export.{(format == ExportFormat.Xlsx ? "xlsx" : "csv")}";

        var options = new ExportOptions
        {
            IncludeHeaders = !parse.GetValue(noHeadersOpt),
            FlushEveryRows = parse.GetValue(flushOpt),
            Csv = new CsvOptions { Delimiter = parse.GetValue(delimiterOpt) ?? "," },
            Xlsx = new XlsxOptions { SheetName = parse.GetValue(sheetOpt) ?? "Datos" }
        };

        var request = new ExportRequest(sql!, binds, format, outPath, options);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var result = await exporter.ExecuteAsync(request, new ConsoleProgressSink(), cts.Token);
        Console.Error.WriteLine();
        Console.Error.WriteLine($"OK: {result.RowCount:N0} filas, {result.BytesWritten:N0} bytes, {result.Elapsed}.");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("\nCancelado.");
        return 130;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nError: {ex.Message}");
        return ex is ArgumentException or FileNotFoundException ? 1 : 2;
    }
    finally
    {
        Log.CloseAndFlush();
    }
});

return await root.Parse(args).InvokeAsync();

// ---- Helpers ----
static IHost BuildHost(string? envFilePath)
{
    // Precedencia (menor a mayor): appsettings.json < .env < variables de entorno reales < CLI.
    // DotNetEnv no sobreescribe variables ya presentes en el proceso: si ya existe HEXPORTER_..., gana esa.
    LoadDotEnv(envFilePath);

    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.AddEnvironmentVariables("HEXPORTER_");

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    builder.Services.AddHExporterOracle(builder.Configuration);
    builder.Services.AddHExporterWriters();
    builder.Services.AddHExporterApplication(builder.Configuration);
    return builder.Build();
}

static void LoadDotEnv(string? envFilePath)
{
    string path = envFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(path))
    {
        if (envFilePath is not null)
            throw new FileNotFoundException($"Archivo .env no encontrado: {envFilePath}");
        return; // .env es opcional por defecto — configuración puede venir solo de env vars reales.
    }
    Env.Load(path); // no sobreescribe variables ya presentes en el proceso
}

static Dictionary<string, object?> ParseBinds(string[] pairs)
{
    var d = new Dictionary<string, object?>();
    foreach (var p in pairs)
    {
        int eq = p.IndexOf('=');
        if (eq <= 0) throw new ArgumentException($"Bind inválido (esperado k=v): {p}");
        d[p[..eq]] = p[(eq + 1)..];
    }
    return d;
}
