using System;
using System.Configuration;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;

namespace Southridge.Clinic.Infrastructure
{
    /// <summary>
    /// Application wide Unity container. Each host registers its own services in its start-up code;
    /// the optional &lt;unity&gt; section of the configuration file can override registrations per server.
    /// </summary>
    public static class ClinicContainer
    {
        private static readonly object SyncRoot = new object();
        private static IUnityContainer _current;

        public static IUnityContainer Current
        {
            get
            {
                if (_current == null)
                {
                    lock (SyncRoot)
                    {
                        if (_current == null)
                        {
                            _current = Create();
                        }
                    }
                }

                return _current;
            }
        }

        public static T Resolve<T>()
        {
            return Current.Resolve<T>();
        }

        public static IUnityContainer Create()
        {
            var container = new UnityContainer();
            container.RegisterType<IClock, SystemClock>(new ContainerControlledLifetimeManager());
            container.RegisterType<IMailer, SmtpMailer>(new ContainerControlledLifetimeManager());
            return container;
        }

        /// <summary>Applies the &lt;unity&gt; section after the host's own registrations.</summary>
        public static void ApplyConfiguration(IUnityContainer container)
        {
            var section = ConfigurationManager.GetSection("unity") as UnityConfigurationSection;
            if (section != null)
            {
                section.Configure(container);
            }
        }

        public static void Reset(IUnityContainer container)
        {
            lock (SyncRoot)
            {
                if (_current != null && !ReferenceEquals(_current, container))
                {
                    _current.Dispose();
                }

                _current = container;
            }
        }
    }

    public interface IClock
    {
        DateTime Now { get; }

        DateTime Today { get; }
    }

    /// <summary>Clinic local time. The servers run in the clinic time zone (Pacific).</summary>
    public class SystemClock : IClock
    {
        public DateTime Now
        {
            get { return DateTime.Now; }
        }

        public DateTime Today
        {
            get { return DateTime.Today; }
        }
    }
}
