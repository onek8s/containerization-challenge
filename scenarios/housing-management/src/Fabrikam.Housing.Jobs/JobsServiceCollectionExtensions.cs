using System.Text;
using Fabrikam.Housing.Jobs.Infrastructure;
using Fabrikam.Housing.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fabrikam.Housing.Jobs
{
    public static class JobsServiceCollectionExtensions
    {
        public static IServiceCollection AddHousingJobs(this IServiceCollection services, IConfiguration configuration)
        {
            // Sage reads the journal files as Windows-1252.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            services.Configure<JobsOptions>(configuration.GetSection("Jobs"));
            services.AddScoped<PaystreamEventProcessor>();
            services.AddScoped<TenantMessenger>();
            foreach (var entry in JobCatalog.All)
                services.AddScoped(entry.JobType);
            return services;
        }
    }
}
