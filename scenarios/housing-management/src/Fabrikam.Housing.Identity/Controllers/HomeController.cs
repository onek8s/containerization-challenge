using System.Diagnostics;
using System.Threading.Tasks;
using Fabrikam.Housing.Identity.Models;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Fabrikam.Housing.Identity.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IConfiguration _configuration;

        public HomeController(IIdentityServerInteractionService interaction, IConfiguration configuration)
        {
            _interaction = interaction;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewBag.TenantPortal = _configuration["Clients:TenantPortal:BaseUrl"];
            ViewBag.BackOffice = _configuration["Clients:BackOffice:BaseUrl"];
            return View();
        }

        public async Task<IActionResult> Error(string errorId)
        {
            var model = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
            var message = await _interaction.GetErrorContextAsync(errorId);
            if (message != null)
            {
                model.Error = message.Error;
                model.Description = message.ErrorDescription;
            }
            return View(model);
        }
    }
}
