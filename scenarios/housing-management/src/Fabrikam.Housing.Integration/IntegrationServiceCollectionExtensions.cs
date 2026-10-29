using Fabrikam.Housing.Integration.Bacs;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Integration.Geo;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Integration.Simulated;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fabrikam.Housing.Integration
{
    public static class IntegrationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the payments, SMS, e-mail, postcode, GIS, Bacs bureau and document service clients.
        /// Integration:Mode = Simulated swaps every external service for a local simulator.
        /// </summary>
        public static IServiceCollection AddHousingIntegrations(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection("Integration");
            services.Configure<IntegrationOptions>(section);
            services.AddMemoryCache();
            services.AddSingleton<RestClientFactory>();
            services.AddSingleton<IDocumentServiceClient, DocumentServiceClient>();

            if (section.GetValue("Mode", IntegrationMode.Live) == IntegrationMode.Simulated)
            {
                services.AddSingleton<IPaymentsGateway, SimulatedPaymentsGateway>();
                services.AddSingleton<ISmsSender, SimulatedSmsSender>();
                services.AddSingleton<IEmailSender, SimulatedEmailSender>();
                services.AddSingleton<IPostcodeLookup, SimulatedPostcodeLookup>();
                services.AddSingleton<IPatchLocator, SimulatedPatchLocator>();
                services.AddSingleton<IBacsBureau, SimulatedBacsBureau>();
            }
            else
            {
                services.AddSingleton<IPaymentsGateway, PaystreamGateway>();
                services.AddSingleton<ISmsSender, TextlineSmsSender>();
                services.AddSingleton<IEmailSender, SmtpEmailSender>();
                services.AddSingleton<IPostcodeLookup, PostcodesIoLookup>();
                services.AddSingleton<IPatchLocator, ArcGisPatchLocator>();
                services.AddSingleton<IBacsBureau, SftpBacsBureau>();
            }
            return services;
        }
    }
}
