using System;

namespace Wingtip.FieldSales.Core
{
    /// <summary>The API refuses order submits from client versions older than the configured minimum.</summary>
    public static class ClientVersionPolicy
    {
        public static bool IsSupported(string clientVersion, string minimumVersion)
        {
            if (string.IsNullOrEmpty(minimumVersion)) return true;

            Version client, minimum;
            if (!Version.TryParse(clientVersion ?? "", out client)) return false;
            if (!Version.TryParse(minimumVersion, out minimum)) return true;
            return client >= minimum;
        }
    }
}
