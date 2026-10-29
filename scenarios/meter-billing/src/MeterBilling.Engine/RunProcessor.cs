using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Coho.Billing.Core;
using Coho.Billing.Core.Output;
using Coho.Billing.Core.Rating;
using Coho.Billing.Core.Reads;
using Coho.Billing.Core.Runs;
using Coho.Billing.Data;

namespace Coho.Billing.Engine
{
    public class RunBusyException : Exception
    {
        public RunBusyException()
            : base("The engine is working on bill runs, try again in a moment")
        {
        }
    }

    public class RunStateException : Exception
    {
        public RunStateException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Opens, rates and releases bill runs. Operator actions from the control endpoint go through the same lock as the work loop.</summary>
    public class RunProcessor
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly EngineSettings _settings;
        private readonly BillingDb _db;
        private readonly ReadValidator _validator;
        private readonly ConsumptionEstimator _estimator = new ConsumptionEstimator();
        private readonly BillCalculator _calculator = new BillCalculator();
        private readonly object _runLock = new object();

        public RunProcessor(EngineSettings settings, BillingDb db)
        {
            _settings = settings;
            _db = db;
            _validator = new ReadValidator(settings.ReadChecks);
        }

        public void OpenDueRuns(DateTime today)
        {
            foreach (var cycle in _db.GetDueCycles(today))
            {
                var billDate = (cycle.NextCutOff > today ? cycle.NextCutOff : today).AddDays(_settings.BillDateOffsetDays);
                var runId = _db.CreateRun(cycle, billDate);
                _db.AddEvent(runId, "Info", null, string.Format(Invariant, "Run opened for cycle {0}, cut-off {1:yyyy-MM-dd}, bill date {2:yyyy-MM-dd}", cycle.CycleCode, cycle.NextCutOff, billDate));
                EngineLog.Event(EventLogEntryType.Information, 1101, string.Format(Invariant, "Bill run {0} opened for cycle {1}", runId, cycle.CycleCode));
            }
        }

        public void ProcessRuns(Func<bool> stopping)
        {
            lock (_runLock)
            {
                foreach (var run in _db.GetRunsInStage(BillRunStage.Created, BillRunStage.Rating, BillRunStage.Review, BillRunStage.Approved, BillRunStage.Releasing))
                {
                    if (stopping())
                    {
                        return;
                    }

                    switch (run.Stage)
                    {
                        case BillRunStage.Created:
                        case BillRunStage.Rating:
                            Rate(run);
                            break;

                        case BillRunStage.Review:
                            CheckReview(run.RunId, null);
                            break;

                        case BillRunStage.Approved:
                            Release(run);
                            break;

                        case BillRunStage.Releasing:
                            // Interrupted during release (service stopped or server restarted); the output files
                            // are written again under the same names.
                            EngineLog.Warning("Run {0} was interrupted during release, releasing again", run.RunId);
                            Release(run);
                            break;
                    }
                }
            }
        }

