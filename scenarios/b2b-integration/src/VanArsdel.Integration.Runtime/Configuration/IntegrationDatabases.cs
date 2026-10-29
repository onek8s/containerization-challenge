using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using VanArsdel.Integration.Data;

namespace VanArsdel.Integration.Runtime.Configuration
{
    /// <summary>The application database for code running in a BizTalk host: the connection string lives in SSO, not in BTSNTSvc64.exe.config.</summary>
    public static class IntegrationDatabases
    {
        public static IntegrationDatabase FromSso()
        {
            return new IntegrationDatabase(new SqlDatabase(SsoSettings.Get(SettingNames.IntegrationDb)));
        }
    }
}
