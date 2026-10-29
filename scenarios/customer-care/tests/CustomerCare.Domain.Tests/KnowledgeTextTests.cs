using System;
using Tailspin.CustomerCare.Domain;
using Xunit;

namespace Tailspin.CustomerCare.Domain.Tests
{
    public class KnowledgeTextTests
    {
        [Theory]
        [InlineData("How do I read my meter?", "how-do-i-read-my-meter")]
        [InlineData("  Sähkökatko — mitä tehdä  ", "sahkokatko-mita-tehda")]
        [InlineData("", "")]
        public void Slugify(string title, string expected)
        {
            Assert.Equal(expected, KnowledgeText.Slugify(title));
        }

        [Fact]
        public void Slug_is_limited()
        {
            Assert.True(KnowledgeText.Slugify(new string('a', 120)).Length <= 80);
        }

        [Fact]
        public void Plain_text_and_excerpt()
        {
            Assert.Equal("Hello & world", KnowledgeText.PlainText("<p>Hello &amp;</p>\n<b>world</b>"));
            Assert.Equal("one two…", KnowledgeText.Excerpt("<p>one two three four</p>", 9));
        }

        [Fact]
        public void Sanitize_removes_scripts_and_handlers()
        {
            var s = KnowledgeText.Sanitize("<p onclick=\"x()\">Hi</p><script>alert(1)</script><a href=\"javascript:x\">l</a>");
            Assert.DoesNotContain("script", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onclick", s);
            Assert.Contains("<p>Hi</p>", s);
        }

        [Fact]
        public void Score_prefers_title_matches()
        {
            var a = new KbArticle { Title = "Meter reading", Body = "<p>Read the meter monthly.</p>", Tags = "meter" };
            var b = new KbArticle { Title = "Moving house", Body = "<p>Give a final meter reading.</p>" };
            Assert.True(KnowledgeText.Score(a, "meter") > KnowledgeText.Score(b, "meter"));
            Assert.Equal(0, KnowledgeText.Score(b, "invoice"));
        }

        [Fact]
        public void Split_tags()
        {
            Assert.Equal(new[] { "billing", "meter" }, KnowledgeText.SplitTags("Billing; meter, billing"));
        }
    }
}
