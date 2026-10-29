using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.Contracts.Customers;
using VanArsdel.Customers.Api.Documents;

namespace VanArsdel.Customers.Api.Domain
{
    public class CustomerService
    {
        private static readonly Random CardRandom = new Random();
        private readonly ICustomerStore _store;
        private readonly IPublishEndpoint _publish;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(ICustomerStore store, IPublishEndpoint publish, ILogger<CustomerService> logger)
        {
            _store = store;
            _publish = publish;
            _logger = logger;
        }

        public async Task<CustomerDocument> EnrolAsync(EnrolRequest request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var existing = await _store.FindByEmailAsync(email, ct);
            if (existing != null)
            {
                if (existing.Loyalty != null) throw new ConflictException("The customer is already a member.");
                existing.Loyalty = NewAccount();
                existing.FirstName ??= request.FirstName;
                existing.LastName ??= request.LastName;
                existing.Phone ??= request.Phone;
                existing.UpdatedUtc = DateTime.UtcNow;
                if (!await _store.SaveAsync(existing, ct)) throw new ConflictException("The customer was changed at the same time. Try again.");
                return existing;
            }

            var customer = new CustomerDocument
            {
                // The first half of a NewId is the same for every id from one process; the second half varies.
                Id = "C" + NewId.NextGuid().ToString("N").Substring(16).ToUpperInvariant(),
                Email = email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                Market = request.Market ?? "FI",
                PreferredStore = request.PreferredStore,
                BirthDate = request.BirthDate,
                Consents = new Consents { Email = request.EmailConsent, Sms = request.SmsConsent, UpdatedUtc = DateTime.UtcNow },
                Loyalty = NewAccount(),
                Source = request.Source ?? "Store",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            };
            if (!await _store.SaveAsync(customer, ct)) throw new ConflictException("A customer with this e-mail address already exists.");
            _logger.LogInformation("Enrolled customer {CustomerId} with card {CardNo}", customer.Id, customer.Loyalty.CardNo);
            return customer;
        }

        /// <summary>Applies a change with optimistic retries (POS files for one card can arrive in parallel).</summary>
        public async Task<CustomerDocument> UpdateAsync(Func<CancellationToken, Task<CustomerDocument>> load, Func<CustomerDocument, bool> change, CancellationToken ct)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var customer = await load(ct);
                if (customer == null) return null;
                if (!change(customer)) return customer;
                customer.UpdatedUtc = DateTime.UtcNow;
                if (await _store.SaveAsync(customer, ct)) return customer;
                await Task.Delay(50 * (attempt + 1), ct);
            }
            throw new ConflictException("The customer is being changed by another process.");
        }

        public async Task<int> RecalculateTiersAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var changed = 0;
            const int page = 200;
            for (var skip = 0; ; skip += page)
            {
                var batch = await _store.PageAsync(skip, page, ct);
                if (batch.Count == 0) break;
                foreach (var c in batch)
                {
                    if (c.Loyalty == null || c.Erased) continue;
                    var spend = LoyaltyRules.RollingSpend(c.Loyalty, now);
                    var tier = LoyaltyRules.TierFor(spend);
                    if (tier == c.Loyalty.Tier && spend == c.Loyalty.RollingSpend) continue;

                    var previous = c.Loyalty.Tier;
                    var updated = await UpdateAsync(t => _store.GetAsync(c.Id, t), doc =>
                    {
                        doc.Loyalty.RollingSpend = spend;
                        if (doc.Loyalty.Tier != tier)
                        {
                            doc.Loyalty.Tier = tier;
                            doc.Loyalty.TierSince = now;
                        }
                        return true;
                    }, ct);

                    if (previous != tier)
                    {
                        changed++;
                        await _publish.Publish(new LoyaltyTierChanged
                        {
                            CustomerId = updated.Id,
                            CardNo = updated.Loyalty.CardNo,
                            Email = updated.Consents.Email ? updated.Email : null,
                            Phone = updated.Consents.Sms ? updated.Phone : null,
                            FirstName = updated.FirstName,
                            PreviousTier = previous,
                            NewTier = tier,
                            RollingSpend = spend,
                        }, ct);
                    }
                }
            }
            _logger.LogInformation("Tier recalculation done, {Changed} tier changes", changed);
            return changed;
        }

        public async Task EraseAsync(string id, CancellationToken ct)
        {
            string emailHash = null;
            var customer = await UpdateAsync(t => _store.GetAsync(id, t), c =>
            {
                if (c.Erased) return false;
                emailHash = Hash(c.Email);
                c.Erased = true;
                c.Email = null;
                c.FirstName = null;
                c.LastName = null;
                c.Phone = null;
                c.BirthDate = null;
                c.Addresses = new List<Address>();
                c.Consents = new Consents { UpdatedUtc = DateTime.UtcNow };
                return true;
            }, ct) ?? throw new NotFoundException("Customer", id);

            if (emailHash != null)
            {
                await _publish.Publish(new CustomerErased { CustomerId = customer.Id, EmailHash = emailHash, ErasedUtc = DateTime.UtcNow }, ct);
            }
        }

        private static LoyaltyAccount NewAccount()
        {
            string cardNo;
            lock (CardRandom)
            {
                cardNo = LoyaltyRules.NewCardNo(CardRandom);
            }
            return new LoyaltyAccount { CardNo = cardNo, Tier = LoyaltyTiers.Bronze, TierSince = DateTime.UtcNow, JoinedUtc = DateTime.UtcNow };
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            // Same hash as the notifications send log, so erasure can find the rows there.
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes((value ?? string.Empty).Trim().ToLowerInvariant())));
        }
    }

    public class EnrolRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Market { get; set; }
        public string PreferredStore { get; set; }
        public DateTime? BirthDate { get; set; }
        public bool EmailConsent { get; set; }
        public bool SmsConsent { get; set; }
        public string Source { get; set; }
    }
}
