using System.Web.Mvc;
using Tailspin.DealerOrders.SalesDesk.Infrastructure;

namespace Tailspin.DealerOrders.SalesDesk.Controllers
{
    public abstract class DeskController : Controller
    {
        protected string DeskUser => User.Identity.Name;

        protected string AxUser => DeskUsers.AxUserFor(User);

        protected bool IsManager => DeskUsers.IsIn(User, DeskRole.SalesManager);

        protected void Flash(string message) => TempData["Message"] = message;
    }
}
