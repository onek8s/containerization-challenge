using System;
using Microsoft.Practices.EnterpriseLibrary.ExceptionHandling;

namespace Southridge.Clinic.Infrastructure
{
    public static class ExceptionPolicies
    {
        /// <summary>Web pages: log and show a friendly message (ClinicRuleException messages are shown as they are).</summary>
        public const string UserInterface = "UI Policy";

        /// <summary>Service boundaries: log and replace with a FaultException / SoapException without internals.</summary>
        public const string ServiceBoundary = "Service Policy";

        /// <summary>Data access: log and rethrow.</summary>
        public const string DataAccess = "Data Access Policy";
    }

    public static class ErrorHandler
    {
        /// <summary>
        /// Runs the exception through the named policy. Returns the exception to throw (the original when the
        /// policy says rethrow, the replacement when it has a replace handler) or null when it was handled.
        /// </summary>
        public static Exception Handle(Exception ex, string policy)
        {
            Exception toThrow;
            try
            {
                bool rethrow = ExceptionPolicy.HandleException(ex, policy, out toThrow);
                if (toThrow != null)
                {
                    return toThrow;
                }

                return rethrow ? ex : null;
            }
            catch (Exception policyFailure)
            {
                ClinicLog.Error(LogCategory.General, "Exception policy " + policy + " failed: " + policyFailure.Message, ex);
                return ex;
            }
        }
    }
}
