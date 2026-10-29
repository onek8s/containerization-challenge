using System.Web;
using Southridge.Clinic.Referrals;

namespace Southridge.Clinic.StaffWeb.Handlers
{
    /// <summary>Scanned referral documents and decision letters (lazy loaded blob through NHibernate).</summary>
    public class ReferralDocumentHandler : PdfHandler
    {
        protected override byte[] Create(HttpContext context, int id, out string fileName, ref string contentType)
        {
            ReferralDocument doc = ReferralSessions.InUnitOfWork(s =>
            {
                ReferralDocument d = s.Get<ReferralDocument>(id);
                if (d != null)
                {
                    NHibernate.NHibernateUtil.Initialize(d.Content);
                    byte[] touch = d.Content;
                }

                return d;
            });
            if (doc == null)
            {
                fileName = null;
                return null;
            }

            fileName = doc.FileName;
            contentType = doc.ContentType;
            return doc.Content;
        }
    }
}
