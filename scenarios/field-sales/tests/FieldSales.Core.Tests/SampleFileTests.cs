using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Wingtip.FieldSales.Core.Tests
{
    public class SampleFileTests
    {
        [Fact]
        public void Sample_price_list_imports_without_errors()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.TextInfo.ListSeparator = ";";

            var path = Path.Combine(FindRoot(), "samples", "pricelist-update.csv");
            using (var reader = new StreamReader(path, Encoding.GetEncoding(1252)))
            {
                var result = new PriceListCsv(culture).Read(reader);
                Assert.Empty(result.Errors);
                Assert.True(result.Products.Count > 50);
                Assert.Contains(result.Products, p => p.ItemNo == "40500" && !p.Active);
            }
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FieldSales.sln"))) dir = dir.Parent;
            return dir.FullName;
        }
    }
}
