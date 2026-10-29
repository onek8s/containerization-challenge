using System;
using System.Collections.Generic;
using System.Linq;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Hl7;
using Southridge.Clinic.Infrastructure;

namespace Southridge.Clinic.Scheduling
{
    /// <summary>
    /// Builds the outbound HL7 messages for scheduling and registration events and puts them in the interface
    /// outbox (InterfaceMessage). The interface engine service sends them.
    /// </summary>
    public class InterfaceNotifier
    {
        private static readonly Hl7Party Sender = new Hl7Party("SMGCLINIC", "SMG");

        private readonly InterfaceRepository _interfaces;
        private readonly SystemRepository _system;

        public InterfaceNotifier()
            : this(new InterfaceRepository(), new SystemRepository())
        {
        }

        public InterfaceNotifier(InterfaceRepository interfaces, SystemRepository system)
        {
            _interfaces = interfaces;
            _system = system;
        }

        public static Hl7Party Receiver(string partner)
        {
            switch (partner)
            {
                case Partners.Hospital:
                    return new Hl7Party(ClinicSettings.Get("Hl7.HospitalApplication", "STCADT"), ClinicSettings.Get("Hl7.HospitalFacility", "STCATH"));
                case Partners.HospitalBilling:
                    return new Hl7Party(ClinicSettings.Get("Hl7.HospitalBillingApplication", "STCPFS"), ClinicSettings.Get("Hl7.HospitalFacility", "STCATH"));
                default:
                    return new Hl7Party(ClinicSettings.Get("Hl7.LabApplication", "LABSYS"), ClinicSettings.Get("Hl7.LabFacility", "CASCADELAB"));
            }
        }

        public void AppointmentEvent(int appointmentId, string trigger)
        {
            if (!_system.GetSetting("Interface.HospitalEnabled", true))
            {
                return;
            }

            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                var a = db.Appointments.Where(x => x.AppointmentId == appointmentId)
                    .Select(x => new { Appointment = x, x.Patient, x.Provider, x.Site, x.AppointmentType })
                    .SingleOrDefault();
                if (a == null || !a.AppointmentType.SendToHospital)
                {
                    // Only imaging and echo bookings are shared with the hospital (performed on hospital equipment).
                    return;
                }

                Hl7Message message = MessageBuilder.Siu(trigger, ToHl7(a.Appointment, a.Site, a.AppointmentType), ToHl7(a.Patient), ToHl7(a.Provider),
                    Sender, Receiver(Partners.Hospital), ControlIdGenerator.Next("SIU"));
                _interfaces.Enqueue(Partners.Hospital, message, a.Patient.PatientId);
            }
        }

        public void PatientEvent(int patientId, string trigger)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Patient patient = db.Patients.SingleOrDefault(p => p.PatientId == patientId);
                if (patient == null)
                {
                    return;
                }

                Hl7Patient hl7 = ToHl7(patient);
                if (_system.GetSetting("Interface.LabEnabled", true))
                {
                    _interfaces.Enqueue(Partners.Lab, MessageBuilder.Adt(trigger, hl7, Sender, Receiver(Partners.Lab), ControlIdGenerator.Next("ADT"), null), patientId);
                }

