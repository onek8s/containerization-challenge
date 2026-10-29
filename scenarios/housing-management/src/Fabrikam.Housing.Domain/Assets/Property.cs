using System.Collections.Generic;
using Fabrikam.Housing.Common;

namespace Fabrikam.Housing.Assets
{
    public enum PropertyType
    {
        House = 1,
        Flat = 2,
        Bungalow = 3,
        Maisonette = 4,
        Bedsit = 5
    }

    public enum PropertyStatus
    {
        Let = 1,
        Void = 2,
        Disposed = 3
    }

    public class Estate : Entity
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string PatchCode { get; set; }
        public bool HasCommunalAreas { get; set; }
        public bool HasLift { get; set; }
        public List<Property> Properties { get; set; } = new List<Property>();
    }

    public class Patch : Entity
    {
        public string Code { get; set; }
        public string Name { get; set; }
        /// <summary>Staff login (CORP\user) of the housing officer for the patch.</summary>
        public string OfficerLogin { get; set; }
        public string OfficerName { get; set; }
        public string OfficerEmail { get; set; }
    }

    public class Property : AuditedEntity
    {
        /// <summary>Unique Property Reference Number from the local land and property gazetteer.</summary>
        public long Uprn { get; set; }
        public string Reference { get; set; }
        public int EstateId { get; set; }
        public Estate Estate { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string Town { get; set; }
        public string Postcode { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string PatchCode { get; set; }
        public PropertyType Type { get; set; }
        public int Bedrooms { get; set; }
        public int? YearBuilt { get; set; }
        public PropertyStatus Status { get; set; }
        public bool HasGasSupply { get; set; }
        public bool HasSmokeAlarms { get; set; }
        public bool AsbestosPresent { get; set; }
        public decimal WeeklyNetRent { get; set; }
        public decimal WeeklyServiceCharge { get; set; }
        public decimal WeeklyWaterCharge { get; set; }

        public decimal WeeklyTotalCharge => WeeklyNetRent + WeeklyServiceCharge + WeeklyWaterCharge;

        public string SingleLineAddress =>
            string.Join(", ", new[] { AddressLine1, AddressLine2, Town, Postcode }).Replace(", ,", ",").Trim(',', ' ');
    }
}
