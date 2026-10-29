using System;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Web.Mvc;
using Northwind.Inspections.Contracts;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Web.Models;
using Northwind.Inspections.Web.Services;

namespace Northwind.Inspections.Web.Controllers
{
    public class InspectionsController : Controller
    {
        private readonly InspectionRepository inspections = new InspectionRepository(PortalData.Db);

        public ActionResult Index(bool closed = false)
        {
            var user = PortalUser.Current;
            var list = user.IsSupervisor ? inspections.Recent(200) : inspections.ForAdjuster(user.Id, closed);
            ViewBag.Recent = PortalSession.Recent;
            ViewBag.Notices = PortalSession.TakeNotices();
            ViewBag.ShowClosed = closed;
            return View(list);
        }

        public ActionResult Create()
        {
            var draft = PortalSession.Draft ?? new InspectionForm { InspectedOn = DateTime.Today };
            ViewBag.Perils = PerilList(draft.PerilCode);
            return View(draft);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(InspectionForm form, string saveDraft)
        {
            PortalSession.Draft = form;
            string claim;
            if (!string.IsNullOrEmpty(form.ClaimNumber) && !ClaimNumber.TryNormalize(form.ClaimNumber, out claim))
            {
                ModelState.AddModelError("ClaimNumber", "Use the format HO-2024-004512.");
            }
            if (saveDraft != null || !ModelState.IsValid)
            {
                ViewBag.Perils = PerilList(form.PerilCode);
                ViewBag.DraftSaved = saveDraft != null;
                return View(form);
            }

            var user = PortalUser.Current;
            var id = inspections.Create(new Inspection
            {
                ClaimNumber = ClaimNumber.Normalize(form.ClaimNumber),
                PolicyHolder = form.PolicyHolder.Trim(),
                PropertyAddress = form.PropertyAddress.Trim(),
                PerilCode = form.PerilCode,
                InspectedOn = form.InspectedOn,
                AdjusterId = user.Id,
                HandlerEmail = form.HandlerEmail.Trim(),
                EstimatedLoss = form.EstimatedLoss,
                Notes = form.Notes
            }, user.Login);
            PortalSession.Draft = null;
            return RedirectToRoute("Inspection", new { id });
        }

        public ActionResult Details(int id)
        {
            var inspection = Load(id);
            PortalSession.Visited(id, inspection.ClaimNumber);
            var photos = PhotoServiceClient.Call(c => c.GetPhotos(id));
            string reason;
            ReportSchedule.CanRequestReport(inspection, photos.Count(p => !p.Purged), out reason);
            return View(new InspectionDetailsModel
            {
                Inspection = inspection,
                Photos = photos,
                History = inspections.History(id),
                Notices = PortalSession.TakeNotices(),
                CanEdit = inspection.Status < InspectionStatus.ReportQueued,
                ReportBlockedReason = reason
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Submit(int id)
        {
            var inspection = Load(id);
            if (inspection.Status == InspectionStatus.Draft)
            {
                inspections.SetStatus(id, InspectionStatus.Submitted, PortalUser.Current.Login);
            }
            return RedirectToRoute("Inspection", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Upload(int id, string caption)
        {
            var inspection = Load(id);
            var user = PortalUser.Current;
            var files = Enumerable.Range(0, Request.Files.Count).Select(i => Request.Files[i]).Where(f => f != null && f.ContentLength > 0).ToList();
            if (files.Count == 0)
            {
                PortalSession.AddNotice("Choose one or more photos to upload.");
            }

            foreach (var file in files)
            {
                try
                {
                    var photo = PhotoServiceClient.Call(c => c.UploadPhoto(new UploadPhotoRequest
                    {
                        InspectionId = inspection.Id,
                        FileName = Path.GetFileName(file.FileName),
                        Caption = files.Count == 1 ? caption : null,
                        Content = ReadAll(file),
                        UploadedBy = user.Login
                    }));
                    PortalSession.AddNotice("Uploaded " + photo.FileName + " (" + photo.Width + " x " + photo.Height + ").");
                }
                catch (FaultException<InspectionFault> fault)
                {
                    PortalSession.AddNotice(Path.GetFileName(file.FileName) + ": " + fault.Detail.Message);
                }
            }
            return RedirectToRoute("Inspection", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Caption(int id, Guid photoId, string caption)
        {
            Load(id);
            PhotoServiceClient.Call(c => c.UpdateCaption(photoId, caption, PortalUser.Current.Login));
            return RedirectToRoute("Inspection", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DeletePhoto(int id, Guid photoId)
        {
            Load(id);
            try
            {
                PhotoServiceClient.Call(c => c.DeletePhoto(photoId, PortalUser.Current.Login));
            }
            catch (FaultException<InspectionFault> fault)
            {
                PortalSession.AddNotice(fault.Detail.Message);
            }
            return RedirectToRoute("Inspection", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult RequestReport(int id)
        {
            Load(id);
            try
            {
                var queued = PhotoServiceClient.Call(c => c.RequestReport(id, PortalUser.Current.Login));
                PortalSession.AddNotice("Report request " + queued + " queued. The claim handler receives it by e-mail within a few minutes.");
            }
            catch (FaultException<InspectionFault> fault)
            {
                PortalSession.AddNotice(fault.Detail.Message);
            }
            return RedirectToRoute("Inspection", new { id });
        }

        private Inspection Load(int id)
        {
            var inspection = inspections.Get(id);
            var user = PortalUser.Current;
            if (inspection == null || (!user.IsSupervisor && inspection.AdjusterId != user.Id))
            {
                throw new HttpException(404, "Inspection not found.");
            }
            return inspection;
        }

        private static byte[] ReadAll(HttpPostedFileBase file)
        {
            using (var ms = new MemoryStream(file.ContentLength))
            {
                file.InputStream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static SelectList PerilList(string selected)
        {
            return new SelectList(Perils.Codes.Select(c => new { Code = c, Name = ReportBuilder.PerilName(c) }), "Code", "Name", selected);
        }
    }
}
