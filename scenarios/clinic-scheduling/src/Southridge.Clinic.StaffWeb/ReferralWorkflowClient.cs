using System;
using System.Configuration;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Southridge.Clinic.Contracts;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.StaffWeb
{
    /// <summary>
    /// Tells the referral workflow on the services host about decisions saved here. The decision is already in the
    /// database, so a failed call is logged and does not stop the user; the services host re-syncs on start.
    /// </summary>
    public static class ReferralWorkflowClient
    {
        private static readonly object Sync = new object();
        private static ChannelFactory<IReferralWorkflowService> _factory;

        public static bool Start(int referralId)
        {
            return Call("Start", referralId, c => c.Start(referralId));
        }

        public static bool Decided(int referralId, bool approved, string user, string note)
        {
            return Call("Decided", referralId, c => c.Decided(referralId, approved, user, note));
        }

        public static bool Scheduled(int referralId, int appointmentId)
        {
            return Call("Scheduled", referralId, c => c.Scheduled(referralId, appointmentId));
        }

        public static bool Cancelled(int referralId, string user, string note)
        {
            return Call("Cancelled", referralId, c => c.Cancelled(referralId, user, note));
        }

        private static ChannelFactory<IReferralWorkflowService> Factory
        {
            get
            {
                lock (Sync)
                {
                    if (_factory == null || _factory.State == CommunicationState.Faulted)
                    {
                        _factory = new ChannelFactory<IReferralWorkflowService>("ReferralWorkflow");
                    }

                    return _factory;
                }
            }
        }

        private static bool Call(string operation, int referralId, Action<IReferralWorkflowService> call)
        {
            IReferralWorkflowService channel = Factory.CreateChannel();
            var client = (IClientChannel)channel;
            try
            {
                using (new OperationContextScope(client))
                {
                    var http = new HttpRequestMessageProperty();
                    http.Headers["X-Clinic-Key"] = ConfigurationManager.AppSettings["Clinic:InternalServiceKey"];
                    OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = http;
                    call(channel);
                }

                client.Close();
                return true;
            }
            catch (FaultException<ClinicFault> ex)
            {
                client.Abort();
                ClinicLog.Warning(LogCategory.General, "Referral workflow " + operation + " for referral " + referralId + " was refused: " + ex.Detail.Message);
                return false;
            }
            catch (CommunicationException ex)
            {
                client.Abort();
                ClinicLog.Error(LogCategory.General, "Referral workflow " + operation + " for referral " + referralId + " failed.", ex);
                return false;
            }
            catch (TimeoutException ex)
            {
                client.Abort();
                ClinicLog.Error(LogCategory.General, "Referral workflow " + operation + " for referral " + referralId + " timed out.", ex);
                return false;
            }
        }
    }
}
