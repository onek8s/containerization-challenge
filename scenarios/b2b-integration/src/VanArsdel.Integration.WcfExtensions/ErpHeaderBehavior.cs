using System;
using System.Configuration;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using VanArsdel.Integration.Runtime.Configuration;

namespace VanArsdel.Integration.WcfExtensions
{
    public class ErpHeaderBehavior : IEndpointBehavior
    {
        private readonly string _clientIdSetting;
        private readonly string _secretSetting;

        public ErpHeaderBehavior(string clientIdSetting, string secretSetting)
        {
            _clientIdSetting = clientIdSetting;
            _secretSetting = secretSetting;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new ErpHeaderInspector(_clientIdSetting, _secretSetting));
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            throw new NotSupportedException("The ERP header behaviour is for client (send port) endpoints only.");
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }

    /// <summary>
    /// Registered in machine.config (behaviorExtensions, name "vanArsdelErpHeaders") on the BizTalk servers so the
    /// WCF-Custom send port can select it on the Behavior tab.
    /// </summary>
    public class ErpHeaderBehaviorElement : BehaviorExtensionElement
    {
        [ConfigurationProperty("clientIdSetting", DefaultValue = SettingNames.ErpClientId)]
        public string ClientIdSetting
        {
            get { return (string)this["clientIdSetting"]; }
            set { this["clientIdSetting"] = value; }
        }

        [ConfigurationProperty("secretSetting", DefaultValue = SettingNames.ErpClientSecret)]
        public string SecretSetting
        {
            get { return (string)this["secretSetting"]; }
            set { this["secretSetting"] = value; }
        }

        public override Type BehaviorType
        {
            get { return typeof(ErpHeaderBehavior); }
        }

        protected override object CreateBehavior()
        {
            return new ErpHeaderBehavior(ClientIdSetting, SecretSetting);
        }
    }
}
