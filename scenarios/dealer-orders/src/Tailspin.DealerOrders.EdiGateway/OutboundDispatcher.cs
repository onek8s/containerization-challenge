using System;
using System.Collections.Concurrent;
using System.Linq;
using log4net;
using Tailspin.DealerOrders.Domain;
using Tailspin.DealerOrders.EdiGateway.As2;
using Tailspin.DealerOrders.EdiGateway.Sftp;
using Tailspin.DealerOrders.Processing.Ports;

namespace Tailspin.DealerOrders.EdiGateway
{
    /// <summary>Sends queued 855, 856 and 997 interchanges over each partner's transport.</summary>
    public class OutboundDispatcher
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OutboundDispatcher));

        // Messages that failed wait before the next attempt; kept in memory, a restart retries at once.
        private static readonly ConcurrentDictionary<int, DateTime> NotBefore = new ConcurrentDictionary<int, DateTime>();

        private readonly IEdiStore _store;
        private readonly As2Sender _as2;
        private readonly IFileTransport _files;
        private readonly GatewaySettings _settings;
        private readonly IClock _clock;

        public OutboundDispatcher(IEdiStore store, As2Sender as2, IFileTransport files, GatewaySettings settings, IClock clock)
        {
            _store = store;
            _as2 = as2;
            _files = files;
            _settings = settings;
            _clock = clock;
        }

        public int Run(int max)
        {
            var now = _clock.UtcNow;
            var due = _store.Outbound(EdiMessageState.Queued, max)
                .Where(m => !NotBefore.TryGetValue(m.Id, out var next) || next <= now).ToList();
            var sent = 0;
            foreach (var group in due.GroupBy(m => m.PartnerId))
            {
                var partner = _store.GetPartner(group.Key);
                if (partner == null || !partner.Active)
                {
                    foreach (var m in group) Fail(m, "Partner is not active.", permanent: true);
                    continue;
                }

                if (partner.Transport == EdiTransport.As2)
                {
                    foreach (var message in group)
                    {
                        try
                        {
                            var result = _as2.Send(partner, message);
                            message.As2MessageId = result.MessageId;
                            message.MdnStatus = result.Disposition;
                            if (result.Delivered) { Sent(message); sent++; }
                            else Fail(message, result.Error, permanent: false);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn($"AS2 send of {message.TransactionSet} {message.Reference} to {partner.Name} failed", ex);
                            Fail(message, ex.GetBaseException().Message, permanent: false);
                        }
                    }
                }
                else
                {
                    try
                    {
                        using (var session = _files.Open(partner))
                        {
                            foreach (var message in group)
                            {
                                session.Write($"{message.TransactionSet}_{message.InterchangeControl}.x12", message.Content);
                                Sent(message);
                                sent++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"SFTP upload to {partner.Name} failed", ex);
                        foreach (var m in group.Where(m => m.State == EdiMessageState.Queued)) Fail(m, ex.GetBaseException().Message, permanent: false);
                    }
                }
            }
            return sent;
        }

        private void Sent(EdiMessage message)
        {
            message.State = EdiMessageState.Sent;
            message.SentUtc = _clock.UtcNow;
            message.Attempts++;
            message.Error = null;
            _store.SaveMessage(message);
            NotBefore.TryRemove(message.Id, out _);
        }

        private void Fail(EdiMessage message, string error, bool permanent)
        {
            message.Attempts++;
            message.Error = error;
            if (permanent || message.Attempts >= _settings.MaxSendAttempts)
            {
                message.State = EdiMessageState.Failed;
                Log.ErrorFormat("{0} {1} to partner {2} failed for good: {3}", message.TransactionSet, message.Reference, message.PartnerId, error);
            }
            else
            {
                NotBefore[message.Id] = _clock.UtcNow.AddMinutes(Math.Min(60, message.Attempts * message.Attempts));
            }
            _store.SaveMessage(message);
        }
    }

    /// <summary>Picks up purchase orders that SFTP dealers drop into their inbound folder.</summary>
    public class SftpPoller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SftpPoller));

        private readonly IEdiStore _store;
        private readonly IFileTransport _files;
        private readonly Func<Processing.Edi.EdiOrderIntake> _intake;

        public SftpPoller(IEdiStore store, IFileTransport files, Func<Processing.Edi.EdiOrderIntake> intake)
        {
            _store = store;
            _files = files;
            _intake = intake;
        }

        public int Run()
        {
            var count = 0;
            foreach (var partner in _store.Partners().Where(p => p.Active && p.Transport == EdiTransport.Sftp))
            {
                try
                {
                    using (var session = _files.Open(partner))
                    {
                        foreach (var name in session.ListInbound())
                        {
                            var content = session.Read(name);
                            try
                            {
                                var result = _intake().Receive(content, "sftp:" + name, partner);
                                Log.InfoFormat("{0} from {1}: ISA {2}, orders {3}, rejected {4}", name, partner.Name, result.InterchangeControl,
                                    string.Join(",", result.CreatedOrders), result.Rejected.Count);
                            }
                            catch (Edi.X12.X12FormatException ex)
                            {
                                Log.Error($"{name} from {partner.Name} is not a valid interchange; archived without processing", ex);
                            }
                            session.Archive(name);
                            count++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"SFTP poll of {partner.Name} failed", ex);
                }
            }
            return count;
        }
    }
}
