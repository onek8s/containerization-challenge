using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Litware.DocControl.Register
{
    /// <summary>
    /// COM client for the Document Control API. Created in VBA with
    /// <c>CreateObject("Litware.DocControl.RegisterClient")</c>. Methods that return tables give a
    /// two-dimensional array (header row first) that can be assigned directly to a worksheet range.
    /// On failure they return Empty and set <see cref="LastError"/>.
    /// </summary>
    [ComVisible(true)]
    [Guid("e8a45c1f-7b2d-4a96-b3e0-5d9c71f2a6b8")]
    [ProgId("Litware.DocControl.RegisterClient")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(IRegisterClient))]
    public sealed class RegisterClient : IRegisterClient
    {
        private const string SettingsKey = @"Software\Litware\DocControl\Register";
        private const string PolicyKey = @"SOFTWARE\Policies\Litware\DocControl\Register";
        private const string DefaultUrl = "https://doccontrol.corp.local";

        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

        public RegisterClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var user = Registry.CurrentUser.OpenSubKey(SettingsKey))
            using (var policy = Registry.LocalMachine.OpenSubKey(PolicyKey))
            {
                BaseUrl = user?.GetValue("BaseUrl") as string ?? policy?.GetValue("BaseUrl") as string ?? DefaultUrl;
                TimeoutSeconds = user?.GetValue("TimeoutSeconds") is int t ? t : 60;
            }
            LastError = "";
        }

        public string BaseUrl { get; set; }
        public string LastError { get; private set; }
        public int TimeoutSeconds { get; set; }

        public object GetProjects() =>
            Table(() => Get("api/projects"), new[] { "code", "name", "client" }, new[] { "Project", "Name", "Client" });

        public object GetRegister(string project) =>
            Table(() => Get("api/register/" + Uri.EscapeDataString(project)),
                new[] { "number", "title", "discipline", "type", "latestRevision", "latestStatus", "approvedRevision" },
                new[] { "Document number", "Title", "Discipline", "Type", "Latest", "Status", "Approved" });

        public object GetRevisions(string documentNumber) =>
            Table(() => Get("api/documents/" + Uri.EscapeDataString(documentNumber)),
                new[] { "revision", "status", "fileName", "size", "submittedBy", "submittedUtc", "reviewedBy", "comment" },
                new[] { "Rev", "Status", "File", "Bytes", "Submitted by", "Submitted", "Reviewed by", "Comment" });

        public object GetTransmittals(string project) =>
            Table(() => Get("api/transmittals/" + Uri.EscapeDataString(project)),
                new[] { "number", "purpose", "recipient", "status", "items", "createdUtc", "issuedUtc" },
                new[] { "Transmittal", "Purpose", "Recipient", "Status", "Documents", "Created", "Issued" });

        public string CreateTransmittal(string project, string purpose, string recipientCompany, string recipientName,
            string recipientEmail, string notes, string documentNumbers)
        {
            LastError = "";
            try
            {
                var documents = (documentNumbers ?? "")
                    .Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim())
                    .Where(d => d.Length > 0)
                    .ToList();

                var body = _json.Serialize(new Dictionary<string, object>
                {
                    ["project"] = project,
                    ["purpose"] = purpose,
                    ["recipientCompany"] = recipientCompany,
                    ["recipientName"] = recipientName,
                    ["recipientEmail"] = recipientEmail,
                    ["notes"] = notes,
                    ["documents"] = documents,
                });

                var result = _json.Deserialize<Dictionary<string, object>>(Send("POST", "api/transmittals", body));
                return result.TryGetValue("number", out var number) ? Convert.ToString(number) : "";
            }
            catch (Exception ex)
            {
                LastError = Describe(ex);
                return "";
            }
        }

        public void OpenInPortal(string documentNumber)
        {
            var url = BaseUrl.TrimEnd('/') + "/register/" + Uri.EscapeDataString(documentNumber);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        public string Version() => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private object Table(Func<string> fetch, string[] fields, string[] headers)
        {
            LastError = "";
            try
            {
                var rows = _json.Deserialize<ArrayList>(fetch());
                var table = new object[rows.Count + 1, fields.Length];
                for (var c = 0; c < headers.Length; c++) table[0, c] = headers[c];
                for (var r = 0; r < rows.Count; r++)
                {
                    var row = (Dictionary<string, object>)rows[r];
                    for (var c = 0; c < fields.Length; c++)
                        table[r + 1, c] = row.TryGetValue(fields[c], out var v) ? ToCell(v) : null;
                }
                return table;
            }
            catch (Exception ex)
            {
                LastError = Describe(ex);
                return null;
            }
        }

        private static object ToCell(object value)
        {
            if (value is string s && s.Length >= 19 && s[4] == '-' && s[10] == 'T'
                && DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime();
            return value;
        }

        private string Get(string path) => Send("GET", path, null);

        private string Send(string method, string path, string body)
        {
            var request = (HttpWebRequest)WebRequest.Create(BaseUrl.TrimEnd('/') + "/" + path);
            request.Method = method;
            request.Accept = "application/json";
            request.UseDefaultCredentials = true;
            request.PreAuthenticate = true;
            request.Timeout = TimeoutSeconds * 1000;
            request.UserAgent = "Litware.DocControl.Register/" + Version();

            if (body != null)
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = bytes.Length;
                using (var s = request.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse error)
            {
                string text;
                using (var reader = new StreamReader(error.GetResponseStream(), Encoding.UTF8)) text = reader.ReadToEnd();
                throw new InvalidOperationException(ParseErrors(text) ?? $"{(int)error.StatusCode} {error.StatusDescription}");
            }
        }

        private string ParseErrors(string text)
        {
            try
            {
                var obj = _json.Deserialize<Dictionary<string, object>>(text);
                if (obj != null && obj.TryGetValue("errors", out var errors) && errors is ArrayList list)
                    return string.Join(Environment.NewLine, list.Cast<object>());
            }
            catch (ArgumentException)
            {
            }
            return null;
        }

        private static string Describe(Exception ex) => ex is WebException web ? $"{web.Status}: {web.Message}" : ex.Message;
    }
}
