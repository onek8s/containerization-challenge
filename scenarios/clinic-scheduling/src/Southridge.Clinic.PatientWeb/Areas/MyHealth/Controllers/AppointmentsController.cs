using System;
using System.Linq;
using System.Web.Mvc;
using Southridge.Clinic.PatientWeb.Areas.MyHealth.Models;
using Southridge.Clinic.PatientWeb.BookingWs;

namespace Southridge.Clinic.PatientWeb.Areas.MyHealth.Controllers
{
    public class AppointmentsController : PatientControllerBase
    {
        private readonly IBookingClient _booking;

        public AppointmentsController(IBookingClient booking, IPatientContext patientContext)
            : base(patientContext)
        {
            _booking = booking;
        }

        public ActionResult Index()
        {
            DateTime now = DateTime.Now;
            AppointmentItem[] all = _booking.GetAppointments(Patient.PatientId, now.Date.AddYears(-1), now.Date.AddDays(180));
            return View(new AppointmentsViewModel
            {
                PatientName = Patient.FirstName,
                Upcoming = all.Where(a => a.Start >= now.Date).OrderBy(a => a.Start).ToList(),
                Past = all.Where(a => a.Start < now.Date).OrderByDescending(a => a.Start).Take(20).ToList()
            });
        }

        public ActionResult Cancel(int id)
        {
            AppointmentItem appointment = Find(id);
            if (appointment == null || !appointment.CanCancel)
            {
                TempData["Message"] = "That appointment can no longer be cancelled online. Please call the clinic.";
                return RedirectToAction("Index");
            }

            return View(new CancelViewModel { Appointment = appointment });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Cancel")]
        public ActionResult CancelConfirmed(int id, CancelViewModel model)
        {
            try
            {
                _booking.Cancel(Patient.PatientId, id, string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim(), PatientContext.UserName);
                TempData["Message"] = "Your appointment has been cancelled.";
            }
            catch (BookingException ex)
            {
                if (!ex.IsRuleViolation)
                {
                    throw;
                }

                TempData["Message"] = ex.Message;
            }

            return RedirectToAction("Index");
        }

        private AppointmentItem Find(int id)
        {
            return _booking.GetAppointments(Patient.PatientId, DateTime.Today, DateTime.Today.AddDays(180)).FirstOrDefault(a => a.AppointmentId == id);
        }
    }
}
