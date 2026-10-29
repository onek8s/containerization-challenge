using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace VanArsdel.BuildingBlocks.Caching
{
    public static class CachingExtensions
    {
        /// <summary>
        /// Redis cache and locks when "Redis:Configuration" is set (redis01.corp.local:6379,password=...,ssl=False),
        /// otherwise in-memory equivalents.
        /// </summary>
        public static IServiceCollection AddVanArsdelCaching(this IServiceCollection services, IConfiguration configuration, string instanceName)
        {
            var redis = configuration["Redis:Configuration"];
            if (string.IsNullOrWhiteSpace(redis))
            {
                services.AddDistributedMemoryCache();
                services.AddSingleton<IDistributedLockProvider, LocalLockProvider>();
                return services;
            }

            var options = ConfigurationOptions.Parse(redis);
            options.AbortOnConnectFail = false;
            var multiplexer = ConnectionMultiplexer.Connect(options);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            services.AddStackExchangeRedisCache(o =>
            {
                o.ConfigurationOptions = options;
                o.InstanceName = "vanarsdel:" + instanceName + ":";
            });

            var factory = RedLockFactory.Create(new List<RedLockMultiplexer> { new RedLockMultiplexer(multiplexer) });
            services.AddSingleton(factory);
            services.AddSingleton<IDistributedLockProvider>(new RedLockProvider(factory, "vanarsdel:lock:"));
            return services;
        }

        public static async Task<T> GetOrSetJsonAsync<T>(this IDistributedCache cache, string key, TimeSpan ttl,
            Func<Task<T>> factory, CancellationToken cancellationToken = default) where T : class
        {
            var cached = await cache.GetStringAsync(key, cancellationToken);
            if (cached != null)
            {
                return JsonConvert.DeserializeObject<T>(cached);
            }

            var value = await factory();
            if (value != null)
            {
                await cache.SetStringAsync(key, JsonConvert.SerializeObject(value),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
            }
            return value;
        }
    }
}
