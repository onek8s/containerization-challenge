using System;
using System.Collections.Generic;
using System.Linq;
using VanArsdel.Integration.Domain.Orders;
using VanArsdel.Integration.Edi.Envelopes;

namespace VanArsdel.Integration.Edi.Acks
{
    public class AckResult
    {
        public EdiStandard Standard { get; set; }

        /// <summary>Control number of the interchange (CONTRL UCI) or group (997 AK1) being acknowledged.</summary>
        public string AcknowledgedControlNumber { get; set; }

        public AckStatus Status { get; set; }
        public List<string> RejectedSets { get; } = new List<string>();
        public string SenderId { get; set; }
    }

    /// <summary>Reads received 997/CONTRL transaction sets to update the outbound interchange status.</summary>
    public static class AckReader
    {
        public static AckResult Read(ParsedInterchange interchange, TransactionSet set)
        {
            if (set.Type == "997")
            {
                var ak1 = set.Segments.First(s => s.Tag == "AK1");
                var ak9 = set.Segments.First(s => s.Tag == "AK9");
                var result = new AckResult
                {
                    Standard = EdiStandard.X12,
                    AcknowledgedControlNumber = ak1[2],
                    Status = FromX12(ak9[1]),
                    SenderId = interchange.Header.SenderId
                };

                string current = null;
                foreach (var s in set.Segments)
                {
                    if (s.Tag == "AK2")
                    {
                        current = s[2];
                    }
                    else if (s.Tag == "AK5" && s[1] == "R" && current != null)
                    {
                        result.RejectedSets.Add(current);
                    }
                }

                return result;
            }

            if (set.Type == "CONTRL")
            {
                var uci = set.Segments.First(s => s.Tag == "UCI");
                var result = new AckResult
                {
                    Standard = EdiStandard.Edifact,
                    AcknowledgedControlNumber = uci[1],
                    SenderId = interchange.Header.SenderId
                };

                foreach (var ucm in set.Segments.Where(s => s.Tag == "UCM" && s[3] == "4"))
                {
                    result.RejectedSets.Add(ucm[1]);
                }

                var ucms = set.Segments.Count(s => s.Tag == "UCM");
                if (uci[4] == "4")
                {
                    result.Status = AckStatus.Rejected;
                }
                else if (result.RejectedSets.Count == 0)
                {
                    result.Status = AckStatus.Accepted;
                }
                else
                {
                    result.Status = result.RejectedSets.Count == ucms ? AckStatus.Rejected : AckStatus.AcceptedWithErrors;
                }

                return result;
            }

            throw new ArgumentException("Transaction set " + set.Type + " is not an acknowledgement.", "set");
        }

        private static AckStatus FromX12(string ak901)
        {
            switch (ak901)
            {
                case "A":
                    return AckStatus.Accepted;
                case "E":
                case "P":
                    return AckStatus.AcceptedWithErrors;
                default:
                    return AckStatus.Rejected;
            }
        }
    }
}
