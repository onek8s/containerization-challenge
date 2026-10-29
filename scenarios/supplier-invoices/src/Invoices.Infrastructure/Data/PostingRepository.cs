using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Serialization;
using Wingtip.Invoices.Core.Erp;

namespace Wingtip.Invoices.Infrastructure.Data
{
    public class PostingBatch
    {
        public long BatchId { get; set; }
        public DateTime CreatedAt { get; set; }
        public IList<PostingInvoice> Invoices { get; set; }
    }

    public class PostingRepository
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(PostingInvoice));

        /// <summary>Stores a released invoice for the next import file. Safe to call again for the same invoice.</summary>
        public void Enqueue(PostingInvoice invoice)
        {
            string xml;
            using (var sw = new StringWriter())
            {
                Serializer.Serialize(sw, invoice);
                xml = sw.ToString();
            }

            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.QueuePosting"))
            {
                cmd.Add("@InvoiceId", SqlDbType.BigInt, invoice.InvoiceId);
                cmd.Add("@Payload", SqlDbType.NVarChar, xml, -1);
                cmd.ExecuteNonQuery();
            }
        }

        public int CountPending()
        {
            using (var cn = Db.Open())
            using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM inv.PostingQueue WHERE BatchId IS NULL", cn))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        /// <summary>Assigns pending invoices to a new batch. Returns null when nothing is pending.</summary>
        public PostingBatch CreateBatch(int maxInvoices)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.CreatePostingBatch"))
            {
                cmd.Add("@MaxInvoices", SqlDbType.Int, maxInvoices);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    var batch = new PostingBatch
                    {
                        BatchId = r.GetInt64(0),
                        CreatedAt = r.GetDateTime(1),
                        Invoices = new List<PostingInvoice>()
                    };
                    r.NextResult();
                    while (r.Read())
                    {
                        using (var sr = new StringReader(r.GetString(0)))
                        {
                            batch.Invoices.Add((PostingInvoice)Serializer.Deserialize(sr));
                        }
                    }
                    return batch.Invoices.Count == 0 ? null : batch;
                }
            }
        }

        /// <summary>Batches created but not written, e.g. after a share outage.</summary>
        public IList<PostingBatch> GetUnwrittenBatches()
        {
            var ids = new List<long>();
            using (var cn = Db.Open())
            using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT BatchId FROM inv.PostingBatch WHERE Status = 'Created' ORDER BY BatchId", cn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read()) ids.Add(r.GetInt64(0));
            }

            var list = new List<PostingBatch>();
            foreach (var id in ids)
            {
                using (var cn = Db.Open())
                using (var cmd = Db.Procedure(cn, "inv.GetPostingBatch"))
                {
                    cmd.Add("@BatchId", SqlDbType.BigInt, id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) continue;
                        var batch = new PostingBatch { BatchId = id, CreatedAt = r.GetDateTime(1), Invoices = new List<PostingInvoice>() };
                        r.NextResult();
                        while (r.Read())
                        {
                            using (var sr = new StringReader(r.GetString(0)))
                            {
                                batch.Invoices.Add((PostingInvoice)Serializer.Deserialize(sr));
                            }
                        }
                        list.Add(batch);
                    }
                }
            }
            return list;
        }

        public void MarkWritten(long batchId, string fileName, decimal grossTotal)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.SetBatchWritten"))
            {
                cmd.Add("@BatchId", SqlDbType.BigInt, batchId);
                cmd.Add("@FileName", SqlDbType.NVarChar, fileName, 260);
                cmd.Add("@GrossTotal", SqlDbType.Decimal, grossTotal);
                cmd.ExecuteNonQuery();
            }
        }

        public void ApplyAcknowledgement(string ackFile, ErpAcknowledgement ack)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.ApplyAcknowledgement"))
            {
                cmd.Add("@AckFile", SqlDbType.NVarChar, ackFile, 260);
                cmd.Add("@InvoiceId", SqlDbType.BigInt, ack.InvoiceId);
                cmd.Add("@Accepted", SqlDbType.Bit, ack.Accepted);
                cmd.Add("@VoucherNumber", SqlDbType.VarChar, ack.VoucherNumber, 30);
                cmd.Add("@Message", SqlDbType.NVarChar, FileRepository.Truncate(ack.Message, 400), 400);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
