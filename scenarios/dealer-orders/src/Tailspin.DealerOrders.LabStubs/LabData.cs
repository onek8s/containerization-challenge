using System;
using System.Collections.Generic;
using System.Linq;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.LabStubs
{
    /// <summary>Master data for test and training environments: dealers, trade agreements, discounts, freight, rates, EDI partners.</summary>
    public static class LabData
    {
        public static readonly DateTime PriceListStart = new DateTime(2018, 9, 1);

        /// <summary>EUR list prices of machines and options (LIST price group).</summary>
        public static readonly Dictionary<string, decimal> EuroListPrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["TF14"] = 268000m, ["TF10"] = 198500m, ["TH8"] = 445000m, ["TL450"] = 52400m,
            ["OPT-ENG205"] = 0m, ["OPT-ENG240"] = 14800m, ["OPT-ENGT4F"] = 2600m, ["OPT-ENG160"] = 0m, ["OPT-ENGT4F160"] = 2400m, ["OPT-ENGT4F240"] = 2900m, ["OPT-ENG55"] = 0m,
            ["OPT-CRANE10"] = 0m, ["OPT-CRANE11"] = 11200m, ["OPT-CRANE8"] = 0m, ["OPT-CRANEH10"] = 0m, ["OPT-CRANEH11"] = 12900m,
            ["OPT-GRP028"] = 0m, ["OPT-GRP036"] = 3400m, ["OPT-GRP022"] = 0m,
            ["OPT-LOADXL"] = 6900m, ["OPT-LOADVAR"] = 18500m, ["OPT-BOGIEHD"] = 7400m,
            ["OPT-TYRE710"] = 0m, ["OPT-TYRE780"] = 5200m, ["OPT-TYRE600"] = 0m,
            ["OPT-CABLEV"] = 16900m, ["OPT-CABLEVH"] = 17900m,
            ["OPT-TRKECO"] = 9800m, ["OPT-COLD"] = 4300m, ["OPT-TROPIC"] = 3900m, ["OPT-LED"] = 2650m, ["OPT-FIRE"] = 6100m, ["OPT-SCALE"] = 7800m,
            ["OPT-WINCH"] = 24500m, ["OPT-LINK3"] = 3600m, ["OPT-WEXT36"] = 11900m, ["OPT-TRAIN2"] = 2400m,
            ["OPT-H480"] = 0m, ["OPT-H560"] = 38500m, ["OPT-TMPRO"] = 9600m, ["OPT-CMARK"] = 3100m, ["OPT-STUMP"] = 2800m,
            ["OPT-BOOMTEL"] = 6400m, ["OPT-HSKID"] = 900m, ["OPT-BKTGP"] = 1450m, ["OPT-FORKS"] = 980m, ["OPT-SWEEP"] = 3900m, ["OPT-SNOW"] = 2750m, ["OPT-HEAT"] = 690m,
            ["P-10045"] = 64.50m, ["P-10046"] = 38.90m, ["P-10210"] = 89.00m, ["P-10211"] = 312.00m, ["P-20001"] = 1890.00m,
            ["P-20310"] = 4250.00m, ["P-30500"] = 1240.00m, ["P-30510"] = 2380.00m, ["P-40002"] = 42.80m, ["P-40100"] = 268.00m
        };

        public static IList<ExchangeRate> ExchangeRates() => new List<ExchangeRate>
        {
            new ExchangeRate { FromCurrency = "EUR", ToCurrency = "SEK", Rate = 10.4500m, ValidFrom = PriceListStart },
            new ExchangeRate { FromCurrency = "EUR", ToCurrency = "NOK", Rate = 9.8500m, ValidFrom = PriceListStart },
            new ExchangeRate { FromCurrency = "EUR", ToCurrency = "USD", Rate = 1.1200m, ValidFrom = PriceListStart },
            new ExchangeRate { FromCurrency = "EUR", ToCurrency = "CAD", Rate = 1.4900m, ValidFrom = PriceListStart },
            new ExchangeRate { FromCurrency = "EUR", ToCurrency = "GBP", Rate = 0.8900m, ValidFrom = PriceListStart }
        };

        /// <summary>
        /// Trade agreements: the EUR LIST group for all items plus own price lists in SEK, NOK and USD (machines and options
        /// rounded to whole hundreds as product management publishes them). CAD and GBP dealers are priced from EUR.
        /// </summary>
        public static IList<PriceEntry> Prices()
        {
            var prices = new List<PriceEntry>();
            // Options included in the base machine have a zero price agreement so they still resolve to a price.
            foreach (var kv in EuroListPrices)
            {
                prices.Add(new PriceEntry { ItemId = kv.Key, PriceGroup = "LIST", Currency = "EUR", Amount = kv.Value, QuantityFrom = 0, FromDate = PriceListStart });
                foreach (var local in new[] { Tuple.Create("DLR-SE", "SEK", 10.45m), Tuple.Create("DLR-NO", "NOK", 9.85m), Tuple.Create("DLR-US", "USD", 1.12m) })
                {
                    var amount = kv.Key.StartsWith("P-", StringComparison.Ordinal)
                        ? Math.Round(kv.Value * local.Item3, local.Item2 == "USD" ? 2 : 0)
                        : Math.Round(kv.Value * local.Item3 / 100m, 0) * 100m;
                    prices.Add(new PriceEntry { ItemId = kv.Key, PriceGroup = local.Item1, Currency = local.Item2, Amount = amount, QuantityFrom = 0, FromDate = PriceListStart });
                }
            }
            // Volume price for filters bought by the box.
            prices.Add(new PriceEntry { ItemId = "P-10045", PriceGroup = "LIST", Currency = "EUR", Amount = 55.00m, QuantityFrom = 24, FromDate = PriceListStart });
            return prices;
        }

        public static IList<DiscountEntry> Discounts()
        {
            var table = new Dictionary<string, decimal[]>
            {
                // A, B, C, Sub
                ["MACHINE"] = new[] { 18m, 15m, 12m, 8m },
                ["OPTION"] = new[] { 15m, 12m, 10m, 6m },
                ["ATTACHMENT"] = new[] { 20m, 18m, 15m, 10m },
                ["SERVICE"] = new[] { 0m, 0m, 0m, 0m },
                ["PARTS"] = new[] { 25m, 22m, 20m, 12m }
            };
            return table.SelectMany(kv => new[] { DiscountTier.A, DiscountTier.B, DiscountTier.C, DiscountTier.Sub }
                .Select((tier, i) => new DiscountEntry { Tier = tier, DiscountGroup = kv.Key, Percent = kv.Value[i] })).ToList();
        }

        public static IList<Campaign> Campaigns() => new List<Campaign>
        {
            new Campaign { Code = "AUT19-TF14", Name = "Autumn 2019 TF14 campaign", ModelCode = "TF14", Percent = 3m, FromDate = new DateTime(2019, 9, 1), ToDate = new DateTime(2019, 12, 31) },
            new Campaign { Code = "AUT19-LINK", Name = "TailspinLink free year", OptionCode = "TELEMATICS", Percent = 33.33m, FromDate = new DateTime(2019, 9, 1), ToDate = new DateTime(2019, 12, 31) }
        };

        public static IList<FreightRate> FreightRates()
        {
            var rates = new List<FreightRate>();
            var byCountry = new Dictionary<string, decimal> { ["FI"] = 1200m, ["SE"] = 2400m, ["NO"] = 3100m, ["DK"] = 3300m, ["EE"] = 2600m, ["DE"] = 4800m, ["AT"] = 5600m, ["PL"] = 4200m, ["GB"] = 6900m, ["FR"] = 6200m, ["US"] = 14500m, ["CA"] = 16800m };
            foreach (var kv in byCountry)
            {
                rates.Add(new FreightRate { CountryCode = kv.Key, Zone = "1", Family = ProductFamily.Forwarder, AmountEur = kv.Value });
                rates.Add(new FreightRate { CountryCode = kv.Key, Zone = "1", Family = ProductFamily.Harvester, AmountEur = Math.Round(kv.Value * 1.25m, 0) });
                rates.Add(new FreightRate { CountryCode = kv.Key, Zone = "1", Family = ProductFamily.CompactLoader, AmountEur = Math.Round(kv.Value * 0.3m, 0) });
            }
            return rates;
        }

        public static IList<Dealer> Dealers()
        {
            var list = new List<Dealer>
            {
                Dealer(1, "D-10010", "Metsäkone Pohjola Oy", "FI", "EUR", "fi", "LIST", DiscountTier.A, 3m, 1500000m, "NET30", DeliveryTerms.Cpt, "FI-KON", "NORTH-FI", "Mikko Laine", "tilaukset@metsakone-pohjola.example",
                    Address(1, "Metsäkone Pohjola, Rovaniemi", "Teollisuustie 14", "96320", "Rovaniemi", null, "FI", true, null),
                    Address(2, "Metsäkone Pohjola, Kajaani", "Kuurnankatu 3", "87100", "Kajaani", null, "FI", false, null)),
                Dealer(2, "D-10022", "Skogsmaskiner Norr AB", "SE", "SEK", "sv", "DLR-SE", DiscountTier.A, 3m, 18000000m, "NET30", DeliveryTerms.Cpt, "FI-KON", "SWEDEN", "Mikko Laine", "order@skogsmaskiner-norr.example",
                    Address(3, "Skogsmaskiner Norr, Umeå", "Västra Industrigatan 9", "903 30", "Umeå", null, "SE", true, null)),
                Dealer(3, "D-10031", "Skog og Anlegg AS", "NO", "NOK", "en", "DLR-NO", DiscountTier.B, 2m, 9000000m, "NET30", DeliveryTerms.Dap, "FI-KON", "NORWAY", "Anna Virtanen", "post@skog-anlegg.example",
                    Address(4, "Skog og Anlegg, Elverum", "Grundsetvegen 22", "2411", "Elverum", null, "NO", true, null)),
                Dealer(4, "D-10045", "Forsttechnik Bayern GmbH", "DE", "EUR", "de", "LIST", DiscountTier.B, 2m, 900000m, "NET45", DeliveryTerms.Dap, "FI-KON", "CENTRAL-EU", "Anna Virtanen", "bestellung@forsttechnik-bayern.example",
                    Address(5, "Forsttechnik Bayern", "Industriestraße 41", "94469", "Deggendorf", "BY", "DE", true, null)),
                Dealer(5, "D-20007", "Great Lakes Forestry Equipment Inc.", "US", "USD", "en", "DLR-US", DiscountTier.A, 3m, 2500000m, "NET45", DeliveryTerms.Fca, "US-DUL", "NA-EAST", "Jake Olson", "purchasing@greatlakes-forestry.example",
                    Address(6, "Great Lakes Forestry, Duluth", "4120 Airpark Blvd", "55811", "Duluth", "MN", "US", true, "GLF-DUL"),
                    Address(7, "Great Lakes Forestry, Marquette", "1600 US-41 W", "49855", "Marquette", "MI", "US", false, "GLF-MQT")),
                Dealer(6, "D-20011", "Northern Timber Machines Ltd.", "CA", "CAD", "en", "LIST", DiscountTier.B, 2m, 2000000m, "NET45", DeliveryTerms.Fca, "US-DUL", "NA-EAST", "Jake Olson", "orders@northern-timber.example",
                    Address(8, "Northern Timber Machines, Thunder Bay", "520 Fort William Rd", "P7B 2Z8", "Thunder Bay", "ON", "CA", true, "NTM-TB")),
                Dealer(7, "D-10051", "Eesti Metsatehnika OÜ", "EE", "EUR", "en", "LIST", DiscountTier.C, 1m, 300000m, "NET14", DeliveryTerms.Cpt, "FI-KON", "BALTIC", "Anna Virtanen", "info@metsatehnika.example",
                    Address(9, "Eesti Metsatehnika, Tartu", "Ringtee 37", "50105", "Tartu", null, "EE", true, null))
            };
            list[6].CreditHold = true;
            list[6].OpenBalance = 214000m;
            list[0].OpenBalance = 392000m;
            list[3].OpenBalance = 515000m;
            return list;
        }

        public static IList<EdiPartner> EdiPartners() => new List<EdiPartner>
        {
            new EdiPartner
            {
                Id = 1, Name = "Great Lakes Forestry", DealerId = 5, Transport = EdiTransport.As2, InterchangeQualifier = "01", InterchangeId = "078912345",
                ApplicationCode = "GLFEQUIP", As2Id = "GLFEQUIP", As2Url = "http://localhost:4090/as2", PartnerCertificateFile = "certs\\glf-as2.cer",
                EncryptOutbound = true, RequestSignedMdn = true, Send855 = true, Send856 = true, Active = true, LastInterchangeControl = 1000
            },
            new EdiPartner
            {
                Id = 2, Name = "Northern Timber Machines", DealerId = 6, Transport = EdiTransport.Sftp, InterchangeQualifier = "ZZ", InterchangeId = "NORTHTIMBER",
                ApplicationCode = "NTMLTD", SftpHost = "sftp.northern-timber.example", SftpPort = 22, SftpUser = "tailspin",
                SftpInboundFolder = "/outbox", SftpOutboundFolder = "/inbox", Send855 = true, Send856 = false, Active = true, LastInterchangeControl = 500
            }
        };

        private static Dealer Dealer(int id, string account, string name, string country, string currency, string language, string priceGroup, DiscountTier tier,
            decimal maxDiscount, decimal creditLimit, string paymentTerms, DeliveryTerms delivery, string warehouse, string district, string salesResponsible, string email,
            params DealerAddress[] addresses)
        {
            var dealer = new Dealer
            {
                Id = id, AccountNumber = account, Name = name, CountryCode = country, Currency = currency, LanguageCode = language, PriceGroup = priceGroup,
                DiscountTier = tier, MaxSelfApprovedDiscount = maxDiscount, CreditLimit = creditLimit, PaymentTerms = paymentTerms, DeliveryTerms = delivery,
                Warehouse = warehouse, SalesDistrict = district, SalesResponsible = salesResponsible, Email = email, Status = DealerStatus.Active
            };
            foreach (var a in addresses) { a.DealerId = id; dealer.Addresses.Add(a); }
            return dealer;
        }

        private static DealerAddress Address(int id, string name, string street, string postal, string city, string state, string country, bool isDefault, string ediCode) =>
            new DealerAddress { Id = id, Name = name, Street = street, PostalCode = postal, City = city, State = state, CountryCode = country, IsDefaultDelivery = isDefault, EdiLocationCode = ediCode };
    }
}
