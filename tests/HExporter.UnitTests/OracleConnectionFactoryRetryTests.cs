using FluentAssertions;
using HExporter.Infrastructure.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace HExporter.UnitTests;

public class OracleConnectionFactoryRetryTests
{
    private sealed class CountingLogger : ILogger<OracleConnectionFactory>
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

    [Fact]
    public async Task Retries_configured_number_of_times_on_transient_connect_failure()
    {
        var options = Options.Create(new OracleOptions
        {
            ConnectionString = "Data Source=###malformed###;User Id=x;Password=x",
            ConnectRetryAttempts = 2,
            ConnectRetryBaseDelaySeconds = 0.01
        });
        var logger = new CountingLogger();
        var factory = new OracleConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OracleException>();
        logger.WarningCount.Should().Be(2);
    }

    [Fact]
    public async Task Does_not_retry_when_disabled()
    {
        var options = Options.Create(new OracleOptions
        {
            ConnectionString = "Data Source=###malformed###;User Id=x;Password=x",
            ConnectRetryAttempts = 0
        });
        var logger = new CountingLogger();
        var factory = new OracleConnectionFactory(options, logger);

        var act = async () => await factory.OpenAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OracleException>();
        logger.WarningCount.Should().Be(0);
    }
}
