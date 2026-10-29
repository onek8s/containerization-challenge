using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CityPermits.Gis.Geomatix;

/// <summary>
/// Hosts the Geomatix Pro SDK (CoreHost) in this process. The SDK assemblies ship with the
/// Geomatix Pro installation and are loaded from its bin folder so they always match the
/// installed build and its native engine. Host.Initialize must run on an STA thread and every
/// later SDK call must be made from that same thread, so all work is queued to one engine thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GeomatixRuntime : IDisposable
{
    private readonly GisOptions _options;
    private readonly ILogger<GeomatixRuntime> _logger;
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Assembly? _core;
    private Assembly? _hosting;
    private Assembly? _mapping;

    public GeomatixRuntime(IOptions<GisOptions> options, ILogger<GeomatixRuntime> logger)
    {
        _options = options.Value;
        _logger = logger;
        _thread = new Thread(Run) { IsBackground = true, Name = "Geomatix engine" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public string? LicenceStatus { get; private set; }
    public string? EngineVersion { get; private set; }
    public dynamic? Replica { get; private set; }
    public dynamic? MapDocument { get; private set; }

    private string BinDir => Path.Combine(_options.InstallDir, "bin");

    private void Run()
    {
        try
        {
            AssemblyLoadContext.Default.Resolving += ResolveFromInstall;
            _hosting = Load("Geomatix.Core.Hosting");
            _core = Load("Geomatix.Core");
            _mapping = Load("Geomatix.Mapping");

            // Geomatix.Core.Hosting.Host.Initialize(LicenseProduct) checks out the licence that this
            // machine is authorised for (Licence Administrator, single use authorisation file).
            var hostType = _hosting.GetType("Geomatix.Core.Hosting.Host", true)!;
            var productType = _hosting.GetType("Geomatix.Core.Hosting.LicenseProduct", true)!;
            var product = Enum.Parse(productType, _options.LicenceLevel);
            hostType.GetMethod("Initialize", new[] { productType })!.Invoke(null, new[] { product });
            LicenceStatus = hostType.GetProperty("LicenseStatus")!.GetValue(null)?.ToString();
            EngineVersion = hostType.GetProperty("Version")!.GetValue(null)?.ToString();
            if (LicenceStatus != "CheckedOut")
                throw new InvalidOperationException($"Geomatix licence {_options.LicenceLevel} not available: {LicenceStatus}.");

            Replica = OpenFileGeodatabase(_options.ReplicaPath);
            MapDocument = Static(_mapping, "Geomatix.Mapping.MapDocument", "Open", _options.MapDocument);
            _logger.LogInformation("Geomatix {Version} initialised, licence {Level} {Status}, replica {Replica}",
                EngineVersion, _options.LicenceLevel, LicenceStatus, _options.ReplicaPath);
            _ready.SetResult();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Geomatix engine could not be initialised from {InstallDir}", _options.InstallDir);
            _ready.SetException(ex);
            return;
        }

        foreach (var work in _queue.GetConsumingEnumerable())
            work();

        TryShutdown();
    }

    private Assembly Load(string name) =>
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(BinDir, name + ".dll"));

    private Assembly? ResolveFromInstall(AssemblyLoadContext ctx, AssemblyName name)
    {
        var path = Path.Combine(BinDir, name.Name + ".dll");
        return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
    }

    public dynamic OpenFileGeodatabase(string path)
    {
        var connType = _core!.GetType("Geomatix.Core.Data.FileGeodatabaseConnectionPath", true)!;
        var conn = Activator.CreateInstance(connType, new Uri(path))!;
        return Activator.CreateInstance(_core.GetType("Geomatix.Core.Data.Geodatabase", true)!, conn)!;
    }

    public dynamic OpenEnterpriseGeodatabase(string connectionFile)
    {
        var connType = _core!.GetType("Geomatix.Core.Data.DatabaseConnectionFile", true)!;
        var conn = Activator.CreateInstance(connType, new Uri(connectionFile))!;
        return Activator.CreateInstance(_core.GetType("Geomatix.Core.Data.Geodatabase", true)!, conn)!;
    }

    public object Static(Assembly? asm, string type, string method, params object?[] args)
    {
        var t = (asm ?? _core)!.GetType(type, true)!;
        return t.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == method && m.GetParameters().Length == args.Length)
            .Invoke(null, args)!;
    }

    public Assembly Core => _core!;
    public Assembly Mapping => _mapping!;

    /// <summary>The engine's geometry operations (GeometryEngine.Instance).</summary>
    public dynamic Geometry => _core!.GetType("Geomatix.Core.Geometry.GeometryEngine", true)!.GetProperty("Instance")!.GetValue(null)!;

    public dynamic SpatialReference(int wkid) => Static(_core, "Geomatix.Core.Geometry.SpatialReferenceBuilder", "CreateSpatialReference", wkid);

    public dynamic NewObject(string type, params object?[] args) => Activator.CreateInstance((type.StartsWith("Geomatix.Mapping") ? _mapping : _core)!.GetType(type, true)!, args)!;

    /// <summary>Runs <paramref name="work"/> on the engine thread and waits for the result.</summary>
    public T Invoke<T>(Func<GeomatixRuntime, T> work)
    {
        if (!_ready.Task.Wait(TimeSpan.FromSeconds(_options.EngineTimeoutSeconds)))
            throw new TimeoutException("Geomatix engine did not start in time.");
        _ready.Task.GetAwaiter().GetResult();
        if (Thread.CurrentThread == _thread) return work(this);

        var tcs = new TaskCompletionSource<T>();
        _queue.Add(() =>
        {
            try { tcs.SetResult(work(this)); }
            catch (Exception ex) { tcs.SetException(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex); }
        });
        if (!tcs.Task.Wait(TimeSpan.FromSeconds(_options.EngineTimeoutSeconds)))
            throw new TimeoutException("Geomatix engine call timed out.");
        return tcs.Task.GetAwaiter().GetResult();
    }

    /// <summary>Re-opens the replica after the nightly synchronisation replaced it.</summary>
    public void ReopenReplica() => Invoke(rt =>
    {
        (Replica as IDisposable)?.Dispose();
        Replica = OpenFileGeodatabase(_options.ReplicaPath);
        return true;
    });

    private void TryShutdown()
    {
        try
        {
            (Replica as IDisposable)?.Dispose();
            _hosting?.GetType("Geomatix.Core.Hosting.Host")?.GetMethod("Shutdown")?.Invoke(null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Geomatix shutdown failed");
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
    }
}
