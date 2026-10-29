using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fabrikam.Housing.Identity
{
    /// <summary>
    /// Browsers reject SameSite=None cookies that are not Secure. On plain HTTP (developer machines, the
    /// HTTP binding behind the load balancer health check) the SameSite attribute is left off instead.
    /// </summary>
    public static class SameSiteCookies
    {
        public static IServiceCollection AddSameSiteCookiePolicy(this IServiceCollection services)
        {
            return services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
                options.OnAppendCookie = context => Check(context.Context, context.CookieOptions);
                options.OnDeleteCookie = context => Check(context.Context, context.CookieOptions);
            });
        }

        private static void Check(HttpContext http, CookieOptions options)
        {
            if (options.SameSite == SameSiteMode.None && !http.Request.IsHttps)
                options.SameSite = SameSiteMode.Unspecified;
        }
    }
}
