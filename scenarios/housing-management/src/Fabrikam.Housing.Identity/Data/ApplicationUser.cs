using System;
using Microsoft.AspNetCore.Identity;

namespace Fabrikam.Housing.Identity.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public bool IsStaff { get; set; }
        /// <summary>Housing database Tenant.Id for portal users.</summary>
        public int? TenantId { get; set; }
        public string TenancyReference { get; set; }
        /// <summary>Housing patch of a housing officer (from AD extensionAttribute1).</summary>
        public string Patch { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSignInAt { get; set; }
    }
}
