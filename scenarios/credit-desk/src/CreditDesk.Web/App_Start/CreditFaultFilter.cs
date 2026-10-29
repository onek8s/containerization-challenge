using System.ServiceModel;
using System.Web.Mvc;
using CreditDesk.Contracts;

namespace CreditDesk.Web
{
    /// <summary>Shows faults from the credit service as a friendly page instead of the error page.</summary>
    public sealed class CreditFaultFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            string message;
            string[] errors = null;
            if (context.Exception is FaultException<CreditFault> fault)
            {
                message = fault.Detail.Message;
                errors = fault.Detail.Errors;
            }
            else if (context.Exception is System.Security.Authentication.AuthenticationException
                     || context.Exception is System.ServiceModel.Security.SecurityNegotiationException)
            {
                message = "The credit service did not accept your Windows sign-in. Contact the service desk and quote 'Credit Desk delegation'.";
            }
            else if (context.Exception is EndpointNotFoundException || context.Exception is CommunicationException)
            {
                message = "The credit service is not reachable at the moment. Try again in a few minutes.";
            }
            else
            {
                return;
            }

            context.ExceptionHandled = true;
            context.Result = new ViewResult
            {
                ViewName = "ServiceError",
                ViewData = new ViewDataDictionary(new Models.ServiceErrorModel { Message = message, Errors = errors })
            };
        }
    }
}
