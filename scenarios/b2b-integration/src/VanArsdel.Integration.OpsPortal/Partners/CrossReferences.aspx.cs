using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Data;
using VanArsdel.Integration.Domain.Partners;

namespace VanArsdel.Integration.OpsPortal.Partners
{
    public partial class CrossReferencesPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CrossReferencesPage));
        private TradingPartner _partner;

        [Dependency]
        public PartnerRepository Partners { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            var id = TradingPartner.NormalizeId(Request.QueryString["partner"]);
            _partner = Partners.LoadPartners().FirstOrDefault(p => p.PartnerId == id);
            if (_partner == null)
            {
                throw new HttpException(404, "Unknown partner '" + id + "'.");
            }

            PartnerTitle.Text = _partner.PartnerId + " " + _partner.Name;
            AddPanel.Visible = IsAdmin;
            XrefGrid.Columns[XrefGrid.Columns.Count - 1].Visible = IsAdmin;
        }

        public IQueryable<PartnerCrossReference> GetCrossReferences()
        {
            CrossReferenceKind kind;
            var all = Partners.LoadCrossReferences().Where(x => x.PartnerId == _partner.PartnerId || x.PartnerId == "*");
            if (Enum.TryParse(KindFilter.SelectedValue, out kind))
            {
                all = all.Where(x => x.Kind == kind);
            }

            return all.OrderBy(x => x.Kind).ThenBy(x => x.PartnerValue, StringComparer.OrdinalIgnoreCase).AsQueryable();
        }

        public void UpdateCrossReference(string partnerId, CrossReferenceKind kind, string partnerValue)
        {
            Demand(PageAccess.Administer);
            var item = Partners.LoadCrossReferences().FirstOrDefault(x => x.PartnerId == partnerId && x.Kind == kind && x.PartnerValue == partnerValue);
            if (item == null)
            {
                ModelState.AddModelError(string.Empty, "The cross reference was deleted by someone else.");
                return;
            }

            TryUpdateModel(item);
            if (!ModelState.IsValid)
            {
                return;
            }

            Partners.SaveCrossReference(item, UserName);
            Changed(string.Format(CultureInfo.InvariantCulture, "Updated {0} {1} {2} -> {3}", partnerId, kind, partnerValue, item.InternalValue));
        }

        public void DeleteCrossReference(string partnerId, CrossReferenceKind kind, string partnerValue)
        {
            Demand(PageAccess.Administer);
            Partners.DeleteCrossReference(partnerId, kind, partnerValue, UserName);
            Changed(string.Format(CultureInfo.InvariantCulture, "Deleted {0} {1} {2}", partnerId, kind, partnerValue));
        }

        protected void KindFilter_Changed(object sender, EventArgs e)
        {
            XrefGrid.PageIndex = 0;
            XrefGrid.DataBind();
        }

        protected void Add_Click(object sender, EventArgs e)
        {
            Demand(PageAccess.Administer);
            if (!IsValid)
            {
                return;
            }

            DateTime from;
            var item = new PartnerCrossReference
            {
                PartnerId = _partner.PartnerId,
                Kind = (CrossReferenceKind)Enum.Parse(typeof(CrossReferenceKind), NewKind.SelectedValue),
                PartnerValue = NewPartnerValue.Text.Trim(),
                InternalValue = NewInternalValue.Text.Trim(),
                ValidFrom = DateTime.TryParseExact(NewValidFrom.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from) ? from : (DateTime?)null
            };

            Partners.SaveCrossReference(item, UserName);
            Changed(string.Format(CultureInfo.InvariantCulture, "Added {0} {1} {2} -> {3}", item.PartnerId, item.Kind, item.PartnerValue, item.InternalValue));
            Response.Redirect(Request.RawUrl, false);
        }

        private void Changed(string what)
        {
            DatabaseXrefProvider.Invalidate();
            Log.InfoFormat("{0} by {1}", what, UserName);
            Flash(what + ". The maps use the new value within 10 minutes.");
        }
    }
}
