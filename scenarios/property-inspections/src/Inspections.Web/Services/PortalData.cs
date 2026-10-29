using System.Configuration;
using Northwind.Inspections.Infrastructure.Data;

namespace Northwind.Inspections.Web.Services
{
    public static class PortalData
    {
        private static readonly Db SharedDb = new Db(ConfigurationManager.ConnectionStrings["Inspections"].ConnectionString);

        public static Db Db
        {
            get { return SharedDb; }
        }
    }
}
