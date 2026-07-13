using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Polly;
using Polly.Retry;

namespace HExporter.Infrastructure.Oracle;

public sealed class OracleConnectionFactory
{
    private readonly OracleOptions _options;
    private readonly ResiliencePipeline _retryPipeline;

    public OracleConnectionFactory(IOptions<OracleOptions> options, ILogger<OracleConnectionFactory> logger)
    {
        _options = options.Value;
        _retryPipeline = BuildRetryPipeline(_options, logger);
    }

    public OracleOptions Options => _options;

    public async Task<OracleConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "Oracle:ConnectionString no configurado. Ver docs/05-configuration.md §2.");

        return await _retryPipeline.ExecuteAsync(async token =>
        {
            var conn = new OracleConnection(_options.ConnectionString);
            await conn.OpenAsync(token);
            return conn;
        }, ct);
    }

    // Reintentos ante fallo transitorio (listener caído, red, etc.) al abrir la conexión.
    // No reintenta OperationCanceledException (cancelación explícita) ni errores de config.
    private static ResiliencePipeline BuildRetryPipeline(OracleOptions options, ILogger logger)
    {
        if (options.ConnectRetryAttempts <= 0)
            return ResiliencePipeline.Empty;

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<OracleException>(),
                MaxRetryAttempts = options.ConnectRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(options.ConnectRetryBaseDelaySeconds),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Reintentando conexión Oracle (intento {Attempt}/{Max}) tras {Delay}",
                        args.AttemptNumber + 1, options.ConnectRetryAttempts, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }
}
