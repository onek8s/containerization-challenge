namespace Litware.DocControl.Transmittals;

public enum TransmittalPurpose
{
    /// <summary>IFR, issued for review.</summary>
    ForReview = 0,
    /// <summary>IFA, issued for approval.</summary>
    ForApproval = 1,
    /// <summary>IFC, issued for construction.</summary>
    ForConstruction = 2,
    /// <summary>IFI, issued for information.</summary>
    ForInformation = 3,
}

public static class TransmittalPurposeExtensions
{
    public static string Code(this TransmittalPurpose purpose) => purpose switch
    {
        TransmittalPurpose.ForReview => "IFR",
        TransmittalPurpose.ForApproval => "IFA",
        TransmittalPurpose.ForConstruction => "IFC",
        TransmittalPurpose.ForInformation => "IFI",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };

    public static string Description(this TransmittalPurpose purpose) => purpose switch
    {
        TransmittalPurpose.ForReview => "Issued for review",
        TransmittalPurpose.ForApproval => "Issued for approval",
        TransmittalPurpose.ForConstruction => "Issued for construction",
        TransmittalPurpose.ForInformation => "Issued for information",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
    };
}