                if (_system.GetSetting("Interface.HospitalEnabled", true) && !string.IsNullOrEmpty(patient.HospitalId))
                {
                    _interfaces.Enqueue(Partners.Hospital, MessageBuilder.Adt(trigger, hl7, Sender, Receiver(Partners.Hospital), ControlIdGenerator.Next("ADT"), null), patientId);
                }
            }
        }

        public void LabOrder(int labOrderId)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                var o = db.LabOrders.Where(x => x.LabOrderId == labOrderId)
                    .Select(x => new { Order = x, x.Patient, x.LabTest, Provider = db.Providers.Single(p => p.ProviderId == x.ProviderId) })
                    .Single();
                var order = new Hl7Order
                {
                    PlacerOrderNumber = o.Order.OrderNumber,
                    TestCode = o.LabTest.TestCode,
                    TestName = o.LabTest.Name,
                    OrderedAt = o.Order.OrderedOn,
                    Priority = o.Order.Priority == 'S' ? "S" : "R",
                    ClinicalInfo = o.Order.ClinicalInfo
                };
                Hl7Message message = MessageBuilder.Orm(order, ToHl7(o.Patient), ToHl7(o.Provider), Sender, Receiver(Partners.Lab), ControlIdGenerator.Next("ORM"));
                _interfaces.Enqueue(Partners.Lab, message, o.Patient.PatientId);
            }
        }

        /// <summary>Charges for services read by hospital radiologists go to the hospital's billing system.</summary>
        public void EncounterCharges(int encounterId)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                var e = db.Encounters.Where(x => x.EncounterId == encounterId)
                    .Select(x => new { Encounter = x, x.Patient, x.Provider, x.Site })
                    .Single();
                if (e.Site.Code != "IMG")
                {
                    return;
                }

                List<string> dx = db.EncounterDiagnosis.Where(d => d.EncounterId == encounterId).OrderBy(d => d.Sequence).Select(d => d.Code).ToList();
                Hl7Charge[] charges = db.Charges.Where(c => c.EncounterId == encounterId && c.Status != (byte)ChargeStatus.Voided)
                    .OrderBy(c => c.LineNumber)
                    .Select(c => new Hl7Charge
                    {
                        TransactionId = c.ChargeId.ToString(),
                        DateOfService = e.Encounter.DateOfService,
                        ProcedureCode = c.CptCode,
                        ProcedureDescription = c.ProcedureCode.Description,
                        Modifier = c.Modifier1,
                        Units = c.Units,
                        Amount = c.Amount
                    }).ToArray();
                foreach (Hl7Charge c in charges)
                {
                    c.DiagnosisCodes = dx;
                }

                Hl7Message message = MessageBuilder.Dft(ToHl7(e.Patient), ToHl7(e.Provider), "E" + encounterId, e.Site.Code, charges,
                    Sender, Receiver(Partners.HospitalBilling), ControlIdGenerator.Next("DFT"));
                _interfaces.Enqueue(Partners.HospitalBilling, message, e.Patient.PatientId);
            }
        }

        public static Hl7Patient ToHl7(Patient p)
        {
            return new Hl7Patient
            {
                Mrn = p.Mrn,
                ExternalId = p.HospitalId,
                ExternalAuthority = "STC",
                LastName = p.LastName,
                FirstName = p.FirstName,
                MiddleName = p.MiddleName,
                DateOfBirth = p.DateOfBirth,
                Sex = PatientIdentity.SexCode((Sex)p.Sex),
                Address1 = p.Address1,
                Address2 = p.Address2,
                City = p.City,
                State = p.State,
                Zip = p.Zip,
                HomePhone = PhoneNumber.Format(p.HomePhone),
                Email = p.Email
            };
        }

        public static Hl7Provider ToHl7(Provider p)
        {
            return new Hl7Provider { Npi = p.Npi, FirstName = p.FirstName, LastName = p.LastName, Credentials = p.Credentials };
        }

        public static Hl7Appointment ToHl7(Appointment a, Site site, AppointmentType type)
        {
            return new Hl7Appointment
            {
                AppointmentId = a.AppointmentId,
                Start = a.StartsAt,
                DurationMinutes = (int)(a.EndsAt - a.StartsAt).TotalMinutes,
                AppointmentTypeCode = type.Code,
                AppointmentTypeName = type.Name,
                Reason = a.Reason,
                SiteCode = site.Code,
                SiteName = site.Name,
                CancelReason = a.CancelReason,
                FillerStatus = a.Status == (byte)AppointmentStatus.Cancelled ? "Cancelled" : "Booked"
            };
        }
    }
}
