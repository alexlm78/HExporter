using HExporter.Core.Abstractions;
using HExporter.Infrastructure.Oracle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HExporter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHExporterOracle(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OracleOptions>(config.GetSection(OracleOptions.SectionName));
        services.AddSingleton<OracleConnectionFactory>();
        services.AddSingleton<IRecordReaderFactory, OracleRecordReaderFactory>();
        return services;
    }
}
