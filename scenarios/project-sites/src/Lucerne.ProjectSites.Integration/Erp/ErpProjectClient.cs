using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Lucerne.ProjectSites.Contracts.Erp;

namespace Lucerne.ProjectSites.Integration.Erp
{
    public interface IErpProjectSource
    {
        IEnumerable<ErpProject> GetChangedProjects(DateTime sinceUtc);
        ErpProject GetProject(string projectNumber);
        IList<ErpTimesheetLine> GetApprovedTimesheetLines(DateTime from, DateTime to, string projectNumber);
    }

    /// <summary>
    /// Client of the ERP project accounting SOAP service. BasicHttpBinding with transport security and a
    /// basic-auth integration account in production; plain HTTP is accepted for test systems.
    /// </summary>
    public class ErpProjectClient : IErpProjectSource
    {
        private readonly ErpSettings _settings;
        private readonly Action<string> _trace;
        private readonly ChannelFactory<IErpProjectService> _factory;

        public ErpProjectClient(ErpSettings settings, Action<string> trace = null)
        {
            _settings = settings;
            _trace = trace ?? (_ => { });
            _factory = new ChannelFactory<IErpProjectService>(CreateBinding(settings), new EndpointAddress(settings.Endpoint));
            if (!string.IsNullOrEmpty(settings.UserName))
            {
                _factory.Credentials.UserName.UserName = settings.UserName;
                _factory.Credentials.UserName.Password = settings.Password;
            }
        }

        public static Binding CreateBinding(ErpSettings settings)
        {
            var https = settings.Endpoint.StartsWith("https:", StringComparison.OrdinalIgnoreCase);
            var binding = new BasicHttpBinding(https ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.TransportCredentialOnly)
            {
                MaxReceivedMessageSize = 16 * 1024 * 1024,
                SendTimeout = settings.Timeout,
                ReceiveTimeout = settings.Timeout
            };
            binding.ReaderQuotas.MaxStringContentLength = 1024 * 1024;
            binding.Security.Transport.ClientCredentialType = string.IsNullOrEmpty(settings.UserName)
                ? HttpClientCredentialType.None
                : HttpClientCredentialType.Basic;
            if (!https && string.IsNullOrEmpty(settings.UserName))
            {
                binding.Security.Mode = BasicHttpSecurityMode.None;
            }

            return binding;
        }

        public IEnumerable<ErpProject> GetChangedProjects(DateTime sinceUtc)
        {
            var page = 1;
            while (true)
            {
                var current = page;
                var result = Call(c => c.GetProjectsChangedSince(sinceUtc, current, _settings.PageSize));
                _trace("ERP projects page " + current + ": " + (result.Projects?.Count ?? 0) + " of " + result.TotalCount);
                if (result.Projects != null)
                {
                    foreach (var p in result.Projects)
                    {
                        yield return p;
                    }
                }

                if (!result.HasMore)
                {
                    yield break;
                }

                page++;
            }
        }

        public ErpProject GetProject(string projectNumber)
        {
            return Call(c => c.GetProject(projectNumber));
        }

        public IList<ErpTimesheetLine> GetApprovedTimesheetLines(DateTime from, DateTime to, string projectNumber)
        {
            return Call(c => c.GetApprovedTimesheetLines(from, to, projectNumber)) ?? new List<ErpTimesheetLine>();
        }

        private T Call<T>(Func<IErpProjectService, T> call)
        {
            return RetryPolicies.Wcf((ex, delay) => _trace("ERP call failed (" + ex.Message + "), retrying in " + delay)).Execute(() =>
            {
                var channel = _factory.CreateChannel();
                var comm = (ICommunicationObject)channel;
                try
                {
                    var result = call(channel);
                    comm.Close();
                    return result;
                }
                catch (FaultException<ErpFault> fault)
                {
                    comm.Abort();
                    throw new IntegrationException("ERP fault " + fault.Detail.Code + ": " + fault.Detail.Message, fault);
                }
                catch
                {
                    comm.Abort();
                    throw;
                }
            });
        }
    }
}
