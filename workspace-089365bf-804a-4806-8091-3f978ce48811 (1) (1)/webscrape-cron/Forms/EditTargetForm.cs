// ========== FILE: Forms/EditTargetForm.cs ==========
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using WebScrapeCron.Config;
using WebScrapeCron.Data;
using WebScrapeCron.Models;

namespace WebScrapeCron.Forms
{
    /* ========================================================================
     * CLASS: EditTargetForm
     * ========================================================================
     * OOP PRINCIPLE: SINGLE RESPONSIBILITY
     * Loads existing field mappings for a target, allows editing NodePath
     * values, and supports adding new fields (with ALTER TABLE).
     * Cannot rename/delete fields that already have data in the clean table.
     * ======================================================================== */

    public class EditTargetForm : Form
    {
        private readonly int _targetId;
        private FlowLayoutPanel flpFields;
        private List<FieldMapping> _mappings = new();
        private ScrapeJob? _job;

        public EditTargetForm(int targetId)
        {
            _targetId = targetId;
            this.Text = "Edit Target";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ColorTranslator.FromHtml("#2B2B2B");
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var lblTitle = new Label { Text = $"Editing Target ID: {_targetId}", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#39FF14"), Location = new Point(15, 10), AutoSize = true };
            this.Controls.Add(lblTitle);

            flpFields = new FlowLayoutPanel { Location = new Point(15, 45), Size = new Size(540, 340), AutoScroll = true, BackColor = ColorTranslator.FromHtml("#3C3C3C"), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            this.Controls.Add(flpFields);

            var btnAddField = new Button { Text = "+ Add Field", Location = new Point(15, 400), Size = new Size(110, 30), BackColor = ColorTranslator.FromHtml("#1E90FF"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnAddField.FlatAppearance.BorderSize = 0;
            btnAddField.Click += async (s, e) => await BtnAddField_ClickAsync();

            var btnSave = new Button { Text = "Save Changes", Location = new Point(350, 400), Size = new Size(110, 30), BackColor = ColorTranslator.FromHtml("#39FF14"), ForeColor = ColorTranslator.FromHtml("#2B2B2B"), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button { Text = "Cancel", Location = new Point(470, 400), Size = new Size(80, 30), BackColor = ColorTranslator.FromHtml("#888888"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(flpFields);
            this.Controls.Add(btnAddField);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            this.Shown += async (s, e) => await LoadMappingsAsync();
        }

        private async Task LoadMappingsAsync()
        {
            try
            {
                var repo = new SqlDataRepository();
                _mappings = await repo.GetFieldMappingsAsync(_targetId);
                var jobs = await repo.GetAllJobsAsync();
                _job = jobs.FirstOrDefault(j => j.Id == _targetId);
                await RenderFieldsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed: {ex.Message}");
            }
        }

        private async Task RenderFieldsAsync()
        {
            flpFields.Controls.Clear();
            string sanitized = SqlDataRepository.SanitizeLabel(_job?.Label ?? "Unknown");
            string tableName = _job?.PortionType == "bulk" ? $"Clean_Bulk_{sanitized}" : $"Clean_Sched_{sanitized}";

            bool tableHasData = false;
            try
            {
                var repo = new SqlDataRepository();
                var dt = await repo.GetCleanTableAsync(tableName);
                tableHasData = dt.Rows.Count > 0;
            }
            catch { }

            foreach (var m in _mappings)
            {
                var panel = new Panel { Size = new Size(520, 35), BackColor = ColorTranslator.FromHtml("#3C3C3C") };
                var lblName = new Label { Text = m.FieldName, Location = new Point(5, 8), Size = new Size(120, 22), ForeColor = tableHasData ? Color.Gray : Color.White, AutoSize = false };
                var txtPath = new TextBox { Text = m.NodePath, Location = new Point(130, 5), Size = new Size(300, 26), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Tag = m.Id };
                var lblStatus = new Label { Text = tableHasData ? "has data" : "", Location = new Point(440, 8), ForeColor = ColorTranslator.FromHtml("#FF8888"), AutoSize = true, Font = new Font("Segoe UI", 8) };
                panel.Controls.AddRange(new Control[] { lblName, txtPath, lblStatus });
                flpFields.Controls.Add(panel);
            }
        }

        private async Task BtnAddField_ClickAsync()
        {
            using var inputForm = new Form
            {
                Text = "Add Field",
                Size = new Size(400, 200),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = ColorTranslator.FromHtml("#2B2B2B"),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            inputForm.Controls.Add(new Label { Text = "Field Name:", ForeColor = Color.White, Location = new Point(15, 15), AutoSize = true });
            var txtName = new TextBox { Location = new Point(15, 37), Size = new Size(350, 28), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            inputForm.Controls.Add(txtName);

            inputForm.Controls.Add(new Label { Text = "XPath/JSONPath:", ForeColor = Color.White, Location = new Point(15, 75), AutoSize = true });
            var txtPath = new TextBox { Location = new Point(15, 97), Size = new Size(350, 28), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            inputForm.Controls.Add(txtPath);

            var btnOk = new Button { Text = "Add", Location = new Point(260, 130), Size = new Size(80, 30), BackColor = ColorTranslator.FromHtml("#39FF14"), ForeColor = ColorTranslator.FromHtml("#2B2B2B"), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            btnOk.FlatAppearance.BorderSize = 0;
            inputForm.Controls.Add(btnOk);
            inputForm.AcceptButton = btnOk;

            if (inputForm.ShowDialog() == DialogResult.OK)
            {
                string fieldName = txtName.Text.Trim();
                string fieldPath = txtPath.Text.Trim();
                if (string.IsNullOrEmpty(fieldName)) { MessageBox.Show("Name required."); return; }

                try
                {
                    string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                    using var conn = new SqlConnection(connStr);
                    conn.Open();

                    // Insert mapping
                    int maxOrder = _mappings.Count > 0 ? _mappings.Max(m => m.FieldOrder) + 1 : 1;
                    using (var cmd = new SqlCommand(
                        "INSERT INTO FieldMappings (TargetId, FieldName, NodePath, FieldOrder) VALUES (@Tid, @Name, @Path, @Ord)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Tid", _targetId);
                        cmd.Parameters.AddWithValue("@Name", fieldName);
                        cmd.Parameters.AddWithValue("@Path", fieldPath);
                        cmd.Parameters.AddWithValue("@Ord", maxOrder);
                        cmd.ExecuteNonQuery();
                    }

                    // ALTER TABLE to add column
                    string sanitized = SqlDataRepository.SanitizeLabel(_job?.Label ?? "Unknown");
                    string tableName = _job?.PortionType == "bulk" ? $"Clean_Bulk_{sanitized}" : $"Clean_Sched_{sanitized}";
                    try
                    {
                        var repo = new SqlDataRepository();
                        await repo.ExecuteDDLAsync($"ALTER TABLE {tableName} ADD {fieldName} NVARCHAR(500) NULL");
                    }
                    catch (SqlException ex) when (ex.Message.Contains("already exists") || ex.Number == 2705)
                    {
                        MessageBox.Show($"Column '{fieldName}' already exists in the table. Mapping added but column was not re-created.", "Note");
                    }

                    await LoadMappingsAsync();
                }
                catch (Exception ex) { MessageBox.Show($"Add field failed: {ex.Message}"); }
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                using var conn = new SqlConnection(connStr);
                conn.Open();

                foreach (var ctrl in flpFields.Controls)
                {
                    if (ctrl is Panel p)
                    {
                        var txtPath = p.Controls.Cast<Control>().FirstOrDefault(c => c.Tag is int) as TextBox;
                        if (txtPath != null && int.TryParse(txtPath.Tag?.ToString(), out int mappingId))
                        {
                            using var cmd = new SqlCommand(
                                "UPDATE FieldMappings SET NodePath = @Path WHERE Id = @Id", conn);
                            cmd.Parameters.AddWithValue("@Path", txtPath.Text.Trim());
                            cmd.Parameters.AddWithValue("@Id", mappingId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                this.DialogResult = DialogResult.OK;
                MessageBox.Show("Changes saved!", "Success");
            }
            catch (Exception ex) { MessageBox.Show($"Save failed: {ex.Message}"); }
        }
    }
}
