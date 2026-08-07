using HExporter.Core.Abstractions;
using HExporter.Infrastructure.Oracle;
using HExporter.Infrastructure.Postgres;
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

    public static IServiceCollection AddHExporterPostgres(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PostgresOptions>(config.GetSection(PostgresOptions.SectionName));
        services.AddSingleton<PostgresConnectionFactory>();
        services.AddSingleton<IRecordReaderFactory, PostgresRecordReaderFactory>();
        return services;
    }

    /// <summary>Registra el adaptador de base de datos correspondiente al motor seleccionado
    /// (ver <see cref="DatabaseEngineResolver"/>). Un único <see cref="IRecordReaderFactory"/>
    /// queda registrado por ejecución — no hay despacho en tiempo de fila.</summary>
    public static IServiceCollection AddHExporterDatabase(
        this IServiceCollection services, IConfiguration config, DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.Oracle => services.AddHExporterOracle(config),
        DatabaseEngine.Postgres => services.AddHExporterPostgres(config),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unsupported database engine.")
    };
}
