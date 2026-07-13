using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace HExporter.Infrastructure.Oracle;

public sealed class OracleConnectionFactory
{
    private readonly OracleOptions _options;

    public OracleConnectionFactory(IOptions<OracleOptions> options) => _options = options.Value;

    public OracleOptions Options => _options;

    public async Task<OracleConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Oracle:ConnectionString no configurado. Ver docs/05-configuration.md §2.");

        var conn = new OracleConnection(_options.ConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
