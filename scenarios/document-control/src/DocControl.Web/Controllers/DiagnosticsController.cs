using System.Reflection;
using System.Runtime.InteropServices;
using Litware.DocControl.Data.Storage;
using Litware.DocControl.Packaging;
using Litware.DocControl.Platform;
using Litware.DocControl.Web.Models;
using Litware.DocControl.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Litware.DocControl.Web.Controllers;

[Authorize(Policy = Policies.Controllers)]
public sealed class DiagnosticsController : Controller
{
    private readonly StorageOptions _storage;
    private readonly IWebHostEnvironment _env;

    public DiagnosticsController(IOptions<StorageOptions> storage, IWebHostEnvironment env)
    {
        _storage = storage.Value;
        _env = env;
    }

    public IActionResult Index()
    {
        var model = new DiagnosticsViewModel();
        void Add(string name, Func<string> value)
        {
            try { model.Rows.Add((name, value())); }
            catch (Exception ex) { model.Rows.Add((name, $"({ex.GetType().Name}: {ex.Message.Split('\n')[0].Trim()})")); }
        }

        Add("Version", () => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "");
        Add("Environment", () => _env.EnvironmentName);
        Add("Runtime", () => RuntimeInformation.FrameworkDescription + " " + RuntimeInformation.ProcessArchitecture);
        Add("Process user", () => Environment.UserDomainName + "\\" + Environment.UserName);
        Add("Content root", () => _env.ContentRootPath);
        Add("Install path", () => InstallLocation.InstallPath("Web"));

        Add("Server", () => $"{HostInfo.Current.MachineName}.{HostInfo.Current.Domain}");
        Add("Operating system", () => $"{HostInfo.Current.OperatingSystem} ({HostInfo.Current.OsVersion})");
        Add("Processors / memory", () => $"{HostInfo.Current.LogicalProcessors} / {CoverSheet.FormatSize(HostInfo.Current.TotalMemoryBytes)}");
        Add("Last boot", () => HostInfo.Current.LastBootUtc?.ToString("u") ?? "");

        foreach (var (name, path) in new[] { ("Vault", _storage.VaultRoot), ("Transmittals", _storage.TransmittalRoot), ("Drop", _storage.DropRoot) })
        {
            Add(name, () => path);
            Add(name + " free space", () =>
            {
                var space = DiskSpace.Get(path);
                return $"{CoverSheet.FormatSize((long)space.FreeBytesAvailable)} of {CoverSheet.FormatSize((long)space.TotalBytes)} ({space.FreePercent:0.0} %)";
            });
        }

        return View(model);
    }
}
