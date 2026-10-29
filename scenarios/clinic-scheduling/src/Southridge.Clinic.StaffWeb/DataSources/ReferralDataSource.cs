using System.Collections.Generic;
using System.ComponentModel;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Referrals;

namespace Southridge.Clinic.StaffWeb.DataSources
{
    /// <summary>ObjectDataSource over the NHibernate referral repository (session bound by ReferralSessionModule).</summary>
    [DataObject]
    public class ReferralDataSource
    {
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public IList<Referral> Queue(string specialtyCode)
        {
            return new ReferralRepository(ReferralSessions.Current).Queue(specialtyCode);
        }

        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public IList<Referral> Search(string text, string specialtyCode, int? status, int startRowIndex, int maximumRows)
        {
            int total;
            IList<Referral> rows = new ReferralRepository(ReferralSessions.Current).Search(new ReferralSearch
            {
                Text = text,
                SpecialtyCode = specialtyCode,
                Status = status.HasValue ? (ReferralStatus?)status.Value : null,
                PageIndex = maximumRows > 0 ? startRowIndex / maximumRows : 0,
                PageSize = maximumRows
            }, out total);
            System.Web.HttpContext.Current.Items["ReferralDataSource.Count"] = total;
            return rows;
        }

        public int SearchCount(string text, string specialtyCode, int? status)
        {
            object count = System.Web.HttpContext.Current.Items["ReferralDataSource.Count"];
            return count == null ? 0 : (int)count;
        }
    }
}
