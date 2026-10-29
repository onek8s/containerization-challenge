// VB6 Currency (VT_CY) is still the type RateCore uses for money.
#pragma warning disable CS0618

using System.Runtime.InteropServices;

namespace Humongous.Quoting.Rating.Interop;

// Interfaces of RateCore.dll (VB6 ActiveX DLL, see legacy\RateCore). Declared by hand from the
// type library because the SDK build cannot run tlbimp. Member order must match the order of the
// public members in Engine.cls and Risk.cls: the calls go through the vtable.
// Binary compatibility is set in RateCore.vbp, so these GUIDs do not change between releases.

[ComImport]
[Guid("6F1C2B4E-8A3D-4B17-9C55-2E7A1D0B9F31")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface _Engine
{
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool Initialise([MarshalAs(UnmanagedType.BStr)] string tablesPath);

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_Version();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_LastError();

    [return: MarshalAs(UnmanagedType.Interface)]
    _Risk NewRisk([MarshalAs(UnmanagedType.BStr)] string product, DateTime effectiveDate);

    [return: MarshalAs(UnmanagedType.BStr)]
    string TableVersion([MarshalAs(UnmanagedType.BStr)] string product, DateTime effectiveDate);

    void Terminate();
}

[ComImport]
[Guid("6F1C2B4F-8A3D-4B17-9C55-2E7A1D0B9F31")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface _Risk
{
    void SetValue([MarshalAs(UnmanagedType.BStr)] string name, [MarshalAs(UnmanagedType.Struct)] object value);

    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool AddSection([MarshalAs(UnmanagedType.BStr)] string sectionCode);

    void SetSectionValue([MarshalAs(UnmanagedType.BStr)] string sectionCode, [MarshalAs(UnmanagedType.BStr)] string name, [MarshalAs(UnmanagedType.Struct)] object value);

    void AddClaim(DateTime claimDate, [MarshalAs(UnmanagedType.BStr)] string sectionCode, [MarshalAs(UnmanagedType.Currency)] decimal amount);

    int Calculate();

    int get_SectionCount();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_SectionCode(int index);

    [return: MarshalAs(UnmanagedType.Currency)]
    decimal get_SectionPremium(int index);

    double get_SectionRate(int index);

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_SectionBasis(int index);

    int get_MessageCount();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_MessageType(int index);

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_MessageCode(int index);

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_MessageText(int index);

    [return: MarshalAs(UnmanagedType.Struct)]
    object get_Clauses();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_TableVersion();

    [return: MarshalAs(UnmanagedType.BStr)]
    string get_ErrorText();
}

public static class RateCoreErrors
{
    // vbObjectError (0x80040000) + 512 + n, raised by modErrors.bas
    public const int VbObjectError = unchecked((int)0x80040000);

    public static string Describe(int hresult) => (hresult - VbObjectError - 512) switch
    {
        1 => "Rate tables not loaded",
        2 => "Unknown product",
        3 => "No table version for the effective date",
        4 => "Unknown risk field",
        5 => "Unknown section",
        6 => "Rate tables locked by another process",
        7 => "Postcode district not found in the zone table",
        8 => "Trade not found in the trade table",
        _ => $"RateCore error 0x{hresult:X8}"
    };

    // Calculate() return values
    public const int Ok = 0;
    public const int Referred = 1;
    public const int Declined = 2;
}
