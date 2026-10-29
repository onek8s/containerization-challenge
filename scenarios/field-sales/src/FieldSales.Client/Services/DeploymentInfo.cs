using System;
using System.Deployment.Application;
using log4net;

namespace Wingtip.FieldSales.Client.Services
{
    /// <summary>ClickOnce deployment details. When started from Visual Studio the assembly version is shown instead.</summary>
    public static class DeploymentInfo
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DeploymentInfo));

        public static bool IsNetworkDeployed
        {
            get { return ApplicationDeployment.IsNetworkDeployed; }
        }

        public static string Version
        {
            get
            {
                return IsNetworkDeployed
                    ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString()
                    : typeof(DeploymentInfo).Assembly.GetName().Version.ToString();
            }
        }

        public static string UpdateLocation
        {
            get { return IsNetworkDeployed ? ApplicationDeployment.CurrentDeployment.UpdateLocation.ToString() : "(not installed)"; }
        }

        /// <summary>Checks the deployment site for a newer version. Returns false when offline or not installed with ClickOnce.</summary>
        public static bool IsUpdateAvailable()
        {
            if (!IsNetworkDeployed) return false;
            try
            {
                return ApplicationDeployment.CurrentDeployment.CheckForUpdate(false);
            }
            catch (DeploymentException ex)
            {
                Log.Warn("Update check failed", ex);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Log.Warn("Update check failed", ex);
                return false;
            }
        }

        /// <summary>Downloads the new version synchronously. The application must be restarted afterwards.</summary>
        public static bool Update()
        {
            if (!IsNetworkDeployed) return false;
            try
            {
                return ApplicationDeployment.CurrentDeployment.Update();
            }
            catch (DeploymentException ex)
            {
                Log.Error("Update failed", ex);
                return false;
            }
        }
    }
}
