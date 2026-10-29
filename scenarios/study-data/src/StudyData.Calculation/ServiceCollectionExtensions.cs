using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyData.Calculation.Development;
using StudyData.Calculation.Excel;
using StudyData.Core.Services;

namespace StudyData.Calculation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStudyCalculation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CalculationOptions>(configuration.GetSection(CalculationOptions.Section));
        var provider = configuration.GetSection(CalculationOptions.Section)["Provider"] ?? "Excel";
        services.AddSingleton<WorkbookStore>();

        if (provider.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDerivationService, DevelopmentDerivationService>();
            services.AddSingleton<IReportWorkbookRunner, DevelopmentReportRunner>();
            return services;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Excel calculation provider needs Windows with Microsoft Excel.");
        }

        services.AddSingleton<ExcelPool>();
        services.AddSingleton<IDerivationService, ExcelDerivationService>();
        services.AddSingleton<IReportWorkbookRunner, ExcelReportRunner>();
        return services;
    }
}
