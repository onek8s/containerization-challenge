using System.Linq;
using AutoMapper;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Rent;
using Fabrikam.Housing.Repairs;
using Fabrikam.Housing.Tenancies;
using Fabrikam.Housing.TenantPortal.Models;

namespace Fabrikam.Housing.TenantPortal.Mapping
{
    public class PortalProfile : Profile
    {
        public PortalProfile()
        {
            CreateMap<DirectDebitMandate, MandateViewModel>();
            CreateMap<DocumentRecord, DocumentViewModel>()
                .ForMember(d => d.Category, o => o.MapFrom(s => s.Category.ToString()));
            CreateMap<TenantEnquiry, EnquiryViewModel>();
            CreateMap<Tenant, ProfileViewModel>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName + " " + s.LastName))
                .ForMember(d => d.TenancyReference, o => o.MapFrom(s => s.Tenancy.Reference))
                .ForMember(d => d.Address, o => o.MapFrom(s => s.Tenancy.Property.AddressLine1 + ", " + s.Tenancy.Property.Postcode));

            CreateMap<WorkOrder, RepairSummary>()
                .ForMember(d => d.ContractorName, o => o.MapFrom(s => s.Contractor != null ? s.Contractor.Name : null))
                .ForMember(d => d.NextAppointment, o => o.MapFrom(s => s.Appointments.Where(a => !a.Cancelled).OrderByDescending(a => a.Date).Select(a => (System.DateTime?)a.Date).FirstOrDefault()))
                .ForMember(d => d.NextSlot, o => o.MapFrom(s => s.Appointments.Where(a => !a.Cancelled).OrderByDescending(a => a.Date).Select(a => (AppointmentSlot?)a.Slot).FirstOrDefault()));
            CreateMap<WorkOrder, RepairDetailViewModel>()
                .IncludeBase<WorkOrder, RepairSummary>()
                .ForMember(d => d.Appointments, o => o.MapFrom(s => s.Appointments.OrderBy(a => a.Date).ToList()))
                .ForMember(d => d.Events, o => o.MapFrom(s => s.Events.OrderBy(e => e.At).ToList()))
                .ForMember(d => d.Photos, o => o.Ignore())
                .ForMember(d => d.CanCancel, o => o.Ignore())
                .ForMember(d => d.CanRate, o => o.Ignore());
        }
    }
}
