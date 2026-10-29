using System;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using Southridge.Clinic.Billing;
using Southridge.Clinic.Data;
using Southridge.Clinic.Domain;
using Southridge.Clinic.Infrastructure;
using Southridge.Clinic.Scheduling;

namespace Southridge.Clinic.StaffWeb.Clinical
{
    /// <summary>Visit documentation, diagnosis coding, charge capture and lab orders for one encounter.</summary>
    public partial class EncounterPage : ClinicPage
    {
        private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

        private int EncounterId
        {
            get { return QueryInt("id") ?? 0; }
        }

        private DiagnosisCodeSet CodeSet
        {
            get { return (DiagnosisCodeSet)(ViewState["CodeSet"] ?? DiagnosisCodeSet.Icd10); }
            set { ViewState["CodeSet"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (!BindAll(true))
                {
                    return;
                }

                CptList.DataSource = StaffCache.ProcedureCodes();
                CptList.DataBind();
                using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
                {
                    LabTestList.DataSource = db.LabTests.Where(t => t.IsActive).OrderBy(t => t.Name).ToList();
                    LabTestList.DataBind();
                }
            }
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            if (IsValid)
            {
                SaveVisit(false);
            }
        }

        protected void SignButton_Click(object sender, EventArgs e)
        {
            if (!IsInAnyRole(ClinicRoles.Provider))
            {
                ShowError("Only providers can sign a visit.");
                return;
            }

            if (IsValid)
            {
                SaveVisit(true);
            }
        }

        protected void AddDiagnosisButton_Click(object sender, EventArgs e)
        {
            string code = DiagnosisBox.Text.Trim().Split(' ')[0].ToUpperInvariant();
            if (!DiagnosisCodes.IsValid(code, CodeSet))
            {
                ShowError(code + " is not a valid " + CodeSetText(CodeSet) + " code for this date of service.");
                return;
            }

            try
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Encounter encounter = LoadEditable(db);
                    if (encounter.EncounterDiagnosis.Any(d => d.Code == code))
                    {
                        throw new ClinicRuleException(code + " is already on this visit.");
                    }

                    if (encounter.EncounterDiagnosis.Count >= 12)
                    {
                        throw new ClinicRuleException("A claim carries at most 12 diagnoses.");
                    }

                    if (!db.DiagnosisCodes.Any(d => d.Code == code && d.CodeSet == (byte)CodeSet))
                    {
                        throw new ClinicRuleException(code + " is not in the code table.");
                    }

                    encounter.EncounterDiagnosis.Add(new EncounterDiagnosis
                    {
                        Sequence = (byte)(encounter.EncounterDiagnosis.Count == 0 ? 1 : encounter.EncounterDiagnosis.Max(d => d.Sequence) + 1),
                        Code = code,
                        CodeSet = (byte)CodeSet
                    });
                    db.SubmitChanges();
                }

                DiagnosisBox.Text = string.Empty;
                BindAll(false);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected void DiagnosisGrid_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            byte sequence = (byte)DiagnosisGrid.DataKeys[e.RowIndex].Value;
            try
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Encounter encounter = LoadEditable(db);
                    string pointer = sequence.ToString(CultureInfo.InvariantCulture);
                    if (encounter.Charges.Any(c => c.Status != (byte)ChargeStatus.Voided && c.DiagnosisPointers.Contains(pointer)))
                    {
                        throw new ClinicRuleException("A charge points at this diagnosis. Void or change the charge first.");
                    }

                    db.EncounterDiagnosis.DeleteOnSubmit(encounter.EncounterDiagnosis.Single(d => d.Sequence == sequence));
                    db.SubmitChanges();
                }

                BindAll(false);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }

