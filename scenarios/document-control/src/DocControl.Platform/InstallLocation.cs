using Microsoft.Win32;

namespace Litware.DocControl.Platform;

/// <summary>
/// The setup writes <c>HKLM\SOFTWARE\Litware\DocControl\&lt;Component&gt;</c> with <c>InstallPath</c>
/// (binaries, templates, logo) and <c>DataPath</c> (work folders). When the key is missing,
/// for example on a developer machine, the application folder is used.
/// </summary>
public static class InstallLocation
{
    public const string RootKey = @"SOFTWARE\Litware\DocControl";

    public static string InstallPath(string component) =>
        Read(component, "InstallPath") ?? AppContext.BaseDirectory;

    public static string DataPath(string component) =>
        Read(component, "DataPath") ?? Path.Combine(AppContext.BaseDirectory, "App_Data");

    public static string? Read(string component, string valueName)
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey($@"{RootKey}\{component}");
        var value = key?.GetValue(valueName) as string;
        return string.IsNullOrWhiteSpace(value) ? null : Environment.ExpandEnvironmentVariables(value);
    }
}
