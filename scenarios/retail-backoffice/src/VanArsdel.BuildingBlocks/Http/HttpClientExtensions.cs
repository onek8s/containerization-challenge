using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Refit;
using VanArsdel.BuildingBlocks.Security;

namespace VanArsdel.BuildingBlocks.Http
{
    public static class HttpClientExtensions
    {
        /// <summary>Client credentials for calls to the other services.</summary>
        public static IServiceCollection AddServiceIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ServiceIdentityOptions>(configuration.GetSection("ServiceIdentity"));
            services.AddHttpClient("token");
            services.AddSingleton<ServiceTokenProvider>();
            services.AddTransient<ClientCredentialsHandler>();
            return services;
        }

        /// <summary>
        /// Typed Refit client for another service, with retries and a circuit breaker.
        /// The base address comes from "Services:{name}".
        /// </summary>
        public static IHttpClientBuilder AddServiceClient<TClient>(this IServiceCollection services, IConfiguration configuration,
            string name, bool authenticate = true) where TClient : class
        {
            var baseAddress = configuration["Services:" + name];
            if (string.IsNullOrEmpty(baseAddress))
            {
                throw new InvalidOperationException($"Services:{name} is not configured.");
            }

            var builder = services
                .AddRefitClient<TClient>()
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri(baseAddress.TrimEnd('/') + "/");
                    c.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Services:TimeoutSeconds", 30));
                })
                .AddPolicyHandler(RetryPolicy())
                .AddPolicyHandler(CircuitBreakerPolicy());

            if (authenticate)
            {
                builder.AddHttpMessageHandler<ClientCredentialsHandler>();
            }
            return builder;
        }

        /// <summary>Typed Refit client for an external party (no service token).</summary>
        public static IHttpClientBuilder AddRefitClientFor<TClient>(this IServiceCollection services, string baseUrl) where TClient : class
        {
            return services.AddRefitClient<TClient>()
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri(baseUrl ?? throw new InvalidOperationException(typeof(TClient).Name + " base URL is not configured."));
                    c.Timeout = TimeSpan.FromSeconds(30);
                })
                .AddPolicyHandler(RetryPolicy())
                .AddPolicyHandler(CircuitBreakerPolicy());
        }

        public static IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
            HttpPolicyExtensions.HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(3, attempt)));

        public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
            HttpPolicyExtensions.HandleTransientHttpError()
                .CircuitBreakerAsync(8, TimeSpan.FromSeconds(30));
    }
}
