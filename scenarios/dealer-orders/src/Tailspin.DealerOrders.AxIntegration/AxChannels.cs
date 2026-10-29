using System;
using System.Collections.Concurrent;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Security;
using Polly;
using Tailspin.DealerOrders.Contracts.Ax;

namespace Tailspin.DealerOrders.AxIntegration
{
    /// <summary>
    /// WCF channel factories for the AOS NetTcp endpoints (AIF integration port and Query Service). The AOS only accepts
    /// Windows credentials: calls run as the service account, which must be an AX user with the TSDealerIntegration role.
    /// </summary>
    public sealed class AxChannels : IDisposable
    {
        private readonly AxSettings _settings;
        private readonly ConcurrentDictionary<Type, ChannelFactory> _factories = new ConcurrentDictionary<Type, ChannelFactory>();

        public AxChannels(AxSettings settings)
        {
            _settings = settings;
        }

        public static NetTcpBinding Binding(TimeSpan timeout)
        {
            var binding = new NetTcpBinding(SecurityMode.Transport)
            {
                MaxReceivedMessageSize = 64 * 1024 * 1024,
                MaxBufferSize = 64 * 1024 * 1024,
                SendTimeout = timeout,
                ReceiveTimeout = timeout,
                OpenTimeout = TimeSpan.FromSeconds(30),
                TransactionFlow = false
            };
            binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
            return binding;
        }

        private ChannelFactory<T> Factory<T>(string address) => (ChannelFactory<T>)_factories.GetOrAdd(typeof(T), _ =>
        {
            var endpoint = new EndpointAddress(new Uri(address), EndpointIdentity.CreateUpnIdentity(_settings.AosServiceUpn));
            var factory = new ChannelFactory<T>(Binding(_settings.CallTimeout), endpoint);
            factory.Credentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Identification;
            factory.Credentials.Windows.ClientCredential = CredentialCache.DefaultNetworkCredentials;
            return factory;
        });

        public string AifAddress => _settings.AifBaseAddress.TrimEnd('/') + "/" + _settings.AifPort;
        public string QueryServiceAddress => _settings.AifBaseAddress.TrimEnd('/') + "/QueryService";

        /// <summary>
        /// Calls an operation on a fresh channel. Connection-level failures are retried twice with a short back-off, then
        /// surface as <see cref="AxUnavailableException"/>; AIF faults surface as <see cref="AxBusinessException"/>.
        /// </summary>
        public TResult Call<TChannel, TResult>(string address, Func<TChannel, TResult> call, string what)
        {
            var policy = Policy
                .Handle<EndpointNotFoundException>()
                .Or<CommunicationObjectFaultedException>()
                .Or<ServerTooBusyException>()
                .WaitAndRetry(2, attempt => TimeSpan.FromSeconds(2 * attempt));

            try
            {
                return policy.Execute(() =>
                {
                    var channel = Factory<TChannel>(address).CreateChannel();
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
                });
            }
            catch (FaultException<Aif.AifFault> fault)
            {
                var messages = fault.Detail?.FaultMessageListArray ?? new Aif.FaultMessageList[0];
                var lines = new System.Collections.Generic.List<string>();
                foreach (var list in messages)
                    foreach (var m in list.FaultMessageArray ?? new Aif.FaultMessage[0])
                        lines.Add(m.Message);
                throw new AxBusinessException($"{what}: AIF fault.", lines, fault);
            }
            catch (FaultException fault)
            {
                throw new AxBusinessException($"{what}: {fault.Message}", null, fault);
            }
            catch (SecurityNegotiationException ex)
            {
                throw new AxBusinessException($"{what}: the AOS refused the Windows credentials of {System.Security.Principal.WindowsIdentity.GetCurrent().Name}.", null, ex);
            }
            catch (CommunicationException ex)
            {
                throw new AxUnavailableException($"{what}: {ex.Message}", ex);
            }
            catch (TimeoutException ex)
            {
                throw new AxUnavailableException($"{what}: timed out.", ex);
            }
        }

        public void Dispose()
        {
            foreach (var factory in _factories.Values)
            {
                try { factory.Close(); }
                catch (CommunicationException) { factory.Abort(); }
                catch (TimeoutException) { factory.Abort(); }
            }
        }
    }
}
