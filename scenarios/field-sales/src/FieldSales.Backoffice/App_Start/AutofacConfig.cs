using System.Web;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using Microsoft.AspNet.Identity.Owin;
using Wingtip.FieldSales.Backoffice.Services;
using Wingtip.FieldSales.Data;
using Wingtip.FieldSales.Data.Mail;

namespace Wingtip.FieldSales.Backoffice
{
    public static class AutofacConfig
    {
        public static void Register()
        {
            var builder = new ContainerBuilder();
            builder.RegisterControllers(typeof(MvcApplication).Assembly);

            builder.RegisterInstance(BackofficeSettings.FromConfig());
            builder.RegisterType<FieldSalesDbContext>().AsSelf().InstancePerRequest();
            builder.RegisterType<SalesProcedures>().InstancePerRequest();
            builder.RegisterType<ConfirmationMailer>().InstancePerRequest();
            builder.Register(c => MailSettings.FromConfig()).SingleInstance();

            // Identity objects live in the OWIN context (created in Startup) so that sign-in and the controllers share them.
            builder.Register(c => HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>()).InstancePerRequest().ExternallyOwned();
            builder.Register(c => HttpContext.Current.GetOwinContext().Get<ApplicationSignInManager>()).InstancePerRequest().ExternallyOwned();

            DependencyResolver.SetResolver(new AutofacDependencyResolver(builder.Build()));
        }
    }
}
