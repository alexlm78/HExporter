using FluentAssertions;
using HExporter.Infrastructure.Postgres;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace HExporter.UnitTests;

public class PostgresConnectionFactoryRetryTests
{
    private sealed class CountingLogger : ILogger<PostgresConnectionFactory>
    {
        public int WarningCount { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) WarningCount++;
        }
    }

    // Puerto reservado (TCP no asignable) => rechazo de conexión inmediato, sin espera de red real.
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=1";

    [Fact]
    public async Task Retries_configured_number_of_times_on_transient_connect_failure()
    {
        var options = Options.Create(new PostgresOptions
        {
            ConnectionString = UnreachableConnectionString,
            ConnectRetryAttempts = 2,
            ConnectRetryBaseDelaySeconds = 0.01
        });
        var logger = new CountingLogger();
        var factory = new PostgresConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NpgsqlException>();
        logger.WarningCount.Should().Be(2);
    }

    [Fact]
    public async Task Does_not_retry_when_disabled()
    {
        var options = Options.Create(new PostgresOptions
        {
            ConnectionString = UnreachableConnectionString,
            ConnectRetryAttempts = 0
        });
        var logger = new CountingLogger();
        var factory = new PostgresConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NpgsqlException>();
        logger.WarningCount.Should().Be(0);
    }

    [Fact]
    public async Task Throws_when_connection_string_missing()
    {
        var options = Options.Create(new PostgresOptions { ConnectionString = "" });
        var factory = new PostgresConnectionFactory(options, new CountingLogger());

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
