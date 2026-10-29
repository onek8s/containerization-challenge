using System;
using System.Linq;
using System.Web.Mvc;
using Southridge.Clinic.PatientWeb.Areas.MyHealth.Models;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth.Controllers
{
    public class FormsController : PatientControllerBase
    {
        private readonly IBookingClient _booking;

        public FormsController(IBookingClient booking, IPatientContext patientContext)
            : base(patientContext)
        {
            _booking = booking;
        }

        public ActionResult Intake(int? appointmentId)
        {
            var model = new IntakeFormModel { AppointmentId = appointmentId };
            Describe(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Intake(IntakeFormModel model)
        {
            if (!ModelState.IsValid)
            {
                Describe(model);
                return View(model);
            }

            _booking.SubmitIntakeForm(new IntakeFormSubmission
            {
                PatientId = Patient.PatientId,
                AppointmentId = model.AppointmentId,
                ReasonForVisit = model.ReasonForVisit,
                Medications = model.Medications,
                Allergies = model.Allergies,
                Conditions = model.Conditions,
                PharmacyName = model.PharmacyName,
                PharmacyPhone = model.PharmacyPhone
            });
            TempData["Message"] = "Thank you. Your form has been sent to the clinic.";
            return RedirectToAction("Index", "Appointments");
        }

        private void Describe(IntakeFormModel model)
        {
            if (!model.AppointmentId.HasValue)
            {
                return;
            }

            AppointmentItem appointment = _booking.GetAppointments(Patient.PatientId, DateTime.Today, DateTime.Today.AddDays(180))
                .FirstOrDefault(a => a.AppointmentId == model.AppointmentId.Value);
            if (appointment == null)
            {
                model.AppointmentId = null;
                return;
            }

            model.AppointmentText = appointment.AppointmentType + " with " + appointment.ProviderName + " on " + appointment.Start.ToString("dddd MMMM d 'at' h:mm tt");
        }
    }
}
