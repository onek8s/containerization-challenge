using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Tailspin.DealerOrders.AxIntegration;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Data.Stores;
using Tailspin.DealerOrders.Infrastructure;
using Tailspin.DealerOrders.LabStubs;
using Tailspin.DealerOrders.Processing.Ports;
using Tailspin.DealerOrders.Processing.Sync;

namespace Tailspin.DealerOrders.LabTools
{
    /// <summary>Lab data in SQL: the seven lab dealers with addresses, their EDI partners and portal users.</summary>
    internal static class SeedCommand
    {
        public static int Run(Options options)
        {
            var password = options["password", "Lab-Passw0rd"];
            var dealerIds = new Dictionary<int, int>();
            using (var db = new DealerOrdersContext())
            {
                foreach (var lab in LabData.Dealers())
                {
                    var existing = db.Dealers.SingleOrDefault(d => d.AccountNumber == lab.AccountNumber);
                    if (existing != null)
                    {
                        dealerIds[lab.Id] = existing.Id;
                        continue;
                    }
                    var labId = lab.Id;
                    lab.Id = 0;
                    foreach (var a in lab.Addresses) a.Id = 0;
                    lab.LastSyncedUtc = DateTime.UtcNow;
                    db.Dealers.Add(lab);
                    db.SaveChanges();
                    dealerIds[labId] = lab.Id;
                    Console.WriteLine($"Dealer {lab.AccountNumber} {lab.Name} -> {lab.Id}");
                }

                foreach (var partner in LabData.EdiPartners())
                {
                    if (db.EdiPartners.Any(p => p.Id == partner.Id)) continue;
                    partner.DealerId = dealerIds[partner.DealerId];
                    db.EdiPartners.Add(partner);
                    db.SaveChanges();
                    var dealer = db.Dealers.Find(partner.DealerId);
                    dealer.EdiPartnerId = partner.Id;
                    db.SaveChanges();
                    Console.WriteLine($"EDI partner {partner.Name} ({partner.Transport})");
                }

                var users = new UserManager<PortalUser>(new UserStore<PortalUser>(db))
                {
                    PasswordValidator = new PasswordValidator { RequiredLength = 10, RequireDigit = true, RequireLowercase = true, RequireUppercase = true }
                };
                foreach (var dealer in db.Dealers.ToList())
                {
                    var prefix = dealer.AccountNumber.ToLowerInvariant().Replace("-", "");
                    CreateUser(users, dealer, prefix + ".admin", "Admin " + dealer.AccountNumber, password, PortalRoles.DealerAdmin, PortalRoles.DealerSales, PortalRoles.DealerParts);
                    CreateUser(users, dealer, prefix + ".sales", "Sales " + dealer.AccountNumber, password, PortalRoles.DealerSales);
                    CreateUser(users, dealer, prefix + ".parts", "Parts " + dealer.AccountNumber, password, PortalRoles.DealerParts);
                }
            }
            return 0;
        }

        private static void CreateUser(UserManager<PortalUser> users, Domain.Dealer dealer, string userName, string displayName, string password, params string[] roles)
        {
            if (users.FindByName(userName) != null) return;
            var user = new PortalUser
            {
                UserName = userName,
                Email = userName + "@" + dealer.Email.Split('@')[1],
                DisplayName = displayName,
                DealerId = dealer.Id,
                LanguageCode = dealer.LanguageCode,
                CreatedUtc = DateTime.UtcNow,
                LockoutEnabled = true
            };
            var result = users.Create(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(userName + ": " + string.Join(" ", result.Errors));
            users.AddToRoles(user.Id, roles);
            Console.WriteLine($"Portal user {userName} ({string.Join(", ", roles)})");
        }

        /// <summary>One pass of the price, stock, item, rate and balance sync, as the sync service runs it.</summary>
        public static int Sync(Options options)
        {
            using (var ax = AxGateways.FromConfig())
            using (var db = new DealerOrdersContext())
            {
                var connections = SqlConnectionFactory.FromConfig();
                var sync = new CacheSync(ax.Query, new SqlCacheStore(connections), new EfDealerStore(db),
                    new Processing.Ports.SystemClock(Settings.Get("Orders.FactoryTimeZone", "FLE Standard Time")), Settings.Processing());
                Console.WriteLine($"AX mode {ax.Mode}");
                Console.WriteLine($"items {sync.SyncItems()}, rates {sync.SyncExchangeRates()}, prices {sync.SyncPrices()}, stock {sync.SyncStock()}, balances {sync.SyncDealerBalances()}");
            }
            return 0;
        }
    }
}
