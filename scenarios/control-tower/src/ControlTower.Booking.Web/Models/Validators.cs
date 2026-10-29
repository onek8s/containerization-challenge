using System.Globalization;
using FluentValidation;
using Proseware.ControlTower.Domain;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Models;

public sealed class BookingPartiesValidator : AbstractValidator<BookingDraft>
{
    public BookingPartiesValidator()
    {
        RuleFor(d => d.CustomerId).NotNull().WithMessage("Select the customer.");
        RuleFor(d => d.OriginId).NotNull().WithMessage("Select the pickup location.");
        RuleFor(d => d.DestinationId).NotNull().WithMessage("Select the delivery location.")
            .NotEqual(d => d.OriginId).WithMessage("Pickup and delivery locations must differ.");
        RuleFor(d => d.ShipperName).NotEmpty().MaximumLength(70);
        RuleFor(d => d.ConsigneeName).NotEmpty().MaximumLength(70);
        RuleFor(d => d.ConsigneeEmail).EmailAddress().When(d => !string.IsNullOrEmpty(d.ConsigneeEmail));
        RuleFor(d => d.CustomerReference).MaximumLength(35);
        RuleFor(d => d.GoodsDescription).NotEmpty().MaximumLength(350);
        RuleFor(d => d.IncotermCode)
            .Must(code => Incoterm.TryParse(code, out _)).WithMessage("Unknown Incoterm.")
            .Must((d, code) => !Incoterm.TryParse(code, out var i) || i.IsValidFor(d.Mode))
            .WithMessage(d => $"{d.IncotermCode} is for sea freight only.");
        RuleFor(d => d.PickupDate).GreaterThanOrEqualTo(DateTime.Today.AddDays(-1)).WithMessage("Pickup date is in the past.");
        RuleFor(d => d.DeclaredValue).GreaterThan(0).When(d => d.DeclaredValue.HasValue);
        RuleFor(d => d.TemperatureMin).LessThan(d => d.TemperatureMax).When(d => d.TemperatureMin.HasValue && d.TemperatureMax.HasValue)
            .WithMessage("Minimum temperature must be below the maximum.");
        RuleFor(d => d.TemperatureMin).InclusiveBetween(-30m, 30m).When(d => d.TemperatureMin.HasValue);
        RuleFor(d => d.TemperatureMax).InclusiveBetween(-30m, 30m).When(d => d.TemperatureMax.HasValue);
        RuleFor(d => d.DeclaredValue).NotNull().When(d => d.Insurance).WithMessage("Insurance needs the declared value.");
    }
}

public sealed class PackageLineValidator : AbstractValidator<PackageLine>
{
    private static readonly string[] HazardClasses =
        { "1", "2.1", "2.2", "2.3", "3", "4.1", "4.2", "4.3", "5.1", "5.2", "6.1", "6.2", "7", "8", "9" };

    public PackageLineValidator()
    {
        RuleFor(p => p.Quantity).InclusiveBetween(1, 999);
        RuleFor(p => p.WeightKg).GreaterThan(0).LessThanOrEqualTo(30000);
        RuleFor(p => p.LengthCm).InclusiveBetween(1, 1400);
        RuleFor(p => p.WidthCm).InclusiveBetween(1, 260);
        RuleFor(p => p.HeightCm).InclusiveBetween(1, 300);
        RuleFor(p => p.Marks).MaximumLength(140);
        RuleFor(p => p.WeightKg).LessThanOrEqualTo(31.5m).When(p => p.Kind == PackageKind.Parcel)
            .WithMessage("Parcels can weigh at most 31.5 kg.");
        When(p => p.HasDangerousGoods, () =>
        {
            RuleFor(p => p.DgUnNumber).Matches("^[0-9]{4}$").WithMessage("UN number has four digits.");
            RuleFor(p => p.DgShippingName).NotEmpty().MaximumLength(200);
            RuleFor(p => p.DgClass).Must(c => HazardClasses.Contains(c)).WithMessage("Unknown hazard class.");
            RuleFor(p => p.DgPackingGroup).Must(pg => pg is null or "I" or "II" or "III").WithMessage("Packing group is I, II or III.");
            RuleFor(p => p.DgNetQuantity).NotNull().GreaterThan(0);
        });
    }
}

