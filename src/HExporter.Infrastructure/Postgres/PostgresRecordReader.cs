using System.Data;
using HExporter.Core.Abstractions;
using HExporter.Core.Models;
using Npgsql;

namespace HExporter.Infrastructure.Postgres;

/// <summary>
/// Adaptador PostgreSQL. El protocolo binario de Npgsql ya transmite fila a fila (no bufferiza
/// el resultset completo); CommandBehavior.SequentialAccess evita además bufferizar columnas
/// grandes (bytea/text) al leerlas.
/// </summary>
public sealed class PostgresRecordReader : IRecordReader
{
    private readonly NpgsqlConnection _conn;
    private readonly NpgsqlCommand _cmd;
    private readonly NpgsqlDataReader _reader;

    public IReadOnlyList<ColumnSchema> Schema { get; }

    private PostgresRecordReader(NpgsqlConnection conn, NpgsqlCommand cmd, NpgsqlDataReader reader)
    {
        _conn = conn;
        _cmd = cmd;
        _reader = reader;
        Schema = BuildSchema(reader);
    }

    public static async Task<PostgresRecordReader> OpenAsync(
        PostgresConnectionFactory factory,
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
            foreach (var (k, v) in binds)
                cmd.Parameters.Add(new NpgsqlParameter(k, v ?? DBNull.Value));

            var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            return new PostgresRecordReader(conn, cmd, reader);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private static IReadOnlyList<ColumnSchema> BuildSchema(NpgsqlDataReader reader)
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
