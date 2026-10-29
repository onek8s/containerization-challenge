using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Lucerne.ProjectSites.Contracts.Erp;
using Lucerne.ProjectSites.Contracts.Hr;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.LabStubs.Seed
{
    public class SeedProject
    {
        public ErpProject Erp { get; set; }
        public List<string> TeamEmployeeNos { get; set; } = new List<string>();
        public List<string> FormerTeamEmployeeNos { get; set; } = new List<string>();

        /// <summary>Health profile used to generate site content: 0 healthy, 1 slipping, 2 troubled.</summary>
        public int Profile { get; set; }
    }

    /// <summary>
    /// Deterministic test data: staff (HR), projects and timesheets (ERP), and the content PMs put on project
    /// sites (deliverables, risks, RFIs, actions, documents). Dates are relative to "today".
    /// </summary>
    public class SeedData
    {
        private static readonly string[] FirstNames =
        {
            "Anna", "Beat", "Claudia", "Daniel", "Elena", "Fabian", "Gabriela", "Hans", "Isabelle", "Jonas", "Katrin", "Lukas",
            "Martina", "Nicolas", "Olivia", "Patrick", "Regula", "Stefan", "Tanja", "Urs", "Vera", "Werner", "Yvonne", "Marco",
            "Sophie", "Thomas", "Laura", "Reto", "Sandra", "Matthias", "Julia", "Andreas", "Nadine", "Florian", "Corinne", "Simon"
        };

        private static readonly string[] LastNames =
        {
            "Meier", "Keller", "Brunner", "Huber", "Fischer", "Gerber", "Baumann", "Frei", "Moser", "Widmer", "Steiner", "Zimmermann",
            "Graf", "Roth", "Berger", "Favre", "Rochat", "Bachmann", "Wyss", "Kunz", "Lehmann", "Schmid", "Hofer", "Egger",
            "Leclerc", "Bianchi", "Gruber", "Wagner", "Bauer", "Pichler", "Maurer", "Suter"
        };

        private static readonly string[] Grades = { "E1", "E2", "E3", "E4", "E5", "PL", "DIR" };

        private readonly Random _random = new Random(2019);

        public SeedData(DateTime today)
        {
            Today = today.Date;
            Employees = CreateEmployees();
            Projects = CreateProjects();
            Timesheets = CreateTimesheets();
        }

        public DateTime Today { get; }

        /// <summary>The lab clock: late afternoon of "today", in UTC.</summary>
        public DateTime NowUtc => DateTime.SpecifyKind(Today.AddHours(17), DateTimeKind.Utc);
        public List<HrEmployee> Employees { get; }
        public List<SeedProject> Projects { get; }
        public List<ErpTimesheetLine> Timesheets { get; }

        public static string OfficeDigit(string office) => (Office.All.ToList().FindIndex(o => o.Code == office) + 1).ToString(CultureInfo.InvariantCulture);

        private List<HrEmployee> CreateEmployees()
        {
            var list = new List<HrEmployee>();
            var disciplines = new[] { "STR", "STR", "STR", "CIV", "CIV", "GEO", "MEP", "MEP", "ELE", "ENV", "FIR", "PMG" };
            var used = new HashSet<string>();
            for (var i = 0; i < 84; i++)
            {
                var first = FirstNames[(i * 7 + 3) % FirstNames.Length];
                var last = LastNames[(i * 11 + 5) % LastNames.Length];
                var account = (first.Substring(0, 1) + last).ToLowerInvariant().Replace("ü", "ue");
                while (!used.Add(account))
                {
                    account += "2";
                }

                var office = Office.All[i % Office.All.Count].Code;
                var discipline = disciplines[(i * 5) % disciplines.Length];
                var grade = i < 8 ? "DIR" : i < 24 ? "PL" : Grades[i % 5];
                var digit = Array.IndexOf(new[] { "STR", "CIV", "GEO", "MEP", "ELE", "ENV", "FIR", "PMG" }, discipline) + 1;
                var status = "Active";
                DateTime? leaving = null;
                if (i == 70 || i == 71)
                {
                    status = "Left";
                    leaving = Today.AddDays(-40 - i);
                }
                else if (i == 72)
                {
                    status = "Notice";
                    leaving = Today.AddDays(-2);
                }

                list.Add(new HrEmployee
                {
                    PersonnelNumber = (10100 + i).ToString(CultureInfo.InvariantCulture),
                    FirstName = first,
                    LastName = last,
                    WindowsAccount = i >= 80 ? null : "CORP\\" + account,
                    Email = account + "@lucerne-eng.example",
                    Location = office,
                    CostCentre = OfficeDigit(office) + digit + "00",
                    JobTitle = grade == "DIR" ? "Director" : grade == "PL" ? "Project Leader" : discipline + " Engineer",
                    Grade = grade,
                    ManagerPersonnelNumber = i < 8 ? null : (10100 + i % 8).ToString(CultureInfo.InvariantCulture),
                    EmploymentStatus = i >= 80 ? "Contractor" : status,
                    LeavingDate = leaving,
                    LastModified = NowUtc.AddDays(-(i % 30)).AddMinutes(-i)
                });
            }

            return list;
        }

        private List<SeedProject> CreateProjects()
        {
            var specs = new[]
            {
                // number, name, client, office, sector, status, disciplines, budget, value, currency, complete, profile, restricted
                new object[] { "P17044", "Rotsee Rowing Centre Extension", "Rotsee Sports Foundation", "LUZ", "Buildings", 20, "STR,MEP,FIR", 5200m, 1450000m, "CHF", 82m, 0, false },
                new object[] { "P17112", "Kriens Interchange Bridges", "Central Corridor Road Authority", "LUZ", "Bridges", 20, "STR,CIV,GEO,ENV", 18400m, 6200000m, "CHF", 64m, 2, false },
                new object[] { "P18007", "Oerlikon Office Tower Retrofit", "Northgate Property Partners AG", "ZRH", "Buildings", 20, "STR,MEP,ELE,FIR", 9600m, 2800000m, "CHF", 47m, 1, false },
                new object[] { "P18031", "Limmat Valley Rail Viaduct", "Limmat Rail Infrastructure Co.", "ZRH", "Rail", 20, "STR,CIV,GEO", 14200m, 4900000m, "CHF", 38m, 0, false },
                new object[] { "P18058", "Rhine Port Quay Wall Renewal", "Upper Rhine Port Operators", "BSL", "Water", 20, "CIV,GEO,ENV", 7800m, 2100000m, "CHF", 71m, 1, false },
                new object[] { "P18090", "University Hospital Lab Building", "Riverside Health Trust", "BSL", "Healthcare", 20, "STR,MEP,ELE,FIR", 12800m, 3900000m, "CHF", 23m, 0, true },
                new object[] { "P18115", "Bern Wankdorf Data Centre", "Confidential client", "BRN", "Industrial", 20, "STR,MEP,ELE", 6400m, 1800000m, "CHF", 55m, 2, true },
                new object[] { "P18140", "Lake Geneva Pumping Station", "Lakeshore Water Utility", "GVA", "Water", 20, "CIV,MEP,ELE,ENV", 5600m, 1600000m, "CHF", 61m, 0, false },
                new object[] { "P19002", "Munich Riem Logistics Hub", "Riem Freight Park GmbH", "MUC", "Industrial", 20, "STR,CIV", 4800m, 980000m, "EUR", 18m, 0, false },
                new object[] { "P19017", "Stuttgart Station Canopy Review", "Neckar Rail Projects GmbH", "STR", "Rail", 20, "STR,FIR", 3100m, 720000m, "EUR", 44m, 1, false },
                new object[] { "P19033", "Vienna Danube Footbridge", "Danube Crossings Agency", "VIE", "Bridges", 20, "STR,CIV,GEO", 4200m, 1100000m, "EUR", 9m, 0, false },
                new object[] { "P18072", "Zug Solar Park Substations", "Zugerberg Solar Cooperative", "ZRH", "Energy", 30, "ELE,CIV", 2600m, 540000m, "CHF", 35m, 1, false },
                new object[] { "P17089", "Emmen Depot Refurbishment", "Emmen Estates Office", "LUZ", "Buildings", 40, "STR,MEP", 3900m, 860000m, "CHF", 97m, 0, false },
                new object[] { "P16051", "Sursee Water Treatment Upgrade", "Sursee Water Board", "LUZ", "Water", 50, "CIV,MEP,ENV", 6100m, 1300000m, "CHF", 100m, 0, false },
                new object[] { "P19041", "Basel Exhibition Hall 3 Roof", "Basel Exhibition Halls Co.", "BSL", "Buildings", 10, "STR,FIR", 2800m, 650000m, "CHF", 0m, 0, false },
                new object[] { "P19046", "Geneva Airport Pier Study", "Lakeside Airport Holdings", "GVA", "Buildings", 90, "STR,MEP", 900m, 120000m, "CHF", 0m, 0, false }
            };

            var active = Employees.Where(e => e.EmploymentStatus == "Active" && e.WindowsAccount != null).ToList();
            var projects = new List<SeedProject>();
            var n = 0;
            foreach (var s in specs)
            {
                var office = (string)s[3];
                var local = active.Where(e => e.Location == office).ToList();
                var manager = local.FirstOrDefault(e => e.Grade == "PL") ?? local.First();
                var director = active.First(e => e.Grade == "DIR" && (e.Location == office || n % 2 == 0));
                var status = (int)s[5];
                var start = Today.AddMonths(-(30 - n * 2)).AddDays(-n * 3);
                var project = new SeedProject
                {
                    Profile = (int)s[11],
                    Erp = new ErpProject
                    {
                        ProjectNo = (string)s[0],
                        Description = (string)s[1],
                        CustomerName = (string)s[2],
                        CustomerNo = "C" + (40000 + n * 17).ToString(CultureInfo.InvariantCulture),
                        ProfitCentre = office + "-2" + (n % 9).ToString(CultureInfo.InvariantCulture) + "0",
                        MarketSector = (string)s[4],
                        StatusCode = status,
                        ManagerEmployeeNo = manager.PersonnelNumber,
                        DirectorEmployeeNo = director.PersonnelNumber,
                        StartDate = start,
                        EndDate = start.AddMonths(30),
                        ClosedDate = status == 50 ? Today.AddYears(-2).AddMonths(-3) : (DateTime?)null,
                        BudgetHours = (decimal)s[7],
                        ContractValue = (decimal)s[8],
                        CurrencyCode = (string)s[9],
                        PercentComplete = (decimal)s[10],
                        Restricted = (bool)s[12],
                        DisciplineCodes = (string)s[6],
                        LastChangedUtc = NowUtc.AddHours(-(n * 5 + 1))
                    }
                };

                var disciplines = ((string)s[6]).Split(',');
                var pool = active.Where(e => e.PersonnelNumber != manager.PersonnelNumber && e.Grade != "DIR")
                    .OrderBy(e => e.Location == office ? 0 : 1).ThenBy(e => (int.Parse(e.PersonnelNumber) * 31 + n) % 97)
                    .ToList();
                project.TeamEmployeeNos.Add(manager.PersonnelNumber);
                project.TeamEmployeeNos.AddRange(pool.Take(4 + n % 5).Select(e => e.PersonnelNumber));
                project.FormerTeamEmployeeNos.AddRange(pool.Skip(10).Take(2).Select(e => e.PersonnelNumber));

                // Leaver still booked on one project
                if (n == 1)
                {
                    project.TeamEmployeeNos.Add(Employees.First(e => e.EmploymentStatus == "Left").PersonnelNumber);
                }

                projects.Add(project);
                n++;
            }

            return projects;
        }

        private List<ErpTimesheetLine> CreateTimesheets()
        {
            var lines = new List<ErpTimesheetLine>();
            long id = 900000;
            var monday = IsoWeek.FromDate(Today).Monday;
            foreach (var p in Projects.Where(x => x.Erp.StatusCode >= 20 && x.Erp.StatusCode <= 40))
            {
                var heavy = p.Profile == 2 ? 1.6m : p.Profile == 1 ? 1.2m : 0.8m;
                for (var w = 0; w < 14; w++)
                {
                    var weekMonday = monday.AddDays(-7 * w);
                    var bookers = w >= 10 ? p.FormerTeamEmployeeNos : p.TeamEmployeeNos;
                    foreach (var emp in bookers)
                    {
                        for (var d = 0; d < 5; d++)
                        {
                            var day = weekMonday.AddDays(d);
                            if (day > Today || _random.Next(10) < 5)
                            {
                                continue;
                            }

                            lines.Add(new ErpTimesheetLine
                            {
                                LineId = id++,
                                EmployeeNo = emp,
                                ProjectNo = p.Erp.ProjectNo,
                                ActivityCode = d == 4 ? "MEET" : "DSGN",
                                WorkDate = day,
                                Quantity = Math.Round((2 + _random.Next(7)) * heavy * 2, MidpointRounding.AwayFromZero) / 2,
                                ApprovalState = w == 0 && d >= 3 ? "Submitted" : "Approved"
                            });
                        }
                    }
                }
            }

            return lines;
        }

        /// <summary>Hours booked before the 14 weeks of detail lines (so budget burn looks like a real project).</summary>
        public decimal HistoricHours(SeedProject p)
        {
            var target = p.Erp.BudgetHours * p.Erp.PercentComplete / 100m * (p.Profile == 2 ? 1.35m : p.Profile == 1 ? 1.12m : 0.9m);
            var recent = Timesheets.Where(t => t.ProjectNo == p.Erp.ProjectNo).Sum(t => t.Quantity);
            return Math.Max(0, Math.Round(target - recent));
        }

        public static byte[] FakeFile(string title, string number, string revision)
        {
            return Encoding.UTF8.GetBytes("%PDF-1.4\n% Lucerne Engineering lab file\n% " + number + " rev " + revision + "\n% " + title + "\n%%EOF\n");
        }
    }
}
