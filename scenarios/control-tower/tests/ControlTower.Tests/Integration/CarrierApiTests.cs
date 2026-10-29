using Proseware.ControlTower.Domain.Customs;
using Proseware.ControlTower.Domain.Shipments;
using Proseware.ControlTower.Integration.Carriers;
using Proseware.ControlTower.Integration.Customs;
using Proseware.ControlTower.Integration.Sftp;
using Xunit;

namespace Proseware.ControlTower.Tests.Integration;

public class CarrierApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parcel_webhook_signature_round_trip()
    {
        var body = Samples.Text("carriers", "parcelnord-webhook-delivered.json");
        var header = ParcelNordClient.SignatureHeader(body, "s3cret", Now);

        Assert.True(ParcelNordClient.VerifySignature(header, body, "s3cret", Now.AddMinutes(1), TimeSpan.FromMinutes(5)));
        Assert.False(ParcelNordClient.VerifySignature(header, body + " ", "s3cret", Now, TimeSpan.FromMinutes(5)));
        Assert.False(ParcelNordClient.VerifySignature(header, body, "other", Now, TimeSpan.FromMinutes(5)));
        Assert.False(ParcelNordClient.VerifySignature(header, body, "s3cret", Now.AddMinutes(10), TimeSpan.FromMinutes(5)));
        Assert.False(ParcelNordClient.VerifySignature("", body, "s3cret", Now, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Parcel_webhook_maps_to_delivered_status()
    {
        var status = ParcelNordClient.ParseWebhook(Samples.Text("carriers", "parcelnord-webhook-delivered.json"));

        Assert.NotNull(status);
        Assert.Equal(MilestoneCodes.Delivered, status!.MilestoneCode);
        Assert.Equal("PF26-0000390", status.ShipmentNumber);
        Assert.Equal("JJFI6430000507", status.CarrierReference);
        Assert.Equal("364300123450039007", status.Sscc);
        Assert.Contains("Signed by A. Korhonen", status.Remarks);
        Assert.Equal("api:PRCL", status.Source);
    }

    [Fact]
    public void Pickup_point_delivery_names_the_point()
    {
        var status = ParcelNordClient.ParseWebhook(Samples.Text("carriers", "parcelnord-webhook-pickup-point.json"));
        Assert.Equal(MilestoneCodes.Delivered, status!.MilestoneCode);
        Assert.Equal("Delivered to pickup point K-Market Hatanpää", status.Remarks);
    }

    [Theory]
    [InlineData("FIHEL-AP", "HEL")]
    [InlineData("CNPVG-AP", "PVG")]
    public void Airport_codes_are_taken_from_locode(string location, string iata) =>
        Assert.Equal(iata, AerofreightClient.Iata(location));

    [Theory]
    [InlineData("*.edi", "IFTSTA_1.edi", true)]
    [InlineData("*.edi", "IFTSTA_1.EDI", true)]
    [InlineData("*.edi", "IFTSTA_1.edi.tmp", false)]
    [InlineData("IFT???_*.txt", "IFTSTA_99.txt", true)]
    public void File_patterns(string pattern, string name, bool match) =>
        Assert.Equal(match, SshNetFileChannel.WildcardToRegex(pattern).IsMatch(name));

    [Fact]
    public void Host_key_fingerprints_in_md5_and_sha256_form()
    {
        var key = new byte[] { 1, 2, 3, 4, 5 };
        var md5 = System.Security.Cryptography.MD5.HashData(key);
        var sha = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(key)).TrimEnd('=');

        Assert.True(SshNetFileChannel.MatchesFingerprint("SHA256:" + sha, key, md5));
        Assert.True(SshNetFileChannel.MatchesFingerprint("MD5:" + string.Join(":", md5.Select(b => b.ToString("x2"))), key, md5));
        Assert.True(SshNetFileChannel.MatchesFingerprint(string.Join("-", md5.Select(b => b.ToString("X2"))), key, md5));
        Assert.False(SshNetFileChannel.MatchesFingerprint("SHA256:AAAA", key, md5));
    }

    [Fact]
    public void Local_folder_channel_uploads_through_tmp_and_archives()
    {
        var root = Directory.CreateTempSubdirectory("ct-partner-").FullName;
        try
        {
            using var channel = new LocalFolderFileChannel(new SftpOptions { LocalFolder = root });
            File.WriteAllText(Path.Combine(root, "out", "IFTSTA_1.edi"), "x");
            File.WriteAllText(Path.Combine(root, "out", "notes.txt"), "x");

            var files = channel.ListInbound();
            Assert.Single(files);
            channel.Archive(files[0]);
            channel.Upload("IFTMIN_1.edi", new byte[] { 65 });

            Assert.True(File.Exists(Path.Combine(root, "out", "archive", "IFTSTA_1.edi")));
            Assert.True(File.Exists(Path.Combine(root, "in", "IFTMIN_1.edi")));
            Assert.False(File.Exists(Path.Combine(root, "in", "IFTMIN_1.edi.tmp")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Declaration_maps_to_customs_request()
    {
        var shipment = TestData.ReeferShipment(Now);
        var declaration = new CustomsDeclaration
        {
            Type = DeclarationType.Export,
            Lrn = "PF2610290000003901",
            DeclarantEori = "FI01234567",
            ConsignorEori = "FI24871234",
            OfficeOfDeclaration = "FI004400",
            OfficeOfExit = "SE600001"
        };
        declaration.Lines.Add(new DeclarationLine { LineNumber = 2, CommodityCode = "04012099", Description = "Milk", OriginCountry = "FI", NetMassKg = 900, GrossMassKg = 1000.5m, StatisticalValue = 4200, ProcedureCode = "1000", Packages = 2, PackageKindCode = "PX" });
        declaration.Lines.Add(new DeclarationLine { LineNumber = 1, CommodityCode = "04061050", Description = "Cheese", OriginCountry = "FI", NetMassKg = 10, GrossMassKg = 11, StatisticalValue = 300, ProcedureCode = "1000", Packages = 1, PackageKindCode = "PX" });

        var request = DeclarationMapper.ToRequest(declaration, shipment, "FI01234567");

        Assert.Equal("EX", request.DeclarationType);
        Assert.Equal("NRDL0000030", request.TransportDocument);
        Assert.Equal("3", request.ModeOfTransportAtBorder);
        Assert.Equal(new[] { 1, 2 }, request.GoodsItems.Select(g => g.ItemNumber));
        Assert.Equal(DeclarationStatus.UnderControl, DeclarationMapper.MapStatus("UNDER_CONTROL"));
        Assert.Equal(MilestoneCodes.CustomsReleased, DeclarationMapper.MilestoneFor(DeclarationStatus.Released));
    }
}
