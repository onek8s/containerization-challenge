using CommandLine;

namespace VanArsdel.Integration.BindingTool.Commands
{
    [Verb("export-settings", HelpText = "Export one settings XML per environment from the settings workbook (SpreadsheetML).")]
    public class ExportSettingsOptions
    {
        [Option("workbook", Required = true, HelpText = "SettingsFileGenerator.xml")]
        public string Workbook { get; set; }

        [Option("out", Required = true, HelpText = "Output folder")]
        public string OutputFolder { get; set; }
    }

    [Verb("expand", HelpText = "Replace ${Setting} tokens in a bindings template with the values of one environment.")]
    public class ExpandOptions
    {
        [Option("template", Required = true)]
        public string Template { get; set; }

        [Option("settings", Required = true, HelpText = "<ENV>_settings.xml written by export-settings")]
        public string Settings { get; set; }

        [Option("out", Required = true)]
        public string Output { get; set; }
    }

    [Verb("inspect", HelpText = "List the endpoints of a bindings file and report problems (dev paths, missing hosts, disabled tracking).")]
    public class InspectOptions
    {
        [Option("bindings", Required = true)]
        public string Bindings { get; set; }

        [Option("environment", Default = "PROD")]
        public string Environment { get; set; }
    }

    [Verb("sso", HelpText = "Create or update the SSO configuration store application and write the settings.")]
    public class SsoOptions
    {
        [Option("settings", Required = true)]
        public string Settings { get; set; }

        [Option("secrets", HelpText = "JSON object with the secret values (from the password vault export, deleted after the run)")]
        public string Secrets { get; set; }

        [Option("user-group", Default = @"CORP\BizTalk Application Users")]
        public string UserGroup { get; set; }

        [Option("admin-group", Default = @"CORP\BizTalk Server Administrators")]
        public string AdminGroup { get; set; }

        [Option("dry-run")]
        public bool DryRun { get; set; }
    }

    [Verb("parties", HelpText = "Create or update BizTalk parties and their aliases from the partner list.")]
    public class PartiesOptions
    {
        [Option("partners", Required = true, HelpText = "partners.json (same format as samples/seed/partners.json)")]
        public string Partners { get; set; }

        [Option("server", Required = true, HelpText = "SQL Server of the BizTalk management database")]
        public string Server { get; set; }

        [Option("database", Default = "BizTalkMgmtDb")]
        public string Database { get; set; }

        [Option("dry-run")]
        public bool DryRun { get; set; }
    }
}
