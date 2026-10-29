using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Statements;
using System.Linq.Expressions;
using System.ServiceModel;
using System.ServiceModel.Activities;
using System.ServiceModel.Dispatcher;
using System.Xml.Linq;
using StaffOnboarding.Core;
using StaffOnboarding.Core.Provisioning;
using StaffOnboarding.Core.Requests;
using StaffOnboarding.Workflows.Activities;

namespace StaffOnboarding.Workflows.Definitions
{
    /// <summary>Building blocks shared by the joiner, mover and leaver workflow definitions.</summary>
    internal static class Wf
    {
        public const string CorrelationKey = "RequestNumber";

        public static XName Contract(string name)
        {
            return XName.Get(name, OnboardingNamespaces.Service);
        }

        /// <summary>Correlation query on the request number inside a message body.</summary>
        public static MessageQuerySet RequestKey(string operation, string parameter, bool insideDataContract)
        {
            var ns = new XPathMessageContext();
            ns.AddNamespace("svc", OnboardingNamespaces.Service);
            ns.AddNamespace("d", OnboardingNamespaces.Data);
            var path = "sm:body()/svc:" + operation + "/svc:" + parameter + (insideDataContract ? "/d:RequestNumber" : "");
            return new MessageQuerySet { { CorrelationKey, new XPathMessageQuery(path, ns) } };
        }

        public static Assign<T> Set<T>(Variable<T> target, Expression<Func<ActivityContext, T>> value)
        {
            return new Assign<T> { To = new OutArgument<T>(target), Value = new InArgument<T>(value) };
        }

        public static Assign<T> Set<T>(Variable<T> target, T value)
        {
            return new Assign<T> { To = new OutArgument<T>(target), Value = new InArgument<T>(value) };
        }

        public static InArgument<T> In<T>(Expression<Func<ActivityContext, T>> value)
        {
            return new InArgument<T>(value);
        }

        /// <summary>Receive + empty reply for a two-way operation without a return value.</summary>
        public static Sequence ReceiveCommand(string contract, string operation, Variable<CorrelationHandle> correlation,
            MessageQuerySet key, ReceiveParametersContent content)
        {
            var receive = new Receive
            {
                ServiceContractName = Contract(contract),
                OperationName = operation,
                CorrelatesWith = correlation,
                CorrelatesOn = key,
                Content = content
            };
            return new Sequence
            {
                DisplayName = "Receive " + operation,
                Activities = { receive, new SendReply { Request = receive, Content = new SendParametersContent() } }
            };
        }

        /// <summary>Waits until a local due time; the timer is durable.</summary>
        public static Sequence WaitUntil(Variable<string> requestNumber, Milestone milestone,
            Expression<Func<ActivityContext, DateTime>> date, Expression<Func<ActivityContext, int>> days, string step)
        {
            var due = new Variable<DateTime>("due");
            var wait = new Variable<TimeSpan>("wait");
            return new Sequence
            {
                DisplayName = "Wait for " + step,
                Variables = { due, wait },
                Activities =
                {
                    new GetDueTime { Milestone = milestone, Date = In(date), Days = days == null ? new InArgument<int>(0) : In(days), Result = due },
                    new GetDelay { RequestNumber = requestNumber, DueLocal = due, Step = step, Result = wait },
                    new Delay { Duration = wait },
                    new ClearSchedule { RequestNumber = requestNumber }
                }
            };
        }

