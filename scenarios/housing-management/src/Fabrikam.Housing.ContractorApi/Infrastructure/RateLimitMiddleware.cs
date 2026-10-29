using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Fabrikam.Housing.ContractorApi.Infrastructure
{
    public class ApiLimitsOptions
    {
        public int RequestsPerMinute { get; set; } = 120;
    }

    /// <summary>
    /// Fixed one-minute window per contractor. Counts are kept in the web server's memory.
    /// </summary>
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ApiLimitsOptions _options;

        public RateLimitMiddleware(RequestDelegate next, IMemoryCache cache, IOptions<ApiLimitsOptions> options)
        {
            _next = next;
            _cache = cache;
            _options = options.Value;
        }

        private class Window
        {
            public int Count;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var code = CurrentContractor.CodeFrom(context.User);
            if (code != null)
            {
                var now = DateTime.Now;
                var key = "rate:" + code + ":" + now.ToString("yyyyMMddHHmm");
                var window = _cache.GetOrCreate(key, e =>
                {
                    e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                    return new Window();
                });
                int count;
                lock (window) count = ++window.Count;
                if (count > _options.RequestsPerMinute)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = (60 - now.Second).ToString();
                    await context.Response.WriteAsync("Too many requests");
                    return;
                }
            }
            await _next(context);
        }
    }
}
