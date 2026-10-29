using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Reporting.WinForms;
using Woodgrove.Statements.Core.Reports;

namespace Woodgrove.Statements.Reporting
{
    /// <summary>One report to render: definition, datasets, parameters and subreport data.</summary>
    public class ReportJob
    {
        public string Definition { get; set; }
        public Dictionary<string, IEnumerable> DataSources { get; } = new Dictionary<string, IEnumerable>();
        public Dictionary<string, string> Parameters { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> Subreports { get; } = new Dictionary<string, string>();
        /// <summary>Datasets of subreports by subreport name and dataset name.</summary>
        public Func<string, string, IEnumerable> SubreportData { get; set; }
    }

    /// <summary>
    /// Renders report definitions with the ReportViewer local processing engine. LocalReport is not thread safe, so every
    /// render uses its own instance; a semaphore limits parallel renders because each one holds the whole document in memory.
    /// </summary>
    public class ReportRenderer
    {
        static readonly System.Threading.SemaphoreSlim Gate = new System.Threading.SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public IList<string> LastWarnings { get; private set; } = new List<string>();

        public byte[] Render(ReportJob job, OutputFormat format, out int pageCount)
        {
            if (!Gate.Wait(Timeout)) throw new TimeoutException("Report rendering is busy, try again later.");
            try
            {
                var report = new LocalReport();
                try
                {
                    report.EnableExternalImages = false;
                    report.EnableHyperlinks = false;
                    report.DisplayName = ReportDefinitions.ReportName(job.Definition);
                    using (var stream = ReportDefinitions.Open(job.Definition))
                        report.LoadReportDefinition(stream);
                    foreach (var sub in job.Subreports)
                        using (var stream = ReportDefinitions.Open(sub.Value))
                            report.LoadSubreportDefinition(sub.Key, stream);
                    if (job.SubreportData != null)
                    {
                        report.SubreportProcessing += (sender, e) =>
                        {
                            foreach (var name in e.DataSourceNames)
                                e.DataSources.Add(new ReportDataSource(name, job.SubreportData(e.ReportPath, name) ?? new object[0]));
                        };
                    }
                    foreach (var ds in job.DataSources)
                        report.DataSources.Add(new ReportDataSource(ds.Key, ds.Value));
                    if (job.Parameters.Count > 0)
                        report.SetParameters(job.Parameters.Select(p => new ReportParameter(p.Key, p.Value)).ToArray());

                    string mimeType, encoding, extension;
                    string[] streams;
                    Warning[] warnings;
                    var watch = Stopwatch.StartNew();
                    var bytes = report.Render(OutputFormats.RenderFormat(format), DeviceInfo(format), PageCountMode.Actual,
                                              out mimeType, out encoding, out extension, out streams, out warnings);
                    pageCount = report.GetTotalPages();
                    LastWarnings = warnings.Select(w => w.Code + ": " + w.Message).ToList();
                    Trace.TraceInformation("Rendered {0} as {1}: {2} bytes, {3} pages, {4} ms", job.Definition, format, bytes.Length, pageCount, watch.ElapsedMilliseconds);
                    return bytes;
                }
                finally
                {
                    // The expression host lives in a sandbox AppDomain per report; release it or the process keeps growing.
                    report.ReleaseSandboxAppDomain();
                    report.Dispose();
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        static string DeviceInfo(OutputFormat format)
        {
            switch (format)
            {
                case OutputFormat.Pdf:
                    return "<DeviceInfo><EmbedFonts>Subset</EmbedFonts><HumanReadablePDF>False</HumanReadablePDF></DeviceInfo>";
                case OutputFormat.Excel:
                    return "<DeviceInfo><SimplePageHeaders>True</SimplePageHeaders></DeviceInfo>";
                default:
                    return null;
            }
        }
    }
}
