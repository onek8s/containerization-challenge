using System.Xml;
using VanArsdel.Integration.Deployment;
using VanArsdel.Integration.LabStubs.Lab;
using Xunit;

namespace VanArsdel.Integration.Tests
{
    public class DeploymentTests
    {
        private static string Deploy(params string[] parts) => Path.Combine(new[] { ScenarioPaths.Root, "deploy" }.Concat(parts).ToArray());

        public static IEnumerable<object[]> Environments() =>
            SettingsSpreadsheet.Load(Deploy("EnvironmentSettings", "SettingsFileGenerator.xml")).Select(e => new object[] { e.Name });

        [Fact]
        public void Workbook_has_the_four_environments_and_the_runtime_sso_settings()
        {
            var envs = SettingsSpreadsheet.Load(Deploy("EnvironmentSettings", "SettingsFileGenerator.xml"));

            Assert.Equal(new[] { "DEV", "LAB", "TEST", "PROD" }, envs.Select(e => e.Name));
            Assert.Equal("PROD_settings.xml", envs[3].FileName);
            var sso = SsoApplicationSettings.FromEnvironment(envs[3], new Dictionary<string, string> { ["ErpClientSecret"] = "x" });
            Assert.Empty(sso.MissingRequired(new[] { "IntegrationDbConnectionString", "ArchiveRoot", "ErpOrderServiceUrl", "ErpClientId", "ErpClientSecret", "SmtpHost", "AlertFromAddress" }));
            Assert.Equal("15", sso.Values["OrderCutOffHour"]);
            Assert.Equal("LABAPP01", envs[1]["SmtpHost"]);
        }

        [Theory]
        [MemberData(nameof(Environments))]
        public void Master_bindings_expand_to_valid_bindings(string environment)
        {
            var env = SettingsSpreadsheet.Load(Deploy("EnvironmentSettings", "SettingsFileGenerator.xml")).Single(e => e.Name == environment);

            var expanded = TemplateExpander.Expand(File.ReadAllText(Deploy("PortBindingsMaster.xml")), env.Values);

            Assert.Empty(expanded.MissingTokens);
            var doc = new XmlDocument();
            doc.LoadXml(expanded.Text);
            var report = BindingsInspector.Inspect(doc);
            Assert.Empty(report.Problems);
            Assert.Equal(6, report.Orchestrations.Count);
            if (environment != "DEV")
            {
                Assert.DoesNotContain(report.Endpoints, e => (e.Address ?? string.Empty).Contains("localhost", StringComparison.OrdinalIgnoreCase));
            }

            var transports = report.Endpoints.Where(e => e.Kind == "ReceiveLocation").ToDictionary(e => e.Name, e => e.TransportType);
            var folders = env["UsePartnerFolders"] == "True";
            Assert.Equal(folders ? "FILE" : "SFTP", transports["rl_Customer_EDI_SFTP_C1002"]);
            Assert.Equal(folders ? "FILE" : "FTP", transports["rl_Customer_EDI_FTP_C2002"]);
            Assert.Equal("HTTP", transports["rl_Customer_EDI_AS2"]);
        }

        [Fact]
        public void Lab_receive_locations_exist_in_the_bindings()
        {
            var env = SettingsSpreadsheet.Load(Deploy("EnvironmentSettings", "SettingsFileGenerator.xml")).Single(e => e.Name == "LAB");
            var doc = new XmlDocument();
            doc.LoadXml(TemplateExpander.Expand(File.ReadAllText(Deploy("PortBindingsMaster.xml")), env.Values).Text);
            var report = BindingsInspector.Inspect(doc);

            foreach (var location in LabPlatform.Locations)
            {
                Assert.Contains(report.Endpoints, e => e.Port == location.Port && e.Pipeline.EndsWith("." + location.Pipeline, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void Every_setting_the_deployment_scripts_read_is_in_the_workbook()
        {
            var envs = SettingsSpreadsheet.Load(Deploy("EnvironmentSettings", "SettingsFileGenerator.xml"));
            var used = Directory.GetFiles(Deploy("scripts"), "*.ps1")
                .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"\$Settings\.(?<name>[A-Za-z0-9_]+)").Select(m => m.Groups["name"].Value))
                .Distinct()
                .ToList();

            Assert.NotEmpty(used);
            foreach (var env in envs)
            {
                Assert.All(used, name => Assert.True(env.Values.ContainsKey(name), env.Name + " has no setting " + name));
            }
        }

        [Fact]
        public void Secrets_the_scripts_read_are_in_the_template()
        {
            var template = File.ReadAllText(Deploy("secrets.template.json"));
            var used = Directory.GetFiles(Deploy("scripts"), "*.ps1")
                .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"Get-B2BSecret \$Secrets '(?<name>[A-Za-z0-9_]+)'").Select(m => m.Groups["name"].Value))
                .Concat(System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Deploy("scripts", "Install-BizTalkApplication.ps1")), @"= '(?<name>[A-Za-z0-9]+_(Sftp|Ftp)Password)'").Select(m => m.Groups["name"].Value))
                .Distinct()
                .ToList();

            Assert.Contains("ErpClientSecret", used);
            Assert.All(used, name => Assert.Contains("\"" + name + "\"", template));
        }

        [Fact]
        public void Clear_text_password_is_reported()
        {
            var doc = new XmlDocument();
            doc.LoadXml("<BindingInfo><SendPortCollection><SendPort Name=\"sp\" IsStatic=\"true\"><PrimaryTransport><Address>ftp://x</Address>"
                + "<TransportTypeData>&lt;CustomProps&gt;&lt;Password vt=\"8\"&gt;secret&lt;/Password&gt;&lt;/CustomProps&gt;</TransportTypeData>"
                + "<SendHandler Name=\"B2BSendHost\" /></PrimaryTransport></SendPort></SendPortCollection></BindingInfo>");

            Assert.Contains(BindingsInspector.Inspect(doc).Problems, p => p.StartsWith("Clear text password", StringComparison.Ordinal));
        }
    }
}
