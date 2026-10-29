using System.Collections.Generic;

namespace Southridge.Clinic.Hl7
{
    /// <summary>Reads the inbound messages we accept from the hospital (ADT) and the lab (ORU).</summary>
    public static class MessageReader
    {
        public static Hl7Patient ReadPatient(Hl7Message message, string hospitalAuthority)
        {
            Hl7Segment pid = message.GetSegment("PID");
            if (pid == null)
            {
                throw new Hl7Exception("PID segment missing.");
            }

            var patient = new Hl7Patient
            {
                LastName = message.Get("PID-5-1"),
                FirstName = message.Get("PID-5-2"),
                MiddleName = message.Get("PID-5-3"),
                DateOfBirth = Hl7DateTime.Parse(message.Get("PID-7")),
                Sex = message.Get("PID-8"),
                Address1 = message.Get("PID-11-1"),
                Address2 = message.Get("PID-11-2"),
                City = message.Get("PID-11-3"),
                State = message.Get("PID-11-4"),
                Zip = message.Get("PID-11-5"),
                HomePhone = message.Get("PID-13-1"),
                Email = message.Get("PID-13-4")
            };

            // PID-3 repeats: our MRN (authority SMG) and the hospital's own number.
            int reps = pid.RepetitionCount(3);
            for (int r = 1; r <= reps; r++)
            {
                string id = pid.Get(3, r, 1, 0);
                string authority = pid.Get(3, r, 4, 0);
                if (authority == "SMG")
                {
                    patient.Mrn = id;
                }
                else if (string.IsNullOrEmpty(hospitalAuthority) || authority == hospitalAuthority || string.IsNullOrEmpty(authority))
                {
                    patient.ExternalId = id;
                    patient.ExternalAuthority = string.IsNullOrEmpty(authority) ? hospitalAuthority : authority;
                }
            }

            // PID-2 (external id) is still used by the older lab interface.
            if (string.IsNullOrEmpty(patient.Mrn) && !string.IsNullOrEmpty(message.Get("PID-2-1")))
            {
                patient.Mrn = message.Get("PID-2-1");
            }

            if (string.IsNullOrEmpty(patient.LastName) || !patient.DateOfBirth.HasValue)
            {
                throw new Hl7Exception("Patient name and date of birth are required.");
            }

            return patient;
        }

        public static Hl7Visit ReadVisit(Hl7Message message)
        {
            return new Hl7Visit
            {
                EventType = message.TriggerEvent,
                PatientClass = message.Get("PV1-2"),
                Facility = message.Get("PV1-3-4"),
                AttendingNpi = message.Get("PV1-7-1"),
                AttendingName = message.Get("PV1-7-2") + ", " + message.Get("PV1-7-3"),
                VisitNumber = message.Get("PV1-19"),
                AdmitAt = Hl7DateTime.Parse(message.Get("PV1-44")),
                DischargeAt = Hl7DateTime.Parse(message.Get("PV1-45")),
                DischargeDisposition = message.Get("PV1-36"),
                Diagnosis = message.Get("DG1-3-2")
            };
        }

        public static List<Hl7Result> ReadResults(Hl7Message message)
        {
            if (message.MessageType != "ORU")
            {
                throw new Hl7Exception("Not a result message: " + message.TypeAndTrigger);
            }

            Hl7Patient patient = ReadPatient(message, null);
            var results = new List<Hl7Result>();
            Hl7Result current = null;
            foreach (Hl7Segment segment in message.Segments)
            {
                if (segment.Name == "OBR")
                {
                    current = new Hl7Result
                    {
                        Patient = patient,
                        PlacerOrderNumber = segment.Get(2, 1),
                        FillerOrderNumber = segment.Get(3, 1),
                        TestCode = segment.Get(4, 1),
                        TestName = segment.Get(4, 2),
                        ObservedAt = Hl7DateTime.Parse(segment.Get(7)),
                        OrderingProviderNpi = segment.Get(16, 1),
                        ResultStatus = segment.Get(25)
                    };
                    results.Add(current);
                }
                else if (segment.Name == "OBX" && current != null)
                {
                    current.Observations.Add(new Hl7Observation
                    {
                        ValueType = segment.Get(2),
                        Code = segment.Get(3, 1),
                        Name = segment.Get(3, 2),
                        Value = segment.Get(5),
                        Units = segment.Get(6, 1),
                        ReferenceRange = segment.Get(7),
                        AbnormalFlag = segment.Get(8),
                        ResultStatus = segment.Get(11),
                        ObservedAt = Hl7DateTime.Parse(segment.Get(14))
                    });
                }
            }

            if (results.Count == 0)
            {
                throw new Hl7Exception("ORU message without OBR segments.");
            }

            return results;
        }
    }
}
