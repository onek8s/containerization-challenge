using System.Globalization;
using System.Text.Json;

namespace PolicyDesk.Lab;

public sealed class Policy
{
    public string Number { get; set; } = "";
    public string Holder { get; set; } = "";
    public string Product { get; set; } = "";
    public string Status { get; set; } = "In force";
    public DateTime StartDate { get; set; }
    public decimal AnnualPremium { get; set; }
    public string? LockedBy { get; set; }
    public Address Address { get; set; } = new();
    public Payment Payment { get; set; } = new();
    public List<Beneficiary> Beneficiaries { get; set; } = new();
    public List<Endorsement> Endorsements { get; set; } = new();

    public bool InForce => Status == "In force";
    public bool IsMotor => Product.StartsWith("Motor", StringComparison.Ordinal);
    public bool IsLife => Product.StartsWith("Life", StringComparison.Ordinal);
}

public sealed class Address
{
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "FI";

    public override string ToString() => $"{Street}, {PostalCode} {City}, {Country}";
}

public sealed class Payment
{
    public string Method { get; set; } = "Invoice";
    public string Iban { get; set; } = "";
    public string AccountHolder { get; set; } = "";
    public int BillingDay { get; set; } = 15;

    public override string ToString() =>
        Method == "Direct debit" ? $"Direct debit {Iban} ({AccountHolder}), day {BillingDay}" : $"Invoice, day {BillingDay}";
}

public sealed class Beneficiary
{
    public string Name { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public int Share { get; set; }
}

public sealed class Endorsement
{
    public string Number { get; set; } = "";
    public DateTime Effective { get; set; }
    public string Type { get; set; } = "";
    public string EnteredBy { get; set; } = "";
    public DateTime EnteredAt { get; set; }
}

/// <summary>Policies of the lab environment, kept in a JSON file under ProgramData.</summary>
public sealed class LabData
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PolicyDesk", "lab", "policies.json");

    public int NextEndorsement { get; set; } = 6004101;
    public List<Policy> Policies { get; set; } = new();

    public static LabData Load()
    {
        if (!File.Exists(FilePath))
        {
            var seeded = Seed();
            seeded.Save();
            return seeded;
        }
        // Shared by every desk session on the machine.
        using var stream = OpenShared(FileMode.Open);
        return JsonSerializer.Deserialize<LabData>(stream, Json) ?? Seed();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        using var stream = OpenShared(FileMode.Create);
        JsonSerializer.Serialize(stream, this, Json);
    }

    private static FileStream OpenShared(FileMode mode)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(FilePath, mode, mode == FileMode.Open ? FileAccess.Read : FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(100);
            }
        }
    }

    public Policy? Find(string number) =>
        Policies.FirstOrDefault(p => string.Equals(p.Number, number.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Re-reads the file, applies the change to the policy and saves; returns the endorsement number.</summary>
    public static string Endorse(string policyNumber, DateTime effective, string type, string user, Action<Policy> change)
    {
        var data = Load();
        var policy = data.Find(policyNumber) ?? throw new InvalidOperationException($"Policy {policyNumber} was not found.");
        change(policy);
        var number = data.NextEndorsement++.ToString(CultureInfo.InvariantCulture);
        policy.Endorsements.Add(new Endorsement { Number = number, Effective = effective, Type = type, EnteredBy = user, EnteredAt = DateTime.Now });
        data.Save();
        return number;
    }

    private static LabData Seed()
    {
        var today = DateTime.Today;
        return new LabData
        {
            Policies =
            {
                new Policy
                {
                    Number = "HL-1042118", Holder = "Aino Virtanen", Product = "Motor Comprehensive", StartDate = today.AddYears(-2).AddDays(17),
                    AnnualPremium = 684.20m,
                    Address = new Address { Street = "Hämeenkatu 12 B 7", PostalCode = "33100", City = "TAMPERE", Country = "FI" },
                    Payment = new Payment { Method = "Direct debit", Iban = "FI21 1234 5600 0007 85", AccountHolder = "Aino Virtanen", BillingDay = 5 },
                },
                new Policy
                {
                    Number = "HL-1042126", Holder = "Mikko Lahtinen", Product = "Home Plus", StartDate = today.AddMonths(-7),
                    AnnualPremium = 412.00m,
                    Address = new Address { Street = "Rantakatu 4", PostalCode = "90100", City = "OULU", Country = "FI" },
                },
                new Policy
                {
                    Number = "HL-2056331", Holder = "Erik Lindqvist", Product = "Life Term 20", StartDate = today.AddYears(-6),
                    AnnualPremium = 1290.00m,
                    Address = new Address { Street = "Storgatan 18", PostalCode = "411 38", City = "GÖTEBORG", Country = "SE" },
                    Payment = new Payment { Method = "Direct debit", Iban = "SE45 5000 0000 0583 9825 7466", AccountHolder = "Erik Lindqvist", BillingDay = 25 },
                    Beneficiaries = { new Beneficiary { Name = "Karin Lindqvist", BirthDate = new DateTime(1979, 3, 14), Share = 100 } },
                },
                new Policy
                {
                    Number = "HL-3008711", Holder = "Laura Nieminen", Product = "Motor Third Party", StartDate = today.AddYears(-1).AddDays(-40),
                    AnnualPremium = 298.50m, Status = "Cancelled",
                    Address = new Address { Street = "Kauppakatu 3", PostalCode = "40100", City = "JYVÄSKYLÄ", Country = "FI" },
                },
                new Policy
                {
                    Number = "HL-3008729", Holder = "Juha Mäkelä", Product = "Home Basic", StartDate = today.AddYears(-3),
                    AnnualPremium = 233.10m, LockedBy = "MKORHONEN",
                    Address = new Address { Street = "Puistokatu 9", PostalCode = "70110", City = "KUOPIO", Country = "FI" },
                },
                new Policy
                {
                    Number = "HL-4120903", Holder = "Sofia Berg", Product = "Life Savings", StartDate = today.AddDays(-20),
                    AnnualPremium = 2400.00m,
                    Address = new Address { Street = "Mannerheimintie 40 A 12", PostalCode = "00100", City = "HELSINKI", Country = "FI" },
                },
            },
        };
    }
}
