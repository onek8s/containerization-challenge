using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace Lucerne.ProjectSites.SyncService
{
    /// <summary>
    /// installutil /username=CORP\svc-ps-sync /password=... LucerneProjectSync.exe
    /// (Install-SyncService.ps1 passes the account; the installer then grants "log on as a service").
    /// </summary>
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var process = new ServiceProcessInstaller { Account = ServiceAccount.User };
            var service = new ServiceInstaller
            {
                ServiceName = SyncWindowsService.Name,
                DisplayName = "Lucerne Project Sites Sync",
                Description = "Synchronises projects, staff and timesheets from ERP and HR into SharePoint project sites and publishes approved documents to document control.",
                StartType = ServiceStartMode.Automatic,
                DelayedAutoStart = true
            };
            Installers.Add(process);
            Installers.Add(service);
        }
    }
}
