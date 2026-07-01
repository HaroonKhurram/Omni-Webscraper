// ========== FILE: Utilities/CsvExporter.cs ==========
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WebScrapeCron.Utilities
{
    /* ========================================================================
     * STATIC CLASS: CsvExporter
     * ========================================================================
     * OOP PRINCIPLE: ENCAPSULATION (Utility/Helper Pattern)
     * Stateless utility class for CSV export. RFC 4180 compliant.
     * Handles commas, quotes, and line breaks in field values.
     * ======================================================================== */

    public static class CsvExporter
    {
        public static void ExportToCSV(DataTable data, string defaultName)
        {
            if (data == null || data.Rows.Count == 0)
            {
                MessageBox.Show("No data available to export.", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"{defaultName}_{DateTime.Now:yyyyMMdd_HHmmss}",
                Title = "Export Data to CSV"
            };

            if (saveDialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                using var writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8);

                // Header row
                string[] headers = new string[data.Columns.Count];
                for (int i = 0; i < data.Columns.Count; i++)
                    headers[i] = EscapeCsvField(data.Columns[i].ColumnName);
                writer.WriteLine(string.Join(",", headers));

                // Data rows
                foreach (DataRow row in data.Rows)
                {
                    string[] fields = new string[data.Columns.Count];
                    for (int i = 0; i < data.Columns.Count; i++)
                    {
                        object value = row[i];
                        string fieldValue = value == DBNull.Value ? string.Empty : value.ToString() ?? string.Empty;
                        fields[i] = EscapeCsvField(fieldValue);
                    }
                    writer.WriteLine(string.Join(",", fields));
                }

                MessageBox.Show($"Data exported successfully!\n\nFile: {saveDialog.FileName}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error:\n\n{ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}
