// ========== FILE: Forms/MainForm.Designer.cs ==========
using System.Drawing;
using System.Windows.Forms;

namespace WebScrapeCron.Forms
{
    partial class MainForm
    {
        // Container controls
        private Panel headerPanel, sidebarPanel, contentPanel, statusBarPanel;

        // Header controls
        private Label titleLabel;
        private Button btnStartScheduler, btnStopScheduler, btnManualScrape;

        // Sidebar nav buttons
        private Button navDashboard, navScheduling, navBulkScraper, navCleanData, navHistory, navSettings;

        // Dashboard panel
        private Panel dashboardPanel;
        private Label lblTotalTargets, lblTotalRecords, lblLastRun, lblErrorCount;
        private Label lblTargetsTitle, lblRecordsTitle, lblLastRunTitle, lblErrorsTitle;

        // Scheduling panel
        private Panel schedulingPanel;
        private DataGridView dgvScheduling;
        private Button btnAddScheduleTarget, btnEditTarget, btnDeleteTarget;

        // Bulk scraper panel
        private Panel bulkScraperPanel;
        private DataGridView dgvBulkTargets;
        private Button btnAddBulkTarget, btnEditBulkTarget, btnDeleteBulkTarget, btnStartBulkScrape, btnCancelBulkScrape;
        private ProgressBar progressBarBulk;
        private Label lblBulkProgress;

        // Clean Data panel — two-section layout
        private Panel cleanDataPanel;
        private TabControl cleanTabControl;
        // Scheduling clean data tab
        private TabPage tabCleanSched;
        private FlowLayoutPanel flpSchedButtons;
        private DataGridView dgvCleanSched;
        private Button btnCleanSchedExport, btnCleanSchedDelete, btnCleanSchedRefresh;
        // Bulk clean data tab
        private TabPage tabCleanBulk;
        private FlowLayoutPanel flpBulkButtons;
        private DataGridView dgvCleanBulk;
        private Button btnCleanBulkExport, btnCleanBulkDelete, btnCleanBulkRefresh;

        // History panel
        private Panel historyPanel;
        private DataGridView dgvHistory;
        private ComboBox cmbFilterTarget;
        private Button btnExportCSV, btnRefreshHistory;

        // Settings panel
        private Panel settingsPanel;
        private TextBox txtServerName, txtDatabaseName, txtSqlUsername, txtSqlPassword;
        private RadioButton rbWindowsAuth, rbSqlAuth;
        private Button btnTestConnection, btnSaveSettings, btnFactoryReset;
        private Label lblConnectionStatus;

        // Status bar
        private Label lblSchedulerStatus, lblLastTick;
        private PictureBox picStatus;

        // Warning banner
        private Panel warningBanner;
        private Label lblWarning;

        // Colors
        private static readonly Color ColorLime = ColorTranslator.FromHtml("#39FF14");
        private static readonly Color ColorBlue = ColorTranslator.FromHtml("#1E90FF");
        private static readonly Color ColorDarkBg = ColorTranslator.FromHtml("#2B2B2B");
        private static readonly Color ColorPanelBg = ColorTranslator.FromHtml("#3C3C3C");
        private static readonly Color ColorCardBg = ColorTranslator.FromHtml("#4A4A4A");
        private static readonly Color ColorRed = ColorTranslator.FromHtml("#FF4444");
        private static readonly Color ColorGray = ColorTranslator.FromHtml("#888888");
        private static readonly Color ColorSidebarBg = ColorTranslator.FromHtml("#333333");
        private static readonly Color ColorWhite = Color.White;

