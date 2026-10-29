using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace VanArsdel.Integration.SlaMonitor
{
    /// <summary>
    /// InstallUtil.exe VanArsdel.Integration.SlaMonitor.exe. The account is set afterwards by Install-SlaMonitor.ps1
    /// (sc.exe config ... obj= CORP\svc-b2bmonitor), so the installer only registers the service.
    /// </summary>
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            Installers.Add(new ServiceProcessInstaller { Account = ServiceAccount.NetworkService });
            Installers.Add(new ServiceInstaller
            {
                ServiceName = SlaMonitorService.Name,
                DisplayName = "VanArsdel B2B SLA Monitor",
                Description = "Checks acknowledgements, order milestones, suspended instances and host instances of the B2B platform and e-mails the support groups.",
                StartType = ServiceStartMode.Automatic,
                DelayedAutoStart = true
            });
        }
    }
}
