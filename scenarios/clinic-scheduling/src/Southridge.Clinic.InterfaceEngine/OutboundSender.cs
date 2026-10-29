using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using log4net;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Hl7;

namespace Southridge.Clinic.InterfaceEngine
{
    /// <summary>
    /// Sends one partner's outbox in order. A message that cannot be delivered blocks the ones behind it for that
    /// partner (HL7 receivers expect updates in sequence), so failures back off and retry.
    /// </summary>
    public class OutboundSender
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OutboundSender));
        private static readonly ILog Traffic = LogManager.GetLogger("Traffic");
        private static readonly int[] RetryMinutes = { 1, 2, 5, 10, 15, 30, 60 };

        private readonly SenderElement _config;
        private readonly InterfaceRepository _repository;
        private readonly MllpClient _client;
        private readonly TimeSpan _interval;
        private Timer _timer;
        private int _busy;
        private volatile bool _stopping;

        public OutboundSender(SenderElement config, InterfaceRepository repository, TimeSpan interval)
        {
            _config = config;
            _repository = repository;
            _interval = interval;
            _client = new MllpClient(config.Host, config.Port, config.TimeoutSeconds * 1000);
        }

        public string Partner
        {
            get { return _config.Partner; }
        }

        public int SentCount { get; private set; }

        public int FailedCount { get; private set; }

        public void Start()
        {
            _timer = new Timer(Tick, null, TimeSpan.FromSeconds(1), _interval);
            Log.InfoFormat("Sending {0} outbox to {1}:{2} every {3}s", _config.Partner, _config.Host, _config.Port, _interval.TotalSeconds);
        }

        public void Stop()
        {
            _stopping = true;
            if (_timer != null)
            {
                _timer.Dispose();
            }

            for (int i = 0; i < 50 && _busy == 1; i++)
            {
                Thread.Sleep(100);
            }
        }

        /// <summary>One pass over the outbox. Public so the console mode can run a single pass (/once).</summary>
        public int RunOnce()
        {
            List<OutboundMessage> batch = _repository.Dequeue(_config.Partner, _config.BatchSize, _config.TimeoutSeconds * 2 + 30);
            int sent = 0;
            bool blocked = false;
            foreach (OutboundMessage message in batch)
            {
                if (_stopping)
                {
                    break;
                }

                if (blocked)
                {
                    // Leave it for the next pass: release the lease without counting the attempt as a failure.
                    _repository.Complete(message.MessageId, MessageStatus.Pending, null, null, 0);
                    continue;
                }

                if (Send(message))
                {
                    sent++;
                }
                else
                {
                    blocked = true;
                }
            }

            return sent;
        }

        private void Tick(object state)
        {
            if (_stopping || Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                return;
            }

            try
            {
                RunOnce();
            }
            catch (Exception ex)
            {
                Log.Error("Outbox pass for " + _config.Partner + " failed.", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private bool Send(OutboundMessage message)
        {
            Hl7Message ack;
            try
            {
                Traffic.Info("OUT " + _config.Partner + " " + message.Payload.Replace('\r', '\n'));
                ack = _client.Send(Hl7Message.Parse(message.Payload));
                Traffic.Info("ACK " + _config.Partner + " " + ack.ToString().Replace('\r', '\n'));
            }
            catch (Exception ex)
            {
                if (!(ex is SocketException || ex is IOException || ex is Hl7Exception))
                {
                    throw;
                }

                Retry(message, "Delivery failed: " + ex.Message);
                return false;
            }

            if (Acknowledgement.IsPositive(ack))
            {
                _repository.Complete(message.MessageId, MessageStatus.Acknowledged, ack.ToString(), null, null);
                AfterAcknowledged(message);
                SentCount++;
                return true;
            }

            string code = ack.Get("MSA-1");
            string text = ack.Get("MSA-3");
            if (code == AckCode.Reject || code == "CR")
            {
                // The receiver will never take this message; park it and let the queue move on.
                _repository.Complete(message.MessageId, MessageStatus.Error, ack.ToString(), "Rejected (" + code + "): " + text, null);
                FailedCount++;
                Log.Warn(_config.Partner + " rejected " + message.MessageType + " " + message.ControlId + ": " + text);
                return true;
            }

            Retry(message, "Application error (" + code + "): " + text);
            return false;
        }

        private void Retry(OutboundMessage message, string error)
        {
            FailedCount++;
            if (message.Attempts >= _config.MaxAttempts)
            {
                _repository.Complete(message.MessageId, MessageStatus.Error, null, error + " (gave up after " + message.Attempts + " attempts)", null);
                Log.Error(_config.Partner + " " + message.MessageType + " " + message.ControlId + " moved to Error: " + error);
                return;
            }

            int minutes = RetryMinutes[Math.Min(message.Attempts, RetryMinutes.Length) - 1];
            _repository.Complete(message.MessageId, MessageStatus.Pending, null, error, minutes);
            Log.Warn(_config.Partner + " " + message.MessageType + " " + message.ControlId + " attempt " + message.Attempts + " failed, retry in " + minutes + " min: " + error);
        }

        /// <summary>The lab order is marked sent once the lab has acknowledged the ORM.</summary>
        private void AfterAcknowledged(OutboundMessage message)
        {
            if (message.MessageType == MessageTypes.Order)
            {
                string orderNumber = Hl7Message.Parse(message.Payload).Get("ORC-2");
                if (!string.IsNullOrEmpty(orderNumber))
                {
                    _repository.MarkLabOrderSent(orderNumber);
                }
            }
        }
    }
}
