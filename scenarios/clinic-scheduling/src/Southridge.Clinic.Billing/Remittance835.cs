using System;
using System.Collections.Generic;

namespace Southridge.Clinic.Billing
{
    public class Remittance
    {
        public Remittance()
        {
            Claims = new List<RemittanceClaim>();
        }

        public string PayerName { get; set; }
        public string PayerIdentifier { get; set; }
        public string PaymentMethod { get; set; }
        public decimal TotalPaid { get; set; }
        public DateTime PaymentDate { get; set; }
        public string TraceNumber { get; set; }
        public List<RemittanceClaim> Claims { get; private set; }
    }

    public class RemittanceClaim
    {
        public RemittanceClaim()
        {
            Adjustments = new List<ClaimAdjustment>();
        }

        public string ClaimNumber { get; set; }
        public string StatusCode { get; set; }
        public decimal ChargeAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PatientResponsibility { get; set; }
        public string PayerClaimNumber { get; set; }
        public List<ClaimAdjustment> Adjustments { get; private set; }

        /// <summary>Contractual and other payer adjustments (everything except patient responsibility, group PR).</summary>
        public decimal PayerAdjustments
        {
            get
            {
                decimal total = 0;
                foreach (ClaimAdjustment a in Adjustments)
                {
                    if (a.GroupCode != "PR")
                    {
                        total += a.Amount;
                    }
                }

                return total;
            }
        }

        public bool IsDenied
        {
            get { return StatusCode == "4"; }
        }
    }

    public class ClaimAdjustment
    {
        public string GroupCode { get; set; }
        public string ReasonCode { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>835 Health Care Claim Payment/Advice reader (claim level CLP/CAS; service lines are summed into the claim).</summary>
    public static class Remittance835Parser
    {
        public static Remittance Parse(string content)
        {
            var reader = new X12Reader(content);
            var remit = new Remittance();
            RemittanceClaim current = null;
            bool inPayerLoop = false;
            foreach (string[] s in reader.Segments)
            {
                switch (s[0])
                {
                    case "ST":
                        if (X12Reader.Element(s, 1) != "835")
                        {
                            throw new FormatException("Transaction set " + X12Reader.Element(s, 1) + " is not an 835.");
                        }

                        break;
                    case "BPR":
                        remit.TotalPaid = X12Reader.Amount(s, 2);
                        remit.PaymentMethod = X12Reader.Element(s, 4);
                        if (X12Reader.Element(s, 16).Length > 0)
                        {
                            remit.PaymentDate = X12Reader.Date(X12Reader.Element(s, 16));
                        }

                        break;
                    case "TRN":
                        remit.TraceNumber = X12Reader.Element(s, 2);
                        break;
                    case "N1":
                        inPayerLoop = X12Reader.Element(s, 1) == "PR";
                        if (inPayerLoop)
                        {
                            remit.PayerName = X12Reader.Element(s, 2);
                        }

                        break;
                    case "REF":
                        if (inPayerLoop && current == null && X12Reader.Element(s, 1) == "2U")
                        {
                            remit.PayerIdentifier = X12Reader.Element(s, 2);
                        }

                        break;
                    case "CLP":
                        current = new RemittanceClaim
                        {
                            ClaimNumber = X12Reader.Element(s, 1),
                            StatusCode = X12Reader.Element(s, 2),
                            ChargeAmount = X12Reader.Amount(s, 3),
                            PaidAmount = X12Reader.Amount(s, 4),
                            PatientResponsibility = X12Reader.Amount(s, 5),
                            PayerClaimNumber = X12Reader.Element(s, 7)
                        };
                        remit.Claims.Add(current);
                        inPayerLoop = false;
                        break;
                    case "CAS":
                        if (current != null)
                        {
                            // CAS*group*reason*amount*qty*reason*amount*qty ... up to six reasons per segment
                            for (int i = 2; i + 1 < s.Length; i += 3)
                            {
                                if (s[i].Length == 0)
                                {
                                    continue;
                                }

                                current.Adjustments.Add(new ClaimAdjustment { GroupCode = s[1], ReasonCode = s[i], Amount = X12Reader.Amount(s, i + 1) });
                            }
                        }

                        break;
                }
            }

            if (remit.PaymentDate == DateTime.MinValue)
            {
                remit.PaymentDate = DateTime.Today;
            }

            return remit;
        }
    }

    public class ImplementationAck
    {
        public int GroupControlNumber { get; set; }
        public string Status { get; set; }
        public int AcceptedTransactions { get; set; }
        public int ReceivedTransactions { get; set; }
        public List<string> Errors { get; set; }
    }

    /// <summary>999 Implementation Acknowledgment reader: AK1 (group control number) and AK9 (A, E, P or R).</summary>
    public static class Ack999Parser
    {
        public static ImplementationAck Parse(string content)
        {
            var reader = new X12Reader(content);
            var ack = new ImplementationAck { Errors = new List<string>() };
            foreach (string[] s in reader.Segments)
            {
                switch (s[0])
                {
                    case "AK1":
                        ack.GroupControlNumber = int.Parse(X12Reader.Element(s, 2), System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "IK3":
                        ack.Errors.Add("Segment " + X12Reader.Element(s, 1) + " at position " + X12Reader.Element(s, 2) + ": error " + X12Reader.Element(s, 4));
                        break;
                    case "IK4":
                        ack.Errors.Add("Element " + X12Reader.Element(s, 1) + ": error " + X12Reader.Element(s, 3));
                        break;
                    case "AK9":
                        ack.Status = X12Reader.Element(s, 1);
                        ack.ReceivedTransactions = int.Parse(X12Reader.Element(s, 3), System.Globalization.CultureInfo.InvariantCulture);
                        ack.AcceptedTransactions = int.Parse(X12Reader.Element(s, 4), System.Globalization.CultureInfo.InvariantCulture);
                        break;
                }
            }

            if (string.IsNullOrEmpty(ack.Status))
            {
                throw new FormatException("999 without AK9.");
            }

            return ack;
        }
    }
}
