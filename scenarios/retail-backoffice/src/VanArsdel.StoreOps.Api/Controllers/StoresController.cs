using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;
using VanArsdel.BuildingBlocks.Exceptions;
using VanArsdel.BuildingBlocks.Outbox;
using VanArsdel.Contracts;
using VanArsdel.Contracts.Stores;
using VanArsdel.StoreOps.Api.Data;
using VanArsdel.StoreOps.Api.Domain;

namespace VanArsdel.StoreOps.Api.Controllers
{
    public class SaveStore
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } = LocationTypes.Store;
        public string Region { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string TimeZone { get; set; }
        public string Phone { get; set; }
        public string ManagerEmail { get; set; }
        public bool ShipFromStore { get; set; }
        public bool ClickAndCollect { get; set; }
        public string OpeningHours { get; set; }
        public DateTime? OpenedOn { get; set; }
    }

    public class SaveStoreValidator : AbstractValidator<SaveStore>
    {
        private static readonly string[] Types = { LocationTypes.Store, LocationTypes.Outlet, LocationTypes.DistributionCentre };

        public SaveStoreValidator()
        {
            // The code comes from the route, so it is only known when the controller validates again.
            RuleFor(x => x.Code).Matches("^[A-Z]{2}[0-9]{3}$|^DC[0-9]{2}$")
                .WithMessage("Store code is country + three digits (FI012), DC codes are DC + two digits.")
                .When(x => x.Code != null);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Type).Must(t => Types.Contains(t)).WithMessage("Unknown location type.");
            RuleFor(x => x.Country).NotEmpty().Must(c => Markets.Currencies.ContainsKey(c ?? string.Empty)).WithMessage("Unknown market.");
            RuleFor(x => x.TimeZone).Must(tz => string.IsNullOrEmpty(tz) || TZConvert.TryGetTimeZoneInfo(tz, out _))
                .WithMessage("Unknown time zone.");
            RuleFor(x => x.ManagerEmail).EmailAddress().When(x => !string.IsNullOrEmpty(x.ManagerEmail));
        }
    }

    [ApiController]
    [Route("api/stores")]
    [Authorize(Policy = Policies.Read)]
    public class StoresController : ControllerBase
    {
        private static readonly IDictionary<string, string> DefaultTimeZones = new Dictionary<string, string>
        {
            ["FI"] = "Europe/Helsinki", ["SE"] = "Europe/Stockholm", ["EE"] = "Europe/Tallinn", ["LV"] = "Europe/Riga", ["LT"] = "Europe/Vilnius",
        };

        private readonly StoreOpsDbContext _db;
        private readonly IOutbox _outbox;

        public StoresController(StoreOpsDbContext db, IOutbox outbox)
        {
            _db = db;
            _outbox = outbox;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<List<Store>> List([FromQuery] string country, [FromQuery] bool includeClosed = false, [FromQuery] bool collectOnly = false)
        {
            var q = _db.Stores.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(country)) q = q.Where(s => s.Country == country.ToUpperInvariant());
            if (!includeClosed) q = q.Where(s => s.ClosedOn == null);
            if (collectOnly) q = q.Where(s => s.ClickAndCollect);
            return await q.OrderBy(s => s.Code).ToListAsync();
        }

        [HttpGet("{code}")]
        public async Task<Store> Get(string code) =>
            await _db.Stores.AsNoTracking().SingleOrDefaultAsync(s => s.Code == code.ToUpperInvariant())
            ?? throw new NotFoundException("Store", code);

        /// <summary>Opens a store or updates its details. Other services learn about stores from these events.</summary>
        [HttpPut("{code}")]
        [Authorize(Policy = Policies.Manage)]
        public async Task<Store> Save(string code, [FromBody] SaveStore command)
        {
            command.Code = code.ToUpperInvariant();
            new SaveStoreValidator().ValidateAndThrow(command);

            var store = await _db.Stores.FindAsync(command.Code);
            var isNew = store == null;
            if (isNew)
            {
                store = new Store { Code = command.Code, OpenedOn = (command.OpenedOn ?? DateTime.UtcNow).Date };
                _db.Stores.Add(store);
            }
            else if (store.ClosedOn != null)
            {
                throw new ConflictException("Store " + code + " is closed.");
            }

            store.Name = command.Name;
            store.Type = command.Type;
            store.Region = command.Region;
            store.Country = command.Country.ToUpperInvariant();
            store.City = command.City;
            store.Street = command.Street;
            store.PostalCode = command.PostalCode;
            store.Currency = Markets.CurrencyOf(store.Country);
            store.TimeZone = command.TimeZone ?? (DefaultTimeZones.TryGetValue(store.Country, out var tz) ? tz : "Europe/Helsinki");
            store.Phone = command.Phone;
            store.ManagerEmail = command.ManagerEmail;
            store.ShipFromStore = command.ShipFromStore;
            store.ClickAndCollect = command.ClickAndCollect;
            store.OpeningHours = command.OpeningHours;
            store.UpdatedUtc = DateTime.UtcNow;

            if (isNew)
            {
                _outbox.Add(new StoreOpened
                {
                    StoreCode = store.Code, Name = store.Name, LocationType = store.Type, Country = store.Country, City = store.City,
                    Currency = store.Currency, ShipFromStore = store.ShipFromStore, ClickAndCollect = store.ClickAndCollect, OpenedOn = store.OpenedOn,
                });
            }
            else
            {
                _outbox.Add(new StoreUpdated
                {
                    StoreCode = store.Code, Name = store.Name, ShipFromStore = store.ShipFromStore, ClickAndCollect = store.ClickAndCollect,
                });
            }
            await _db.SaveChangesAsync();
            return store;
        }

        [HttpPost("{code}/close")]
        [Authorize(Policy = Policies.Manage)]
        public async Task<Store> Close(string code, [FromQuery] DateTime? closedOn)
        {
            var store = await _db.Stores.FindAsync(code.ToUpperInvariant()) ?? throw new NotFoundException("Store", code);
            if (store.ClosedOn != null) return store;
            if (await _db.PickTasks.AnyAsync(t => t.StoreCode == store.Code && (t.Status == PickTaskStatus.Open || t.Status == PickTaskStatus.Picking)))
            {
                throw new ConflictException("Store " + code + " still has open pick tasks.");
            }
            store.ClosedOn = (closedOn ?? DateTime.UtcNow).Date;
            store.ShipFromStore = false;
            store.ClickAndCollect = false;
            store.UpdatedUtc = DateTime.UtcNow;
            _outbox.Add(new StoreClosed { StoreCode = store.Code, ClosedOn = store.ClosedOn.Value });
            await _db.SaveChangesAsync();
            return store;
        }

        /// <summary>Local time and whether the store is open now, for the web shop store finder.</summary>
        [HttpGet("{code}/now")]
        [AllowAnonymous]
        public async Task<object> Now(string code)
        {
            var store = await Get(code);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo(store.TimeZone));
            return new { store.Code, LocalTime = local, Open = OpeningHours.IsOpen(store.OpeningHours, local) };
        }
    }
}
