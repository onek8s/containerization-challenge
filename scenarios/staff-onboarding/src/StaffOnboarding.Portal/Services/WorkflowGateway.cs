using System.ServiceModel;
using Microsoft.Extensions.Options;
using StaffOnboarding.Contracts;
using StaffOnboarding.Core.Requests;

namespace StaffOnboarding.Portal.Services;

/// <summary>
/// Sends messages to the joiner, mover and leaver workflow services. Calls are made with the application
/// pool identity; the acting user is passed in the message.
/// </summary>
public sealed class WorkflowGateway : IDisposable
{
    private readonly ChannelFactory<IJoinerProcess> _joiner;
    private readonly ChannelFactory<IMoverProcess> _mover;
    private readonly ChannelFactory<ILeaverProcess> _leaver;

    public WorkflowGateway(IOptions<PortalOptions> options)
    {
        var o = options.Value;
        var baseAddress = new Uri(o.WorkflowBaseAddress.EndsWith('/') ? o.WorkflowBaseAddress : o.WorkflowBaseAddress + "/");
        _joiner = Create<IJoinerProcess>(baseAddress, "joiner", o.WorkflowSpn);
        _mover = Create<IMoverProcess>(baseAddress, "mover", o.WorkflowSpn);
        _leaver = Create<ILeaverProcess>(baseAddress, "leaver", o.WorkflowSpn);
    }

    public Task<string> SubmitAsync(JoinerRequest request) => Call(_joiner, c => c.Submit(request));
    public Task<string> SubmitAsync(MoverRequest request) => Call(_mover, c => c.Submit(request));
    public Task<string> SubmitAsync(LeaverRequest request) => Call(_leaver, c => c.Submit(request));

    public Task DecideAsync(RequestKind kind, ApprovalDecision decision) => kind switch
    {
        RequestKind.Joiner => Call(_joiner, c => { c.Decide(decision); return true; }),
        RequestKind.Mover => Call(_mover, c => { c.Decide(decision); return true; }),
        _ => Call(_leaver, c => { c.Decide(decision); return true; })
    };

    public Task CancelAsync(RequestKind kind, string requestNumber, string by, string reason) => kind switch
    {
        RequestKind.Joiner => Call(_joiner, c => { c.Cancel(requestNumber, by, reason); return true; }),
        RequestKind.Mover => Call(_mover, c => { c.Cancel(requestNumber, by, reason); return true; }),
        _ => Call(_leaver, c => { c.Cancel(requestNumber, by, reason); return true; })
    };

    public Task ChangeStartDateAsync(string requestNumber, DateTime startDate, string by) =>
        Call(_joiner, c => { c.ChangeStartDate(requestNumber, startDate, by); return true; });

    public Task ChangeLastWorkingDayAsync(string requestNumber, DateTime lastDay, string by) =>
        Call(_leaver, c => { c.ChangeLastWorkingDay(requestNumber, lastDay, by); return true; });

    public Task DisableNowAsync(string requestNumber, string by, string reason) =>
        Call(_leaver, c => { c.DisableNow(requestNumber, by, reason); return true; });

    private static ChannelFactory<T> Create<T>(Uri baseAddress, string path, string? spn) where T : class
    {
        var binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly)
        {
            SendTimeout = TimeSpan.FromSeconds(60),
            MaxReceivedMessageSize = 1024 * 1024
        };
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;

        var identity = string.IsNullOrEmpty(spn) ? null : new SpnEndpointIdentity(spn);
        var address = new EndpointAddress(new Uri(baseAddress, path), identity);
        var factory = new ChannelFactory<T>(binding, address);
        factory.Credentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Identification;
        return factory;
    }

    /// <summary>
    /// A message the instance is not waiting for (for example a second decision on a closed task)
    /// comes back as a fault from the workflow host.
    /// </summary>
    private static async Task<TResult> Call<T, TResult>(ChannelFactory<T> factory, Func<T, TResult> call) where T : class
    {
        var channel = factory.CreateChannel();
        var client = (IClientChannel)channel;
        try
        {
            var result = await Task.Run(() => call(channel));
            client.Close();
            return result;
        }
        catch (FaultException ex)
        {
            client.Abort();
            throw new WorkflowRejectedException(ex.Message, ex);
        }
        catch
        {
            client.Abort();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var f in new ICommunicationObject[] { _joiner, _mover, _leaver })
        {
            try { f.Close(); } catch { f.Abort(); }
        }
    }
}

public sealed class WorkflowRejectedException(string message, Exception inner) : Exception(message, inner);