        private void Rate(BillRunSummary run)
        {
            _db.SetStage(run.RunId, BillRunStage.Rating, null);
            _db.ClearDrafts(run.RunId);

            var cycle = _db.GetCycle(run.CycleCode);
            var tariffs = _db.GetTariffs();
            var resolutions = _db.GetResolutions(run.RunId);
            var accounts = _db.GetAccounts(run.CycleCode);
            var sequence = 1;
            var failures = 0;
            var watch = Stopwatch.StartNew();

            foreach (var account in accounts)
            {
                try
                {
                    if (RateAccount(run, cycle, account, tariffs, resolutions, sequence))
                    {
                        sequence++;
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    _db.AddEvent(run.RunId, "Error", null, "Account " + account.AccountNumber + ": " + ex.Message);
                    EngineLog.Error(ex, "Rating account {0} in run {1} failed", account.AccountNumber, run.RunId);
                }
            }

            if (failures > 0)
            {
                _db.SetStage(run.RunId, BillRunStage.Failed, string.Format(Invariant, "{0} accounts could not be rated", failures));
                EngineLog.Event(EventLogEntryType.Error, 3101, string.Format(Invariant, "Bill run {0} failed: {1} accounts could not be rated", run.RunId, failures));
                return;
            }

            _db.SetStage(run.RunId, BillRunStage.Review, string.Format(Invariant, "{0} bills rated in {1:0.0} s", sequence - 1, watch.Elapsed.TotalSeconds));
            CheckReview(run.RunId, null);
        }

        private bool RateAccount(BillRunSummary run, BillCycle cycle, BillingAccount account, List<Tariff> tariffs,
            Dictionary<string, ExceptionResolution> resolutions, int sequence)
        {
            var opening = account.LastBilledRead as StoredRead;
            if (opening == null)
            {
                _db.AddEvent(run.RunId, "Warning", null, "Account " + account.AccountNumber + " has no opening read and is not billed");
                return false;
            }

            ExceptionResolution resolution;
            resolutions.TryGetValue(account.AccountNumber, out resolution);

            var read = _db.FindNewRead(account.Meter.Serial, opening.ReadDate, run.CutOffDate);
            var useEstimate = read == null
                || read.ReadDate < run.CutOffDate.AddDays(-cycle.ReadWindowDays)
                || resolution == ExceptionResolution.UseEstimate;

            MeterRead closing = useEstimate ? _estimator.Estimate(account, opening, run.CutOffDate) : read;
            var check = _validator.Check(account, opening, closing, DateTime.Today);

            var exceptions = new List<BillException>();
            if (!useEstimate && resolution == ExceptionResolution.Open)
            {
                var expected = account.AverageDailyUse * check.Days;
                exceptions.AddRange(check.Exceptions.Select(code => new BillException { Code = code, Consumption = check.Consumption, Expected = expected }));
            }

            var period = new BillingPeriod
            {
                From = opening.ReadDate,
                To = closing.ReadDate,
                OpeningReading = opening.Reading,
                ClosingReading = closing.Reading,
                Consumption = check.Consumption,
                Estimated = useEstimate
            };

            var bill = _calculator.Calculate(account, period, tariffs, run.BillDate, cycle.PaymentDays);
            bill.BillNumber = BillNumbers.Create(run.BillDate, run.RunId, sequence);
            bill.PaymentReference = BillNumbers.PaymentReference(account.AccountNumber, bill.BillNumber);

            var status = resolution == ExceptionResolution.Exclude ? BillStatus.Excluded : BillStatus.Draft;
            _db.SaveDraft(run.RunId, bill, opening.ReadId, closing, useEstimate ? (int?)null : read.ReadId, status, exceptions);
            return true;
        }

        private void CheckReview(int runId, string approvedBy)
        {
            var run = _db.GetRun(runId);
            var next = BillRunRules.AfterReview(run, _settings.AutoReleaseLimit, approvedBy != null);
            if (next != run.Stage)
            {
                _db.SetStage(runId, next, approvedBy == null ? "No open exceptions" : null, approvedBy);
            }
        }

        private void Release(BillRunSummary run)
        {
            _db.SetStage(run.RunId, BillRunStage.Releasing, null);
            try
            {
                var bills = _db.GetBills(run.RunId, BillStatus.Draft);
                var accounts = bills.Select(b => b.AccountNumber).Distinct().ToDictionary(a => a, a => _db.GetAccount(a));

                var stamp = DateTime.Now;
                var printName = string.Format(Invariant, "CW_{0}_{1:D6}_{2:yyyyMMdd}.prt", run.CycleCode, run.RunId, run.BillDate);
                var printPath = OutputFiles.Write(_settings.PrintOutput, printName, stream =>
                    new PrintFileWriter().Write("RUN" + run.RunId.ToString("D6", Invariant), stamp,
                        bills.Select(b => new PrintItem { Bill = b, Account = accounts[b.AccountNumber] }), stream));

                string debitPath = null;
                var debits = bills
                    .Where(b => b.TotalAmount > 0)
                    .Select(b => new { Bill = b, Account = accounts[b.AccountNumber] })
                    .Where(x => x.Account.PaymentMethod == PaymentMethod.DirectDebit)
                    .ToList();

                var withoutMandate = debits.Where(x => !Iban.IsValid(x.Account.Iban) || string.IsNullOrEmpty(x.Account.MandateId) || !x.Account.MandateSignedOn.HasValue).ToList();
                foreach (var item in withoutMandate)
                {
                    _db.AddEvent(run.RunId, "Warning", null, "Account " + item.Account.AccountNumber + " has no valid mandate or IBAN, bill " + item.Bill.BillNumber + " is sent as an invoice");
                }

                debits = debits.Except(withoutMandate).ToList();
                if (debits.Count > 0)
                {
                    var batch = new DirectDebitBatch
                    {
                        MessageId = "CWDD" + stamp.ToString("yyyyMMdd", Invariant) + run.RunId.ToString("D6", Invariant),
                        CreatedAt = stamp,
                        CollectionDate = debits.Max(x => x.Bill.DueDate)
                    };

                    batch.Items.AddRange(debits.Select(x => new DirectDebitItem
                    {
                        EndToEndId = x.Bill.BillNumber,
                        Amount = x.Bill.TotalAmount,
                        MandateId = x.Account.MandateId,
                        MandateSignedOn = x.Account.MandateSignedOn.Value,
                        FirstCollection = x.Account.FirstCollection,
                        DebtorName = x.Account.CustomerName,
                        DebtorIban = x.Account.Iban,
                        CreditorReference = x.Bill.PaymentReference
                    }));

                    debitPath = OutputFiles.Write(_settings.BankOutput, batch.MessageId + ".xml", stream => new SepaDirectDebitWriter(_settings.Creditor).Write(batch, stream));
                }

                _db.CompleteRun(run.RunId, printPath, debitPath);
                EngineLog.Event(EventLogEntryType.Information, 1102, string.Format(Invariant,
                    "Bill run {0} released: {1} bills, {2} direct debits, print file {3}", run.RunId, bills.Count, debits.Count, printPath));
            }
            catch (Exception ex)
            {
                _db.SetStage(run.RunId, BillRunStage.Failed, "Release failed: " + ex.Message);
                EngineLog.Error(ex, "Releasing run {0} failed", run.RunId);
            }
        }

        public void ProcessReprints()
        {
            var requests = _db.GetPendingReprints();
            if (requests.Count == 0)
            {
                return;
            }

            var items = new List<PrintItem>();
            var done = new List<int>();
            foreach (var request in requests)
            {
                var bill = _db.GetBill(request.BillNumber);
                if (bill == null)
                {
                    EngineLog.Warning("Reprint {0}: bill {1} not found", request.RequestId, request.BillNumber);
                    done.Add(request.RequestId);
                    continue;
                }

                items.Add(new PrintItem { Bill = bill, Account = _db.GetAccount(bill.AccountNumber) });
                done.Add(request.RequestId);
            }

            string path = null;
            if (items.Count > 0)
            {
                var stamp = DateTime.Now;
                path = OutputFiles.Write(System.IO.Path.Combine(_settings.PrintOutput, "Reprint"), "CW_REPRINT_" + stamp.ToString("yyyyMMddHHmmss", Invariant) + ".prt",
                    stream => new PrintFileWriter().Write("REPRINT", stamp, items, stream));
                EngineLog.Info("Wrote {0} reprints to {1}", items.Count, path);
            }

            _db.MarkReprinted(done, path);
        }

        // ---- operator actions ----

        public void Hold(int runId, string user)
        {
            WithRun(runId, run =>
            {
                if (!BillRunRules.CanHold(run.Stage)) throw new RunStateException("Run " + runId + " cannot be held in stage " + run.Stage);
                _db.SetStage(runId, BillRunStage.Held, "Held from stage " + run.Stage, user);
            });
        }

        public void Resume(int runId, string user)
        {
            WithRun(runId, run =>
            {
                if (!BillRunRules.CanResume(run.Stage)) throw new RunStateException("Run " + runId + " is not held or failed");
                var next = run.Stage == BillRunStage.Held && run.Bills > 0 ? BillRunStage.Review : BillRunStage.Created;
                _db.SetStage(runId, next, "Resumed", user);
            });
        }

        public void Rerate(int runId, string user)
        {
            WithRun(runId, run =>
            {
                if (!BillRunRules.CanRerate(run.Stage)) throw new RunStateException("Run " + runId + " cannot be rated again in stage " + run.Stage);
                _db.SetStage(runId, BillRunStage.Created, "Rate again requested", user);
            });
        }

        public void Approve(int runId, string user)
        {
            WithRun(runId, run =>
            {
                if (run.Stage != BillRunStage.Review) throw new RunStateException("Run " + runId + " is not in review");
                if (run.OpenExceptions > 0) throw new RunStateException(string.Format(Invariant, "Run {0} has {1} open exceptions", runId, run.OpenExceptions));
                CheckReview(runId, user);
            });
        }

        public void Cancel(int runId, string user)
        {
            WithRun(runId, run =>
            {
                if (!BillRunRules.CanCancel(run.Stage)) throw new RunStateException("Run " + runId + " cannot be cancelled in stage " + run.Stage);
                _db.ClearDrafts(runId);
                _db.SetStage(runId, BillRunStage.Cancelled, "Cancelled", user);
            });
        }

        public BillException Resolve(int exceptionId, ExceptionResolution resolution, string user, string note)
        {
            if (resolution == ExceptionResolution.Open) throw new RunStateException("Choose a resolution");

            BillException resolved = null;
            Locked(() =>
            {
                resolved = _db.ResolveException(exceptionId, resolution, user, note);
                if (resolved == null)
                {
                    throw new RunStateException("Exception " + exceptionId + " is already resolved or does not exist");
                }

                _db.AddEvent(resolved.RunId, "Info", user, string.Format(Invariant, "Account {0} {1}: {2}{3}", resolved.AccountNumber, resolved.Code, resolution,
                    string.IsNullOrEmpty(note) ? string.Empty : " (" + note + ")"));

                if (resolution != ExceptionResolution.AcceptRead)
                {
                    // Estimate or exclude: rate the run again, earlier resolutions are kept.
                    _db.SetStage(resolved.RunId, BillRunStage.Created, "Rate again after exception resolution", user);
                }
            });

            return resolved;
        }

        private void WithRun(int runId, Action<BillRunSummary> action)
        {
            Locked(() =>
            {
                var run = _db.GetRun(runId);
                if (run == null)
                {
                    throw new RunStateException("Run " + runId + " does not exist");
                }

                action(run);
            });
        }

        private void Locked(Action action)
        {
            if (!Monitor.TryEnter(_runLock, TimeSpan.FromSeconds(10)))
            {
                throw new RunBusyException();
            }

            try
            {
                action();
            }
            finally
            {
                Monitor.Exit(_runLock);
            }
        }
    }
}
