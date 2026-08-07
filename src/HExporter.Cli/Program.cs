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

// ---- CLI options ----
var sqlOpt = new Option<string?>("--sql") { Description = "SELECT query to export." };
var sqlFileOpt = new Option<string?>("--sql-file") { Description = "Path to a .sql file with the query to export." };
var tableOpt = new Option<string?>("--table") { Description = "Table/view to export (SELECT *)." };
var profileOpt = new Option<string?>("--profile") { Description = "Path to a report.json." };
var formatOpt = new Option<ExportFormat>("--format") { Description = "csv | xlsx.", DefaultValueFactory = _ => ExportFormat.Csv };
var outOpt = new Option<string?>("--out") { Description = "Destination file ('-' = stdout, CSV only)." };
var bindOpt = new Option<string[]>("--bind") { Description = "Bind variable k=v (repeatable).", AllowMultipleArgumentsPerToken = true };
var delimiterOpt = new Option<string>("--delimiter") { Description = "CSV delimiter.", DefaultValueFactory = _ => "," };
var noHeadersOpt = new Option<bool>("--no-headers") { Description = "Omit headers." };
var sheetOpt = new Option<string>("--sheet") { Description = "XLSX sheet name.", DefaultValueFactory = _ => "Data" };
var flushOpt = new Option<int>("--flush-every") { Description = "Rows between flushes.", DefaultValueFactory = _ => 10_000 };
var envFileOpt = new Option<string?>("--env-file") { Description = "Path to an alternate .env file (default: .env in the current directory)." };
var dbEngineOpt = new Option<string?>("--db-engine") { Description = "oracle | postgres. Overrides HEXPORTER_Database__Engine / appsettings.json (default: oracle)." };

var root = new RootCommand("HExporter — streams Oracle/PostgreSQL tables/queries to CSV/XLSX.");
foreach (var o in new Option[] { sqlOpt, sqlFileOpt, tableOpt, profileOpt, formatOpt, outOpt, bindOpt, delimiterOpt, noHeadersOpt, sheetOpt, flushOpt, envFileOpt, dbEngineOpt })
    root.Options.Add(o);

if (args.Length == 0) args = ["--help"];

root.SetAction(async (parse, ct) =>
{
    try
    {
        var host = BuildHost(parse.GetValue(envFileOpt), parse.GetValue(dbEngineOpt));
        var loader = host.Services.GetRequiredService<ReportProfileLoader>();
        var exporter = host.Services.GetRequiredService<ExportService>();

        string? sql = parse.GetValue(sqlOpt);
        string? sqlFile = parse.GetValue(sqlFileOpt);
        string? table = parse.GetValue(tableOpt);
        string? profilePath = parse.GetValue(profileOpt);
        var format = parse.GetValue(formatOpt);
        var binds = ParseBinds(parse.GetValue(bindOpt) ?? Array.Empty<string>());

        // Resolve source -> final SQL
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
                Console.Error.WriteLine($"Invalid table name: {table}");
                return 1;
            }
            sql = $"SELECT * FROM {table}";
        }
        else if (sqlFile is not null)
        {
            if (sql is not null)
            {
                Console.Error.WriteLine("Use --sql or --sql-file, not both.");
                return 1;
            }
            if (!File.Exists(sqlFile))
            {
                Console.Error.WriteLine($"--sql-file file not found: {sqlFile}");
                return 1;
            }
            sql = await File.ReadAllTextAsync(sqlFile, ct);
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            Console.Error.WriteLine("Specify --sql, --sql-file, --table, or --profile.");
            return 1;
        }

        string outPath = parse.GetValue(outOpt) ?? $"export.{(format == ExportFormat.Xlsx ? "xlsx" : "csv")}";

        var options = new ExportOptions
        {
            IncludeHeaders = !parse.GetValue(noHeadersOpt),
            FlushEveryRows = parse.GetValue(flushOpt),
            Csv = new CsvOptions { Delimiter = parse.GetValue(delimiterOpt) ?? "," },
            Xlsx = new XlsxOptions { SheetName = parse.GetValue(sheetOpt) ?? "Data" }
        };

        var request = new ExportRequest(sql!, binds, format, outPath, options);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var result = await exporter.ExecuteAsync(request, new ConsoleProgressSink(), cts.Token);
        Console.Error.WriteLine();
        Console.Error.WriteLine($"OK: {result.RowCount:N0} rows, {result.BytesWritten:N0} bytes, {result.Elapsed}.");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("\nCancelled.");
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
static IHost BuildHost(string? envFilePath, string? dbEngineOverride)
{
    // Precedence (lowest to highest): appsettings.json < .env < real env vars < CLI.
    // DotNetEnv does not overwrite variables already present in the process: if HEXPORTER_... already exists, it wins.
    LoadDotEnv(envFilePath);

    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.AddEnvironmentVariables("HEXPORTER_");

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    var engine = DatabaseEngineResolver.Resolve(
        dbEngineOverride, builder.Configuration[DatabaseEngineResolver.ConfigKey]);
    builder.Services.AddHExporterDatabase(builder.Configuration, engine);
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
            throw new FileNotFoundException($".env file not found: {envFilePath}");
        return; // .env is optional by default — configuration can come solely from real env vars.
    }
    Env.Load(path); // does not overwrite variables already present in the process
}

static Dictionary<string, object?> ParseBinds(string[] pairs)
{
    var d = new Dictionary<string, object?>();
    foreach (var p in pairs)
    {
        int eq = p.IndexOf('=');
        if (eq <= 0) throw new ArgumentException($"Invalid bind (expected k=v): {p}");
        d[p[..eq]] = p[(eq + 1)..];
    }
    return d;
}