public sealed class BookingPackagesValidator : AbstractValidator<BookingDraft>
{
    public BookingPackagesValidator()
    {
        RuleFor(d => d.Packages).NotEmpty().WithMessage("Add at least one package.");
        RuleForEach(d => d.Packages).SetValidator(new PackageLineValidator());
        RuleFor(d => d.Packages)
            .Must(p => !p.Any(x => x.HasDangerousGoods && x.DgClass == "7"))
            .WithMessage("Class 7 (radioactive) goods are not accepted; contact the dangerous goods advisor.");
        RuleFor(d => d.Packages)
            .Must((d, p) => d.Mode != TransportMode.Air || p.Sum(x => x.WeightKg * x.Quantity) <= 5000)
            .WithMessage("Air bookings above 5 000 kg need a charter quote.");
    }
}

public sealed class CustomerEditValidator : AbstractValidator<CustomerEditModel>
{
    public CustomerEditValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(10).Matches("^[A-Z0-9]+$").WithMessage("Code uses upper case letters and digits.");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(70);
        RuleFor(c => c.CountryCode).Length(2);
        RuleFor(c => c.InvoiceCurrency).Length(3);
        RuleFor(c => c.ContactEmail).EmailAddress().When(c => !string.IsNullOrEmpty(c.ContactEmail));
        RuleFor(c => c.Eori).Matches("^[A-Z]{2}[0-9A-Z]{1,15}$").When(c => !string.IsNullOrEmpty(c.Eori));
        RuleFor(c => c.Eori).NotEmpty().When(c => c.CustomsBrokerageAgreed).WithMessage("Customs brokerage needs the customer's EORI.");
        RuleFor(c => c.DomesticSlaDays).InclusiveBetween(1, 10);
        RuleFor(c => c.DiscountPercent).InclusiveBetween(0, 50);
    }
}

public sealed class RateCardEditValidator : AbstractValidator<RateCardEditModel>
{
    public RateCardEditValidator()
    {
        RuleFor(r => r.OriginCountry).Length(2);
        RuleFor(r => r.DestinationCountry).Length(2);
        RuleFor(r => r.Currency).Length(3);
        RuleFor(r => r.MinimumCharge).GreaterThanOrEqualTo(0);
        RuleFor(r => r.FuelSurchargePercent).InclusiveBetween(0, 100);
        RuleFor(r => r.TransitDays).InclusiveBetween(0, 90);
        RuleFor(r => r.ValidTo).GreaterThan(r => r.ValidFrom);
        RuleFor(r => r.Breaks).NotEmpty().Must(b => RateBreakParser.TryParse(b, out _))
            .WithMessage("Enter one break per line as \"from kg; price per 100 kg\", starting from 0.");
    }
}

public static class RateBreakParser
{
    public static bool TryParse(string? text, out List<(decimal FromKg, decimal Price)> breaks)
    {
        breaks = new();
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !decimal.TryParse(parts[0].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var from)
                || !decimal.TryParse(parts[1].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                || from < 0 || price <= 0)
                return false;
            breaks.Add((from, price));
        }
        breaks = breaks.OrderBy(b => b.FromKg).ToList();
        return breaks.Count > 0 && breaks[0].FromKg == 0 && breaks.Select(b => b.FromKg).Distinct().Count() == breaks.Count;
    }

    public static string Format(IEnumerable<(decimal FromKg, decimal Price)> breaks) =>
        string.Join("\n", breaks.Select(b => string.Create(CultureInfo.InvariantCulture, $"{b.FromKg:0.#}; {b.Price:0.00}")));
}
