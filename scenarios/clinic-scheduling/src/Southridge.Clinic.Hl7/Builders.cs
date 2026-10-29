using System;
using System.Globalization;

namespace Southridge.Clinic.Hl7
{
    /// <summary>
    /// Header values for messages sent by the clinic system (MSH-3/4). Receiving application and facility come
    /// from the partner configuration.
    /// </summary>
    public class Hl7Party
    {
        public Hl7Party(string application, string facility)
        {
            Application = application;
            Facility = facility;
        }

        public string Application { get; private set; }

        public string Facility { get; private set; }
    }

    public static class MessageBuilder
    {
        public const string Version = "2.3";

        public static Hl7Message NewMessage(string typeAndTrigger, Hl7Party sender, Hl7Party receiver, string controlId)
        {
            string[] parts = typeAndTrigger.Split('^');
            return Hl7Message.Create(parts[0], parts[1], null, sender.Application, sender.Facility, receiver.Application, receiver.Facility,
                controlId, DateTime.Now, Version);
        }

        public static void AddEvent(Hl7Message message, string trigger, string operatorId)
        {
            message.Set("EVN-1", trigger);
            message.Set("EVN-2", Hl7DateTime.Format(DateTime.Now));
            if (!string.IsNullOrEmpty(operatorId))
            {
                message.Set("EVN-5", operatorId);
            }
        }

        public static void AddPatient(Hl7Message message, Hl7Patient patient)
        {
            message.Set("PID-1", "1");
            message.Set("PID-3-1", patient.Mrn);
            message.Set("PID-3-4", "SMG");
            message.Set("PID-3-5", "MR");
            if (!string.IsNullOrEmpty(patient.ExternalId))
            {
                Hl7Segment pid = message.GetSegment("PID");
                pid.AddRepetition(3, message.Encoding.Escape(patient.ExternalId) + "^^^" + message.Encoding.Escape(patient.ExternalAuthority) + "^PI");
            }

            message.Set("PID-5-1", patient.LastName);
            message.Set("PID-5-2", patient.FirstName);
            message.Set("PID-5-3", patient.MiddleName);
            message.Set("PID-7", patient.DateOfBirth.HasValue ? Hl7DateTime.FormatDate(patient.DateOfBirth.Value) : string.Empty);
            message.Set("PID-8", patient.Sex);
            message.Set("PID-11-1", patient.Address1);
            message.Set("PID-11-2", patient.Address2);
            message.Set("PID-11-3", patient.City);
            message.Set("PID-11-4", patient.State);
            message.Set("PID-11-5", patient.Zip);
            message.Set("PID-13-1", patient.HomePhone);
            if (!string.IsNullOrEmpty(patient.Email))
            {
                message.Set("PID-13-4", patient.Email);
            }
        }

        public static Hl7Message Adt(string trigger, Hl7Patient patient, Hl7Party sender, Hl7Party receiver, string controlId, string operatorId)
        {
            Hl7Message message = NewMessage("ADT^" + trigger, sender, receiver, controlId);
            AddEvent(message, trigger, operatorId);
            AddPatient(message, patient);
            message.Set("PV1-1", "1");
            message.Set("PV1-2", "O");
            return message;
        }

        public static Hl7Message Siu(string trigger, Hl7Appointment appointment, Hl7Patient patient, Hl7Provider provider, Hl7Party sender, Hl7Party receiver, string controlId)
        {
            Hl7Message message = NewMessage("SIU^" + trigger, sender, receiver, controlId);
            string id = appointment.AppointmentId.ToString(CultureInfo.InvariantCulture);
            message.Set("SCH-1-1", id);
            message.Set("SCH-1-2", "SMG");
            message.Set("SCH-2-1", id);
            message.Set("SCH-6-1", trigger == "S15" ? appointment.CancelReason ?? "CANCELLED" : string.Empty);
            message.Set("SCH-7-1", appointment.AppointmentTypeCode);
            message.Set("SCH-7-2", appointment.Reason);
            message.Set("SCH-8-1", appointment.AppointmentTypeCode);
            message.Set("SCH-8-2", appointment.AppointmentTypeName);
            message.Set("SCH-9", appointment.DurationMinutes.ToString(CultureInfo.InvariantCulture));
            message.Set("SCH-10-1", "MIN");
            message.Set("SCH-11-3", appointment.DurationMinutes.ToString(CultureInfo.InvariantCulture));
            message.Set("SCH-11-4", Hl7DateTime.Format(appointment.Start));
            message.Set("SCH-11-5", Hl7DateTime.Format(appointment.Start.AddMinutes(appointment.DurationMinutes)));
            message.Set("SCH-25-1", appointment.FillerStatus ?? "Booked");
            AddPatient(message, patient);
            message.Set("RGS-1", "1");
            message.Set("AIG-1", "1");
            message.Set("AIG-3-1", provider.Npi);
            message.Set("AIG-3-2", provider.LastName + ", " + provider.FirstName);
            message.Set("AIG-4-1", "PROVIDER");
            message.Set("AIL-1", "1");
            message.Set("AIL-3-1", appointment.SiteCode);
            message.Set("AIL-3-4", appointment.SiteName);
            message.Set("AIP-1", "1");
            message.Set("AIP-3-1", provider.Npi);
            message.Set("AIP-3-2", provider.LastName);
            message.Set("AIP-3-3", provider.FirstName);
            message.Set("AIP-3-6", provider.Credentials);
            message.Set("AIP-4", "ATTENDING");
            message.Set("AIP-6", Hl7DateTime.Format(appointment.Start));
            message.Set("AIP-8", appointment.DurationMinutes.ToString(CultureInfo.InvariantCulture));
            message.Set("AIP-9", "MIN");
            return message;
        }

