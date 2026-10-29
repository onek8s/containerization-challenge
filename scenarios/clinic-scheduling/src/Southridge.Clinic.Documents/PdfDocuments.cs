using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.Documents
{
    public class LetterAddress
    {
        public string Name { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
    }

    public class AppointmentLetterData
    {
        public LetterAddress To { get; set; }
        public string PatientName { get; set; }
        public string Mrn { get; set; }
        public DateTime StartsAt { get; set; }
        public string ProviderName { get; set; }
        public string AppointmentType { get; set; }
        public string SiteName { get; set; }
        public string SiteAddress { get; set; }
        public string SitePhone { get; set; }
        public string ConfirmationCode { get; set; }
        public bool NewPatient { get; set; }
    }

    public class VisitSummaryData
    {
        public VisitSummaryData()
        {
            Diagnoses = new List<string>();
            Procedures = new List<string>();
            Results = new List<string[]>();
        }

        public string PatientName { get; set; }
        public string Mrn { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DateTime DateOfService { get; set; }
        public string ProviderName { get; set; }
        public string SiteName { get; set; }
        public string ChiefComplaint { get; set; }
        public string Vitals { get; set; }
        public string Notes { get; set; }
        public List<string> Diagnoses { get; private set; }
        public List<string> Procedures { get; private set; }
        public List<string[]> Results { get; private set; }
        public DateTime? NextAppointment { get; set; }
    }

    public class ReferralLetterData
    {
        public string ReferringProviderName { get; set; }
        public string ReferringPractice { get; set; }
        public string ReferralNumber { get; set; }
        public string PatientName { get; set; }
        public DateTime PatientDateOfBirth { get; set; }
        public string Specialty { get; set; }
        public bool Approved { get; set; }
        public string AuthorizationNumber { get; set; }
        public int? VisitsAuthorized { get; set; }
        public string DecisionNote { get; set; }
        public string DecidedBy { get; set; }
    }

    /// <summary>
    /// Patient facing PDF documents built with iTextSharp: appointment letters, statements and visit summaries.
    /// </summary>
    public static class PdfDocuments
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
        private static readonly Font Title = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
        private static readonly Font Heading = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
        private static readonly Font Body = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        private static readonly Font Small = FontFactory.GetFont(FontFactory.HELVETICA, 8);

        public static byte[] AppointmentLetter(AppointmentLetterData data)
        {
            return Build(delegate(Document doc)
            {
                AddLetterhead(doc);
                AddAddress(doc, data.To);
                doc.Add(new Paragraph(DateTime.Today.ToString("MMMM d, yyyy", Us), Body));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Dear " + data.PatientName + ",", Body));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("This letter confirms your appointment:", Body));
                PdfPTable table = new PdfPTable(2);
                table.WidthPercentage = 80;
                table.SetWidths(new[] { 1f, 2f });
                AddRow(table, "Date", data.StartsAt.ToString("dddd, MMMM d, yyyy", Us));
                AddRow(table, "Time", data.StartsAt.ToString("h:mm tt", Us));
                AddRow(table, "With", data.ProviderName);
                AddRow(table, "Visit", data.AppointmentType);
                AddRow(table, "Location", data.SiteName + "\n" + data.SiteAddress);
                AddRow(table, "Confirmation", data.ConfirmationCode);
                table.SpacingBefore = 8;
                table.SpacingAfter = 8;
                doc.Add(table);
                if (data.NewPatient)
                {
                    doc.Add(new Paragraph("Please arrive 20 minutes early and bring your insurance card, a photo ID and a list of your medications. "
                        + "You can fill in the health history form on the booking site before your visit.", Body));
                }
                else
                {
                    doc.Add(new Paragraph("Please arrive 10 minutes early. You can check in at the kiosk in the lobby with your confirmation code.", Body));
                }

                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("If you need to cancel or reschedule, please call " + PhoneNumber.Format(data.SitePhone)
                    + " at least 24 hours before your appointment. Late cancellations and missed appointments may be charged.", Body));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Sincerely,\nPatient Services", Body));
                doc.Add(new Paragraph("MRN " + data.Mrn, Small));
            });
        }

        public static byte[] Statement(StatementData data, DateTime statementDate, DateTime dueDate, decimal amountDue, string message)
        {
            return Build(delegate(Document doc)
            {
                AddLetterhead(doc);
                PdfPTable header = new PdfPTable(2);
                header.WidthPercentage = 100;
                PdfPCell address = new PdfPCell(new Phrase(data.FirstName + " " + data.LastName + "\n" + data.Address1 + "\n"
                    + (string.IsNullOrEmpty(data.Address2) ? string.Empty : data.Address2 + "\n") + data.City + ", " + data.State + " " + data.Zip, Body));
                address.Border = Rectangle.NO_BORDER;
                header.AddCell(address);
                PdfPTable box = new PdfPTable(2);
                AddRow(box, "Statement date", statementDate.ToString("MM/dd/yyyy", Us));
                AddRow(box, "Account (MRN)", data.Mrn);
                AddRow(box, "Amount due", amountDue.ToString("C", Us));
                AddRow(box, "Please pay by", dueDate.ToString("MM/dd/yyyy", Us));
                PdfPCell boxCell = new PdfPCell(box);
                boxCell.Border = Rectangle.NO_BORDER;
                header.AddCell(boxCell);
                header.SpacingAfter = 12;
                doc.Add(header);

                PdfPTable lines = new PdfPTable(new[] { 1.1f, 3.2f, 1f, 1f, 1f, 1f });
                lines.WidthPercentage = 100;
                lines.HeaderRows = 1;
                foreach (string h in new[] { "Date", "Service", "Charge", "Insurance", "Adjustment", "You owe" })
                {
                    PdfPCell cell = new PdfPCell(new Phrase(h, Heading));
                    cell.GrayFill = 0.9f;
                    lines.AddCell(cell);
                }

                foreach (StatementLine l in data.Lines)
                {
                    lines.AddCell(new Phrase(l.DateOfService.ToString("MM/dd/yy", Us), Small));
                    lines.AddCell(new Phrase(l.CptCode + " " + l.Description + " (" + l.ProviderName + ", " + l.SiteName + ")", Small));
                    lines.AddCell(AmountCell(l.Amount * l.Units));
                    lines.AddCell(AmountCell(-l.InsurancePaid));
                    lines.AddCell(AmountCell(-l.Adjustment));
                    lines.AddCell(AmountCell(l.PatientAmount));
                }

                foreach (StatementPayment p in data.Payments)
                {
                    lines.AddCell(new Phrase(p.PostedOn.ToString("MM/dd/yy", Us), Small));
                    lines.AddCell(new Phrase("Payment, thank you (" + p.Method + (string.IsNullOrEmpty(p.Reference) ? string.Empty : " " + p.Reference) + ")", Small));
                    lines.AddCell(new Phrase(string.Empty));
                    lines.AddCell(new Phrase(string.Empty));
                    lines.AddCell(new Phrase(string.Empty));
                    lines.AddCell(AmountCell(-p.Amount));
                }

                doc.Add(lines);
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(message, Heading));
                doc.Add(new Paragraph("Pay online at the patient booking site, by phone at (253) 555-0199 or mail a check payable to Southridge Medical Group, PO Box 1180, Tacoma, WA 98401.", Small));

                PdfPTable stub = new PdfPTable(3);
                stub.WidthPercentage = 100;
                stub.SpacingBefore = 30;
                stub.AddCell(new Phrase("Account " + data.Mrn, Body));
                stub.AddCell(new Phrase("Amount due " + amountDue.ToString("C", Us), Body));
                stub.AddCell(new Phrase("Amount enclosed $__________", Body));
                doc.Add(new Paragraph("- - - - - - - - - - - - - - - - - - - - detach and return with your payment - - - - - - - - - - - - - - - - - - - -", Small));
                doc.Add(stub);
            });
        }

        public static byte[] ReferralLetter(ReferralLetterData data)
        {
            return Build(delegate(Document doc)
            {
                AddLetterhead(doc);
                AddAddress(doc, new LetterAddress { Name = data.ReferringProviderName, Line1 = data.ReferringPractice });
                doc.Add(new Paragraph(DateTime.Today.ToString("MMMM d, yyyy", Us), Body));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Referral " + data.ReferralNumber + " - " + (data.Approved ? "accepted" : "not accepted"), Title));
                PdfPTable table = new PdfPTable(2);
                table.WidthPercentage = 80;
                table.SetWidths(new[] { 1f, 2f });
                table.SpacingBefore = 8;
                table.SpacingAfter = 8;
                AddRow(table, "Patient", data.PatientName);
                AddRow(table, "Date of birth", data.PatientDateOfBirth.ToString("MM/dd/yyyy", Us));
                AddRow(table, "Specialty", data.Specialty);
                if (data.Approved)
                {
                    AddRow(table, "Authorization", string.IsNullOrEmpty(data.AuthorizationNumber) ? "Not required" : data.AuthorizationNumber);
                    AddRow(table, "Visits", data.VisitsAuthorized.HasValue ? data.VisitsAuthorized.Value.ToString(Us) : "As needed");
                }

                doc.Add(table);
                doc.Add(new Paragraph(data.Approved
                    ? "Thank you for your referral. Our scheduling team will contact the patient to book the first visit and we will send you a visit summary afterwards."
                    : "Thank you for your referral. We are unable to accept it for the reason below. Please contact our referral desk if you have questions.", Body));
                if (!string.IsNullOrEmpty(data.DecisionNote))
                {
                    Section(doc, "Note from the referral desk", data.DecisionNote);
                }

                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Referral desk, Southridge Medical Group\nPhone (253) 555-0150, fax (253) 555-0151", Body));
                doc.Add(new Paragraph("Reviewed by " + data.DecidedBy, Small));
            });
        }

        public static byte[] VisitSummary(VisitSummaryData data)
        {
            return Build(delegate(Document doc)
            {
                AddLetterhead(doc);
                doc.Add(new Paragraph("After Visit Summary", Title));
                PdfPTable info = new PdfPTable(4);
                info.WidthPercentage = 100;
                info.SpacingBefore = 6;
                AddRow(info, "Patient", data.PatientName);
                AddRow(info, "MRN", data.Mrn);
                AddRow(info, "Date of birth", data.DateOfBirth.ToString("MM/dd/yyyy", Us));
                AddRow(info, "Visit date", data.DateOfService.ToString("MM/dd/yyyy", Us));
                AddRow(info, "Provider", data.ProviderName);
                AddRow(info, "Location", data.SiteName);
                doc.Add(info);

                Section(doc, "Reason for visit", data.ChiefComplaint);
                Section(doc, "Vital signs", data.Vitals);
                Section(doc, "Diagnoses", string.Join("\n", data.Diagnoses.ToArray()));
                Section(doc, "Services", string.Join("\n", data.Procedures.ToArray()));
                if (data.Results.Count > 0)
                {
                    doc.Add(new Paragraph("Recent results", Heading));
                    PdfPTable results = new PdfPTable(new[] { 3f, 1.4f, 1.6f, 0.6f, 1.4f });
                    results.WidthPercentage = 100;
                    foreach (string h in new[] { "Test", "Value", "Range", "Flag", "Date" })
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(h, Heading));
                        cell.GrayFill = 0.9f;
                        results.AddCell(cell);
                    }

                    foreach (string[] r in data.Results)
                    {
                        foreach (string v in r)
                        {
                            results.AddCell(new Phrase(v ?? string.Empty, Small));
                        }
                    }

                    doc.Add(results);
                }

                Section(doc, "Instructions from your provider", data.Notes);
                if (data.NextAppointment.HasValue)
                {
                    Section(doc, "Your next appointment", data.NextAppointment.Value.ToString("dddd, MMMM d, yyyy h:mm tt", Us));
                }
            });
        }

        private delegate void Compose(Document doc);

        private static byte[] Build(Compose compose)
        {
            using (var stream = new MemoryStream())
            {
                var doc = new Document(PageSize.LETTER, 54, 54, 54, 54);
                PdfWriter writer = PdfWriter.GetInstance(doc, stream);
                writer.CloseStream = false;
                doc.AddAuthor("Southridge Medical Group");
                doc.AddCreator("Southridge Clinic Systems");
                doc.Open();
                compose(doc);
                doc.Close();
                return stream.ToArray();
            }
        }

        private static void AddLetterhead(Document doc)
        {
            doc.Add(new Paragraph("Southridge Medical Group", Title));
            doc.Add(new Paragraph("1200 Northgate Way, Tacoma, WA 98403  |  (253) 555-0100  |  southridgemedical.example", Small));
            doc.Add(new Paragraph(" "));
        }

        private static void AddAddress(Document doc, LetterAddress to)
        {
            if (to == null)
            {
                return;
            }

            doc.Add(new Paragraph(to.Name + "\n" + to.Line1 + (string.IsNullOrEmpty(to.Line2) ? string.Empty : "\n" + to.Line2) + "\n" + to.City + ", " + to.State + " " + to.Zip, Body));
            doc.Add(new Paragraph(" "));
        }

        private static void AddRow(PdfPTable table, string label, string value)
        {
            PdfPCell l = new PdfPCell(new Phrase(label, Heading));
            l.GrayFill = 0.95f;
            table.AddCell(l);
            table.AddCell(new Phrase(value ?? string.Empty, Body));
        }

        private static PdfPCell AmountCell(decimal amount)
        {
            PdfPCell cell = new PdfPCell(new Phrase(amount == 0 ? string.Empty : amount.ToString("C", Us), Small));
            cell.HorizontalAlignment = Element.ALIGN_RIGHT;
            return cell;
        }

        private static void Section(Document doc, string heading, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Paragraph h = new Paragraph(heading, Heading);
            h.SpacingBefore = 8;
            doc.Add(h);
            doc.Add(new Paragraph(text, Body));
        }
    }
}
