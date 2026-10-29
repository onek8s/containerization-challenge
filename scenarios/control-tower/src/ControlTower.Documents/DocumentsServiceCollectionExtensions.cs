using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Proseware.ControlTower.Documents.Labels;
using Proseware.ControlTower.Documents.Pdf;
using Proseware.ControlTower.Documents.Storage;

namespace Proseware.ControlTower.Documents;

public static class DocumentsServiceCollectionExtensions
{
    /// <summary>Document store only (reading and storing documents).</summary>
    public static IServiceCollection AddControlTowerDocumentStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DocumentOptions>(configuration.GetSection(DocumentOptions.Section));
        var store = configuration[$"{DocumentOptions.Section}:Store"] ?? "FileStream";
        if (string.Equals(store, "Folder", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IDocumentStore, FolderDocumentStore>();
        else
            services.AddScoped<IDocumentStore, SqlFileStreamDocumentStore>();
        return services;
    }

    /// <summary>Store plus PDF generation.</summary>
    public static IServiceCollection AddControlTowerDocuments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControlTowerDocumentStore(configuration);
        services.AddSingleton<IHtmlToPdfConverter, WkHtmlToPdfConverter>();
        services.AddSingleton<RazorShipmentDocumentRenderer>();
        services.AddSingleton<IShipmentDocumentRenderer>(sp => sp.GetRequiredService<RazorShipmentDocumentRenderer>());
        services.AddScoped<IShipmentDocumentService, ShipmentDocumentService>();
        return services;
    }

    public static IServiceCollection AddControlTowerLabelPrinting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PrinterOptions>(configuration.GetSection(PrinterOptions.Section));
        services.AddSingleton<ZplLabelPrinter>();
        services.AddSingleton<ILabelPrinter>(sp => sp.GetRequiredService<ZplLabelPrinter>());
        return services;
    }
}
