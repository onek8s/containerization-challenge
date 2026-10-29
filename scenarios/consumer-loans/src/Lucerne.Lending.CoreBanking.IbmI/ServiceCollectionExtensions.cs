using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Lucerne.Lending.CoreBanking.IbmI;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddIbmICore(this IServiceCollection services, bool withEventSource = false)
    {
        services.AddSingleton<CwbxPool>();
        services.AddSingleton<ProgramCaller>();
        services.AddSingleton<OdbcCore>();
        services.AddSingleton<OleDbHistory>();
        services.AddSingleton<ICoreCustomerService, IbmICustomerService>();
        services.AddSingleton<ICoreLoanService, IbmILoanService>();
        services.AddSingleton<ICoreReferenceData, IbmIReferenceData>();
        if (withEventSource) services.AddSingleton<ICoreEventSource, IbmIEventSource>();
        return services;
    }

    [SupportedOSPlatform("windows")]
    public static bool ClientInstalled() => Cwbx.IsRegistered();
}
