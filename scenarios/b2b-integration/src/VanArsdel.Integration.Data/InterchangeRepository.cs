using System;
using System.Collections.Generic;
using System.Data;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Edi;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.Data
{
    /// <summary>dbo.Interchange (outbound ack tracking), dbo.ReceivedInterchange (duplicates) and control numbers.</summary>
    public class InterchangeRepository : IDuplicateStore, IControlNumberSequence
    {
        private readonly IntegrationDatabase _db;

        public InterchangeRepository(IntegrationDatabase db)
        {
            _db = db;
        }

        public long RegisterOutbound(InterchangeRecord record)
        {
            using (var cmd = _db.Procedure("dbo.usp_RegisterOutboundInterchange"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, record.PartnerId);
                _db.In(cmd, "@ControlNumber", DbType.String, record.ControlNumber);
                _db.In(cmd, "@GroupControlNumber", DbType.String, record.GroupControlNumber);
                _db.In(cmd, "@TransactionType", DbType.String, record.TransactionType);
                _db.In(cmd, "@TransactionCount", DbType.Int32, record.TransactionCount);
                _db.In(cmd, "@CreatedUtc", DbType.DateTime2, record.CreatedUtc);
                _db.In(cmd, "@AckDueUtc", DbType.DateTime2, record.AckDueUtc);
                _db.In(cmd, "@FileName", DbType.String, record.FileName);
                _db.In(cmd, "@BodyHash", DbType.String, record.BodyHash);
                return Convert.ToInt64(_db.Database.ExecuteScalar(cmd));
            }
        }

        public bool ApplyAck(string partnerId, string acknowledgedControlNumber, AckStatus status, DateTime receivedUtc)
        {
            using (var cmd = _db.Procedure("dbo.usp_ApplyFunctionalAck"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                _db.In(cmd, "@ControlNumber", DbType.String, acknowledgedControlNumber);
                _db.In(cmd, "@AckStatusId", DbType.Int32, (int)status);
                _db.In(cmd, "@ReceivedUtc", DbType.DateTime2, receivedUtc);
                return Convert.ToInt32(_db.Database.ExecuteScalar(cmd)) > 0;
            }
        }

        public IList<InterchangeRecord> PendingAcks()
        {
            return Query("dbo.usp_GetInterchangesAwaitingAck", null, null, null);
        }

        public IList<InterchangeRecord> Search(string partnerId, DateTime fromUtc, DateTime toUtc)
        {
            return Query("dbo.usp_SearchInterchanges", partnerId, fromUtc, toUtc);
        }

        private IList<InterchangeRecord> Query(string procedure, string partnerId, DateTime? fromUtc, DateTime? toUtc)
        {
            var list = new List<InterchangeRecord>();
            using (var cmd = _db.Procedure(procedure))
            {
                if (fromUtc.HasValue)
                {
                    _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                    _db.In(cmd, "@FromUtc", DbType.DateTime2, fromUtc);
                    _db.In(cmd, "@ToUtc", DbType.DateTime2, toUtc);
                }

                using (var r = _db.Database.ExecuteReader(cmd))
                {
                    while (r.Read())
                    {
                        list.Add(new InterchangeRecord
                        {
                            InterchangeKey = Convert.ToInt64(r["InterchangeKey"]),
                            PartnerId = IntegrationDatabase.S(r, "PartnerId"),
                            Direction = (InterchangeDirection)IntegrationDatabase.I(r, "DirectionId"),
                            ControlNumber = IntegrationDatabase.S(r, "ControlNumber"),
                            GroupControlNumber = IntegrationDatabase.S(r, "GroupControlNumber"),
                            TransactionType = IntegrationDatabase.S(r, "TransactionType"),
                            TransactionCount = IntegrationDatabase.I(r, "TransactionCount"),
                            CreatedUtc = IntegrationDatabase.D(r, "CreatedUtc").GetValueOrDefault(),
                            AckStatus = (AckStatus)IntegrationDatabase.I(r, "AckStatusId"),
                            AckReceivedUtc = IntegrationDatabase.D(r, "AckReceivedUtc"),
                            AckDueUtc = IntegrationDatabase.D(r, "AckDueUtc"),
                            FileName = IntegrationDatabase.S(r, "FileName")
                        });
                    }
                }
            }

            return list;
        }

        public bool TryRegister(string partnerId, string key, DateTime receivedUtc, TimeSpan window)
        {
            using (var cmd = _db.Procedure("dbo.usp_RegisterReceivedInterchange"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                _db.In(cmd, "@DuplicateKey", DbType.String, key);
                _db.In(cmd, "@ReceivedUtc", DbType.DateTime2, receivedUtc);
                _db.In(cmd, "@WindowMinutes", DbType.Int32, (int)window.TotalMinutes);
                return Convert.ToInt32(_db.Database.ExecuteScalar(cmd)) == 1;
            }
        }

        public long Next(string partnerId, string level)
        {
            using (var cmd = _db.Procedure("dbo.usp_NextControlNumber"))
            {
                _db.In(cmd, "@PartnerId", DbType.String, partnerId);
                _db.In(cmd, "@Level", DbType.String, level);
                return Convert.ToInt64(_db.Database.ExecuteScalar(cmd));
            }
        }
    }
}
