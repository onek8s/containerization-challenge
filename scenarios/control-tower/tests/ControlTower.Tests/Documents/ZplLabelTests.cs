using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proseware.ControlTower.Documents.Labels;
using Xunit;

namespace Proseware.ControlTower.Tests.Documents;

public class ZplLabelTests
{
    private static ZplLabelPrinter Printer(string? folder = null)
    {
        var options = new PrinterOptions();
        if (folder is not null) options.Printers["zt-test"] = new PrinterDefinition { Kind = PrinterKind.File, Address = folder };
        return new ZplLabelPrinter(Options.Create(options), NullLogger<ZplLabelPrinter>.Instance);
    }

    [Fact]
    public void Label_is_one_complete_zpl_format()
    {
        var s = DocumentTestData.ReeferShipmentWithDangerousGoods();
        var zpl = Printer().BuildPackageLabel(s, s.Packages[0], 1, 2);

        Assert.StartsWith("^XA", zpl);
        Assert.EndsWith("^XZ\n", zpl);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(zpl, @"\^XA"));
    }

    [Fact]
    public void Sscc_is_encoded_as_gs1_128_with_application_identifier_00()
    {
        var s = DocumentTestData.ReeferShipmentWithDangerousGoods();
        var package = s.Packages[0];
        var zpl = Printer().BuildPackageLabel(s, package, 1, 2);

        Assert.Contains($"^FD>;>800{package.Sscc}^FS", zpl);
        Assert.Contains("^BCN,", zpl);
        Assert.Contains("(00) 3 6430012345 424201 ", zpl);
    }

    [Fact]
    public void Label_shows_route_dangerous_goods_reefer_and_handling()
    {
        var s = DocumentTestData.ReeferShipmentWithDangerousGoods();
        var zpl = Printer().BuildPackageLabel(s, s.Packages[0], 1, 2);

        Assert.Contains("^FDSE12^FS", zpl);          // route code: country + first two postcode characters
        Assert.Contains("UN 1263", zpl);
        Assert.Contains("KEEP 2 - 6 C", zpl);
        Assert.Contains("DO NOT STACK", zpl);
        Assert.Contains("PKG 1 / 2", zpl);
    }

    [Fact]
    public void Field_data_never_contains_command_prefixes()
    {
        var s = DocumentTestData.ReeferShipmentWithDangerousGoods();
        var zpl = Printer().BuildPackageLabel(s, s.Packages[1], 2, 2);

        Assert.Contains("REF PO-77 1", zpl);
        Assert.DoesNotContain("PO-77^1", zpl);
        Assert.DoesNotContain("UN 1263", zpl);
    }

    [Fact]
    public async Task File_printer_writes_the_label_without_byte_order_mark()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ct-zpl-" + Guid.NewGuid().ToString("N"));
        try
        {
            var printer = Printer(folder);
            await printer.PrintAsync("zt-test", "^XA^FDtest^FS^XZ");
            var file = Assert.Single(Directory.GetFiles(folder, "*.zpl"));
            var bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal((byte)'^', bytes[0]);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task Unknown_printer_is_rejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Printer().PrintAsync("zt-nowhere", "^XA^XZ"));
    }

    [Theory]
    [InlineData("zt-vantaa-1.corp.local:9100", "zt-vantaa-1.corp.local", 9100)]
    [InlineData("10.20.4.31", "10.20.4.31", 9100)]
    [InlineData("10.20.4.31:6101", "10.20.4.31", 6101)]
    public void Printer_address_defaults_to_raw_port(string address, string host, int port)
    {
        Assert.Equal((host, port), ZplLabelPrinter.ParseAddress(address));
    }
}
