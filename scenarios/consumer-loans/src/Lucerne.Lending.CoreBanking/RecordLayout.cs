namespace Lucerne.Lending.CoreBanking;

public enum FieldType
{
    /// <summary>EBCDIC character (CCSID 278).</summary>
    Char,
    /// <summary>Packed decimal (COMP-3).</summary>
    Packed,
    /// <summary>Zoned decimal.</summary>
    Zoned
}

public sealed record FieldSpec(string Name, FieldType Type, int Length, int Decimals = 0)
{
    /// <summary>Length in bytes in the program parameter (packed: digits/2+1).</summary>
    public int ByteLength => Type == FieldType.Packed ? Length / 2 + 1 : Length;
}

/// <summary>Fixed layout of an RPG program parameter or data queue entry (see legacy/core/qrpglesrc).</summary>
public sealed class RecordLayout
{
    private readonly Dictionary<string, (FieldSpec Spec, int Offset)> _byName = new(StringComparer.OrdinalIgnoreCase);

    public RecordLayout(string name, params FieldSpec[] fields)
    {
        Name = name;
        Fields = fields;
        var offset = 0;
        foreach (var f in fields)
        {
            _byName.Add(f.Name, (f, offset));
            offset += f.ByteLength;
        }
        Length = offset;
    }

    public string Name { get; }
    public IReadOnlyList<FieldSpec> Fields { get; }
    public int Length { get; }

    public FieldSpec this[string name] => _byName[name].Spec;
    public int OffsetOf(string name) => _byName[name].Offset;
}

public static class CoreLayouts
{
    public const int CodePage = 278;

    // CUSTINQ: customer inquiry by HETU or CIF
    public static readonly RecordLayout CustInqIn = new("CUSTINQ.IN",
        new("HETU", FieldType.Char, 11),
        new("CIF", FieldType.Char, 10));

    public static readonly RecordLayout CustInqOut = new("CUSTINQ.OUT",
        new("RC", FieldType.Char, 2),
        new("CIF", FieldType.Char, 10),
        new("HETU", FieldType.Char, 11),
        new("NAME", FieldType.Char, 60),
        new("STREET", FieldType.Char, 40),
        new("POSTAL", FieldType.Char, 5),
        new("CITY", FieldType.Char, 30),
        new("SEGMENT", FieldType.Char, 3),
        new("KYC", FieldType.Char, 1),
        new("SINCE", FieldType.Zoned, 8),
        new("RATING", FieldType.Char, 2),
        new("DECEASED", FieldType.Char, 1),
        new("BLOCKED", FieldType.Char, 1),
        new("BRANCH", FieldType.Char, 4),
        new("MSG", FieldType.Char, 78));

    // CUSCRT: create customer (prospect)
    public static readonly RecordLayout CusCrtIn = new("CUSCRT.IN",
        new("HETU", FieldType.Char, 11),
        new("FNAME", FieldType.Char, 30),
        new("LNAME", FieldType.Char, 30),
        new("STREET", FieldType.Char, 40),
        new("POSTAL", FieldType.Char, 5),
        new("CITY", FieldType.Char, 30),
        new("CITIZEN", FieldType.Char, 2),
        new("EMAIL", FieldType.Char, 60),
        new("PHONE", FieldType.Char, 20),
        new("BRANCH", FieldType.Char, 4),
        new("OFFICER", FieldType.Char, 10));

    public static readonly RecordLayout CusCrtOut = new("CUSCRT.OUT",
        new("RC", FieldType.Char, 2),
        new("CIF", FieldType.Char, 10),
        new("MSG", FieldType.Char, 78));

    // LNBOOK: open a loan account
    public static readonly RecordLayout LnBookIn = new("LNBOOK.IN",
        new("APPREF", FieldType.Char, 20),
        new("CIF", FieldType.Char, 10),
        new("COCIF", FieldType.Char, 10),
        new("PRODUCT", FieldType.Char, 4),
        new("AMOUNT", FieldType.Packed, 13, 2),
        new("TERM", FieldType.Packed, 3, 0),
        new("REFRATE", FieldType.Char, 3),
        new("MARGIN", FieldType.Packed, 7, 4),
        new("RATE", FieldType.Packed, 7, 4),
        new("ARRFEE", FieldType.Packed, 9, 2),
        new("MTHFEE", FieldType.Packed, 7, 2),
        new("DUEDAY", FieldType.Packed, 3, 0),
        new("FIRSTDUE", FieldType.Zoned, 8),
        new("BRANCH", FieldType.Char, 4),
        new("OFFICER", FieldType.Char, 10));

