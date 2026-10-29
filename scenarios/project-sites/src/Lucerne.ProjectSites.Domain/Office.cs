using System;
using System.Collections.Generic;
using System.Linq;

namespace Lucerne.ProjectSites.Domain
{
    public sealed class Office
    {
        public Office(string code, string name, string country, string currency)
        {
            Code = code;
            Name = name;
            Country = country;
            Currency = currency;
        }

        public string Code { get; }
        public string Name { get; }
        public string Country { get; }
        public string Currency { get; }

        public static readonly IReadOnlyList<Office> All = new[]
        {
            new Office("LUZ", "Lucerne", "CH", "CHF"),
            new Office("ZRH", "Zurich", "CH", "CHF"),
            new Office("BSL", "Basel", "CH", "CHF"),
            new Office("BRN", "Bern", "CH", "CHF"),
            new Office("GVA", "Geneva", "CH", "CHF"),
            new Office("MUC", "Munich", "DE", "EUR"),
            new Office("STR", "Stuttgart", "DE", "EUR"),
            new Office("VIE", "Vienna", "AT", "EUR")
        };

        public static Office Find(string code)
        {
            return All.FirstOrDefault(o => string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class Discipline
    {
        public Discipline(string code, string name, string libraryName)
        {
            Code = code;
            Name = name;
            LibraryName = libraryName;
        }

        public string Code { get; }
        public string Name { get; }

        /// <summary>Document library created for the discipline on every project site.</summary>
        public string LibraryName { get; }

        public static readonly IReadOnlyList<Discipline> All = new[]
        {
            new Discipline("STR", "Structural", "Structural"),
            new Discipline("CIV", "Civil", "Civil"),
            new Discipline("GEO", "Geotechnical", "Geotechnical"),
            new Discipline("MEP", "Building Services", "Building Services"),
            new Discipline("ELE", "Electrical", "Electrical"),
            new Discipline("ENV", "Environmental", "Environmental"),
            new Discipline("FIR", "Fire Engineering", "Fire Engineering"),
            new Discipline("PMG", "Project Management", "Project Management")
        };

        public static Discipline Find(string code)
        {
            return All.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }
}
