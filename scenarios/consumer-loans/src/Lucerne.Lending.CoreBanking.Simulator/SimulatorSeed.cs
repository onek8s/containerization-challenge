using Dapper;
using Lucerne.Lending.Domain;
using Microsoft.Data.Sqlite;

namespace Lucerne.Lending.CoreBanking.Simulator;

/// <summary>Training customers; the same personal identity codes are used by the bureau stub fixtures.</summary>
internal static class SimulatorSeed
{
    public static void Apply(SqliteConnection cn)
    {
        using var tx = cn.BeginTransaction();
        var today = DateOnly.FromDateTime(DateTime.Today);
        cn.Execute("INSERT INTO busdate(id, date) VALUES (1, @d)", new { d = SimulatorDatabase.D(today) }, tx);
        foreach (var (code, rate) in new[] { ("E12", 3.512m), ("E3M", 3.287m), ("FIX", 0m) })
            cn.Execute("INSERT INTO refrate(code, valid, rate) VALUES (@code, @valid, @rate)",
                new { code, valid = SimulatorDatabase.D(today.AddDays(-today.Day + 1)), rate }, tx);

        Customer(cn, tx, "0004102331", "150385-2481", "VIRTANEN AINO MARIA", "Hämeenkatu 12 B 7", "33100", "TAMPERE", "PRI", "C", "2009-05-12", "2A");
        Customer(cn, tx, "0004418702", "020790-317C", "KORHONEN MIKKO JUHANI", "Kauppakatu 3 A 1", "40100", "JYVÄSKYLÄ", "PRI", "C", "2015-02-03", "4B");
        Customer(cn, tx, "0003005518", "231178-426K", "NIEMINEN LAURA KATARIINA", "Pohjoisranta 20", "00170", "HELSINKI", "PRE", "C", "2001-11-20", "1A");
        Customer(cn, tx, "0005220914", "110601A139N", "MÄKINEN JUHA PETTERI", "Opiskelijankatu 4 C 31", "33720", "TAMPERE", "YNG", "C", "2021-08-30", "5C");
        Customer(cn, tx, "0004999120", "300888-562M", "HÄMÄLÄINEN SANNA LIISA", "Rantatie 8", "20100", "TURKU", "PRI", "C", "2017-04-18", "6D");
        Customer(cn, tx, "0001208833", "051259-2152", "LAINE PEKKA OLAVI", "Koivukuja 2", "15100", "LAHTI", "SEN", "C", "1994-03-01", "2A");
        Customer(cn, tx, "0004771205", "080883-3776", "KOSKINEN TUOMAS MATIAS", "Satamakatu 9", "48100", "KOTKA", "PRI", "C", "2016-10-10", "7E", blocked: true);
        Customer(cn, tx, "0005301166", "271095-482J", "JÄRVINEN KAISA ELINA", "Kirkkokatu 15 A 4", "90100", "OULU", "YNG", "R", "2020-06-15", "4B");
        Customer(cn, tx, "0003911078", "140772-159C", "LEHTONEN VILLE ANTERO", "Teollisuuskatu 44", "28100", "PORI", "PRI", "C", "2008-01-22", "8E");
        Customer(cn, tx, "0004418755", "121186-236D", "KORHONEN ANNA SOFIA", "Kauppakatu 3 A 1", "40100", "JYVÄSKYLÄ", "PRI", "C", "2015-02-03", "3B");

        Account(cn, tx, "47901200018400", "0004102331", "KAYT", "Current account", 2410.55m);
        Account(cn, tx, "47901200022600", "0004418702", "KAYT", "Current account", 380.10m);
        Account(cn, tx, "47901200023400", "0004418755", "KAYT", "Current account", 1250.00m);
        Account(cn, tx, "47901200031700", "0003005518", "KAYT", "Current account", 18830.42m);
        Account(cn, tx, "47901200032500", "0003005518", "SAST", "Savings account", 45200.00m);
        Account(cn, tx, "47901200040800", "0005220914", "KAYT", "Current account", 155.27m);
        Account(cn, tx, "47901200051500", "0004999120", "KAYT", "Current account", 12.00m);
        Account(cn, tx, "47901200061400", "0001208833", "KAYT", "Current account", 6120.00m);
        Account(cn, tx, "47901200071300", "0005301166", "KAYT", "Current account", 820.65m);
        Account(cn, tx, "47901200081200", "0003911078", "KAYT", "Current account", 0.00m);

        // existing loans: one current, one 12 days past due, one in collections
        Loan(cn, tx, "479085100011", "0004102331", "CL01", 8000m, 5120.33m, 7.9m, 36, today.AddMonths(-14), 0m, 0, today);
        Loan(cn, tx, "479085100029", "0004418702", "CL02", 18000m, 14402.10m, 6.4m, 60, today.AddMonths(-9), 352.15m, 12, today);
        Loan(cn, tx, "479085100037", "0003911078", "CL01", 6000m, 4870.00m, 11.9m, 48, today.AddMonths(-20), 712.40m, 95, today);
        cn.Execute("INSERT INTO seq(name, value) VALUES ('loan', 100040), ('cif', 5400000), ('txn', 900000)", transaction: tx);
        tx.Commit();
    }