            e.Cancel = true;
        }

        protected void AddChargeButton_Click(object sender, EventArgs e)
        {
            if (!IsValid)
            {
                return;
            }

            string modifier = ModifierBox.Text.Trim().ToUpperInvariant();
            if (modifier.Length > 0 && !ProcedureCodes.IsValidModifier(modifier))
            {
                ShowError("Modifier " + modifier + " is not valid.");
                return;
            }

            try
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Encounter encounter = LoadEditable(db);
                    string pointers = PointerBox.Text.Trim();
                    foreach (char p in pointers)
                    {
                        byte sequence = (byte)(p - '0');
                        if (!encounter.EncounterDiagnosis.Any(d => d.Sequence == sequence))
                        {
                            throw new ClinicRuleException("Diagnosis pointer " + p + " does not match a diagnosis on this visit.");
                        }
                    }

                    int units = int.Parse(UnitsBox.Text, CultureInfo.InvariantCulture);
                    decimal fee = new FeeCalculator().ChargeAmount(db, CptList.SelectedValue);
                    encounter.Charges.Add(new Charge
                    {
                        LineNumber = (byte)(encounter.Charges.Count == 0 ? 1 : encounter.Charges.Max(c => c.LineNumber) + 1),
                        CptCode = CptList.SelectedValue,
                        Modifier1 = modifier.Length > 0 ? modifier : null,
                        Units = units,
                        Amount = fee * units,
                        DiagnosisPointers = pointers,
                        Status = (byte)ChargeStatus.Entered,
                        EnteredBy = UserName,
                        EnteredOn = DateTime.Now
                    });
                    db.SubmitChanges();
                }

                CptList.SelectedIndex = 0;
                ModifierBox.Text = string.Empty;
                UnitsBox.Text = "1";
                BindAll(false);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        protected void ChargeGrid_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            int chargeId = (int)ChargeGrid.DataKeys[e.RowIndex].Value;
            try
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Charge charge = db.Charges.Single(c => c.ChargeId == chargeId && c.EncounterId == EncounterId);
                    if (charge.ClaimId != null)
                    {
                        throw new ClinicRuleException("This charge is on a claim. Use a corrected claim from the billing pages.");
                    }

                    charge.Status = (byte)ChargeStatus.Voided;
                    db.SubmitChanges();
                }

                Audit("VoidCharge", "Charge", chargeId.ToString(CultureInfo.InvariantCulture), null, null);
                BindAll(false);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }

            e.Cancel = true;
        }

        protected void ChargeGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.Footer)
            {
                e.Row.Cells[5].Text = "Total";
                e.Row.Cells[6].Text = ((decimal)(ViewState["ChargeTotal"] ?? 0m)).ToString("C", Us);
            }
            else if (e.Row.RowType == DataControlRowType.DataRow && (string)System.Web.UI.DataBinder.Eval(e.Row.DataItem, "StatusText") != ChargeStatus.Entered.ToString())
            {
                e.Row.Cells[8].Controls.Clear();
            }
        }

        /// <summary>Moves entered charges to Ready so the nightly batch puts them on a claim.</summary>
        protected void ReadyButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Encounter encounter = db.Encounters.Single(x => x.EncounterId == EncounterId);
                    var entered = encounter.Charges.Where(c => c.Status == (byte)ChargeStatus.Entered).ToList();
                    if (entered.Count == 0)
                    {
                        throw new ClinicRuleException("There are no new charges on this visit.");
                    }

                    if (encounter.EncounterDiagnosis.Count == 0)
                    {
                        throw new ClinicRuleException("Add at least one diagnosis before releasing charges.");
                    }

                    foreach (Charge charge in entered)
                    {
                        charge.Status = (byte)ChargeStatus.Ready;
                    }

                    encounter.Status = (byte)EncounterStatus.ChargesEntered;
                    db.SubmitChanges();
                }

                new InterfaceNotifier().EncounterCharges(EncounterId);
                Audit("ChargesReady", "Encounter", EncounterId.ToString(CultureInfo.InvariantCulture), null, null);
                BindAll(false);
                ShowMessage("Charges released for billing.");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        /// <summary>Creates the order and queues ORM^O01 for the lab; the interface engine sends it.</summary>
        protected void OrderLabButton_Click(object sender, EventArgs e)
        {
            try
            {
                string orderNumber = null;
                int? labOrderId = null;
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Encounter encounter = db.Encounters.Single(x => x.EncounterId == EncounterId);
                    string info = ClinicalInfoBox.Text.Trim();
                    db.CreateLabOrder(encounter.EncounterId, encounter.PatientId, encounter.ProviderId, LabTestList.SelectedValue,
                        LabPriorityList.SelectedValue[0], info.Length > 0 ? info : null, ref labOrderId, ref orderNumber);
                }

                new InterfaceNotifier().LabOrder(labOrderId.Value);
                ClinicalInfoBox.Text = string.Empty;
                BindAll(false);
                ShowMessage("Lab order " + orderNumber + " created.");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void SaveVisit(bool sign)
        {
            try
            {
                using (ClinicDataContext db = ClinicDataContextFactory.Create())
                {
                    Encounter encounter = LoadEditable(db);
                    if (encounter.SignedOn != null)
                    {
                        throw new ClinicRuleException("This visit was signed by " + encounter.SignedBy + " and can no longer be changed.");
                    }

                    encounter.ChiefComplaint = NullIfEmpty(ComplaintBox.Text);
                    encounter.Notes = NullIfEmpty(NotesBox.Text);
                    encounter.VitalsBp = NullIfEmpty(BpBox.Text);
                    encounter.VitalsPulse = PulseBox.Text.Trim().Length > 0 ? int.Parse(PulseBox.Text, CultureInfo.InvariantCulture) : (int?)null;
                    encounter.VitalsWeightLb = WeightBox.Text.Trim().Length > 0 ? decimal.Parse(WeightBox.Text, CultureInfo.InvariantCulture) : (decimal?)null;
                    encounter.VitalsTempF = TempBox.Text.Trim().Length > 0 ? decimal.Parse(TempBox.Text, CultureInfo.InvariantCulture) : (decimal?)null;
                    if (sign)
                    {
                        encounter.SignedBy = UserName;
                        encounter.SignedOn = DateTime.Now;
                    }

                    db.SubmitChanges();
                }

                Audit(sign ? "SignVisit" : "EditVisit", "Encounter", EncounterId.ToString(CultureInfo.InvariantCulture), null, null);
                BindAll(sign);
                ShowMessage(sign ? "Visit signed." : "Saved.");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private Encounter LoadEditable(ClinicDataContext db)
        {
            Encounter encounter = db.Encounters.SingleOrDefault(x => x.EncounterId == EncounterId);
            if (encounter == null)
            {
                throw new ClinicRuleException("The visit was not found.");
            }

            if (encounter.Status == (byte)EncounterStatus.Voided || encounter.Status == (byte)EncounterStatus.Billed)
            {
                throw new ClinicRuleException("This visit is " + ((EncounterStatus)encounter.Status).ToString().ToLowerInvariant() + ".");
            }

            return encounter;
        }

        private bool BindAll(bool fields)
        {
            using (ClinicDataContext db = ClinicDataContextFactory.CreateReadOnly())
            {
                Encounter encounter = db.Encounters.SingleOrDefault(x => x.EncounterId == EncounterId);
                if (encounter == null)
                {
                    ShowError("The visit was not found.");
                    EncounterUpdate.Visible = false;
                    return false;
                }

                CodeSet = DiagnosisCodes.CodeSetFor(encounter.DateOfService);
                DiagnosisComplete.ContextKey = ((int)CodeSet).ToString(CultureInfo.InvariantCulture);
                CodeSetLabel.Text = CodeSetText(CodeSet);
                DateLabel.Text = encounter.DateOfService.ToString("dddd MM/dd/yyyy", Us);
                PatientLink.Text = encounter.Patient.LastName + ", " + encounter.Patient.FirstName + " (" + encounter.Patient.Mrn + ")";
                PatientLink.NavigateUrl = "~/Patients/Chart.aspx?id=" + encounter.PatientId;
                ProviderLabel.Text = Server.HtmlEncode(ClinicLookups.ProviderName(encounter.Provider));
                SiteLabel.Text = Server.HtmlEncode(encounter.Site.Name);
                StatusLabel.Text = ((EncounterStatus)encounter.Status).ToString();
                SummaryLink.NavigateUrl = "~/Handlers/VisitSummary.ashx?id=" + encounter.EncounterId;

                bool signed = encounter.SignedOn != null;
                bool open = encounter.Status == (byte)EncounterStatus.Open || encounter.Status == (byte)EncounterStatus.ChargesEntered;
                SignedLabel.Text = signed ? "Signed by " + Server.HtmlEncode(encounter.SignedBy) + " " + encounter.SignedOn.Value.ToString("MM/dd/yyyy h:mm tt", Us) : string.Empty;
                SaveButton.Visible = SignButton.Visible = open && !signed;
                SignButton.Visible = SignButton.Visible && User.IsInRole(ClinicRoles.Provider);
                AddDiagnosisPanel.Visible = AddChargePanel.Visible = ReadyButton.Visible = open;
                AddLabPanel.Visible = open && IsInAnyRole(ClinicRoles.Provider, ClinicRoles.Clinical);

                if (fields)
                {
                    ComplaintBox.Text = encounter.ChiefComplaint;
                    NotesBox.Text = encounter.Notes;
                    BpBox.Text = encounter.VitalsBp;
                    PulseBox.Text = encounter.VitalsPulse.HasValue ? encounter.VitalsPulse.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                    WeightBox.Text = encounter.VitalsWeightLb.HasValue ? encounter.VitalsWeightLb.Value.ToString("0.0", CultureInfo.InvariantCulture) : string.Empty;
                    TempBox.Text = encounter.VitalsTempF.HasValue ? encounter.VitalsTempF.Value.ToString("0.0", CultureInfo.InvariantCulture) : string.Empty;
                    ComplaintBox.ReadOnly = NotesBox.ReadOnly = signed || !open;
                }

                byte set = (byte)CodeSet;
                DiagnosisGrid.DataSource = (from d in encounter.EncounterDiagnosis
                                            join c in db.DiagnosisCodes.Where(x => x.CodeSet == set) on d.Code equals c.Code into codes
                                            from c in codes.DefaultIfEmpty()
                                            orderby d.Sequence
                                            select new { d.Sequence, d.Code, Description = c == null ? string.Empty : c.Description }).ToList();
                DiagnosisGrid.Columns[3].Visible = open;
                DiagnosisGrid.DataBind();

                var charges = encounter.Charges.OrderBy(c => c.LineNumber).Select(c => new
                {
                    c.ChargeId,
                    c.LineNumber,
                    c.CptCode,
                    c.ProcedureCode.Description,
                    Modifiers = (c.Modifier1 + " " + c.Modifier2).Trim(),
                    c.Units,
                    c.DiagnosisPointers,
                    c.Amount,
                    StatusText = ((ChargeStatus)c.Status).ToString()
                }).ToList();
                ViewState["ChargeTotal"] = charges.Where(c => c.StatusText != ChargeStatus.Voided.ToString()).Sum(c => c.Amount);
                ChargeGrid.DataSource = charges;
                ChargeGrid.DataBind();

                LabOrderGrid.DataSource = db.LabOrders.Where(o => o.EncounterId == encounter.EncounterId).OrderBy(o => o.OrderedOn).Select(o => new
                {
                    o.OrderNumber,
                    TestName = o.LabTest.Name,
                    Priority = o.Priority == 'S' ? "Stat" : "Routine",
                    o.Status,
                    o.OrderedOn,
                    o.ResultedOn
                }).ToList();
                LabOrderGrid.DataBind();
            }

            return true;
        }

        private static string CodeSetText(DiagnosisCodeSet codeSet)
        {
            return codeSet == DiagnosisCodeSet.Icd10 ? "ICD-10-CM" : "ICD-9-CM";
        }

        private static string NullIfEmpty(string value)
        {
            value = value == null ? null : value.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
