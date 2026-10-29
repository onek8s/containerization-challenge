using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using log4net;
using VanArsdel.Integration.Data;

namespace VanArsdel.Integration.Management.Operations
{
    public class ResubmitRequest
    {
        public Guid InstanceId { get; set; }
        public Guid MessageId { get; set; }
        public string Reason { get; set; }

        /// <summary>Edited body text; null resubmits the original bytes.</summary>
        public string EditedBody { get; set; }

        public bool TerminateOriginal { get; set; }
        public string RequestedBy { get; set; }
    }

    public class ResubmitOutcome
    {
        public long ResubmitId { get; set; }
        public string DroppedFile { get; set; }
        public bool OriginalTerminated { get; set; }
        public string Warning { get; set; }
    }

    /// <summary>
    /// Resubmits a suspended message by writing its body to the resubmit share of the receive port it came in on.
    /// Each EDI/XML receive port has a FILE receive location on \\b2bfs01\b2b$\resubmit\&lt;port&gt; with the same
    /// receive pipeline, so a resubmitted interchange is disassembled, resolved and tracked exactly like the original.
    /// </summary>
    public class ResubmitService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ResubmitService));
        private readonly IInstanceOperations _instances;
        private readonly ResubmitRepository _log;
        private readonly IDictionary<string, string> _folders;
        private readonly string _defaultFolder;

        public ResubmitService(IInstanceOperations instances, ResubmitRepository log, IDictionary<string, string> foldersByReceivePort, string defaultFolder)
        {
            _instances = instances;
            _log = log;
            _folders = new Dictionary<string, string>(foldersByReceivePort ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            _defaultFolder = defaultFolder;
        }

        /// <summary>Reads the folders from appSettings keys "Resubmit.Folder.&lt;ReceivePortName&gt;" and "Resubmit.DefaultFolder".</summary>
        public static ResubmitService FromConfig(IInstanceOperations instances, ResubmitRepository log)
        {
            var folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string prefix = "Resubmit.Folder.";
            foreach (string key in ConfigurationManager.AppSettings.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    folders[key.Substring(prefix.Length)] = ConfigurationManager.AppSettings[key];
                }
            }

            return new ResubmitService(instances, log, folders, ConfigurationManager.AppSettings["Resubmit.DefaultFolder"]);
        }

        public string TargetFor(InstanceMessage message)
        {
            string folder;
            if (!string.IsNullOrEmpty(message.ReceivePortName) && _folders.TryGetValue(message.ReceivePortName, out folder))
            {
                return folder;
            }

            return _defaultFolder;
        }

        public ResubmitOutcome Resubmit(ResubmitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new ArgumentException("A reason is required for every resubmission.", "request");
            }

            var instance = _instances.Find(request.InstanceId);
            if (instance == null)
            {
                throw new InvalidOperationException("Instance " + request.InstanceId + " no longer exists.");
            }

            var message = _instances.GetMessage(request.InstanceId, request.MessageId);
            if (message == null)
            {
                throw new InvalidOperationException("Message " + request.MessageId + " is not part of instance " + request.InstanceId + ".");
            }

            var folder = TargetFor(message);
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidOperationException("No resubmit folder is configured for receive port '" + message.ReceivePortName + "'.");
            }

            var original = message.Body ?? new byte[0];
            var edited = request.EditedBody != null;
            var submitted = edited ? EncodingFor(message.Charset).GetBytes(request.EditedBody) : original;

            var entry = new ResubmitEntry
            {
                OriginalInstanceId = request.InstanceId,
                OriginalMessageId = request.MessageId,
                ServiceName = instance.ServiceName,
                PartnerId = message.PartnerId,
                MessageType = message.MessageType,
                Reason = request.Reason.Trim(),
                BodyEdited = edited,
                ResubmittedBy = request.RequestedBy,
                Target = folder
            };

            var id = _log.Log(entry, original, submitted);
            var file = Path.Combine(folder, FileName(message, id));

            // write under a temporary name; the FILE adapter only picks up the final extension
            var temp = file + ".tmp";
            File.WriteAllBytes(temp, submitted);
            File.Move(temp, file);
            Log.InfoFormat("Resubmission {0} of message {1} by {2} written to {3} (edited: {4})", id, request.MessageId, request.RequestedBy, file, edited);

            var outcome = new ResubmitOutcome { ResubmitId = id, DroppedFile = file };
            if (request.TerminateOriginal)
            {
                var result = _instances.Terminate(request.InstanceId);
                if (result.Succeeded)
                {
                    _log.MarkOriginalTerminated(id);
                    outcome.OriginalTerminated = true;
                }
                else
                {
                    outcome.Warning = "The message was resubmitted but the original instance could not be terminated: " + result.Message;
                }
            }

            return outcome;
        }

        internal static string FileName(InstanceMessage message, long resubmitId)
        {
            var partner = string.IsNullOrEmpty(message.PartnerId) ? "UNKNOWN" : message.PartnerId;
            var extension = ExtensionFor(message);
            return string.Format(CultureInfo.InvariantCulture, "{0}_RS{1:D7}_{2:N}{3}", partner, resubmitId, message.MessageId, extension);
        }

        private static string ExtensionFor(InstanceMessage message)
        {
            var location = message.InboundTransportLocation ?? string.Empty;
            var name = location.Substring(location.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            var dot = name.LastIndexOf('.');
            var ext = dot < 0 ? string.Empty : name.Substring(dot);
            if (!string.IsNullOrEmpty(ext) && ext != ".*" && ext.Length <= 5)
            {
                return ext;
            }

            return (message.MessageType ?? string.Empty).StartsWith("http", StringComparison.Ordinal) ? ".xml" : ".edi";
        }

        private static Encoding EncodingFor(string charset)
        {
            if (string.IsNullOrEmpty(charset))
            {
                return new UTF8Encoding(false);
            }

            try
            {
                var encoding = Encoding.GetEncoding(charset);
                return encoding is UTF8Encoding ? new UTF8Encoding(false) : encoding;
            }
            catch (ArgumentException)
            {
                return new UTF8Encoding(false);
            }
        }
    }
}
