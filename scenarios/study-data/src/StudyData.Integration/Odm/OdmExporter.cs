using System.Globalization;
using System.Text;
using System.Xml;
using Ionic.Zip;
using Ionic.Zlib;
using Microsoft.EntityFrameworkCore;
using StudyData.Core.Design;
using StudyData.Core.Subjects;
using StudyData.Data;

namespace StudyData.Integration.Odm;

public record OdmExportResult(string XmlPath, string? ArchivePath, int Subjects, int Items);

/// <summary>
/// CDISC ODM 1.3.2 export: study metadata (MetaDataVersion) and clinical data (snapshot) of a study,
/// optionally packed into an AES-256 zip for the sponsor.
/// </summary>
public class OdmExporter
{
    private const string Ns = "http://www.cdisc.org/ns/odm/v1.3";

    private readonly StudyDataContext _db;

    public OdmExporter(StudyDataContext db)
    {
        _db = db;
    }

    public async Task<OdmExportResult> ExportAsync(Study study, string outputFolder, string fileStem, bool includeArm, string? archivePassword,
        DateTime now, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        var subjects = await _db.Subjects.Where(s => s.StudyId == study.Id)
            .Include(s => s.Site).Include(s => s.Arm)
            .Include(s => s.Visits).ThenInclude(v => v.Forms).ThenInclude(f => f.Values)
            .AsSplitQuery()
            .OrderBy(s => s.Number)
            .ToListAsync(ct);

        var xmlPath = Path.Combine(outputFolder, fileStem + ".xml");
        var items = 0;
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), Async = true };
        await using (var stream = File.Create(xmlPath))
        await using (var w = XmlWriter.Create(stream, settings))
        {
            await w.WriteStartDocumentAsync();
            w.WriteStartElement("ODM", Ns);
            w.WriteAttributeString("FileType", "Snapshot");
            w.WriteAttributeString("FileOID", $"FCR.{study.Code}.{now:yyyyMMddHHmmss}");
            w.WriteAttributeString("CreationDateTime", now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.WriteAttributeString("ODMVersion", "1.3.2");
            w.WriteAttributeString("Originator", "Fabrikam Clinical Research Study Data");

            WriteMetadata(w, study);

            w.WriteStartElement("ClinicalData", Ns);
            w.WriteAttributeString("StudyOID", study.Code);
            w.WriteAttributeString("MetaDataVersionOID", $"MDV.{study.DesignVersion}");
            foreach (var s in subjects)
            {
                w.WriteStartElement("SubjectData", Ns);
                w.WriteAttributeString("SubjectKey", s.Number);
                w.WriteStartElement("SiteRef", Ns);
                w.WriteAttributeString("LocationOID", $"SITE.{s.Site!.Number}");
                w.WriteEndElement();
                if (includeArm && s.Arm is not null)
                {
                    w.WriteStartElement("StudyEventData", Ns);
                    w.WriteAttributeString("StudyEventOID", "SE.RAND");
                    w.WriteStartElement("FormData", Ns);
                    w.WriteAttributeString("FormOID", "F.RAND");
                    w.WriteStartElement("ItemGroupData", Ns);
                    w.WriteAttributeString("ItemGroupOID", "IG.RAND");
                    Item(w, "I.RAND.ARM", s.Arm.Code);
                    Item(w, "I.RAND.RANDNO", s.RandomisationNumber);
                    w.WriteEndElement();
                    w.WriteEndElement();
                    w.WriteEndElement();
                }

                foreach (var visit in s.Visits.Where(v => v.Forms.Any(f => f.Values.Count > 0)))
                {
                    var vdef = study.Visits.First(d => d.Id == visit.VisitDefinitionId);
                    w.WriteStartElement("StudyEventData", Ns);
                    w.WriteAttributeString("StudyEventOID", $"SE.{vdef.Code}");
                    if (vdef.Unscheduled)
                    {
                        w.WriteAttributeString("StudyEventRepeatKey", visit.Repeat.ToString(CultureInfo.InvariantCulture));
                    }

                    foreach (var form in visit.Forms.Where(f => f.Values.Count > 0).OrderBy(f => f.Row))
                    {
                        var fdef = study.Forms.First(d => d.Id == form.FormDefinitionId);
                        w.WriteStartElement("FormData", Ns);
                        w.WriteAttributeString("FormOID", $"F.{fdef.Code}");
                        if (fdef.Repeating)
                        {
                            w.WriteAttributeString("FormRepeatKey", form.Row.ToString(CultureInfo.InvariantCulture));
                        }

                        w.WriteStartElement("ItemGroupData", Ns);
                        w.WriteAttributeString("ItemGroupOID", $"IG.{fdef.Code}");
                        foreach (var field in fdef.Fields.OrderBy(f => f.Order))
                        {
                            var value = form.Values.FirstOrDefault(v => v.FieldCode == field.Code)?.Value;
                            if (value is not null)
                            {
                                Item(w, $"I.{fdef.Code}.{field.Code}", value);
                                items++;
                            }
                        }

                        w.WriteEndElement();
                        w.WriteEndElement();
                    }

                    w.WriteEndElement();
                }

                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndElement();
            await w.WriteEndDocumentAsync();
        }

        string? archive = null;
        if (!string.IsNullOrEmpty(archivePassword))
        {
            archive = Path.Combine(outputFolder, fileStem + ".zip");
            using var zip = new ZipFile(archive, Encoding.UTF8)
            {
                CompressionLevel = CompressionLevel.BestCompression,
                Encryption = EncryptionAlgorithm.WinZipAes256,
                Password = archivePassword
            };
            zip.AddFile(xmlPath, "");
            zip.Comment = $"{study.Code} ODM snapshot {now:yyyy-MM-dd HH:mm} UTC";
            zip.Save();
            File.Delete(xmlPath);
        }

        return new OdmExportResult(archive is null ? xmlPath : "", archive, subjects.Count, items);
    }

    private static void WriteMetadata(XmlWriter w, Study study)
    {
        w.WriteStartElement("Study", Ns);
        w.WriteAttributeString("OID", study.Code);
        w.WriteStartElement("GlobalVariables", Ns);
        w.WriteElementString("StudyName", Ns, study.Code);
        w.WriteElementString("StudyDescription", Ns, study.Title);
        w.WriteElementString("ProtocolName", Ns, study.Code);
        w.WriteEndElement();
        w.WriteStartElement("MetaDataVersion", Ns);
        w.WriteAttributeString("OID", $"MDV.{study.DesignVersion}");
        w.WriteAttributeString("Name", $"Design version {study.DesignVersion}");

        w.WriteStartElement("Protocol", Ns);
        foreach (var v in study.Visits.OrderBy(v => v.Order))
        {
            w.WriteStartElement("StudyEventRef", Ns);
            w.WriteAttributeString("StudyEventOID", $"SE.{v.Code}");
            w.WriteAttributeString("OrderNumber", v.Order.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("Mandatory", v.Unscheduled ? "No" : "Yes");
            w.WriteEndElement();
        }

        w.WriteEndElement();

        foreach (var v in study.Visits.OrderBy(v => v.Order))
        {
            w.WriteStartElement("StudyEventDef", Ns);
            w.WriteAttributeString("OID", $"SE.{v.Code}");
            w.WriteAttributeString("Name", v.Name);
            w.WriteAttributeString("Repeating", v.Unscheduled ? "Yes" : "No");
            w.WriteAttributeString("Type", v.Unscheduled ? "Unscheduled" : "Scheduled");
            foreach (var code in v.FormCodeList)
            {
                w.WriteStartElement("FormRef", Ns);
                w.WriteAttributeString("FormOID", $"F.{code}");
                w.WriteAttributeString("Mandatory", "Yes");
                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        foreach (var f in study.Forms.OrderBy(f => f.Order))
        {
            w.WriteStartElement("FormDef", Ns);
            w.WriteAttributeString("OID", $"F.{f.Code}");
            w.WriteAttributeString("Name", f.Name);
            w.WriteAttributeString("Repeating", f.Repeating ? "Yes" : "No");
            w.WriteStartElement("ItemGroupRef", Ns);
            w.WriteAttributeString("ItemGroupOID", $"IG.{f.Code}");
            w.WriteAttributeString("Mandatory", "Yes");
            w.WriteEndElement();
            w.WriteEndElement();
        }

        foreach (var f in study.Forms.OrderBy(f => f.Order))
        {
            w.WriteStartElement("ItemGroupDef", Ns);
            w.WriteAttributeString("OID", $"IG.{f.Code}");
            w.WriteAttributeString("Name", f.Name);
            w.WriteAttributeString("Repeating", "No");
            foreach (var field in f.Fields.OrderBy(x => x.Order))
            {
                w.WriteStartElement("ItemRef", Ns);
                w.WriteAttributeString("ItemOID", $"I.{f.Code}.{field.Code}");
                w.WriteAttributeString("Mandatory", field.Required ? "Yes" : "No");
                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        foreach (var f in study.Forms.OrderBy(f => f.Order))
        {
            foreach (var field in f.Fields.OrderBy(x => x.Order))
            {
                w.WriteStartElement("ItemDef", Ns);
                w.WriteAttributeString("OID", $"I.{f.Code}.{field.Code}");
                w.WriteAttributeString("Name", field.Code);
                w.WriteAttributeString("DataType", field.Type switch
                {
                    FieldType.Integer => "integer",
                    FieldType.Decimal or FieldType.Derived => "float",
                    FieldType.Date => "date",
                    FieldType.Time => "time",
                    FieldType.Boolean => "boolean",
                    _ => "text"
                });
                w.WriteStartElement("Question", Ns);
                w.WriteStartElement("TranslatedText", Ns);
                w.WriteAttributeString("xml", "lang", null, "en");
                w.WriteString(field.Label);
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();
            }
        }

        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void Item(XmlWriter w, string oid, string? value)
    {
        w.WriteStartElement("ItemData", Ns);
        w.WriteAttributeString("ItemOID", oid);
        w.WriteAttributeString("Value", value ?? "");
        w.WriteEndElement();
    }
}