        /// <summary>
        /// Sends a provisioning command until it succeeds, fails permanently or runs out of attempts.
        /// The delay between attempts is durable, so retries continue after a host restart.
        /// </summary>
        public static Sequence ProvisionWithRetry(Expression<Func<ActivityContext, ProvisioningCommand>> command,
            Variable<ProvisioningResult> result, Variable<ProcessSettings> settings)
        {
            var attempt = new Variable<int>("attempt");
            return new Sequence
            {
                DisplayName = "Provision",
                Variables = { attempt },
                Activities =
                {
                    Set(attempt, 0),
                    Set(result, env => (ProvisioningResult)null),
                    new DoWhile(env => !result.Get(env).Succeeded && result.Get(env).Retryable && attempt.Get(env) < settings.Get(env).MaxAttempts)
                    {
                        Body = new Sequence
                        {
                            Activities =
                            {
                                Set(attempt, env => attempt.Get(env) + 1),
                                new Provision { Command = In(command), Result = result },
                                new If(env => !result.Get(env).Succeeded && result.Get(env).Retryable && attempt.Get(env) < settings.Get(env).MaxAttempts)
                                {
                                    Then = new Delay { Duration = In(env => settings.Get(env).RetryDelay) }
                                }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// One approval task with reminders, escalation to the assignee's manager and expiry.
        /// Sets <paramref name="outcome"/> and <paramref name="decision"/> when done.
        /// </summary>
        public static Sequence Approval(string contract, TaskKind kind, Variable<string> requestNumber,
            Variable<CorrelationHandle> correlation, Expression<Func<ActivityContext, string>> approver,
            Variable<ProcessSettings> settings, Variable<string> subject,
            Variable<DecisionOutcome> outcome, Variable<ApprovalDecision> decision)
        {
            var taskId = new Variable<Guid>("taskId");
            var received = new Variable<ApprovalDecision>("received");
            var reminders = new Variable<int>("reminders");
            var escalations = new Variable<int>("escalations");
            var assignee = new Variable<string>("assignee");
            var next = new Variable<string>("next");
            var done = new Variable<bool>("done");

            var waitingStatus = kind == TaskKind.ITApproval ? RequestStatus.AwaitingIT : RequestStatus.AwaitingManager;

            return new Sequence
            {
                DisplayName = CreateApprovalTask.Describe(kind),
                Variables = { taskId, received, reminders, escalations, assignee, next, done },
                Activities =
                {
                    Set(assignee, approver),
                    new RecordStatus { RequestNumber = requestNumber, Status = waitingStatus, Detail = In(env => "Waiting for " + assignee.Get(env)) },
                    new CreateApprovalTask { RequestNumber = requestNumber, Kind = kind, AssignedTo = assignee, Subject = subject, Settings = settings, Result = taskId },
                    new While(env => !done.Get(env))
                    {
                        Body = new Pick
                        {
                            Branches =
                            {
                                new PickBranch
                                {
                                    DisplayName = "Decision",
                                    Trigger = ReceiveCommand(contract, "Decide", correlation, RequestKey("Decide", "decision", true),
                                        new ReceiveParametersContent { Parameters = { { "decision", new OutArgument<ApprovalDecision>(received) } } }),
                                    Action = new If(env => received.Get(env).TaskId == taskId.Get(env))
                                    {
                                        Then = new Sequence
                                        {
                                            Activities =
                                            {
                                                new CloseApprovalTask { TaskId = taskId, Status = "Done", Decision = received },
                                                new AddTimeline
                                                {
                                                    RequestNumber = requestNumber,
                                                    Actor = In(env => received.Get(env).DecidedBy),
                                                    Event = In(env => CreateApprovalTask.Describe(kind) + ": " + received.Get(env).Outcome),
                                                    Detail = In(env => received.Get(env).Comment)
                                                },
                                                Set(outcome, env => received.Get(env).Outcome),
                                                Set(decision, env => received.Get(env)),
                                                Set(done, true)
                                            }
                                        },
                                        Else = new AddTimeline
                                        {
                                            RequestNumber = requestNumber,
                                            Actor = In(env => received.Get(env).DecidedBy),
                                            Event = "Decision ignored",
                                            Detail = In(env => "Task " + received.Get(env).TaskId + " is no longer open.")
                                        }
                                    }
                                },
                                new PickBranch
                                {
                                    DisplayName = "Reminder",
                                    Trigger = new Delay { Duration = In(env => settings.Get(env).ReminderInterval) },
                                    Action = new If(env => reminders.Get(env) < settings.Get(env).RemindersBeforeEscalation)
                                    {
                                        Then = new Sequence
                                        {
                                            Activities =
                                            {
                                                Set(reminders, env => reminders.Get(env) + 1),
                                                new SendTaskReminder
                                                {
                                                    TaskId = taskId, RequestNumber = requestNumber, AssignedTo = assignee,
                                                    Subject = subject, ReminderNumber = reminders, Settings = settings
                                                }
                                            }
                                        },
                                        Else = new If(env => escalations.Get(env) < settings.Get(env).EscalationsBeforeExpiry)
                                        {
                                            Then = new Sequence
                                            {
                                                Activities =
                                                {
                                                    new ResolveEscalation { Assignee = assignee, Settings = settings, Result = next },
                                                    new CloseApprovalTask { TaskId = taskId, Status = "Escalated" },
                                                    new AddTimeline
                                                    {
                                                        RequestNumber = requestNumber, Event = "Escalated",
                                                        Detail = In(env => assignee.Get(env) + " -> " + next.Get(env))
                                                    },
                                                    Set(assignee, env => next.Get(env)),
                                                    Set(escalations, env => escalations.Get(env) + 1),
                                                    Set(reminders, 0),
                                                    new RecordStatus { RequestNumber = requestNumber, Status = waitingStatus, Detail = In(env => "Waiting for " + assignee.Get(env)) },
                                                    new CreateApprovalTask { RequestNumber = requestNumber, Kind = kind, AssignedTo = assignee, Subject = subject, Settings = settings, Result = taskId }
                                                }
                                            },
                                            Else = new Sequence
                                            {
                                                Activities =
                                                {
                                                    new CloseApprovalTask { TaskId = taskId, Status = "Expired" },
                                                    new AddTimeline { RequestNumber = requestNumber, Event = "Task expired", Detail = In(env => "No decision from " + assignee.Get(env)) },
                                                    Set(outcome, DecisionOutcome.Rejected),
                                                    Set(decision, env => new ApprovalDecision
                                                    {
                                                        RequestNumber = requestNumber.Get(env),
                                                        TaskId = taskId.Get(env),
                                                        Outcome = DecisionOutcome.Rejected,
                                                        Comment = "No decision was made in time.",
                                                        DecidedBy = "workflow"
                                                    }),
                                                    Set(done, true)
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>Receive for Cancel(requestNumber, cancelledBy, reason).</summary>
        public static Sequence ReceiveCancel(string contract, Variable<CorrelationHandle> correlation,
            Variable<string> cancelledBy, Variable<string> reason)
        {
            var number = new Variable<string>("cancelNumber");
            return new Sequence
            {
                Variables = { number },
                Activities =
                {
                    ReceiveCommand(contract, "Cancel", correlation, RequestKey("Cancel", "requestNumber", false),
                        new ReceiveParametersContent
                        {
                            Parameters =
                            {
                                { "requestNumber", new OutArgument<string>(number) },
                                { "cancelledBy", new OutArgument<string>(cancelledBy) },
                                { "reason", new OutArgument<string>(reason) }
                            }
                        })
                }
            };
        }

        public static Sequence Reject(Variable<string> requestNumber, Variable<ApprovalDecision> decision,
            Variable<bool> rejected, Expression<Func<ActivityContext, string[]>> notify)
        {
            return new Sequence
            {
                Activities =
                {
                    new RecordStatus
                    {
                        RequestNumber = requestNumber,
                        Status = RequestStatus.Rejected,
                        Actor = In(env => decision.Get(env).DecidedBy),
                        Detail = In(env => decision.Get(env).Comment)
                    },
                    new Notify
                    {
                        RequestNumber = requestNumber,
                        Template = "request-rejected",
                        Recipients = In(notify),
                        Values = In(env => new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "DecidedBy", decision.Get(env).DecidedBy },
                            { "Comment", decision.Get(env).Comment ?? "" }
                        })
                    },
                    Set(rejected, true)
                }
            };
        }
    }
}
