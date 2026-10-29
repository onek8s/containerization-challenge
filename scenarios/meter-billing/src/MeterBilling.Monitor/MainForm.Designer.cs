namespace Coho.Billing.Monitor
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.exitItem = new System.Windows.Forms.ToolStripMenuItem();
            this.engineMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.pauseItem = new System.Windows.Forms.ToolStripMenuItem();
            this.continueItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scanItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.refreshButton = new System.Windows.Forms.ToolStripButton();
            this.toolSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.holdButton = new System.Windows.Forms.ToolStripButton();
            this.resumeButton = new System.Windows.Forms.ToolStripButton();
            this.rerateButton = new System.Windows.Forms.ToolStripButton();
            this.approveButton = new System.Windows.Forms.ToolStripButton();
            this.cancelRunButton = new System.Windows.Forms.ToolStripButton();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.runsTab = new System.Windows.Forms.TabPage();
            this.runsSplit = new System.Windows.Forms.SplitContainer();
            this.runsGrid = new System.Windows.Forms.DataGridView();
            this.eventsList = new System.Windows.Forms.ListView();
            this.eventTimeColumn = new System.Windows.Forms.ColumnHeader();
            this.eventLevelColumn = new System.Windows.Forms.ColumnHeader();
            this.eventUserColumn = new System.Windows.Forms.ColumnHeader();
            this.eventMessageColumn = new System.Windows.Forms.ColumnHeader();
            this.exceptionsTab = new System.Windows.Forms.TabPage();
            this.exceptionsGrid = new System.Windows.Forms.DataGridView();
            this.exceptionsPanel = new System.Windows.Forms.Panel();
            this.showResolvedCheck = new System.Windows.Forms.CheckBox();
            this.excludeButton = new System.Windows.Forms.Button();
            this.estimateButton = new System.Windows.Forms.Button();
            this.acceptButton = new System.Windows.Forms.Button();
            this.noteBox = new System.Windows.Forms.TextBox();
            this.noteLabel = new System.Windows.Forms.Label();
            this.filesTab = new System.Windows.Forms.TabPage();
            this.filesGrid = new System.Windows.Forms.DataGridView();
            this.logTab = new System.Windows.Forms.TabPage();
            this.logBox = new System.Windows.Forms.TextBox();
            this.logPanel = new System.Windows.Forms.Panel();
            this.logFollowCheck = new System.Windows.Forms.CheckBox();
            this.logDatePicker = new System.Windows.Forms.DateTimePicker();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.engineLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.workLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.lastCycleLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.refreshTimer = new System.Windows.Forms.Timer(this.components);
            this.menuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.runsTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.runsSplit)).BeginInit();
            this.runsSplit.Panel1.SuspendLayout();
            this.runsSplit.Panel2.SuspendLayout();
            this.runsSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.runsGrid)).BeginInit();
            this.exceptionsTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.exceptionsGrid)).BeginInit();
            this.exceptionsPanel.SuspendLayout();
            this.filesTab.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.filesGrid)).BeginInit();
            this.logTab.SuspendLayout();
            this.logPanel.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStrip
            //
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.fileMenu, this.engineMenu, this.helpMenu });
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1184, 24);
            //
            // fileMenu
            //
            this.fileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.exitItem });
            this.fileMenu.Name = "fileMenu";
            this.fileMenu.Text = "&File";
            this.exitItem.Name = "exitItem";
            this.exitItem.Text = "E&xit";
            this.exitItem.Click += new System.EventHandler(this.ExitItem_Click);
            //
            // engineMenu
            //
            this.engineMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.pauseItem, this.continueItem, this.scanItem });
            this.engineMenu.Name = "engineMenu";
            this.engineMenu.Text = "&Engine";
            this.pauseItem.Name = "pauseItem";
            this.pauseItem.Text = "&Pause engine";
            this.pauseItem.Click += new System.EventHandler(this.PauseItem_Click);
            this.continueItem.Name = "continueItem";
            this.continueItem.Text = "&Continue engine";
            this.continueItem.Click += new System.EventHandler(this.ContinueItem_Click);
            this.scanItem.Name = "scanItem";
            this.scanItem.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this.scanItem.Text = "&Read files now";
            this.scanItem.Click += new System.EventHandler(this.ScanItem_Click);
            //
            // helpMenu
            //
            this.helpMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.aboutItem });
            this.helpMenu.Name = "helpMenu";
            this.helpMenu.Text = "&Help";
            this.aboutItem.Name = "aboutItem";
            this.aboutItem.Text = "&About Billing Monitor";
            this.aboutItem.Click += new System.EventHandler(this.AboutItem_Click);
            //
            // toolStrip
            //
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.refreshButton, this.toolSeparator, this.holdButton, this.resumeButton, this.rerateButton, this.approveButton, this.cancelRunButton });
            this.toolStrip.Location = new System.Drawing.Point(0, 24);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1184, 25);
            this.refreshButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Text = "Refresh (F5)";
            this.refreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            this.toolSeparator.Name = "toolSeparator";
            this.holdButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.holdButton.Name = "holdButton";
            this.holdButton.Tag = "hold";
            this.holdButton.Text = "Hold";
            this.holdButton.Click += new System.EventHandler(this.RunCommand_Click);
            this.resumeButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.resumeButton.Name = "resumeButton";
            this.resumeButton.Tag = "resume";
            this.resumeButton.Text = "Resume";
            this.resumeButton.Click += new System.EventHandler(this.RunCommand_Click);
            this.rerateButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.rerateButton.Name = "rerateButton";
            this.rerateButton.Tag = "rerate";
            this.rerateButton.Text = "Rate again";
            this.rerateButton.Click += new System.EventHandler(this.RunCommand_Click);
            this.approveButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.approveButton.Name = "approveButton";
            this.approveButton.Tag = "approve";
            this.approveButton.Text = "Approve for release";
            this.approveButton.Click += new System.EventHandler(this.RunCommand_Click);
            this.cancelRunButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.cancelRunButton.Name = "cancelRunButton";
            this.cancelRunButton.Tag = "cancel";
            this.cancelRunButton.Text = "Cancel run";
            this.cancelRunButton.Click += new System.EventHandler(this.RunCommand_Click);
            //
            // tabControl
            //
            this.tabControl.Controls.Add(this.runsTab);
            this.tabControl.Controls.Add(this.exceptionsTab);
            this.tabControl.Controls.Add(this.filesTab);
            this.tabControl.Controls.Add(this.logTab);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 49);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(1184, 590);
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.TabControl_SelectedIndexChanged);
            //
            // runsTab
            //
            this.runsTab.Controls.Add(this.runsSplit);
            this.runsTab.Name = "runsTab";
            this.runsTab.Padding = new System.Windows.Forms.Padding(3);
            this.runsTab.Text = "Bill runs";
            this.runsTab.UseVisualStyleBackColor = true;
            //
            // runsSplit
            //
            this.runsSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.runsSplit.Name = "runsSplit";
            this.runsSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.runsSplit.Panel1.Controls.Add(this.runsGrid);
            this.runsSplit.Panel2.Controls.Add(this.eventsList);
            this.runsSplit.Size = new System.Drawing.Size(1170, 558);
            this.runsSplit.SplitterDistance = 320;
            //
            // runsGrid
            //
            this.runsGrid.AllowUserToAddRows = false;
            this.runsGrid.AllowUserToDeleteRows = false;
            this.runsGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.runsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.runsGrid.MultiSelect = false;
            this.runsGrid.Name = "runsGrid";
            this.runsGrid.ReadOnly = true;
            this.runsGrid.RowHeadersVisible = false;
            this.runsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.runsGrid.SelectionChanged += new System.EventHandler(this.RunsGrid_SelectionChanged);
            this.runsGrid.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.RunsGrid_CellFormatting);
            //
            // eventsList
            //
            this.eventsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this.eventTimeColumn, this.eventLevelColumn, this.eventUserColumn, this.eventMessageColumn });
            this.eventsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.eventsList.FullRowSelect = true;
            this.eventsList.HideSelection = false;
            this.eventsList.Name = "eventsList";
            this.eventsList.View = System.Windows.Forms.View.Details;
            this.eventTimeColumn.Text = "Time";
            this.eventTimeColumn.Width = 130;
            this.eventLevelColumn.Text = "Level";
            this.eventLevelColumn.Width = 70;
            this.eventUserColumn.Text = "User";
            this.eventUserColumn.Width = 160;
            this.eventMessageColumn.Text = "Message";
            this.eventMessageColumn.Width = 780;
            //
            // exceptionsTab
            //
            this.exceptionsTab.Controls.Add(this.exceptionsGrid);
            this.exceptionsTab.Controls.Add(this.exceptionsPanel);
            this.exceptionsTab.Name = "exceptionsTab";
            this.exceptionsTab.Padding = new System.Windows.Forms.Padding(3);
            this.exceptionsTab.Text = "Exceptions";
            this.exceptionsTab.UseVisualStyleBackColor = true;
            //
            // exceptionsGrid
            //
            this.exceptionsGrid.AllowUserToAddRows = false;
            this.exceptionsGrid.AllowUserToDeleteRows = false;
            this.exceptionsGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.exceptionsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.exceptionsGrid.Name = "exceptionsGrid";
            this.exceptionsGrid.ReadOnly = true;
            this.exceptionsGrid.RowHeadersVisible = false;
            this.exceptionsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            //
            // exceptionsPanel
            //
            this.exceptionsPanel.Controls.Add(this.showResolvedCheck);
            this.exceptionsPanel.Controls.Add(this.excludeButton);
            this.exceptionsPanel.Controls.Add(this.estimateButton);
            this.exceptionsPanel.Controls.Add(this.acceptButton);
            this.exceptionsPanel.Controls.Add(this.noteBox);
            this.exceptionsPanel.Controls.Add(this.noteLabel);
            this.exceptionsPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.exceptionsPanel.Name = "exceptionsPanel";
            this.exceptionsPanel.Size = new System.Drawing.Size(1170, 44);
            this.noteLabel.AutoSize = true;
            this.noteLabel.Location = new System.Drawing.Point(6, 14);
            this.noteLabel.Name = "noteLabel";
            this.noteLabel.Text = "Note:";
            this.noteBox.Location = new System.Drawing.Point(48, 11);
            this.noteBox.MaxLength = 500;
            this.noteBox.Name = "noteBox";
            this.noteBox.Size = new System.Drawing.Size(420, 20);
            this.acceptButton.Location = new System.Drawing.Point(480, 9);
            this.acceptButton.Name = "acceptButton";
            this.acceptButton.Size = new System.Drawing.Size(120, 24);
            this.acceptButton.Tag = Coho.Billing.Core.Runs.ExceptionResolution.AcceptRead;
            this.acceptButton.Text = "Accept read";
            this.acceptButton.Click += new System.EventHandler(this.ResolveButton_Click);
            this.estimateButton.Location = new System.Drawing.Point(606, 9);
            this.estimateButton.Name = "estimateButton";
            this.estimateButton.Size = new System.Drawing.Size(120, 24);
            this.estimateButton.Tag = Coho.Billing.Core.Runs.ExceptionResolution.UseEstimate;
            this.estimateButton.Text = "Use estimate";
            this.estimateButton.Click += new System.EventHandler(this.ResolveButton_Click);
            this.excludeButton.Location = new System.Drawing.Point(732, 9);
            this.excludeButton.Name = "excludeButton";
            this.excludeButton.Size = new System.Drawing.Size(140, 24);
            this.excludeButton.Tag = Coho.Billing.Core.Runs.ExceptionResolution.Exclude;
            this.excludeButton.Text = "Leave out of run";
            this.excludeButton.Click += new System.EventHandler(this.ResolveButton_Click);
            this.showResolvedCheck.AutoSize = true;
            this.showResolvedCheck.Location = new System.Drawing.Point(890, 13);
            this.showResolvedCheck.Name = "showResolvedCheck";
            this.showResolvedCheck.Text = "Show resolved";
            this.showResolvedCheck.CheckedChanged += new System.EventHandler(this.ShowResolvedCheck_CheckedChanged);
            //
            // filesTab
            //
            this.filesTab.Controls.Add(this.filesGrid);
            this.filesTab.Name = "filesTab";
            this.filesTab.Padding = new System.Windows.Forms.Padding(3);
            this.filesTab.Text = "Read files";
            this.filesTab.UseVisualStyleBackColor = true;
            this.filesGrid.AllowUserToAddRows = false;
            this.filesGrid.AllowUserToDeleteRows = false;
            this.filesGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.filesGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filesGrid.Name = "filesGrid";
            this.filesGrid.ReadOnly = true;
            this.filesGrid.RowHeadersVisible = false;
            this.filesGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            //
            // logTab
            //
            this.logTab.Controls.Add(this.logBox);
            this.logTab.Controls.Add(this.logPanel);
            this.logTab.Name = "logTab";
            this.logTab.Padding = new System.Windows.Forms.Padding(3);
            this.logTab.Text = "Engine log";
            this.logTab.UseVisualStyleBackColor = true;
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Consolas", 9F);
            this.logBox.Multiline = true;
            this.logBox.Name = "logBox";
            this.logBox.ReadOnly = true;
            this.logBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.logBox.WordWrap = false;
            this.logPanel.Controls.Add(this.logFollowCheck);
            this.logPanel.Controls.Add(this.logDatePicker);
            this.logPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.logPanel.Name = "logPanel";
            this.logPanel.Size = new System.Drawing.Size(1170, 32);
            this.logDatePicker.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.logDatePicker.Location = new System.Drawing.Point(6, 6);
            this.logDatePicker.Name = "logDatePicker";
            this.logDatePicker.Size = new System.Drawing.Size(110, 20);
            this.logDatePicker.ValueChanged += new System.EventHandler(this.LogDatePicker_ValueChanged);
            this.logFollowCheck.AutoSize = true;
            this.logFollowCheck.Checked = true;
            this.logFollowCheck.Location = new System.Drawing.Point(130, 8);
            this.logFollowCheck.Name = "logFollowCheck";
            this.logFollowCheck.Text = "Follow";
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.engineLabel, this.workLabel, this.lastCycleLabel });
            this.statusStrip.Location = new System.Drawing.Point(0, 639);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1184, 22);
            this.engineLabel.Name = "engineLabel";
            this.engineLabel.Text = "Engine: connecting...";
            this.workLabel.Name = "workLabel";
            this.workLabel.Spring = true;
            this.workLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lastCycleLabel.Name = "lastCycleLabel";
            //
            // refreshTimer
            //
            this.refreshTimer.Interval = 15000;
            this.refreshTimer.Tick += new System.EventHandler(this.RefreshTimer_Tick);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 661);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.menuStrip);
            this.Controls.Add(this.statusStrip);
            this.KeyPreview = true;
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(900, 500);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Billing Monitor";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MainForm_KeyDown);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.runsTab.ResumeLayout(false);
            this.runsSplit.Panel1.ResumeLayout(false);
            this.runsSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.runsSplit)).EndInit();
            this.runsSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.runsGrid)).EndInit();
            this.exceptionsTab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.exceptionsGrid)).EndInit();
            this.exceptionsPanel.ResumeLayout(false);
            this.exceptionsPanel.PerformLayout();
            this.filesTab.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.filesGrid)).EndInit();
            this.logTab.ResumeLayout(false);
            this.logTab.PerformLayout();
            this.logPanel.ResumeLayout(false);
            this.logPanel.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileMenu;
        private System.Windows.Forms.ToolStripMenuItem exitItem;
        private System.Windows.Forms.ToolStripMenuItem engineMenu;
        private System.Windows.Forms.ToolStripMenuItem pauseItem;
        private System.Windows.Forms.ToolStripMenuItem continueItem;
        private System.Windows.Forms.ToolStripMenuItem scanItem;
        private System.Windows.Forms.ToolStripMenuItem helpMenu;
        private System.Windows.Forms.ToolStripMenuItem aboutItem;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton refreshButton;
        private System.Windows.Forms.ToolStripSeparator toolSeparator;
        private System.Windows.Forms.ToolStripButton holdButton;
        private System.Windows.Forms.ToolStripButton resumeButton;
        private System.Windows.Forms.ToolStripButton rerateButton;
        private System.Windows.Forms.ToolStripButton approveButton;
        private System.Windows.Forms.ToolStripButton cancelRunButton;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage runsTab;
        private System.Windows.Forms.SplitContainer runsSplit;
        private System.Windows.Forms.DataGridView runsGrid;
        private System.Windows.Forms.ListView eventsList;
        private System.Windows.Forms.ColumnHeader eventTimeColumn;
        private System.Windows.Forms.ColumnHeader eventLevelColumn;
        private System.Windows.Forms.ColumnHeader eventUserColumn;
        private System.Windows.Forms.ColumnHeader eventMessageColumn;
        private System.Windows.Forms.TabPage exceptionsTab;
        private System.Windows.Forms.DataGridView exceptionsGrid;
        private System.Windows.Forms.Panel exceptionsPanel;
        private System.Windows.Forms.CheckBox showResolvedCheck;
        private System.Windows.Forms.Button excludeButton;
        private System.Windows.Forms.Button estimateButton;
        private System.Windows.Forms.Button acceptButton;
        private System.Windows.Forms.TextBox noteBox;
        private System.Windows.Forms.Label noteLabel;
        private System.Windows.Forms.TabPage filesTab;
        private System.Windows.Forms.DataGridView filesGrid;
        private System.Windows.Forms.TabPage logTab;
        private System.Windows.Forms.TextBox logBox;
        private System.Windows.Forms.Panel logPanel;
        private System.Windows.Forms.CheckBox logFollowCheck;
        private System.Windows.Forms.DateTimePicker logDatePicker;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel engineLabel;
        private System.Windows.Forms.ToolStripStatusLabel workLabel;
        private System.Windows.Forms.ToolStripStatusLabel lastCycleLabel;
        private System.Windows.Forms.Timer refreshTimer;
    }
}
