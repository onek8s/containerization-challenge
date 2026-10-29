using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace Southridge.Clinic.InterfaceEngine
{
    /// <summary>installutil SouthridgeInterfaceEngine.exe (see deploy/Install-InterfaceEngine.ps1).</summary>
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var process = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalService
            };
            var service = new ServiceInstaller
            {
                ServiceName = InterfaceEngineService.Name,
                DisplayName = "Southridge Interface Engine",
                Description = "HL7 MLLP interfaces between the Southridge clinics, the hospital and the reference lab.",
                StartType = ServiceStartMode.Automatic,
                DelayedAutoStart = true
            };
            Installers.Add(process);
            Installers.Add(service);
        }
    }
}
