using System.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using Contoso.Purchasing.Core;

namespace Contoso.Purchasing.Api.Infrastructure
{
    public class PolicyExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            var policy = context.Exception as PolicyException;
            if (policy != null)
            {
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.BadRequest, policy.Message);
            }
        }
    }

    public class UnauthorizedFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception is System.UnauthorizedAccessException)
            {
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.Forbidden, context.Exception.Message);
            }
        }
    }

    public class SqlExceptionFilter : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            var sql = context.Exception as SqlException;
            if (sql != null)
            {
                Trace.TraceError("SQL error {0} in {1}: {2}", sql.Number, context.Request.RequestUri, sql.Message);
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, "The purchasing database is not available.");
            }
        }
    }
}
