using System.Globalization;
using System.Text;
using System.Xml;

namespace ExpenseClaims.Core.Payments;

/// <summary>
/// Writes a SEPA credit transfer initiation (ISO 20022 pain.001.001.03) as accepted by the bank's
/// corporate payment gateway. One payment information block per file, purpose code OTHR.
/// </summary>
public static class Pain001Writer
{
    public const string Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.03";

    public static byte[] Write(
        string messageId,
        DateTime createdAt,
        DateOnly executionDate,
        DebtorAccount debtor,
        IReadOnlyList<PaymentInstruction> payments)
    {
        if (payments.Count == 0)
            throw new ArgumentException("A payment file needs at least one payment.", nameof(payments));
        if (messageId.Length > 35)
            throw new ArgumentException("Message id can be at most 35 characters.", nameof(messageId));

        var total = payments.Sum(p => p.Amount);
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using var buffer = new MemoryStream();
        using (var x = XmlWriter.Create(buffer, settings))
        {
            x.WriteStartDocument();
            x.WriteStartElement("Document", Namespace);
            x.WriteStartElement("CstmrCdtTrfInitn");

            x.WriteStartElement("GrpHdr");
            x.WriteElementString("MsgId", messageId);
            x.WriteElementString("CreDtTm", createdAt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
            x.WriteElementString("NbOfTxs", payments.Count.ToString(CultureInfo.InvariantCulture));
            x.WriteElementString("CtrlSum", Amount(total));
            x.WriteStartElement("InitgPty");
            x.WriteElementString("Nm", Text(debtor.Name, 70));
            WriteOrgId(x, debtor.OrganisationId);
            x.WriteEndElement();
            x.WriteEndElement();

            x.WriteStartElement("PmtInf");
            x.WriteElementString("PmtInfId", messageId);
            x.WriteElementString("PmtMtd", "TRF");
            x.WriteElementString("BtchBookg", "true");
            x.WriteElementString("NbOfTxs", payments.Count.ToString(CultureInfo.InvariantCulture));
            x.WriteElementString("CtrlSum", Amount(total));
            x.WriteStartElement("PmtTpInf");
            x.WriteStartElement("SvcLvl");
            x.WriteElementString("Cd", "SEPA");
            x.WriteEndElement();
            x.WriteEndElement();
            x.WriteElementString("ReqdExctnDt", executionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            x.WriteStartElement("Dbtr");
            x.WriteElementString("Nm", Text(debtor.Name, 70));
            WriteOrgId(x, debtor.OrganisationId);
            x.WriteEndElement();
            x.WriteStartElement("DbtrAcct");
            x.WriteStartElement("Id");
            x.WriteElementString("IBAN", Iban.Normalize(debtor.Iban));
            x.WriteEndElement();
            x.WriteEndElement();
            x.WriteStartElement("DbtrAgt");
            x.WriteStartElement("FinInstnId");
            x.WriteElementString("BIC", debtor.Bic);
            x.WriteEndElement();
            x.WriteEndElement();
            x.WriteElementString("ChrgBr", "SLEV");

            foreach (var p in payments)
            {
                x.WriteStartElement("CdtTrfTxInf");
                x.WriteStartElement("PmtId");
                x.WriteElementString("EndToEndId", Text(p.EndToEndId, 35));
                x.WriteEndElement();
                x.WriteStartElement("Amt");
                x.WriteStartElement("InstdAmt");
                x.WriteAttributeString("Ccy", "EUR");
                x.WriteString(Amount(p.Amount));
                x.WriteEndElement();
                x.WriteEndElement();
                if (!string.IsNullOrWhiteSpace(p.CreditorBic))
                {
                    x.WriteStartElement("CdtrAgt");
                    x.WriteStartElement("FinInstnId");
                    x.WriteElementString("BIC", p.CreditorBic);
                    x.WriteEndElement();
                    x.WriteEndElement();
                }
                x.WriteStartElement("Cdtr");
                x.WriteElementString("Nm", Text(p.CreditorName, 70));
                x.WriteEndElement();
                x.WriteStartElement("CdtrAcct");
                x.WriteStartElement("Id");
                x.WriteElementString("IBAN", Iban.Normalize(p.CreditorIban));
                x.WriteEndElement();
                x.WriteEndElement();
                x.WriteStartElement("Purp");
                x.WriteElementString("Cd", "OTHR");
                x.WriteEndElement();
                x.WriteStartElement("RmtInf");
                x.WriteElementString("Ustrd", Text(p.RemittanceInfo, 140));
                x.WriteEndElement();
                x.WriteEndElement();
            }

            x.WriteEndElement();
            x.WriteEndElement();
            x.WriteEndElement();
            x.WriteEndDocument();
        }
        return buffer.ToArray();
    }

    private static void WriteOrgId(XmlWriter x, string organisationId)
    {
        x.WriteStartElement("Id");
        x.WriteStartElement("OrgId");
        x.WriteStartElement("Othr");
        x.WriteElementString("Id", organisationId);
        x.WriteStartElement("SchmeNm");
        x.WriteElementString("Cd", "BANK");
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
    }

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Restricts free text to the SEPA basic Latin character set and the given length.</summary>
    public static string Text(string value, int maxLength)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') || "/-?:().,'+ ".IndexOf(c) >= 0 ? c : ' ');
        }
        var text = sb.ToString().Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
