using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Security;

namespace Southridge.Clinic.StaffWeb.DataSources
{
    public class StaffUser
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Roles { get; set; }
        public bool IsApproved { get; set; }
        public bool IsLockedOut { get; set; }
        public System.DateTime LastLoginDate { get; set; }
    }

    [DataObject]
    public class UserDataSource
    {
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public List<StaffUser> All(string role)
        {
            var list = new List<StaffUser>();
            foreach (MembershipUser user in Membership.GetAllUsers())
            {
                string[] roles = System.Web.Security.Roles.GetRolesForUser(user.UserName);
                if (!string.IsNullOrEmpty(role) && !roles.Contains(role))
                {
                    continue;
                }

                list.Add(new StaffUser
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    FullName = StaffProfile.For(user.UserName).FullName,
                    Roles = string.Join(", ", roles),
                    IsApproved = user.IsApproved,
                    IsLockedOut = user.IsLockedOut,
                    LastLoginDate = user.LastLoginDate
                });
            }

            return list.OrderBy(u => u.UserName).ToList();
        }
    }
}
