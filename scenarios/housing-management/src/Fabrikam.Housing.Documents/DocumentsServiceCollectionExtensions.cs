using Fabrikam.Housing.Documents.Excel;
using Fabrikam.Housing.Documents.Imaging;
using Fabrikam.Housing.Documents.Import;
using Fabrikam.Housing.Documents.Letters;
using Fabrikam.Housing.Documents.Pdf;
using Fabrikam.Housing.Documents.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fabrikam.Housing.Documents
{
    public static class DocumentsServiceCollectionExtensions
    {
        /// <summary>Registers PDF, letter, statement, Excel and import services (not the search index).</summary>
        public static IServiceCollection AddHousingDocuments(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DocumentsOptions>(configuration.GetSection("Documents"));
            services.AddSingleton<IPdfRenderer, PdfRenderer>();
            services.AddSingleton<ILetterComposer, LetterComposer>();
            services.AddSingleton<IStatementPdfWriter, StatementPdfWriter>();
            services.AddSingleton<IReportWorkbookWriter, ReportWorkbookWriter>();
            services.AddSingleton<IHousingBenefitScheduleReader, HousingBenefitScheduleReader>();
            return services;
        }

        /// <summary>Full text index, text extraction and thumbnails (document server only).</summary>
        public static IServiceCollection AddDocumentIndexing(this IServiceCollection services, string indexFolder)
        {
            services.AddSingleton<IDocumentIndex>(_ => new LuceneDocumentIndex(indexFolder));
            services.AddSingleton<ITextExtractor, TextExtractor>();
            services.AddSingleton<IThumbnailer, Thumbnailer>();
            return services;
        }
    }
}
