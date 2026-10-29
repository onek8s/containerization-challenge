using System.Collections.Generic;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Data
{
    public class CatalogRepository
    {
        private readonly Database _db;

        public CatalogRepository(Database db)
        {
            _db = db;
        }

        public IList<Supplier> Suppliers(bool activeOnly)
        {
            var result = new List<Supplier>();
            using (var connection = _db.Open())
            using (var command = Database.Command(connection,
                "SELECT Id, Code, Name, VatNumber, Email, ErpVendorNo, Active FROM dbo.Suppliers " +
                (activeOnly ? "WHERE Active = 1 " : "") + "ORDER BY Name"))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(new Supplier
                    {
                        Id = reader.GetInt32(0),
                        Code = reader.GetString(1),
                        Name = reader.GetString(2),
                        VatNumber = Database.GetString(reader, "VatNumber"),
                        Email = Database.GetString(reader, "Email"),
                        ErpVendorNo = reader.GetString(5),
                        Active = reader.GetBoolean(6)
                    });
                }
            }
            return result;
        }

        public IList<CostCenter> CostCenters()
        {
            var result = new List<CostCenter>();
            using (var connection = _db.Open())
            using (var command = Database.Command(connection,
                "SELECT Code, Name, ManagerAccount, ManagerEmail FROM dbo.CostCenters ORDER BY Code"))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    result.Add(new CostCenter
                    {
                        Code = reader.GetString(0),
                        Name = reader.GetString(1),
                        ManagerAccount = reader.GetString(2),
                        ManagerEmail = Database.GetString(reader, "ManagerEmail")
                    });
                }
            }
            return result;
        }
    }
}
