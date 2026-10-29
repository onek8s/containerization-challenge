using System;
using System.Globalization;
using System.Xml;
using Microsoft.XLANGs.BaseTypes;
using VanArsdel.Integration.Contracts.Canonical;
using VanArsdel.Integration.Contracts.Erp;
using VanArsdel.Integration.Rules;

namespace VanArsdel.Integration.Runtime.Messages
{
    /// <summary>
    /// Builds and reads the ERP order service messages (schema Erp\OrderManagementService.xsd) in message
    /// assignment shapes, so the orchestrations do not need maps for a four-field request.
    /// </summary>
    [Serializable]
    public static class ErpMessages
    {
        public static XmlDocument CreateCheckCredit(XLANGMessage orderMessage)
        {
            var order = XLangMessages.ToOrder(orderMessage);
            var facts = new OrderFacts(order);
            var doc = new XmlDocument();
            var root = doc.AppendChild(doc.CreateElement("CheckCredit", ErpNamespaces.Service));
            var request = root.AppendChild(doc.CreateElement("request", ErpNamespaces.Service));
            Add(request, "SoldTo", order.Header.ErpPartnerNumber);
            Add(request, "OrderValue", facts.GetOrderValue().ToString("0.00", CultureInfo.InvariantCulture));
            Add(request, "Currency", order.Header.Currency);
            Add(request, "Reference", order.Header.OrderNumber);
            return doc;
        }

        public static PartnerFacts ApplyCreditResponse(XLANGMessage responseMessage, PartnerFacts partner)
        {
            var doc = XLangMessages.ToXmlDocument(responseMessage);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("e", ErpNamespaces.Service);
            var result = doc.SelectSingleNode("/e:CheckCreditResponse/e:CheckCreditResult", ns);
            partner.CreditChecked = true;
            if (result == null)
            {
                partner.CreditApproved = false;
                partner.BlockReason = "No credit check result";
                return partner;
            }

            partner.CreditApproved = Text(result, "e:Approved", ns) == "true";
            partner.CreditLimit = Decimal(Text(result, "e:CreditLimit", ns));
            partner.OpenExposure = Decimal(Text(result, "e:OpenExposure", ns));
            partner.BlockReason = Text(result, "e:BlockReason", ns);
            return partner;
        }

        private static void Add(XmlNode parent, string name, string value)
        {
            var e = parent.OwnerDocument.CreateElement(name, ErpNamespaces.Service);
            e.InnerText = value ?? string.Empty;
            parent.AppendChild(e);
        }

        private static string Text(XmlNode node, string xpath, XmlNamespaceManager ns)
        {
            var n = node.SelectSingleNode(xpath, ns);
            return n == null ? null : n.InnerText;
        }

        private static decimal Decimal(string value)
        {
            decimal d;
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : 0m;
        }
    }
}
