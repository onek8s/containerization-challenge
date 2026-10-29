using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tailspin.CustomerCare.Domain
{
    public static class KnowledgeText
    {
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex Unsafe = new Regex(@"<\s*(script|iframe|object|embed|style)[^>]*>.*?<\s*/\s*\1\s*>|\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)|javascript:",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static string Slugify(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            var normalized = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            var dash = false;
            foreach (var ch in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;

                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                    dash = false;
                }
                else if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }

            var slug = sb.ToString().TrimEnd('-');
            return slug.Length > 80 ? slug.Substring(0, 80).TrimEnd('-') : slug;
        }

        public static string PlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var text = Tags.Replace(html, " ");
            text = WebUtility.HtmlDecode(text);
            return Spaces.Replace(text, " ").Trim();
        }

        public static string Excerpt(string html, int maxLength)
        {
            var text = PlainText(html);
            if (text.Length <= maxLength) return text;
            var cut = text.LastIndexOf(' ', maxLength);
            if (cut < maxLength / 2) cut = maxLength;
            return text.Substring(0, cut).TrimEnd() + "…";
        }

        /// <summary>Removes script blocks, event handler attributes and javascript: URLs from article HTML.</summary>
        public static string Sanitize(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            string previous;
            do
            {
                previous = html;
                html = Unsafe.Replace(html, string.Empty);
            }
            while (html != previous);
            return html;
        }

        public static IList<string> SplitTags(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags)) return new List<string>();
            return tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length > 0)
                .Distinct()
                .ToList();
        }

        /// <summary>Simple relevance score used to order knowledge base search results.</summary>
        public static int Score(KbArticle article, string query)
        {
            if (article == null || string.IsNullOrWhiteSpace(query)) return 0;

            var terms = query.ToLowerInvariant().Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            var title = (article.Title ?? string.Empty).ToLowerInvariant();
            var body = PlainText(article.Body).ToLowerInvariant();
            var tags = SplitTags(article.Tags);

            var score = 0;
            foreach (var t in terms)
            {
                if (title.Contains(t)) score += 10;
                if (tags.Contains(t)) score += 5;
                var idx = 0;
                var hits = 0;
                while ((idx = body.IndexOf(t, idx, StringComparison.Ordinal)) >= 0 && hits < 5)
                {
                    hits++;
                    idx += t.Length;
                }

                score += hits;
            }

            return score;
        }
    }
}
