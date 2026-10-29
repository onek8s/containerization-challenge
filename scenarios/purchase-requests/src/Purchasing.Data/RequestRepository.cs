using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Data
{
    public class RequestRepository
    {
        private const string SelectRequests =
            "SELECT r.Id, r.Number, r.RequesterAccount, r.RequesterName, r.RequesterEmail, r.CostCenterCode, c.Name AS CostCenterName, " +
            "r.SupplierId, s.Name AS SupplierName, r.Category, r.Title, r.Justification, r.NeededBy, r.Currency, r.Status, " +
            "r.CreatedAt, r.SubmittedAt, r.LastReminderAt, o.PoNumber " +
            "FROM dbo.PurchaseRequests r " +
            "JOIN dbo.CostCenters c ON c.Code = r.CostCenterCode " +
            "JOIN dbo.Suppliers s ON s.Id = r.SupplierId " +
            "LEFT JOIN dbo.PurchaseOrders o ON o.RequestId = r.Id ";

        private readonly Database _db;

        public RequestRepository(Database db)
        {
            _db = db;
        }

        public PurchaseRequest Get(int id)
        {
            using (var connection = _db.Open())
            {
                var list = Load(connection, null, "WHERE r.Id = @id", p => p.AddWithValue("@id", id));
                return list.FirstOrDefault();
            }
        }

        public IList<PurchaseRequest> ByRequester(string account)
        {
            using (var connection = _db.Open())
            {
                return Load(connection, null, "WHERE r.RequesterAccount = @account ORDER BY r.CreatedAt DESC",
                    p => p.AddWithValue("@account", account));
            }
        }

        public IList<PurchaseRequest> ByStatus(params RequestStatus[] statuses)
        {
            var codes = string.Join(",", statuses.Select(s => ((byte)s).ToString()));
            using (var connection = _db.Open())
            {
                return Load(connection, null, "WHERE r.Status IN (" + codes + ") ORDER BY r.SubmittedAt, r.Id", p => { });
            }
        }

        public PurchaseRequest Create(PurchaseRequest request)
        {
            using (var connection = _db.Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                long sequence;
                using (var cmd = Database.Command(connection, "SELECT NEXT VALUE FOR dbo.RequestNumbers", tx))
                {
                    sequence = Convert.ToInt64(cmd.ExecuteScalar());
                }

                request.Number = DocumentNumbers.RequestNumber(request.CreatedAt.Year, sequence);
                request.Status = RequestStatus.Draft;

                using (var cmd = Database.Command(connection,
                    "INSERT INTO dbo.PurchaseRequests (Number, RequesterAccount, RequesterName, RequesterEmail, CostCenterCode, SupplierId, " +
                    "Category, Title, Justification, NeededBy, Currency, Status, CreatedAt) " +
                    "OUTPUT INSERTED.Id VALUES (@number, @account, @name, @email, @cc, @supplier, @category, @title, @justification, " +
                    "@neededBy, @currency, @status, @createdAt)", tx))
                {
                    cmd.Parameters.AddWithValue("@number", request.Number);
                    cmd.Parameters.AddWithValue("@account", request.RequesterAccount);
                    cmd.Parameters.AddWithValue("@name", Database.DbValue(request.RequesterName));
                    cmd.Parameters.AddWithValue("@email", Database.DbValue(request.RequesterEmail));
                    cmd.Parameters.AddWithValue("@cc", request.CostCenterCode);
                    cmd.Parameters.AddWithValue("@supplier", request.SupplierId);
                    cmd.Parameters.AddWithValue("@category", request.Category);
                    cmd.Parameters.AddWithValue("@title", request.Title);
                    cmd.Parameters.AddWithValue("@justification", Database.DbValue(request.Justification));
                    cmd.Parameters.AddWithValue("@neededBy", Database.DbValue(request.NeededBy));
                    cmd.Parameters.AddWithValue("@currency", request.Currency);
                    cmd.Parameters.AddWithValue("@status", (byte)request.Status);
                    cmd.Parameters.AddWithValue("@createdAt", request.CreatedAt);
                    request.Id = (int)cmd.ExecuteScalar();
                }

                InsertLines(connection, tx, request);
                tx.Commit();
            }
            return request;
        }

        public void UpdateDraft(PurchaseRequest request)
        {
            using (var connection = _db.Open())
            using (var tx = connection.BeginTransaction())
            {
                using (var cmd = Database.Command(connection,
                    "UPDATE dbo.PurchaseRequests SET CostCenterCode = @cc, SupplierId = @supplier, Category = @category, Title = @title, " +
                    "Justification = @justification, NeededBy = @neededBy WHERE Id = @id AND Status IN (0, 3)", tx))
                {
                    cmd.Parameters.AddWithValue("@cc", request.CostCenterCode);
                    cmd.Parameters.AddWithValue("@supplier", request.SupplierId);
                    cmd.Parameters.AddWithValue("@category", request.Category);
                    cmd.Parameters.AddWithValue("@title", request.Title);
                    cmd.Parameters.AddWithValue("@justification", Database.DbValue(request.Justification));
                    cmd.Parameters.AddWithValue("@neededBy", Database.DbValue(request.NeededBy));
                    cmd.Parameters.AddWithValue("@id", request.Id);
                    if (cmd.ExecuteNonQuery() != 1)
                    {
                        throw new PolicyException("Request " + request.Number + " can no longer be edited.");
                    }
                }

                using (var cmd = Database.Command(connection, "DELETE FROM dbo.RequestLines WHERE RequestId = @id", tx))
                {
                    cmd.Parameters.AddWithValue("@id", request.Id);
                    cmd.ExecuteNonQuery();
                }

                InsertLines(connection, tx, request);
                tx.Commit();
            }
        }

        /// <summary>
        /// Saves status, workflow timestamps and any approval records that have not been stored yet.
        /// </summary>
        public void SaveWorkflow(PurchaseRequest request)
        {
            using (var connection = _db.Open())
            using (var tx = connection.BeginTransaction())
            {
                using (var cmd = Database.Command(connection,
                    "UPDATE dbo.PurchaseRequests SET Status = @status, SubmittedAt = @submittedAt, LastReminderAt = @reminder WHERE Id = @id", tx))
                {
                    cmd.Parameters.AddWithValue("@status", (byte)request.Status);
                    cmd.Parameters.AddWithValue("@submittedAt", Database.DbValue(request.SubmittedAt));
                    cmd.Parameters.AddWithValue("@reminder", Database.DbValue(request.LastReminderAt));
                    cmd.Parameters.AddWithValue("@id", request.Id);
                    cmd.ExecuteNonQuery();
                }

                if (request.Approvals.Count == 0)
                {
                    using (var cmd = Database.Command(connection, "DELETE FROM dbo.Approvals WHERE RequestId = @id", tx))
                    {
                        cmd.Parameters.AddWithValue("@id", request.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                foreach (var approval in request.Approvals.Where(a => a.Id == 0))
                {
                    using (var cmd = Database.Command(connection,
                        "INSERT INTO dbo.Approvals (RequestId, Step, ApproverAccount, ApproverName, Decision, Comment, DecidedAt) " +
                        "OUTPUT INSERTED.Id VALUES (@id, @step, @account, @name, @decision, @comment, @decidedAt)", tx))
                    {
                        cmd.Parameters.AddWithValue("@id", request.Id);
                        cmd.Parameters.AddWithValue("@step", (byte)approval.Step);
                        cmd.Parameters.AddWithValue("@account", approval.ApproverAccount);
                        cmd.Parameters.AddWithValue("@name", Database.DbValue(approval.ApproverName));
                        cmd.Parameters.AddWithValue("@decision", (byte)approval.Decision);
                        cmd.Parameters.AddWithValue("@comment", Database.DbValue(approval.Comment));
                        cmd.Parameters.AddWithValue("@decidedAt", approval.DecidedAt);
                        approval.Id = (int)cmd.ExecuteScalar();
                    }
                }

                tx.Commit();
            }
        }

        public void MarkReminded(int requestId, DateTime at)
        {
            using (var connection = _db.Open())
            using (var cmd = Database.Command(connection, "UPDATE dbo.PurchaseRequests SET LastReminderAt = @at WHERE Id = @id"))
            {
                cmd.Parameters.AddWithValue("@at", at);
                cmd.Parameters.AddWithValue("@id", requestId);
                cmd.ExecuteNonQuery();
            }
        }

        public AttachmentInfo AddAttachment(AttachmentInfo attachment, string storedName)
        {
            using (var connection = _db.Open())
            using (var cmd = Database.Command(connection,
                "INSERT INTO dbo.Attachments (RequestId, FileName, StoredName, ContentType, SizeBytes, UploadedBy, UploadedAt) " +
                "OUTPUT INSERTED.Id VALUES (@request, @file, @stored, @type, @size, @by, @at)"))
            {
                cmd.Parameters.AddWithValue("@request", attachment.RequestId);
                cmd.Parameters.AddWithValue("@file", attachment.FileName);
                cmd.Parameters.AddWithValue("@stored", storedName);
                cmd.Parameters.AddWithValue("@type", attachment.ContentType);
                cmd.Parameters.AddWithValue("@size", attachment.SizeBytes);
                cmd.Parameters.AddWithValue("@by", attachment.UploadedBy);
                cmd.Parameters.AddWithValue("@at", attachment.UploadedAt);
                attachment.Id = (int)cmd.ExecuteScalar();
            }
            return attachment;
        }

        public AttachmentInfo GetAttachment(int id, out string storedName)
        {
            storedName = null;
            using (var connection = _db.Open())
            using (var cmd = Database.Command(connection,
                "SELECT Id, RequestId, FileName, StoredName, ContentType, SizeBytes, UploadedBy, UploadedAt FROM dbo.Attachments WHERE Id = @id"))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    storedName = reader.GetString(3);
                    return ReadAttachment(reader);
                }
            }
        }

        private static void InsertLines(SqlConnection connection, SqlTransaction tx, PurchaseRequest request)
        {
            var lineNo = 1;
            foreach (var line in request.Lines)
            {
                line.LineNo = lineNo++;
                using (var cmd = Database.Command(connection,
                    "INSERT INTO dbo.RequestLines (RequestId, [LineNo], Description, Quantity, Unit, UnitPrice, VatRate) " +
                    "VALUES (@id, @line, @description, @quantity, @unit, @price, @vat)", tx))
                {
                    cmd.Parameters.AddWithValue("@id", request.Id);
                    cmd.Parameters.AddWithValue("@line", line.LineNo);
                    cmd.Parameters.AddWithValue("@description", line.Description);
                    cmd.Parameters.AddWithValue("@quantity", line.Quantity);
                    cmd.Parameters.AddWithValue("@unit", Database.DbValue(line.Unit));
                    cmd.Parameters.AddWithValue("@price", line.UnitPrice);
                    cmd.Parameters.AddWithValue("@vat", line.VatRate);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static IList<PurchaseRequest> Load(SqlConnection connection, SqlTransaction tx, string where, Action<SqlParameterCollection> parameters)
        {
            var requests = new List<PurchaseRequest>();
            using (var cmd = Database.Command(connection, SelectRequests + where, tx))
            {
                parameters(cmd.Parameters);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        requests.Add(new PurchaseRequest
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Number = reader.GetString(reader.GetOrdinal("Number")),
                            RequesterAccount = reader.GetString(reader.GetOrdinal("RequesterAccount")),
                            RequesterName = Database.GetString(reader, "RequesterName"),
                            RequesterEmail = Database.GetString(reader, "RequesterEmail"),
                            CostCenterCode = reader.GetString(reader.GetOrdinal("CostCenterCode")),
                            CostCenterName = reader.GetString(reader.GetOrdinal("CostCenterName")),
                            SupplierId = reader.GetInt32(reader.GetOrdinal("SupplierId")),
                            SupplierName = reader.GetString(reader.GetOrdinal("SupplierName")),
                            Category = reader.GetString(reader.GetOrdinal("Category")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),
                            Justification = Database.GetString(reader, "Justification"),
                            NeededBy = Database.GetDate(reader, "NeededBy"),
                            Currency = reader.GetString(reader.GetOrdinal("Currency")),
                            Status = (RequestStatus)reader.GetByte(reader.GetOrdinal("Status")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            SubmittedAt = Database.GetDate(reader, "SubmittedAt"),
                            LastReminderAt = Database.GetDate(reader, "LastReminderAt"),
                            PoNumber = Database.GetString(reader, "PoNumber")
                        });
                    }
                }
            }

            if (requests.Count == 0) return requests;

            var byId = requests.ToDictionary(r => r.Id);
            var ids = string.Join(",", byId.Keys);

            using (var cmd = Database.Command(connection,
                "SELECT RequestId, [LineNo], Description, Quantity, Unit, UnitPrice, VatRate FROM dbo.RequestLines " +
                "WHERE RequestId IN (" + ids + ") ORDER BY RequestId, [LineNo]", tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    byId[reader.GetInt32(0)].Lines.Add(new RequestLine
                    {
                        LineNo = reader.GetInt32(1),
                        Description = reader.GetString(2),
                        Quantity = reader.GetDecimal(3),
                        Unit = Database.GetString(reader, "Unit"),
                        UnitPrice = reader.GetDecimal(5),
                        VatRate = reader.GetDecimal(6)
                    });
                }
            }

            using (var cmd = Database.Command(connection,
                "SELECT Id, RequestId, Step, ApproverAccount, ApproverName, Decision, Comment, DecidedAt FROM dbo.Approvals " +
                "WHERE RequestId IN (" + ids + ") ORDER BY DecidedAt, Id", tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    byId[reader.GetInt32(1)].Approvals.Add(new ApprovalRecord
                    {
                        Id = reader.GetInt32(0),
                        Step = (ApprovalStep)reader.GetByte(2),
                        ApproverAccount = reader.GetString(3),
                        ApproverName = Database.GetString(reader, "ApproverName"),
                        Decision = (ApprovalDecision)reader.GetByte(5),
                        Comment = Database.GetString(reader, "Comment"),
                        DecidedAt = reader.GetDateTime(7)
                    });
                }
            }

            using (var cmd = Database.Command(connection,
                "SELECT Id, RequestId, FileName, StoredName, ContentType, SizeBytes, UploadedBy, UploadedAt FROM dbo.Attachments " +
                "WHERE RequestId IN (" + ids + ") ORDER BY Id", tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var attachment = ReadAttachment(reader);
                    byId[attachment.RequestId].Attachments.Add(attachment);
                }
            }

            return requests;
        }

        private static AttachmentInfo ReadAttachment(IDataRecord reader)
        {
            return new AttachmentInfo
            {
                Id = reader.GetInt32(0),
                RequestId = reader.GetInt32(1),
                FileName = reader.GetString(2),
                ContentType = reader.GetString(4),
                SizeBytes = reader.GetInt64(5),
                UploadedBy = reader.GetString(6),
                UploadedAt = reader.GetDateTime(7)
            };
        }
    }
}
