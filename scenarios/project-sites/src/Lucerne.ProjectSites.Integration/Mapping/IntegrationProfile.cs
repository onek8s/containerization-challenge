using System;
using System.Linq;
using AutoMapper;
using Lucerne.ProjectSites.Contracts.Erp;
using Lucerne.ProjectSites.Contracts.Hr;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.Integration.Mapping
{
    /// <summary>AutoMapper profile for ERP and HR records. Employee numbers are resolved to logins by the sync engine.</summary>
    public class IntegrationProfile : Profile
    {
        public IntegrationProfile()
        {
            CreateMap<ErpProject, Project>()
                .ForMember(d => d.Number, o => o.MapFrom(s => s.ProjectNo))
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Description))
                .ForMember(d => d.ClientName, o => o.MapFrom(s => s.CustomerName))
                .ForMember(d => d.ClientNumber, o => o.MapFrom(s => s.CustomerNo))
                .ForMember(d => d.OfficeCode, o => o.MapFrom(s => OfficeFromProfitCentre(s.ProfitCentre)))
                .ForMember(d => d.Sector, o => o.MapFrom(s => s.MarketSector))
                .ForMember(d => d.Stage, o => o.MapFrom(s => StageFromStatus(s.StatusCode)))
                .ForMember(d => d.PlannedEndDate, o => o.MapFrom(s => s.EndDate))
                .ForMember(d => d.FeeValue, o => o.MapFrom(s => s.ContractValue))
                .ForMember(d => d.Currency, o => o.MapFrom(s => s.CurrencyCode))
                .ForMember(d => d.Confidential, o => o.MapFrom(s => s.Restricted))
                .ForMember(d => d.ErpModifiedUtc, o => o.MapFrom(s => s.LastChangedUtc))
                .ForMember(d => d.ProjectManagerLogin, o => o.Ignore())
                .ForMember(d => d.ProjectDirectorLogin, o => o.Ignore())
                .ForMember(d => d.SiteUrl, o => o.Ignore())
                .ForMember(d => d.Disciplines, o => o.Ignore())
                .AfterMap((s, d) =>
                {
                    d.Disciplines.Clear();
                    d.Disciplines.AddRange((s.DisciplineCodes ?? string.Empty)
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim().ToUpperInvariant())
                        .Where(x => Discipline.Find(x) != null)
                        .Distinct());
                });

            CreateMap<HrEmployee, StaffMember>()
                .ForMember(d => d.EmployeeId, o => o.MapFrom(s => s.PersonnelNumber))
                .ForMember(d => d.AccountName, o => o.MapFrom(s => string.IsNullOrEmpty(s.WindowsAccount) ? null : s.WindowsAccount.ToLowerInvariant()))
                .ForMember(d => d.DisplayName, o => o.MapFrom(s => s.FirstName + " " + s.LastName))
                .ForMember(d => d.OfficeCode, o => o.MapFrom(s => s.Location))
                .ForMember(d => d.DisciplineCode, o => o.MapFrom(s => DisciplineFromCostCentre(s.CostCentre)))
                .ForMember(d => d.ManagerEmployeeId, o => o.MapFrom(s => s.ManagerPersonnelNumber))
                .ForMember(d => d.Active, o => o.MapFrom(s => s.EmploymentStatus == "Active" || s.EmploymentStatus == "Notice"));
        }

        /// <summary>ERP status codes to project stage.</summary>
        public static ProjectStage StageFromStatus(int code)
        {
            switch (code)
            {
                case 10: return ProjectStage.Bid;
                case 20: return ProjectStage.Active;
                case 30: return ProjectStage.OnHold;
                case 40: return ProjectStage.Closing;
                case 50: return ProjectStage.Closed;
                case 90: return ProjectStage.Lost;
                default: throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown ERP project status");
            }
        }

        /// <summary>Profit centres are office code + discipline digits, e.g. ZRH-210.</summary>
        public static string OfficeFromProfitCentre(string profitCentre)
        {
            if (string.IsNullOrEmpty(profitCentre) || profitCentre.Length < 3)
            {
                return null;
            }

            return profitCentre.Substring(0, 3).ToUpperInvariant();
        }

        /// <summary>HR cost centres: 4-digit, the second digit is the discipline group.</summary>
        public static string DisciplineFromCostCentre(string costCentre)
        {
            if (string.IsNullOrEmpty(costCentre) || costCentre.Length < 2)
            {
                return null;
            }

            switch (costCentre[1])
            {
                case '1': return "STR";
                case '2': return "CIV";
                case '3': return "GEO";
                case '4': return "MEP";
                case '5': return "ELE";
                case '6': return "ENV";
                case '7': return "FIR";
                case '8': return "PMG";
                default: return null;
            }
        }

        public static IMapper CreateMapper()
        {
            return new MapperConfiguration(c => c.AddProfile<IntegrationProfile>()).CreateMapper();
        }
    }
}
