using HExporter.Core.Abstractions;
using HExporter.Core.Models;

namespace HExporter.Infrastructure.Oracle;

public sealed class OracleRecordReaderFactory : IRecordReaderFactory
{
    private readonly OracleConnectionFactory _connectionFactory;

    public OracleRecordReaderFactory(OracleConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<IRecordReader> OpenAsync(ExportRequest request, CancellationToken ct)
        => await OracleRecordReader.OpenAsync(_connectionFactory, request.Sql, request.Binds, ct);
}
