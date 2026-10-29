using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coho.Billing.Core.Runs;
using Coho.Billing.Data;

namespace Coho.Billing.Monitor
{
    public partial class MainForm : Form
    {
        private readonly BillingDb _db;
        private readonly EngineClient _engine;
        private readonly string _logShare;
        private long _logLength;
        private string _logFile;
        private bool _refreshing;

        public MainForm()
        {
            InitializeComponent();

            _db = BillingDb.FromConfig();
            _engine = new EngineClient(ConfigurationManager.AppSettings["EngineUrl"]);
            _logShare = ConfigurationManager.AppSettings["EngineLogShare"];

            int seconds;
            if (int.TryParse(ConfigurationManager.AppSettings["RefreshSeconds"], out seconds) && seconds >= 5)
            {
                refreshTimer.Interval = seconds * 1000;
            }

            Text = "Billing Monitor - " + Environment.UserDomainName + "\\" + Environment.UserName;
        }

        private int? SelectedRunId
        {
            get
            {
                var row = runsGrid.CurrentRow;
                return row == null ? (int?)null : ((BillRunSummary)row.DataBoundItem).RunId;
            }
        }

        private BillRunSummary SelectedRun
        {
            get
            {
                var row = runsGrid.CurrentRow;
                return row == null ? null : (BillRunSummary)row.DataBoundItem;
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            logDatePicker.Value = DateTime.Today;
            await RefreshAllAsync();
            refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await RefreshAllAsync();
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            await RefreshAllAsync();
        }

        private async void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                await RefreshAllAsync();
            }
        }

