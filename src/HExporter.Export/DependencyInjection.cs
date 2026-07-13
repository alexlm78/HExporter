using HExporter.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HExporter.Export;

public static class DependencyInjection
{
    public static IServiceCollection AddHExporterWriters(this IServiceCollection services)
    {
        services.AddSingleton<IExportWriterFactory, ExportWriterFactory>();
        return services;
    }
}
