# 03 — Diseño Técnico Detallado

## 1. Estructura de la solución

```
HExporter.sln
├─ src/
│  ├─ HExporter.Core/            # Puertos, modelos, contratos. Sin dependencias externas.
│  │   ├─ Abstractions/
│  │   │   ├─ IRecordReader.cs
│  │   │   ├─ IExportWriter.cs
│  │   │   └─ IExportWriterFactory.cs
│  │   ├─ Models/
│  │   │   ├─ ColumnSchema.cs
│  │   │   ├─ ExportRequest.cs
│  │   │   ├─ ExportResult.cs
│  │   │   ├─ ExportFormat.cs      # enum: Csv, Xlsx
│  │   │   └─ ReportProfile.cs
│  │   └─ Progress/IProgressSink.cs
│  ├─ HExporter.Application/      # ExportService, validación, carga de perfiles
│  │   ├─ ExportService.cs
│  │   ├─ ReportProfileLoader.cs
│  │   └─ Validation/ExportRequestValidator.cs
│  ├─ HExporter.Infrastructure/   # Adaptador Oracle
│  │   ├─ Oracle/OracleRecordReader.cs
│  │   ├─ Oracle/OracleConnectionFactory.cs
│  │   └─ Oracle/OracleOptions.cs
│  ├─ HExporter.Export/           # Writers CSV/XLSX
│  │   ├─ Csv/CsvExportWriter.cs
│  │   ├─ Csv/CsvOptions.cs
│  │   ├─ Xlsx/XlsxExportWriter.cs
│  │   ├─ Xlsx/XlsxOptions.cs
│  │   └─ ExportWriterFactory.cs
│  └─ HExporter.Cli/             # Entry point, System.CommandLine, host DI, Serilog
│      ├─ Program.cs
│      └─ Commands/ExportCommand.cs
└─ tests/
   ├─ HExporter.UnitTests/
   └─ HExporter.IntegrationTests/   # Oracle via Testcontainers
```

## 2. Contratos (puertos)

### 2.1 `ColumnSchema`

```csharp
public sealed record ColumnSchema(int Ordinal, string Name, Type ClrType, string DbTypeName);
```

### 2.2 `IRecordReader`

Forward-only. Envuelve el `OracleDataReader` sin exponer detalles del driver.

```csharp
public interface IRecordReader : IAsyncDisposable
{
    IReadOnlyList<ColumnSchema> Schema { get; }

    /// Avanza a la siguiente fila. False cuando no hay más.
    ValueTask<bool> ReadAsync(CancellationToken ct);

    /// Valor de la columna en la fila actual (boxed, o use GetValue para tipado).
    object? GetValue(int ordinal);
    bool IsDBNull(int ordinal);
}
```

> **Nota de rendimiento:** `GetValue` retorna `object?` (boxing). Para columnas numéricas de altísimo volumen se puede añadir accesores tipados (`GetInt64`, `GetDecimal`, `GetString`) que los writers usen para evitar boxing. Ver [04-streaming-strategy.md](./04-streaming-strategy.md) §5.

### 2.3 `IExportWriter`

```csharp
public interface IExportWriter : IAsyncDisposable
{
    /// Escribe encabezados / inicializa la hoja. Recibe el schema del reader.
    ValueTask BeginAsync(IReadOnlyList<ColumnSchema> schema, CancellationToken ct);

    /// Escribe una fila leyendo del reader actual. No debe retener referencias.
    void WriteRow(IRecordReader row);

    /// Fuerza el vaciado del buffer al stream subyacente.
    ValueTask FlushAsync(CancellationToken ct);

    /// Cierra estructuras del formato (footer XLSX, flush final).
    ValueTask EndAsync(CancellationToken ct);
}
```

### 2.4 `IExportWriterFactory`

```csharp
public interface IExportWriterFactory
{
    IExportWriter Create(ExportFormat format, Stream destination, ExportOptions options);
}
```

## 3. Modelos de aplicación

```csharp
public enum ExportFormat { Csv, Xlsx }

public sealed record ExportRequest(
    string Sql,                                   // o nombre de tabla resuelto a SELECT *
    IReadOnlyDictionary<string, object?> Binds,   // bind variables
    ExportFormat Format,
    string DestinationPath,
    ExportOptions Options);

public sealed record ExportResult(long RowCount, long BytesWritten, TimeSpan Elapsed);
```

`ExportOptions` agrupa `CsvOptions` y `XlsxOptions` + comunes (encoding, `IncludeHeaders`, `FlushEveryRows`, `DateFormat`, `NumberFormat`, `CultureName`).

## 4. `OracleRecordReader` (núcleo de lectura)

