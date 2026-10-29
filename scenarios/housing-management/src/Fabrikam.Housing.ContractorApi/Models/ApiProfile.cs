using AutoMapper;
using Fabrikam.Housing.Compliance;
using Fabrikam.Housing.Contractors;
using Fabrikam.Housing.Repairs;

namespace Fabrikam.Housing.ContractorApi.Models
{
    public class ApiProfile : Profile
    {
        public ApiProfile()
        {
            CreateMap<ScheduleOfRatesItem, ScheduleOfRatesModel>();
            CreateMap<ContractorInvoice, InvoiceModel>()
                .ForMember(m => m.WorkOrderReference, o => o.MapFrom(i => i.WorkOrder.Reference));
            CreateMap<ComplianceCertificate, CertificateModel>()
                .ForMember(m => m.PropertyReference, o => o.MapFrom(c => c.Property.Reference))
                .ForMember(m => m.RemedialWorkOrder, o => o.Ignore())
                .ForMember(m => m.DocumentKey, o => o.Ignore());
        }
    }
}