    public static readonly RecordLayout LnBookOut = new("LNBOOK.OUT",
        new("RC", FieldType.Char, 2),
        new("ACCOUNT", FieldType.Char, 12),
        new("MSG", FieldType.Char, 78));

    // LNDISB: disburse to own account or create outgoing SEPA transfer
    public static readonly RecordLayout LnDisbIn = new("LNDISB.IN",
        new("ACCOUNT", FieldType.Char, 12),
        new("AMOUNT", FieldType.Packed, 13, 2),
        new("IBAN", FieldType.Char, 34),
        new("TEXT", FieldType.Char, 70),
        new("OFFICER", FieldType.Char, 10));

    public static readonly RecordLayout LnDisbOut = new("LNDISB.OUT",
        new("RC", FieldType.Char, 2),
        new("TXNID", FieldType.Char, 20),
        new("EXTERNAL", FieldType.Char, 1),
        new("MSG", FieldType.Char, 78));

    // LNFEE: charge a fee to a loan account
    public static readonly RecordLayout LnFeeIn = new("LNFEE.IN",
        new("ACCOUNT", FieldType.Char, 12),
        new("FEECODE", FieldType.Char, 4),
        new("AMOUNT", FieldType.Packed, 9, 2),
        new("TEXT", FieldType.Char, 40));

    // LNPPLN: payment plan, up to 12 instalments
    public static readonly RecordLayout LnPplnIn = BuildPlanLayout();

    // LNTERM: terminate (accelerate) the loan
    public static readonly RecordLayout LnTermIn = new("LNTERM.IN",
        new("ACCOUNT", FieldType.Char, 12),
        new("EFFDATE", FieldType.Zoned, 8),
        new("OFFICER", FieldType.Char, 10));

    public static readonly RecordLayout ResultOut = new("RESULT.OUT",
        new("RC", FieldType.Char, 2),
        new("MSG", FieldType.Char, 78));

    // Data queue LNLIB/LNEVTQ entry, 64 bytes
    public static readonly RecordLayout EventEntry = new("LNEVTQ",
        new("TYPE", FieldType.Char, 4),
        new("ACCOUNT", FieldType.Char, 12),
        new("DATE", FieldType.Zoned, 8),
        new("TIME", FieldType.Zoned, 6),
        new("AMOUNT", FieldType.Packed, 13, 2),
        new("ARREARS", FieldType.Packed, 13, 2),
        new("DPD", FieldType.Zoned, 4),
        new("FILLER", FieldType.Char, 16));

    private static RecordLayout BuildPlanLayout()
    {
        var fields = new List<FieldSpec>
        {
            new("ACCOUNT", FieldType.Char, 12),
            new("OFFICER", FieldType.Char, 10),
            new("COUNT", FieldType.Packed, 3, 0)
        };
        for (var i = 1; i <= 12; i++)
        {
            fields.Add(new FieldSpec($"DUE{i:00}", FieldType.Zoned, 8));
            fields.Add(new FieldSpec($"AMT{i:00}", FieldType.Packed, 11, 2));
        }
        return new RecordLayout("LNPPLN.IN", fields.ToArray());
    }
}

public static class CoreReturnCodes
{
    public const string Ok = "00";
    public const string NotFound = "NF";
    public const string Duplicate = "DU";
    public const string Locked = "LK";
    public const string Rejected = "RJ";
    public const string SystemError = "SE";

    public static string Describe(string rc) => rc switch
    {
        Ok => "OK",
        NotFound => "Not found",
        Duplicate => "Already exists",
        Locked => "Record locked by another job",
        Rejected => "Rejected by core validation",
        SystemError => "Core system error",
        _ => "Unknown return code " + rc
    };
}
