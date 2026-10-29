using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Documents;
using Proseware.ControlTower.Documents.Pdf;
using Proseware.ControlTower.Domain.Documents;
using Xunit;

namespace Proseware.ControlTower.Tests.Documents;

public class DocumentTemplateTests
{
    private sealed class NoPdf : IHtmlToPdfConverter
    {
        public byte[] Convert(string html, string title, bool landscape = false) => throw new NotSupportedException();
    }

    private static RazorShipmentDocumentRenderer Renderer() =>
        new(new NoPdf(), Options.Create(new DocumentOptions { TrackingUrl = "https://track.example/t/{number}" }), NullLogger<RazorShipmentDocumentRenderer>.Instance);

    [Theory]
    [InlineData(DocumentKind.Cmr, "CMR consignment note")]
    [InlineData(DocumentKind.DeliveryNote, "Delivery note")]
    [InlineData(DocumentKind.DangerousGoodsNote, "Dangerous goods note")]
    [InlineData(DocumentKind.HouseBillOfLading, "House bill of lading")]
    [InlineData(DocumentKind.HouseAirWaybill, "House air waybill")]
    public async Task Templates_render_the_shipment(DocumentKind kind, string title)
    {
        var s = DocumentTestData.ReeferShipmentWithDangerousGoods();
        var html = await Renderer().RenderHtmlAsync(s, kind, new DateTimeOffset(2024, 6, 10, 8, 0, 0, TimeSpan.Zero));

        Assert.Contains(title, html);
        Assert.Contains(s.Number, html);
        Assert.Contains(s.Packages[0].Sscc, html);
        Assert.Contains("data:image/png;base64,", html);
    }

    [Fact]
    public async Task Cmr_lists_dangerous_goods_and_temperature()
    {
        var s = DocumentTestData.ReeferShipmentWithDangerousGoods();
        var html = await Renderer().RenderHtmlAsync(s, DocumentKind.Cmr, DateTimeOffset.UtcNow);

        Assert.Contains("UN 1263 PAINT", html);
        Assert.Contains("Temperature controlled", html);
        Assert.Contains("Nordic Linehaul Oy", html);
    }

    [Fact]
    public void Other_kinds_are_not_rendered()
    {
        Assert.False(Renderer().Supports(DocumentKind.ProofOfDelivery));
        Assert.True(Renderer().Supports(DocumentKind.Cmr));
    }
}
