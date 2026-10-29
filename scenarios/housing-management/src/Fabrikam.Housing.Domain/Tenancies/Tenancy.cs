using System;
using System.Collections.Generic;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Tenancies
{
    public enum TenancyType
    {
        Starter = 1,
        Assured = 2,
        AssuredShorthold = 3,
        SharedOwnership = 4
    }

    public enum TenancyStatus
    {
        Current = 1,
        NoticeGiven = 2,
        Ended = 3
    }

    public enum ContactPreference
    {
        Email = 1,
        Sms = 2,
        Letter = 3
    }

    public class Tenancy : AuditedEntity
    {
        public string Reference { get; set; }
        public int PropertyId { get; set; }
        public Property Property { get; set; }
        public TenancyType Type { get; set; }
        public TenancyStatus Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        /// <summary>Weekly charge agreed at sign-up or at the last rent review.</summary>
        public decimal WeeklyCharge { get; set; }
        public bool HousingBenefitClaimed { get; set; }
        public string HousingBenefitClaimRef { get; set; }
        public bool UniversalCredit { get; set; }
        public List<Tenant> Tenants { get; set; } = new List<Tenant>();
    }

    public class Tenant : AuditedEntity
    {
        public int TenancyId { get; set; }
        public Tenancy Tenancy { get; set; }
        public bool IsLeadTenant { get; set; }
        public string Title { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Email { get; set; }
        public string MobilePhone { get; set; }
        public ContactPreference ContactPreference { get; set; }
        public bool NeedsLargePrint { get; set; }
        public bool Vulnerable { get; set; }
        public string VulnerabilityNotes { get; set; }
        /// <summary>Subject id of the tenant's portal account on the identity server, if registered.</summary>
        public string PortalSubjectId { get; set; }

        public string FullName => (Title + " " + FirstName + " " + LastName).Trim();
    }
}
