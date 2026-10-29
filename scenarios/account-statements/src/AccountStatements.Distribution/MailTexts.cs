using System;
using Woodgrove.Statements.Core.Ledger;
using Woodgrove.Statements.Core.Statements;

namespace Woodgrove.Statements.Distribution
{
    static class MailTexts
    {
        public static string StatementSubject(Statement st)
        {
            var date = st.StatementDate.ToString("dd.MM.yyyy");
            switch (Lang(st.Customer))
            {
                case "fi": return "Tiliote " + date + " - Woodgrove Leasing";
                case "sv": return "Kontoutdrag " + date + " - Woodgrove Leasing";
                default: return "Account statement " + date + " - Woodgrove Leasing";
            }
        }

        public static string StatementBody(Statement st)
        {
            var c = st.Customer;
            var balance = st.ClosingBalance.ToString("#,0.00") + " " + c.CurrencyCode;
            var overdue = st.Aging.Overdue;
            switch (Lang(c))
            {
                case "fi":
                    return "Hyvä asiakkaamme,\r\n\r\nliitteenä on tiliotteenne ajalta " + st.PeriodStart.ToString("d.M.yyyy") + " - " + st.StatementDate.ToString("d.M.yyyy") +
                           ". Saldo tiliotteen päivänä: " + balance + "." +
                           (overdue > 0 ? "\r\nTilillänne on erääntyneitä eriä " + overdue.ToString("#,0.00") + " " + c.CurrencyCode + ". Maksuohjeet ovat tiliotteella." : "") +
                           "\r\n\r\nKysymykset: receivables@woodgrove-leasing.fi, puh. 09 4241 7700.\r\n\r\nYstävällisin terveisin\r\nWoodgrove Leasing Oy, reskontra";
                case "sv":
                    return "Bästa kund,\r\n\r\nbifogat finns ert kontoutdrag för perioden " + st.PeriodStart.ToString("yyyy-MM-dd") + " - " + st.StatementDate.ToString("yyyy-MM-dd") +
                           ". Saldo per kontoutdragsdatum: " + balance + "." +
                           (overdue > 0 ? "\r\nNi har förfallna poster på " + overdue.ToString("#,0.00") + " " + c.CurrencyCode + ". Betalningsinstruktioner finns på kontoutdraget." : "") +
                           "\r\n\r\nFrågor: receivables@woodgrove-leasing.fi, tel. +358 9 4241 7700.\r\n\r\nMed vänliga hälsningar\r\nWoodgrove Leasing Oy, kundreskontra";
                default:
                    return "Dear customer,\r\n\r\nplease find attached your account statement for " + st.PeriodStart.ToString("dd.MM.yyyy") + " - " + st.StatementDate.ToString("dd.MM.yyyy") +
                           ". Balance on the statement date: " + balance + "." +
                           (overdue > 0 ? "\r\nYour account has overdue items of " + overdue.ToString("#,0.00") + " " + c.CurrencyCode + ". Payment instructions are on the statement." : "") +
                           "\r\n\r\nQuestions: receivables@woodgrove-leasing.fi, tel. +358 9 4241 7700.\r\n\r\nKind regards\r\nWoodgrove Leasing Oy, accounts receivable";
            }
        }

        public static string ReportBody(string subscriptionName, string fileName, DateTime runDate)
        {
            return "Attached: " + fileName + "\r\nSubscription: " + subscriptionName + "\r\nCreated: " + runDate.ToString("dd.MM.yyyy HH:mm") +
                   "\r\n\r\nManage your subscriptions at https://statements.corp.local/Subscriptions\r\n";
        }

        static string Lang(Customer c)
        {
            var l = (c.Language ?? "").ToLowerInvariant();
            return l.StartsWith("fi") ? "fi" : l.StartsWith("sv") ? "sv" : "en";
        }
    }
}
