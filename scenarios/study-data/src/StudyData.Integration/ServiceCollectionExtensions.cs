using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyData.Integration.Labs;
using StudyData.Integration.Mail;
using StudyData.Integration.Odm;

namespace StudyData.Integration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStudyIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var lab = configuration.GetSection(LabTransferOptions.Section);
        services.Configure<LabTransferOptions>(lab);
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.Section));
        services.Configure<ExportOptions>(configuration.GetSection(ExportOptions.Section));

        if ((lab["Source"] ?? "Sftp").Equals("Folder", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ILabFileSource, FolderLabFileSource>();
        }
        else
        {
            services.AddSingleton<ILabFileSource, SftpLabFileSource>();
        }

        services.AddScoped<LabImportService>();
        services.AddScoped<OdmExporter>();
        services.AddSingleton<Mailer>();
        return services;
    }
}
