using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.Customers.Api.Documents;
using VanArsdel.Customers.Api.Domain;

namespace VanArsdel.Customers.Api.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize(Policy = Policies.Read)]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerStore _store;
        private readonly CustomerService _customers;

        public CustomersController(ICustomerStore store, CustomerService customers)
        {
            _store = store;
            _customers = customers;
        }

        /// <summary>Finds customers by e-mail, card number, phone or last name.</summary>
        [HttpGet("customers")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int take = 20, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3) throw new DomainException("Give at least three characters.");
            var result = await _store.SearchAsync(q.Trim(), Math.Clamp(take, 1, 100), ct);
            return Ok(result.Where(c => !c.Erased).Select(Summary));
        }

        [HttpGet("customers/{id}")]
        public async Task<CustomerDocument> Get(string id, CancellationToken ct)
        {
            var c = await _store.GetAsync(id, ct) ?? throw new NotFoundException("Customer", id);
            if (c.Loyalty != null) c.Loyalty.Transactions = c.Loyalty.Transactions.OrderByDescending(t => t.Date).Take(50).ToList();
            return c;
        }

        [HttpGet("cards/{cardNo}")]
        public async Task<IActionResult> ByCard(string cardNo, CancellationToken ct)
        {
            var c = await _store.FindByCardAsync(cardNo, ct) ?? throw new NotFoundException("Card", cardNo);
            return Ok(Summary(c));
        }

        [HttpPost("customers")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Enrol([FromBody] EnrolRequest request, CancellationToken ct)
        {
            var c = await _customers.EnrolAsync(request, ct);
            return CreatedAtAction(nameof(Get), new { id = c.Id }, Summary(c));
        }

        [HttpPut("customers/{id}/consents")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> Consents(string id, [FromBody] Consents consents, CancellationToken ct)
        {
            var c = await _customers.UpdateAsync(t => _store.GetAsync(id, t), doc =>
            {
                doc.Consents = new Consents { Email = consents.Email, Sms = consents.Sms, Profiling = consents.Profiling, UpdatedUtc = DateTime.UtcNow };
                return true;
            }, ct) ?? throw new NotFoundException("Customer", id);
            return Ok(Summary(c));
        }

        [HttpPost("customers/{id}/points")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> AdjustPoints(string id, [FromBody] PointsAdjustment adjustment, CancellationToken ct)
        {
            var user = User.UserName();
            var c = await _customers.UpdateAsync(t => _store.GetAsync(id, t), doc =>
            {
                if (doc.Loyalty == null) throw new DomainException("The customer is not a member.");
                doc.Loyalty.Transactions.Add(new PointsTransaction { Date = DateTime.UtcNow, Type = "Adjust", Points = adjustment.Points, Source = user, Reference = adjustment.Reason });
                doc.Loyalty.Points = Math.Max(0, doc.Loyalty.Points + adjustment.Points);
                return true;
            }, ct) ?? throw new NotFoundException("Customer", id);
            return Ok(Summary(c));
        }

        /// <summary>Right to erasure: removes personal data, keeps the anonymous points history.</summary>
        [HttpPost("customers/{id}/erase")]
        [Authorize(Policy = Policies.Erase)]
        public async Task<IActionResult> Erase(string id, CancellationToken ct)
        {
            await _customers.EraseAsync(id, ct);
            return NoContent();
        }

        /// <summary>Daily tier recalculation (scheduler, 03:00).</summary>
        [HttpPost("loyalty/recalculate-tiers")]
        [Authorize(Policy = Policies.Write)]
        public async Task<IActionResult> RecalculateTiers(CancellationToken ct) => Ok(new { changed = await _customers.RecalculateTiersAsync(ct) });

        [HttpGet("loyalty/stats")]
        public async Task<IActionResult> Stats(CancellationToken ct) => Ok(new { customers = await _store.CountAsync(ct) });

        private static object Summary(CustomerDocument c) => new
        {
            c.Id,
            c.Email,
            c.FirstName,
            c.LastName,
            c.Phone,
            c.Market,
            c.PreferredStore,
            CardNo = c.Loyalty?.CardNo,
            Tier = c.Loyalty?.Tier,
            Points = c.Loyalty?.Points ?? 0,
            RollingSpend = c.Loyalty?.RollingSpend ?? 0,
            c.Consents,
        };
    }

    public class PointsAdjustment
    {
        public int Points { get; set; }
        public string Reason { get; set; }
    }

    public class EnrolRequestValidator : AbstractValidator<EnrolRequest>
    {
        public EnrolRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Phone).Matches(@"^\+?[0-9 ]{6,20}$").When(x => !string.IsNullOrEmpty(x.Phone));
            RuleFor(x => x.Market).Must(m => m == null || VanArsdel.Contracts.Markets.Currencies.ContainsKey(m)).WithMessage("Unknown market.");
            RuleFor(x => x.BirthDate).LessThan(DateTime.UtcNow.AddYears(-15)).When(x => x.BirthDate.HasValue).WithMessage("Members must be at least 15 years old.");
        }
    }
}
