using System;
using System.Configuration;

namespace Contoso.Purchasing.Api.Infrastructure
{
    public static class AppSettings
    {
        public static string FinanceGroup { get { return Get("Purchasing.Group.Finance"); } }
        public static string DirectorGroup { get { return Get("Purchasing.Group.Directors"); } }
        public static string BuyerGroup { get { return Get("Purchasing.Group.Buyers"); } }
        public static string AdminGroup { get { return Get("Purchasing.Group.Admins"); } }
        public static string AttachmentRoot { get { return Get("Purchasing.AttachmentRoot"); } }
        public static string DirectoryDomain { get { return ConfigurationManager.AppSettings["Purchasing.DirectoryDomain"]; } }

        /// <summary>
        /// Accounts (normally the portal app pool identity) that may call the API on behalf of another user
        /// by sending the X-Purchasing-User header. Used where Kerberos delegation is not configured.
        /// </summary>
        public static string[] TrustedCallers
        {
            get
            {
                var value = ConfigurationManager.AppSettings["Purchasing.TrustedCallers"] ?? "";
                return value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public static int MaxAttachmentBytes
        {
            get { return int.Parse(Get("Purchasing.MaxAttachmentKb")) * 1024; }
        }

        private static string Get(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
            {
                throw new ConfigurationErrorsException("appSettings '" + key + "' is missing.");
            }
            return value;
        }
    }
}
