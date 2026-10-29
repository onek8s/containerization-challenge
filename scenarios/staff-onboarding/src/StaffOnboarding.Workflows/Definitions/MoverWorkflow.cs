using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Statements;
using System.Collections.Generic;
using System.ServiceModel.Activities;
using StaffOnboarding.Contracts;
using StaffOnboarding.Core.Access;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Activities;

namespace StaffOnboarding.Workflows.Definitions
{
    /// <summary>
    /// Mover: approval by the new manager (and IT for privileged access), new access on the effective
    /// date, old role access removed after the handover period.
    /// </summary>
    public static class MoverWorkflow
    {
        private const string C = ContractNames.Mover;

        public static WorkflowService Create(Version version)
        {
            var request = new Variable<MoverRequest>("request");
            var number = new Variable<string>("requestNumber");
            var correlation = new Variable<CorrelationHandle>("correlation");
            var settings = new Variable<ProcessSettings>("settings");
            var subject = new Variable<string>("subject");
            var current = new Variable<AccountSummary>("current");
            var plan = new Variable<AccessPlan>("plan");
            var outcome = new Variable<DecisionOutcome>("outcome");
            var decision = new Variable<ApprovalDecision>("decision");
            var rejected = new Variable<bool>("rejected");
            var finished = new Variable<bool>("finished");
            var cancelled = new Variable<bool>("cancelled");
            var cancelledBy = new Variable<string>("cancelledBy");
            var cancelReason = new Variable<string>("cancelReason");
            var update = new Variable<UpdateAccessCommand>("update");
            var applied = new Variable<ProvisioningResult>("applied");
            var removed = new Variable<ProvisioningResult>("removed");

            var submit = new Receive
            {
                ServiceContractName = Wf.Contract(C),
                OperationName = "Submit",
                CanCreateInstance = true,
                Content = new ReceiveParametersContent { Parameters = { { "request", new OutArgument<MoverRequest>(request) } } },
                CorrelationInitializers =
                {
                    new QueryCorrelationInitializer { CorrelationHandle = correlation, MessageQuerySet = Wf.RequestKey("Submit", "request", true) }
                }
            };

            var approvedPath = new Sequence
            {
                Activities =
                {
                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Scheduled },
                    Wf.WaitUntil(number, Milestone.MoverApply, env => request.Get(env).EffectiveDate, null, "Apply new access"),

                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Provisioning },
                    new BuildMoverUpdate { Request = request, Plan = plan, AfterHandover = false, Result = update },
                    Wf.ProvisionWithRetry(env => update.Get(env), applied, settings),
                    new If(env => !applied.Get(env).Succeeded)
                    {
                        Then = JoinerWorkflow.Failed(number, applied, settings),
                        Else = new Sequence
                        {
                            Activities =
                            {
                                new Persist(),
                                new Notify
                                {
                                    RequestNumber = number,
                                    Template = "mover-applied",
                                    Recipients = Wf.In(env => new[] { request.Get(env).NewPosition.ManagerAccount, current.Get(env).ManagerAccount, request.Get(env).SamAccountName }),
                                    Values = Wf.In(env => new Dictionary<string, string>
                                    {
                                        { "Subject", subject.Get(env) },
                                        { "Title", request.Get(env).NewPosition.Title },
                                        { "Added", string.Join(", ", plan.Get(env).AddGroups) },
                                        { "RemovedLater", string.Join(", ", plan.Get(env).DeferredRemoveGroups) },
                                        { "HandoverDays", request.Get(env).HandoverDays.ToString() }
                                    })
                                },
                                new If(env => plan.Get(env).DeferredRemoveGroups.Count > 0)
                                {
                                    Then = new Sequence
                                    {
                                        Activities =
                                        {
                                            new RecordStatus { RequestNumber = number, Status = RequestStatus.AwaitingCleanup, Detail = "Handover period" },
                                            Wf.WaitUntil(number, Milestone.MoverRemoveOldAccess, env => request.Get(env).EffectiveDate,
                                                env => request.Get(env).HandoverDays, "Remove previous access"),
                                            new BuildMoverUpdate { Request = request, Plan = plan, AfterHandover = true, Result = update },
                                            Wf.ProvisionWithRetry(env => update.Get(env), removed, settings),
                                            new If(env => !removed.Get(env).Succeeded)
                                            {
                                                Then = JoinerWorkflow.Failed(number, removed, settings)
                                            }
                                        }
                                    }
                                },
                                new If(env => removed.Get(env) == null || removed.Get(env).Succeeded)
                                {
                                    Then = new RecordStatus { RequestNumber = number, Status = RequestStatus.Completed }
                                }
                            }
                        }
                    }
                }
            };