        private void InitializeComponent()
        {
            this.Text = "WebScrape Cron - Background Dataset Builder";
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(960, 640);
            this.BackColor = ColorDarkBg;
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;

            // ================================================================
            // HEADER PANEL
            // ================================================================
            headerPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = ColorPanelBg };
            titleLabel = new Label { Text = "WebScrape Cron", Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = ColorLime, AutoSize = true, Location = new Point(15, 14) };
            btnStartScheduler = MakeButton("Start Scheduler", ColorLime, ColorDarkBg);
            btnStartScheduler.SetBounds(220, 12, 140, 32);
            btnStopScheduler = MakeButton("Stop Scheduler", ColorRed, Color.White);
            btnStopScheduler.SetBounds(368, 12, 140, 32);
            btnManualScrape = MakeButton("Manual Scrape", ColorBlue, Color.White);
            btnManualScrape.SetBounds(516, 12, 130, 32);
            headerPanel.Controls.AddRange(new Control[] { titleLabel, btnStartScheduler, btnStopScheduler, btnManualScrape });

            // ================================================================
            // STATUS BAR
            // ================================================================
            statusBarPanel = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = ColorPanelBg };
            picStatus = new PictureBox { Size = new Size(10, 10), Location = new Point(12, 11), BackColor = ColorRed };
            picStatus.Paint += (s, e) => { e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.FillEllipse(new SolidBrush(picStatus.BackColor), 0, 0, 10, 10); };
            lblSchedulerStatus = new Label { Text = "Stopped", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ColorRed, AutoSize = true, Location = new Point(28, 7) };
            lblLastTick = new Label { Text = "Last Tick: -", Font = new Font("Segoe UI", 8), ForeColor = ColorGray, AutoSize = true, Location = new Point(1000, 9) };
            statusBarPanel.Controls.AddRange(new Control[] { picStatus, lblSchedulerStatus, lblLastTick });

