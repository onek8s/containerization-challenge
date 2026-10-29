using System.Globalization;
using Lucerne.Lending.Data;
using Lucerne.Lending.Domain;

namespace Lucerne.Lending.Documents.Rendering;

public sealed record LoanDocumentModel(ApplicationRecord Application, LoanProduct Product, OfferRow Offer, DecisionRow? Decision);

/// <summary>Customer documents of the origination process.</summary>
public static class LoanDocuments
{
    private static readonly CultureInfo Fi = CultureInfo.GetCultureInfo("fi-FI");

    public static string Eur(decimal v) => v.ToString("#,##0.00", Fi) + " EUR";
    public static string Pct(decimal v) => v.ToString("0.00", Fi) + " %";

    /// <summary>Standard European Consumer Credit Information (Directive 2008/48/EC Annex II).</summary>
    public static byte[] PreContractInformation(LoanDocumentModel m)
    {
        var app = m.Application.Row;
        var main = m.Application.Applicants.First(a => a.IsMain);
        var o = m.Offer;
        using var pdf = new PdfBuilder("Standard European Consumer Credit Information", app.Reference);
        pdf.Address(main.FullName, main.StreetAddress, main.PostalCode, main.City)
            .Heading("STANDARD EUROPEAN CONSUMER CREDIT INFORMATION")
            .Text($"Application {app.Reference}. This information is valid until {o.ValidUntilUtc.ToLocalTime():d.M.yyyy}.")
            .SubHeading("1. Identity and contact details of the creditor")
            .KeyValues(new[] { ("Creditor", "Lucerne Savings Bank"), ("Address", "Aleksanterinkatu 30, 00100 Helsinki"), ("Telephone", "+358 10 555 0100") })
            .SubHeading("2. Description of the main features of the credit product")
            .KeyValues(new[]
            {
                ("Type of credit", m.Product.Name),
                ("Total amount of credit", Eur(o.Amount)),
                ("Conditions governing the drawdown", "Paid in one instalment to the account named by the borrower after the agreement is signed."),
                ("Duration of the credit agreement", $"{o.TermMonths} months"),
                ("Instalments", $"{o.TermMonths} monthly instalments of {Eur(o.MonthlyPayment + o.MonthlyFee)} due on day {app.DueDay} of each month"),
                ("Total amount you will have to pay", Eur(o.TotalRepayable)),
                ("Security", m.Product.RequiresCollateral ? "The financed vehicle (ownership reservation)" : "None")
            })
            .SubHeading("3. Costs of the credit")
            .KeyValues(new[]
            {
                ("Borrowing rate", o.ReferenceRateCode == "FIX"
                    ? $"Fixed {Pct(o.NominalRatePercent)}"
                    : $"Variable: 12 month Euribor ({Pct(o.ReferenceRatePercent)}) + margin {Pct(o.MarginPercent)} = {Pct(o.NominalRatePercent)}; reviewed every 12 months, the reference rate is never below 0 %"),
                ("Annual percentage rate of charge (APR)", Pct(o.AprPercent) + $" (calculated on {Eur(o.Amount)} over {o.TermMonths} months with the fees below)"),
                ("Arrangement fee", Eur(o.ArrangementFee)),
                ("Monthly account fee", Eur(o.MonthlyFee)),
                ("Costs in the case of late payments", "Penalty interest according to the Interest Act (reference rate + 7 %), reminder fee 5,00 EUR (max. 3 times)")
            })
            .SubHeading("4. Other important legal aspects")
            .Text("Right of withdrawal: you may withdraw from the credit agreement within 14 calendar days from signing. " +
                  "Early repayment: you may repay the credit in full or in part at any time without charge. " +
                  "Consultation of a database: the creditor has checked your credit information from Nordic Credit Information Ltd.");
        return pdf.Build();
    }

