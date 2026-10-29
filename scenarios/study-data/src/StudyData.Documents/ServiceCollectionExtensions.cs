using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyData.Documents.Development;
using StudyData.Documents.Printing;
using StudyData.Documents.Wpf;

namespace StudyData.Documents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStudyDocuments(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(DocumentOptions.Section);
        services.Configure<DocumentOptions>(section);

        if ((section["Provider"] ?? "Wpf").Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICasebookRenderer>(sp => new SvgCasebookRenderer(sp.GetRequiredService<IOptions<DocumentOptions>>().Value.RowsPerPage));
            return services;
        }

        services.AddSingleton<WpfRenderThread>();
        services.AddSingleton<PdfPrintQueue>();
        services.AddSingleton<ICasebookRenderer, WpfCasebookRenderer>();
        return services;
    }
}
