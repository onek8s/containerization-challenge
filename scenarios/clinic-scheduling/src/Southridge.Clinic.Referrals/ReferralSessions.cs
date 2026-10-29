using System;
using ConfigurationManager = System.Configuration.ConfigurationManager;
using System.Web;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Context;
using NHibernate.Dialect;
using NHibernate.Driver;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Referrals
{
    /// <summary>
    /// Owns the NHibernate session factory for the referral module. The factory reads the hibernate-configuration
    /// section when the host has one; otherwise it is configured from the "Clinic" connection string.
    /// </summary>
    public static class ReferralSessions
    {
        private static readonly object Sync = new object();
        private static ISessionFactory _factory;
        private static string _contextClass = "thread_static";

        /// <summary>Call once at start-up: "web" in ASP.NET hosts, "thread_static" in services and batch jobs.</summary>
        public static void Initialize(string currentSessionContext)
        {
            lock (Sync)
            {
                _contextClass = currentSessionContext;
                _factory = null;
            }
        }

        public static ISessionFactory Factory
        {
            get
            {
                if (_factory == null)
                {
                    lock (Sync)
                    {
                        if (_factory == null)
                        {
                            _factory = BuildConfiguration().BuildSessionFactory();
                        }
                    }
                }

                return _factory;
            }
        }

        public static Configuration BuildConfiguration()
        {
            var cfg = new Configuration();
            if (ConfigurationManager.GetSection("hibernate-configuration") != null)
            {
                cfg.Configure();
            }
            else
            {
                cfg.SetProperty(NHibernate.Cfg.Environment.ConnectionProvider, "NHibernate.Connection.DriverConnectionProvider");
                cfg.SetProperty(NHibernate.Cfg.Environment.ConnectionDriver, typeof(SqlClientDriver).AssemblyQualifiedName);
                cfg.SetProperty(NHibernate.Cfg.Environment.Dialect, typeof(MsSql2008Dialect).AssemblyQualifiedName);
                cfg.SetProperty(NHibernate.Cfg.Environment.ConnectionStringName, "Clinic");
                cfg.SetProperty(NHibernate.Cfg.Environment.ShowSql, "false");
                cfg.SetProperty(NHibernate.Cfg.Environment.CommandTimeout, "60");
                cfg.SetProperty(NHibernate.Cfg.Environment.BatchSize, "20");
                cfg.SetProperty(NHibernate.Cfg.Environment.ProxyFactoryFactoryClass, "NHibernate.Bytecode.DefaultProxyFactoryFactory, NHibernate");
            }

            cfg.SetProperty(NHibernate.Cfg.Environment.CurrentSessionContextClass, _contextClass);
            cfg.AddAssembly(typeof(Referral).Assembly);
            return cfg;
        }

        public static ISession Current
        {
            get
            {
                if (!CurrentSessionContext.HasBind(Factory))
                {
                    throw new InvalidOperationException("No NHibernate session is bound. Use ReferralSessions.Bind or the ReferralSessionModule.");
                }

                return Factory.GetCurrentSession();
            }
        }

        public static void Bind()
        {
            if (!CurrentSessionContext.HasBind(Factory))
            {
                ISession session = Factory.OpenSession();
                session.FlushMode = FlushMode.Commit;
                CurrentSessionContext.Bind(session);
            }
        }

        public static void Unbind()
        {
            if (_factory == null)
            {
                return;
            }

            ISession session = CurrentSessionContext.Unbind(_factory);
            if (session == null)
            {
                return;
            }

            try
            {
                if (session.Transaction != null && session.Transaction.IsActive)
                {
                    session.Transaction.Rollback();
                }
            }
            finally
            {
                session.Dispose();
            }
        }

        /// <summary>Runs work in its own session and transaction (services, batch jobs, workflow callbacks).</summary>
        public static T InUnitOfWork<T>(Func<ISession, T> work)
        {
            using (ISession session = Factory.OpenSession())
            using (ITransaction tx = session.BeginTransaction())
            {
                T result = work(session);
                tx.Commit();
                return result;
            }
        }

        public static void InUnitOfWork(Action<ISession> work)
        {
            InUnitOfWork<object>(s =>
            {
                work(s);
                return null;
            });
        }
    }

    /// <summary>Session-per-request for the staff site. Registered in system.webServer/modules.</summary>
    public class ReferralSessionModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.PostAcquireRequestState += OnAcquire;
            context.EndRequest += OnEnd;
        }

        public void Dispose()
        {
        }

        private static void OnAcquire(object sender, EventArgs e)
        {
            var app = (HttpApplication)sender;
            if (app.Context.Handler is System.Web.UI.Page && app.Request.Path.IndexOf("/Referrals/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ReferralSessions.Bind();
            }
        }

        private static void OnEnd(object sender, EventArgs e)
        {
            try
            {
                ReferralSessions.Unbind();
            }
            catch (Exception ex)
            {
                ClinicLog.Error(LogCategory.General, "Closing the referral session failed.", ex);
            }
        }
    }
}
