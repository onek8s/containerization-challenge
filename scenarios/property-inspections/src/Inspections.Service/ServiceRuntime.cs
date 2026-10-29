using System;
using Northwind.Inspections.Infrastructure.Configuration;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Service
{
    internal static class ServiceRuntime
    {
        private static readonly Lazy<Db> SharedDb = new Lazy<Db>(() => new Db(Settings.ConnectionString));
        private static readonly Lazy<PhotoStore> SharedStore = new Lazy<PhotoStore>(() => new PhotoStore(Settings, SharedDb.Value));

        public static InspectionSettings Settings
        {
            get { return InspectionSettings.Current; }
        }

        public static Db Db
        {
            get { return SharedDb.Value; }
        }

        public static PhotoStore Store
        {
            get { return SharedStore.Value; }
        }
    }
}
