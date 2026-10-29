using System;
using System.Collections.Generic;
using System.IO;
using File = System.IO.File;
using System.Linq;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Mapping;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Documents;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.SharePoint;
using Lucerne.ProjectSites.SharePoint.Csom;
using Microsoft.SharePoint.Client;
using CamlQuery = Lucerne.ProjectSites.SharePoint.CamlQuery;

namespace Lucerne.ProjectSites.AdminTool
{
    /// <summary>
    /// Administration tool for the SharePoint team, run on an admin workstation with the farm admin account
    /// (Windows credentials, CSOM).
    ///   psadmin sites        -hub URL [-stage Active]
    ///   psadmin register     -hub URL -project P19041 -out file.xlsx
    ///   psadmin portfolio    -hub URL -out file.xlsx
    ///   psadmin permissions  -hub URL -project P19041
    ///   psadmin rollup       -hub URL
    ///   psadmin request-site -hub URL -project P19041
    ///   psadmin reapply      -hub URL -project P19041   (re-activates the project site feature)
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("psadmin sites|register|portfolio|permissions|rollup|request-site|reapply -hub <url> [-project Pnnnnn] [-out file] [-stage Active]");
                return 2;
            }

            var options = Parse(args.Skip(1));
            var hubUrl = Required(options, "hub");
            var connector = new CsomSiteConnector(new WindowsContextFactory());
            var log = new ConsoleLog();

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "sites":
                        return Sites(connector, hubUrl, Get(options, "stage"));
                    case "register":
                        return Register(connector, hubUrl, Required(options, "project"), Required(options, "out"), log);
                    case "portfolio":
                        return Portfolio(connector, hubUrl, Required(options, "out"));
                    case "permissions":
                        return Permissions(hubUrl, connector, Required(options, "project"));
                    case "rollup":
                        var result = new StatusRollupService(connector, hubUrl, new StatusAssessor(), log).Run(DateTime.Today);
                        Console.WriteLine("{0} assessed, {1} failed", result.Statuses.Count, result.Failures.Count);
                        return result.Failures.Count == 0 ? 0 : 1;
                    case "request-site":
                        return RequestSite(connector, hubUrl, Required(options, "project"));
                    case "reapply":
                        return Reapply(connector, hubUrl, Required(options, "project"));
                    default:
                        Console.Error.WriteLine("Unknown command " + args[0]);
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int Sites(ISiteConnector connector, string hubUrl, string stage)
        {
            using (var hub = connector.Open(hubUrl))
            {
                ProjectStage s;
                var stages = stage != null && Enum.TryParse(stage, true, out s) ? new[] { s } : new ProjectStage[0];
                foreach (var p in new HubRepository(hub).GetProjects(stages).Select(x => x.Value))
                {
                    Console.WriteLine("{0,-7} {1,-8} {2,-4} {3,-40} {4}", p.Number, p.Stage, p.OfficeCode, Trim(p.Name, 40), p.SiteUrl ?? "(no site)");
                }
            }

            return 0;
        }

        private static int Register(ISiteConnector connector, string hubUrl, string number, string output, IActivityLog log)
        {
            Project project;
            using (var hub = connector.Open(hubUrl))
            {
                var entry = new HubRepository(hub).FindProject(number);
                if (entry == null || string.IsNullOrEmpty(entry.Value.Value.SiteUrl))
                {
                    Console.Error.WriteLine(number + " has no project site");
                    return 1;
                }

                project = entry.Value.Value;
            }

            var snapshot = new StatusRollupService(connector, hubUrl, null, log).ReadSnapshot(project);
            File.WriteAllBytes(output, Workbooks.DeliverableRegister(number, snapshot.Deliverables, DateTime.Today));
            Console.WriteLine("{0} deliverables written to {1}", snapshot.Deliverables.Count, output);
            return 0;
        }

        private static int Portfolio(ISiteConnector connector, string hubUrl, string output)
        {
            using (var hub = connector.Open(hubUrl))
            {
                var statuses = hub.GetList(ListNames.ProjectStatus).Query(CamlQuery.All()).Select(ListMapper.ToStatus).ToDictionary(s => s.ProjectNumber ?? string.Empty);
                var rows = new HubRepository(hub).GetProjects(ProjectStage.Active, ProjectStage.OnHold, ProjectStage.Closing)
                    .Select(p =>
                    {
                        ProjectStatus s;
                        statuses.TryGetValue(p.Value.Number, out s);
                        return new PortfolioRow { Project = p.Value, Status = s };
                    })
                    .ToList();
                File.WriteAllBytes(output, Workbooks.Portfolio(rows, DateTime.Today));
                Console.WriteLine("{0} projects written to {1}", rows.Count, output);
            }

            return 0;
        }

        /// <summary>Lists unique permissions below the site (broken inheritance is reviewed every quarter).</summary>
        private static int Permissions(string hubUrl, ISiteConnector connector, string number)
        {
            string siteUrl;
            using (var hub = connector.Open(hubUrl))
            {
                var entry = new HubRepository(hub).FindProject(number);
                siteUrl = entry?.Value.SiteUrl;
            }

            if (string.IsNullOrEmpty(siteUrl))
            {
                Console.Error.WriteLine(number + " has no project site");
                return 1;
            }

            using (var ctx = new WindowsContextFactory().Create(siteUrl))
            {
                var web = ctx.Web;
                var lists = web.Lists;
                ctx.Load(web.RoleAssignments, ra => ra.Include(r => r.Member.LoginName, r => r.RoleDefinitionBindings.Include(b => b.Name)));
                ctx.Load(lists, l => l.Include(x => x.Title, x => x.HasUniqueRoleAssignments, x => x.Hidden));
                ctx.ExecuteQuery();

                Console.WriteLine("Site {0}", siteUrl);
                foreach (var ra in web.RoleAssignments)
                {
                    Console.WriteLine("  {0,-50} {1}", ra.Member.LoginName, string.Join(", ", ra.RoleDefinitionBindings.Select(b => b.Name)));
                }

                foreach (var list in lists.Where(l => !l.Hidden && l.HasUniqueRoleAssignments))
                {
                    Console.WriteLine("Unique permissions: {0}", list.Title);
                }
            }

            return 0;
        }

        private static int RequestSite(ISiteConnector connector, string hubUrl, string number)
        {
            if (!Project.IsValidNumber(number))
            {
                Console.Error.WriteLine("Invalid project number " + number);
                return 2;
            }

            using (var hub = connector.Open(hubUrl))
            {
                var id = hub.GetList(ListNames.SiteRequests).Add(new FieldValues()
                    .Set(FieldNames.Title, "Site for " + number)
                    .Set(FieldNames.ProjectNumber, number));
                Console.WriteLine("Site request {0} created; the provisioning job picks it up within 5 minutes.", id);
            }

            return 0;
        }

        private static int Reapply(ISiteConnector connector, string hubUrl, string number)
        {
            string siteUrl;
            using (var hub = connector.Open(hubUrl))
            {
                siteUrl = new HubRepository(hub).FindProject(number)?.Value.SiteUrl;
            }

            using (var ctx = new WindowsContextFactory().Create(siteUrl))
            {
                var feature = new Guid("C5D3F8A4-2B6E-4D9C-9FA7-3A4B5C6D7E83");
                ctx.Web.Features.Remove(feature, true);
                ctx.ExecuteQuery();
                ctx.Web.Features.Add(feature, true, FeatureDefinitionScope.Farm);
                ctx.ExecuteQuery();
                Console.WriteLine("Project site feature re-activated on " + siteUrl);
            }

            return 0;
        }

        private static Dictionary<string, string> Parse(IEnumerable<string> args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string key = null;
            foreach (var a in args)
            {
                if (a.StartsWith("-", StringComparison.Ordinal))
                {
                    key = a.TrimStart('-');
                    result[key] = "true";
                }
                else if (key != null)
                {
                    result[key] = a;
                    key = null;
                }
            }

            return result;
        }

        private static string Get(Dictionary<string, string> options, string key)
        {
            string v;
            return options.TryGetValue(key, out v) ? v : null;
        }

        private static string Required(Dictionary<string, string> options, string key)
        {
            var v = Get(options, key);
            if (string.IsNullOrEmpty(v))
            {
                throw new ArgumentException("-" + key + " is required");
            }

            return v;
        }

        private static string Trim(string s, int n)
        {
            return s == null || s.Length <= n ? s : s.Substring(0, n - 1) + "~";
        }
    }

    internal sealed class ConsoleLog : IActivityLog
    {
        public void Info(string message)
        {
            Console.WriteLine(message);
        }

        public void Warn(string message)
        {
            Console.WriteLine("WARN " + message);
        }

        public void Error(string message, Exception exception)
        {
            Console.Error.WriteLine("ERROR " + message + ": " + exception?.Message);
        }
    }
}
