using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using log4net;
using VanArsdel.Integration.Contracts.Gateway;

namespace VanArsdel.Integration.Gateway.Behaviors
{
    /// <summary>Logs unexpected exceptions and turns them into a GatewayFault without internal details.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GatewayErrorHandlingAttribute : Attribute, IServiceBehavior, IErrorHandler
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GatewayErrorHandlingAttribute));

        public bool HandleError(Exception error)
        {
            if (!(error is FaultException))
            {
                Log.Error("Gateway call failed", error);
            }

            return true;
        }

        public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
        {
            if (error is FaultException)
            {
                return;
            }

            var incident = Guid.NewGuid().ToString("N").Substring(0, 12);
            Log.ErrorFormat("Incident {0}: {1}", incident, error.Message);
            var detail = new FaultException<GatewayFault>(
                new GatewayFault { Code = "InternalError", Message = "The request could not be processed. Quote incident " + incident + " to VanArsdel B2B support." },
                new FaultReason("Internal error " + incident), new FaultCode("InternalError"));
            fault = Message.CreateMessage(version, detail.CreateMessageFault(), detail.Action);
        }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ChannelDispatcherBase dispatcherBase in serviceHostBase.ChannelDispatchers)
            {
                var dispatcher = dispatcherBase as ChannelDispatcher;
                if (dispatcher != null)
                {
                    dispatcher.ErrorHandlers.Add(this);
                }
            }
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }
    }
}
