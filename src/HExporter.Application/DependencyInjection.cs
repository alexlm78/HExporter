using HExporter.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HExporter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHExporterApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<ExportSecurityOptions>(config.GetSection(ExportSecurityOptions.SectionName));
        services.AddSingleton<ExportRequestValidator>();
        services.AddSingleton<ReportProfileLoader>();
        services.AddSingleton<ExportService>();
        return services;
    }
}
