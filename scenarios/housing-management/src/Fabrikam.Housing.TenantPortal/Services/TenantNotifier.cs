using System;
using System.Threading.Tasks;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Tenancies;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.TenantPortal.Services
{
    /// <summary>Sends confirmations to tenants and records them in the communications log.</summary>
    public class TenantNotifier
    {
        private readonly ISmsSender _sms;
        private readonly IEmailSender _email;
        private readonly HousingDbContext _db;
        private readonly ILogger<TenantNotifier> _logger;

        public TenantNotifier(ISmsSender sms, IEmailSender email, HousingDbContext db, ILogger<TenantNotifier> logger)
        {
            _sms = sms;
            _email = email;
            _db = db;
            _logger = logger;
        }

        public async Task SmsAsync(Tenant tenant, string templateCode, string text)
        {
            if (string.IsNullOrWhiteSpace(tenant.MobilePhone)) return;
            var log = new CommunicationLog
            {
                TenancyId = tenant.TenancyId, Channel = Channel.Sms, TemplateCode = templateCode, Recipient = tenant.MobilePhone,
                Body = SmsTemplates.Truncate(text), CreatedAt = DateTime.Now, Status = CommunicationStatus.Queued
            };
            try
            {
                log.ProviderReference = await _sms.SendAsync(tenant.MobilePhone, log.Body);
                log.Status = CommunicationStatus.Sent;
                log.SentAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS {Template} to tenancy {Tenancy} failed", templateCode, tenant.TenancyId);
                log.Status = CommunicationStatus.Failed;
                log.Error = ex.Message;
            }
            _db.Communications.Add(log);
            await _db.SaveChangesAsync();
        }

        public async Task EmailAsync(Tenant tenant, string templateCode, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(tenant.Email)) return;
            var log = new CommunicationLog
            {
                TenancyId = tenant.TenancyId, Channel = Channel.Email, TemplateCode = templateCode, Recipient = tenant.Email,
                Subject = subject, Body = html, CreatedAt = DateTime.Now, Status = CommunicationStatus.Queued
            };
            try
            {
                await _email.SendAsync(tenant.Email, subject, html);
                log.Status = CommunicationStatus.Sent;
                log.SentAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-mail {Template} to tenancy {Tenancy} failed", templateCode, tenant.TenancyId);
                log.Status = CommunicationStatus.Failed;
                log.Error = ex.Message;
            }
            _db.Communications.Add(log);
            await _db.SaveChangesAsync();
        }

        /// <summary>Confirmation by the tenant's preferred channel (letters are not sent for portal actions).</summary>
        public Task ConfirmAsync(Tenant tenant, string templateCode, string smsText, string subject, string html)
        {
            if (tenant.ContactPreference == ContactPreference.Sms && !string.IsNullOrWhiteSpace(tenant.MobilePhone))
                return SmsAsync(tenant, templateCode, smsText);
            return EmailAsync(tenant, templateCode, subject, html);
        }
    }
}
