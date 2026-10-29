using AutoMapper;
using Lucerne.Lending.Contracts;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Workflow;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ApplicationRequest, LoanApplication>()
            .ForMember(d => d.Purpose, o => o.MapFrom(s => Enum.Parse<LoanPurpose>(s.Purpose, true)))
            .ForMember(d => d.Channel, o => o.MapFrom(s => Enum.Parse<Channel>(s.Channel, true)))
            .ForMember(d => d.DisbursementIban, o => o.MapFrom(s => string.IsNullOrWhiteSpace(s.DisbursementIban) ? null : Iban.Normalize(s.DisbursementIban)))
            .ForMember(d => d.Applicants, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore());
        CreateMap<ApplicantDto, Applicant>()
            .ForMember(d => d.PersonalIdentityCode, o => o.MapFrom(s => s.PersonalIdentityCode.Trim().ToUpperInvariant()))
            .ForMember(d => d.MaritalStatus, o => o.MapFrom(s => Enum.Parse<MaritalStatus>(s.MaritalStatus, true)))
            .ForMember(d => d.Housing, o => o.MapFrom(s => Enum.Parse<HousingType>(s.Housing, true)))
            .ForMember(d => d.Employment, o => o.MapFrom(s => Enum.Parse<EmploymentType>(s.Employment, true)))
            .ForMember(d => d.EmployedSince, o => o.MapFrom(s => s.EmployedSince.HasValue ? DateOnly.FromDateTime(s.EmployedSince.Value) : (DateOnly?)null))
            .ForMember(d => d.IsMain, o => o.Ignore())
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CoreCustomerNumber, o => o.Ignore());
        CreateMap<OfferRow, OfferDto>();
        CreateMap<DocumentRow, DocumentDto>();
        CreateMap<LoanProduct, ProductDto>()
            .ForMember(d => d.Purposes, o => o.MapFrom(s => s.Purposes.Select(p => p.ToString()).ToArray()))
            .ForMember(d => d.IndicativeRatePercent, o => o.Ignore());
    }
}
