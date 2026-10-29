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
    /// Leaver: manager confirms, account is disabled at the end of the last working day (or at once),
    /// home folder is handed over and archived after the retention period.
    /// </summary>
    public static class LeaverWorkflow
    {
        private const string C = ContractNames.Leaver;

        public static WorkflowService Create(Version version)
        {
            var request = new Variable<LeaverRequest>("request");
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
            var disabled = new Variable<ProvisioningResult>("disabled");
            var archived = new Variable<ProvisioningResult>("archived");
            var handoverTask = new Variable<Guid>("handoverTask");
            var handover = new Variable<ApprovalDecision>("handover");

            var submit = new Receive
            {
                ServiceContractName = Wf.Contract(C),
                OperationName = "Submit",
                CanCreateInstance = true,
                Content = new ReceiveParametersContent { Parameters = { { "request", new OutArgument<LeaverRequest>(request) } } },
                CorrelationInitializers =
                {
                    new QueryCorrelationInitializer { CorrelationHandle = correlation, MessageQuerySet = Wf.RequestKey("Submit", "request", true) }
                }
            };

            var disableAndArchive = new Sequence
            {
                Activities =
                {
                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Scheduled },
                    new If(env => !request.Get(env).Immediate) { Then = WaitForLastDay(request, number, correlation) },

                    new GetAccount { SamAccountName = Wf.In(env => request.Get(env).SamAccountName), Result = current },
                    new PlanLeaverAccess { RequestNumber = number, Account = current, Result = plan },
                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Provisioning },
                    Wf.ProvisionWithRetry(env => new DisableAccountCommand
                    {
                        RequestNumber = number.Get(env),
                        Step = "disable",
                        SamAccountName = request.Get(env).SamAccountName,
                        TargetOu = plan.Get(env).TargetOu,
                        RemoveGroups = plan.Get(env).RemoveGroups,
                        Reason = "Leaver " + number.Get(env)
                    }, disabled, settings),

                    new If(env => !disabled.Get(env).Succeeded)
                    {
                        Then = JoinerWorkflow.Failed(number, disabled, settings),
                        Else = new Sequence
                        {
                            Activities =
                            {
                                new Persist(),
                                new RecordStatus { RequestNumber = number, Status = RequestStatus.Disabled },
                                new Notify
                                {
                                    RequestNumber = number,
                                    Template = "leaver-disabled",
                                    Recipients = Wf.In(env => new[] { current.Get(env).ManagerAccount, request.Get(env).HandoverTo, settings.Get(env).HRTeam }),
                                    Values = Wf.In(env => new Dictionary<string, string>
                                    {
                                        { "Subject", subject.Get(env) },
                                        { "HomeDirectory", disabled.Get(env).HomeDirectory ?? "" },
                                        { "RemovedGroups", string.Join(", ", plan.Get(env).RemoveGroups) }
                                    })
                                },

                                new RecordStatus { RequestNumber = number, Status = RequestStatus.AwaitingCleanup },
                                new If(env => !string.IsNullOrEmpty(request.Get(env).HandoverTo))
                                {
                                    Then = HomeFolderHandover(request, number, correlation, settings, subject, handoverTask, handover),
                                    Else = Wf.WaitUntil(number, Milestone.LeaverCleanup, env => request.Get(env).LastWorkingDay, null, "Archive account")
                                },

                                Wf.ProvisionWithRetry(env => new ArchiveAccountCommand
                                {
                                    RequestNumber = number.Get(env),
                                    Step = "archive",
                                    SamAccountName = request.Get(env).SamAccountName,
                                    HandoverTo = request.Get(env).HandoverTo
                                }, archived, settings),
                                new If(env => archived.Get(env).Succeeded)
                                {
                                    Then = new RecordStatus
                                    {
                                        RequestNumber = number,
                                        Status = RequestStatus.Completed,
                                        Detail = Wf.In(env => "Home folder archived to " + archived.Get(env).HomeDirectory)
                                    },
                                    Else = JoinerWorkflow.Failed(number, archived, settings)
                                }
                            }
                        }
                    }
                }
            };

            var main = new Sequence
            {
                DisplayName = "Leaver",
                Activities =
                {
                    Wf.Approval(C, TaskKind.ManagerApproval, number, correlation, env => current.Get(env).ManagerAccount ?? settings.Get(env).HRTeam,
                        settings, subject, outcome, decision),
                    new If(env => outcome.Get(env) != DecisionOutcome.Approved)
                    {
                        Then = Wf.Reject(number, decision, rejected, env => new[] { request.Get(env).RequestedBy, settings.Get(env).HRTeam }),
                        Else = disableAndArchive
                    },
                    Wf.Set(finished, true)
                }
            };

            var body = new Sequence
            {
                Variables =
                {
                    request, number, correlation, settings, subject, current, plan, outcome, decision, rejected, finished,
                    cancelled, cancelledBy, cancelReason, disabled, archived, handoverTask, handover
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
                    new AttachInstance { RequestNumber = number, Kind = RequestKind.Leaver, Subject = subject, Definition = "Leaver " + version },
                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Submitted, Actor = Wf.In(env => request.Get(env).RequestedBy) },
                    new GetAccount { SamAccountName = Wf.In(env => request.Get(env).SamAccountName), Result = current },

                    new If(env => current.Get(env) == null)
                    {
                        Then = new RecordStatus { RequestNumber = number, Status = RequestStatus.Failed, Detail = "Account not found in the directory." },
                        Else = new Sequence
                        {
                            Activities =
                            {
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
                                            new If(env => disabled.Get(env) != null && disabled.Get(env).Succeeded)
                                            {
                                                Then = new Notify
                                                {
                                                    RequestNumber = number,
                                                    Template = "leaver-cancelled-after-disable",
                                                    Recipients = Wf.In(env => new[] { settings.Get(env).ServiceDesk, settings.Get(env).HRTeam }),
                                                    Values = Wf.In(env => new Dictionary<string, string>
                                                    {
                                                        { "Subject", subject.Get(env) },
                                                        { "RemovedGroups", string.Join(", ", plan.Get(env).RemoveGroups) },
                                                        { "PreviousOu", current.Get(env).DistinguishedName }
                                                    })
                                                }
                                            },
                                            new RecordStatus { RequestNumber = number, Status = RequestStatus.Cancelled, Actor = cancelledBy, Detail = cancelReason }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return new WorkflowService
            {
                Name = Wf.Contract(C),
                ConfigurationName = "StaffOnboarding." + C,
                DefinitionIdentity = new WorkflowIdentity("Leaver", version, null),
                Body = body
            };
        }

        /// <summary>Waits for the end of the last working day; HR can bring it forward or move it.</summary>
        private static Activity WaitForLastDay(Variable<LeaverRequest> request, Variable<string> number, Variable<CorrelationHandle> correlation)
        {
            var due = new Variable<DateTime>("due");
            var wait = new Variable<TimeSpan>("wait");
            var reached = new Variable<bool>("reached");
            var msgNumber = new Variable<string>("msgNumber");
            var newDate = new Variable<DateTime>("newDate");
            var by = new Variable<string>("by");
            var reason = new Variable<string>("reason");

            return new Sequence
            {
                DisplayName = "Wait for last working day",
                Variables = { due, wait, reached, msgNumber, newDate, by, reason },
                Activities =
                {
                    new While(env => !reached.Get(env))
                    {
                        Body = new Sequence
                        {
                            Activities =
                            {
                                new GetDueTime { Milestone = Milestone.LeaverDisable, Date = Wf.In(env => request.Get(env).LastWorkingDay), Result = due },
                                new GetDelay { RequestNumber = number, DueLocal = due, Step = "Disable account", Result = wait },
                                new Pick
                                {
                                    Branches =
                                    {
                                        new PickBranch { Trigger = new Delay { Duration = wait }, Action = Wf.Set(reached, true) },
                                        new PickBranch
                                        {
                                            Trigger = Wf.ReceiveCommand(C, "DisableNow", correlation, Wf.RequestKey("DisableNow", "requestNumber", false),
                                                new ReceiveParametersContent
                                                {
                                                    Parameters =
                                                    {
                                                        { "requestNumber", new OutArgument<string>(msgNumber) },
                                                        { "requestedBy", new OutArgument<string>(by) },
                                                        { "reason", new OutArgument<string>(reason) }
                                                    }
                                                }),
                                            Action = new Sequence
                                            {
                                                Activities =
                                                {
                                                    new AddTimeline { RequestNumber = number, Actor = by, Event = "Disable now", Detail = reason },
                                                    Wf.Set(reached, true)
                                                }
                                            }
                                        },
                                        new PickBranch
                                        {
                                            Trigger = Wf.ReceiveCommand(C, "ChangeLastWorkingDay", correlation, Wf.RequestKey("ChangeLastWorkingDay", "requestNumber", false),
                                                new ReceiveParametersContent
                                                {
                                                    Parameters =
                                                    {
                                                        { "requestNumber", new OutArgument<string>(msgNumber) },
                                                        { "lastWorkingDay", new OutArgument<DateTime>(newDate) },
                                                        { "changedBy", new OutArgument<string>(by) }
                                                    }
                                                }),
                                            Action = new ApplyLastWorkingDay { Request = request, LastWorkingDay = newDate, ChangedBy = by }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new ClearSchedule { RequestNumber = number }
                }
            };
        }

        /// <summary>
        /// The handover recipient confirms that they have copied what they need; the account is archived then,
        /// or at the end of the retention period at the latest.
        /// </summary>
        private static Activity HomeFolderHandover(Variable<LeaverRequest> request, Variable<string> number, Variable<CorrelationHandle> correlation,
            Variable<ProcessSettings> settings, Variable<string> subject, Variable<Guid> taskId, Variable<ApprovalDecision> confirmation)
        {
            var due = new Variable<DateTime>("due");
            var wait = new Variable<TimeSpan>("wait");
            var done = new Variable<bool>("done");

            return new Sequence
            {
                DisplayName = "Home folder handover",
                Variables = { due, wait, done },
                Activities =
                {
                    new CreateApprovalTask
                    {
                        RequestNumber = number, Kind = TaskKind.HomeFolderHandover, AssignedTo = Wf.In(env => request.Get(env).HandoverTo),
                        Subject = subject, Settings = settings, Result = taskId
                    },
                    new GetDueTime { Milestone = Milestone.LeaverCleanup, Date = Wf.In(env => request.Get(env).LastWorkingDay), Result = due },
                    new While(env => !done.Get(env))
                    {
                        Body = new Sequence
                        {
                            Activities =
                            {
                                new GetDelay { RequestNumber = number, DueLocal = due, Step = "Archive account", Result = wait },
                                new Pick
                                {
                                    Branches =
                                    {
                                        new PickBranch
                                        {
                                            Trigger = Wf.ReceiveCommand(C, "Decide", correlation, Wf.RequestKey("Decide", "decision", true),
                                                new ReceiveParametersContent { Parameters = { { "decision", new OutArgument<ApprovalDecision>(confirmation) } } }),
                                            Action = new If(env => confirmation.Get(env).TaskId == taskId.Get(env))
                                            {
                                                Then = new Sequence
                                                {
                                                    Activities =
                                                    {
                                                        new CloseApprovalTask { TaskId = taskId, Status = "Done", Decision = confirmation },
                                                        new AddTimeline { RequestNumber = number, Actor = Wf.In(env => confirmation.Get(env).DecidedBy), Event = "Handover confirmed" },
                                                        Wf.Set(done, true)
                                                    }
                                                }
                                            }
                                        },
                                        new PickBranch
                                        {
                                            Trigger = new Delay { Duration = wait },
                                            Action = new Sequence
                                            {
                                                Activities =
                                                {
                                                    new CloseApprovalTask { TaskId = taskId, Status = "Expired" },
                                                    Wf.Set(done, true)
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new ClearSchedule { RequestNumber = number }
                }
            };
        }
    }
}
