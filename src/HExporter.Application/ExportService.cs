using System.Diagnostics;
using HExporter.Application.Validation;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using Microsoft.Extensions.Logging;

namespace HExporter.Application;

/// <summary>
/// Orquesta la exportación: reader forward-only → writer incremental.
/// Bombea UNA fila viva a la vez. Flush periódico. Escribe a .tmp y renombra atómico.
/// </summary>
public sealed class ExportService
{
    private readonly IRecordReaderFactory _readerFactory;
    private readonly IExportWriterFactory _writerFactory;
    private readonly ExportRequestValidator _validator;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        IRecordReaderFactory readerFactory,
        IExportWriterFactory writerFactory,
        ExportRequestValidator validator,
        ILogger<ExportService> logger)
    {
        _readerFactory = readerFactory;
        _writerFactory = writerFactory;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ExportResult> ExecuteAsync(
        ExportRequest request, IProgressSink? progress, CancellationToken ct)
    {
        _validator.Validate(request);
        progress ??= NullProgressSink.Instance;

        bool toStdout = request.DestinationPath == "-";
        string finalPath = request.DestinationPath;
        string writePath = toStdout ? finalPath : finalPath + ".tmp";
        var exportId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();
        long rows = 0;
        long bytes = 0;

        _logger.LogInformation("Export {ExportId} iniciada. Formato={Format} Destino={Dest}",
            exportId, request.Format, finalPath);

        Stream destination = toStdout
            ? Console.OpenStandardOutput()
            : new FileStream(writePath, FileMode.Create, FileAccess.Write, FileShare.None,
                request.Options.FileBufferBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);

        try
        {
            await using var reader = await _readerFactory.OpenAsync(request, ct);
            await using (var writer = _writerFactory.Create(request.Format, destination, request.Options))
            {
                await writer.BeginAsync(reader.Schema, ct);
                while (await reader.ReadAsync(ct))
                {
                    writer.WriteRow(reader);
                    if (++rows % request.Options.FlushEveryRows == 0)
                    {
                        await writer.FlushAsync(ct);
                        progress.Report(rows);
                    }
                }
                await writer.EndAsync(ct);
            }

            bytes = destination.CanSeek ? destination.Length : 0;
        }
        catch (Exception ex)
        {
            await destination.DisposeAsync();
            if (!toStdout) TryDeletePartial(writePath);
            if (ex is OperationCanceledException)
                _logger.LogWarning("Export {ExportId} cancelada tras {Rows} filas.", exportId, rows);
            else
                _logger.LogError(ex, "Export {ExportId} falló tras {Rows} filas.", exportId, rows);
            throw;
        }

        await destination.DisposeAsync();

        if (!toStdout)
        {
            File.Move(writePath, finalPath, overwrite: true); // rename atómico
            bytes = new FileInfo(finalPath).Length;
        }

        sw.Stop();
        var result = new ExportResult(rows, bytes, sw.Elapsed);
        _logger.LogInformation(
            "Export {ExportId} completada. Filas={Rows} Bytes={Bytes} Duración={Elapsed} ({Rps:N0} filas/s)",
            exportId, result.RowCount, result.BytesWritten, result.Elapsed, result.RowsPerSecond);
        return result;
    }

    private void TryDeletePartial(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudo borrar el parcial {Path}", path); }
    }
}
