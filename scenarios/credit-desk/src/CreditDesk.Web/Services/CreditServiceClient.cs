using System;
using System.ServiceModel;
using CreditDesk.Contracts;

namespace CreditDesk.Web.Services
{
    /// <summary>
    /// Calls the credit service as the user signed in to the site. Web.config enables ASP.NET
    /// impersonation and the client endpoint allows delegation, so the request thread's Windows
    /// identity is passed on to the credit server with Kerberos.
    /// </summary>
    public static class CreditServiceClient
    {
        private static readonly Lazy<ChannelFactory<ICreditService>> Factory =
            new Lazy<ChannelFactory<ICreditService>>(() => new ChannelFactory<ICreditService>("CreditService"));

        public static T Call<T>(Func<ICreditService, T> call)
        {
            var channel = Factory.Value.CreateChannel();
            var client = (IClientChannel)channel;
            var ok = false;
            try
            {
                var result = call(channel);
                client.Close();
                ok = true;
                return result;
            }
            finally
            {
                if (!ok) client.Abort();
            }
        }

        public static void Close()
        {
            if (!Factory.IsValueCreated) return;
            try { Factory.Value.Close(); }
            catch (CommunicationException) { Factory.Value.Abort(); }
            catch (TimeoutException) { Factory.Value.Abort(); }
        }
    }
}
