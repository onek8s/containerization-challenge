using System.Management;

namespace Litware.DocControl.Platform;

public sealed record HostDetails(
    string MachineName,
    string Domain,
    int LogicalProcessors,
    long TotalMemoryBytes,
    string OperatingSystem,
    string OsVersion,
    DateTime? LastBootUtc);

/// <summary>Machine details from WMI for the diagnostics page and for sizing the worker pools.</summary>
public static class HostInfo
{
    private static readonly Lazy<HostDetails> Cached = new(Query);

    public static HostDetails Current => Cached.Value;

    private static HostDetails Query()
    {
        string name = Environment.MachineName, domain = "", caption = "", version = "";
        int cpus = 1;
        long memory = 0;
        DateTime? boot = null;

        using (var searcher = new ManagementObjectSearcher("SELECT Name, Domain, NumberOfLogicalProcessors, TotalPhysicalMemory FROM Win32_ComputerSystem"))
        {
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    name = mo["Name"] as string ?? name;
                    domain = mo["Domain"] as string ?? "";
                    cpus = Convert.ToInt32(mo["NumberOfLogicalProcessors"] ?? 1);
                    memory = Convert.ToInt64(mo["TotalPhysicalMemory"] ?? 0L);
                }
            }
        }

        using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, LastBootUpTime FROM Win32_OperatingSystem"))
        {
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    caption = (mo["Caption"] as string ?? "").Trim();
                    version = mo["Version"] as string ?? "";
                    if (mo["LastBootUpTime"] is string dmtf)
                        boot = ManagementDateTimeConverter.ToDateTime(dmtf).ToUniversalTime();
                }
            }
        }

        return new HostDetails(name, domain, cpus, memory, caption, version, boot);
    }
}
