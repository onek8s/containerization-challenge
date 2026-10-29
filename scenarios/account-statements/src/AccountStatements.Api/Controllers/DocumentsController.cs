using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Woodgrove.Statements.Api.Models;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Scheduling;
using Woodgrove.Statements.Data;
using Woodgrove.Statements.Reporting;

namespace Woodgrove.Statements.Api.Controllers
{
    [RoutePrefix("api")]
    public class DocumentsController : ApiController
    {
        static readonly Db Database = Db.FromConfig();
        readonly DocumentService _documents = new DocumentService(Database);
        readonly DocumentRepository _log = new DocumentRepository(Database);

        ApiClient Client
        {
            get { return (ApiClient)Request.Properties[ApiKeyHandler.ClientProperty]; }
        }

        [HttpGet, Route("reports")]
        public IEnumerable<ReportInfo> Reports()
        {
            return ReportDefinitionInfo.All
                .Where(r => r.Code != ReportCodes.StatementRun && Client.Allows(r.Code))
                .Select(r => new ReportInfo { Code = r.Code, Title = r.Title, Category = r.Category, Parameters = r.Parameters,
                                              Formats = r.Formats.Select(f => OutputFormats.Extension(f).TrimStart('.')).ToList() });
        }

        /// <summary>Customer statement. Defaults: statement date today, period from the first day of that month.</summary>
        [HttpPost, Route("documents/statement")]
        public HttpResponseMessage Statement(StatementRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CustomerNo))
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "customerNo is required.");
            var statementDate = (request.StatementDate ?? DateTime.Today).Date;
            var periodStart = (request.PeriodStart ?? new DateTime(statementDate.Year, statementDate.Month, 1)).Date;
            var parameters = new ParameterSet();
            parameters["CustomerNo"] = request.CustomerNo.Trim();
            parameters["PeriodStart"] = periodStart.ToString(ParameterSet.DateFormat);
            parameters["StatementDate"] = statementDate.ToString(ParameterSet.DateFormat);
            return Produce(ReportCodes.CustomerStatement, parameters, request.Format);
        }

        [HttpGet, Route("documents/invoice/{invoiceNo}")]
        public HttpResponseMessage Invoice(string invoiceNo)
        {
            var parameters = new ParameterSet();
            parameters["InvoiceNo"] = invoiceNo;
            return Produce(ReportCodes.InvoiceCopy, parameters, "pdf");
        }

        [HttpPost, Route("documents/{reportCode}")]
        public HttpResponseMessage Report(string reportCode, ReportRequest request)
        {
            var parameters = new ParameterSet();
            if (request?.Parameters != null)
                foreach (var p in request.Parameters) parameters[p.Key] = p.Value;
            return Produce(reportCode.ToUpperInvariant(), parameters, request?.Format);
        }

        [HttpGet, Route("customers/{customerNo}/balance")]
        public IHttpActionResult Balance(string customerNo)
        {
            if (!Client.Allows(ReportCodes.CustomerStatement)) return StatusCode(HttpStatusCode.Forbidden);
            var customer = new CustomerRepository(Database).Get(customerNo);
            if (customer == null) return NotFound();
            var asOf = DateTime.Today;
            var st = _documents.BuildStatement(customer, asOf, asOf);
            return Ok(new CustomerBalanceInfo
            {
                CustomerNo = customer.CustomerNo, Name = customer.Name, OpenBalance = st.Aging.Total, Overdue = st.Aging.Overdue,
                NotDue = st.Aging.NotDue, Days1To30 = st.Aging.Days1To30, Days31To60 = st.Aging.Days31To60, Days61To90 = st.Aging.Days61To90,
                Over90 = st.Aging.Over90, Unallocated = st.Aging.Unallocated, DunningLevel = st.DunningLevel, OnHold = customer.OnHold,
                LedgerDate = new LedgerRepository(Database).LastLoad()?.LedgerDate ?? asOf
            });
        }

        HttpResponseMessage Produce(string reportCode, ParameterSet parameters, string format)
        {
            var info = ReportDefinitionInfo.Find(reportCode);
            if (info == null || reportCode == ReportCodes.StatementRun)
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Unknown report " + reportCode + ".");
            if (!Client.Allows(info.Code))
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "This API key may not request " + info.Code + ".");

            OutputFormat outputFormat;
            try
            {
                outputFormat = OutputFormats.Parse(string.IsNullOrWhiteSpace(format) ? "pdf" : format);
            }
            catch (FormatException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex.Message);
            }

            var log = new DocumentRequest
            {
                Channel = "API", RequestedBy = Client.Name, ReportCode = info.Code, Parameters = parameters.ToString(),
                OutputFormat = outputFormat.ToString().ToUpperInvariant()
            };
            var watch = Stopwatch.StartNew();
            try
            {
                var doc = _documents.Render(info.Code, parameters, outputFormat, DateTime.Now);
                log.Succeeded = true;
                log.Bytes = doc.Content.Length;
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(doc.Content) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(doc.MimeType);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = doc.FileName };
                if (doc.PageCount > 0) response.Headers.Add("X-Page-Count", doc.PageCount.ToString());
                return response;
            }
            catch (KeyNotFoundException ex)
            {
                log.Error = ex.Message;
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
            {
                log.Error = ex.Message;
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (TimeoutException ex)
            {
                log.Error = ex.Message;
                var busy = Request.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, ex.Message);
                busy.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
                return busy;
            }
            catch (Exception ex)
            {
                log.Error = ex.GetBaseException().Message;
                Trace.TraceError("Rendering {0} ({1}) failed: {2}", info.Code, parameters, ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "The document could not be produced.");
            }
            finally
            {
                log.DurationMs = (int)watch.ElapsedMilliseconds;
                try { _log.LogRequest(log); } catch (Exception ex) { Trace.TraceWarning("Request log failed: {0}", ex.Message); }
            }
        }
    }
}
