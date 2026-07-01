// ========== FILE: Forms/MainForm.cs ==========
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using WebScrapeCron.Config;
using WebScrapeCron.Data;
using WebScrapeCron.Models;
using WebScrapeCron.Scrapers;
using WebScrapeCron.Utilities;

namespace WebScrapeCron.Forms
{
    public partial class MainForm : Form
    {
        private readonly SqlDataRepository _repository;
        private CronScheduler _scheduler;
        private Button _activeNavButton;
        private CancellationTokenSource? _bulkCts;
        private string? _activeCleanSchedTable;
        private string? _activeCleanBulkTable;

        public MainForm()
        {
            InitializeComponent();
            _repository = new SqlDataRepository();
            _scheduler = new CronScheduler(_repository, 300000);
            WireUpEvents();
            _activeNavButton = navDashboard;

            this.Shown += async (s, e) =>
            {
                try { await InitializeAsync(); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Initialization error:\n\n{ex.Message}", "Startup Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
        }

        private async Task InitializeAsync()
        {
            if (!AppSettings.IsConfigured) { warningBanner.Visible = true; DisableDbButtons(); return; }
            try
            {
                await LoadSchedulerScrapersAsync();
                _scheduler.TickCompleted += OnSchedulerTickCompleted;
                await RefreshAllAsync();
                warningBanner.Visible = false;
            }
            catch (Exception ex)
            {
                warningBanner.Visible = true; DisableDbButtons();
                MessageBox.Show($"Database connection failed:\n\n{ex.Message}\n\nGo to Settings to configure.",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task LoadSchedulerScrapersAsync()
        {
            _scheduler.ClearScrapers();
            var jobs = await _repository.GetActiveJobsAsync("scheduling");
            foreach (var job in jobs) _scheduler.AddScraper(ScraperFactory.Create(job, _repository));
        }

        private void DisableDbButtons() { btnStartScheduler.Enabled = false; btnStopScheduler.Enabled = false; btnManualScrape.Enabled = false; }
        private void EnableDbButtons() { btnStartScheduler.Enabled = true; btnStopScheduler.Enabled = true; btnManualScrape.Enabled = true; }

        // ====================================================================
        // EVENT WIRING
        // ====================================================================
        private void WireUpEvents()
        {
            // Navigation
            navDashboard.Click += (s, e) => SwitchPanel(dashboardPanel, navDashboard);
            navScheduling.Click += (s, e) => SwitchPanel(schedulingPanel, navScheduling);
            navBulkScraper.Click += (s, e) => SwitchPanel(bulkScraperPanel, navBulkScraper);
            navCleanData.Click += (s, e) => SwitchPanel(cleanDataPanel, navCleanData);
            navHistory.Click += (s, e) => SwitchPanel(historyPanel, navHistory);
            navSettings.Click += (s, e) => SwitchPanel(settingsPanel, navSettings);

            // IsActive toggle for scheduling grid
            dgvScheduling.CellClick += async (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvScheduling.Columns[e.ColumnIndex].Name != "IsActive") return;
                try
                {
                    int id = (int)dgvScheduling.Rows[e.RowIndex].Cells["Id"].Value;
                    bool current = Convert.ToBoolean(dgvScheduling.Rows[e.RowIndex].Cells["IsActive"].Value);
                    string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                    using var conn = new SqlConnection(connStr); await conn.OpenAsync();
                    using var cmd = new SqlCommand("UPDATE ScrapeTargets SET IsActive = @Val WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Val", !current ? 1 : 0); cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();
                    await RefreshSchedulingAsync(); await LoadSchedulerScrapersAsync();
                }
                catch (Exception ex) { MessageBox.Show($"Toggle failed: {ex.Message}", "Error"); }
            };
            // IsActive toggle for bulk grid
            dgvBulkTargets.CellClick += async (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgvBulkTargets.Columns[e.ColumnIndex].Name != "IsActive") return;
                try
                {
                    int id = (int)dgvBulkTargets.Rows[e.RowIndex].Cells["Id"].Value;
                    bool current = Convert.ToBoolean(dgvBulkTargets.Rows[e.RowIndex].Cells["IsActive"].Value);
                    string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                    using var conn = new SqlConnection(connStr); await conn.OpenAsync();
                    using var cmd = new SqlCommand("UPDATE ScrapeTargets SET IsActive = @Val WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Val", !current ? 1 : 0); cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync(); await RefreshBulkAsync();
                }
                catch (Exception ex) { MessageBox.Show($"Toggle failed: {ex.Message}", "Error"); }
            };

            // Header buttons
            btnStartScheduler.Click += async (s, e) =>
            {
                try
                {
                    // Ensure latest scheduler scrapers are loaded before starting
                    await LoadSchedulerScrapersAsync();
                    _scheduler.Start();
                    UpdateSchedulerStatus(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to start scheduler: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            btnStopScheduler.Click += (s, e) => { _scheduler.Stop(); UpdateSchedulerStatus(false); };
            btnManualScrape.Click += BtnManualScrape_Click;

            // Scheduling CRUD
            btnAddScheduleTarget.Click += async (s, e) => { using var f = new AddScheduleTargetForm(); if (f.ShowDialog() == DialogResult.OK) { await RefreshSchedulingAsync(); await LoadSchedulerScrapersAsync(); } };
            btnEditTarget.Click += BtnEditTarget_Click;
            btnDeleteTarget.Click += BtnDeleteTarget_Click;

            // Bulk CRUD
            btnAddBulkTarget.Click += (s, e) => { using var f = new AddBulkTargetForm(); if (f.ShowDialog() == DialogResult.OK) { _ = RefreshBulkAsync(); } };
            btnEditBulkTarget.Click += BtnEditBulkTarget_Click;
            btnDeleteBulkTarget.Click += BtnDeleteBulkTarget_Click;
            btnStartBulkScrape.Click += BtnStartBulkScrape_Click;
            btnCancelBulkScrape.Click += (s, e) => _bulkCts?.Cancel();

            // History
            btnRefreshHistory.Click += (s, e) => _ = RefreshHistoryAsync();
            btnExportCSV.Click += BtnExportCSV_Click;
            cmbFilterTarget.SelectedIndexChanged += CmbFilterTarget_SelectedIndexChanged;

            // Clean Data buttons
            btnCleanSchedRefresh.Click += (s, e) => RegisterSchedButtons();
            btnCleanSchedExport.Click += (s, e) => { var dt = dgvCleanSched.DataSource as DataTable; if (dt != null) CsvExporter.ExportToCSV(dt, _activeCleanSchedTable ?? "sched_data"); else MessageBox.Show("No data to export."); };
            btnCleanSchedDelete.Click += BtnCleanSchedDelete_Click;
            btnCleanBulkRefresh.Click += (s, e) => RegisterBulkButtons();
            btnCleanBulkExport.Click += (s, e) => { var dt = dgvCleanBulk.DataSource as DataTable; if (dt != null) CsvExporter.ExportToCSV(dt, _activeCleanBulkTable ?? "bulk_data"); else MessageBox.Show("No data to export."); };
            btnCleanBulkDelete.Click += BtnCleanBulkDelete_Click;

            // Settings
            btnTestConnection.Click += BtnTestConnection_Click;
            btnSaveSettings.Click += BtnSaveSettings_Click;
            btnFactoryReset.Click += BtnFactoryReset_Click;

            // Form closing
            this.FormClosing += (s, e) => { _scheduler.Stop(); _bulkCts?.Cancel(); };
        }

        // ====================================================================
        // NAVIGATION
        // ====================================================================
        private void SwitchPanel(Panel target, Button navBtn)
        {
            foreach (var p in new[] { dashboardPanel, schedulingPanel, bulkScraperPanel, cleanDataPanel, historyPanel, settingsPanel })
                p.Visible = false;
            target.Visible = true;

            _activeNavButton.BackColor = ColorTranslator.FromHtml("#333333");
            _activeNavButton.ForeColor = Color.White;
            navBtn.BackColor = ColorTranslator.FromHtml("#39FF14");
            navBtn.ForeColor = ColorTranslator.FromHtml("#2B2B2B");
            _activeNavButton = navBtn;

            if (AppSettings.IsConfigured)
            {
                if (target == dashboardPanel) _ = RefreshDashboardAsync();
                else if (target == schedulingPanel) _ = RefreshSchedulingAsync();
                else if (target == bulkScraperPanel) _ = RefreshBulkAsync();
                else if (target == cleanDataPanel) { RegisterSchedButtons(); RegisterBulkButtons(); }
                else if (target == historyPanel) _ = RefreshHistoryAsync();
            }
        }

        // ====================================================================
        // SCHEDULER EVENT
        // ====================================================================
        private void OnSchedulerTickCompleted(string summary)
        {
            try { this.Invoke((Action)(() => { lblLastTick.Text = $"Last Tick: {DateTime.Now:HH:mm:ss}"; _ = RefreshAllAsync(); })); }
            catch (InvalidOperationException) { }
        }

        // ====================================================================
        // MANUAL SCRAPE
        // ====================================================================
        private async void BtnManualScrape_Click(object? sender, EventArgs e)
        {
            btnManualScrape.Enabled = false; btnManualScrape.Text = "Scraping...";
            try { await _scheduler.ManualTriggerAsync(); MessageBox.Show("Manual scrape completed!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception ex) { MessageBox.Show($"Scrape failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { btnManualScrape.Enabled = true; btnManualScrape.Text = "Manual Scrape"; }
        }

        // ====================================================================
        // SCHEDULING CRUD
        // ====================================================================
        private async void BtnEditTarget_Click(object? sender, EventArgs e)
        {
            if (dgvScheduling.CurrentRow == null) { MessageBox.Show("Select a target first."); return; }
            int id = (int)dgvScheduling.CurrentRow.Cells["Id"].Value;
            using var f = new EditTargetForm(id);
            if (f.ShowDialog() == DialogResult.OK)
            {
                await RefreshSchedulingAsync();
                await LoadSchedulerScrapersAsync();
            }
        }

        private async void BtnDeleteTarget_Click(object? sender, EventArgs e)
        {
            if (dgvScheduling.CurrentRow == null) return;
            int id = (int)dgvScheduling.CurrentRow.Cells["Id"].Value;
            string label = dgvScheduling.CurrentRow.Cells["Label"].Value?.ToString() ?? "";
            if (MessageBox.Show($"Delete target '{label}' and all its data?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                // Try to drop the clean table
                string sanitized = SqlDataRepository.SanitizeLabel(label);
                try { await _repository.ExecuteDDLAsync($"DROP TABLE IF EXISTS Clean_Sched_{sanitized}"); } catch { }
                using var conn = new SqlConnection(AppSettings.LoadConnectionString()); await conn.OpenAsync();
                using var cmd = new SqlCommand("DELETE FROM ScrapeTargets WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id); await cmd.ExecuteNonQueryAsync();
                await RefreshSchedulingAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Delete failed: {ex.Message}", "Error"); }
        }

        // ====================================================================
        // BULK CRUD (Edit + Delete)
        // ====================================================================
        private void BtnEditBulkTarget_Click(object? sender, EventArgs e)
        {
            if (dgvBulkTargets.CurrentRow == null) { MessageBox.Show("Select a bulk target first."); return; }
            int id = (int)dgvBulkTargets.CurrentRow.Cells["Id"].Value;
            using var f = new EditTargetForm(id);
            if (f.ShowDialog() == DialogResult.OK) _ = RefreshBulkAsync();
        }

        private async void BtnDeleteBulkTarget_Click(object? sender, EventArgs e)
        {
            if (dgvBulkTargets.CurrentRow == null) return;
            int id = (int)dgvBulkTargets.CurrentRow.Cells["Id"].Value;
            string label = dgvBulkTargets.CurrentRow.Cells["Label"].Value?.ToString() ?? "";
            if (MessageBox.Show($"Delete bulk target '{label}' and all its data?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                string sanitized = SqlDataRepository.SanitizeLabel(label);
                try { await _repository.ExecuteDDLAsync($"DROP TABLE IF EXISTS Clean_Bulk_{sanitized}"); } catch { }
                using var conn = new SqlConnection(AppSettings.LoadConnectionString()); await conn.OpenAsync();
                using var cmd = new SqlCommand("DELETE FROM ScrapeTargets WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id); await cmd.ExecuteNonQueryAsync();
                await RefreshBulkAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Delete failed: {ex.Message}", "Error"); }
        }

        // ====================================================================
        // BULK SCRAPE
        // ====================================================================
        private async void BtnStartBulkScrape_Click(object? sender, EventArgs e)
        {
            if (dgvBulkTargets.CurrentRow == null) { MessageBox.Show("Select a target first."); return; }
            int id = (int)dgvBulkTargets.CurrentRow.Cells["Id"].Value;
            _bulkCts = new CancellationTokenSource();
            btnStartBulkScrape.Enabled = false; btnCancelBulkScrape.Enabled = true;
            try
            {
                var jobs = await _repository.GetActiveJobsAsync("bulk");
                var job = jobs.FirstOrDefault(j => j.Id == id);
                if (job == null) { MessageBox.Show("Target not found."); return; }
                var scraper = ScraperFactory.Create(job, _repository) as BulkHtmlScraper;
                if (scraper == null) { MessageBox.Show("Invalid scraper type."); return; }
                scraper.OnProgress += (msg) =>
                {
                    try
                    {
                        this.Invoke((Action)(() =>
                        {
                            lblBulkProgress.Text = msg;
                            if (job.MaxRecords.HasValue && job.MaxRecords.Value > 0)
                            {
                                var tokens = msg.Split(' ');
                                int recordCount = 0;
                                foreach (var token in tokens)
                                    if (int.TryParse(token, out int parsed)) { recordCount = parsed; break; }
                                progressBarBulk.Value = Math.Min(100, (int)(100.0 * recordCount / job.MaxRecords.Value));
                            }
                        }));
                    }
                    catch { }
                };
                await scraper.RunAsync(_bulkCts.Token);
                MessageBox.Show("Bulk scrape complete!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { MessageBox.Show("Bulk scrape cancelled."); }
            catch (Exception ex) { MessageBox.Show($"Bulk scrape error: {ex.Message}", "Error"); }
            finally { btnStartBulkScrape.Enabled = true; btnCancelBulkScrape.Enabled = false; _bulkCts?.Dispose(); _bulkCts = null; }
        }

        // ====================================================================
        // SETTINGS
        // ====================================================================
        private async void BtnTestConnection_Click(object? sender, EventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(BuildConnectionString()); await conn.OpenAsync();
                lblConnectionStatus.Text = "Connected successfully"; lblConnectionStatus.ForeColor = ColorTranslator.FromHtml("#39FF14");
            }
            catch (Exception ex) { lblConnectionStatus.Text = $"Failed: {ex.Message}"; lblConnectionStatus.ForeColor = ColorTranslator.FromHtml("#FF4444"); }
        }

        private void BtnSaveSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                AppSettings.SaveConnectionString(BuildConnectionString());
                MessageBox.Show("Settings saved securely!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                warningBanner.Visible = false; EnableDbButtons();
                _ = InitializeAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnFactoryReset_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("This will clear all saved settings (connection string).\nThe application will restart in unconfigured state.\n\nContinue?",
                "Factory Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                _scheduler.Stop();
                AppSettings.ClearSettings();
                MessageBox.Show("Settings cleared. The application will now close.\nPlease restart and reconfigure.", "Factory Reset Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            }
            catch (Exception ex) { MessageBox.Show($"Reset failed: {ex.Message}", "Error"); }
        }

        private string BuildConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = txtServerName.Text.Trim(),
                InitialCatalog = txtDatabaseName.Text.Trim(),
                TrustServerCertificate = true, Encrypt = false, ConnectTimeout = 10
            };
            if (rbWindowsAuth.Checked) builder.IntegratedSecurity = true;
            else { builder.IntegratedSecurity = false; builder.UserID = txtSqlUsername.Text.Trim(); builder.Password = txtSqlPassword.Text; }
            return builder.ConnectionString;
        }

        // ====================================================================
        // HISTORY (shows BOTH scheduling AND bulk data)
        // ====================================================================
        private async void CmbFilterTarget_SelectedIndexChanged(object? sender, EventArgs e)
        {
            try
            {
                if (cmbFilterTarget.SelectedItem is ScrapeJob job)
                    dgvHistory.DataSource = await _repository.GetHistoryAsync(job.Id);
                else
                    dgvHistory.DataSource = await _repository.GetHistoryAsync();
            }
            catch { }
        }

        private void BtnExportCSV_Click(object? sender, EventArgs e)
        {
            var dt = dgvHistory.DataSource as DataTable;
            if (dt != null) CsvExporter.ExportToCSV(dt, "history");
            else MessageBox.Show("No data to export.");
        }

        // ====================================================================
        // CLEAN DATA - Delete records
        // ====================================================================
        private async void BtnCleanSchedDelete_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_activeCleanSchedTable)) { MessageBox.Show("Select a table first."); return; }
            if (MessageBox.Show($"Delete ALL records from '{_activeCleanSchedTable}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                using var conn = new SqlConnection(connStr); await conn.OpenAsync();
                using var cmd = new SqlCommand($"DELETE FROM {_activeCleanSchedTable}", conn);
                await cmd.ExecuteNonQueryAsync();
                dgvCleanSched.DataSource = await _repository.GetCleanTableAsync(_activeCleanSchedTable);
                MessageBox.Show("Records deleted.", "Done");
            }
            catch (Exception ex) { MessageBox.Show($"Delete failed: {ex.Message}"); }
        }

        private async void BtnCleanBulkDelete_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_activeCleanBulkTable)) { MessageBox.Show("Select a table first."); return; }
            if (MessageBox.Show($"Delete ALL records from '{_activeCleanBulkTable}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                using var conn = new SqlConnection(connStr); await conn.OpenAsync();
                using var cmd = new SqlCommand($"DELETE FROM {_activeCleanBulkTable}", conn);
                await cmd.ExecuteNonQueryAsync();
                dgvCleanBulk.DataSource = await _repository.GetCleanTableAsync(_activeCleanBulkTable);
                MessageBox.Show("Records deleted.", "Done");
            }
            catch (Exception ex) { MessageBox.Show($"Delete failed: {ex.Message}"); }
        }

        // ====================================================================
        // DYNAMIC CLEAN DATA BUTTONS
        // ====================================================================
        public async void RegisterBulkButtons()
        {
            foreach (var ctrl in flpBulkButtons.Controls.Cast<Control>().Where(c => c.Tag?.ToString() == "dynamic_bulk").ToList())
                flpBulkButtons.Controls.Remove(ctrl);
            try
            {
                if (!AppSettings.IsConfigured) return;
                var jobs = await _repository.GetAllJobsByTypeAsync("bulk");
                foreach (var job in jobs)
                {
                    string tableName = $"Clean_Bulk_{SqlDataRepository.SanitizeLabel(job.Label)}";
                    var btn = new Button
                    {
                        Text = job.Label, Tag = "dynamic_bulk",
                        BackColor = ColorTranslator.FromHtml("#1E90FF"), ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                        Size = new Size(170, 30), Cursor = Cursors.Hand
                    };
                    btn.FlatAppearance.BorderSize = 0;
                    string tbl = tableName;
                    btn.Click += async (s, e) =>
                    {
                        try { _activeCleanBulkTable = tbl; dgvCleanBulk.DataSource = await _repository.GetCleanTableAsync(tbl); }
                        catch (Exception ex) { MessageBox.Show($"Load failed: {ex.Message}"); }
                    };
                    flpBulkButtons.Controls.Add(btn);
                }
            }
            catch { }
        }

        public async void RegisterSchedButtons()
        {
            foreach (var ctrl in flpSchedButtons.Controls.Cast<Control>().Where(c => c.Tag?.ToString() == "dynamic_sched").ToList())
                flpSchedButtons.Controls.Remove(ctrl);
            try
            {
                if (!AppSettings.IsConfigured) return;
                var jobs = await _repository.GetAllJobsByTypeAsync("scheduling");
                foreach (var job in jobs)
                {
                    string tableName = $"Clean_Sched_{SqlDataRepository.SanitizeLabel(job.Label)}";
                    var btn = new Button
                    {
                        Text = job.Label, Tag = "dynamic_sched",
                        BackColor = ColorTranslator.FromHtml("#39FF14"), ForeColor = ColorTranslator.FromHtml("#2B2B2B"),
                        FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                        Size = new Size(170, 30), Cursor = Cursors.Hand
                    };
                    btn.FlatAppearance.BorderSize = 0;
                    string tbl = tableName;
                    btn.Click += async (s, e) =>
                    {
                        try { _activeCleanSchedTable = tbl; dgvCleanSched.DataSource = await _repository.GetCleanTableAsync(tbl); }
                        catch (Exception ex) { MessageBox.Show($"Load failed: {ex.Message}"); }
                    };
                    flpSchedButtons.Controls.Add(btn);
                }
            }
            catch { }
        }

        // ====================================================================
        // DATA REFRESH
        // ====================================================================
        private async Task RefreshAllAsync() { try { await RefreshDashboardAsync(); } catch { } }

        private async Task RefreshDashboardAsync()
        {
            try
            {
                var stats = await _repository.GetSummaryStatsAsync();
                lblTotalTargets.Text = stats["TotalTargets"]?.ToString() ?? "0";
                lblTotalRecords.Text = stats["TotalRecords"]?.ToString() ?? "0";
                lblLastRun.Text = stats["LastRunTime"]?.ToString() ?? "Never";
                lblErrorCount.Text = stats["ErrorCount"]?.ToString() ?? "0";
            }
            catch { }
        }

        private async Task RefreshSchedulingAsync()
        {
            try { dgvScheduling.DataSource = await _repository.GetAllJobsByTypeAsync("scheduling"); } catch { }
        }

        private async Task RefreshBulkAsync()
        {
            try { dgvBulkTargets.DataSource = await _repository.GetAllJobsByTypeAsync("bulk"); } catch { }
        }

        private async Task RefreshHistoryAsync()
        {
            try
            {
                var jobs = await _repository.GetAllJobsAsync();
                cmbFilterTarget.Items.Clear();
                // Show job labels instead of type names
                cmbFilterTarget.DisplayMember = "Label";
                cmbFilterTarget.ValueMember = "Id";
                cmbFilterTarget.Items.Add("All Targets");
                foreach (var j in jobs) cmbFilterTarget.Items.Add(j);
                cmbFilterTarget.SelectedIndex = 0;
                dgvHistory.DataSource = await _repository.GetHistoryAsync();
            }
            catch { }
        }

        // ====================================================================
        // STATUS BAR
        // ====================================================================
        private void UpdateSchedulerStatus(bool running)
        {
            picStatus.BackColor = running ? ColorTranslator.FromHtml("#39FF14") : ColorTranslator.FromHtml("#FF4444");
            lblSchedulerStatus.Text = running ? "Running" : "Stopped";
            lblSchedulerStatus.ForeColor = running ? ColorTranslator.FromHtml("#39FF14") : ColorTranslator.FromHtml("#FF4444");
            picStatus.Invalidate();
        }
    }
}
