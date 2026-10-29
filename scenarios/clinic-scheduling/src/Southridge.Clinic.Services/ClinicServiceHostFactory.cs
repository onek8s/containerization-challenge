using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Web.Hosting;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Services
{
    /// <summary>
    /// Host factory for the message-security services. The service certificate is loaded from a PFX file next to
    /// the site (Clinic:ServiceCertificatePath) instead of the machine store so the site can be copied between
    /// servers by the deployment script.
    /// </summary>
    public class ClinicServiceHostFactory : ServiceHostFactory
    {
        private static readonly object Sync = new object();
        private static X509Certificate2 _certificate;

        protected override ServiceHost CreateServiceHost(Type serviceType, Uri[] baseAddresses)
        {
            ServiceHost host = base.CreateServiceHost(serviceType, baseAddresses);
            host.Credentials.ServiceCertificate.Certificate = LoadCertificate();
            return host;
        }

        public static X509Certificate2 LoadCertificate()
        {
            if (_certificate == null)
            {
                lock (Sync)
                {
                    if (_certificate == null)
                    {
                        string path = ClinicSettings.Get("ServiceCertificatePath", "~/App_Data/clinic-svc.pfx");
                        if (path.StartsWith("~/", StringComparison.Ordinal))
                        {
                            path = HostingEnvironment.MapPath(path);
                        }

                        if (!File.Exists(path))
                        {
                            throw new InvalidOperationException("Service certificate " + path + " was not found.");
                        }

                        _certificate = new X509Certificate2(path, ClinicSettings.Get("ServiceCertificatePassword", string.Empty),
                            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                        ClinicLog.Info(LogCategory.Security, "Service certificate {0} loaded, expires {1:d}.", _certificate.Subject, _certificate.NotAfter);
                    }
                }
            }

            return _certificate;
        }
    }
}
