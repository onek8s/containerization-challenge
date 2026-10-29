using System.Net;
using System.Web.Mvc;

namespace Contoso.Purchasing.Web.Services
{
    public class ApiErrorFilter : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            var api = filterContext.Exception as ApiException;
            if (api == null || filterContext.ExceptionHandled) return;

            if (api.Status == HttpStatusCode.NotFound || api.Status == HttpStatusCode.Forbidden)
            {
                filterContext.Result = new HttpStatusCodeResult(api.Status);
                filterContext.ExceptionHandled = true;
            }
        }
    }
}
