using System.Runtime.InteropServices;

namespace FurnaceRecords.Acquisition.Opc;

// OPC Data Access 2.05a custom interfaces (subset used for cache reads).

internal enum OpcDataSource
{
    Cache = 1,
    Device = 2,
}

internal enum OpcServerState
{
    Running = 1,
    Failed = 2,
    NoConfig = 3,
    Suspended = 4,
    Test = 5,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OpcItemDef
{
    [MarshalAs(UnmanagedType.LPWStr)] public string? AccessPath;
    [MarshalAs(UnmanagedType.LPWStr)] public string ItemId;
    public int Active;
    public int ClientHandle;
    public int BlobSize;
    public IntPtr Blob;
    public short RequestedDataType;
    public short Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct OpcItemResult
{
    public int ServerHandle;
    public short CanonicalDataType;
    public short Reserved;
    public int AccessRights;
    public int BlobSize;
    public IntPtr Blob;
}

[StructLayout(LayoutKind.Sequential)]
internal struct OpcItemState
{
    public int ClientHandle;

    // FILETIME is two DWORDs (4-byte aligned).
    public uint TimestampLow;
    public uint TimestampHigh;
    public short Quality;
    public short Reserved;
    public VariantBuffer Value;

    public long Timestamp => ((long)TimestampHigh << 32) | TimestampLow;
}

/// <summary>Space for a VARIANT, decoded with <see cref="Marshal.GetObjectForNativeVariant(IntPtr)"/>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct VariantBuffer
{
    public long A;
    public long B;
    public long C;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OpcServerStatus
{
    public long StartTime;
    public long CurrentTime;
    public long LastUpdateTime;
    public OpcServerState ServerState;
    public int GroupCount;
    public int BandWidth;
    public short MajorVersion;
    public short MinorVersion;
    public short BuildNumber;
    public short Reserved;
    [MarshalAs(UnmanagedType.LPWStr)] public string VendorInfo;
}

[ComImport]
[Guid("39C13A4D-011E-11D0-9675-0020AFD8ADB3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOPCServer
{
    void AddGroup(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        [MarshalAs(UnmanagedType.Bool)] bool active,
        int requestedUpdateRate,
        int clientGroup,
        IntPtr timeBias,
        IntPtr percentDeadband,
        int localeId,
        out int serverGroup,
        out int revisedUpdateRate,
        ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown, IidParameterIndex = 9)] out object group);

    void GetErrorString(int error, int localeId, [MarshalAs(UnmanagedType.LPWStr)] out string text);

    void GetGroupByName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown, IidParameterIndex = 1)] out object group);

    void GetStatus(out IntPtr status);

    void RemoveGroup(int serverGroup, [MarshalAs(UnmanagedType.Bool)] bool force);

    void CreateGroupEnumerator(int scope, ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown, IidParameterIndex = 1)] out object enumerator);
}

[ComImport]
[Guid("39C13A54-011E-11D0-9675-0020AFD8ADB3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOPCItemMgt
{
    void AddItems(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] OpcItemDef[] items,
        out IntPtr results, out IntPtr errors);

    void ValidateItems(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] OpcItemDef[] items,
        [MarshalAs(UnmanagedType.Bool)] bool blobUpdate, out IntPtr results, out IntPtr errors);

    void RemoveItems(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] serverHandles, out IntPtr errors);

    void SetActiveState(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] serverHandles,
        [MarshalAs(UnmanagedType.Bool)] bool active, out IntPtr errors);

    void SetClientHandles(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] serverHandles,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] clientHandles, out IntPtr errors);

    void SetDatatypes(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] serverHandles,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] short[] requestedTypes, out IntPtr errors);

    void CreateEnumerator(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown, IidParameterIndex = 0)] out object enumerator);
}

[ComImport]
[Guid("39C13A52-011E-11D0-9675-0020AFD8ADB3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOPCSyncIO
{
    void Read(OpcDataSource source, int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] serverHandles,
        out IntPtr itemValues, out IntPtr errors);

    void Write(int count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] serverHandles,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] object[] values, out IntPtr errors);
}
