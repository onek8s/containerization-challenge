using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Integration.DocumentStore;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Tenancies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Jobs.Infrastructure
{
    /// <summary>
    /// Sends texts, e-mails and printed letters and records each one in the communication log.
    /// Letters are rendered to PDF in the print folder, which the post room prints and posts every afternoon.
    /// </summary>
    public class TenantMessenger
    {
        private readonly HousingDbContext _db;
        private readonly ISmsSender _sms;
        private readonly IEmailSender _email;
        private readonly ILetterComposer _letters;
        private readonly IPdfRenderer _pdf;
        private readonly IDocumentServiceClient _documents;
        private readonly IClock _clock;
        private readonly JobsOptions _options;
        private readonly ILogger<TenantMessenger> _logger;

        public TenantMessenger(HousingDbContext db, ISmsSender sms, IEmailSender email, ILetterComposer letters, IPdfRenderer pdf,
            IDocumentServiceClient documents, IClock clock, IOptions<JobsOptions> options, ILogger<TenantMessenger> logger)
        {
            _db = db;
            _sms = sms;
            _email = email;
            _letters = letters;
            _pdf = pdf;
            _documents = documents;
            _clock = clock;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<CommunicationLog> SmsAsync(int? tenancyId, string mobile, string templateCode, string text)
        {
            text = SmsTemplates.Truncate(text);
            var log = new CommunicationLog { TenancyId = tenancyId, Channel = Channel.Sms, TemplateCode = templateCode, Recipient = mobile, Body = text, CreatedAt = _clock.Now };
            try
            {
                log.ProviderReference = await _sms.SendAsync(mobile, text);
                log.Status = CommunicationStatus.Sent;
                log.SentAt = _clock.Now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS {Template} to tenancy {TenancyId} failed", templateCode, tenancyId);
                log.Status = CommunicationStatus.Failed;
                log.Error = Truncate(ex.Message);
            }
            _db.Communications.Add(log);
            return log;
        }

        public async Task<CommunicationLog> EmailAsync(int? tenancyId, int? contractorId, string to, string templateCode, string subject, string html,
            IEnumerable<EmailAttachment> attachments = null)
        {
            var log = new CommunicationLog { TenancyId = tenancyId, ContractorId = contractorId, Channel = Channel.Email, TemplateCode = templateCode, Recipient = to, Subject = subject, Body = html, CreatedAt = _clock.Now };
            try
            {
                await _email.SendAsync(to, subject, html, attachments);
                log.Status = CommunicationStatus.Sent;
                log.SentAt = _clock.Now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "E-mail {Template} to {To} failed", templateCode, to);
                log.Status = CommunicationStatus.Failed;
                log.Error = Truncate(ex.Message);
            }
            _db.Communications.Add(log);
            return log;
        }

        /// <summary>Internal notification to a staff mailbox; not tied to a tenancy.</summary>
        public Task<CommunicationLog> StaffEmailAsync(string to, string templateCode, string subject, string html) =>
            EmailAsync(null, null, to, templateCode, subject, html);

        public async Task<string> LetterAsync(Tenancy tenancy, string templateCode, IDictionary<string, string> fields, string reference = null)
        {
            var lead = tenancy.Tenants.FirstOrDefault(t => t.IsLeadTenant) ?? tenancy.Tenants.First();
            var property = tenancy.Property;
            var model = new LetterModel
            {
                TemplateCode = templateCode,
                RecipientName = string.Join(" & ", tenancy.Tenants.OrderByDescending(t => t.IsLeadTenant).Select(t => t.FullName)),
                AddressLines = new List<string> { property.AddressLine1, property.AddressLine2, property.Town, property.Postcode },
                Date = _clock.Today,
                Reference = reference ?? tenancy.Reference,
                LargePrint = lead.NeedsLargePrint,
                Fields = new Dictionary<string, string>(fields)
            };
            if (!model.Fields.ContainsKey("Salutation"))
                model.Fields["Salutation"] = (lead.Title + " " + lead.LastName).Trim();
            var html = _letters.Compose(model);
            var pdf = _pdf.RenderHtml(html, templateCode + " " + tenancy.Reference);
            var path = await WritePrintFileAsync(templateCode + "_" + tenancy.Reference + ".pdf", pdf);

            var log = new CommunicationLog { TenancyId = tenancy.Id, Channel = Channel.Letter, TemplateCode = templateCode, Recipient = string.Join(", ", model.AddressLines.Where(l => !string.IsNullOrEmpty(l))), Subject = templateCode, Status = CommunicationStatus.Printed, CreatedAt = _clock.Now, SentAt = _clock.Now };
            try
            {
                using (var stream = new MemoryStream(pdf))
                {
                    var record = await _documents.UploadAsync(new DocumentUpload
                    {
                        Category = DocumentCategory.Letter,
                        Title = templateCode + " " + _clock.Today.ToString("dd/MM/yyyy"),
                        FileName = Path.GetFileName(path),
                        ContentType = "application/pdf",
                        PropertyId = tenancy.PropertyId,
                        TenancyId = tenancy.Id,
                        VisibleToTenant = true,
                        UploadedBy = "housing-jobs"
                    }, stream);
                    if (record != null && record.Id > 0) log.DocumentId = record.Id;
                }
            }
            catch (Exception ex)
            {
                // The printed copy has been produced; the electronic copy can be added from the print folder later.
                _logger.LogWarning(ex, "Could not store letter {Template} for {Tenancy} in the document service", templateCode, tenancy.Reference);
            }
            _db.Communications.Add(log);
            return path;
        }

        public async Task<string> WritePrintFileAsync(string fileName, byte[] content)
        {
            var folder = Path.Combine(_options.PrintFolder, _clock.Today.ToString("yyyyMMdd"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);
            await File.WriteAllBytesAsync(path, content);
            return path;
        }

        private static string Truncate(string s) => s != null && s.Length > 1000 ? s.Substring(0, 1000) : s;
    }
}