        private async Task RefreshAllAsync()
        {
            if (_refreshing)
            {
                return;
            }

            _refreshing = true;
            try
            {
                await RefreshStatusAsync();
                RefreshRuns();

                if (tabControl.SelectedTab == exceptionsTab) RefreshExceptions();
                else if (tabControl.SelectedTab == filesTab) RefreshFiles();
                else if (tabControl.SelectedTab == logTab) RefreshLog();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private async Task RefreshStatusAsync()
        {
            try
            {
                var status = await _engine.GetStatusAsync();
                engineLabel.Text = string.Format(CultureInfo.CurrentCulture, "Engine {0} on {1}: {2}", status.Version, status.Machine,
                    status.Paused ? "PAUSED by " + status.PausedBy : status.Busy ? "working" : "idle");
                engineLabel.ForeColor = status.Paused ? Color.DarkOrange : SystemColors.ControlText;
                workLabel.Text = status.LastError != null ? "Last error: " + status.LastError : status.CurrentWork ?? string.Empty;
                workLabel.ForeColor = status.LastError != null ? Color.Firebrick : SystemColors.ControlText;
                lastCycleLabel.Text = status.LastCycleAt.HasValue ? "Last pass " + status.LastCycleAt.Value.ToString("g", CultureInfo.CurrentCulture) : string.Empty;
                pauseItem.Enabled = !status.Paused;
                continueItem.Enabled = status.Paused;
            }
            catch (Exception ex)
            {
                engineLabel.Text = "Engine not reachable";
                engineLabel.ForeColor = Color.Firebrick;
                workLabel.Text = ex is EngineCallException ? ex.Message : ex.GetBaseException().Message;
            }
        }

        private void RefreshRuns()
        {
            var selected = SelectedRunId;
            List<BillRunSummary> runs;
            try
            {
                runs = _db.GetRuns(90);
            }
            catch (Exception ex)
            {
                workLabel.Text = "Database: " + ex.Message;
                return;
            }

            runsGrid.DataSource = runs;
            foreach (DataGridViewColumn column in runsGrid.Columns)
            {
                if (column.DataPropertyName == "TotalAmount") column.DefaultCellStyle.Format = "N2";
                if (column.DataPropertyName == "CutOffDate" || column.DataPropertyName == "BillDate") column.DefaultCellStyle.Format = "d";
            }

            if (selected.HasValue)
            {
                foreach (DataGridViewRow row in runsGrid.Rows)
                {
                    if (((BillRunSummary)row.DataBoundItem).RunId == selected.Value)
                    {
                        runsGrid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            UpdateRunButtons();
        }

        private void RunsGrid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateRunButtons();
            RefreshEvents();
            if (tabControl.SelectedTab == exceptionsTab)
            {
                RefreshExceptions();
            }
        }

        private void RunsGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var run = runsGrid.Rows[e.RowIndex].DataBoundItem as BillRunSummary;
            if (run == null) return;

            if (run.Stage == BillRunStage.Failed) e.CellStyle.BackColor = Color.MistyRose;
            else if (run.Stage == BillRunStage.Held) e.CellStyle.BackColor = Color.LightYellow;
            else if (run.Stage == BillRunStage.Review && run.OpenExceptions > 0) e.CellStyle.BackColor = Color.AntiqueWhite;
        }

        private void UpdateRunButtons()
        {
            var run = SelectedRun;
            holdButton.Enabled = run != null && BillRunRules.CanHold(run.Stage);
            resumeButton.Enabled = run != null && BillRunRules.CanResume(run.Stage);
            rerateButton.Enabled = run != null && BillRunRules.CanRerate(run.Stage);
            approveButton.Enabled = run != null && run.Stage == BillRunStage.Review && run.OpenExceptions == 0;
            cancelRunButton.Enabled = run != null && BillRunRules.CanCancel(run.Stage);
        }

        private void RefreshEvents()
        {
            eventsList.BeginUpdate();
            try
            {
                eventsList.Items.Clear();
                var runId = SelectedRunId;
                if (!runId.HasValue) return;

                foreach (var item in _db.GetRunEvents(runId.Value))
                {
                    var row = new ListViewItem(item.At.ToString("g", CultureInfo.CurrentCulture));
                    row.SubItems.Add(item.Level);
                    row.SubItems.Add(item.UserName ?? string.Empty);
                    row.SubItems.Add(item.Message);
                    if (item.Level == "Error") row.ForeColor = Color.Firebrick;
                    else if (item.Level == "Warning") row.ForeColor = Color.DarkOrange;
                    eventsList.Items.Add(row);
                }
            }
            finally
            {
                eventsList.EndUpdate();
            }
        }

        private async void RunCommand_Click(object sender, EventArgs e)
        {
            var run = SelectedRun;
            var action = (string)((ToolStripItem)sender).Tag;
            if (run == null) return;

            if (action == "cancel" || action == "approve")
            {
                var question = action == "cancel"
                    ? string.Format(CultureInfo.CurrentCulture, "Cancel run {0} for cycle {1}? Draft bills are deleted.", run.RunId, run.CycleCode)
                    : string.Format(CultureInfo.CurrentCulture, "Release run {0}: {1} bills, total {2:N2} EUR? Print and direct debit files are sent.", run.RunId, run.Bills, run.TotalAmount);
                if (MessageBox.Show(this, question, "Billing Monitor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }

            await CallEngineAsync(() => _engine.RunCommandAsync(run.RunId, action));
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab == exceptionsTab) RefreshExceptions();
            else if (tabControl.SelectedTab == filesTab) RefreshFiles();
            else if (tabControl.SelectedTab == logTab) RefreshLog();
        }

        private void RefreshExceptions()
        {
            var runId = SelectedRunId;
            exceptionsTab.Text = runId.HasValue ? "Exceptions (run " + runId.Value + ")" : "Exceptions";
            exceptionsGrid.DataSource = runId.HasValue ? _db.GetExceptions(runId.Value, !showResolvedCheck.Checked) : new List<BillException>();
            foreach (DataGridViewColumn column in exceptionsGrid.Columns)
            {
                if (column.DataPropertyName == "RunId" || column.DataPropertyName == "ReadId") column.Visible = false;
                if (column.DataPropertyName == "Consumption" || column.DataPropertyName == "Expected") column.DefaultCellStyle.Format = "N1";
            }
        }

        private void ShowResolvedCheck_CheckedChanged(object sender, EventArgs e)
        {
            RefreshExceptions();
        }

        private async void ResolveButton_Click(object sender, EventArgs e)
        {
            var resolution = (ExceptionResolution)((Button)sender).Tag;
            var selected = exceptionsGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => (BillException)r.DataBoundItem)
                .Where(x => x.Resolution == ExceptionResolution.Open)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select one or more open exceptions.", "Billing Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (resolution != ExceptionResolution.AcceptRead && string.IsNullOrWhiteSpace(noteBox.Text))
            {
                MessageBox.Show(this, "Enter a note for the customer record.", "Billing Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                noteBox.Focus();
                return;
            }

            foreach (var item in selected)
            {
                await CallEngineAsync(() => _engine.ResolveAsync(item.ExceptionId, resolution, noteBox.Text.Trim()), false);
            }

            noteBox.Clear();
            await RefreshAllAsync();
        }

        private void RefreshFiles()
        {
            filesGrid.DataSource = _db.GetImportFiles(30).Select(f => new
            {
                f.ReceivedAt,
                f.FileName,
                Source = f.Source == 1 ? "AMR" : "Handheld",
                f.BatchId,
                Reads = f.ReadCount,
                NotUsed = f.RejectedCount,
                Status = f.Status == 0 ? "Imported" : f.Status == 1 ? "Rejected" : "Duplicate",
                f.Message
            }).ToList();
        }

        private void LogDatePicker_ValueChanged(object sender, EventArgs e)
        {
            _logFile = null;
            RefreshLog();
        }

        /// <summary>Shows the engine's daily log file from the log share; follows new lines while the tab is open.</summary>
        private void RefreshLog()
        {
            var path = Path.Combine(_logShare, "engine-" + logDatePicker.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
            try
            {
                if (!File.Exists(path))
                {
                    logBox.Text = "No log file " + path;
                    _logFile = null;
                    return;
                }

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (_logFile != path || stream.Length < _logLength)
                    {
                        _logFile = path;
                        _logLength = Math.Max(0, stream.Length - 512 * 1024);
                        logBox.Clear();
                    }

                    stream.Seek(_logLength, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        var text = reader.ReadToEnd();
                        _logLength = stream.Length;
                        if (text.Length > 0)
                        {
                            logBox.AppendText(text.Replace("\n", Environment.NewLine).Replace("\r\r", "\r"));
                        }
                    }
                }

                if (logFollowCheck.Checked)
                {
                    logBox.SelectionStart = logBox.TextLength;
                    logBox.ScrollToCaret();
                }
            }
            catch (IOException ex)
            {
                logBox.Text = ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                logBox.Text = ex.Message;
            }
        }

        private async void PauseItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Pause the billing engine? Read files are not imported and runs are not processed until it is continued.",
                    "Billing Monitor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                await CallEngineAsync(() => _engine.PauseAsync());
            }
        }

        private async void ContinueItem_Click(object sender, EventArgs e)
        {
            await CallEngineAsync(() => _engine.ContinueAsync());
        }

        private async void ScanItem_Click(object sender, EventArgs e)
        {
            await CallEngineAsync(() => _engine.ScanAsync());
        }

        private void ExitItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void AboutItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this, "Billing Monitor " + Assembly.GetExecutingAssembly().GetName().Version + Environment.NewLine + "Coho Water - Meter Billing",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task CallEngineAsync(Func<Task> call, bool refresh = true)
        {
            UseWaitCursor = true;
            try
            {
                await call();
            }
            catch (EngineCallException ex)
            {
                MessageBox.Show(this, ex.Message, "Billing engine (" + (int)ex.Status + ")", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The billing engine did not answer: " + ex.GetBaseException().Message, "Billing Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }

            if (refresh)
            {
                await RefreshAllAsync();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer.Stop();
            _engine.Dispose();
            base.OnFormClosed(e);
        }
    }
}
