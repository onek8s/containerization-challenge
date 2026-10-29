using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fabrikam.Housing.DocumentService
{
    /// <summary>
    /// The housing applications call the API with a shared key (Api:Keys). The service only listens on the
    /// server network, so there is no user authentication here; callers pass the user name as uploadedBy.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string[] _keys;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _keys = configuration.GetSection("Api:Keys").Get<string[]>() ?? new string[0];
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var given = context.Request.Headers["X-Api-Key"].ToString();
                if (string.IsNullOrEmpty(given) || !_keys.Any(k => FixedTimeEquals(k, given)))
                {
                    _logger.LogWarning("Rejected {Method} {Path} from {Remote}: missing or wrong API key", context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await _next(context);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var x = Encoding.UTF8.GetBytes(a);
            var y = Encoding.UTF8.GetBytes(b);
            return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
        }
    }
}
