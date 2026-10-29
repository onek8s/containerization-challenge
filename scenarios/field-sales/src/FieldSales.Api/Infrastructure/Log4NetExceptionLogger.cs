using System.Web.Http.ExceptionHandling;
using log4net;

namespace Wingtip.FieldSales.Api.Infrastructure
{
    public class Log4NetExceptionLogger : ExceptionLogger
    {
        private static readonly ILog Logger = LogManager.GetLogger("FieldSales.Api.Errors");

        public override void Log(ExceptionLoggerContext context)
        {
            var request = context.Request;
            var user = context.RequestContext != null && context.RequestContext.Principal != null
                ? context.RequestContext.Principal.Identity.Name
                : "-";
            Logger.Error(string.Format("{0} {1} failed for {2}", request.Method, request.RequestUri.PathAndQuery, user), context.Exception);
        }
    }
}
