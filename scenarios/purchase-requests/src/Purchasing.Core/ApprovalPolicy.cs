using System;
using System.Collections.Generic;
using System.Linq;

namespace Contoso.Purchasing.Core
{
    /// <summary>
    /// Approval chain from the purchasing policy (rev. 2022-03).
    /// Net amounts are compared in EUR.
    /// </summary>
    public class ApprovalPolicy
    {
        public const decimal FinanceThreshold = 2500m;
        public const decimal DirectorThreshold = 15000m;

        public IList<ApprovalStep> RequiredSteps(PurchaseRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");

            var steps = new List<ApprovalStep> { ApprovalStep.Manager };
            var net = request.NetTotal;

            if (net >= FinanceThreshold || request.Category == RequestCategory.Capital || request.Category == RequestCategory.ItEquipment)
            {
                steps.Add(ApprovalStep.Finance);
            }

            if (net >= DirectorThreshold || request.Category == RequestCategory.Capital)
            {
                steps.Add(ApprovalStep.Director);
            }

            return steps;
        }

        public ApprovalStep? NextStep(PurchaseRequest request)
        {
            if (request.Status != RequestStatus.InApproval) return null;

            foreach (var step in RequiredSteps(request))
            {
                if (!request.Approvals.Any(a => a.Step == step && a.Decision == ApprovalDecision.Approved))
                {
                    return step;
                }
            }

            return null;
        }

        public bool CanDecide(PurchaseRequest request, UserContext user)
        {
            var next = NextStep(request);
            if (next == null) return false;

            if (string.Equals(request.RequesterAccount, user.Account, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // The same person may not approve two steps of the same request.
            if (request.Approvals.Any(a => string.Equals(a.ApproverAccount, user.Account, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            switch (next.Value)
            {
                case ApprovalStep.Manager:
                    return user.Manages(request.CostCenterCode);
                case ApprovalStep.Finance:
                    return user.IsFinance;
                case ApprovalStep.Director:
                    return user.IsDirector;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Applies a decision and returns the resulting status of the request.
        /// </summary>
        public RequestStatus Apply(PurchaseRequest request, UserContext user, ApprovalDecision decision, string comment, DateTime now)
        {
            if (!CanDecide(request, user))
            {
                throw new PolicyException("You cannot approve or reject request " + request.Number + ".");
            }

            if (decision == ApprovalDecision.Rejected && string.IsNullOrWhiteSpace(comment))
            {
                throw new PolicyException("A comment is required when rejecting a request.");
            }

            var step = NextStep(request).Value;
            request.Approvals.Add(new ApprovalRecord
            {
                Step = step,
                ApproverAccount = user.Account,
                ApproverName = user.DisplayName,
                Decision = decision,
                Comment = comment,
                DecidedAt = now
            });

            if (decision == ApprovalDecision.Rejected)
            {
                request.Status = RequestStatus.Rejected;
            }
            else if (NextStep(request) == null)
            {
                request.Status = RequestStatus.Approved;
            }

            return request.Status;
        }

        public void Submit(PurchaseRequest request, DateTime now)
        {
            if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.Rejected)
            {
                throw new PolicyException("Only draft or rejected requests can be submitted.");
            }

            var errors = Validate(request);
            if (errors.Count > 0)
            {
                throw new PolicyException(string.Join(" ", errors));
            }

            request.Approvals.Clear();
            request.Status = RequestStatus.InApproval;
            request.SubmittedAt = now;
            request.LastReminderAt = null;
        }

        public IList<string> Validate(PurchaseRequest request)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("Title is required.");
            if (string.IsNullOrWhiteSpace(request.CostCenterCode)) errors.Add("Cost center is required.");
            if (request.SupplierId <= 0) errors.Add("Supplier is required.");
            if (!RequestCategory.All.Contains(request.Category)) errors.Add("Category is not valid.");
            if (request.Lines.Count == 0) errors.Add("At least one line is required.");
            if (request.Lines.Any(l => l.Quantity <= 0)) errors.Add("Quantities must be greater than zero.");
            if (request.Lines.Any(l => l.UnitPrice < 0)) errors.Add("Unit prices cannot be negative.");
            if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) errors.Add("Every line needs a description.");
            if (request.Category == RequestCategory.Capital && string.IsNullOrWhiteSpace(request.Justification))
            {
                errors.Add("Capital requests need a business justification.");
            }
            return errors;
        }
    }
}
