using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ProductReturns.Portal.Services
{
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T GetObject<T>(this ISession session, string key)
            where T : class
        {
            var json = session.GetString(key);
            return json == null ? null : JsonSerializer.Deserialize<T>(json);
        }
    }
}
