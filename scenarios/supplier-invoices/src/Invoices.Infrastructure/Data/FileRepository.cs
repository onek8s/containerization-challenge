using System;
using System.Collections.Generic;
using System.Data;

namespace Wingtip.Invoices.Infrastructure.Data
{
    public static class FileStatus
    {
        public const string Queued = "Queued";
        public const string Processed = "Processed";
        public const string Failed = "Failed";
        public const string Duplicate = "Duplicate";
        public const string Poison = "Poison";
    }

    public class InboundFileRecord
    {
        public long FileId { get; set; }
        public int ChannelId { get; set; }
        public int SupplierId { get; set; }
        public string FileName { get; set; }
        public string ArchivePath { get; set; }
        public string Status { get; set; }
        public int Attempts { get; set; }
    }

    public class FileRepository
    {
        public IList<InboundChannel> GetActiveChannels()
        {
            var list = new List<InboundChannel>();
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.GetActiveChannels"))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new InboundChannel
                    {
                        ChannelId = r.GetInt32(r.GetOrdinal("ChannelId")),
                        SupplierId = r.GetInt32(r.GetOrdinal("SupplierId")),
                        SupplierCode = r.String("SupplierCode"),
                        ChannelType = r.String("ChannelType"),
                        Location = r.String("Location"),
                        FilePattern = r.String("FilePattern") ?? "*.*",
                        CodePage = r.GetInt32(r.GetOrdinal("CodePage"))
                    });
                }
            }
            return list;
        }

        /// <summary>Registers a received file. Returns null when the same content was already received from the supplier.</summary>
        public long? Register(int channelId, string fileName, byte[] sha256, long size)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.RegisterInboundFile"))
            {
                cmd.Add("@ChannelId", SqlDbType.Int, channelId);
                cmd.Add("@FileName", SqlDbType.NVarChar, fileName, 260);
                cmd.Add("@Sha256", SqlDbType.Binary, sha256, 32);
                cmd.Add("@SizeBytes", SqlDbType.BigInt, size);
                var id = cmd.AddOutput("@FileId", SqlDbType.BigInt);
                cmd.ExecuteNonQuery();
                return id.Value == DBNull.Value ? (long?)null : (long)id.Value;
            }
        }

        public void SetArchived(long fileId, string archivePath)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.SetFileArchived"))
            {
                cmd.Add("@FileId", SqlDbType.BigInt, fileId);
                cmd.Add("@ArchivePath", SqlDbType.NVarChar, archivePath, 400);
                cmd.ExecuteNonQuery();
            }
        }

        public void SetStatus(long fileId, string status, string error)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.SetFileStatus"))
            {
                cmd.Add("@FileId", SqlDbType.BigInt, fileId);
                cmd.Add("@Status", SqlDbType.VarChar, status, 20);
                cmd.Add("@Error", SqlDbType.NVarChar, Truncate(error, 2000), 2000);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Counts a failed processing attempt and returns the number of attempts so far.</summary>
        public int AddAttempt(long fileId, string error)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.AddFileAttempt"))
            {
                cmd.Add("@FileId", SqlDbType.BigInt, fileId);
                cmd.Add("@Error", SqlDbType.NVarChar, Truncate(error, 2000), 2000);
                var attempts = cmd.AddOutput("@Attempts", SqlDbType.Int);
                cmd.ExecuteNonQuery();
                return (int)attempts.Value;
            }
        }

        public InboundFileRecord Get(long fileId)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.GetInboundFile"))
            {
                cmd.Add("@FileId", SqlDbType.BigInt, fileId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new InboundFileRecord
                    {
                        FileId = fileId,
                        ChannelId = r.GetInt32(r.GetOrdinal("ChannelId")),
                        SupplierId = r.GetInt32(r.GetOrdinal("SupplierId")),
                        FileName = r.String("FileName"),
                        ArchivePath = r.String("ArchivePath"),
                        Status = r.String("Status"),
                        Attempts = r.GetInt32(r.GetOrdinal("Attempts"))
                    };
                }
            }
        }

        public void TouchChannel(int channelId, string error)
        {
            using (var cn = Db.Open())
            using (var cmd = Db.Procedure(cn, "inv.SetChannelPolled"))
            {
                cmd.Add("@ChannelId", SqlDbType.Int, channelId);
                cmd.Add("@Error", SqlDbType.NVarChar, Truncate(error, 1000), 1000);
                cmd.ExecuteNonQuery();
            }
        }

        internal static string Truncate(string value, int length)
        {
            if (value == null) return null;
            return value.Length <= length ? value : value.Substring(0, length);
        }
    }
}
