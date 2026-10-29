using System.Runtime.InteropServices;

namespace StudyData.Coding.Coderis;

// Coderis AutoCode 6 type library (CoderisAC6.tlb). The engine is a 32-bit in-process server
// (CoderisAC6.dll), registered with an AppID that runs it in the COM surrogate (dllhost.exe) so 64-bit
// processes can use it out of process.

internal static class CoderisIds
{
    public const string ProgId = "Coderis.AutoCoder.6";
    public static readonly Guid Clsid = new("8B1F3C27-5D4E-4A61-9E0B-2C7D41A9F6E3");
    public const string IAutoCoder = "4E9A2B71-0C3D-4F58-A6E1-93B7D2C05A18";
}

[ComImport]
[Guid(CoderisIds.IAutoCoder)]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
internal interface IAutoCoder
{
    [DispId(1)]
    void OpenDictionary([MarshalAs(UnmanagedType.BStr)] string dictionary, [MarshalAs(UnmanagedType.BStr)] string version,
        [MarshalAs(UnmanagedType.BStr)] string licenceFile);

    [DispId(2)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string DictionaryInfo([MarshalAs(UnmanagedType.BStr)] string dictionary);

    /// <summary>
    /// Returns a SAFEARRAY of BSTR, one candidate per element:
    /// code \t term \t preferred code \t preferred term \t hierarchy \t score.
    /// </summary>
    [DispId(3)]
    [return: MarshalAs(UnmanagedType.Struct)]
    object Code([MarshalAs(UnmanagedType.BStr)] string dictionary, [MarshalAs(UnmanagedType.BStr)] string verbatim, int maxCandidates, int flags);

    [DispId(4)]
    void CloseAll();

    [DispId(5)]
    int LastError { get; }
}

internal static class Ole32
{
    public const uint ClsctxLocalServer = 0x4;

    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Interface)]
    public static extern object CoCreateInstance([In] ref Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] object? outer, uint context, [In] ref Guid iid);
}
