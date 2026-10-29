using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Data
{
    public class OrderRepository
    {
        private readonly Database _db;
        private readonly RequestRepository _requests;

        public OrderRepository(Database db)
        {
            _db = db;
            _requests = new RequestRepository(db);
        }

        public PurchaseOrder CreateFromRequest(int requestId, string buyerAccount, string deliveryAddress, DateTime orderDate)
        {
            using (var connection = _db.Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                using (var cmd = Database.Command(connection,
                    "UPDATE dbo.PurchaseRequests SET Status = @ordered WHERE Id = @id AND Status = @approved", tx))
                {
                    cmd.Parameters.AddWithValue("@ordered", (byte)RequestStatus.Ordered);
                    cmd.Parameters.AddWithValue("@approved", (byte)RequestStatus.Approved);
                    cmd.Parameters.AddWithValue("@id", requestId);
                    if (cmd.ExecuteNonQuery() != 1)
                    {
                        throw new PolicyException("Only approved requests can be ordered.");
                    }
                }

                long sequence;
                using (var cmd = Database.Command(connection, "SELECT NEXT VALUE FOR dbo.OrderNumbers", tx))
                {
                    sequence = Convert.ToInt64(cmd.ExecuteScalar());
                }

                var order = new PurchaseOrder
                {
                    PoNumber = DocumentNumbers.OrderNumber(sequence),
                    RequestId = requestId,
                    BuyerAccount = buyerAccount,
                    DeliveryAddress = deliveryAddress,
                    OrderDate = orderDate.Date
                };

                using (var cmd = Database.Command(connection,
                    "INSERT INTO dbo.PurchaseOrders (PoNumber, RequestId, BuyerAccount, OrderDate, DeliveryAddress) " +
                    "OUTPUT INSERTED.Id VALUES (@po, @request, @buyer, @date, @address)", tx))
                {
                    cmd.Parameters.AddWithValue("@po", order.PoNumber);
                    cmd.Parameters.AddWithValue("@request", requestId);
                    cmd.Parameters.AddWithValue("@buyer", buyerAccount);
                    cmd.Parameters.AddWithValue("@date", order.OrderDate);
                    cmd.Parameters.AddWithValue("@address", deliveryAddress);
                    order.Id = (int)cmd.ExecuteScalar();
                }

                tx.Commit();
                return order;
            }
        }

        public IList<PurchaseOrder> Recent(int days)
        {
            return Query("WHERE o.OrderDate >= @from ORDER BY o.Id DESC", DateTime.Today.AddDays(-days));
        }

        public IList<PurchaseOrder> NotExported()
        {
            return Query("WHERE o.ExportedAt IS NULL ORDER BY o.Id", null);
        }

        public PurchaseOrder Get(int id)
        {
            return Query("WHERE o.Id = @id", id).FirstOrDefault();
        }

        public void MarkExported(IEnumerable<int> orderIds, string fileName, DateTime at)
        {
            var ids = string.Join(",", orderIds);
            if (ids.Length == 0) return;

            using (var connection = _db.Open())
            using (var cmd = Database.Command(connection,
                "UPDATE dbo.PurchaseOrders SET ExportedAt = @at, ExportFile = @file WHERE Id IN (" + ids + ") AND ExportedAt IS NULL"))
            {
                cmd.Parameters.AddWithValue("@at", at);
                cmd.Parameters.AddWithValue("@file", fileName);
                cmd.ExecuteNonQuery();
            }
        }

        private IList<PurchaseOrder> Query(string where, object parameter)
        {
            var orders = new List<PurchaseOrder>();
            using (var connection = _db.Open())
            using (var cmd = Database.Command(connection,
                "SELECT o.Id, o.PoNumber, o.RequestId, r.Number, o.BuyerAccount, o.OrderDate, o.DeliveryAddress, o.ExportedAt, o.ExportFile, " +
                "s.Id AS SupplierId, s.Code, s.Name, s.VatNumber, s.Email, s.ErpVendorNo, s.Active " +
                "FROM dbo.PurchaseOrders o JOIN dbo.PurchaseRequests r ON r.Id = o.RequestId JOIN dbo.Suppliers s ON s.Id = r.SupplierId " + where))
            {
                if (where.Contains("@from")) cmd.Parameters.AddWithValue("@from", parameter);
                if (where.Contains("@id")) cmd.Parameters.AddWithValue("@id", parameter);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        orders.Add(new PurchaseOrder
                        {
                            Id = reader.GetInt32(0),
                            PoNumber = reader.GetString(1),
                            RequestId = reader.GetInt32(2),
                            RequestNumber = reader.GetString(3),
                            BuyerAccount = reader.GetString(4),
                            OrderDate = reader.GetDateTime(5),
                            DeliveryAddress = Database.GetString(reader, "DeliveryAddress"),
                            ExportedAt = Database.GetDate(reader, "ExportedAt"),
                            ExportFile = Database.GetString(reader, "ExportFile"),
                            Supplier = new Supplier
                            {
                                Id = reader.GetInt32(9),
                                Code = reader.GetString(10),
                                Name = reader.GetString(11),
                                VatNumber = Database.GetString(reader, "VatNumber"),
                                Email = Database.GetString(reader, "Email"),
                                ErpVendorNo = reader.GetString(14),
                                Active = reader.GetBoolean(15)
                            }
                        });
                    }
                }
            }

            foreach (var order in orders)
            {
                order.Request = _requests.Get(order.RequestId);
            }

            return orders;
        }
    }
}
