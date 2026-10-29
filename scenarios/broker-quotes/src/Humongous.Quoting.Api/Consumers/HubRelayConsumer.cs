using Humongous.Quoting.Api.Hubs;
using Humongous.Quoting.Contracts.Hubs;
using Humongous.Quoting.Contracts.Messages;
using MassTransit;

namespace Humongous.Quoting.Api.Consumers;

/// <summary>Passes events from the policy and document services to the connected portal and workbench users.</summary>
public sealed class HubRelayConsumer(HubNotifier notifier) :
    IConsumer<PolicyIssued>,
    IConsumer<BindFailed>,
    IConsumer<DocumentReady>,
    IConsumer<AdjustmentProcessed>,
    IConsumer<PolicyCancelled>,
    IConsumer<RenewalInvited>
{
    public async Task Consume(ConsumeContext<PolicyIssued> context)
    {
        var m = context.Message;
        await notifier.PolicyChanged(new PolicyNotice(m.PolicyId, m.PolicyNumber, m.AgencyCode, "Issued", null));
        await notifier.QuoteChanged(new QuoteNotice(m.QuoteId, "", m.AgencyCode, Domain.QuoteStatus.Bound, null, $"Policy {m.PolicyNumber} issued."));
    }

    public Task Consume(ConsumeContext<BindFailed> context) =>
        notifier.QuoteChanged(new QuoteNotice(context.Message.QuoteId, "", context.Message.AgencyCode,
            Domain.QuoteStatus.Quoted, null, context.Message.Reason));

    public Task Consume(ConsumeContext<DocumentReady> context)
    {
        var m = context.Message;
        return notifier.DocumentReady(m.AgencyCode, new DocumentNotice(m.DocumentId, m.DocumentType.ToString(), m.QuoteId, m.PolicyId, m.FileName));
    }

    public Task Consume(ConsumeContext<AdjustmentProcessed> context)
    {
        var m = context.Message;
        var text = m.Succeeded
            ? $"Change processed, premium {(m.AdditionalPremium >= 0 ? "additional" : "return")} {Math.Abs(m.AdditionalPremium):N2}."
            : m.Message;
        return notifier.PolicyChanged(new PolicyNotice(m.PolicyId, "", m.AgencyCode, m.Referred ? "AdjustmentReferred" : "Adjusted", text));
    }

    public Task Consume(ConsumeContext<PolicyCancelled> context) =>
        notifier.PolicyChanged(new PolicyNotice(context.Message.PolicyId, "", context.Message.AgencyCode, "Cancelled",
            $"Return premium {context.Message.ReturnPremium:N2}."));

    public Task Consume(ConsumeContext<RenewalInvited> context) =>
        notifier.PolicyChanged(new PolicyNotice(context.Message.PolicyId, "", context.Message.AgencyCode, "RenewalInvited",
            $"Renewal terms {context.Message.Status}."));
}

public sealed class HubRelayConsumerDefinition : ConsumerDefinition<HubRelayConsumer>
{
    public HubRelayConsumerDefinition()
    {
        // Each API server has its own queue so every server's hub clients get the events.
        Endpoint(e => e.InstanceId = Environment.MachineName.ToLowerInvariant());
    }
}
