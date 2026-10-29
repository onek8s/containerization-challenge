[assembly: WebActivator.PreApplicationStartMethod(typeof(Southridge.Clinic.PatientWeb.App_Start.NinjectWebCommon), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Southridge.Clinic.PatientWeb.App_Start.NinjectWebCommon), "Stop")]

namespace Southridge.Clinic.PatientWeb.App_Start
{
    using System;
    using System.Web;
    using Microsoft.Web.Infrastructure.DynamicModuleHelper;
    using Ninject;
    using Ninject.Web.Common;
    using Southridge.Clinic.Billing;
    using Southridge.Clinic.PatientWeb.Areas.MyHealth.Models;

    /// <summary>Ninject for the MVC area (added with the Ninject.MVC3 package in 2012). The Web Forms pages do not use it.</summary>
    public static class NinjectWebCommon
    {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();

        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestHttpModule));
            DynamicModuleUtility.RegisterModule(typeof(NinjectHttpModule));
            bootstrapper.Initialize(CreateKernel);
        }

        public static void Stop()
        {
            bootstrapper.ShutDown();
        }

        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();
            kernel.Bind<Func<IKernel>>().ToMethod(ctx => () => new Bootstrapper().Kernel);
            kernel.Bind<IHttpModule>().To<HttpApplicationInitializationHttpModule>();
            RegisterServices(kernel);
            return kernel;
        }

        private static void RegisterServices(IKernel kernel)
        {
            kernel.Bind<IBookingClient>().To<BookingClient>().InRequestScope();
            kernel.Bind<IPatientContext>().To<SessionPatientContext>().InRequestScope();
            kernel.Bind<IStatementSource>().To<StatementSource>();
            kernel.Bind<StatementService>().ToMethod(ctx => new StatementService());
        }
    }
}
