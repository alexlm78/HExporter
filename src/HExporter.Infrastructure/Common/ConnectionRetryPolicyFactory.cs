using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace HExporter.Infrastructure.Common;

/// <summary>Reintentos con backoff exponencial ante fallo transitorio al abrir una conexión.
/// ShouldHandle solo captura TException (la excepción propia del driver) — no reintenta
/// OperationCanceledException (cancelación explícita) ni errores de configuración.</summary>
internal static class ConnectionRetryPolicyFactory
{
    public static ResiliencePipeline Build<TException>(
        int maxAttempts, double baseDelaySeconds, ILogger logger, string providerName)
        where TException : Exception
    {
        if (maxAttempts <= 0)
            return ResiliencePipeline.Empty;

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<TException>(),
                MaxRetryAttempts = maxAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(baseDelaySeconds),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying {Provider} connection (attempt {Attempt}/{Max}) after {Delay}",
                        providerName, args.AttemptNumber + 1, maxAttempts, args.RetryDelay);
                    return default;
                }
            })
            .Build();
    }
}
