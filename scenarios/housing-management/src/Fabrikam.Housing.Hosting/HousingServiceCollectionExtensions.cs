using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoMapper;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fabrikam.Housing.Hosting
{
    public static class HousingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the database, domain services, integrations and document services shared by all applications.
        /// </summary>
        public static IServiceCollection AddHousingCore(this IServiceCollection services, IConfiguration configuration, params Assembly[] mappingAssemblies)
        {
            services.AddMemoryCache();
            services.AddHousingData(configuration);
            services.AddHousingIntegrations(configuration);
            services.AddHousingDocuments(configuration);
            services.AddAutoMapper(mappingAssemblies.Concat(new[] { typeof(HousingServiceCollectionExtensions).Assembly }).ToArray());

            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped(sp => LoadCalendar(sp));
            services.AddSingleton<RentCalendar>();
            services.AddScoped<RentChargeRun>();
            services.AddScoped<CollectionSchedule>();
            services.AddScoped<RepairRules>();
            services.AddSingleton(sp =>
            {
                var policy = new ArrearsPolicy();
                configuration.GetSection("Arrears").Bind(policy);
                return policy;
            });
            return services;
        }

        /// <summary>
        /// Bank holidays come from the database and are cached for a day.
        /// </summary>
        private static WorkingCalendar LoadCalendar(IServiceProvider sp)
        {
            var cache = sp.GetRequiredService<IMemoryCache>();
            return cache.GetOrCreate("bank-holidays", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                var db = sp.GetRequiredService<HousingDbContext>();
                var days = db.BankHolidays.Select(b => b.Date).ToList();
                return days.Count == 0 ? new WorkingCalendar() : new WorkingCalendar(days);
            });
        }

        /// <summary>
        /// Data protection keys (cookies, anti-forgery, OIDC state). Keys are kept in DataProtection:KeyFolder
        /// and, on the servers, encrypted to the machine with DPAPI.
        /// </summary>
        public static IServiceCollection AddHousingDataProtection(this IServiceCollection services, IConfiguration configuration, string applicationName)
        {
            var folder = configuration["DataProtection:KeyFolder"] ?? Path.Combine(@"D:\HousingData\keys", applicationName);
            var builder = services.AddDataProtection()
                .SetApplicationName("Fabrikam.Housing." + applicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(folder));
            if (configuration.GetValue("DataProtection:ProtectWithDpapi", true))
                builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
            return services;
        }
    }
}
