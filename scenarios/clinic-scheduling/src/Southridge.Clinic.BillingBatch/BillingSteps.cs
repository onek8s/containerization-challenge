using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Data;
using Southridge.Clinic.Data.ReportsDataSetTableAdapters;
using Southridge.Clinic.Data.Repositories;
using Southridge.Clinic.Documents;
using Southridge.Clinic.Domain;

namespace Southridge.Clinic.BillingBatch
{
    /// <summary>The steps of the nightly billing run. Each step can also be run on its own from the command line.</summary>
    public class BillingSteps
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BillingSteps));

        private readonly BatchSettings _settings;
        private readonly BillingRepository _billing;
        private readonly SystemRepository _system;
        private readonly DateTime _runDate;

        public BillingSteps(BatchSettings settings, DateTime runDate)
        {
            _settings = settings;
            _runDate = runDate;
            _billing = new BillingRepository();
            _system = new SystemRepository();
        }

        /// <summary>Creates claims for encounters with released charges; self-pay visits go to the patient balance.</summary>
        public string CreateClaims()
        {
            int lag = _system.GetSetting("Billing.ClaimLagDays", 1);
            int claims = 0, selfPay = 0;
            foreach (ReadyEncounter e in _billing.ReadyEncounters(_runDate.Date.AddDays(1), lag))
            {
                if (e.PatientInsuranceId.HasValue && e.PayerType != (int)PayerType.SelfPay)
                {
                    string number;
                    _billing.CreateClaim(e.EncounterId, e.PatientInsuranceId.Value, out number);
                    Log.InfoFormat("Claim {0} for encounter {1} ({2}, {3}) {4:C}", number, e.EncounterId, e.Mrn, e.PayerCode, e.TotalCharge);
                    claims++;
                }
                else
                {
                    decimal amount = _billing.BillToPatient(e.EncounterId);
                    Log.InfoFormat("Encounter {0} ({1}) is self-pay: {2:C} to patient balance", e.EncounterId, e.Mrn, amount);
                    selfPay++;
                }
            }

            return claims + " claims created, " + selfPay + " self-pay visits";
        }

        /// <summary>Scrubs ready claims, writes one 837P file and sends it to the clearinghouse.</summary>
        public string SendClaims()
        {
            var scrubber = new ClaimScrubber(_runDate);
            var good = new List<ClaimData>();
            int rejected = 0;
            foreach (int claimId in _billing.ClaimsToSend())
            {
                ClaimData claim = _billing.GetClaimData(claimId);
                IList<string> errors = scrubber.Check(claim, _settings.TimelyFilingDays);
                if (errors.Count > 0)
                {
                    _billing.RejectClaim(claimId, string.Join(" ", errors.ToArray()));
                    Log.WarnFormat("Claim {0} held back: {1}", claim.ClaimNumber, string.Join(" ", errors.ToArray()));
                    rejected++;
                    continue;
                }

                good.Add(claim);
            }

            if (good.Count == 0)
            {
                return "no claims to send, " + rejected + " held back";
            }

            int icn;
            int batchId = _billing.CreateBatch("pending", good.Select(c => c.ClaimId), out icn);
            string fileName = "SMG837P_" + icn.ToString("000000000", CultureInfo.InvariantCulture) + ".txt";
            _billing.RenameBatchFile(batchId, fileName);

            string content = new Claim837Writer(_settings.Submitter).Write(good, icn, DateTime.Now);
            string outgoing = Path.Combine(_settings.WorkFolder, "outgoing");
            Directory.CreateDirectory(outgoing);
            string path = Path.Combine(outgoing, fileName);
            File.WriteAllText(path, content, Encoding.ASCII);

            using (IClearinghouseTransport transport = ClearinghouseTransport.Create(_settings))
            {
                transport.Upload(path);
            }

            _billing.MarkBatchSent(batchId);
            Log.InfoFormat("Sent {0} with {1} claims ({2:C})", fileName, good.Count, good.Sum(c => c.TotalCharge));
            return good.Count + " claims sent in " + fileName + ", " + rejected + " held back";
        }

        /// <summary>Picks up 999 acknowledgments and 835 remittances from the clearinghouse.</summary>
        public string ReceiveResponses()
        {
            string inbound = Path.Combine(_settings.WorkFolder, "inbound");
            string processed = Path.Combine(inbound, "processed");
            string failed = Path.Combine(inbound, "failed");
            Directory.CreateDirectory(processed);
            Directory.CreateDirectory(failed);
            int acks = 0, remits = 0, errors = 0;
            using (IClearinghouseTransport transport = ClearinghouseTransport.Create(_settings))
            {
                foreach (string name in transport.ListInbound())
                {
                    string local = Path.Combine(inbound, name);
                    transport.Download(name, local);
                    try
                    {
                        string content = File.ReadAllText(local);
                        string kind = TransactionSet(content);
                        if (kind == "999")
                        {
                            ImplementationAck ack = Ack999Parser.Parse(content);
                            int updated = _billing.AcknowledgeBatch(ack.GroupControlNumber, ack.Status, name);
                            Log.InfoFormat("{0}: 999 for group {1} status {2}, {3} claims updated{4}", name, ack.GroupControlNumber, ack.Status, updated,
                                ack.Errors.Count > 0 ? " (" + string.Join("; ", ack.Errors.ToArray()) + ")" : string.Empty);
                            acks++;
                        }
                        else if (kind == "835")
                        {
                            PostRemittance(name, content);
                            remits++;
                        }
                        else
                        {
                            throw new FormatException("Unexpected transaction set " + kind + ".");
                        }

                        MoveTo(local, processed);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(name + " could not be processed and was moved to " + failed, ex);
                        MoveTo(local, failed);
                        errors++;
                    }

                    transport.RemoveInbound(name);
                }
            }

            return acks + " acknowledgments, " + remits + " remittances, " + errors + " failed files";
        }

        /// <summary>Patient statements as PDF files plus a manifest for the print and mail vendor.</summary>
        public string Statements()
        {
            decimal minimum = _system.GetSetting("Billing.StatementMinimum", 5m);
            string folder = Path.Combine(_settings.StatementFolder, _runDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            var service = new StatementService();
            var manifest = new StringBuilder("PatientId,Mrn,Name,AmountDue,DueDate,DunningLevel,File\r\n");
            int count = 0;
            foreach (StatementCandidate candidate in _billing.StatementCandidates(_runDate, minimum))
            {
                string fileName = "STMT_" + candidate.Mrn + "_" + _runDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".pdf";
                StatementResult result = service.Issue(candidate.PatientId, _runDate, fileName);
                if (result == null)
                {
                    continue;
                }

                File.WriteAllBytes(Path.Combine(folder, fileName), result.Pdf);
                manifest.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},\"{2}\",{3:0.00},{4:yyyy-MM-dd},{5},{6}\r\n",
                    candidate.PatientId, candidate.Mrn, candidate.PatientName.Replace("\"", "'"), result.AmountDue, result.DueDate, result.DunningLevel, fileName);
                count++;
            }

            File.WriteAllText(Path.Combine(folder, "manifest.csv"), manifest.ToString(), Encoding.UTF8);
            return count + " statements in " + folder;
        }

        /// <summary>A/R aging workbook for the practice manager (NPOI).</summary>
        public string AgingReport()
        {
            ReportsDataSet.ArAgingDataTable aging = new ArAgingTableAdapter().GetData(_runDate);
            byte[] workbook = ExcelExports.AgingWorkbook(aging, _runDate);
            Directory.CreateDirectory(_settings.ReportFolder);
            string path = Path.Combine(_settings.ReportFolder, "AR-Aging-" + _runDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".xls");
            File.WriteAllBytes(path, workbook);
            return aging.Count + " payers in " + path;
        }

        private void PostRemittance(string name, string content)
        {
            Remittance remit = Remittance835Parser.Parse(content);
            int remittanceId = _billing.InsertRemittance(name, remit.PayerName, remit.PayerIdentifier, remit.TraceNumber, remit.PaymentMethod,
                remit.PaymentDate, remit.TotalPaid, remit.Claims.Count);
            int unmatched = 0;
            foreach (RemittanceClaim c in remit.Claims)
            {
                bool matched = _billing.PostClaimPayment(remittanceId, c.ClaimNumber, c.StatusCode, c.ChargeAmount, c.PaidAmount,
                    c.PatientResponsibility, c.PayerAdjustments, c.PayerClaimNumber, remit.TraceNumber);
                if (!matched)
                {
                    unmatched++;
                    Log.WarnFormat("{0}: claim {1} in the 835 does not match a clinic claim", name, c.ClaimNumber);
                }
            }

            Log.InfoFormat("{0}: 835 from {1} trace {2}, {3} claims, {4:C} paid, {5} unmatched", name, remit.PayerName, remit.TraceNumber, remit.Claims.Count, remit.TotalPaid, unmatched);
        }

        private static string TransactionSet(string content)
        {
            int st = content.IndexOf("ST*", StringComparison.Ordinal);
            return st < 0 || content.Length < st + 6 ? null : content.Substring(st + 3, 3);
        }

        private static void MoveTo(string file, string folder)
        {
            string target = Path.Combine(folder, Path.GetFileName(file));
            if (File.Exists(target))
            {
                target = Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + "-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + Path.GetExtension(file));
            }

            File.Move(file, target);
        }
    }
}
