using System.IO;
using VanArsdel.Integration.Deployment;

namespace VanArsdel.Integration.BindingTool.Commands
{
    internal static class ExportSettingsCommand
    {
        public static int Run(ExportSettingsOptions o, TextWriter output)
        {
            Directory.CreateDirectory(o.OutputFolder);
            foreach (var env in SettingsSpreadsheet.Load(o.Workbook))
            {
                var path = Path.Combine(o.OutputFolder, env.FileName);
                SettingsSpreadsheet.ToSettingsXml(env).Save(path);
                output.WriteLine("{0,-8} {1} settings -> {2}", env.Name, env.Values.Count, path);
            }

            return 0;
        }
    }
}
