namespace HExporter.Core.Abstractions;

/// <summary>Recibe reportes de progreso (nº de filas escritas hasta el momento).</summary>
public interface IProgressSink
{
    void Report(long rowsWritten);
}

/// <summary>Sink nulo por defecto.</summary>
public sealed class NullProgressSink : IProgressSink
{
    public static readonly NullProgressSink Instance = new();
    public void Report(long rowsWritten) { }
}
