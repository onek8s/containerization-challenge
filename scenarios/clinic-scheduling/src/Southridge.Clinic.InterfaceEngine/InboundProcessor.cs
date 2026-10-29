using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Hl7;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.InterfaceEngine
{
    /// <summary>
    /// Handles one inbound message: hospital ADT updates the patient table, lab ORU stores results. Every message
    /// is logged with its ACK. Application errors are acknowledged with AE so the sender does not resend forever.
    /// </summary>
    public class InboundProcessor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(InboundProcessor));
        private static readonly ILog Traffic = LogManager.GetLogger("Traffic");

        private readonly InterfaceRepository _repository;
        private readonly string _hospitalAuthority;

        public InboundProcessor(InterfaceRepository repository)
        {
            _repository = repository;
            _hospitalAuthority = ClinicSettings.Get("Hl7.HospitalAuthority", "STC");
        }

        public string Process(string partner, string accept, string text)
        {
            Traffic.Info("IN " + partner + " " + text.Replace('\r', '\n'));
            Hl7Message message;
            try
            {
                message = Hl7Message.Parse(text);
            }
            catch (Hl7Exception ex)
            {
                Log.Warn(partner + ": unparseable message: " + ex.Message);
                _repository.LogInbound(partner, null, null, text, MessageStatus.Error, null, null, ex.Message);
                return RawReject(ex.Message);
            }

            string type = message.TypeAndTrigger;
            string controlId = message.ControlId;
            string ackId = ControlIdGenerator.Next("ACK");
            if (!Accepts(accept, type))
            {
                Hl7Message reject = Acknowledgement.Create(message, AckCode.Reject, "Message type " + type + " is not accepted on this interface", ackId);
                _repository.LogInbound(partner, type, controlId, text, MessageStatus.Ignored, null, reject.ToString(), "Not accepted: " + type);
                return Respond(partner, reject);
            }

            if (_repository.IsDuplicate(partner, controlId))
            {
                Log.InfoFormat("{0}: duplicate {1} {2} acknowledged without processing", partner, type, controlId);
                Hl7Message dup = Acknowledgement.Create(message, AckCode.Accept, "Duplicate", ackId);
                _repository.LogInbound(partner, type, controlId, text, MessageStatus.Ignored, null, dup.ToString(), "Duplicate control id");
                return Respond(partner, dup);
            }

            int? patientId = null;
            try
            {
                string note;
                patientId = Handle(message, partner, out note);
                Hl7Message ack = Acknowledgement.Create(message, AckCode.Accept, note, ackId);
                _repository.LogInbound(partner, type, controlId, text, MessageStatus.Processed, patientId, ack.ToString(), null);
                Log.InfoFormat("{0}: {1} {2} processed ({3})", partner, type, controlId, note);
                return Respond(partner, ack);
            }
            catch (Exception ex)
            {
                bool expected = ex is Hl7Exception || ex is ClinicRuleException;
                if (expected)
                {
                    Log.Warn(partner + ": " + type + " " + controlId + " rejected: " + ex.Message);
                }
                else
                {
                    Log.Error(partner + ": " + type + " " + controlId + " failed.", ex);
                }

                Hl7Message error = Acknowledgement.Create(message, AckCode.Error, expected ? ex.Message : "Processing error", ackId);
                _repository.LogInbound(partner, type, controlId, text, MessageStatus.Error, patientId, error.ToString(), ex.Message);
                return Respond(partner, error);
            }
        }

        private int? Handle(Hl7Message message, string partner, out string note)
        {
            switch (message.MessageType)
            {
                case "ADT":
                    return HandleAdt(message, out note);
                case "ORU":
                    return HandleResult(message, out note);
                default:
                    throw new Hl7Exception("No handler for " + message.TypeAndTrigger + " from " + partner + ".");
            }
        }

        /// <summary>A04/A01 may create a clinic chart (hospital registration); A08/A03 only update an existing one.</summary>
        private int? HandleAdt(Hl7Message message, out string note)
        {
            Hl7Patient patient = MessageReader.ReadPatient(message, _hospitalAuthority);
            if (string.IsNullOrEmpty(patient.LastName) || !patient.DateOfBirth.HasValue)
            {
                throw new Hl7Exception("PID-5 name and PID-7 date of birth are required.");
            }

            string trigger = message.TriggerEvent;
            bool create = trigger == "A04" || trigger == "A01";
            PatientUpsertResult result = _repository.UpsertPatient(patient, create);
            note = result.Action;
            return result.PatientId > 0 ? result.PatientId : (int?)null;
        }

        private int? HandleResult(Hl7Message message, out string note)
        {
            List<Hl7Result> results = MessageReader.ReadResults(message);
            Hl7Patient patient = results[0].Patient;
            PatientUpsertResult match = _repository.UpsertPatient(patient, false);
            if (match.PatientId <= 0)
            {
                throw new ClinicRuleException("No clinic patient matches MRN " + patient.Mrn + ".");
            }

            int count = 0;
            foreach (Hl7Result result in results)
            {
                foreach (Hl7Observation observation in result.Observations.Where(o => !string.IsNullOrEmpty(o.Code)))
                {
                    _repository.InsertLabResult(match.PatientId, result, observation, null);
                    count++;
                }
            }

            note = count + " results";
            return match.PatientId;
        }

        private static bool Accepts(string accept, string type)
        {
            foreach (string allowed in accept.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string a = allowed.Trim();
                if (a == "*" || string.Equals(a, type, StringComparison.OrdinalIgnoreCase) || (a.EndsWith("^*", StringComparison.Ordinal) && type.StartsWith(a.Substring(0, a.Length - 1), StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Respond(string partner, Hl7Message ack)
        {
            string text = ack.ToString();
            Traffic.Info("ACK " + partner + " " + text.Replace('\r', '\n'));
            return text;
        }

        /// <summary>ACK for a message whose MSH could not be read: echo nothing we cannot trust.</summary>
        private static string RawReject(string reason)
        {
            Hl7Message ack = Hl7Message.Create("ACK", string.Empty, "ACK", "SMGCLINIC", "SMG", string.Empty, string.Empty, ControlIdGenerator.Next("ACK"), DateTime.Now, "2.3");
            ack.Set("MSA-1", AckCode.Reject);
            ack.Set("MSA-3", reason.Length > 80 ? reason.Substring(0, 80) : reason);
            return ack.ToString();
        }
    }
}
