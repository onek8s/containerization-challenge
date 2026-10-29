using Xunit;

namespace Contoso.Purchasing.Core.Tests
{
    public class RequestTotalsTests
    {
        [Fact]
        public void Totals_are_rounded_per_line()
        {
            var request = TestData.Request(0.335m, 3);
            request.Lines.Add(new RequestLine { LineNo = 2, Description = "Freight", Quantity = 1, UnitPrice = 12.5m, VatRate = 25.5m });

            Assert.Equal(1.01m, request.Lines[0].NetAmount);
            Assert.Equal(0.26m, request.Lines[0].VatAmount);
            Assert.Equal(13.51m, request.NetTotal);
            Assert.Equal(3.45m, request.VatTotal);
            Assert.Equal(16.96m, request.GrossTotal);
        }

        [Fact]
        public void Document_numbers_have_fixed_width()
        {
            Assert.Equal("PR-2026-00042", DocumentNumbers.RequestNumber(2026, 42));
            Assert.Equal("4500001234", DocumentNumbers.OrderNumber(1234));
        }
    }
}