    public static byte[] LoanAgreement(LoanDocumentModel m)
    {
        var app = m.Application.Row;
        var o = m.Offer;
        var schedule = Annuity.Schedule(o.Amount, o.NominalRatePercent, o.TermMonths,
            DueDates.FirstDueDate(DateOnly.FromDateTime(DateTime.Today), app.DueDay), o.MonthlyFee);
        using var pdf = new PdfBuilder("Consumer credit agreement", app.Reference);
        pdf.Heading($"CONSUMER CREDIT AGREEMENT {app.Reference}")
            .SubHeading("Parties")
            .KeyValues(m.Application.Applicants.Select(a => (a.IsMain ? "Borrower" : "Co-borrower",
                $"{a.FullName}, {PersonalIdentityCode.Mask(a.PersonalIdentityCode)}, {a.StreetAddress}, {a.PostalCode} {a.City}")).Append(("Creditor", "Lucerne Savings Bank")))
            .SubHeading("Credit")
            .KeyValues(new[]
            {
                ("Product", m.Product.Name),
                ("Amount of credit", Eur(o.Amount)),
                ("Term", $"{o.TermMonths} months"),
                ("Borrowing rate", Pct(o.NominalRatePercent) + (o.ReferenceRateCode == "FIX" ? " fixed" : $" (12 month Euribor + {Pct(o.MarginPercent)})")),
                ("APR", Pct(o.AprPercent)),
                ("Monthly instalment", Eur(o.MonthlyPayment + o.MonthlyFee)),
                ("Arrangement fee", Eur(o.ArrangementFee)),
                ("Total amount payable", Eur(o.TotalRepayable)),
                ("Disbursement account", app.DisbursementIban == null ? "-" : Iban.Format(app.DisbursementIban)),
                ("Joint and several liability", m.Application.Applicants.Count > 1 ? "The borrowers are jointly and severally liable for the credit." : "-")
            })
            .SubHeading("General terms")
            .Text("The general terms for consumer credit of Lucerne Savings Bank (version 1.3.2022) form part of this agreement. " +
                  "The borrower confirms having received the Standard European Consumer Credit Information and the general terms before signing. " +
                  "The agreement enters into force when signed electronically by all borrowers.")
            .NewPage()
            .Heading("Repayment schedule (estimate)")
            .Table(new[] { "#", "Due date", "Instalment", "Interest", "Principal", "Fee", "Balance" },
                schedule.Select(l => new[]
                {
                    l.Number.ToString(), l.DueDate.ToString("d.M.yyyy"), l.Payment.ToString("0.00", Fi), l.Interest.ToString("0.00", Fi),
                    l.Principal.ToString("0.00", Fi), l.Fee.ToString("0.00", Fi), l.Balance.ToString("0.00", Fi)
                }), new[] { 0.6f, 1.4f, 1.3f, 1.2f, 1.3f, 0.9f, 1.4f })
            .SignatureLines(m.Application.Applicants.Select(a => a.FullName));
        return pdf.Build();
    }

    public static byte[] DeclineLetter(ApplicationRecord application, DecisionRow? decision, bool bureauUsed)
    {
        var main = application.Applicants.First(a => a.IsMain);
        using var pdf = new PdfBuilder("Credit decision", application.Row.Reference);
        pdf.Address(main.FullName, main.StreetAddress, main.PostalCode, main.City)
            .Heading("Decision on your loan application")
            .Text($"Thank you for your loan application {application.Row.Reference} of {Eur(application.Row.Amount)}. " +
                  "We have carefully assessed the application and unfortunately we cannot grant the loan.")
            .Text(bureauUsed
                ? "The decision is based in part on information obtained from the credit information register of Nordic Credit Information Ltd " +
                  "(PL 16, 00101 Helsinki). You have the right to check the information recorded about you in the register (Credit Information Act 474/2007, 36 §)."
                : "The decision is based on the information in your application and our credit policy.")
            .Text("If you have questions about the decision, please contact our customer service on +358 10 555 0100 (Mon-Fri 8-18).")
            .Text("Kind regards,\nLucerne Savings Bank, Consumer Lending");
        return pdf.Build();
    }
}
