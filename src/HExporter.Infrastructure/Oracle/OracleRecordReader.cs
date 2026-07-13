using System.Data;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace HExporter.Infrastructure.Oracle;

/// <summary>
/// Adaptador Oracle. Cursor server-side forward-only. FetchSize acota el lote de red.
/// SequentialAccess permite streaming de columnas/LOB sin bufferizarlos completos.
/// </summary>
public sealed class OracleRecordReader : IRecordReader
{
    private readonly OracleConnection _conn;
    private readonly OracleCommand _cmd;
    private readonly OracleDataReader _reader;

    public IReadOnlyList<ColumnSchema> Schema { get; }

    private OracleRecordReader(OracleConnection conn, OracleCommand cmd, OracleDataReader reader)
    {
        _conn = conn;
        _cmd = cmd;
        _reader = reader;
        Schema = BuildSchema(reader);
    }

    public static async Task<OracleRecordReader> OpenAsync(
        OracleConnectionFactory factory,
        string sql,
        IReadOnlyDictionary<string, object?> binds,
        CancellationToken ct)
    {
        var opt = factory.Options;
        var conn = await factory.OpenAsync(ct);
        try
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = opt.CommandTimeoutSeconds;
            cmd.FetchSize = opt.FetchSizeBytes;      // clave: lote acotado, no todo el resultado
            cmd.InitialLOBFetchSize = -1;            // stream de LOBs
            cmd.BindByName = opt.BindByName;
            foreach (var (k, v) in binds)
                cmd.Parameters.Add(new OracleParameter(k, v ?? DBNull.Value));

            var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess, ct);
            return new OracleRecordReader(conn, cmd, reader);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static IReadOnlyList<ColumnSchema> BuildSchema(OracleDataReader reader)
    {
        var cols = new ColumnSchema[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
            cols[i] = new ColumnSchema(i, reader.GetName(i), reader.GetFieldType(i), reader.GetDataTypeName(i));
        return cols;
    }

    public ValueTask<bool> ReadAsync(CancellationToken ct) => new(_reader.ReadAsync(ct));

    public object? GetValue(int ordinal) => _reader.IsDBNull(ordinal) ? null : _reader.GetValue(ordinal);

    public bool IsDBNull(int ordinal) => _reader.IsDBNull(ordinal);

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync();
        await _cmd.DisposeAsync();
        await _conn.DisposeAsync(); // devuelve al pool
    }
}
