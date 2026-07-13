using HExporter.Core.Abstractions;

namespace HExporter.Cli;

/// <summary>Reporta progreso a stderr (no contamina stdout cuando --out -).</summary>
public sealed class ConsoleProgressSink : IProgressSink
{
    public void Report(long rowsWritten)
        => Console.Error.Write($"\r  {rowsWritten:N0} filas...");
}
