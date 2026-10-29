using System.Collections.Generic;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Repairs;

namespace Fabrikam.Housing.Data.Seed
{
    /// <summary>
    /// Reference data that also goes into the production database (see db/04-reference-data.sql).
    /// </summary>
    public static class ReferenceData
    {
        public static IEnumerable<ScheduleOfRatesItem> ScheduleOfRates()
        {
            ScheduleOfRatesItem I(string code, string text, Trade trade, RepairPriority p, decimal price, string unit = "IT") =>
                new ScheduleOfRatesItem { Code = code, Description = text, Trade = trade, DefaultPriority = p, UnitPrice = price, Unit = unit, Active = true };

            return new[]
            {
                I("PL0101", "Renew tap washer or cartridge", Trade.Plumbing, RepairPriority.Routine, 38.50m),
                I("PL0105", "Renew kitchen mixer tap", Trade.Plumbing, RepairPriority.Routine, 96.20m),
                I("PL0210", "Repair WC cistern flush mechanism", Trade.Plumbing, RepairPriority.Urgent, 54.75m),
                I("PL0215", "Renew WC pan and seat", Trade.Plumbing, RepairPriority.Routine, 182.40m),
                I("PL0305", "Reseal bath or shower tray", Trade.Plumbing, RepairPriority.Routine, 46.10m, "LM"),
                I("PL0901", "Attend uncontrollable leak, make safe", Trade.Plumbing, RepairPriority.Emergency, 88.00m),
                I("DR0101", "Clear blocked WC (only WC)", Trade.Drainage, RepairPriority.Emergency, 72.30m),
                I("DR0201", "Clear blocked sink or basin waste", Trade.Drainage, RepairPriority.Urgent, 48.90m),
                I("DR0301", "Clear external gully or drain, jetting", Trade.Drainage, RepairPriority.Urgent, 118.00m),
                I("HT0101", "Attend no heating or hot water", Trade.Heating, RepairPriority.Emergency, 95.00m),
                I("HT0110", "Repressurise boiler and check expansion vessel", Trade.Heating, RepairPriority.Urgent, 64.20m),
                I("HT0204", "Bleed or balance radiator, renew valve", Trade.Heating, RepairPriority.Routine, 57.80m),
                I("HT0301", "Annual gas safety check and service (CP12)", Trade.Heating, RepairPriority.Planned, 62.00m),
                I("HT0310", "Renew combination boiler", Trade.Heating, RepairPriority.Planned, 1985.00m),
                I("EL0101", "Attend loss of power, make safe", Trade.Electrical, RepairPriority.Emergency, 92.00m),
                I("EL0205", "Renew damaged socket or switch", Trade.Electrical, RepairPriority.Urgent, 44.60m),
                I("EL0301", "Repair or renew light fitting", Trade.Electrical, RepairPriority.Routine, 51.30m),
                I("EL0410", "Repair or renew electric shower", Trade.Electrical, RepairPriority.Urgent, 245.00m),
                I("EL0601", "Renew mains smoke or heat alarm", Trade.Electrical, RepairPriority.Urgent, 68.40m),
                I("EL0901", "Electrical installation condition report (EICR)", Trade.Electrical, RepairPriority.Planned, 145.00m),
                I("CA0101", "Repair or renew front door lock", Trade.Carpentry, RepairPriority.Emergency, 86.50m),
                I("CA0301", "Repair kitchen unit door or drawer", Trade.Carpentry, RepairPriority.Routine, 42.00m),
                I("CA0305", "Renew kitchen worktop", Trade.Carpentry, RepairPriority.Routine, 64.00m, "LM"),
                I("CA0601", "Renew fence panel", Trade.Carpentry, RepairPriority.Routine, 79.90m),
                I("CA0701", "Ease and adjust internal door", Trade.Carpentry, RepairPriority.Routine, 36.00m),
                I("GL0101", "Board up and renew double glazed unit", Trade.Glazing, RepairPriority.Urgent, 212.00m),
                I("GL0201", "Renew window handle or friction hinge", Trade.Glazing, RepairPriority.Routine, 49.50m),
                I("RF0101", "Renew slipped or missing roof tiles", Trade.Roofing, RepairPriority.Urgent, 155.00m),
                I("RF0301", "Clear and realign gutter", Trade.Roofing, RepairPriority.Routine, 98.00m),
                I("PS0101", "Hack off and replaster wall", Trade.Plastering, RepairPriority.Routine, 41.00m, "M2"),
                I("DM0101", "Mould wash and treat, fungicidal paint", Trade.DampAndMould, RepairPriority.Urgent, 124.00m),
                I("DM0201", "Damp survey and report", Trade.DampAndMould, RepairPriority.Urgent, 165.00m),
                I("DM0301", "Install humidistat extractor fan", Trade.DampAndMould, RepairPriority.Routine, 210.00m),
                I("GN0101", "General handyperson, per hour", Trade.General, RepairPriority.Routine, 32.00m, "HR"),
                I("GN0201", "Remove bulky items from communal area", Trade.General, RepairPriority.Routine, 85.00m)
            };
        }

        public static IEnumerable<Contractor> Contractors()
        {
            Contractor C(string code, string name, string trades, string patches, bool ooh, int capacity, string sort, string account, bool vat, bool gas = false) =>
                new Contractor
                {
                    Code = code, Name = name, Trades = trades, Patches = patches, OutOfHours = ooh, WeeklyCapacity = capacity,
                    ContactEmail = "jobs@" + code.ToLowerInvariant() + ".example.co.uk", RemittanceEmail = "accounts@" + code.ToLowerInvariant() + ".example.co.uk",
                    ContactPhone = "0117 496 0" + (100 + capacity), BankSortCode = sort, BankAccountNumber = account, BankAccountName = name,
                    VatRegistered = vat, VatNumber = vat ? "GB" + account.Substring(0, 3) + " " + account.Substring(3, 4) + " 21" : null,
                    GasSafeRegistered = gas, GasSafeNumber = gas ? "5" + account.Substring(0, 5) : null,
                    ApiClientId = "contractor-" + code.ToLowerInvariant(), Active = true
                };

            return new[]
            {
                C("AVONPH", "Avon Plumbing & Heating Ltd", "Plumbing,Heating,Drainage", "*", true, 60, "40-11-62", "31926819", true, true),
                C("BRISEL", "Brislington Electrical Services", "Electrical", "*", true, 45, "20-00-00", "55779911", true),
                C("CLIFTJ", "Clifton Joinery & Glazing", "Carpentry,Glazing", "NORTH,EAST", false, 30, "30-94-12", "10293847", true),
                C("DUNDRY", "Dundry Roofing Co", "Roofing,Plastering", "*", false, 15, "60-83-71", "77001234", false),
                C("EASTON", "Easton Property Maintenance", "Carpentry,General,Plastering,Glazing", "SOUTH,EAST", false, 40, "09-01-28", "44556677", true),
                C("FROMEV", "Frome Valley Damp Solutions", "DampAndMould,Plastering", "*", false, 20, "11-00-01", "12345678", true),
                C("GASCAR", "GasCare South West", "Heating", "*", true, 80, "40-46-15", "66332211", true, true),
                C("HENLEA", "Henleaze Drainage", "Drainage,Plumbing", "NORTH", true, 25, "77-91-19", "90807060", false)
            };
        }
    }
}
