using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.Reporting.WebForms;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.ReportsDataSetTableAdapters;
using Southridge.Clinic.Infrastructure;
using RS = Southridge.Clinic.Documents.ReportServer;

namespace Southridge.Clinic.Documents
{
    public enum ReportFormat
    {
        Pdf,
        Excel,
        Image
    }

    public class RenderedReport
    {
        public byte[] Content { get; set; }
        public string MimeType { get; set; }
        public string Extension { get; set; }
        public string RenderedBy { get; set; }
    }

    /// <summary>One report request: report name (as in /Southridge/Clinic on the report server) and parameter values.</summary>
    public class ReportRequest
    {
        public ReportRequest(string name)
        {
            Name = name;
            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Format = ReportFormat.Pdf;
        }

        public string Name { get; private set; }
        public IDictionary<string, object> Parameters { get; private set; }
        public ReportFormat Format { get; set; }

        public ReportRequest With(string name, object value)
        {
            Parameters[name] = value;
            return this;
        }

        internal static string ToInvariant(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is DateTime)
            {
                return ((DateTime)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public interface IReportRenderer
    {
        RenderedReport Render(ReportRequest request);
    }

    public static class ReportNames
    {
        public const string DailySchedule = "DailySchedule";
        public const string ArAging = "ArAging";
        public const string ClaimSummary = "ClaimSummary";
        public const string ProviderProductivity = "ProviderProductivity";

        public static readonly string[] All = { DailySchedule, ArAging, ClaimSummary, ProviderProductivity };
    }

    /// <summary>Renders on SQL Server Reporting Services through the ReportExecution2005 SOAP endpoint.</summary>
    public class ServerReportRenderer : IReportRenderer
    {
        private readonly string _url;
        private readonly string _folder;
        private readonly ICredentials _credentials;
        private readonly int _timeout;

        public ServerReportRenderer()
            : this(ClinicSettings.Required("ReportServerUrl"), ClinicSettings.Get("ReportFolder", "/Southridge/Clinic"),
                   CreateCredentials(), ClinicSettings.GetInt("ReportTimeoutSeconds", 120))
        {
        }

        public ServerReportRenderer(string url, string folder, ICredentials credentials, int timeoutSeconds)
        {
            _url = url;
            _folder = folder.TrimEnd('/');
            _credentials = credentials;
            _timeout = timeoutSeconds;
        }

        public RenderedReport Render(ReportRequest request)
        {
            using (var rs = new RS.ReportExecutionService())
            {
                rs.Url = _url.TrimEnd('/') + "/ReportExecution2005.asmx";
                rs.Credentials = _credentials;
                rs.PreAuthenticate = true;
                rs.Timeout = _timeout * 1000;

                RS.ExecutionInfo info = rs.LoadReport(_folder + "/" + request.Name, null);
                rs.ExecutionHeaderValue = new RS.ExecutionHeader { ExecutionID = info.ExecutionID };

                var values = request.Parameters
                    .Select(p => new RS.ParameterValue { Name = p.Key, Value = ReportRequest.ToInvariant(p.Value) })
                    .ToArray();
                rs.SetExecutionParameters(values, "en-us");

                string extension, mimeType, encoding;
                RS.Warning[] warnings;
                string[] streams;
                byte[] content = rs.Render(ServerFormat(request.Format), null, out extension, out mimeType, out encoding, out warnings, out streams);
                if (warnings != null)
                {
                    foreach (RS.Warning w in warnings)
                    {
                        ClinicLog.Warning(LogCategory.General, "Report " + request.Name + ": " + w.Code + " " + w.Message);
                    }
                }

                return new RenderedReport { Content = content, MimeType = mimeType, Extension = extension, RenderedBy = "ReportServer" };
            }
        }

        private static string ServerFormat(ReportFormat format)
        {
            switch (format)
            {
                case ReportFormat.Excel: return "EXCEL";
                case ReportFormat.Image: return "IMAGE";
                default: return "PDF";
            }
        }

        private static ICredentials CreateCredentials()
        {
            // The report server is set up for Basic authentication with a local reporting account.
            string user = ClinicSettings.Get("ReportServerUser");
            if (string.IsNullOrEmpty(user))
            {
                return null;
            }

            var cache = new CredentialCache();
            cache.Add(new Uri(ClinicSettings.Required("ReportServerUrl")), "Basic",
                      new NetworkCredential(user, ClinicSettings.Get("ReportServerPassword", string.Empty)));
            return cache;
        }
    }

    /// <summary>Renders the embedded RDLC copy of a report with ReportViewer LocalReport, filling the typed DataSet first.</summary>
    public class LocalReportRenderer : IReportRenderer
    {
        public RenderedReport Render(ReportRequest request)
        {
            DataTable data = Fill(request);
            var report = new LocalReport();
            using (Stream definition = OpenDefinition(request.Name))
            {
                report.LoadReportDefinition(definition);
            }

            report.DataSources.Add(new ReportDataSource(request.Name, data));
            report.SetParameters(request.Parameters
                .Select(p => new ReportParameter(p.Key, ReportRequest.ToInvariant(p.Value)))
                .ToList());

            Warning[] warnings;
            string[] streams;
            string mimeType, encoding, extension;
            byte[] content = report.Render(LocalFormat(request.Format), null, out mimeType, out encoding, out extension, out streams, out warnings);
            return new RenderedReport { Content = content, MimeType = mimeType, Extension = extension, RenderedBy = "LocalReport" };
        }

        public static Stream OpenDefinition(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Southridge.Clinic.Documents.Reports." + name + ".rdlc");
            if (stream == null)
            {
                throw new ArgumentException("Unknown report " + name + ".", "name");
            }

            return stream;
        }

        /// <summary>Fills the typed DataSet table that backs a report. Also used by the Excel exports.</summary>
        public static DataTable Fill(ReportRequest request)
        {
            IDictionary<string, object> p = request.Parameters;
            switch (request.Name)
            {
                case ReportNames.DailySchedule:
                    return new DailyScheduleTableAdapter().GetData(Int(p, "SiteId").Value, Date(p, "Date").Value);
                case ReportNames.ArAging:
                    return new ArAgingTableAdapter().GetData(Date(p, "AsOf").Value);
                case ReportNames.ClaimSummary:
                    return new ClaimSummaryTableAdapter().GetData(Date(p, "From").Value, Date(p, "To").Value);
                case ReportNames.ProviderProductivity:
                    return new ProviderProductivityTableAdapter().GetData(Date(p, "From").Value, Date(p, "To").Value, Int(p, "SiteId"));
                default:
                    throw new ArgumentException("Unknown report " + request.Name + ".");
            }
        }

        private static string LocalFormat(ReportFormat format)
        {
            switch (format)
            {
                case ReportFormat.Excel: return "Excel";
                case ReportFormat.Image: return "Image";
                default: return "PDF";
            }
        }

        private static int? Int(IDictionary<string, object> p, string name)
        {
            object value;
            if (!p.TryGetValue(name, out value) || value == null || (value as string) == string.Empty)
            {
                return null;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static DateTime? Date(IDictionary<string, object> p, string name)
        {
            object value;
            if (!p.TryGetValue(name, out value) || value == null)
            {
                return null;
            }

            return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Picks the renderer from Clinic:ReportMode (Server, Local or Auto). Auto tries the report server and falls back to
    /// the RDLC copy when the server is down, which is how the clinics kept printing schedules during the 2012 outage.
    /// </summary>
    public class ReportRendererFactory
    {
        public IReportRenderer Create()
        {
            string mode = ClinicSettings.Get("ReportMode", "Auto");
            if (string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ClinicSettings.Get("ReportServerUrl")))
            {
                return new LocalReportRenderer();
            }

            if (string.Equals(mode, "Server", StringComparison.OrdinalIgnoreCase))
            {
                return new ServerReportRenderer();
            }

            return new FallbackRenderer(new ServerReportRenderer(), new LocalReportRenderer());
        }

        private class FallbackRenderer : IReportRenderer
        {
            private readonly IReportRenderer _primary;
            private readonly IReportRenderer _fallback;

            public FallbackRenderer(IReportRenderer primary, IReportRenderer fallback)
            {
                _primary = primary;
                _fallback = fallback;
            }

            public RenderedReport Render(ReportRequest request)
            {
                try
                {
                    return _primary.Render(request);
                }
                catch (WebException ex)
                {
                    ClinicLog.Warning(LogCategory.General, "Report server unavailable, rendering " + request.Name + " locally: " + ex.Message);
                    return _fallback.Render(request);
                }
                catch (System.Web.Services.Protocols.SoapException ex)
                {
                    ClinicLog.Warning(LogCategory.General, "Report server error, rendering " + request.Name + " locally: " + ex.Message);
                    return _fallback.Render(request);
                }
            }
        }
    }
}
