using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml;

namespace VanArsdel.Integration.Deployment
{
    public class BindingEndpoint
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Port { get; set; }
        public string TransportType { get; set; }
        public string Address { get; set; }
        public string Host { get; set; }
        public string Pipeline { get; set; }
    }

    public class BindingsReport
    {
        public List<BindingEndpoint> Endpoints { get; } = new List<BindingEndpoint>();
        public List<string> Problems { get; } = new List<string>();
        public List<string> Orchestrations { get; } = new List<string>();
    }

    /// <summary>
    /// Reads an expanded BindingInfo.xml: lists receive locations and send ports, flags unexpanded tokens, duplicate
    /// receive addresses, missing handlers and SFTP/FTP passwords left in clear text (they must come from SSO).
    /// </summary>
    public static class BindingsInspector
    {
        public static BindingsReport Inspect(XmlDocument bindings)
        {
            var report = new BindingsReport();
            foreach (XmlElement rl in bindings.SelectNodes("//ReceivePort/ReceiveLocations/ReceiveLocation"))
            {
                var port = ((XmlElement)rl.ParentNode.ParentNode).GetAttribute("Name");
                report.Endpoints.Add(new BindingEndpoint
                {
                    Kind = "ReceiveLocation",
                    Name = rl.GetAttribute("Name"),
                    Port = port,
                    Address = Text(rl, "Address"),
                    TransportType = Attr(rl, "ReceiveLocationTransportType", "Name"),
                    Host = Attr(rl, "ReceiveHandler", "Name"),
                    Pipeline = Attr(rl, "ReceivePipeline", "Name")
                });
            }

            foreach (XmlElement sp in bindings.SelectNodes("//SendPortCollection/SendPort"))
            {
                var dynamicPort = sp.GetAttribute("IsStatic") == "false";
                report.Endpoints.Add(new BindingEndpoint
                {
                    Kind = dynamicPort ? "DynamicSendPort" : "SendPort",
                    Name = sp.GetAttribute("Name"),
                    Port = sp.GetAttribute("Name"),
                    Address = dynamicPort ? null : Text(sp, "PrimaryTransport/Address"),
                    TransportType = dynamicPort ? null : Attr(sp, "PrimaryTransport/TransportType", "Name"),
                    Host = dynamicPort ? null : Attr(sp, "PrimaryTransport/SendHandler", "Name"),
                    Pipeline = Attr(sp, "TransmitPipeline", "Name")
                });
            }

            foreach (XmlElement o in bindings.SelectNodes("//ModuleRef/Services/Service"))
            {
                report.Orchestrations.Add(o.GetAttribute("Name"));
            }

            var raw = bindings.OuterXml;
            var tokenAt = raw.IndexOf("${", StringComparison.Ordinal);
            if (tokenAt >= 0)
            {
                var end = raw.IndexOf('}', tokenAt);
                report.Problems.Add("Unexpanded setting " + raw.Substring(tokenAt, Math.Max(2, end - tokenAt + 1)));
            }

            foreach (var dup in report.Endpoints.Where(e => e.Kind == "ReceiveLocation" && !string.IsNullOrEmpty(e.Address))
                .GroupBy(e => e.Address, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                report.Problems.Add("Receive address used more than once: " + dup.Key + " (" + string.Join(", ", dup.Select(d => d.Name)) + ")");
            }

            foreach (var e in report.Endpoints.Where(e => e.Kind != "DynamicSendPort" && string.IsNullOrEmpty(e.Host)))
            {
                report.Problems.Add(e.Kind + " " + e.Name + " has no handler host.");
            }

            foreach (XmlElement data in bindings.SelectNodes("//TransportTypeData | //ReceiveLocationTransportTypeData"))
            {
                var inner = WebUtility.HtmlDecode(data.InnerText);
                if (inner.IndexOf("<Password", StringComparison.OrdinalIgnoreCase) >= 0 && inner.IndexOf("<Password vt=\"1\"", StringComparison.OrdinalIgnoreCase) < 0
                    && inner.IndexOf("<Password vt=\"8\">******", StringComparison.OrdinalIgnoreCase) < 0 && inner.IndexOf("<Password vt=\"8\"></Password>", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    var owner = data.ParentNode is XmlElement ? ((XmlElement)(data.ParentNode.Name == "PrimaryTransport" ? data.ParentNode.ParentNode : data.ParentNode)).GetAttribute("Name") : "?";
                    report.Problems.Add("Clear text password in transport data of " + owner + ".");
                }
            }

            return report;
        }

        private static string Text(XmlElement e, string path)
        {
            var n = e.SelectSingleNode(path);
            return n == null ? null : n.InnerText;
        }

        private static string Attr(XmlElement e, string path, string attr)
        {
            var n = e.SelectSingleNode(path) as XmlElement;
            return n == null ? null : n.GetAttribute(attr);
        }
    }
}
