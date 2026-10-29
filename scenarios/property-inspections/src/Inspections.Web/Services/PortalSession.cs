using System.Collections.Generic;
using System.Web;
using Northwind.Inspections.Web.Models;

namespace Northwind.Inspections.Web.Services
{
    /// <summary>
    /// Values kept in ASP.NET session state (SQL Server mode, so everything stored must be serializable).
    /// </summary>
    public static class PortalSession
    {
        private const string RecentKey = "recent-inspections";
        private const string DraftKey = "inspection-draft";
        private const string NoticeKey = "upload-notices";

        private static HttpSessionStateBase Session
        {
            get { return new HttpSessionStateWrapper(HttpContext.Current.Session); }
        }

        public static List<RecentInspection> Recent
        {
            get
            {
                var list = Session[RecentKey] as List<RecentInspection>;
                if (list == null)
                {
                    list = new List<RecentInspection>();
                    Session[RecentKey] = list;
                }
                return list;
            }
        }

        public static void Visited(int id, string claimNumber)
        {
            var list = Recent;
            list.RemoveAll(r => r.Id == id);
            list.Insert(0, new RecentInspection { Id = id, ClaimNumber = claimNumber });
            if (list.Count > 8)
            {
                list.RemoveRange(8, list.Count - 8);
            }
            Session[RecentKey] = list;
        }

        /// <summary>The new inspection form is kept while the adjuster looks up the claim in another tab.</summary>
        public static InspectionForm Draft
        {
            get { return Session[DraftKey] as InspectionForm; }
            set { Session[DraftKey] = value; }
        }

        public static List<string> TakeNotices()
        {
            var list = Session[NoticeKey] as List<string> ?? new List<string>();
            Session.Remove(NoticeKey);
            return list;
        }

        public static void AddNotice(string notice)
        {
            var list = Session[NoticeKey] as List<string> ?? new List<string>();
            list.Add(notice);
            Session[NoticeKey] = list;
        }
    }
}
