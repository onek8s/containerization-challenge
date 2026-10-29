using System.Runtime.InteropServices;
using System.Text;

namespace FreightRates.RatingHost.Interop;

// Declarations follow vendor/Meridian/RateEngine-7.2/include/mrsrate.h.
internal static class NativeMethods
{
    public const string EngineDll = "MRSRATE32.DLL";

    public const int MR_MAX_LINES = 20;

    public const int MR_FLAG_SINGLE_SHIPMENT = 0x0001;
    public const int MR_FLAG_DEFICIT_RATING = 0x0002;
    public const int MR_FLAG_TRANSIT_DAYS = 0x0004;

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false)]
    public static extern int MR_Initialize(string dataPath, string licenceFile, string password, out int session);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int MR_Terminate(int session);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int MR_RateShipment(
        int session,
        ref MR_SHIPMENT_HEADER header,
        [In] MR_LINE_ITEM[] lines,
        int lineCount,
        ref MR_RATE_RESULT result);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern int MR_GetTariffInfo(int session, StringBuilder tariffName, int tariffNameLen, out int effectiveDate);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int MR_ReloadTariffs(int session);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern int MR_GetKeyStatus(ref MR_KEY_STATUS status);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern int MR_GetVersion(StringBuilder version, int versionLen);

    [DllImport(EngineDll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern int MR_GetLastErrorText(int session, StringBuilder text, int textLen);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetDllDirectory(string lpPathName);
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
internal struct MR_SHIPMENT_HEADER
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
    public string OriginZip;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
    public string DestZip;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
    public string OriginCountry;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
    public string DestCountry;
    public int ShipDate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 13)]
    public string TariffName;
    public int Flags;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct MR_LINE_ITEM
{
    public int ClassX10;
    public int WeightLbs;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct MR_LINE_RESULT
{
    public int RatePerCwtCents;
    public int ChargeCents;
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
internal struct MR_RATE_RESULT
{
    public int Status;
    public int TotalCents;
    public int MinimumChargeCents;
    public int DeficitWeightLbs;
    public int DeficitChargeCents;
    public int TransitDays;
    public int LineCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = NativeMethods.MR_MAX_LINES)]
    public MR_LINE_RESULT[] Lines;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 13)]
    public string TariffUsed;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
    public string OriginTerminal;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
    public string DestTerminal;

    public static MR_RATE_RESULT Create() => new()
    {
        Lines = new MR_LINE_RESULT[NativeMethods.MR_MAX_LINES],
        TariffUsed = "",
        OriginTerminal = "",
        DestTerminal = "",
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
internal struct MR_KEY_STATUS
{
    public int Present;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string Serial;
    public int LicenceDaysLeft;
    public int Seats;
    public int SeatsInUse;
}
