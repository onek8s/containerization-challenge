namespace Lucerne.Lending.Workflow;

public sealed class LendingOptions
{
    public const string Section = "Lending";

    public int OfferValidDays { get; set; } = 30;
    public int BureauReuseDays { get; set; } = 30;
    /// <summary>Signed applications up to this amount are booked automatically; larger ones by Operations.</summary>
    public decimal AutoBookingLimit { get; set; } = 20000m;
    /// <summary>Underwriter decisions over this amount, or with a price override, need a senior underwriter.</summary>
    public decimal SeniorApprovalLimit { get; set; } = 30000m;
    public string BranchCode { get; set; } = "0901";
    public string SystemUser { get; set; } = "LNWEB";
    public string CostCenter { get; set; } = "4410";
}

public static class GlAccounts
{
    public const string LoansReceivable = "1710";
    public const string CustomerDeposits = "2100";
    public const string OutgoingPayments = "1925";
    public const string FeeIncome = "3110";
    public const string ReminderFeeIncome = "3150";
}
