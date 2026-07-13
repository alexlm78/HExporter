namespace HExporter.Core.Models;

/// <summary>Describe una columna del resultado. Inmutable.</summary>
public sealed record ColumnSchema(int Ordinal, string Name, Type ClrType, string DbTypeName);
