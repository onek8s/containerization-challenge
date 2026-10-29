using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace VanArsdel.BuildingBlocks.Web
{
    /// <summary>Takes X-Correlation-ID from the gateway (or creates one) and puts it on the log context.</summary>
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.Request.Headers[HeaderName].ToString();
            if (string.IsNullOrEmpty(correlationId))
            {
                correlationId = context.TraceIdentifier;
            }
            else
            {
                context.TraceIdentifier = correlationId;
            }

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
