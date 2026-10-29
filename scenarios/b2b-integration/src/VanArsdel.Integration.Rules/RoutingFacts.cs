using System;

namespace VanArsdel.Integration.Rules
{
    /// <summary>
    /// Fact for the VanArsdel.Routing policy that builds the routing slip for outbound documents: which map and which
    /// send port (by logical endpoint name) per partner, standard and document kind.
    /// </summary>
    [Serializable]
    public class RoutingFacts
    {
        public RoutingFacts(string partnerId, string standard, string transport, string documentKind)
        {
            PartnerId = partnerId;
            Standard = standard;
            Transport = transport;
            DocumentKind = documentKind;
        }

        public string PartnerId { get; private set; }
        public string Standard { get; private set; }
        public string Transport { get; private set; }
        public string DocumentKind { get; private set; }

        public string Slip { get; private set; }

        public bool Resolved
        {
            get { return !string.IsNullOrEmpty(Slip); }
        }

        public void SetSlip(string slip)
        {
            Slip = slip;
        }

        /// <summary>Builds the usual slip: map, send to the partner endpoint, archive.</summary>
        public void Route(string map, string endpoint)
        {
            Slip = "0|MAP:" + map + ";SEND:" + endpoint + ";ARCHIVE:outbound;TRACK:" + DocumentKind;
        }

        /// <summary>Endpoint name convention of the send ports: AS2.&lt;PartnerId&gt;, SFTP.&lt;PartnerId&gt;, ...</summary>
        public string PartnerEndpoint()
        {
            return Transport.ToUpperInvariant() + "." + PartnerId;
        }
    }
}