        public static Hl7Message Dft(Hl7Patient patient, Hl7Provider provider, string visitNumber, string siteCode, Hl7Charge[] charges, Hl7Party sender, Hl7Party receiver, string controlId)
        {
            Hl7Message message = NewMessage("DFT^P03", sender, receiver, controlId);
            AddEvent(message, "P03", null);
            AddPatient(message, patient);
            message.Set("PV1-1", "1");
            message.Set("PV1-2", "O");
            message.Set("PV1-3-1", siteCode);
            message.Set("PV1-7-1", provider.Npi);
            message.Set("PV1-7-2", provider.LastName);
            message.Set("PV1-7-3", provider.FirstName);
            message.Set("PV1-19", visitNumber);
            for (int i = 0; i < charges.Length; i++)
            {
                Hl7Charge c = charges[i];
                string ft1 = "FT1(" + (i + 1).ToString(CultureInfo.InvariantCulture) + ")";
                message.AddSegment("FT1");
                message.Set(ft1 + "-1", (i + 1).ToString(CultureInfo.InvariantCulture));
                message.Set(ft1 + "-2", c.TransactionId);
                message.Set(ft1 + "-4", Hl7DateTime.FormatDate(c.DateOfService));
                message.Set(ft1 + "-6", "CG");
                message.Set(ft1 + "-7-1", c.ProcedureCode);
                message.Set(ft1 + "-7-2", c.ProcedureDescription);
                message.Set(ft1 + "-7-3", "CPT");
                message.Set(ft1 + "-10", c.Units.ToString(CultureInfo.InvariantCulture));
                message.Set(ft1 + "-11", c.Amount.ToString("0.00", CultureInfo.InvariantCulture));
                message.Set(ft1 + "-20-1", provider.Npi);
                message.Set(ft1 + "-25-1", c.ProcedureCode);
                if (!string.IsNullOrEmpty(c.Modifier))
                {
                    message.Set(ft1 + "-26", c.Modifier);
                }

                if (c.DiagnosisCodes != null)
                {
                    Hl7Segment seg = message.GetSegment("FT1", i + 1);
                    foreach (string dx in c.DiagnosisCodes)
                    {
                        seg.AddRepetition(19, message.Encoding.Escape(dx) + "^^I10");
                    }
                }
            }

            return message;
        }

        public static Hl7Message Orm(Hl7Order order, Hl7Patient patient, Hl7Provider provider, Hl7Party sender, Hl7Party receiver, string controlId)
        {
            Hl7Message message = NewMessage("ORM^O01", sender, receiver, controlId);
            AddPatient(message, patient);
            message.Set("PV1-1", "1");
            message.Set("PV1-2", "O");
            message.Set("ORC-1", "NW");
            message.Set("ORC-2", order.PlacerOrderNumber);
            message.Set("ORC-9", Hl7DateTime.Format(order.OrderedAt));
            message.Set("ORC-12-1", provider.Npi);
            message.Set("ORC-12-2", provider.LastName);
            message.Set("ORC-12-3", provider.FirstName);
            message.Set("OBR-1", "1");
            message.Set("OBR-2", order.PlacerOrderNumber);
            message.Set("OBR-4-1", order.TestCode);
            message.Set("OBR-4-2", order.TestName);
            message.Set("OBR-4-3", "L");
            message.Set("OBR-5", order.Priority ?? "R");
            message.Set("OBR-13", order.ClinicalInfo);
            message.Set("OBR-16-1", provider.Npi);
            message.Set("OBR-16-2", provider.LastName);
            message.Set("OBR-16-3", provider.FirstName);
            return message;
        }
    }
}
