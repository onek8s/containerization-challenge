namespace Humongous.Quoting.Domain;

public enum TradeAcceptance
{
    Standard,
    Refer,
    Decline
}

/// <summary>Underwriting trade from the trade list agreed with the capacity provider.</summary>
public sealed record Trade(
    string Code,
    string Description,
    string Group,
    TradeAcceptance Acceptance,
    bool ElAllowed = true,
    bool WorkAwayAllowed = true);

public static class TradeList
{
    // Trade list 2024 (UW guide section 3). RateCore holds the trade rates, this list
    // holds acceptance only.
    public static readonly IReadOnlyList<Trade> All =
    [
        new("47110", "Grocery and convenience store", "Retail", TradeAcceptance.Standard, WorkAwayAllowed: false),
        new("47190", "General retail shop", "Retail", TradeAcceptance.Standard, WorkAwayAllowed: false),
        new("47730", "Pharmacy", "Retail", TradeAcceptance.Refer, WorkAwayAllowed: false),
        new("47770", "Jeweller", "Retail", TradeAcceptance.Refer, WorkAwayAllowed: false),
        new("56101", "Restaurant", "Hospitality", TradeAcceptance.Standard, WorkAwayAllowed: false),
        new("56102", "Cafe and takeaway", "Hospitality", TradeAcceptance.Standard, WorkAwayAllowed: false),
        new("56302", "Public house and bar", "Hospitality", TradeAcceptance.Refer, WorkAwayAllowed: false),
        new("10710", "Bakery", "Food production", TradeAcceptance.Standard),
        new("43210", "Electrician", "Trades", TradeAcceptance.Standard),
        new("43220", "Plumber and heating engineer", "Trades", TradeAcceptance.Standard),
        new("43320", "Joiner and carpenter", "Trades", TradeAcceptance.Standard),
        new("43910", "Roofer", "Trades", TradeAcceptance.Refer),
        new("45200", "Vehicle repair garage", "Motor trade", TradeAcceptance.Standard),
        new("62020", "IT consultancy", "Offices", TradeAcceptance.Standard),
        new("69201", "Accountant", "Offices", TradeAcceptance.Standard),
        new("69102", "Solicitor", "Offices", TradeAcceptance.Refer),
        new("71111", "Architect", "Offices", TradeAcceptance.Standard),
        new("86230", "Dental practice", "Health", TradeAcceptance.Refer),
        new("96020", "Hairdresser and beauty salon", "Personal services", TradeAcceptance.Standard, WorkAwayAllowed: false),
        new("93130", "Gym and fitness centre", "Leisure", TradeAcceptance.Refer),
        new("20510", "Fireworks manufacture", "Manufacturing", TradeAcceptance.Decline),
        new("38320", "Waste recycling", "Waste", TradeAcceptance.Decline),
        new("92000", "Gambling and betting", "Leisure", TradeAcceptance.Decline),
    ];

    private static readonly Dictionary<string, Trade> ByCode = All.ToDictionary(t => t.Code);

    public static Trade? Find(string? code) =>
        code is not null && ByCode.TryGetValue(code.Trim(), out var trade) ? trade : null;
}
