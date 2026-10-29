using Fabrikam.Housing.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fabrikam.Housing.ContractorApi.Infrastructure
{
    public class BusinessRuleExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (!(context.Exception is BusinessRuleException ex)) return;
            var problem = new ProblemDetails
            {
                Status = 422,
                Title = "The request breaks a business rule.",
                Detail = ex.Message,
                Type = "https://contractors-api.fabrikamhomes.co.uk/problems/" + ex.Code.ToLowerInvariant()
            };
            problem.Extensions["code"] = ex.Code;
            context.Result = new ObjectResult(problem) { StatusCode = 422 };
            context.ExceptionHandled = true;
        }
    }
}
