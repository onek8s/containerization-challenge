using System;
using CommandLine;
using log4net;
using VanArsdel.Integration.BindingTool.Commands;

namespace VanArsdel.Integration.BindingTool
{
    /// <summary>
    /// b2bdeploy.exe, called by the deployment scripts (deploy\scripts) and by hand:
    ///   b2bdeploy export-settings --workbook SettingsFileGenerator.xml --out EnvironmentSettings
    ///   b2bdeploy expand --template PortBindingsMaster.xml --settings EnvironmentSettings\PROD_settings.xml --out PortBindings.PROD.xml
    ///   b2bdeploy inspect --bindings PortBindings.PROD.xml
    ///   b2bdeploy sso --settings EnvironmentSettings\PROD_settings.xml --secrets secrets.json [--dry-run]
    ///   b2bdeploy parties --partners partners.json --server BTSSQL01 [--dry-run]
    /// </summary>
    internal static class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static int Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            try
            {
                return Parser.Default.ParseArguments<ExportSettingsOptions, ExpandOptions, InspectOptions, SsoOptions, PartiesOptions>(args)
                    .MapResult(
                        (ExportSettingsOptions o) => ExportSettingsCommand.Run(o, Console.Out),
                        (ExpandOptions o) => ExpandCommand.Run(o, Console.Out),
                        (InspectOptions o) => InspectCommand.Run(o, Console.Out),
                        (SsoOptions o) => SsoCommand.Run(o, Console.Out),
                        (PartiesOptions o) => PartiesCommand.Run(o, Console.Out),
                        errors => 2);
            }
            catch (Exception ex)
            {
                Log.Error("b2bdeploy failed", ex);
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }
    }
}
