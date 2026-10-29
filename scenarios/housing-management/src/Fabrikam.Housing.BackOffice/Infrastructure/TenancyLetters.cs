using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Tenancies;

namespace Fabrikam.Housing.BackOffice.Infrastructure
{
    /// <summary>
    /// Produces a letter for a tenancy: merges the template, renders the PDF, files it in the document
    /// service and logs it as a printed letter.
    /// </summary>
    public class TenancyLetters
    {
        private readonly HousingDbContext _db;
        private readonly ILetterComposer _composer;
        private readonly IPdfRenderer _pdf;
        private readonly IDocumentServiceClient _documents;

        public TenancyLetters(HousingDbContext db, ILetterComposer composer, IPdfRenderer pdf, IDocumentServiceClient documents)
        {
            _db = db;
            _composer = composer;
            _pdf = pdf;
            _documents = documents;
        }

        public async Task<DocumentRecord> CreateAsync(Tenancy tenancy, string templateCode, string title, DocumentCategory category,
            IDictionary<string, string> fields, string actor)
        {
            var lead = tenancy.Tenants.FirstOrDefault(t => t.IsLeadTenant) ?? tenancy.Tenants.First();
            var property = tenancy.Property;
            var model = new LetterModel
            {
                TemplateCode = templateCode,
                RecipientName = string.Join(" & ", tenancy.Tenants.Select(t => t.FullName)),
                AddressLines = new List<string> { property.AddressLine1, property.AddressLine2, property.Town, property.Postcode }
                    .Where(l => !string.IsNullOrWhiteSpace(l)).ToList(),
                Date = DateTime.Today,
                Reference = tenancy.Reference,
                LargePrint = lead.NeedsLargePrint,
                Fields = fields ?? new Dictionary<string, string>()
            };
            model.Fields["FirstName"] = lead.FirstName;
            model.Fields["Salutation"] = string.Join(" and ", tenancy.Tenants.Select(t => (t.Title + " " + t.LastName).Trim()));
            model.Fields["TenancyReference"] = tenancy.Reference;
            if (!model.Fields.ContainsKey("PropertyAddress")) model.Fields["PropertyAddress"] = property.SingleLineAddress;

            var html = _composer.Compose(model);
            var pdf = _pdf.RenderHtml(html, title);
            DocumentRecord record;
            using (var stream = new MemoryStream(pdf))
            {
                record = await _documents.UploadAsync(new DocumentUpload
                {
                    Category = category,
                    Title = title,
                    FileName = $"{templateCode}_{tenancy.Reference}_{DateTime.Now:yyyyMMddHHmm}.pdf",
                    ContentType = "application/pdf",
                    PropertyId = tenancy.PropertyId,
                    TenancyId = tenancy.Id,
                    VisibleToTenant = true,
                    UploadedBy = actor
                }, stream);
            }

            _db.Communications.Add(new CommunicationLog
            {
                TenancyId = tenancy.Id,
                Channel = Channel.Letter,
                TemplateCode = templateCode,
                Recipient = model.RecipientName,
                Subject = title,
                Status = CommunicationStatus.Printed,
                CreatedAt = DateTime.Now,
                SentAt = DateTime.Now,
                DocumentId = record?.Id > 0 ? record.Id : (int?)null
            });
            await _db.SaveChangesAsync();
            return record;
        }
    }
}
