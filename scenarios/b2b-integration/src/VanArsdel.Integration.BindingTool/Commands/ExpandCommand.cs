using System.IO;
using System.Text;
using VanArsdel.Integration.Deployment;

namespace VanArsdel.Integration.BindingTool.Commands
{
    internal static class ExpandCommand
    {
        public static int Run(ExpandOptions o, TextWriter output)
        {
            var settings = SettingsFile.Load(o.Settings);
            var result = TemplateExpander.Expand(File.ReadAllText(o.Template), settings);
            if (result.MissingTokens.Count > 0)
            {
                output.WriteLine("Missing settings: " + string.Join(", ", result.MissingTokens));
                return 3;
            }

            // BizTalk's import wizard and BTSTask expect UTF-16 bindings like the ones it exports
            File.WriteAllText(o.Output, result.Text, new UnicodeEncoding(false, true));
            output.WriteLine("Wrote " + o.Output);
            return 0;
        }
    }
}
