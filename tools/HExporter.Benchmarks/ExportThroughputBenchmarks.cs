using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using HExporter.Application;
using HExporter.Application.Validation;
using HExporter.Core.Models;
using HExporter.Export;
using HExporter.MemProbe;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HExporter.Benchmarks;

/// <summary>
/// Mide throughput del pipeline de exportación (reader sintético → writer real → disco)
/// variando FlushEveryRows y FileBufferBytes (T9.3). Sin Oracle: FetchSizeBytes solo
/// importa con un listener real y no se puede medir aquí — ver docs/09-tuning.md.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class ExportThroughputBenchmarks
{
    private const long Rows = 200_000;

    private ExportService _service = null!;
    private string _outDir = null!;

    [Params(1_000, 10_000, 100_000)]
    public int FlushEveryRows { get; set; }

    [Params(65536, 1048576)]
    public int FileBufferBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _outDir = Directory.CreateTempSubdirectory("hexporter-bench").FullName;
        var readerFactory = new SyntheticReaderFactory(Rows);
        var writerFactory = new ExportWriterFactory();
        var validator = new ExportRequestValidator(Options.Create(new ExportSecurityOptions()));
        _service = new ExportService(readerFactory, writerFactory, validator, NullLogger<ExportService>.Instance);
    }

    [Benchmark]
    public async Task Csv()
    {
        string path = Path.Combine(_outDir, $"bench_{Guid.NewGuid():N}.csv");
        var options = new ExportOptions { FlushEveryRows = FlushEveryRows, FileBufferBytes = FileBufferBytes };
        var request = new ExportRequest("SELECT 1", ExportRequest.NoBinds, ExportFormat.Csv, path, options);
        await _service.ExecuteAsync(request, progress: null, CancellationToken.None);
        File.Delete(path);
    }

    [Benchmark]
    public async Task Xlsx()
    {
        string path = Path.Combine(_outDir, $"bench_{Guid.NewGuid():N}.xlsx");
        var options = new ExportOptions { FlushEveryRows = FlushEveryRows, FileBufferBytes = FileBufferBytes };
        var request = new ExportRequest("SELECT 1", ExportRequest.NoBinds, ExportFormat.Xlsx, path, options);
        await _service.ExecuteAsync(request, progress: null, CancellationToken.None);
        File.Delete(path);
    }

    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_outDir, recursive: true);
}