            // ================================================================
            // WARNING BANNER
            // ================================================================
            warningBanner = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(60, 20, 20), Visible = false };
            lblWarning = new Label { Text = "No database configured. Go to Settings.", Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#FF8888"), AutoSize = true, Location = new Point(15, 10) };
            warningBanner.Controls.Add(lblWarning);

            // ================================================================
            // SIDEBAR
            // ================================================================
            sidebarPanel = new Panel { Dock = DockStyle.Left, Width = 180, BackColor = ColorSidebarBg };
            navDashboard = MakeNavBtn("Dashboard", 0);
            navScheduling = MakeNavBtn("Scheduling", 1);
            navBulkScraper = MakeNavBtn("Bulk Scraper", 2);
            navCleanData = MakeNavBtn("Clean Data", 3);
            navHistory = MakeNavBtn("History", 4);
            navSettings = MakeNavBtn("Settings", 5);
            navDashboard.BackColor = ColorLime; navDashboard.ForeColor = ColorDarkBg;
            sidebarPanel.Controls.AddRange(new Control[] { navDashboard, navScheduling, navBulkScraper, navCleanData, navHistory, navSettings });

            // ================================================================
            // DASHBOARD
            // ================================================================
            dashboardPanel = MakePanel();
            dashboardPanel.Controls.AddRange(MakeDashboardCards());

            // ================================================================
            // SCHEDULING
            // ================================================================
            schedulingPanel = MakePanel();
            var schedToolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ColorPanelBg };
            btnAddScheduleTarget = MakeButton("+ Add Target", ColorLime, ColorDarkBg); btnAddScheduleTarget.SetBounds(5, 5, 120, 30);
            btnEditTarget = MakeButton("Edit", ColorBlue, Color.White); btnEditTarget.SetBounds(130, 5, 70, 30);
            btnDeleteTarget = MakeButton("Delete", ColorRed, Color.White); btnDeleteTarget.SetBounds(205, 5, 80, 30);
            schedToolbar.Controls.AddRange(new Control[] { btnAddScheduleTarget, btnEditTarget, btnDeleteTarget });
            dgvScheduling = MakeGrid(); dgvScheduling.Dock = DockStyle.Fill;
            schedulingPanel.Controls.AddRange(new Control[] { dgvScheduling, schedToolbar });

            // ================================================================
            // BULK SCRAPER (with Edit + Delete buttons)
            // ================================================================
            bulkScraperPanel = MakePanel();
            var bulkToolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ColorPanelBg };
            btnAddBulkTarget = MakeButton("+ Add Bulk", ColorLime, ColorDarkBg); btnAddBulkTarget.SetBounds(5, 5, 110, 30);
            btnEditBulkTarget = MakeButton("Edit", ColorBlue, Color.White); btnEditBulkTarget.SetBounds(120, 5, 70, 30);
            btnDeleteBulkTarget = MakeButton("Delete", ColorRed, Color.White); btnDeleteBulkTarget.SetBounds(195, 5, 80, 30);
            btnStartBulkScrape = MakeButton("Start Scrape", ColorBlue, Color.White); btnStartBulkScrape.SetBounds(280, 5, 120, 30);
            btnCancelBulkScrape = MakeButton("Cancel", ColorRed, Color.White); btnCancelBulkScrape.SetBounds(405, 5, 80, 30);
            btnCancelBulkScrape.Enabled = false;
            bulkToolbar.Controls.AddRange(new Control[] { btnAddBulkTarget, btnEditBulkTarget, btnDeleteBulkTarget, btnStartBulkScrape, btnCancelBulkScrape });
            var progressPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = ColorPanelBg };
            progressBarBulk = new ProgressBar { Location = new Point(5, 8), Size = new Size(400, 24), Style = ProgressBarStyle.Continuous };
            lblBulkProgress = new Label { Text = "Ready", Font = new Font("Segoe UI", 9), ForeColor = Color.White, AutoSize = true, Location = new Point(415, 10) };
            progressPanel.Controls.AddRange(new Control[] { progressBarBulk, lblBulkProgress });
            dgvBulkTargets = MakeGrid(); dgvBulkTargets.Dock = DockStyle.Fill;
            bulkScraperPanel.Controls.AddRange(new Control[] { dgvBulkTargets, progressPanel, bulkToolbar });

            // ================================================================
            // CLEAN DATA (Two tabs: Scheduling Data + Bulk Data)
            // ================================================================
            cleanDataPanel = MakePanel();
            cleanTabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            cleanTabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            cleanTabControl.ItemSize = new Size(200, 36);
            cleanTabControl.SizeMode = TabSizeMode.Fixed;
            cleanTabControl.DrawItem += (s, e) =>
            {
                var g = e.Graphics;
                var tabPage = cleanTabControl.TabPages[e.Index];
                var tabBounds = cleanTabControl.GetTabRect(e.Index);
                bool selected = (cleanTabControl.SelectedIndex == e.Index);
                g.FillRectangle(new SolidBrush(selected ? ColorPanelBg : ColorSidebarBg), tabBounds);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(tabPage.Text, cleanTabControl.Font, new SolidBrush(selected ? ColorLime : ColorWhite), tabBounds, sf);
            };

            // -- Scheduling Data Tab --
            tabCleanSched = new TabPage("Scheduling Data") { BackColor = ColorDarkBg };
            var schedCleanToolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ColorPanelBg };
            btnCleanSchedRefresh = MakeButton("Refresh", ColorBlue, Color.White); btnCleanSchedRefresh.SetBounds(5, 5, 90, 30);
            btnCleanSchedExport = MakeButton("Export CSV", ColorGray, Color.White); btnCleanSchedExport.SetBounds(100, 5, 100, 30);
            btnCleanSchedDelete = MakeButton("Delete Data", ColorRed, Color.White); btnCleanSchedDelete.SetBounds(205, 5, 100, 30);
            schedCleanToolbar.Controls.AddRange(new Control[] { btnCleanSchedRefresh, btnCleanSchedExport, btnCleanSchedDelete });
            var schedCleanSplit = new SplitContainer { Dock = DockStyle.Fill, BackColor = ColorDarkBg, Orientation = Orientation.Vertical };
            flpSchedButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = ColorPanelBg, AutoScroll = true, Padding = new Padding(5), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            flpSchedButtons.Controls.Add(new Label { Text = "Tables", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorLime, AutoSize = true, Margin = new Padding(5) });
            dgvCleanSched = MakeGrid(); dgvCleanSched.Dock = DockStyle.Fill;
            schedCleanSplit.Panel1.Controls.Add(flpSchedButtons);
            schedCleanSplit.Panel2.Controls.Add(dgvCleanSched);
            tabCleanSched.Controls.Add(schedCleanSplit);
            tabCleanSched.Controls.Add(schedCleanToolbar);

            // -- Bulk Data Tab --
            tabCleanBulk = new TabPage("Bulk Data") { BackColor = ColorDarkBg };
            var bulkCleanToolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ColorPanelBg };
            btnCleanBulkRefresh = MakeButton("Refresh", ColorBlue, Color.White); btnCleanBulkRefresh.SetBounds(5, 5, 90, 30);
            btnCleanBulkExport = MakeButton("Export CSV", ColorGray, Color.White); btnCleanBulkExport.SetBounds(100, 5, 100, 30);
            btnCleanBulkDelete = MakeButton("Delete Data", ColorRed, Color.White); btnCleanBulkDelete.SetBounds(205, 5, 100, 30);
            bulkCleanToolbar.Controls.AddRange(new Control[] { btnCleanBulkRefresh, btnCleanBulkExport, btnCleanBulkDelete });
            var bulkCleanSplit = new SplitContainer { Dock = DockStyle.Fill, BackColor = ColorDarkBg, Orientation = Orientation.Vertical };
            flpBulkButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = ColorPanelBg, AutoScroll = true, Padding = new Padding(5), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            flpBulkButtons.Controls.Add(new Label { Text = "Tables", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorBlue, AutoSize = true, Margin = new Padding(5) });
            dgvCleanBulk = MakeGrid(); dgvCleanBulk.Dock = DockStyle.Fill;
            bulkCleanSplit.Panel1.Controls.Add(flpBulkButtons);
            bulkCleanSplit.Panel2.Controls.Add(dgvCleanBulk);
            tabCleanBulk.Controls.Add(bulkCleanSplit);
            tabCleanBulk.Controls.Add(bulkCleanToolbar);

            cleanTabControl.TabPages.Add(tabCleanSched);
            cleanTabControl.TabPages.Add(tabCleanBulk);
            cleanDataPanel.Controls.Add(cleanTabControl);

            // ================================================================
            // HISTORY
            // ================================================================
            historyPanel = MakePanel();
            var histToolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = ColorPanelBg };
            var lblFilter = new Label { Text = "Filter:", Font = new Font("Segoe UI", 9), ForeColor = Color.White, AutoSize = true, Location = new Point(5, 12) };
            cmbFilterTarget = new ComboBox { Location = new Point(50, 8), Size = new Size(220, 26), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ColorCardBg, ForeColor = Color.White, Font = new Font("Segoe UI", 9) };
            btnRefreshHistory = MakeButton("Refresh", ColorBlue, Color.White); btnRefreshHistory.SetBounds(280, 6, 80, 28);
            btnExportCSV = MakeButton("Export CSV", ColorGray, Color.White); btnExportCSV.SetBounds(370, 6, 100, 28);
            histToolbar.Controls.AddRange(new Control[] { lblFilter, cmbFilterTarget, btnRefreshHistory, btnExportCSV });
            dgvHistory = MakeGrid(); dgvHistory.Dock = DockStyle.Fill;
            historyPanel.Controls.AddRange(new Control[] { dgvHistory, histToolbar });

            // ================================================================
            // SETTINGS (with Factory Reset)
            // ================================================================
            settingsPanel = MakePanel();
            var lblSettingsTitle = new Label { Text = "Database Connection Settings", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorLime, AutoSize = true, Location = new Point(20, 15) };
            var lblServer = new Label { Text = "Server Name:", Font = new Font("Segoe UI", 10), ForeColor = Color.White, AutoSize = true, Location = new Point(20, 60) };
            txtServerName = new TextBox { Location = new Point(20, 82), Size = new Size(400, 28), BackColor = ColorCardBg, ForeColor = Color.White, Font = new Font("Segoe UI", 10), Text = @"localhost\SQLEXPRESS" };
            var lblDb = new Label { Text = "Database Name:", Font = new Font("Segoe UI", 10), ForeColor = Color.White, AutoSize = true, Location = new Point(20, 120) };
            txtDatabaseName = new TextBox { Location = new Point(20, 142), Size = new Size(400, 28), BackColor = ColorCardBg, ForeColor = Color.White, Font = new Font("Segoe UI", 10), Text = "WebScrapeCronDB" };
            var lblAuth = new Label { Text = "Authentication:", Font = new Font("Segoe UI", 10), ForeColor = ColorWhite, AutoSize = true, Location = new Point(20, 185) };
            rbWindowsAuth = new RadioButton { Text = "Windows Authentication", Font = new Font("Segoe UI", 10), ForeColor = ColorWhite, Location = new Point(20, 210), Checked = true, AutoSize = true };
            rbSqlAuth = new RadioButton { Text = "SQL Server Authentication", Font = new Font("Segoe UI", 10), ForeColor = ColorWhite, Location = new Point(250, 210), AutoSize = true };
            var lblUser = new Label { Text = "Username:", Font = new Font("Segoe UI", 10), ForeColor = ColorWhite, AutoSize = true, Location = new Point(20, 250) };
            txtSqlUsername = new TextBox { Location = new Point(20, 272), Size = new Size(400, 28), BackColor = ColorCardBg, ForeColor = ColorWhite, Font = new Font("Segoe UI", 10), Enabled = false };
            var lblPass = new Label { Text = "Password:", Font = new Font("Segoe UI", 10), ForeColor = ColorWhite, AutoSize = true, Location = new Point(20, 310) };
            txtSqlPassword = new TextBox { Location = new Point(20, 332), Size = new Size(400, 28), BackColor = ColorCardBg, ForeColor = ColorWhite, Font = new Font("Segoe UI", 10), UseSystemPasswordChar = true, Enabled = false };
            btnTestConnection = MakeButton("Test Connection", ColorBlue, ColorWhite); btnTestConnection.SetBounds(20, 380, 150, 34);
            btnSaveSettings = MakeButton("Save Settings", ColorLime, ColorDarkBg); btnSaveSettings.SetBounds(180, 380, 140, 34);
            lblConnectionStatus = new Label { Text = "", Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Location = new Point(20, 425) };
            // Factory Reset button
            btnFactoryReset = MakeButton("Factory Reset", ColorRed, ColorWhite); btnFactoryReset.SetBounds(20, 480, 150, 34);
            var lblResetNote = new Label { Text = "Clears all saved settings and resets the application.", Font = new Font("Segoe UI", 9), ForeColor = ColorGray, AutoSize = true, Location = new Point(180, 488) };
            settingsPanel.Controls.AddRange(new Control[] { lblSettingsTitle, lblServer, txtServerName, lblDb, txtDatabaseName, lblAuth, rbWindowsAuth, rbSqlAuth, lblUser, txtSqlUsername, lblPass, txtSqlPassword, btnTestConnection, btnSaveSettings, lblConnectionStatus, btnFactoryReset, lblResetNote });

            // Auth radio toggle
            rbSqlAuth.CheckedChanged += (s, e) => { txtSqlUsername.Enabled = rbSqlAuth.Checked; txtSqlPassword.Enabled = rbSqlAuth.Checked; };

            // ================================================================
            // CONTENT PANEL
            // ================================================================
            contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = ColorDarkBg };
            dashboardPanel.Dock = DockStyle.Fill;
            schedulingPanel.Dock = DockStyle.Fill;
            bulkScraperPanel.Dock = DockStyle.Fill;
            cleanDataPanel.Dock = DockStyle.Fill;
            historyPanel.Dock = DockStyle.Fill;
            settingsPanel.Dock = DockStyle.Fill;
            contentPanel.Controls.AddRange(new Control[] { dashboardPanel, schedulingPanel, bulkScraperPanel, cleanDataPanel, historyPanel, settingsPanel });
            schedulingPanel.Visible = false;
            bulkScraperPanel.Visible = false;
            cleanDataPanel.Visible = false;
            historyPanel.Visible = false;
            settingsPanel.Visible = false;

            // Add to form (order matters for Dock)
            this.Controls.Add(contentPanel);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(statusBarPanel);
            this.Controls.Add(warningBanner);
            this.Controls.Add(headerPanel);
        }

        private Button MakeButton(string text, Color bg, Color fg)
        {
            var btn = new Button { Text = text, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Light(bg, 0.2f);
            btn.MouseLeave += (s, e) => btn.BackColor = bg;
            return btn;
        }

        private Button MakeNavBtn(string text, int idx)
        {
            var btn = new Button { Text = text, Dock = DockStyle.Top, Height = 48, BackColor = ColorSidebarBg,
                ForeColor = ColorWhite, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => { if (btn.BackColor != ColorLime) btn.BackColor = ColorTranslator.FromHtml("#444444"); };
            btn.MouseLeave += (s, e) => { if (btn.BackColor != ColorLime) btn.BackColor = ColorSidebarBg; };
            return btn;
        }

        private Panel MakePanel() => new Panel { BackColor = ColorDarkBg, Padding = new Padding(10) };

        private DataGridView MakeGrid()
        {
            var dgv = new DataGridView
            {
                BackgroundColor = ColorDarkBg, ForeColor = ColorWhite, BackColor = ColorDarkBg,
                GridColor = Color.FromArgb(60, 60, 60), BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9), RowHeadersVisible = false
            };
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorPanelBg;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColorLime;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 32;
            dgv.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 45);
            dgv.DefaultCellStyle.ForeColor = ColorWhite;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(57, 255, 20, 40);
            dgv.DefaultCellStyle.SelectionForeColor = ColorWhite;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(50, 50, 50);
            return dgv;
        }

        private Control[] MakeDashboardCards()
        {
            var c1 = new Panel { BackColor = ColorCardBg, Location = new Point(10, 10), Size = new Size(220, 90) };
            lblTargetsTitle = new Label { Text = "Total Targets", Font = new Font("Segoe UI", 9), ForeColor = ColorGray, Location = new Point(12, 8), AutoSize = true };
            lblTotalTargets = new Label { Text = "0", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = ColorLime, Location = new Point(12, 35), AutoSize = true };
            c1.Controls.AddRange(new Control[] { lblTargetsTitle, lblTotalTargets });

            var c2 = new Panel { BackColor = ColorCardBg, Location = new Point(245, 10), Size = new Size(220, 90) };
            lblRecordsTitle = new Label { Text = "Total Records", Font = new Font("Segoe UI", 9), ForeColor = ColorGray, Location = new Point(12, 8), AutoSize = true };
            lblTotalRecords = new Label { Text = "0", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = ColorLime, Location = new Point(12, 35), AutoSize = true };
            c2.Controls.AddRange(new Control[] { lblRecordsTitle, lblTotalRecords });

            var c3 = new Panel { BackColor = ColorCardBg, Location = new Point(480, 10), Size = new Size(220, 90) };
            lblLastRunTitle = new Label { Text = "Last Run", Font = new Font("Segoe UI", 9), ForeColor = ColorGray, Location = new Point(12, 8), AutoSize = true };
            lblLastRun = new Label { Text = "Never", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = ColorLime, Location = new Point(12, 35), AutoSize = true };
            c3.Controls.AddRange(new Control[] { lblLastRunTitle, lblLastRun });

            var c4 = new Panel { BackColor = ColorCardBg, Location = new Point(715, 10), Size = new Size(220, 90) };
            lblErrorsTitle = new Label { Text = "Errors", Font = new Font("Segoe UI", 9), ForeColor = ColorGray, Location = new Point(12, 8), AutoSize = true };
            lblErrorCount = new Label { Text = "0", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = ColorRed, Location = new Point(12, 35), AutoSize = true };
            c4.Controls.AddRange(new Control[] { lblErrorsTitle, lblErrorCount });

            return new Control[] { c1, c2, c3, c4 };
        }
    }
}
