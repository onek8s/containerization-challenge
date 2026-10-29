using AutoMapper;
using Fabrikam.Housing.Assets;
using Fabrikam.Housing.BackOffice.Models;
using Fabrikam.Housing.Repairs;

namespace Fabrikam.Housing.BackOffice.Mapping
{
    public class BackOfficeProfile : Profile
    {
        public BackOfficeProfile()
        {
            CreateMap<Property, PropertyListItem>()
                .ForMember(d => d.EstateName, o => o.MapFrom(s => s.Estate.Name));
            CreateMap<WorkOrder, WorkOrderListItem>()
                .ForMember(d => d.PropertyAddress, o => o.MapFrom(s => s.Property.AddressLine1))
                .ForMember(d => d.PatchCode, o => o.MapFrom(s => s.Property.PatchCode))
                .ForMember(d => d.ContractorCode, o => o.MapFrom(s => s.Contractor.Code))
                .ForMember(d => d.Overdue, o => o.Ignore());
        }
    }
}
