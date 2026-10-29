using System;
using System.Linq;
using Woodgrove.Statements.Core.Model;
using Woodgrove.Statements.Core.Payments;
using Woodgrove.Statements.Core.Reports;
using Woodgrove.Statements.Core.Security;
using Xunit;

namespace Woodgrove.Statements.Tests
{
    public class PaymentAndKeyTests
    {
        [Theory]
        [InlineData("1234567", 2)]
        [InlineData("70100001", 1)]
        [InlineData("78100003", 1)]
        public void Reference_check_digit(string body, int expected)
        {
            Assert.Equal(expected, BankBarcode.CheckDigit(body));
            Assert.True(BankBarcode.IsValidNationalReference(body + expected));
        }

        [Fact]
        public void Reference_validation_rejects_wrong_digits()
        {
            Assert.False(BankBarcode.IsValidNationalReference("12345678"));
            Assert.False(BankBarcode.IsValidNationalReference("12a4"));
            Assert.Equal("1 23456 78901", BankBarcode.FormatReference("12345678901"));
        }

        [Fact]
        public void Virtual_barcode_version_4_matches_the_bank_specification_example()
        {
            var code = BankBarcode.Virtual("FI79 4405 2020 0360 82", 4883.15m, "86851 62596 19897", new DateTime(2010, 6, 12));
            Assert.Equal("479440520200360820048831500000000868516259619897100612", code);
            Assert.True(BankBarcode.IsValidNationalReference("868516259619897"));
        }

        [Fact]
        public void Virtual_barcode_version_5_with_rf_reference()
        {
            var code = BankBarcode.Virtual("FI58 1017 1000 0001 22", 482.99m, "RF61 7428 4615 4658 8", new DateTime(2012, 1, 31));
            Assert.Equal(54, code.Length);
            Assert.StartsWith("5581017100000012200048299" + "61", code);
            Assert.EndsWith("120131", code);
        }

        [Fact]
        public void Virtual_barcode_rejects_foreign_iban()
        {
            Assert.Throws<ArgumentException>(() => BankBarcode.Virtual("SE45 5000 0000 0583 9825 7466", 10m, "1232", null));
        }

        [Fact]
        public void Code128_patterns_and_checksum()
        {
            Assert.Equal(107, Code128.Patterns.Length);
            Assert.All(Code128.Patterns.Take(106), p => Assert.Equal(11, p.Sum(ch => ch - '0')));
            Assert.Equal(13, Code128.Patterns[106].Sum(ch => ch - '0'));
            Assert.Equal(107, Code128.Patterns.Distinct().Count());
            Assert.Equal(new[] { 105, 0, 2, 106 }, Code128.Codes("00"));
            Assert.Equal(new[] { 105, 12, 34, 82, 106 }, Code128.Codes("1234"));
            Assert.Throws<ArgumentException>(() => Code128.Codes("123"));
            var modules = Code128.Modules("1234");
            Assert.Equal(Code128.QuietZone * 2 + 11 * 4 + 13, modules.Sum());
        }

        [Fact]
        public void Api_keys_hash_and_prefix()
        {
            var key = ApiKey.Generate("ERP");
            Assert.StartsWith("wgs_erp_", key);
            Assert.Equal("erp", ApiKey.Prefix(key));
            Assert.Equal(64, ApiKey.Hash(key).Length);
            Assert.Equal(ApiKey.Hash(key), ApiKey.Hash(key));
            Assert.NotEqual(ApiKey.Hash(key), ApiKey.Hash(ApiKey.Generate("erp")));
            Assert.Null(ApiKey.Prefix("abc"));
            Assert.True(ApiKey.FixedTimeEquals("abc", "abc"));
            Assert.False(ApiKey.FixedTimeEquals("abc", "abd"));
        }

        [Fact]
        public void Api_client_report_permissions()
        {
            var client = new ApiClient { AllowedReports = "INVOICE, statement" };
            Assert.True(client.Allows("INVOICE"));
            Assert.True(client.Allows("STATEMENT"));
            Assert.False(client.Allows("AGED-DEBT"));
            Assert.True(new ApiClient { AllowedReports = "*" }.Allows("PORTFOLIO"));
        }

        [Fact]
        public void Output_formats_and_catalog()
        {
            Assert.Equal(OutputFormat.Excel, OutputFormats.Parse("xlsx"));
            Assert.Equal("EXCELOPENXML", OutputFormats.RenderFormat(OutputFormat.Excel));
            Assert.Throws<FormatException>(() => OutputFormats.Parse("csv"));
            Assert.Equal(ReportDefinitionInfo.All.Count, ReportDefinitionInfo.All.Select(r => r.Code).Distinct().Count());
            Assert.NotNull(ReportDefinitionInfo.Find("aged-debt"));
        }
    }
}
