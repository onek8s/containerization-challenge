using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace Wingtip.Invoices.Gateway.Channels
{
    /// <summary>
    /// Supplier folder on the partner FTP server (IIS FTP, ftp.corp.local). Suppliers upload to
    /// /suppliers/&lt;code&gt;/out; the gateway moves processed files to /suppliers/&lt;code&gt;/processed.
    /// </summary>
    public class FtpChannel : IInboundChannel
    {
        private readonly Uri folder;
        private readonly Regex pattern;
        private readonly NetworkCredential credential;
        private readonly bool useTls;

        public FtpChannel(string host, string location, string filePattern, NetworkCredential credential, bool useTls)
        {
            folder = new Uri("ftp://" + host + "/" + location.Trim('/') + "/");
            pattern = Wildcard(filePattern);
            this.credential = credential;
            this.useTls = useTls;
        }

        public string Description
        {
            get { return folder.ToString(); }
        }

        public IList<InboundItem> List()
        {
            var items = new List<InboundItem>();
            var request = Create(folder, WebRequestMethods.Ftp.ListDirectoryDetails);
            using (var response = (FtpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var item = ParseListing(line);
                    if (item == null || !pattern.IsMatch(item.Name)) continue;
                    item.Path = new Uri(folder, Uri.EscapeDataString(item.Name)).ToString();
                    items.Add(item);
                }
            }
            return items;
        }

        public byte[] Read(InboundItem item)
        {
            var request = Create(new Uri(item.Path), WebRequestMethods.Ftp.DownloadFile);
            using (var response = (FtpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public void Complete(InboundItem item, bool accepted)
        {
            var target = "../" + (accepted ? "processed" : "rejected") + "/" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" + item.Name;
            var request = Create(new Uri(item.Path), WebRequestMethods.Ftp.Rename);
            request.RenameTo = target;
            using (request.GetResponse())
            {
            }
        }

        private FtpWebRequest Create(Uri uri, string method)
        {
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = method;
            request.Credentials = credential;
            request.EnableSsl = useTls;
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 120000;
            return request;
        }

        // IIS FTP uses the MS-DOS listing style by default:
        // 10-21-26  08:15AM                  727 NF_INVOIC_9100231.edi
        // and the UNIX style when the site is configured for it:
        // -rw-r--r-- 1 owner group 727 Oct 21 08:15 NF_INVOIC_9100231.edi
        private static readonly Regex DosListing = new Regex(@"^(\d{2}-\d{2}-\d{2,4})\s+(\d{1,2}:\d{2}[AP]M)\s+(<DIR>|\d+)\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex UnixListing = new Regex(@"^([\-dl])[rwxsStT\-]{9}\s+\d+\s+\S+\s+\S+\s+(\d+)\s+(\w{3}\s+\d{1,2}\s+[\d:]{4,5})\s+(.+)$", RegexOptions.Compiled);

        internal static InboundItem ParseListing(string line)
        {
            var m = DosListing.Match(line);
            if (m.Success)
            {
                if (m.Groups[3].Value == "<DIR>") return null;
                DateTime modified;
                DateTime.TryParseExact(m.Groups[1].Value + " " + m.Groups[2].Value, new[] { "MM-dd-yy hh:mmtt", "MM-dd-yyyy hh:mmtt" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out modified);
                return new InboundItem
                {
                    Name = m.Groups[4].Value,
                    Size = long.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                    Modified = modified == default(DateTime) ? (DateTime?)null : modified
                };
            }

            m = UnixListing.Match(line);
            if (m.Success)
            {
                if (m.Groups[1].Value != "-") return null;
                return new InboundItem
                {
                    Name = m.Groups[4].Value,
                    Size = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)
                };
            }
            return null;
        }

        internal static Regex Wildcard(string pattern)
        {
            var parts = (pattern ?? "*.*").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var alternatives = new List<string>();
            foreach (var p in parts)
            {
                alternatives.Add("^" + Regex.Escape(p.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$");
            }
            return new Regex(string.Join("|", alternatives), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}
