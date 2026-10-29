using System;
using System.IO;
using System.Text;

namespace Fabrikam.Housing.Integration.Simulated
{
    internal static class SimulatorFolder
    {
        public static string Ensure(string root, string sub)
        {
            var path = Path.Combine(root, sub);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");

        public static string Safe(string value)
        {
            var sb = new StringBuilder();
            foreach (var c in value ?? "")
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_' ? c : '_');
            return sb.ToString();
        }

        public static string Append(string url, string name, string value)
        {
            return url + (url.Contains("?") ? "&" : "?") + name + "=" + Uri.EscapeDataString(value);
        }
    }
}
