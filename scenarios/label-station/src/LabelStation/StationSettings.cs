using Microsoft.Win32;

namespace LabelStation;

/// <summary>
/// Per-operator settings stored under HKEY_CURRENT_USER.
/// </summary>
public sealed class StationSettings
{
    private const string KeyPath = @"Software\Northwind\LabelStation";

    public string PrinterName { get; set; } = "";
    public int LabelWidthMm { get; set; } = 100;
    public int LabelHeightMm { get; set; } = 150;
    public string DefaultCarrier { get; set; } = "PostNord";
    public string StationId { get; set; } = Environment.MachineName;

    public static StationSettings Load()
    {
        var settings = new StationSettings();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key is null)
        {
            return settings;
        }

        settings.PrinterName = key.GetValue(nameof(PrinterName)) as string ?? settings.PrinterName;
        settings.LabelWidthMm = key.GetValue(nameof(LabelWidthMm)) as int? ?? settings.LabelWidthMm;
        settings.LabelHeightMm = key.GetValue(nameof(LabelHeightMm)) as int? ?? settings.LabelHeightMm;
        settings.DefaultCarrier = key.GetValue(nameof(DefaultCarrier)) as string ?? settings.DefaultCarrier;
        settings.StationId = key.GetValue(nameof(StationId)) as string ?? settings.StationId;
        return settings;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(nameof(PrinterName), PrinterName, RegistryValueKind.String);
        key.SetValue(nameof(LabelWidthMm), LabelWidthMm, RegistryValueKind.DWord);
        key.SetValue(nameof(LabelHeightMm), LabelHeightMm, RegistryValueKind.DWord);
        key.SetValue(nameof(DefaultCarrier), DefaultCarrier, RegistryValueKind.String);
        key.SetValue(nameof(StationId), StationId, RegistryValueKind.String);
    }
}
