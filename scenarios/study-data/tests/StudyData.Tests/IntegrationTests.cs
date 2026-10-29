using System.Text;
using System.Xml.Linq;
using Ionic.Zip;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyData.Core.Labs;
using StudyData.Data;
using StudyData.Data.Services;
using StudyData.Integration.Labs;
using StudyData.Integration.Odm;

namespace StudyData.Tests;

public class IntegrationTests
{
    private const string Header = "STUDY;SITE;SUBJECT;VISIT;ACCESSION;COLLECTED;TESTCD;TEST;RESULT;UNIT;LOW;HIGH";

    [Fact]
    public async Task LabFileIsImportedOnce()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var import = scope.ServiceProvider.GetRequiredService<LabImportService>();
        var csv = string.Join('\n', Header,
            "FAB-ONC-201;101;101-001;C1D8;CL7781203;2026-06-23T08:40:00;ANC;Neutrophils;0.9;10^9/L;1.5;7.5",
            "FAB-ONC-201;101;101-001;C1D8;CL7781203;2026-06-23T08:40:00;PLAT;Platelets;131;10^9/L;150;400",
            "FAB-ONC-201;102;101-001;C1D8;CL7781204;2026-06-23T08:40:00;HGB;Haemoglobin;118;g/L;120;160",
            "FAB-ONC-201;101;101-001;C1D8;CL7781203;bad;HGB;Haemoglobin;118;g/L;120;160");
        var file = new LabFile("FCR_FAB201_20260623.csv", Encoding.UTF8.GetBytes(csv));
        var result = await import.ImportAsync(file);
        Assert.Equal(2, result.Imported);
        Assert.Equal(2, result.Rejected);

        var ctx = scope.ServiceProvider.GetRequiredService<StudyDataContext>();
        var anc = await ctx.LabResults.SingleAsync(l => l.TestCode == "ANC");
        Assert.Equal(LabFlag.CriticalLow, anc.Flag);
        Assert.Equal(3, anc.Grade);
        Assert.Equal(new DateTime(2026, 6, 23, 5, 40, 0), anc.CollectedAt);
        Assert.True(await ctx.Notifications.AnyAsync(n => n.Kind == "CriticalLab"));

        var again = await import.ImportAsync(file);
        Assert.Equal(0, again.Imported);
        Assert.Contains("already imported", again.Errors[0]);
    }

    [Fact]
    public async Task WrongHeaderIsRejected()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var result = await scope.ServiceProvider.GetRequiredService<LabImportService>().ImportAsync(new LabFile("x.csv", Encoding.UTF8.GetBytes("A;B\n1;2")));
        Assert.Equal(0, result.Imported);
        Assert.Contains("Unexpected header.", result.Errors);
    }

    [Fact]
    public async Task OdmExportIsBlindedAndEncrypted()
    {
        using var db = await TestDb.CreateAsync();
        using var scope = db.Scope();
        var read = scope.ServiceProvider.GetRequiredService<StudyReadService>();
        var study = (await read.GetDesignAsync("FAB-ONC-201"))!;
        var folder = Directory.CreateTempSubdirectory().FullName;
        var exporter = scope.ServiceProvider.GetRequiredService<OdmExporter>();

        var plain = await exporter.ExportAsync(study, folder, "plain", includeArm: false, archivePassword: null, db.Clock.UtcNow);
        Assert.Equal(3, plain.Subjects);
        var xml = XDocument.Load(plain.XmlPath);
        XNamespace ns = "http://www.cdisc.org/ns/odm/v1.3";
        Assert.Equal(3, xml.Descendants(ns + "SubjectData").Count());
        Assert.DoesNotContain(xml.Descendants(ns + "ItemData"), i => (string?)i.Attribute("ItemOID") == "I.RAND.ARM");
        Assert.Contains(xml.Descendants(ns + "ItemData"), i => (string?)i.Attribute("ItemOID") == "I.VS.VSBMI");

        var zipped = await exporter.ExportAsync(study, folder, "sponsor", includeArm: true, archivePassword: "secret", db.Clock.UtcNow);
        Assert.NotNull(zipped.ArchivePath);
        using var zip = ZipFile.Read(zipped.ArchivePath);
        var entry = zip["sponsor.xml"];
        Assert.Equal(EncryptionAlgorithm.WinZipAes256, entry.Encryption);
        using var ms = new MemoryStream();
        entry.ExtractWithPassword(ms, "secret");
        ms.Position = 0;
        var withArm = XDocument.Load(ms);
        Assert.Contains(withArm.Descendants(ns + "ItemData"), i => (string?)i.Attribute("ItemOID") == "I.RAND.ARM");
    }
}
