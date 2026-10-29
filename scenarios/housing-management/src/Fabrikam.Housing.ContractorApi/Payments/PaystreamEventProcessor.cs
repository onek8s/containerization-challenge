using System;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Common;
using Fabrikam.Housing.Communications;
using Fabrikam.Housing.Data;
using Fabrikam.Housing.Integration.Messaging;
using Fabrikam.Housing.Integration.Payments;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Tenancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.Payments
{
    /// <summary>
    /// Applies Paystream events to mandates, direct debit payments and rent accounts.
    /// This file is shared by the contractor API (webhooks) and the jobs server (events polling).
    /// </summary>
    public class PaystreamEventProcessor
    {
        private readonly HousingDbContext _db;
        private readonly IPaymentsGateway _payments;
        private readonly ISmsSender _sms;
        private readonly IClock _clock;
        private readonly ILogger<PaystreamEventProcessor> _logger;

        public PaystreamEventProcessor(HousingDbContext db, IPaymentsGateway payments, ISmsSender sms, IClock clock, ILogger<PaystreamEventProcessor> logger)
        {
            _db = db;
            _payments = payments;
            _sms = sms;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>Returns false when the event was already processed.</summary>
        public async Task<bool> ProcessAsync(PaymentEvent e, string channel)
        {
            if (string.IsNullOrEmpty(e.Id)) return false;
            if (await _db.ProcessedWebhooks.AnyAsync(p => p.EventId == e.Id)) return false;

            switch (e.ResourceType)
            {
                case "mandates":
                    await Mandate(e);
                    break;
                case "payments":
                    await Payment(e);
                    break;
                case "card_payments":
                    await CardPayment(e);
                    break;
                default:
                    _logger.LogDebug("Ignoring Paystream event {Id} for {Type}", e.Id, e.ResourceType);
                    break;
            }

            _db.ProcessedWebhooks.Add(new ProcessedWebhook { EventId = e.Id, ReceivedAt = _clock.Now, ResourceType = e.ResourceType, Action = e.Action });
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // The webhook and the poller can see the same event at the same moment.
                _logger.LogInformation(ex, "Paystream event {Id} was processed by another request ({Channel})", e.Id, channel);
                return false;
            }
            return true;
        }

        private async Task Mandate(PaymentEvent e)
        {
            var mandate = await _db.Mandates.SingleOrDefaultAsync(m => m.ProviderMandateId == e.ResourceId);
            if (mandate == null)
            {
                _logger.LogWarning("Paystream mandate {Mandate} not found (event {Id})", e.ResourceId, e.Id);
                return;
            }
            switch (e.Action)
            {
                case "submitted": mandate.Status = MandateStatus.Submitted; break;
                case "active": mandate.Status = MandateStatus.Active; break;
                case "failed": mandate.Status = MandateStatus.Failed; break;
                case "expired": mandate.Status = MandateStatus.Expired; break;
                case "cancelled":
                    mandate.Status = MandateStatus.Cancelled;
                    mandate.CancelledAt = _clock.Now;
                    mandate.CancellationReason = e.Cause ?? e.Description;
                    mandate.NextCollectionDate = null;
                    break;
                default: return;
            }
            mandate.ModifiedAt = _clock.Now;
            mandate.ModifiedBy = "paystream";
        }

        private async Task Payment(PaymentEvent e)
        {
            var payment = await _db.DirectDebitPayments.Include(p => p.Mandate).ThenInclude(m => m.Tenancy)
                .SingleOrDefaultAsync(p => p.ProviderPaymentId == e.ResourceId);
            if (payment == null)
            {
                _logger.LogWarning("Paystream payment {Payment} not found (event {Id})", e.ResourceId, e.Id);
                return;
            }
            payment.UpdatedAt = _clock.Now;
            var tenancyId = payment.Mandate.TenancyId;
            var posted = await _db.RentTransactions.AnyAsync(t => t.TenancyId == tenancyId && t.Reference == payment.ProviderPaymentId && t.Type == TransactionType.DirectDebit);

            switch (e.Action)
            {
                case "submitted":
                    payment.Status = PaymentStatus.Submitted;
                    break;
                case "confirmed":
                case "paid_out":
                    payment.Status = e.Action == "confirmed" ? PaymentStatus.Confirmed : PaymentStatus.PaidOut;
                    if (!posted)
                    {
                        var tx = new RentTransaction
                        {
                            TenancyId = tenancyId,
                            PostedOn = payment.ChargeDate.Date.AddHours(6),
                            RentWeek = RentCalendar.WeekStart(payment.ChargeDate),
                            Type = TransactionType.DirectDebit,
                            Amount = -payment.Amount,
                            Reference = payment.ProviderPaymentId,
                            Description = "Direct debit",
                            Source = "Paystream"
                        };
                        _db.RentTransactions.Add(tx);
                        await _db.SaveChangesAsync();
                        payment.RentTransactionId = tx.Id;
                    }
                    break;
                case "failed":
                case "charged_back":
                    payment.Status = e.Action == "failed" ? PaymentStatus.Failed : PaymentStatus.ChargedBack;
                    payment.FailureReason = e.Cause ?? e.Description;
                    var returned = await _db.RentTransactions.AnyAsync(t => t.TenancyId == tenancyId && t.Reference == payment.ProviderPaymentId && t.Type == TransactionType.DirectDebitReturned);
                    if (posted && !returned)
                    {
                        _db.RentTransactions.Add(new RentTransaction
                        {
                            TenancyId = tenancyId,
                            PostedOn = _clock.Now,
                            RentWeek = RentCalendar.WeekStart(_clock.Today),
                            Type = TransactionType.DirectDebitReturned,
                            Amount = payment.Amount,
                            Reference = payment.ProviderPaymentId,
                            Description = e.Action == "failed" ? "Direct debit returned unpaid" : "Direct debit charged back",
                            Source = "Paystream"
                        });
                    }
                    await TellTenant(tenancyId, payment.Amount);
                    break;
                case "cancelled":
                    payment.Status = PaymentStatus.Cancelled;
                    break;
            }
        }

        private async Task CardPayment(PaymentEvent e)
        {
            if (e.Action != "succeeded" && e.Action != "confirmed") return;
            var result = await _payments.GetCardPaymentAsync(e.ResourceId);
            if (result == null || !string.Equals(result.Status, "succeeded", StringComparison.OrdinalIgnoreCase)) return;

            var paymentRef = result.PaymentId ?? result.SessionId;
            if (await _db.RentTransactions.AnyAsync(t => t.Type == TransactionType.CardPayment && (t.Reference == paymentRef || t.Reference == result.SessionId)))
                return; // already posted when the tenant came back to the portal

            var tenancy = await _db.Tenancies.SingleOrDefaultAsync(t => t.Reference == result.Reference);
            if (tenancy == null)
            {
                _logger.LogWarning("Card payment {Session} has unknown tenancy reference {Reference}", result.SessionId, result.Reference);
                return;
            }
            var at = result.CompletedAt ?? _clock.Now;
            _db.RentTransactions.Add(new RentTransaction
            {
                TenancyId = tenancy.Id,
                PostedOn = at,
                RentWeek = RentCalendar.WeekStart(at),
                Type = TransactionType.CardPayment,
                Amount = -result.Amount,
                Reference = paymentRef,
                Description = "Card payment",
                Source = "Paystream"
            });
        }

        private async Task TellTenant(int tenancyId, decimal amount)
        {
            var lead = await _db.Tenants.FirstOrDefaultAsync(t => t.TenancyId == tenancyId && t.IsLeadTenant);
            if (lead == null || string.IsNullOrEmpty(lead.MobilePhone)) return;
            var text = SmsTemplates.Truncate(SmsTemplates.DirectDebitFailed(lead.FirstName, amount));
            var log = new CommunicationLog { TenancyId = tenancyId, Channel = Channel.Sms, TemplateCode = "DirectDebitFailed", Recipient = lead.MobilePhone, Body = text, CreatedAt = _clock.Now };
            try
            {
                log.ProviderReference = await _sms.SendAsync(lead.MobilePhone, text);
                log.Status = CommunicationStatus.Sent;
                log.SentAt = _clock.Now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Direct debit failure SMS to tenancy {TenancyId} failed", tenancyId);
                log.Status = CommunicationStatus.Failed;
                log.Error = ex.Message;
            }
            _db.Communications.Add(log);
        }
    }
}