Responsabilidades:
1. Crear conexión con `OracleConnectionFactory` (pooling on).
2. Crear `OracleCommand`, asignar `FetchSize` (bytes) — clave para el streaming (ver §04).
3. Ejecutar `ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct)`.
4. Proyectar `GetColumnSchema()` a `IReadOnlyList<ColumnSchema>`.

Esbozo:

```csharp
public sealed class OracleRecordReader : IRecordReader
{
    private readonly OracleConnection _conn;
    private readonly OracleCommand _cmd;
    private readonly OracleDataReader _reader;
    public IReadOnlyList<ColumnSchema> Schema { get; }

    private OracleRecordReader(OracleConnection c, OracleCommand cmd, OracleDataReader r)
    { _conn = c; _cmd = cmd; _reader = r; Schema = BuildSchema(r); }

    public static async Task<OracleRecordReader> OpenAsync(
        OracleConnectionFactory factory, string sql,
        IReadOnlyDictionary<string, object?> binds, OracleOptions opt, CancellationToken ct)
    {
        var conn = await factory.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.FetchSize = opt.FetchSizeBytes;      // p.ej. 1 MB. NO es todo el resultado.
        cmd.InitialLOBFetchSize = -1;            // stream de LOBs si aplica
        foreach (var (k, v) in binds)
            cmd.Parameters.Add(new OracleParameter(k, v ?? DBNull.Value));
        cmd.BindByName = true;
        var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, ct);
        return new OracleRecordReader(conn, cmd, reader);
    }

    public ValueTask<bool> ReadAsync(CancellationToken ct) => new(_reader.ReadAsync(ct));
    public object? GetValue(int i) => _reader.IsDBNull(i) ? null : _reader.GetValue(i);
    public bool IsDBNull(int i) => _reader.IsDBNull(i);

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _cmd.DisposeAsync();
        await _conn.DisposeAsync();   // devuelve al pool
    }
}
```

## 5. `CsvExportWriter`

- Envuelve el `Stream` destino en un `StreamWriter` con `bufferSize` configurable y encoding (UTF-8 con/sin BOM).
- Encabezados en `BeginAsync`.
- `WriteRow` recorre las columnas, aplica quoting RFC 4180 (comillas si el valor contiene delimitador, comilla o salto de línea; escapa `"` → `""`).
- Formatea fechas/números con `CultureInfo` fijo (evita sorpresas de locale).
- `FlushAsync` → `StreamWriter.FlushAsync`.

## 6. `XlsxExportWriter` (MiniExcel)

MiniExcel escribe XLSX en modo streaming aceptando un `IDataReader`/`IEnumerable`. Dos enfoques:

- **A (recomendado):** adaptar `IRecordReader` → `IDataReader` y pasar a `MiniExcel.SaveAsByIdataReader`/`SaveAs`, que escribe fila a fila al `Stream` sin construir el árbol OpenXML completo.
- **B (control fino):** exponer un `IEnumerable<IDictionary<string,object>>` perezoso (`yield return` por fila) hacia `MiniExcel.SaveAs`. La pereza del `IEnumerable` preserva el streaming.

Restricción de formato: XLSX tiene un límite de **1,048,576 filas por hoja**. Para resultados mayores → política de particionado por hoja/archivo (ver ADR-0005 y [04](./04-streaming-strategy.md) §6).

## 7. `ExportService`

```csharp
public async Task<ExportResult> ExecuteAsync(ExportRequest req, CancellationToken ct)
{
    _validator.Validate(req);
    var sw = Stopwatch.StartNew();
    await using var reader = await _readerFactory.OpenAsync(req, ct);

    await using var dest = _fs.CreateWrite(req.DestinationPath);   // FileStream buffer
    await using var writer = _writerFactory.Create(req.Format, dest, req.Options);

    await writer.BeginAsync(reader.Schema, ct);
    long n = 0;
    try
    {
        while (await reader.ReadAsync(ct))
        {
            writer.WriteRow(reader);
            if (++n % req.Options.FlushEveryRows == 0)
            {
                await writer.FlushAsync(ct);
                _progress.Report(n);
            }
        }
        await writer.EndAsync(ct);
    }
    catch (OperationCanceledException)
    {
        await _fs.DeletePartialAsync(req.DestinationPath);   // política de limpieza
        throw;
    }
    return new ExportResult(n, dest.Length, sw.Elapsed);
}
```

## 8. Dependencias NuGet

| Paquete | Uso |
|---------|-----|
| `Oracle.ManagedDataAccess.Core` | Driver Oracle managed |
| `MiniExcel` | XLSX streaming |
| `System.CommandLine` | CLI |
| `Serilog` + `Serilog.Sinks.Console` / `.File` | Logging estructurado |
| `Microsoft.Extensions.Hosting` | Host, DI, config |
| `FluentValidation` (opcional) | Validación de requests/perfiles |
| `xUnit`, `FluentAssertions`, `Testcontainers.Oracle` | Pruebas |
