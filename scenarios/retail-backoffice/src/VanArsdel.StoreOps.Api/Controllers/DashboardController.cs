using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Security;
using VanArsdel.StoreOps.Api.Data;
using VanArsdel.StoreOps.Api.Domain;
using VanArsdel.StoreOps.Api.Services;

namespace VanArsdel.StoreOps.Api.Controllers
{
    public class SubmitCashUp
    {
        public DateTime BusinessDate { get; set; }
        public decimal CountedCash { get; set; }
        public string Notes { get; set; }
    }

    public class SubmitCashUpValidator : AbstractValidator<SubmitCashUp>
    {
        public SubmitCashUpValidator()
        {
            RuleFor(x => x.BusinessDate).NotEmpty().LessThanOrEqualTo(_ => DateTime.Today.AddDays(1));
            RuleFor(x => x.CountedCash).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Notes).MaximumLength(500);
        }
    }

    public class FootfallReading
    {
        public DateTime Date { get; set; }
        public int Hour { get; set; }
        public int Visitors { get; set; }
    }

    [ApiController]
    [Route("api/stores/{store}")]
    [Authorize(Policy = Policies.Read)]
    public class DashboardController : ControllerBase
    {
        private readonly StoreOpsDbContext _db;

        public DashboardController(StoreOpsDbContext db)
        {
            _db = db;
        }

        /// <summary>Today's numbers for the store manager start page.</summary>
        [HttpGet("dashboard")]
        public async Task<object> Dashboard(string store, [FromQuery] DateTime? date)
        {
            var code = StoreAccess.Resolve(User, store);
            var day = (date ?? DateTime.Today).Date;
            var takings = await _db.Takings.AsNoTracking().SingleOrDefaultAsync(t => t.StoreCode == code && t.BusinessDate == day);
            var lastWeek = await _db.Takings.AsNoTracking().SingleOrDefaultAsync(t => t.StoreCode == code && t.BusinessDate == day.AddDays(-7));
            var footfall = await _db.Footfall.AsNoTracking().Where(f => f.StoreCode == code && f.Date == day).SumAsync(f => (int?)f.Visitors) ?? 0;
            var openTasks = await _db.PickTasks.CountAsync(t => t.StoreCode == code && (t.Status == PickTaskStatus.Open || t.Status == PickTaskStatus.Picking));
            var overdue = await _db.PickTasks.CountAsync(t => t.StoreCode == code && t.Status == PickTaskStatus.Open && t.DueUtc < DateTime.UtcNow);
            var waiting = await _db.Collections.CountAsync(c => c.StoreCode == code && c.Status == CollectionStatus.Waiting);

            var receipts = takings?.Receipts ?? 0;
            return new
            {
                StoreCode = code,
                BusinessDate = day,
                NetSales = takings?.NetSales ?? 0m,
                NetSalesLastWeek = lastWeek?.NetSales ?? 0m,
                Receipts = receipts,
                Items = takings?.Items ?? 0,
                AverageBasket = receipts == 0 ? 0m : Math.Round(takings.NetSales / receipts, 2),
                Footfall = footfall,
                Conversion = footfall == 0 ? 0m : Math.Round(100m * receipts / footfall, 1),
                LoyaltyShare = receipts == 0 ? 0m : Math.Round(100m * takings.LoyaltyReceipts / receipts, 1),
                OpenPickTasks = openTasks,
                OverduePickTasks = overdue,
                WaitingCollections = waiting,
                LastPosFile = takings?.LastFile,
            };
        }

        [HttpGet("takings")]
        public async Task<List<DailyTakings>> Takings(string store, [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            var code = StoreAccess.Resolve(User, store);
            return await _db.Takings.AsNoTracking()
                .Where(t => t.StoreCode == code && t.BusinessDate >= from.Date && t.BusinessDate <= to.Date)
                .OrderBy(t => t.BusinessDate).ToListAsync();
        }

        /// <summary>End of day cash count. The difference to the POS cash total goes to finance.</summary>
        [HttpPost("cash-up")]
        [Authorize(Policy = Policies.Store)]
        public async Task<CashUp> CashUp(string store, [FromBody] SubmitCashUp command)
        {
            var code = StoreAccess.Resolve(User, store);
            if (!User.HasAnyRole(Contracts.Roles.StoreManager, Contracts.Roles.Admin))
            {
                throw new DomainException("Only the store manager can sign off the cash-up.");
            }
            var day = command.BusinessDate.Date;
            var takings = await _db.Takings.AsNoTracking().SingleOrDefaultAsync(t => t.StoreCode == code && t.BusinessDate == day);
            var cashUp = new CashUp
            {
                StoreCode = code,
                BusinessDate = day,
                ExpectedCash = takings?.CashTotal ?? 0m,
                CountedCash = command.CountedCash,
                CountedBy = User.UserName(),
                Notes = command.Notes,
                CreatedUtc = DateTime.UtcNow,
            };
            cashUp.Difference = cashUp.CountedCash - cashUp.ExpectedCash;
            _db.CashUps.Add(cashUp);
            await _db.SaveChangesAsync();
            return cashUp;
        }

        [HttpGet("cash-ups")]
        public async Task<List<CashUp>> CashUps(string store, [FromQuery] int days = 14)
        {
            var code = StoreAccess.Resolve(User, store);
            var from = DateTime.Today.AddDays(-days);
            return await _db.CashUps.AsNoTracking().Where(c => c.StoreCode == code && c.BusinessDate >= from)
                .OrderByDescending(c => c.BusinessDate).ThenByDescending(c => c.CreatedUtc).ToListAsync();
        }

        /// <summary>Door counter readings. The counters post hourly through the store router.</summary>
        [HttpPost("footfall")]
        [Authorize(Policy = Policies.Store)]
        public async Task<object> Footfall(string store, [FromBody] List<FootfallReading> readings)
        {
            var code = StoreAccess.Resolve(User, store);
            foreach (var r in readings.Where(r => r.Hour >= 0 && r.Hour < 24 && r.Visitors >= 0))
            {
                var date = r.Date.Date;
                var row = await _db.Footfall.SingleOrDefaultAsync(f => f.StoreCode == code && f.Date == date && f.Hour == r.Hour);
                if (row == null)
                {
                    _db.Footfall.Add(new Footfall { StoreCode = code, Date = date, Hour = r.Hour, Visitors = r.Visitors });
                }
                else
                {
                    row.Visitors = r.Visitors;
                }
            }
            await _db.SaveChangesAsync();
            return new { Saved = readings.Count };
        }
    }
}
