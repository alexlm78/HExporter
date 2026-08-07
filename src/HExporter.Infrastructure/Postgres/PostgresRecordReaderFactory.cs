using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.Infrastructure.Postgres;

public sealed class PostgresRecordReaderFactory : IRecordReaderFactory
{
    private readonly PostgresConnectionFactory _connectionFactory;

    public PostgresRecordReaderFactory(PostgresConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct)
        => await PostgresRecordReader.OpenAsync(_connectionFactory, request.Sql, request.Binds, ct);
}
