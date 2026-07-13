using System.Diagnostics;
using HExporter.Application;
using HExporter.Application.Validation;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using HExporter.Export;
using HExporter.MemProbe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// ---- Args ----
//   --rows N        nº de filas sintéticas (def. 10_000_000)
//   --format csv|xlsx (def. csv)
//   --out path      (def. probe.<ext>)
long rows = 10_000_000;
var format = ExportFormat.Csv;
string? outPath = null;

for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--rows": rows = long.Parse(args[++i]); break;
        case "--format": format = Enum.Parse<ExportFormat>(args[++i], ignoreCase: true); break;
        case "--out": outPath = args[++i]; break;
    }
}
outPath ??= $"probe.{(format == ExportFormat.Xlsx ? "xlsx" : "csv")}";

Console.WriteLine($"MemProbe: {rows:N0} filas -> {format} -> {outPath}");
Console.WriteLine($"GC modo servidor: {System.Runtime.GCSettings.IsServerGC}");

// ---- Servicio real (sin Oracle: reader sintético) ----
var service = new ExportService(
    new SyntheticReaderFactory(rows),
    new ExportWriterFactory(),
    new ExportRequestValidator(Options.Create(new ExportSecurityOptions())),
    NullLogger<ExportService>.Instance);

var options = new ExportOptions { FlushEveryRows = 50_000 };
var request = new ExportRequest("synthetic", ExportRequest.NoBinds, format, outPath, options);

// ---- Muestreo de memoria en background ----
long peakWorkingSet = 0;
long peakManagedHeap = 0;
using var stop = new CancellationTokenSource();
var sampler = Task.Run(async () =>
{
    var proc = Process.GetCurrentProcess();
    while (!stop.IsCancellationRequested)
    {
        proc.Refresh();
        peakWorkingSet = Math.Max(peakWorkingSet, proc.WorkingSet64);
        peakManagedHeap = Math.Max(peakManagedHeap, GC.GetTotalMemory(forceFullCollection: false));
        try { await Task.Delay(250, stop.Token); } catch (OperationCanceledException) { break; }
    }
});

var progress = new ConsoleProgress();
var sw = Stopwatch.StartNew();
var result = await service.ExecuteAsync(request, progress, CancellationToken.None);
sw.Stop();

stop.Cancel();
await sampler;

Console.WriteLine();
Console.WriteLine("==== Resultado ====");
Console.WriteLine($"Filas escritas : {result.RowCount:N0}");
Console.WriteLine($"Bytes archivo  : {result.BytesWritten:N0} ({result.BytesWritten / 1024.0 / 1024:N1} MB)");
Console.WriteLine($"Duración       : {result.Elapsed} ({result.RowsPerSecond:N0} filas/s)");
Console.WriteLine($"Peak WorkingSet: {peakWorkingSet / 1024.0 / 1024:N1} MB");
Console.WriteLine($"Peak GC heap   : {peakManagedHeap / 1024.0 / 1024:N1} MB");
Console.WriteLine($"GC gen0/1/2    : {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
Console.WriteLine();
Console.WriteLine(peakWorkingSet < 500L * 1024 * 1024
    ? "PASS: working set < 500 MB (memoria O(1) respecto a filas)."
    : "REVISAR: working set >= 500 MB. Ver docs/04-streaming-strategy.md.");

sealed file class ConsoleProgress : IProgressSink
{
    public void Report(long rowsWritten) => Console.Write($"\r  {rowsWritten:N0} filas...");
}
