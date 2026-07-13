namespace HExporter.Core.Models;

public sealed record ExportResult(long RowCount, long BytesWritten, TimeSpan Elapsed)
{
    public double RowsPerSecond => Elapsed.TotalSeconds > 0 ? RowCount / Elapsed.TotalSeconds : 0;
}
