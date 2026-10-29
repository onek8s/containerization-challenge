using System;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Tenancies
{
    public enum EnquiryTopic
    {
        Rent = 1,
        Repairs = 2,
        AntiSocialBehaviour = 3,
        TenancyChange = 4,
        Complaint = 5,
        Other = 6
    }

    /// <summary>A message a tenant sends from the portal to the housing team.</summary>
    public class TenantEnquiry : Entity
    {
        public int TenancyId { get; set; }
        public Tenancy Tenancy { get; set; }
        public EnquiryTopic Topic { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string AssignedTo { get; set; }
        public string Response { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string RespondedBy { get; set; }

        /// <summary>Complaints are answered within 10 working days (stage 1), other enquiries within 5.</summary>
        public int TargetWorkingDays => Topic == EnquiryTopic.Complaint ? 10 : 5;
    }
}
