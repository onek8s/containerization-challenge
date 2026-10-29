using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.Documents.Letters
{
    /// <summary>
    /// Letters are HTML files in the template folder (TemplateCode.html) with {{Field}} placeholders.
    /// The common header (logo, address block, date, reference) is in _Layout.html.
    /// </summary>
    public class LetterComposer : ILetterComposer
    {
        private static readonly Regex Placeholder = new Regex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);
        private readonly string _folder;

        public LetterComposer(IOptions<DocumentsOptions> options)
        {
            var configured = options.Value.TemplateFolder;
            _folder = Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
        }

        public IEnumerable<string> TemplateCodes =>
            Directory.GetFiles(_folder, "*.html").Select(Path.GetFileNameWithoutExtension).Where(n => !n.StartsWith("_")).OrderBy(n => n);

        public string Compose(LetterModel model)
        {
            var file = Path.Combine(_folder, model.TemplateCode + ".html");
            if (!File.Exists(file)) throw new FileNotFoundException("Letter template not found: " + model.TemplateCode, file);

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Fields ending in "Html" are pre-built markup (e.g. invoice tables) and are not encoded.
            foreach (var kv in model.Fields)
                fields[kv.Key] = kv.Key.EndsWith("Html", StringComparison.Ordinal) ? kv.Value ?? "" : WebUtility.HtmlEncode(kv.Value ?? "");
            fields["RecipientName"] = WebUtility.HtmlEncode(model.RecipientName ?? "");
            fields["Date"] = model.Date.ToString("d MMMM yyyy");
            fields["Reference"] = WebUtility.HtmlEncode(model.Reference ?? "");
            fields["AddressBlock"] = string.Join("<br/>", model.AddressLines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(WebUtility.HtmlEncode));
            fields["BodyClass"] = model.LargePrint ? "large-print" : "standard";

            var body = Merge(File.ReadAllText(file, Encoding.UTF8), fields);
            fields["Content"] = body;
            var layoutFile = Path.Combine(_folder, "_Layout.html");
            return File.Exists(layoutFile) ? Merge(File.ReadAllText(layoutFile, Encoding.UTF8), fields) : body;
        }

        private static string Merge(string template, IDictionary<string, string> fields)
        {
            return Placeholder.Replace(template, m => fields.TryGetValue(m.Groups[1].Value, out var v) ? v : "");
        }
    }
}
