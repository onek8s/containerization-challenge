using System;
using System.Net.Sockets;
using System.Runtime.Remoting;
using Wingtip.Invoices.Contracts.Erp;
using Wingtip.Invoices.Core;
using Wingtip.Invoices.Core.Matching;
using Wingtip.Invoices.Infrastructure.Configuration;

namespace Wingtip.Invoices.Processor
{
    [Serializable]
    public class ErpUnavailableException : Exception
    {
        public ErpUnavailableException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected ErpUnavailableException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Client of the ERP lookup object published by the InvoiceErpLink service. The tcp channel
    /// (Windows authentication) is registered from the &lt;system.runtime.remoting&gt; section of the .config file.
    /// </summary>
    public class ErpLookupClient
    {
        private static readonly object ConfigureLock = new object();
        private static bool configured;
        private readonly IErpLookup proxy;
        private readonly string url;

        public ErpLookupClient()
        {
            lock (ConfigureLock)
            {
                if (!configured)
                {
                    RemotingConfiguration.Configure(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile, true);
                    configured = true;
                }
            }

            url = HubSettings.Get("ErpLookupUrl", "tcp://invoices-erp.corp.local:8095/ErpLookup.rem");
            proxy = (IErpLookup)Activator.GetObject(typeof(IErpLookup), url);
        }

        public Vendor FindVendor(string vatNumber, string mappedVendorNumber)
        {
            return Call(() =>
            {
                var vendor = string.IsNullOrEmpty(vatNumber) ? null : proxy.FindVendorByVatNumber(VatNumber.Normalize(vatNumber));
                if (vendor == null && !string.IsNullOrEmpty(mappedVendorNumber)) vendor = proxy.GetVendor(mappedVendorNumber);
                return vendor;
            });
        }

        public Vendor GetVendor(string vendorNumber)
        {
            return Call(() => proxy.GetVendor(vendorNumber));
        }

        public PurchaseOrder GetPurchaseOrder(string orderNumber)
        {
            if (string.IsNullOrEmpty(orderNumber)) return null;
            return Call(() => proxy.GetPurchaseOrder(orderNumber.Trim()));
        }

        public DateTime GetOpenPeriodStart()
        {
            return Call(() => proxy.GetOpenPeriodStart());
        }

        public string Ping()
        {
            return Call(() => proxy.Ping());
        }

        private T Call<T>(Func<T> call)
        {
            try
            {
                return call();
            }
            catch (RemotingException ex)
            {
                throw new ErpUnavailableException("ERP lookup at " + url + " failed: " + ex.Message, ex);
            }
            catch (SocketException ex)
            {
                throw new ErpUnavailableException("ERP lookup at " + url + " is not reachable: " + ex.Message, ex);
            }
            catch (System.Security.Authentication.AuthenticationException ex)
            {
                throw new ErpUnavailableException("ERP lookup at " + url + " refused the service account: " + ex.Message, ex);
            }
        }
    }
}
