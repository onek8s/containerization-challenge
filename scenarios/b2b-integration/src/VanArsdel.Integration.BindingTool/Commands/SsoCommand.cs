using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EnterpriseSingleSignOn.Interop;
using Newtonsoft.Json;
using VanArsdel.Integration.Deployment;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.BindingTool.Commands
{
    /// <summary>
    /// Writes the SSO configuration store application the BizTalk runtime reads (SsoSettings). The application is
    /// recreated when its field list changed, because SSO cannot add fields to an existing application.
    /// </summary>
    internal static class SsoCommand
    {
        private const string Contact = "b2b-support@vanarsdel.com";

        public static readonly string[] Required =
        {
            SettingNames.IntegrationDb, SettingNames.ArchiveRoot, SettingNames.ErpServiceUrl,
            SettingNames.ErpClientId, SettingNames.ErpClientSecret, SettingNames.SmtpHost, SettingNames.AlertFrom
        };

        public static int Run(SsoOptions o, TextWriter output)
        {
            var settings = new EnvironmentSettings("file", Path.GetFileName(o.Settings), SettingsFile.Load(o.Settings));
            var secrets = string.IsNullOrEmpty(o.Secrets)
                ? new Dictionary<string, string>()
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(o.Secrets));
            var sso = SsoApplicationSettings.FromEnvironment(settings, secrets);

            var missing = sso.MissingRequired(Required).ToList();
            if (missing.Count > 0)
            {
                output.WriteLine("Missing SSO settings: " + string.Join(", ", missing));
                return 3;
            }

            foreach (var kv in sso.Values)
            {
                output.WriteLine("{0,-32} {1}", kv.Key, IsSecret(kv.Key) ? "********" : kv.Value);
            }

            if (o.DryRun)
            {
                output.WriteLine("Dry run: nothing written.");
                return 0;
            }

            var admin = (ISSOAdmin)new SSOAdmin();
            var fields = sso.Values.Keys.ToList();
            if (!FieldsMatch(admin, fields.Count))
            {
                Recreate(admin, fields, o, output);
            }

            var bag = new SsoPropertyBag();
            foreach (var kv in sso.Values)
            {
                object value = kv.Value;
                bag.Write(kv.Key, ref value);
            }

            var store = (ISSOConfigStore)new SSOConfigStore();
            store.SetConfigInfo(SsoApplicationSettings.ApplicationName, SsoApplicationSettings.Identifier, bag);
            admin.PurgeCacheForApplication(SsoApplicationSettings.ApplicationName);
            output.WriteLine("Wrote {0} settings to SSO application {1}; restart the BizTalk host instances to drop their 10 minute cache.", fields.Count, SsoApplicationSettings.ApplicationName);
            return 0;
        }

        private static bool IsSecret(string name)
        {
            return name.IndexOf("Secret", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("ConnectionString", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool FieldsMatch(ISSOAdmin admin, int fieldCount)
        {
            try
            {
                string description, contact, users, admins;
                int flags, numFields;
                admin.GetApplicationInfo(SsoApplicationSettings.ApplicationName, out description, out contact, out users, out admins, out flags, out numFields);

                // the first field is the reserved dummy field every config store application starts with
                return numFields == fieldCount + 1;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return false;
            }
        }

        private static void Recreate(ISSOAdmin admin, IList<string> fields, SsoOptions o, TextWriter output)
        {
            try
            {
                admin.DeleteApplication(SsoApplicationSettings.ApplicationName);
                output.WriteLine("Deleted SSO application {0} (field list changed)", SsoApplicationSettings.ApplicationName);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // did not exist
            }

            var appFlags = SSOFlag.SSO_FLAG_APP_CONFIG_STORE | SSOFlag.SSO_FLAG_SSO_WINDOWS_TO_EXTERNAL | SSOFlag.SSO_FLAG_APP_ALLOW_LOCAL;
            admin.CreateApplication(SsoApplicationSettings.ApplicationName, "VanArsdel B2B integration settings", Contact, o.UserGroup, o.AdminGroup, appFlags, fields.Count + 1);
            admin.CreateFieldInfo(SsoApplicationSettings.ApplicationName, "dummy", SSOFlag.SSO_FLAG_NONE);
            foreach (var field in fields)
            {
                admin.CreateFieldInfo(SsoApplicationSettings.ApplicationName, field, SSOFlag.SSO_FLAG_NONE);
            }

            admin.UpdateApplication(SsoApplicationSettings.ApplicationName, null, null, null, null, SSOFlag.SSO_FLAG_ENABLED, SSOFlag.SSO_FLAG_ENABLED);
            output.WriteLine("Created SSO application {0} with {1} fields", SsoApplicationSettings.ApplicationName, fields.Count);
        }
    }
}
