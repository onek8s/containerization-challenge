using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lucerne.Lending.Integration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLendingIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BureauOptions>(configuration.GetSection(BureauOptions.Section));
        services.Configure<ESignOptions>(configuration.GetSection(ESignOptions.Section));
        services.Configure<PaymentsHubOptions>(configuration.GetSection(PaymentsHubOptions.Section));
        services.Configure<GeneralLedgerOptions>(configuration.GetSection(GeneralLedgerOptions.Section));
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.Section));
        services.AddSingleton<ICreditBureau, CreditBureauClient>();
        services.AddSingleton<IESignClient, ESignClient>();
        services.AddHttpClient<PaymentsHubClient>();
        services.AddSingleton<GeneralLedgerFileWriter>();
        services.AddSingleton<IMailSender, SmtpMailSender>();
        return services;
    }
}