    private static void Customer(SqliteConnection cn, SqliteTransaction tx, string cif, string hetu, string name, string street,
        string postal, string city, string segment, string kyc, string since, string rating, bool blocked = false) =>
        cn.Execute(@"INSERT INTO customer(cif, hetu, name, street, postal, city, segment, kyc, since, rating, blocked, branch)
                     VALUES (@cif, @hetu, @name, @street, @postal, @city, @segment, @kyc, @since, @rating, @blocked, '0901')",
            new { cif, hetu, name, street, postal, city, segment, kyc, since, rating, blocked = blocked ? 1 : 0 }, tx);

    private static void Account(SqliteConnection cn, SqliteTransaction tx, string acct, string cif, string product, string type, decimal balance) =>
        cn.Execute(@"INSERT INTO account(acct, iban, cif, product, type, balance, role, opened)
                     VALUES (@acct, @iban, @cif, @product, @type, @balance, 'OWN', '2015-01-01')",
            new { acct, iban = ToIban(acct), cif, product, type, balance }, tx);

    private static void Loan(SqliteConnection cn, SqliteTransaction tx, string acct, string cif, string product, decimal original,
        decimal balance, decimal rate, int term, DateOnly opened, decimal arrears, int dpd, DateOnly today)
    {
        var instalment = Annuity.MonthlyPayment(original, rate, term);
        var nextDue = new DateOnly(today.Year, today.Month, 15);
        if (nextDue <= today) nextDue = nextDue.AddMonths(1);
        cn.Execute(@"INSERT INTO loan(acct, cif, product, original, balance, rate, margin, refrate, monthly_fee, instalment, due_day,
                       next_due, arrears, oldest_unpaid, dpd, status, opened, term, disbursed)
                     VALUES (@acct, @cif, @product, @original, @balance, @rate, @rate, 'FIX', 2.5, @instalment, 15, @nextDue,
                       @arrears, @oldest, @dpd, @status, @opened, @term, 1)",
            new
            {
                acct, cif, product, original, balance, rate, instalment, nextDue = SimulatorDatabase.D(nextDue), arrears,
                oldest = dpd > 0 ? SimulatorDatabase.D(today.AddDays(-dpd)) : null, dpd,
                status = dpd >= 90 ? "COLL" : "ACTV", opened = SimulatorDatabase.D(opened), term
            }, tx);
        cn.Execute(@"INSERT INTO txn(acct, date, type, amount, principal, interest, fees, balance_after)
                     VALUES (@acct, @date, 'DB', @original, @original, 0, 0, @original)",
            new { acct, date = SimulatorDatabase.D(opened), original }, tx);
        var paid = original;
        for (var d = opened.AddMonths(1); d < today.AddDays(-dpd) && paid > balance; d = d.AddMonths(1))
        {
            var interest = Money.Round(paid * rate / 1200m);
            var principal = Math.Min(paid - balance, instalment - interest);
            paid -= principal;
            cn.Execute(@"INSERT INTO txn(acct, date, type, amount, principal, interest, fees, balance_after)
                         VALUES (@acct, @date, 'PM', @amount, @principal, @interest, 2.5, @paid)",
                new { acct, date = SimulatorDatabase.D(d), amount = principal + interest + 2.5m, principal, interest, paid }, tx);
        }
    }

    public static string ToIban(string bban)
    {
        // FI + check digits + 14 digit BBAN
        var digits = bban + "1518" + "00"; // F=15, I=18
        var mod = 0;
        foreach (var c in digits) mod = (mod * 10 + (c - '0')) % 97;
        return $"FI{98 - mod:00}{bban}";
    }
}
