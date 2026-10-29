using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Services
{
    /// <summary>Lets the staff site call the internal referral workflow endpoint (X-Clinic-Key HTTP header).</summary>
    public class InternalKeyAuthorizationManager : ServiceAuthorizationManager
    {
        public const string HeaderName = "X-Clinic-Key";

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            object property;
            if (!operationContext.IncomingMessageProperties.TryGetValue(HttpRequestMessageProperty.Name, out property))
            {
                return false;
            }

            string key = ((HttpRequestMessageProperty)property).Headers[HeaderName];
            string expected = ClinicSettings.Get("InternalServiceKey");
            bool ok = !string.IsNullOrEmpty(expected) && string.Equals(key, expected, StringComparison.Ordinal);
            if (!ok)
            {
                ClinicLog.Warning(LogCategory.Security, "Rejected call to the referral workflow endpoint without a valid " + HeaderName + ".");
            }

            return ok;
        }
    }
}
