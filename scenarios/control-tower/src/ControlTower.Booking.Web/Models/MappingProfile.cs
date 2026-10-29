using AutoMapper;
using Proseware.ControlTower.Domain.Parties;
using Proseware.ControlTower.Domain.Pricing;
using Proseware.ControlTower.Domain.Shipments;

namespace Proseware.ControlTower.Booking.Models;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Leg, LegViewModel>()
            .ForMember(d => d.CarrierName, o => o.MapFrom(s => s.Carrier != null ? s.Carrier.Name : null))
            .ForMember(d => d.From, o => o.MapFrom(s => s.From != null ? s.From.Code + " " + s.From.City : ""))
            .ForMember(d => d.To, o => o.MapFrom(s => s.To != null ? s.To.Code + " " + s.To.City : ""))
            .ForMember(d => d.VehiclePlate, o => o.MapFrom(s => s.Vehicle != null ? s.Vehicle.Plate : null))
            .ForMember(d => d.TrackingUrl, o => o.MapFrom(s => s.Carrier != null ? s.Carrier.TrackingUrl(s.CarrierReference) : null));

        CreateMap<Milestone, MilestoneViewModel>()
            .ForMember(d => d.Description, o => o.MapFrom(s => MilestoneCodes.Describe(s.Code)))
            .ForMember(d => d.Location, o => o.MapFrom(s => s.LocationText));

        CreateMap<Customer, CustomerEditModel>().ReverseMap()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedUtc, o => o.Ignore());

        CreateMap<RateCard, RateCardEditModel>()
            .ForMember(d => d.Breaks, o => o.MapFrom(s => RateBreakParser.Format(s.Breaks.OrderBy(b => b.FromKg).Select(b => new ValueTuple<decimal, decimal>(b.FromKg, b.PricePer100Kg)))))
            .ForMember(d => d.Carriers, o => o.Ignore());
    }
}
