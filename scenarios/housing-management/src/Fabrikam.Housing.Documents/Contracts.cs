using System;
using System.Collections.Generic;
using System.IO;
using Fabrikam.Housing.Data.Queries;

namespace Fabrikam.Housing.Documents
{
    public interface IPdfRenderer
    {
        byte[] RenderHtml(string html, string title);
    }

    public class LetterModel
    {
        public string TemplateCode { get; set; }
        public string RecipientName { get; set; }
        public IList<string> AddressLines { get; set; } = new List<string>();
        public DateTime Date { get; set; }
        public string Reference { get; set; }
        public bool LargePrint { get; set; }
        public IDictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
    }

    public interface ILetterComposer
    {
        /// <summary>Merges the template with the model and returns HTML.</summary>
        string Compose(LetterModel model);
        IEnumerable<string> TemplateCodes { get; }
    }

    public class StatementHeader
    {
        public string TenancyReference { get; set; }
        public string TenantName { get; set; }
        public string Address { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal WeeklyCharge { get; set; }
    }

    public interface IStatementPdfWriter
    {
        byte[] Write(StatementHeader header, IList<StatementLine> lines);
    }

    public interface IReportWorkbookWriter
    {
        byte[] Arrears(IList<ArrearsRow> rows, DateTime asAt);
        byte[] RepairsPerformance(IList<RepairsPerformanceRow> rows, DateTime from, DateTime to);
        byte[] Compliance(IList<ComplianceRow> rows, DateTime asAt);
        /// <summary>Rent roll in the legacy .xls layout used by the finance team's month end workbook.</summary>
        byte[] RentRollXls(IList<RentRollRow> rows, DateTime asAt);
    }

    public class HousingBenefitLine
    {
        public int RowNumber { get; set; }
        public string ClaimReference { get; set; }
        public string ClaimantName { get; set; }
        public string AddressLine1 { get; set; }
        public string Postcode { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public decimal Amount { get; set; }
    }

    public interface IHousingBenefitScheduleReader
    {
        /// <summary>Reads the council's weekly landlord payment schedule (.xls or .xlsx).</summary>
        IList<HousingBenefitLine> Read(Stream content, string fileName);
    }

    public class IndexedDocument
    {
        public Guid DocumentKey { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Text { get; set; }
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class IndexHit
    {
        public Guid DocumentKey { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Snippet { get; set; }
        public float Score { get; set; }
        public int? PropertyId { get; set; }
        public int? TenancyId { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public interface IDocumentIndex : IDisposable
    {
        void Add(IndexedDocument document);
        void Delete(Guid documentKey);
        IList<IndexHit> Search(string query, int? tenancyId, int? propertyId, int max);
        void Commit();
    }

    public interface ITextExtractor
    {
        /// <summary>Plain text for indexing, or empty when the type is not supported.</summary>
        string Extract(byte[] content, string contentType, string fileName);
    }

    public interface IThumbnailer
    {
        /// <summary>JPEG thumbnail (max 240 px), or null when the content is not an image.</summary>
        byte[] Create(byte[] content, string contentType);
    }
}
