using System;
using AutoMapper;
using VanArsdel.Integration.Domain.Partners;
using VanArsdel.Integration.Management.Operations;
using VanArsdel.Integration.Monitoring;
using VanArsdel.Integration.OpsPortal.Models;

namespace VanArsdel.Integration.OpsPortal
{
    public static class MappingConfig
    {
        public static void Configure()
        {
            Mapper.Initialize(cfg => cfg.AddProfile<PortalProfile>());
            Mapper.AssertConfigurationIsValid();
        }
    }

    public class PortalProfile : Profile
    {
        public PortalProfile()
        {
            CreateMap<SuspendedInstanceInfo, InstanceRow>()
                .ForMember(d => d.Kind, o => o.MapFrom(s => s.Kind.ToString()))
                .ForMember(d => d.Service, o => o.MapFrom(s => s.ShortServiceName))
                .ForMember(d => d.SuspendedLocal, o => o.MapFrom(s => s.SuspendedUtc.ToLocalTime()))
                .ForMember(d => d.ErrorShort, o => o.MapFrom(s => Shorten(s.ErrorDescription, 160)))
                .ForMember(d => d.Messages, o => o.MapFrom(s => s.MessageIds.Count));

            CreateMap<TradingPartner, PartnerRow>()
                .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()))
                .ForMember(d => d.Standard, o => o.MapFrom(s => s.Standard == DocumentStandard.Xml ? "XML" : s.Standard.ToString().ToUpperInvariant() + " " + s.Version))
                .ForMember(d => d.Transport, o => o.MapFrom(s => s.Transport.ToString().ToUpperInvariant()))
                .ForMember(d => d.Interchange, o => o.MapFrom(s => string.IsNullOrEmpty(s.InterchangeId) ? string.Empty : s.InterchangeQualifier + "/" + s.InterchangeId))
                .ForMember(d => d.PartyState, o => o.Ignore());

            CreateMap<PartnerCrossReference, XrefRow>()
                .ForMember(d => d.Kind, o => o.MapFrom(s => s.Kind.ToString()));

            CreateMap<HostInstanceStatus, HostRow>()
                .ForMember(d => d.State, o => o.MapFrom(s => s.State.ToString()))
                .ForMember(d => d.Running, o => o.MapFrom(s => s.State == HostInstanceState.Running));

            CreateMap<ContextEntry, ContextRow>();
        }

        internal static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var oneLine = text.Replace("\r", " ").Replace("\n", " ");
            return oneLine.Length <= max ? oneLine : oneLine.Substring(0, max) + "…";
        }
    }
}
