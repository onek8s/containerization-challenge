using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Woodgrove.Statements.Core.Delivery;
using Woodgrove.Statements.Core.Model;

namespace Woodgrove.Statements.Data
{
    public class DeliveryRepository
    {
        const string Select = @"
SELECT d.DeliveryId, d.RunId, d.CustomerId, c.CustomerNo, c.Name AS CustomerName, d.Method, d.Target, d.DocumentName, d.ArchivePath,
       d.Status, d.Attempts, d.NextAttemptAt, d.LastError, d.CreatedAt, d.DeliveredAt, s.Name AS SubscriptionName, s.EmailSubject
  FROM stm.Delivery d
  JOIN stm.SubscriptionRun r ON r.RunId = d.RunId
  JOIN stm.Subscription s ON s.SubscriptionId = r.SubscriptionId
  LEFT JOIN stm.Customer c ON c.CustomerId = d.CustomerId";
        readonly Db _db;

        public DeliveryRepository(Db db) { _db = db; }

        public long Add(DeliveryRecord d)
        {
            using (var c = _db.Open())
                return c.ExecuteScalar<long>(@"
INSERT stm.Delivery (RunId, CustomerId, Method, Target, DocumentName, ArchivePath, Status, Attempts, NextAttemptAt, LastError, DeliveredAt)
VALUES (@RunId, @CustomerId, @Method, LEFT(@Target, 1000), @DocumentName, @ArchivePath, @Status, @Attempts, @NextAttemptAt, LEFT(@LastError, 2000), @DeliveredAt);
SELECT CAST(SCOPE_IDENTITY() AS bigint);", d);
        }

        public void Update(long deliveryId, string status, int attempts, DateTime? nextAttemptAt, string lastError)
        {
            using (var c = _db.Open())
                c.Execute(@"UPDATE stm.Delivery SET Status = @status, Attempts = @attempts, NextAttemptAt = @nextAttemptAt, LastError = LEFT(@lastError, 2000),
                                   DeliveredAt = CASE WHEN @status = 'Sent' THEN SYSDATETIME() ELSE DeliveredAt END
                             WHERE DeliveryId = @deliveryId", new { deliveryId, status, attempts, nextAttemptAt, lastError });
        }

        public IList<DeliveryRecord> DueRetries(DateTime now, int max = 50)
        {
            using (var c = _db.Open())
                return c.Query<DeliveryRecord>(Select.Replace("SELECT d.DeliveryId", "SELECT TOP (@max) d.DeliveryId") +
                    " WHERE d.Status = 'Retry' AND d.NextAttemptAt <= @now ORDER BY d.NextAttemptAt", new { max, now }).ToList();
        }

        /// <summary>Resend from the portal: the retry loop picks the delivery up at its next poll.</summary>
        public void Resend(long deliveryId)
        {
            using (var c = _db.Open())
                c.Execute(@"UPDATE stm.Delivery SET Status = 'Retry', NextAttemptAt = SYSDATETIME() WHERE DeliveryId = @deliveryId AND Status IN ('Failed', 'Sent')",
                          new { deliveryId });
        }

        public IList<DeliveryRecord> ForRun(int runId)
        {
            using (var c = _db.Open())
                return c.Query<DeliveryRecord>(Select + " WHERE d.RunId = @runId ORDER BY d.DeliveryId", new { runId }).ToList();
        }

        public IList<DeliveryRecord> ForCustomer(int customerId, int max = 50)
        {
            using (var c = _db.Open())
                return c.Query<DeliveryRecord>(Select.Replace("SELECT d.DeliveryId", "SELECT TOP (@max) d.DeliveryId") +
                    " WHERE d.CustomerId = @customerId ORDER BY d.CreatedAt DESC", new { max, customerId }).ToList();
        }

        public IList<DeliveryRecord> Problems(int max = 100)
        {
            using (var c = _db.Open())
                return c.Query<DeliveryRecord>(Select.Replace("SELECT d.DeliveryId", "SELECT TOP (@max) d.DeliveryId") +
                    " WHERE d.Status IN ('Retry', 'Failed') ORDER BY d.CreatedAt DESC", new { max }).ToList();
        }

        public DeliveryRecord Get(long deliveryId)
        {
            using (var c = _db.Open())
                return c.QuerySingleOrDefault<DeliveryRecord>(Select + " WHERE d.DeliveryId = @deliveryId", new { deliveryId });
        }
    }
}
