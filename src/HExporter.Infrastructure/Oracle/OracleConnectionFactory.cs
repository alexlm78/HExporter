using HExporter.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Polly;

namespace HExporter.Infrastructure.Oracle;

public sealed class OracleConnectionFactory
{
    private readonly OracleOptions _options;
    private readonly ResiliencePipeline _retryPipeline;

    public OracleConnectionFactory(IOptions<OracleOptions> options, ILogger<OracleConnectionFactory> logger)
    {
        _options = options.Value;
        _retryPipeline = ConnectionRetryPolicyFactory.Build<OracleException>(
            _options.ConnectRetryAttempts, _options.ConnectRetryBaseDelaySeconds, logger, "Oracle");
    }

    public OracleOptions Options => _options;

    public async Task<OracleConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Oracle:ConnectionString not configured. See docs/05-configuration.md §2.");

        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var conn = new OracleConnection(_options.ConnectionString);
            await conn.OpenAsync(token);
            return conn;
        }, ct);
    }
}
