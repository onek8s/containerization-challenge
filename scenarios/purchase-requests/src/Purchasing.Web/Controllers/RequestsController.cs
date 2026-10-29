using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Web.Models;
using Contoso.Purchasing.Web.Services;

namespace Contoso.Purchasing.Web.Controllers
{
    public class RequestsController : PortalController
    {
        private const string DraftKey = "Purchasing.DraftRequest";

        public ActionResult Index()
        {
            return View(Api.Requests("mine"));
        }

        public ActionResult Details(int id)
        {
            return View(Api.Request(id));
        }

        public ActionResult Create()
        {
            var form = Session[DraftKey] as RequestForm;
            if (form == null || form.Id != null)
            {
                form = new RequestForm { Category = RequestCategory.Consumables };
                form.Lines.Add(new LineForm { Quantity = 1, Unit = "pcs", VatRate = 25.5m });
                Session[DraftKey] = form;
            }
            return EditView(form);
        }

        public ActionResult Edit(int id)
        {
            var view = Api.Request(id);
            if (!view.CanEdit) return RedirectToAction("Details", new { id });

            var r = view.Request;
            var form = new RequestForm
            {
                Id = r.Id,
                Number = r.Number,
                Title = r.Title,
                CostCenterCode = r.CostCenterCode,
                SupplierId = r.SupplierId,
                Category = r.Category,
                Justification = r.Justification,
                NeededBy = r.NeededBy,
                Lines = r.Lines.Select(l => new LineForm
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate
                }).ToList()
            };
            Session[DraftKey] = form;
            return EditView(form);
        }

        /// <summary>
        /// The form posts back to itself to add or remove lines; the draft is kept in session between postbacks.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(RequestForm form, string command, int? removeLine)
        {
            form.Lines = form.Lines ?? new System.Collections.Generic.List<LineForm>();
            Session[DraftKey] = form;

            if (command == "add-line")
            {
                form.Lines.Add(new LineForm { Quantity = 1, Unit = "pcs", VatRate = 25.5m });
                ModelState.Clear();
                return EditView(form);
            }

            if (removeLine != null && removeLine.Value >= 0 && removeLine.Value < form.Lines.Count)
            {
                form.Lines.RemoveAt(removeLine.Value);
                ModelState.Clear();
                return EditView(form);
            }

            if (!ModelState.IsValid)
            {
                return EditView(form);
            }

            var input = new
            {
                form.CostCenterCode,
                form.SupplierId,
                form.Category,
                form.Title,
                form.Justification,
                form.NeededBy,
                Lines = form.Lines.Select(l => new RequestLine
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate
                }).ToList()
            };

            try
            {
                var saved = form.Id == null ? Api.Create(input) : Api.Update(form.Id.Value, input);
                Session.Remove(DraftKey);

                if (command == "submit")
                {
                    saved = Api.Submit(saved.Request.Id);
                    Flash(saved.Request.Number + " was sent for approval.");
                }
                else
                {
                    Flash(saved.Request.Number + " was saved as a draft.");
                }
                return RedirectToAction("Details", new { id = saved.Request.Id });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return EditView(form);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Submit(int id)
        {
            try
            {
                var view = Api.Submit(id);
                Flash(view.Request.Number + " was sent for approval.");
            }
            catch (ApiException ex)
            {
                Flash(ex.Message);
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            try
            {
                var view = Api.Cancel(id);
                Flash(view.Request.Number + " was cancelled.");
            }
            catch (ApiException ex)
            {
                Flash(ex.Message);
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Attach(int id, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
            {
                Flash("Choose a file to attach.");
                return RedirectToAction("Details", new { id });
            }

            try
            {
                var attachment = Api.Upload(id, System.IO.Path.GetFileName(file.FileName), file.ContentType, file.InputStream);
                Flash(attachment.FileName + " was attached.");
            }
            catch (ApiException ex)
            {
                Flash(ex.Message);
            }
            return RedirectToAction("Details", new { id });
        }

        public ActionResult Attachment(int id)
        {
            string contentType, fileName;
            var data = Api.Download(id, out contentType, out fileName);
            return File(data, contentType, fileName);
        }

        private ActionResult EditView(RequestForm form)
        {
            var model = new RequestEditModel
            {
                Form = form,
                CostCenters = Api.CostCenters(),
                Suppliers = Api.Suppliers(),
                Categories = Api.Categories()
            };
            return View("Edit", model);
        }
    }
}
