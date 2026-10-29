using System.Runtime.InteropServices;

namespace FurnaceRecords.Acquisition.Board;

/// <summary>TcxBoardLib 4.3 (vendor/Calvera/TcxSdk-4.3/TcxBoard.idl).</summary>
internal static class TcxBoardLib
{
    public const string ProgId = "Calvera.TcxBoard.4";
    public static readonly Guid BoardClsid = new("5C2E9A42-7B1D-4F6A-9C3E-1A8D2F04B731");
}

internal enum TcxThermocouple
{
    TypeK = 1,
    TypeN = 2,
    TypeJ = 3,
    TypeS = 4,
}

internal enum TcxState
{
    Closed = 0,
    Open = 1,
    Running = 2,
    Fault = 3,
}

[ComImport]
[Guid("5C2E9A41-7B1D-4F6A-9C3E-1A8D2F04B731")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface ITcxBoard
{
    [DispId(1)] string Model { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(2)] int SerialNumber { get; }
    [DispId(3)] string FirmwareVersion { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(4)] string DriverVersion { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(5)] int ChannelCount { get; }
    [DispId(6)] int MaxSampleRate { get; }
    [DispId(7)] DateTime CalibrationDate { get; }
    [DispId(8)] TcxState State { get; }
    [DispId(9)] int LostScans { get; }

    [DispId(20)] void Open(int boardIndex);
    [DispId(21)] void Close();
    [DispId(22)] void ConfigureChannel(int channel, TcxThermocouple type, [MarshalAs(UnmanagedType.VariantBool)] bool enabled);
    [DispId(23)] void SetSampleRate(int scansPerSecond);
    [DispId(24)] void Start();
    [DispId(25)] void Stop();

    [DispId(26)]
    int ReadScans(
        int maxScans,
        int timeoutMs,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_DATE)] out DateTime[] times,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_R8)] out double[] coldJunction,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)] out int[] openMask,
        [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)] out int[,] microvolts);
}