            var main = new Sequence
            {
                DisplayName = "Mover",
                Activities =
                {
                    new GetAccount { SamAccountName = Wf.In(env => request.Get(env).SamAccountName), Result = current },
                    new If(env => current.Get(env) == null)
                    {
                        Then = new RecordStatus { RequestNumber = number, Status = RequestStatus.Failed, Detail = "Account not found in the directory." },
                        Else = new Sequence
                        {
                            Activities =
                            {
                                new PlanMoverAccess { Request = request, Account = current, Result = plan },
                                Wf.Approval(C, TaskKind.ManagerApproval, number, correlation, env => request.Get(env).NewPosition.ManagerAccount,
                                    settings, subject, outcome, decision),
                                new If(env => outcome.Get(env) != DecisionOutcome.Approved)
                                {
                                    Then = Wf.Reject(number, decision, rejected, env => new[] { request.Get(env).RequestedBy, current.Get(env).ManagerAccount })
                                },
                                new If(env => !rejected.Get(env) && plan.Get(env).RequiresITApproval)
                                {
                                    Then = new Sequence
                                    {
                                        Activities =
                                        {
                                            Wf.Approval(C, TaskKind.ITApproval, number, correlation, env => settings.Get(env).ITApprovers,
                                                settings, subject, outcome, decision),
                                            new If(env => outcome.Get(env) != DecisionOutcome.Approved)
                                            {
                                                Then = Wf.Reject(number, decision, rejected, env => new[]
                                                {
                                                    request.Get(env).RequestedBy, request.Get(env).NewPosition.ManagerAccount
                                                })
                                            }
                                        }
                                    }
                                },
                                new If(env => !rejected.Get(env)) { Then = approvedPath }
                            }
                        }
                    },
                    Wf.Set(finished, true)
                }
            };

            var body = new Sequence
            {
                Variables =
                {
                    request, number, correlation, settings, subject, current, plan, outcome, decision, rejected, finished,
                    cancelled, cancelledBy, cancelReason, update, applied, removed
                },
                Activities =
                {
                    submit,
                    new SendReply
                    {
                        Request = submit,
                        Content = new SendParametersContent { Parameters = { { "SubmitResult", Wf.In(env => request.Get(env).RequestNumber) } } }
                    },
                    Wf.Set(number, env => request.Get(env).RequestNumber),
                    new LoadProcessSettings { Result = settings },
                    new RequestSubject { SamAccountName = Wf.In(env => request.Get(env).SamAccountName), Result = subject },
                    new AttachInstance { RequestNumber = number, Kind = RequestKind.Mover, Subject = subject, Definition = "Mover " + version },
                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Submitted, Actor = Wf.In(env => request.Get(env).RequestedBy) },

                    new Parallel
                    {
                        CompletionCondition = new LambdaValue<bool>(env => finished.Get(env) || cancelled.Get(env)),
                        Branches =
                        {
                            main,
                            new Sequence { Activities = { Wf.ReceiveCancel(C, correlation, cancelledBy, cancelReason), Wf.Set(cancelled, true) } }
                        }
                    },

                    new If(env => cancelled.Get(env))
                    {
                        Then = new Sequence
                        {
                            Activities =
                            {
                                new CloseOpenTasks { RequestNumber = number, Status = "Cancelled" },
                                new ClearSchedule { RequestNumber = number },
                                new If(env => applied.Get(env) != null && applied.Get(env).Succeeded)
                                {
                                    Then = new AddTimeline
                                    {
                                        RequestNumber = number,
                                        Event = "Cancelled after changes",
                                        Detail = "New access was already applied. Previous role groups are kept; review the account manually."
                                    }
                                },
                                new RecordStatus { RequestNumber = number, Status = RequestStatus.Cancelled, Actor = cancelledBy, Detail = cancelReason }
                            }
                        }
                    }
                }
            };

            return new WorkflowService
            {
                Name = Wf.Contract(C),
                ConfigurationName = "StaffOnboarding." + C,
                DefinitionIdentity = new WorkflowIdentity("Mover", version, null),
                Body = body
            };
        }
    }
}
