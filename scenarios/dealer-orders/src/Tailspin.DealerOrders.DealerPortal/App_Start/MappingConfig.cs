using System.Linq;
using AutoMapper;
using Tailspin.DealerOrders.Data;
using Tailspin.DealerOrders.Data.Queries;
using Tailspin.DealerOrders.DealerPortal.Infrastructure;
using Tailspin.DealerOrders.DealerPortal.Models;
using Tailspin.DealerOrders.Domain;

namespace Tailspin.DealerOrders.DealerPortal
{
    public static class MappingConfig
    {
        public static void Register()
        {
            Mapper.Initialize(cfg => cfg.AddProfile<PortalProfile>());
            Mapper.AssertConfigurationIsValid();
        }
    }

    public class PortalProfile : Profile
    {
        public PortalProfile()
        {
            CreateMap<Quote, QuoteViewModel>()
                .ForMember(d => d.StatusText, o => o.MapFrom(s => StatusTexts.Quote(s.Status)))
                .ForMember(d => d.Lines, o => o.MapFrom(s => s.Lines.OrderBy(l => l.LineNumber)))
                .ForMember(d => d.CanEdit, o => o.MapFrom(s => s.CanEdit))
                .ForMember(d => d.CanAccept, o => o.Ignore())
                .ForMember(d => d.DeliveryAddresses, o => o.Ignore());
            CreateMap<QuoteLine, QuoteLineViewModel>()
                .ForMember(d => d.OptionList, o => o.MapFrom(s => s.Options.ToList()));
            CreateMap<SalesOrder, OrderViewModel>()
                .ForMember(d => d.StatusText, o => o.MapFrom(s => StatusTexts.Order(s.Status)))
                .ForMember(d => d.DeliveryAddressText, o => o.MapFrom(s => s.DeliveryAddress == null ? null : s.DeliveryAddress.Name + ", " + s.DeliveryAddress.City))
                .ForMember(d => d.Lines, o => o.MapFrom(s => s.Lines.OrderBy(l => l.LineNumber)))
                .ForMember(d => d.History, o => o.MapFrom(s => s.History.OrderByDescending(h => h.ChangedUtc)))
                .ForMember(d => d.HasEdi, o => o.MapFrom(s => s.EdiMessageId.HasValue))
                .ForMember(d => d.Documents, o => o.Ignore());
            CreateMap<OrderLine, OrderLineViewModel>();
            CreateMap<OrderStatusChange, StatusChangeViewModel>()
                .ForMember(d => d.Status, o => o.MapFrom(s => StatusTexts.Order(s.ToStatus)));
            CreateMap<OrderListItem, OrderRowViewModel>()
                .ForMember(d => d.StatusText, o => o.MapFrom(s => StatusTexts.Order(s.Status)));
            CreateMap<PortalUser, UserViewModel>()
                .ForMember(d => d.Roles, o => o.Ignore())
                .ForMember(d => d.IsLockedOut, o => o.Ignore());
        }
    }
}
