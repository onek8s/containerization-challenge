using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Messaging;
using System.Threading;
using CreditDesk.Core;

namespace CreditDesk.LetterService
{
    /// <summary>
    /// Reads decisions from the transactional queue, produces the letter in Word, stores the PDF on the
    /// letters share, prints it on letterhead and records the file path in the credit database.
    /// Word automation requires a single-threaded apartment, so all work runs on one STA thread.
    /// </summary>
    internal sealed class LetterProcessor
    {
        private const string EventSource = "Credit Desk Letters";
        private readonly LetterSettings _settings;
        private readonly Dictionary<string, int> _attempts = new Dictionary<string, int>();
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread _thread;
        private WordLetterWriter _writer;
        private int _sinceRecycle;

        public LetterProcessor(LetterSettings settings)
        {
            _settings = settings;
        }

        public string QueuePath => _settings.QueuePath;

        public void Start()
        {
            if (!MessageQueue.Exists(_settings.QueuePath))
                throw new InvalidOperationException("Queue " + _settings.QueuePath + " does not exist. Run install-letter-service.ps1.");
            _thread = new Thread(Run) { Name = "Letters", IsBackground = false };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public void Stop()
        {
            _stop.Set();
            if (_thread != null && !_thread.Join(TimeSpan.FromSeconds(50)))
                _thread.Abort();
        }

        private void Run()
        {
            Log("Letter service started. Template: " + _settings.TemplatePath + ", printer: " + _settings.Printer, EventLogEntryType.Information, 1000);
            using (var queue = new MessageQueue(_settings.QueuePath, QueueAccessMode.Receive))
            {
                queue.Formatter = new XmlMessageFormatter(new[] { typeof(LetterMessage) });
                queue.MessageReadPropertyFilter.SetAll();
                while (!_stop.WaitOne(0))
                {
                    try
                    {
                        ProcessNext(queue);
                    }
                    catch (Exception ex)
                    {
                        Log("Unexpected error: " + ex, EventLogEntryType.Error, 5000);
                        ResetWord();
                        _stop.WaitOne(TimeSpan.FromSeconds(30));
                    }
                }
            }
            ResetWord();
            Log("Letter service stopped.", EventLogEntryType.Information, 1001);
        }

        private void ProcessNext(MessageQueue queue)
        {
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                Message message;
                try
                {
                    message = queue.Receive(TimeSpan.FromSeconds(5), tx);
                }
                catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                {
                    tx.Abort();
                    return;
                }

                LetterMessage letter;
                try
                {
                    letter = (LetterMessage)message.Body;
                }
                catch (InvalidOperationException ex)
                {
                    MoveToFailed(message, tx, "Message body could not be read: " + ex.Message);
                    tx.Commit();
                    return;
                }

                try
                {
                    var path = Produce(letter);
                    RecordLetter(letter.RequestId, path);
                    tx.Commit();
                    _attempts.Remove(message.Id);
                    Log("Letter " + LetterFields.Reference(letter) + " for " + letter.AccountNumber + " written to " + path,
                        EventLogEntryType.Information, 2000);
                }
                catch (Exception ex)
                {
                    ResetWord();
                    _attempts.TryGetValue(message.Id, out var count);
                    _attempts[message.Id] = ++count;
                    if (count >= _settings.MaxAttempts)
                    {
                        MoveToFailed(message, tx, ex.Message);
                        tx.Commit();
                        _attempts.Remove(message.Id);
                        Log("Letter for request " + letter.RequestId + " failed " + count + " times and was moved to "
                            + _settings.FailedQueuePath + ": " + ex, EventLogEntryType.Error, 4000);
                    }
                    else
                    {
                        tx.Abort();
                        Log("Letter for request " + letter.RequestId + " failed (attempt " + count + "): " + ex.Message,
                            EventLogEntryType.Warning, 3000);
                        _stop.WaitOne(TimeSpan.FromSeconds(15 * count));
                    }
                }
            }
        }

        private string Produce(LetterMessage letter)
        {
            if (_writer == null || _sinceRecycle >= _settings.RecycleWordAfter)
            {
                ResetWord();
                _writer = new WordLetterWriter(_settings.TemplatePath);
                _sinceRecycle = 0;
            }

            var folder = Path.Combine(_settings.OutputFolder, letter.DecidedAt.ToString("yyyy"), letter.DecidedAt.ToString("MM"));
            Directory.CreateDirectory(folder);
            var pdf = Path.Combine(folder, LetterFields.FileName(letter));
            _writer.Write(LetterFields.Build(letter, DateTime.Today), pdf, _settings.Printer, _settings.PrintCopies);
            _sinceRecycle++;
            return pdf;
        }

        private void RecordLetter(int requestId, string path)
        {
            using (var connection = new SqlConnection(_settings.ConnectionString))
            using (var cmd = new SqlCommand("cd.MarkLetterSent", connection) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.Int).Value = requestId;
                cmd.Parameters.Add("@LetterPath", SqlDbType.NVarChar, 400).Value = path;
                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private void MoveToFailed(Message message, MessageQueueTransaction tx, string reason)
        {
            using (var failed = new MessageQueue(_settings.FailedQueuePath, QueueAccessMode.Send))
            {
                message.Label = (message.Label + " | " + reason);
                if (message.Label.Length > 249) message.Label = message.Label.Substring(0, 249);
                failed.Send(message, tx);
            }
        }

        private void ResetWord()
        {
            _writer?.Dispose();
            _writer = null;
        }

        private static void Log(string text, EventLogEntryType type, int id)
        {
            try { EventLog.WriteEntry(EventSource, text, type, id); }
            catch (Exception) { Trace.TraceInformation(text); }
        }
    }
}
