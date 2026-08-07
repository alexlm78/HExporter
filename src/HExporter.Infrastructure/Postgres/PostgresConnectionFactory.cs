using HExporter.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;

namespace HExporter.Infrastructure.Postgres;

public sealed class PostgresConnectionFactory
{
    private readonly PostgresOptions _options;
    private readonly ResiliencePipeline _retryPipeline;

    public PostgresConnectionFactory(IOptions<PostgresOptions> options, ILogger<PostgresConnectionFactory> logger)
    {
        _options = options.Value;
        _retryPipeline = ConnectionRetryPolicyFactory.Build<NpgsqlException>(
            _options.ConnectRetryAttempts, _options.ConnectRetryBaseDelaySeconds, logger, "PostgreSQL");
    }

    public PostgresOptions Options => _options;

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Postgres:ConnectionString not configured. See docs/05-configuration.md §2.");

        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var conn = new NpgsqlConnection(_options.ConnectionString);
            await conn.OpenAsync(token);
            return conn;
        }, ct);
    }
}
