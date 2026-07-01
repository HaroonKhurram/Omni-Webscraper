// ========== FILE: Forms/AddScheduleTargetForm.cs ==========
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using WebScrapeCron.Config;
using WebScrapeCron.Data;

namespace WebScrapeCron.Forms
{
    /* ========================================================================
     * CLASS: AddScheduleTargetForm
     * ========================================================================
     * OOP PRINCIPLE: SINGLE RESPONSIBILITY
     * This form is responsible ONLY for creating new scheduling targets.
     * It handles: input validation, label sanitization, dynamic field rows,
     * parameterized INSERT, and dynamic DDL table creation.
     *
     * DATA FLOW:
     * User fills form -> Validate -> Sanitize label
     *     -> INSERT into ScrapeTargets (parameterized)
     *     -> INSERT into FieldMappings (parameterized)
     *     -> CREATE TABLE Clean_Sched_[SanitizedLabel] (validated DDL)
     *
     * FIELDS:
     *   - Label, URL, SourceType (html/json), Frequency
     *   - HtmlNodePath (for html targets) / JsonPropertyPath (for json targets)
     *   - Dynamic field definitions (FieldName + XPath/JSONPath)
     *
     * REMOVED: PaginationNextLinkXPath, RowXPath, MaxRecords
     *   (these are only relevant for bulk scraping targets)
     * ======================================================================== */

    public class AddScheduleTargetForm : Form
    {
        private TextBox txtLabel, txtUrl, txtNodePath;
        private ComboBox cmbSourceType, cmbFrequency;
        private NumericUpDown nudFieldCount;
        private FlowLayoutPanel flpFields;
        private Button btnSave, btnCancel;
        private Label lblNodePath;

