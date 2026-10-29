using Lucerne.Lending.Data;

namespace Lucerne.Lending.Documents.Rendering;

public static class CollectionDocuments
{
    private static string V(IReadOnlyDictionary<string, string> v, string key) => v.TryGetValue(key, out var s) ? s : "";

    public static byte[] Letter(string template, Recipient recipient, IReadOnlyDictionary<string, string> v)
    {
        var (title, body) = template switch
        {
            "REMINDER1" => ("PAYMENT REMINDER",
                $"We have not received the instalment of your loan {V(v, "account")}. The overdue amount is {V(v, "arrears")} EUR " +
                $"(including a reminder fee of {V(v, "fee")} EUR). Please pay it by {V(v, "dueDate")} using reference {V(v, "reference")}. " +
                "If you have already paid, please disregard this reminder."),
            "REMINDER2" => ("SECOND PAYMENT REMINDER",
                $"Your loan {V(v, "account")} is still overdue by {V(v, "dpd")} days. The overdue amount is {V(v, "arrears")} EUR. " +
                $"Please pay by {V(v, "dueDate")} with reference {V(v, "reference")} or contact us to agree a payment plan."),
            "TERMINATION_NOTICE" => ("NOTICE OF POSSIBLE TERMINATION",
                $"Your loan {V(v, "account")} has been overdue for {V(v, "dpd")} days (overdue amount {V(v, "arrears")} EUR). " +
                "Under the Consumer Protection Act (Chapter 7, section 37) the creditor may declare the whole credit due if the delay continues " +
                $"at least one month after this notice. To avoid this, please pay the overdue amount by {V(v, "dueDate")}."),
            "FINAL_DEMAND" => ("FINAL DEMAND",
                $"Despite our earlier reminders the overdue amount of {V(v, "arrears")} EUR on loan {V(v, "account")} is unpaid. " +
                "Unless it is paid within 14 days, the loan will be terminated and handed over to a debt collection agency, " +
                "and the payment default may be registered in the credit information register."),
            _ => throw new ArgumentException($"Unknown letter template {template}.")
        };
        using var pdf = new PdfBuilder(title, V(v, "account"));
        pdf.Address(recipient.Name, recipient.Street, recipient.PostalCode, recipient.City)
            .Heading(title)
            .Text(body)
            .KeyValues(new[]
            {
                ("Loan account", V(v, "account")),
                ("Overdue amount", V(v, "arrears") + " EUR"),
                ("Payment reference", V(v, "reference")),
                ("Recipient account", "FI71 4790 1000 0000 18, LUCEFIHH"),
                ("Due date", V(v, "dueDate"))
            })
            .Text("If you have difficulties paying, contact our collections team on +358 10 555 0177. You can also get free advice from the " +
                  "debt advisory service of the Legal Aid Office.");
        return pdf.Build();
    }

    public static byte[] PlanConfirmation(Recipient recipient, PaymentPlanRow plan, IReadOnlyDictionary<string, string> v)
    {
        var instalments = System.Text.Json.JsonSerializer.Deserialize<List<Domain.PlanInstalment>>(plan.InstalmentsJson)!;
        using var pdf = new PdfBuilder("Payment plan", plan.AccountNumber);
        pdf.Address(recipient.Name, recipient.Street, recipient.PostalCode, recipient.City)
            .Heading("CONFIRMATION OF PAYMENT PLAN")
            .Text($"We have agreed the following payment plan for your loan {plan.AccountNumber}. Please pay the instalments with reference {V(v, "reference")}. " +
                  "If an instalment is more than 14 days late, the plan ends and the normal collection process continues.")
            .Table(new[] { "#", "Due date", "Amount" }, instalments.Select(i => new[] { i.Number.ToString(), i.DueDate.ToString("d.M.yyyy"), LoanDocuments.Eur(i.Amount) }),
                new[] { 0.5f, 2f, 2f });
        return pdf.Build();
    }
}
