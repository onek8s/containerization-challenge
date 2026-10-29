using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FurnaceRecords.Acquisition.Opc;

public sealed record OpcValue(object? Value, short Quality, DateTime TimestampUtc)
{
    /// <summary>OPC quality GOOD (0xC0 and above, ignoring limit bits).</summary>
    public bool IsGood => (Quality & 0xC0) == 0xC0;
}

/// <summary>Minimal OPC DA 2.0 client: one group, cache reads.</summary>
[SupportedOSPlatform("windows")]
internal sealed class OpcDaClient : IDisposable
{
    private const short QualityBad = 0;

    private readonly string _host;
    private readonly string _progId;
    private readonly Guid? _clsid;
    private IOPCServer? _server;
    private object? _group;
    private int _groupHandle;
    private int[] _serverHandles = Array.Empty<int>();
    private string[] _items = Array.Empty<string>();
    private int[] _itemErrors = Array.Empty<int>();

    public OpcDaClient(string host, string progId, Guid? clsid)
    {
        _host = host;
        _progId = progId;
        _clsid = clsid;
    }

    public string? VendorInfo { get; private set; }

    /// <summary>Creates the server (on the remote host through DCOM when one is given) and adds the items.</summary>
    public void Connect(IReadOnlyList<string> itemIds, int updateRateMs)
    {
        var host = string.IsNullOrWhiteSpace(_host) ? null : _host;
        var type = _clsid is { } clsid
            ? Type.GetTypeFromCLSID(clsid, host, throwOnError: true)!
            : Type.GetTypeFromProgID(_progId, host, throwOnError: true)!;
        _server = (IOPCServer)Activator.CreateInstance(type)!;

        _server.GetStatus(out var statusPtr);
        try
        {
            var status = Marshal.PtrToStructure<OpcServerStatus>(statusPtr);
            VendorInfo = $"{status.VendorInfo} {status.MajorVersion}.{status.MinorVersion}.{status.BuildNumber}";
            if (status.ServerState != OpcServerState.Running)
            {
                throw new COMException($"OPC server {_progId} on {_host} is in state {status.ServerState}.");
            }
        }
        finally
        {
            FreeStatus(statusPtr);
        }

        var iid = typeof(IOPCItemMgt).GUID;
        _server.AddGroup("FurnaceRecords", true, updateRateMs, 1, IntPtr.Zero, IntPtr.Zero, 0x409,
            out _groupHandle, out _, ref iid, out _group);

        var defs = itemIds.Select((id, i) => new OpcItemDef { ItemId = id, Active = 1, ClientHandle = i + 1 }).ToArray();
        ((IOPCItemMgt)_group).AddItems(defs.Length, defs, out var resultsPtr, out var errorsPtr);
        try
        {
            _items = itemIds.ToArray();
            _serverHandles = new int[defs.Length];
            _itemErrors = new int[defs.Length];
            Marshal.Copy(errorsPtr, _itemErrors, 0, defs.Length);
            var size = Marshal.SizeOf<OpcItemResult>();
            for (var i = 0; i < defs.Length; i++)
            {
                var r = Marshal.PtrToStructure<OpcItemResult>(resultsPtr + i * size);
                _serverHandles[i] = r.ServerHandle;
                if (r.Blob != IntPtr.Zero) Marshal.FreeCoTaskMem(r.Blob);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(resultsPtr);
            Marshal.FreeCoTaskMem(errorsPtr);
        }
    }

    /// <summary>Items the server rejected, with the server's error text.</summary>
    public IEnumerable<(string Item, string Error)> RejectedItems()
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_itemErrors[i] != 0)
            {
                string text;
                try { _server!.GetErrorString(_itemErrors[i], 0x409, out text); }
                catch (COMException) { text = $"0x{_itemErrors[i]:X8}"; }
                yield return (_items[i], text.Trim());
            }
        }
    }

    public IReadOnlyDictionary<string, OpcValue> Read()
    {
        if (_group is null) throw new InvalidOperationException("Not connected.");

        var valid = Enumerable.Range(0, _items.Length).Where(i => _itemErrors[i] == 0).ToArray();
        var result = _items.Where((_, i) => _itemErrors[i] != 0)
            .ToDictionary(id => id, _ => new OpcValue(null, QualityBad, DateTime.MinValue));
        if (valid.Length == 0) return result;

        var handles = valid.Select(i => _serverHandles[i]).ToArray();
        ((IOPCSyncIO)_group).Read(OpcDataSource.Cache, handles.Length, handles, out var valuesPtr, out var errorsPtr);
        try
        {
            var errors = new int[handles.Length];
            Marshal.Copy(errorsPtr, errors, 0, handles.Length);
            var size = Marshal.SizeOf<OpcItemState>();
            var valueOffset = Marshal.OffsetOf<OpcItemState>(nameof(OpcItemState.Value)).ToInt32();
            for (var k = 0; k < handles.Length; k++)
            {
                var p = valuesPtr + k * size;
                var state = Marshal.PtrToStructure<OpcItemState>(p);
                object? value = null;
                if (errors[k] == 0)
                {
                    value = Marshal.GetObjectForNativeVariant(p + valueOffset);
                }
                VariantClear(p + valueOffset);
                result[_items[valid[k]]] = new OpcValue(value, errors[k] == 0 ? state.Quality : QualityBad,
                    DateTime.FromFileTimeUtc(state.Timestamp));
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(valuesPtr);
            Marshal.FreeCoTaskMem(errorsPtr);
        }
        return result;
    }

    public void Dispose()
    {
        try
        {
            if (_server is not null && _group is not null)
            {
                _server.RemoveGroup(_groupHandle, false);
            }
        }
        catch (COMException)
        {
            // The server may already be gone.
        }
        if (_group is not null) Marshal.FinalReleaseComObject(_group);
        if (_server is not null) Marshal.FinalReleaseComObject(_server);
        _group = null;
        _server = null;
    }

    private static void FreeStatus(IntPtr statusPtr)
    {
        var vendor = Marshal.ReadIntPtr(statusPtr, Marshal.OffsetOf<OpcServerStatus>(nameof(OpcServerStatus.VendorInfo)).ToInt32());
        if (vendor != IntPtr.Zero) Marshal.FreeCoTaskMem(vendor);
        Marshal.FreeCoTaskMem(statusPtr);
    }

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr variant);
}
