using System;
using System.Data.SqlTypes;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr.Integration
{
    /// <summary>
    /// Posts rebate credit notes to the finance system's AP web service (SOAP 1.1, Windows authentication
    /// as the SQL Server service account).
    /// </summary>
    public static class ErpPosting
    {
        private const string Ns = "http://contosofoods.com/finance/ap/2014/09";

        [SqlProcedure(Name = "clr_PostCreditNote")]
        public static void PostCreditNote(
            [SqlFacet(MaxSize = 400)] SqlString endpoint,
            [SqlFacet(MaxSize = 20)] SqlString vendorNo,
            [SqlFacet(Precision = 19, Scale = 2)] SqlDecimal amount,
            [SqlFacet(MaxSize = 3)] SqlString currency,
            [SqlFacet(MaxSize = 40)] SqlString reference,
            [SqlFacet(MaxSize = 200)] SqlString text,
            SqlInt32 timeoutSeconds,
            [SqlFacet(MaxSize = 20)] out SqlString documentNo)
        {
            string body = BuildEnvelope(vendorNo.Value, amount.Value, currency.IsNull ? "EUR" : currency.Value, reference.Value, text.IsNull ? string.Empty : text.Value);

            var request = (HttpWebRequest)WebRequest.Create(endpoint.Value);
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Headers.Add("SOAPAction", "\"" + Ns + "/PostCreditNote\"");
            request.UseDefaultCredentials = true;
            request.PreAuthenticate = true;
            request.Timeout = (timeoutSeconds.IsNull ? 30 : timeoutSeconds.Value) * 1000;

            byte[] payload = Encoding.UTF8.GetBytes(body);
            request.ContentLength = payload.Length;
            using (Stream s = request.GetRequestStream())
                s.Write(payload, 0, payload.Length);

            string responseText;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()!, Encoding.UTF8))
                    responseText = reader.ReadToEnd();
            }
            catch (WebException ex) when (ex.Response != null)
            {
                using (var reader = new StreamReader(ex.Response.GetResponseStream()!, Encoding.UTF8))
                    responseText = reader.ReadToEnd();
                throw new InvalidOperationException("Finance service rejected credit note " + reference.Value + ": " + ReadFault(responseText));
            }

            documentNo = ReadDocumentNo(responseText);
        }

        internal static string BuildEnvelope(string vendorNo, decimal amount, string currency, string reference, string text)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings { OmitXmlDeclaration = false, Encoding = Encoding.UTF8 };
            using (var w = XmlWriter.Create(new StringWriter(sb, CultureInfo.InvariantCulture), settings))
            {
                w.WriteStartElement("s", "Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
                w.WriteStartElement("s", "Body", "http://schemas.xmlsoap.org/soap/envelope/");
                w.WriteStartElement("PostCreditNote", Ns);
                w.WriteElementString("CompanyCode", Ns, "CF01");
                w.WriteElementString("VendorNo", Ns, vendorNo);
                w.WriteElementString("DocumentType", Ns, "KG");
                w.WriteElementString("PostingDate", Ns, DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                w.WriteElementString("Amount", Ns, amount.ToString("0.00", CultureInfo.InvariantCulture));
                w.WriteElementString("Currency", Ns, currency);
                w.WriteElementString("Reference", Ns, reference);
                w.WriteElementString("GlAccount", Ns, "481200");
                w.WriteElementString("Text", Ns, text);
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();
            }
            return sb.ToString();
        }

        private static string ReadDocumentNo(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("f", Ns);
            XmlNode? node = doc.SelectSingleNode("//f:DocumentNo", ns);
            if (node == null || node.InnerText.Trim().Length == 0)
                throw new InvalidOperationException("Finance service response has no DocumentNo.");
            return node.InnerText.Trim();
        }

        private static string ReadFault(string xml)
        {
            try
            {
                var doc = new XmlDocument { XmlResolver = null };
                doc.LoadXml(xml);
                XmlNode? node = doc.SelectSingleNode("//*[local-name()='faultstring']");
                return node?.InnerText ?? xml;
            }
            catch (XmlException)
            {
                return xml.Length > 300 ? xml.Substring(0, 300) : xml;
            }
        }
    }
}
