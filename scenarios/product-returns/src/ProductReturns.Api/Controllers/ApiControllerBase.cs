using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductReturns.Api.Security;
using ProductReturns.Api.Services;

namespace ProductReturns.Api.Controllers
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>Staff member the portal acts for, or the authenticated caller.</summary>
        protected string Actor
        {
            get
            {
                var onBehalfOf = Request.Headers[Headers.OnBehalfOf].ToString();
                return string.IsNullOrWhiteSpace(onBehalfOf) ? User.Identity?.Name : onBehalfOf.Trim();
            }
        }

        protected async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (ReturnRuleException ex)
            {
                return Problem(detail: ex.Message, statusCode: ex.StatusCode);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Problem(detail: "The return was changed by someone else. Reload and try again.", statusCode: 409);
            }
        }
    }
}
