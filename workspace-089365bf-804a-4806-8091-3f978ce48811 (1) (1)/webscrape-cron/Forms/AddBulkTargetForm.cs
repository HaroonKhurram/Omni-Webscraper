// ========== FILE: Forms/AddBulkTargetForm.cs ==========
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
     * CLASS: AddBulkTargetForm
     * ========================================================================
     * OOP PRINCIPLE: SINGLE RESPONSIBILITY
     * This form handles creation of bulk scraping targets with pagination
     * config, row XPath, and column definitions. Creates the target,
     * field mappings, and Clean_Bulk_[Label] table.
     * ======================================================================== */

    public class AddBulkTargetForm : Form
    {
        private TextBox txtLabel, txtUrl, txtPaginationXPath, txtRowXPath;
        private NumericUpDown nudMaxRecords, nudFieldCount;
        private FlowLayoutPanel flpFields;
        private Button btnSave, btnCancel;

        public AddBulkTargetForm()
        {
            this.Text = "Add Bulk Target";
            this.Size = new Size(660, 720);  // Taller so Save/Cancel buttons are always visible
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = ColorTranslator.FromHtml("#2B2B2B");
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int y = 15;
            AddLbl("Label (alphanumeric + underscores):", 15, y); y += 22;
            txtLabel = AddTb(15, y, 580); y += 35;
            AddLbl("Start URL:", 15, y); y += 22;
            txtUrl = AddTb(15, y, 580); y += 35;
            AddLbl("Pagination Next Link XPath:", 15, y); y += 22;
            txtPaginationXPath = AddTb(15, y, 580); y += 35;
            AddLbl("Row XPath (selects each item on page):", 15, y); y += 22;
            txtRowXPath = AddTb(15, y, 580); y += 35;
            AddLbl("Max Records:", 15, y);
            nudMaxRecords = new NumericUpDown { Location = new Point(120, y - 2), Size = new Size(80, 28), Minimum = 100, Maximum = 50000, Value = 1000, Increment = 100, BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            this.Controls.Add(nudMaxRecords);
            y += 35;
            AddLbl("Number of columns:", 15, y);
            nudFieldCount = new NumericUpDown { Location = new Point(160, y - 2), Size = new Size(60, 28), Minimum = 1, Maximum = 20, Value = 3, BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White };
            nudFieldCount.ValueChanged += (s, e) => GenerateFieldRows();
            this.Controls.Add(nudFieldCount);
            y += 35;
            AddLbl("Column Definitions (Name + XPath relative to row):", 15, y); y += 22;
            flpFields = new FlowLayoutPanel { Location = new Point(15, y), Size = new Size(600, 180), AutoScroll = true, BackColor = ColorTranslator.FromHtml("#3C3C3C"), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            this.Controls.Add(flpFields);
            y += 195;

            btnSave = new Button { Text = "Save", Location = new Point(390, y), Size = new Size(90, 32), BackColor = ColorTranslator.FromHtml("#39FF14"), ForeColor = ColorTranslator.FromHtml("#2B2B2B"), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button { Text = "Cancel", Location = new Point(490, y), Size = new Size(90, 32), BackColor = ColorTranslator.FromHtml("#888888"), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
            btnCancel.FlatAppearance.BorderSize = 0;

            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
            GenerateFieldRows();
        }

        private void AddLbl(string text, int x, int y) =>
            this.Controls.Add(new Label { Text = text, Font = new Font("Segoe UI", 9), ForeColor = Color.White, Location = new Point(x, y), AutoSize = true });

        private TextBox AddTb(int x, int y, int w)
        {
            var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(tb);
            return tb;
        }

        private void GenerateFieldRows()
        {
            flpFields.Controls.Clear();
            for (int i = 0; i < nudFieldCount.Value; i++)
            {
                var p = new Panel { Size = new Size(560, 35), BackColor = ColorTranslator.FromHtml("#3C3C3C") };
                p.Controls.Add(new Label { Text = $"{i + 1}:", Location = new Point(5, 8), AutoSize = true, ForeColor = Color.White });
                var nameTb = new TextBox { Location = new Point(30, 5), Size = new Size(140, 26), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Tag = "colName" };
                var pathTb = new TextBox { Location = new Point(180, 5), Size = new Size(370, 26), BackColor = ColorTranslator.FromHtml("#4A4A4A"), ForeColor = Color.White, Tag = "colPath" };
                p.Controls.AddRange(new Control[] { nameTb, pathTb });
                flpFields.Controls.Add(p);
            }
        }

        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            btnSave.Enabled = false;
            btnSave.Text = "Saving...";
            string label = txtLabel.Text.Trim();
            if (string.IsNullOrEmpty(label) || !Regex.IsMatch(label, @"^[A-Za-z0-9_]+$"))
            { MessageBox.Show("Label must be alphanumeric + underscores.", "Validation"); this.DialogResult = DialogResult.None; return; }
            if (string.IsNullOrEmpty(txtUrl.Text.Trim()))
            { MessageBox.Show("URL is required.", "Validation"); this.DialogResult = DialogResult.None; return; }

            var fields = new List<(string Name, string Path)>();
            foreach (var ctrl in flpFields.Controls)
            {
                if (ctrl is Panel p)
                {
                    var n = p.Controls.Cast<Control>().FirstOrDefault(c => c.Tag?.ToString() == "colName") as TextBox;
                    var x = p.Controls.Cast<Control>().FirstOrDefault(c => c.Tag?.ToString() == "colPath") as TextBox;
                    if (n != null && !string.IsNullOrEmpty(n.Text.Trim()))
                        fields.Add((n.Text.Trim(), x?.Text.Trim() ?? ""));
                }
            }

            try
            {
                string connStr = AppSettings.LoadConnectionString() ?? throw new Exception("DB not configured");
                using var conn = new SqlConnection(connStr);
                conn.Open();

                int targetId;
                using (var cmd = new SqlCommand(
                    @"INSERT INTO ScrapeTargets (Url, Label, Frequency, IsActive, SourceType, PaginationNextLinkXPath, MaxRecords, PortionType, RowXPath)
                      VALUES (@Url, @Label, 'daily', 1, 'html', @PagXpath, @Max, 'bulk', @RowXpath);
                      SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@Url", txtUrl.Text.Trim());
                    cmd.Parameters.AddWithValue("@Label", label);
                    cmd.Parameters.AddWithValue("@PagXpath", (object?)txtPaginationXPath.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Max", (int)nudMaxRecords.Value);
                    cmd.Parameters.AddWithValue("@RowXpath", (object?)txtRowXPath.Text.Trim() ?? DBNull.Value);
                    targetId = Convert.ToInt32(cmd.ExecuteScalar());
                }

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

                string sanitized = SqlDataRepository.SanitizeLabel(label);
                string tableName = $"Clean_Bulk_{sanitized}";
                var columns = string.Join(", ", fields.Select(f => $"{f.Name} NVARCHAR(1000) NULL"));
                string ddl = $"CREATE TABLE {tableName} (Id INT IDENTITY(1,1) PRIMARY KEY, TargetId INT NOT NULL, ScrapedAt DATETIME2 NOT NULL, {columns})";
                await new SqlDataRepository().ExecuteDDLAsync(ddl);

                MessageBox.Show("Bulk target created!", "Success");
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
