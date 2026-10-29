using System;
using System.Activities;
using System.Activities.Statements;
using System.ServiceModel.Activities;
using StaffOnboarding.Contracts;
using StaffOnboarding.Core.Access;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Activities;

namespace StaffOnboarding.Workflows.Definitions
{
    /// <summary>
    /// Joiner: manager approval, IT approval for privileged access, account created disabled a few
    /// business days before the start date, enabled on the first day.
    /// </summary>
    public static class JoinerWorkflow
    {
        private const string C = ContractNames.Joiner;

        /// <param name="allowStartDateChange">Version 4.1 instances do not accept ChangeStartDate.</param>
        public static WorkflowService Create(Version version, bool allowStartDateChange)
        {
            var request = new Variable<JoinerRequest>("request");
            var number = new Variable<string>("requestNumber");
            var correlation = new Variable<CorrelationHandle>("correlation");
            var settings = new Variable<ProcessSettings>("settings");
            var subject = new Variable<string>("subject");
            var plan = new Variable<AccessPlan>("plan");
            var outcome = new Variable<DecisionOutcome>("outcome");
            var decision = new Variable<ApprovalDecision>("decision");
            var rejected = new Variable<bool>("rejected");
            var finished = new Variable<bool>("finished");
            var cancelled = new Variable<bool>("cancelled");
            var cancelledBy = new Variable<string>("cancelledBy");
            var cancelReason = new Variable<string>("cancelReason");
            var create = new Variable<CreateAccountCommand>("create");
            var account = new Variable<ProvisioningResult>("account");
            var step = new Variable<ProvisioningResult>("step");

            var submit = new Receive
            {
                ServiceContractName = Wf.Contract(C),
                OperationName = "Submit",
                CanCreateInstance = true,
                Content = new ReceiveParametersContent { Parameters = { { "request", new OutArgument<JoinerRequest>(request) } } },
                CorrelationInitializers =
                {
                    new QueryCorrelationInitializer { CorrelationHandle = correlation, MessageQuerySet = Wf.RequestKey("Submit", "request", true) }
                }
            };

            var main = new Sequence
            {
                DisplayName = "Joiner",
                Activities =
                {
                    new PlanJoinerAccess { Request = request, Result = plan },

                    Wf.Approval(C, TaskKind.ManagerApproval, number, correlation, env => request.Get(env).Position.ManagerAccount,
                        settings, subject, outcome, decision),
                    new If(env => outcome.Get(env) != DecisionOutcome.Approved)
                    {
                        Then = Wf.Reject(number, decision, rejected, env => new[] { request.Get(env).RequestedBy, settings.Get(env).HRTeam })
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
                                        request.Get(env).RequestedBy, request.Get(env).Position.ManagerAccount, settings.Get(env).HRTeam
                                    })
                                }
                            }
                        }
                    },

                    new If(env => !rejected.Get(env))
                    {
                        Then = new Sequence
                        {
                            Activities =
                            {
                                new RecordStatus { RequestNumber = number, Status = RequestStatus.Scheduled },
                                WaitForStartDate(Milestone.JoinerProvisioning, "Create account", request, number, correlation, allowStartDateChange),

                                new RecordStatus { RequestNumber = number, Status = RequestStatus.Provisioning },
                                new BuildCreateAccount { Request = request, Plan = plan, Result = create },
                                Wf.ProvisionWithRetry(env => create.Get(env), account, settings),

                                new If(env => !account.Get(env).Succeeded)
                                {
                                    Then = Failed(number, account, settings),
                                    Else = new Sequence
                                    {
                                        Activities =
                                        {
                                            new SaveAccount { RequestNumber = number, Result = account },
                                            new Persist(),
                                            new WriteWelcomePack { Request = request, Account = account, Settings = settings },
                                            new RecordStatus
                                            {
                                                RequestNumber = number,
                                                Status = RequestStatus.WaitingForStartDate,
                                                Detail = Wf.In(env => account.Get(env).SamAccountName + " created in " + account.Get(env).DistinguishedName)
                                            },

                                            WaitForStartDate(Milestone.JoinerEnable, "Enable account", request, number, correlation, allowStartDateChange),

                                            Wf.ProvisionWithRetry(env => new EnableAccountCommand
                                            {
                                                RequestNumber = number.Get(env),
                                                Step = "enable",
                                                SamAccountName = account.Get(env).SamAccountName
                                            }, step, settings),
                                            new If(env => step.Get(env).Succeeded)
                                            {
                                                Then = new RecordStatus { RequestNumber = number, Status = RequestStatus.Completed },
                                                Else = Failed(number, step, settings)
                                            }
                                        }
                                    }
                                }
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
                    request, number, correlation, settings, subject, plan, outcome, decision, rejected, finished,
                    cancelled, cancelledBy, cancelReason, create, account, step
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
                    new RequestSubject { Joiner = request, Result = subject },
                    new AttachInstance { RequestNumber = number, Kind = RequestKind.Joiner, Subject = subject, Definition = "Joiner " + version },
                    new RecordStatus { RequestNumber = number, Status = RequestStatus.Submitted, Actor = Wf.In(env => request.Get(env).RequestedBy) },

                    new Parallel
                    {
                        CompletionCondition = new System.Activities.Expressions.LambdaValue<bool>(env => finished.Get(env) || cancelled.Get(env)),
                        Branches =
                        {
                            main,
                            new Sequence
                            {
                                Activities = { Wf.ReceiveCancel(C, correlation, cancelledBy, cancelReason), Wf.Set(cancelled, true) }
                            }
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
                                new If(env => account.Get(env) != null && account.Get(env).Succeeded)
                                {
                                    Then = Wf.ProvisionWithRetry(env => new DisableAccountCommand
                                    {
                                        RequestNumber = number.Get(env),
                                        Step = "cancel-disable",
                                        SamAccountName = account.Get(env).SamAccountName,
                                        Reason = "Joiner request cancelled: " + cancelReason.Get(env)
                                    }, step, settings)
                                },
                                new RecordStatus { RequestNumber = number, Status = RequestStatus.Cancelled, Actor = cancelledBy, Detail = cancelReason },
                                new Notify
                                {
                                    RequestNumber = number,
                                    Template = "request-cancelled",
                                    Recipients = Wf.In(env => new[] { request.Get(env).Position.ManagerAccount, settings.Get(env).HRTeam, settings.Get(env).ServiceDesk }),
                                    Values = Wf.In(env => new System.Collections.Generic.Dictionary<string, string>
                                    {
                                        { "Subject", subject.Get(env) },
                                        { "CancelledBy", cancelledBy.Get(env) },
                                        { "Reason", cancelReason.Get(env) ?? "" }
                                    })
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
                DefinitionIdentity = new WorkflowIdentity("Joiner", version, null),
                Body = body
            };
        }

        /// <summary>
        /// Waits for a milestone that depends on the start date. HR can move the start date while waiting;
        /// the timer is then recalculated.
        /// </summary>
        private static Activity WaitForStartDate(Milestone milestone, string stepName, Variable<JoinerRequest> request,
            Variable<string> number, Variable<CorrelationHandle> correlation, bool allowChange)
        {
            if (!allowChange)
                return Wf.WaitUntil(number, milestone, env => request.Get(env).StartDate, null, stepName);

            var due = new Variable<DateTime>("due");
            var wait = new Variable<TimeSpan>("wait");
            var reached = new Variable<bool>("reached");
            var changeNumber = new Variable<string>("changeNumber");
            var newDate = new Variable<DateTime>("newDate");
            var changedBy = new Variable<string>("changedBy");

            return new Sequence
            {
                DisplayName = "Wait for " + stepName,
                Variables = { due, wait, reached, changeNumber, newDate, changedBy },
                Activities =
                {
                    new While(env => !reached.Get(env))
                    {
                        Body = new Sequence
                        {
                            Activities =
                            {
                                new GetDueTime { Milestone = milestone, Date = Wf.In(env => request.Get(env).StartDate), Result = due },
                                new GetDelay { RequestNumber = number, DueLocal = due, Step = stepName, Result = wait },
                                new Pick
                                {
                                    Branches =
                                    {
                                        new PickBranch { Trigger = new Delay { Duration = wait }, Action = Wf.Set(reached, true) },
                                        new PickBranch
                                        {
                                            Trigger = Wf.ReceiveCommand(C, "ChangeStartDate", correlation, Wf.RequestKey("ChangeStartDate", "requestNumber", false),
                                                new ReceiveParametersContent
                                                {
                                                    Parameters =
                                                    {
                                                        { "requestNumber", new OutArgument<string>(changeNumber) },
                                                        { "startDate", new OutArgument<DateTime>(newDate) },
                                                        { "changedBy", new OutArgument<string>(changedBy) }
                                                    }
                                                }),
                                            Action = new ApplyStartDate { Request = request, StartDate = newDate, ChangedBy = changedBy }
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

        internal static Sequence Failed(Variable<string> number, Variable<ProvisioningResult> result, Variable<ProcessSettings> settings)
        {
            return new Sequence
            {
                Activities =
                {
                    new RecordStatus
                    {
                        RequestNumber = number,
                        Status = RequestStatus.Failed,
                        Detail = Wf.In(env => string.Join(" ", result.Get(env).Messages))
                    },
                    new Notify
                    {
                        RequestNumber = number,
                        Template = "provisioning-failed",
                        Recipients = Wf.In(env => new[] { settings.Get(env).ServiceDesk }),
                        Values = Wf.In(env => new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "Messages", string.Join(Environment.NewLine, result.Get(env).Messages) },
                            { "RequestUrl", settings.Get(env).RequestUrl(number.Get(env)) }
                        })
                    }
                }
            };
        }
    }
}