        public AddScheduleTargetForm()
        {
            this.Text = "Add Scheduling Target";
            this.Size = new Size(620, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ColorTranslator.FromHtml("#2B2B2B");
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int y = 15;
            AddLabel("Label (alphanumeric + underscores):", 15, y); y += 22;
            txtLabel = AddTextBox(15, y, 560); y += 35;
            AddLabel("URL:", 15, y); y += 22;
            txtUrl = AddTextBox(15, y, 560); y += 35;
            AddLabel("Source Type:", 15, y);
            cmbSourceType = new ComboBox { Location = new Point(120, y - 2), Size = new Size(120, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            cmbSourceType.Items.AddRange(new[] { "html", "json" });
            cmbSourceType.SelectedIndex = 0;
            cmbSourceType.SelectedIndexChanged += (s, e) => UpdateNodePathLabel();
            AddLabel("Frequency:", 260, y);
            cmbFrequency = new ComboBox { Location = new Point(360, y - 2), Size = new Size(120, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            cmbFrequency.Items.AddRange(new[] { "5min", "hourly", "daily", "weekly" });
            cmbFrequency.SelectedIndex = 2;
            y += 35;

            // HtmlNodePath / JsonPropertyPath (label changes based on SourceType)
            lblNodePath = new Label { Text = "Html Node Path (XPath):", Font = new Font("Segoe UI", 9), ForeColor = Color.White, Location = new Point(15, y), AutoSize = true };
            this.Controls.Add(lblNodePath);
            y += 22;
            txtNodePath = AddTextBox(15, y, 560);
            y += 35;

            AddLabel("Number of fields to track:", 15, y);
            nudFieldCount = new NumericUpDown { Location = new Point(220, y - 2), Size = new Size(60, 28), Minimum = 1, Maximum = 20, Value = 2, BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            nudFieldCount.ValueChanged += (s, e) => GenerateFieldRows();
            y += 35;
            AddLabel("Field Definitions:", 15, y); y += 22;
            flpFields = new FlowLayoutPanel { Location = new Point(15, y), Size = new Size(560, 230), AutoScroll = true, BackColor = ColorTranslator.FromHtml("#3C3C3C"), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            this.Controls.Add(flpFields);
            y += 240;

            btnSave = new Button { Text = "Save", Location = new Point(380, y), Size = new Size(90, 32), BackColor = ColorTranslator.FromHtml("#39FF14"), ForeColor = ColorTranslator.FromHtml("#2B2B2B"), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Location = new Point(480, y), Size = new Size(90, 32), BackColor = ColorTranslator.FromHtml("#888888"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10), DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(cmbSourceType);
            this.Controls.Add(cmbFrequency);
            this.Controls.Add(nudFieldCount);
            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);

            GenerateFieldRows();
        }

        private void UpdateNodePathLabel()
        {
            if (cmbSourceType.SelectedItem?.ToString() == "json")
                lblNodePath.Text = "Json Property Path (e.g. current):";
            else
                lblNodePath.Text = "Html Node Path (XPath):";
        }

        private void AddLabel(string text, int x, int y)
        {
            this.Controls.Add(new Label { Text = text, Font = new Font("Segoe UI", 9), ForeColor = Color.White, Location = new Point(x, y), AutoSize = true });
        }

        private TextBox AddTextBox(int x, int y, int w)
        {
            var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(tb);
            return tb;
        }

        private void GenerateFieldRows()
        {
            flpFields.Controls.Clear();
            int count = (int)nudFieldCount.Value;
            for (int i = 0; i < count; i++)
            {
                var panel = new Panel { Size = new Size(540, 35), BackColor = ColorTranslator.FromHtml("#3C3C3C") };
                var lblIdx = new Label { Text = $"{i + 1}:", Location = new Point(5, 8), AutoSize = true, ForeColor = Color.White };
                var txtName = new TextBox { Location = new Point(30, 5), Size = new Size(160, 26), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Tag = "fieldName" };
                var txtPath = new TextBox { Location = new Point(200, 5), Size = new Size(330, 26), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Tag = "fieldPath" };
                panel.Controls.AddRange(new Control[] { lblIdx, txtName, txtPath });
                flpFields.Controls.Add(panel);
            }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            btnSave.Enabled = false;
            btnSave.Text = "Saving...";
            // Validate
            string label = txtLabel.Text.Trim();
            if (string.IsNullOrEmpty(label) || !Regex.IsMatch(label, @"^[A-Za-z0-9_]+$"))
            {
                MessageBox.Show("Label must be alphanumeric with underscores only.", "Validation Error");
                btnSave.Enabled = true;
                btnSave.Text = "Save";
                this.DialogResult = DialogResult.None;
                return;
            }
            if (string.IsNullOrEmpty(txtUrl.Text.Trim()))
            {
                MessageBox.Show("URL is required.", "Validation Error");
                btnSave.Enabled = true;
                btnSave.Text = "Save";
                this.DialogResult = DialogResult.None;
                return;
            }

            // Collect field definitions
            var fields = new List<(string Name, string Path)>();
            foreach (var ctrl in flpFields.Controls)
            {
                if (ctrl is Panel p)
                {
                    var nameTb = p.Controls.Cast<Control>().FirstOrDefault(c => c.Tag?.ToString() == "fieldName") as TextBox;
                    var pathTb = p.Controls.Cast<Control>().FirstOrDefault(c => c.Tag?.ToString() == "fieldPath") as TextBox;
                    if (nameTb != null && pathTb != null && !string.IsNullOrEmpty(nameTb.Text.Trim()))
                        fields.Add((nameTb.Text.Trim(), pathTb.Text.Trim()));
                }
            }

            if (fields.Count == 0)
            {
                MessageBox.Show("At least one field definition is required.", "Validation Error");
                btnSave.Enabled = true;
                btnSave.Text = "Save";
                this.DialogResult = DialogResult.None;
                return;
            }

            try
            {
                string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                using var conn = new SqlConnection(connStr);
                conn.Open();

                string sourceType = cmbSourceType.SelectedItem?.ToString() ?? "html";
                string nodePath = txtNodePath.Text.Trim();

                // 1. Insert into ScrapeTargets with HtmlNodePath or JsonPropertyPath
                int targetId;
                using (var cmd = new SqlCommand(
                    @"INSERT INTO ScrapeTargets (Url, HtmlNodePath, JsonPropertyPath, Label, Frequency, IsActive, SourceType, PortionType)
                      VALUES (@Url, @HtmlNodePath, @JsonPropertyPath, @Label, @Freq, 1, @Src, 'scheduling');
                      SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@Url", txtUrl.Text.Trim());
                    cmd.Parameters.AddWithValue("@Label", label);
                    cmd.Parameters.AddWithValue("@Freq", cmbFrequency.SelectedItem?.ToString() ?? "daily");
                    cmd.Parameters.AddWithValue("@Src", sourceType);

                    // Set HtmlNodePath for html sources, JsonPropertyPath for json sources
                    if (sourceType == "json")
                    {
                        cmd.Parameters.AddWithValue("@HtmlNodePath", DBNull.Value);
                        cmd.Parameters.AddWithValue("@JsonPropertyPath", string.IsNullOrEmpty(nodePath) ? (object)DBNull.Value : nodePath);
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@HtmlNodePath", string.IsNullOrEmpty(nodePath) ? (object)DBNull.Value : nodePath);
                        cmd.Parameters.AddWithValue("@JsonPropertyPath", DBNull.Value);
                    }

                    targetId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // 2. Insert FieldMappings
                for (int i = 0; i < fields.Count; i++)
                {
                    using var cmd = new SqlCommand(
                        "INSERT INTO FieldMappings (TargetId, FieldName, NodePath, FieldOrder) VALUES (@Tid, @Name, @Path, @Ord)", conn);
                    cmd.Parameters.AddWithValue("@Tid", targetId);
                    cmd.Parameters.AddWithValue("@Name", fields[i].Name);
                    cmd.Parameters.AddWithValue("@Path", fields[i].Path);
                    cmd.Parameters.AddWithValue("@Ord", i + 1);
                    cmd.ExecuteNonQuery();
                }

                // 3. Create clean table
                string sanitizedLabel = SqlDataRepository.SanitizeLabel(label);
                string tableName = $"Clean_Sched_{sanitizedLabel}";
                var columns = string.Join(", ", fields.Select(f => $"{f.Name} NVARCHAR(500) NULL"));
                string ddl = $"CREATE TABLE {tableName} (Id INT IDENTITY(1,1) PRIMARY KEY, TargetId INT NOT NULL, DatePulled DATETIME2 NOT NULL, {columns})";
                var repo = new SqlDataRepository();
                await repo.ExecuteDDLAsync(ddl);

                MessageBox.Show("Target created successfully!", "Success");
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}", "Error");
                btnSave.Enabled = true;
                btnSave.Text = "Save";
            }
        }
    }
}
