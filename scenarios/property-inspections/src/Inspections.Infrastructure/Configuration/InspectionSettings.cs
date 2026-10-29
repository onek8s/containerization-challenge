using System;
using System.Globalization;
using System.Security.Principal;
using Microsoft.Win32;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Security;

namespace Northwind.Inspections.Infrastructure.Configuration
{
    /// <summary>
    /// Machine settings written by deploy\Set-InspectionSettings.ps1 to HKLM\SOFTWARE\Northwind Insurance\Inspections.
    /// Passwords are stored per service account under the Secrets subkey, protected with DPAPI for that account.
    /// </summary>
    public sealed class InspectionSettings
    {
        private static readonly Lazy<InspectionSettings> CurrentInstance = new Lazy<InspectionSettings>(Load);

        public static InspectionSettings Current
        {
            get { return CurrentInstance.Value; }
        }

        public string ConnectionString { get; private set; }
        public string PhotoShare { get; private set; }
        public string ShareUser { get; private set; }
        public string SmtpHost { get; private set; }
        public int SmtpPort { get; private set; }
        public string SmtpFrom { get; private set; }
        public string SmtpUser { get; private set; }
        public string PortalUrl { get; private set; }
        public int MaxPhotoEdge { get; private set; }
        public int ThumbnailEdge { get; private set; }
        public long JpegQuality { get; private set; }
        public string WatermarkFont { get; private set; }
        public int PurgeAfterDays { get; private set; }
        public int ReportBatchSize { get; private set; }
        public string WorkFolder { get; private set; }

        public string SharePassword
        {
            get { return ReadSecret(SettingNames.SharePassword); }
        }

        public string SmtpPassword
        {
            get { return ReadSecret(SettingNames.SmtpPassword); }
        }

        public static InspectionSettings Load()
        {
            using (var key = OpenKey(false))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Registry key HKLM\\" + SettingNames.RegistryKey + " was not found. Run deploy\\Set-InspectionSettings.ps1.");
                }

                return new InspectionSettings
                {
                    ConnectionString = Required(key, SettingNames.ConnectionString),
                    PhotoShare = Required(key, SettingNames.PhotoShare),
                    ShareUser = Optional(key, SettingNames.ShareUser, null),
                    SmtpHost = Optional(key, SettingNames.SmtpHost, "smtp.corp.local"),
                    SmtpPort = Number(key, SettingNames.SmtpPort, 25),
                    SmtpFrom = Optional(key, SettingNames.SmtpFrom, "inspections@northwind-insurance.example"),
                    SmtpUser = Optional(key, SettingNames.SmtpUser, null),
                    PortalUrl = Optional(key, SettingNames.PortalUrl, "https://inspections.corp.local"),
                    MaxPhotoEdge = Number(key, SettingNames.MaxPhotoEdge, 2048),
                    ThumbnailEdge = Number(key, SettingNames.ThumbnailEdge, 320),
                    JpegQuality = Number(key, SettingNames.JpegQuality, 85),
                    WatermarkFont = Optional(key, SettingNames.WatermarkFont, "Segoe UI"),
                    PurgeAfterDays = Number(key, SettingNames.PurgeAfterDays, 400),
                    ReportBatchSize = Number(key, SettingNames.ReportBatchSize, 25),
                    WorkFolder = Optional(key, SettingNames.WorkFolder, @"C:\ProgramData\Northwind\Inspections\Work")
                };
            }
        }

        /// <summary>Registry value name of a secret for the account running this process, e.g. CORP_svc-insp-svc.</summary>
        public static string SecretKeyForCurrentAccount()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return SettingNames.SecretsSubKey + "\\" + identity.Name.Replace('\\', '_');
            }
        }

        public static void WriteSecret(string name, string plainText)
        {
            var protectedBytes = SecretProtector.Protect(plainText);
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = root.CreateSubKey(SettingNames.RegistryKey + "\\" + SecretKeyForCurrentAccount()))
            {
                key.SetValue(name, protectedBytes, RegistryValueKind.Binary);
            }
        }

        private static string ReadSecret(string name)
        {
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = root.OpenSubKey(SettingNames.RegistryKey + "\\" + SecretKeyForCurrentAccount(), false))
            {
                var value = key == null ? null : key.GetValue(name) as byte[];
                if (value == null)
                {
                    return null;
                }
                return SecretProtector.Unprotect(value);
            }
        }

        private static RegistryKey OpenKey(bool writable)
        {
            var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            return root.OpenSubKey(SettingNames.RegistryKey, writable);
        }

        private static string Required(RegistryKey key, string name)
        {
            var value = key.GetValue(name) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Registry value " + name + " is missing under HKLM\\" + SettingNames.RegistryKey + ".");
            }
            return Environment.ExpandEnvironmentVariables(value);
        }

        private static string Optional(RegistryKey key, string name, string fallback)
        {
            var value = key.GetValue(name) as string;
            return string.IsNullOrWhiteSpace(value) ? fallback : Environment.ExpandEnvironmentVariables(value);
        }

        private static int Number(RegistryKey key, string name, int fallback)
        {
            var value = key.GetValue(name);
            if (value is int)
            {
                return (int)value;
            }
            int parsed;
            return value != null && int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) ? parsed : fallback;
        }
    }
}
