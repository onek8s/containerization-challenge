using System;
using System.Collections.Generic;
using System.Globalization;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Billing
{
    public class SubmitterInfo
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string SubmitterName { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public string ReceiverName { get; set; }
        public string BillingProviderName { get; set; }
        public bool Production { get; set; }
    }

    /// <summary>
    /// 837 Professional (005010X222A1) writer. One interchange per batch file, one claim per 2300 loop,
    /// subscriber hierarchy built per claim (no grouping of claims for the same subscriber).
    /// </summary>
    public class Claim837Writer
    {
        private readonly SubmitterInfo _submitter;

        public Claim837Writer(SubmitterInfo submitter)
        {
            _submitter = submitter;
        }

        public string Write(IList<ClaimData> claims, int interchangeControlNumber, DateTime created)
        {
            if (claims.Count == 0)
            {
                throw new ArgumentException("A claim file needs at least one claim.");
            }

            string control = interchangeControlNumber.ToString("000000000", CultureInfo.InvariantCulture);
            var w = new X12Writer();
            w.Raw(string.Format(CultureInfo.InvariantCulture,
                "ISA*00*          *00*          *ZZ*{0}*ZZ*{1}*{2:yyMMdd}*{2:HHmm}*^*00501*{3}*1*{4}*:",
                Pad(_submitter.SenderId, 15), Pad(_submitter.ReceiverId, 15), created, control, _submitter.Production ? "P" : "T"));
            w.Segment("GS", "HC", _submitter.SenderId, _submitter.ReceiverId, created.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                created.ToString("HHmm", CultureInfo.InvariantCulture), interchangeControlNumber.ToString(CultureInfo.InvariantCulture), "X", "005010X222A1");
            w.Segment("ST", "837", "0001", "005010X222A1");
            w.Segment("BHT", "0019", "00", control, created.ToString("yyyyMMdd", CultureInfo.InvariantCulture), created.ToString("HHmm", CultureInfo.InvariantCulture), "CH");
            w.Segment("NM1", "41", "2", _submitter.SubmitterName, "", "", "", "", "46", _submitter.SenderId);
            w.Segment("PER", "IC", _submitter.ContactName, "TE", _submitter.ContactPhone);
            w.Segment("NM1", "40", "2", _submitter.ReceiverName, "", "", "", "", "46", _submitter.ReceiverId);

            int hl = 0;
            ClaimData first = claims[0];
            hl++;
            int billingHl = hl;
            w.Segment("HL", billingHl.ToString(CultureInfo.InvariantCulture), "", "20", "1");
            w.Segment("PRV", "BI", "PXC", first.TaxonomyCode);
            w.Segment("NM1", "85", "2", _submitter.BillingProviderName, "", "", "", "", "XX", first.GroupNpi);
            w.Segment("N3", first.SiteAddress1);
            w.Segment("N4", first.SiteCity, first.SiteState, Zip9(first.SiteZip));
            w.Segment("REF", "EI", first.TaxId);

            foreach (ClaimData c in claims)
            {
                hl++;
                bool self = c.Relationship == (byte)InsuredRelationship.Self;
                w.Segment("HL", hl.ToString(CultureInfo.InvariantCulture), billingHl.ToString(CultureInfo.InvariantCulture), "22", self ? "0" : "1");
                w.Segment("SBR", "P", self ? "18" : "", c.GroupNumber, "", "", "", "", "", c.FilingIndicator);
                if (self)
                {
                    w.Segment("NM1", "IL", "1", c.PatientLastName, c.PatientFirstName, c.PatientMiddleName, "", "", "MI", c.MemberId);
                    w.Segment("N3", c.PatientAddress1, c.PatientAddress2);
                    w.Segment("N4", c.PatientCity, c.PatientState, c.PatientZip);
                    w.Segment("DMG", "D8", c.PatientDateOfBirth.ToString("yyyyMMdd", CultureInfo.InvariantCulture), SexCode(c.PatientSex));
                }
                else
                {
                    w.Segment("NM1", "IL", "1", c.SubscriberLastName ?? c.PatientLastName, c.SubscriberFirstName, "", "", "", "MI", c.MemberId);
                    if (c.SubscriberDateOfBirth.HasValue)
                    {
                        w.Segment("DMG", "D8", c.SubscriberDateOfBirth.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    }
                }

                w.Segment("NM1", "PR", "2", c.PayerName, "", "", "", "", "PI", c.ClaimPayerId);
                if (!self)
                {
                    int subscriberHl = hl;
                    hl++;
                    w.Segment("HL", hl.ToString(CultureInfo.InvariantCulture), subscriberHl.ToString(CultureInfo.InvariantCulture), "23", "0");
                    w.Segment("PAT", RelationshipCode(c.Relationship));
                    w.Segment("NM1", "QC", "1", c.PatientLastName, c.PatientFirstName, c.PatientMiddleName);
                    w.Segment("N3", c.PatientAddress1, c.PatientAddress2);
                    w.Segment("N4", c.PatientCity, c.PatientState, c.PatientZip);
                    w.Segment("DMG", "D8", c.PatientDateOfBirth.ToString("yyyyMMdd", CultureInfo.InvariantCulture), SexCode(c.PatientSex));
                }

                WriteClaim(w, c);
            }

            w.Segment("SE", (w.TransactionSegmentCount + 1).ToString(CultureInfo.InvariantCulture), "0001");
            w.Segment("GE", "1", interchangeControlNumber.ToString(CultureInfo.InvariantCulture));
            w.Segment("IEA", "1", control);
            return w.ToString();
        }

        private static void WriteClaim(X12Writer w, ClaimData c)
        {
            w.Segment("CLM", c.ClaimNumber, Money.ToX12(c.TotalCharge), "", "", c.PlaceOfService + ":B:" + (c.FrequencyCode ?? "1"), "Y", "A", "Y", "Y");
            if (!string.IsNullOrEmpty(c.OriginalClaimNumber) && c.FrequencyCode != "1")
            {
                w.Segment("REF", "F8", c.PayerClaimNumber ?? c.OriginalClaimNumber);
            }

            if (!string.IsNullOrEmpty(c.AuthorizationNumber))
            {
                w.Segment("REF", "G1", c.AuthorizationNumber);
            }

            w.Segment("REF", "EA", c.Mrn);
            var hi = new List<string>();
            foreach (ClaimDiagnosis d in c.Diagnoses)
            {
                string qualifier = d.CodeSet == 10 ? (hi.Count == 0 ? "ABK" : "ABF") : (hi.Count == 0 ? "BK" : "BF");
                hi.Add(qualifier + ":" + DiagnosisCodes.ToClaimFormat(d.Code));
                if (hi.Count == 12)
                {
                    break;
                }
            }

            w.Segment("HI", hi.ToArray());
            w.Segment("NM1", "82", "1", c.RenderingLastName, c.RenderingFirstName, "", "", "", "XX", c.RenderingNpi);
            w.Segment("PRV", "PE", "PXC", c.TaxonomyCode);
            w.Segment("NM1", "77", "2", c.SiteName, "", "", "", "", "XX", c.GroupNpi);
            w.Segment("N3", c.SiteAddress1);
            w.Segment("N4", c.SiteCity, c.SiteState, Zip9(c.SiteZip));

            int lx = 0;
            foreach (ClaimLine line in c.Lines)
            {
                lx++;
                w.Segment("LX", lx.ToString(CultureInfo.InvariantCulture));
                string procedure = "HC:" + line.CptCode + (string.IsNullOrEmpty(line.Modifier1) ? string.Empty : ":" + line.Modifier1)
                    + (string.IsNullOrEmpty(line.Modifier2) ? string.Empty : ":" + line.Modifier2);
                w.Segment("SV1", procedure, Money.ToX12(line.Amount * line.Units), "UN", line.Units.ToString(CultureInfo.InvariantCulture), "", "",
                    Pointers(line.DiagnosisPointers));
                w.Segment("DTP", "472", "D8", c.DateOfService.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                w.Segment("REF", "6R", line.ChargeId.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string Pointers(string pointers)
        {
            if (string.IsNullOrEmpty(pointers))
            {
                return "1";
            }

            var parts = new List<string>();
            foreach (char p in pointers)
            {
                if (char.IsDigit(p))
                {
                    parts.Add(p.ToString());
                }
            }

            return string.Join(":", parts.ToArray());
        }

        private static string SexCode(byte sex)
        {
            return sex == (byte)Sex.Female ? "F" : sex == (byte)Sex.Male ? "M" : "U";
        }

        private static string RelationshipCode(byte relationship)
        {
            return relationship == (byte)InsuredRelationship.Child ? "19" : relationship == (byte)InsuredRelationship.Spouse ? "01" : "G8";
        }

        private static string Zip9(string zip)
        {
            string digits = (zip ?? string.Empty).Replace("-", string.Empty);
            return digits.Length == 5 ? digits + "9998" : digits;
        }

        private static string Pad(string value, int length)
        {
            value = value ?? string.Empty;
            return value.Length >= length ? value.Substring(0, length) : value.PadRight(length);
        }
    }
}
