using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lucerne.ProjectSites.Contracts.Hr
{
    /// <summary>Employee record of the HR system REST API (hr01, /api/v2/employees).</summary>
    public class HrEmployee
    {
        [JsonProperty("personnelNumber")] public string PersonnelNumber { get; set; }
        [JsonProperty("firstName")] public string FirstName { get; set; }
        [JsonProperty("lastName")] public string LastName { get; set; }
        [JsonProperty("windowsAccount")] public string WindowsAccount { get; set; }
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("location")] public string Location { get; set; }
        [JsonProperty("costCentre")] public string CostCentre { get; set; }
        [JsonProperty("jobTitle")] public string JobTitle { get; set; }
        [JsonProperty("grade")] public string Grade { get; set; }
        [JsonProperty("managerPersonnelNumber")] public string ManagerPersonnelNumber { get; set; }
        [JsonProperty("employmentStatus")] public string EmploymentStatus { get; set; }
        [JsonProperty("leavingDate")] public DateTime? LeavingDate { get; set; }
        [JsonProperty("lastModified")] public DateTime LastModified { get; set; }
    }

    public class HrEmployeePage
    {
        [JsonProperty("items")] public List<HrEmployee> Items { get; set; }
        [JsonProperty("page")] public int Page { get; set; }
        [JsonProperty("pageSize")] public int PageSize { get; set; }
        [JsonProperty("total")] public int Total { get; set; }
    }
}
