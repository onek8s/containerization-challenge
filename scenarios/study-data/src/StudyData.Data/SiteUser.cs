using Microsoft.AspNetCore.Identity;

namespace StudyData.Data;

/// <summary>Site staff account (investigators, study coordinators). CRO staff sign in through ADFS.</summary>
public class SiteUser : IdentityUser
{
    public string FullName { get; set; } = "";

    /// <summary>Site role in the delegation log: Investigator, SubInvestigator, Coordinator, Pharmacist.</summary>
    public string SiteRole { get; set; } = "Coordinator";

    public bool Active { get; set; } = true;
    public DateTime? TrainingCompletedAt { get; set; }

    public List<SiteAssignment> Sites { get; set; } = new();
}

public class SiteAssignment
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int SiteId { get; set; }

    /// <summary>May sign casebooks (principal and sub-investigators only).</summary>
    public bool CanSign { get; set; }
}

public static class Roles
{
    public const string SiteStaff = "SiteStaff";
    public const string Investigator = "Investigator";

    // CRO roles come from ADFS group claims.
    public const string Monitor = "Monitor";
    public const string DataManager = "DataManager";
    public const string MedicalCoder = "MedicalCoder";
    public const string StudyAdmin = "StudyAdmin";
}
