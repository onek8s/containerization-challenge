using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Http;
using Contoso.Purchasing.Api.Infrastructure;
using Contoso.Purchasing.Api.Models;
using Contoso.Purchasing.Core;
using Contoso.Purchasing.Data;

namespace Contoso.Purchasing.Api.Controllers
{
    [RoutePrefix("api/requests")]
    public class RequestsController : PurchasingController
    {
        private RequestRepository Requests
        {
            get { return new RequestRepository(Db); }
        }

        [HttpGet, Route("")]
        public IEnumerable<RequestSummary> List(string scope = "mine")
        {
            IEnumerable<PurchaseRequest> list;
            switch ((scope ?? "mine").ToLowerInvariant())
            {
                case "mine":
                    list = Requests.ByRequester(CurrentUser.Account);
                    break;
                case "approvals":
                    list = Requests.ByStatus(RequestStatus.InApproval).Where(r => Policy.CanDecide(r, CurrentUser));
                    break;
                case "ordering":
                    if (!CurrentUser.IsBuyer) throw new HttpResponseException(HttpStatusCode.Forbidden);
                    list = Requests.ByStatus(RequestStatus.Approved);
                    break;
                case "all":
                    if (!(CurrentUser.IsFinance || CurrentUser.IsAdministrator)) throw new HttpResponseException(HttpStatusCode.Forbidden);
                    list = Requests.ByStatus(RequestStatus.InApproval, RequestStatus.Approved, RequestStatus.Rejected, RequestStatus.Ordered);
                    break;
                default:
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            return list.Select(Summarize).ToList();
        }

        [HttpGet, Route("{id:int}")]
        public RequestView Get(int id)
        {
            return View(Load(id));
        }

        [HttpPost, Route("")]
        public RequestView Create(RequestInput input)
        {
            if (input == null) throw new HttpResponseException(HttpStatusCode.BadRequest);

            var user = CurrentUser;
            var request = new PurchaseRequest
            {
                RequesterAccount = user.Account,
                RequesterName = user.DisplayName,
                RequesterEmail = user.Email,
                CreatedAt = Now
            };
            Apply(request, input);
            Requests.Create(request);

            Trace.TraceInformation("{0} created {1}", user.Account, request.Number);
            return View(Requests.Get(request.Id));
        }

        [HttpPut, Route("{id:int}")]
        public RequestView Update(int id, RequestInput input)
        {
            var request = Load(id);
            EnsureOwner(request);
            Apply(request, input);
            Requests.UpdateDraft(request);
            return View(Requests.Get(id));
        }

        [HttpPost, Route("{id:int}/submit")]
        public RequestView Submit(int id)
        {
            var request = Load(id);
            EnsureOwner(request);
            Policy.Submit(request, Now);
            Requests.SaveWorkflow(request);

            Trace.TraceInformation("{0} submitted {1} ({2:0.00} {3})", CurrentUser.Account, request.Number, request.NetTotal, request.Currency);
            return View(Requests.Get(id));
        }

        [HttpPost, Route("{id:int}/cancel")]
        public RequestView Cancel(int id)
        {
            var request = Load(id);
            EnsureOwner(request);
            if (request.Status == RequestStatus.Ordered || request.Status == RequestStatus.Cancelled)
            {
                throw new PolicyException("Request " + request.Number + " cannot be cancelled.");
            }
            request.Status = RequestStatus.Cancelled;
            Requests.SaveWorkflow(request);
            return View(request);
        }

        [HttpPost, Route("{id:int}/decision")]
        public RequestView Decide(int id, DecisionInput input)
        {
            if (input == null) throw new HttpResponseException(HttpStatusCode.BadRequest);

            var request = Load(id);
            var status = Policy.Apply(request, CurrentUser, input.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected, input.Comment, Now);
            Requests.SaveWorkflow(request);

            Trace.TraceInformation("{0} {1} {2}, status now {3}", CurrentUser.Account, input.Approve ? "approved" : "rejected", request.Number, status);
            return View(Requests.Get(id));
        }

        private PurchaseRequest Load(int id)
        {
            var request = Requests.Get(id);
            if (request == null) throw new HttpResponseException(HttpStatusCode.NotFound);
            if (!CanView(request)) throw new HttpResponseException(HttpStatusCode.Forbidden);
            return request;
        }

        private void EnsureOwner(PurchaseRequest request)
        {
            if (!string.Equals(request.RequesterAccount, CurrentUser.Account, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpResponseException(HttpStatusCode.Forbidden);
            }
        }

        private static void Apply(PurchaseRequest request, RequestInput input)
        {
            request.CostCenterCode = input.CostCenterCode;
            request.SupplierId = input.SupplierId;
            request.Category = input.Category;
            request.Title = input.Title;
            request.Justification = input.Justification;
            request.NeededBy = input.NeededBy;
            request.Lines = (input.Lines ?? new List<RequestLine>()).Where(l => !string.IsNullOrWhiteSpace(l.Description)).ToList();

            if (string.IsNullOrWhiteSpace(request.Title) || request.Lines.Count == 0)
            {
                throw new PolicyException("A title and at least one line are required.");
            }

            if (string.IsNullOrWhiteSpace(request.CostCenterCode) || request.SupplierId <= 0)
            {
                throw new PolicyException("Choose a cost center and a supplier.");
            }
        }

        private RequestView View(PurchaseRequest request)
        {
            var user = CurrentUser;
            var isOwner = string.Equals(request.RequesterAccount, user.Account, StringComparison.OrdinalIgnoreCase);
            return new RequestView
            {
                Request = request,
                RequiredSteps = Policy.RequiredSteps(request),
                NextStep = Policy.NextStep(request),
                CanDecide = Policy.CanDecide(request, user),
                CanEdit = isOwner && (request.Status == RequestStatus.Draft || request.Status == RequestStatus.Rejected),
                CanOrder = user.IsBuyer && request.Status == RequestStatus.Approved
            };
        }

        private RequestSummary Summarize(PurchaseRequest r)
        {
            return new RequestSummary
            {
                Id = r.Id,
                Number = r.Number,
                Title = r.Title,
                RequesterName = r.RequesterName,
                CostCenterCode = r.CostCenterCode,
                SupplierName = r.SupplierName,
                Status = r.Status,
                NextStep = Policy.NextStep(r),
                NetTotal = r.NetTotal,
                Currency = r.Currency,
                CreatedAt = r.CreatedAt,
                SubmittedAt = r.SubmittedAt,
                PoNumber = r.PoNumber
            };
        }
    }
}
