using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Coho.Billing.Core.Output
{
    /// <summary>Writes a SEPA core direct debit initiation (pain.008.001.02) for the bank.</summary>
    public class SepaDirectDebitWriter
    {
        public const string Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.02";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly CreditorSettings _creditor;

        public SepaDirectDebitWriter(CreditorSettings creditor)
        {
            if (creditor == null) throw new ArgumentNullException("creditor");
            if (!Iban.IsValid(creditor.Iban)) throw new ArgumentException("Creditor IBAN is not valid", "creditor");
            _creditor = creditor;
        }

        public void Write(DirectDebitBatch batch, Stream output)
        {
            if (batch == null) throw new ArgumentNullException("batch");
            if (batch.Items.Count == 0) throw new ArgumentException("Batch has no items", "batch");

            var invalid = batch.Items.FirstOrDefault(i => !Iban.IsValid(i.DebtorIban));
            if (invalid != null)
            {
                throw new InvalidOperationException("Debtor IBAN is not valid for " + invalid.EndToEndId);
            }

            var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
            using (var xml = XmlWriter.Create(output, settings))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("Document", Namespace);
                xml.WriteStartElement("CstmrDrctDbtInitn");

                xml.WriteStartElement("GrpHdr");
                xml.WriteElementString("MsgId", batch.MessageId);
                xml.WriteElementString("CreDtTm", batch.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", Invariant));
                xml.WriteElementString("NbOfTxs", batch.Items.Count.ToString(Invariant));
                xml.WriteElementString("CtrlSum", Amount(batch.Items.Sum(i => i.Amount)));
                xml.WriteStartElement("InitgPty");
                xml.WriteElementString("Nm", Text(_creditor.Name, 70));
                xml.WriteEndElement();
                xml.WriteEndElement();

                var groups = batch.Items.GroupBy(i => i.FirstCollection ? "FRST" : "RCUR").OrderBy(g => g.Key);
                foreach (var group in groups)
                {
                    WritePaymentInfo(xml, batch, group.Key, group.ToList());
                }

                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndDocument();
            }
        }

        private void WritePaymentInfo(XmlWriter xml, DirectDebitBatch batch, string sequence, System.Collections.Generic.List<DirectDebitItem> items)
        {
            xml.WriteStartElement("PmtInf");
            xml.WriteElementString("PmtInfId", batch.MessageId + "-" + sequence);
            xml.WriteElementString("PmtMtd", "DD");
            xml.WriteElementString("NbOfTxs", items.Count.ToString(Invariant));
            xml.WriteElementString("CtrlSum", Amount(items.Sum(i => i.Amount)));

            xml.WriteStartElement("PmtTpInf");
            xml.WriteStartElement("SvcLvl");
            xml.WriteElementString("Cd", "SEPA");
            xml.WriteEndElement();
            xml.WriteStartElement("LclInstrm");
            xml.WriteElementString("Cd", "CORE");
            xml.WriteEndElement();
            xml.WriteElementString("SeqTp", sequence);
            xml.WriteEndElement();

            xml.WriteElementString("ReqdColltnDt", batch.CollectionDate.ToString("yyyy-MM-dd", Invariant));

            xml.WriteStartElement("Cdtr");
            xml.WriteElementString("Nm", Text(_creditor.Name, 70));
            xml.WriteEndElement();
            xml.WriteStartElement("CdtrAcct");
            xml.WriteStartElement("Id");
            xml.WriteElementString("IBAN", Iban.Normalize(_creditor.Iban));
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteStartElement("CdtrAgt");
            xml.WriteStartElement("FinInstnId");
            xml.WriteElementString("BIC", _creditor.Bic);
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteElementString("ChrgBr", "SLEV");

            xml.WriteStartElement("CdtrSchmeId");
            xml.WriteStartElement("Id");
            xml.WriteStartElement("PrvtId");
            xml.WriteStartElement("Othr");
            xml.WriteElementString("Id", _creditor.CreditorId);
            xml.WriteStartElement("SchmeNm");
            xml.WriteElementString("Prtry", "SEPA");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();

            foreach (var item in items)
            {
                xml.WriteStartElement("DrctDbtTxInf");
                xml.WriteStartElement("PmtId");
                xml.WriteElementString("EndToEndId", item.EndToEndId);
                xml.WriteEndElement();
                xml.WriteStartElement("InstdAmt");
                xml.WriteAttributeString("Ccy", "EUR");
                xml.WriteString(Amount(item.Amount));
                xml.WriteEndElement();

                xml.WriteStartElement("DrctDbtTx");
                xml.WriteStartElement("MndtRltdInf");
                xml.WriteElementString("MndtId", item.MandateId);
                xml.WriteElementString("DtOfSgntr", item.MandateSignedOn.ToString("yyyy-MM-dd", Invariant));
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteStartElement("DbtrAgt");
                xml.WriteStartElement("FinInstnId");
                xml.WriteStartElement("Othr");
                xml.WriteElementString("Id", "NOTPROVIDED");
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteStartElement("Dbtr");
                xml.WriteElementString("Nm", Text(item.DebtorName, 70));
                xml.WriteEndElement();
                xml.WriteStartElement("DbtrAcct");
                xml.WriteStartElement("Id");
                xml.WriteElementString("IBAN", Iban.Normalize(item.DebtorIban));
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteStartElement("RmtInf");
                xml.WriteStartElement("Strd");
                xml.WriteStartElement("CdtrRefInf");
                xml.WriteStartElement("Tp");
                xml.WriteStartElement("CdOrPrtry");
                xml.WriteElementString("Cd", "SCOR");
                xml.WriteEndElement();
                xml.WriteStartElement("Issr");
                xml.WriteString("ISO");
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteElementString("Ref", item.CreditorReference);
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();

                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        private static string Amount(decimal value)
        {
            return value.ToString("0.00", Invariant);
        }

        private static string Text(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // SEPA basic Latin character set.
            var sb = new StringBuilder(value.Length);
            foreach (var c in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || " /-?:().,'+".IndexOf(c) >= 0 ? c : ' ');
            }

            var text = sb.ToString().Trim();
            return text.Length > max ? text.Substring(0, max) : text;
        }
    }
}
