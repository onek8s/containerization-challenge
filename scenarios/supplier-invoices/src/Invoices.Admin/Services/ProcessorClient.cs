using System.ServiceModel;
using Wingtip.Invoices.Contracts.Control;

namespace Wingtip.Invoices.Admin.Services;

/// <summary>
/// Calls the control endpoint of the InvoiceProcessor service as the application pool account.
/// The signed-in user is passed as a parameter for the audit trail.
/// </summary>
public class ProcessorClient
{
    private readonly ChannelFactory<IProcessorControl> factory;
    private readonly ILogger<ProcessorClient> logger;

    public ProcessorClient(IConfiguration configuration, ILogger<ProcessorClient> logger)
    {
        this.logger = logger;
        var binding = new NetTcpBinding(SecurityMode.Transport)
        {
            SendTimeout = TimeSpan.FromSeconds(configuration.GetValue("Processor:TimeoutSeconds", 30)),
            MaxReceivedMessageSize = 1024 * 1024
        };
        binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;

        var identity = configuration["Processor:ServiceIdentity"];
        var address = string.IsNullOrEmpty(identity)
            ? new EndpointAddress(configuration["Processor:ControlAddress"])
            : new EndpointAddress(new Uri(configuration["Processor:ControlAddress"]), new SpnEndpointIdentity(identity));
        factory = new ChannelFactory<IProcessorControl>(binding, address);
    }

    public Task<QueueStatus[]> GetQueueStatusAsync() => Call(c => c.GetQueueStatus());

    public Task ResubmitFileAsync(long fileId, string user) => Call(c => { c.ResubmitFile(fileId, user); return true; });

    public Task<MatchSummary> RematchAsync(long invoiceId, string user) => Call(c => c.RematchInvoice(invoiceId, user));

    public Task ReleaseAsync(long invoiceId, string user, string comment) => Call(c => { c.ReleaseInvoice(invoiceId, user, comment); return true; });

    private Task<T> Call<T>(Func<IProcessorControl, T> call)
    {
        return Task.Run(() =>
        {
            var channel = factory.CreateChannel();
            var client = (IClientChannel)channel;
            try
            {
                var result = call(channel);
                client.Close();
                return result;
            }
            catch (FaultException<ControlFault> fault)
            {
                client.Abort();
                throw new ProcessorException(fault.Detail.Message, fault);
            }
            catch (CommunicationException ex)
            {
                client.Abort();
                logger.LogWarning(ex, "Invoice processor control call failed");
                throw new ProcessorException("The invoice processor service is not reachable.", ex);
            }
            catch (TimeoutException ex)
            {
                client.Abort();
                throw new ProcessorException("The invoice processor service did not answer in time.", ex);
            }
        });
    }
}

public class ProcessorException : Exception
{
    public ProcessorException(string message, Exception inner) : base(message, inner)
    {
    }
}
