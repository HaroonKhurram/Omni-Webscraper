// ========== FILE: Data/SqlDataRepository.cs ==========
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using WebScrapeCron.Config;
using WebScrapeCron.Models;

namespace WebScrapeCron.Data
{
    /* ========================================================================
     * CLASS: SqlDataRepository
     * ========================================================================
     * OOP PRINCIPLES: ENCAPSULATION + ABSTRACTION (Interface Implementation)
     * -----------------------------------------------------------------------
     * Implements IDataRepository using raw ADO.NET (no Entity Framework).
     * All SQL operations use parameterized commands to prevent injection.
     * Connection strings come from AppSettings.LoadConnectionString()
     * (encrypted DPAPI storage) - NEVER hardcoded.
     *
     * DESIGN DECISIONS:
     * - Raw ADO.NET for max performance and transparency
     * - DDL validation: table names must match ^[A-Za-z0-9_]+$
     * - ETL: ProcessStagingDataAsync() transforms staging data to Clean_ tables
     * - All async operations use SqlDataReader for streaming results
     * ======================================================================== */

    public class SqlDataRepository : IDataRepository
    {
        // Connection string loaded from encrypted storage on every call
        private string GetConnectionString()
        {
            string? connStr = AppSettings.LoadConnectionString();
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException(
                    "Database connection not configured. Go to Settings tab.");
            return connStr;
        }

        // DDL validation: only allow alphanumeric + underscore table names
        private static readonly Regex SafeNameRegex = new(@"^[A-Za-z0-9_]+$");

        private void ValidateTableName(string tableName)
        {
            if (!SafeNameRegex.IsMatch(tableName))
                throw new ArgumentException(
                    $"Invalid table name '{tableName}'. Only alphanumeric and underscore allowed.");
        }

        // ====================================================================
        // GetActiveJobsAsync
        // ====================================================================
        public async Task<List<ScrapeJob>> GetActiveJobsAsync(string? portionType = null)
        {
            var jobs = new List<ScrapeJob>();
            using var connection = new SqlConnection(GetConnectionString());

            string sql = @"SELECT Id, Url, HtmlNodePath, JsonPropertyPath, Label,
                                  Frequency, IsActive, SourceType, PaginationNextLinkXPath,
                                  MaxRecords, PortionType, RowXPath, CreatedAt
                           FROM ScrapeTargets WHERE IsActive = 1";

            if (!string.IsNullOrEmpty(portionType))
                sql += " AND PortionType = @PortionType";

            using var command = new SqlCommand(sql, connection);
            if (!string.IsNullOrEmpty(portionType))
                command.Parameters.AddWithValue("@PortionType", portionType);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                jobs.Add(new ScrapeJob
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Url = reader.IsDBNull(reader.GetOrdinal("Url")) ? string.Empty : reader.GetString(reader.GetOrdinal("Url")),
                    HtmlNodePath = reader.IsDBNull(reader.GetOrdinal("HtmlNodePath")) ? string.Empty : reader.GetString(reader.GetOrdinal("HtmlNodePath")),
                    JsonPropertyPath = reader.IsDBNull(reader.GetOrdinal("JsonPropertyPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("JsonPropertyPath")),
                    Label = reader.IsDBNull(reader.GetOrdinal("Label")) ? string.Empty : reader.GetString(reader.GetOrdinal("Label")),
                    Frequency = reader.IsDBNull(reader.GetOrdinal("Frequency")) ? "daily" : reader.GetString(reader.GetOrdinal("Frequency")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    SourceType = reader.IsDBNull(reader.GetOrdinal("SourceType")) ? "html" : reader.GetString(reader.GetOrdinal("SourceType")),
                    PaginationNextLinkXPath = reader.IsDBNull(reader.GetOrdinal("PaginationNextLinkXPath")) ? null : reader.GetString(reader.GetOrdinal("PaginationNextLinkXPath")),
                    MaxRecords = reader.IsDBNull(reader.GetOrdinal("MaxRecords")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("MaxRecords")),
                    PortionType = reader.IsDBNull(reader.GetOrdinal("PortionType")) ? "scheduling" : reader.GetString(reader.GetOrdinal("PortionType")),
                    RowXPath = reader.IsDBNull(reader.GetOrdinal("RowXPath")) ? null : reader.GetString(reader.GetOrdinal("RowXPath")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            return jobs;
        }

        // ====================================================================
        // SaveAsync
        // ====================================================================
        public async Task SaveAsync(DataPoint point)
        {
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                @"INSERT INTO GatheredData (TargetId, ExtractedValue, DatePulled, Status, ErrorMessage, IsProcessed)
                  VALUES (@TargetId, @ExtractedValue, @DatePulled, @Status, @ErrorMessage, 0)", connection);

            command.Parameters.AddWithValue("@TargetId", point.TargetId);
            command.Parameters.AddWithValue("@ExtractedValue", (object?)point.ExtractedValue ?? DBNull.Value);
            command.Parameters.AddWithValue("@DatePulled", point.DatePulled);
            command.Parameters.AddWithValue("@Status", point.Status ?? "success");
            command.Parameters.AddWithValue("@ErrorMessage", string.IsNullOrEmpty(point.ErrorMessage) ? (object)DBNull.Value : point.ErrorMessage);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ====================================================================
        // SaveBulkAsync
        // ====================================================================
        public async Task SaveBulkAsync(List<BulkRecord> records)
        {
            if (records == null || records.Count == 0) return;

            // Get the table name for this target
            int targetId = records[0].TargetId;
            string? tableName = await GetCleanTableNameAsync(targetId);
            if (string.IsNullOrEmpty(tableName)) return;

            // Get field mappings for column order
            var mappings = await GetFieldMappingsAsync(targetId);
            var fieldNames = mappings.OrderBy(m => m.FieldOrder).Select(m => m.FieldName).ToList();

            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            foreach (var record in records)
            {
                // Build INSERT dynamically with validated table name
                var columns = new List<string> { "TargetId", "ScrapedAt" };
                var paramNames = new List<string> { "@TargetId", "@ScrapedAt" };

                int paramIdx = 0;
                foreach (var fieldName in fieldNames)
                {
                    if (record.Fields.ContainsKey(fieldName))
                    {
                        columns.Add(fieldName);
                        paramNames.Add($"@Field{paramIdx}");
                        paramIdx++;
                    }
                }

                ValidateTableName(tableName);
                string sql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@TargetId", record.TargetId);
                command.Parameters.AddWithValue("@ScrapedAt", record.ScrapedAt);

                paramIdx = 0;
                foreach (var fieldName in fieldNames)
                {
                    if (record.Fields.ContainsKey(fieldName))
                    {
                        command.Parameters.AddWithValue($"@Field{paramIdx}",
                            record.Fields.TryGetValue(fieldName, out string? val) ? (object?)val ?? (object)DBNull.Value : DBNull.Value);
                        paramIdx++;
                    }
                }

                await command.ExecuteNonQueryAsync();
            }
        }

        // ====================================================================
        // GetHistoryAsync
        // ====================================================================
        public async Task<DataTable> GetHistoryAsync(int? targetId = null)
        {
            var dataTable = new DataTable();
            using var connection = new SqlConnection(GetConnectionString());

            // Show history for both scheduling and bulk targets
            string sql = @"SELECT gd.Id, gd.TargetId, st.Label, st.PortionType, gd.ExtractedValue, gd.DatePulled, gd.Status, gd.ErrorMessage
                           FROM GatheredData gd
                           INNER JOIN ScrapeTargets st ON gd.TargetId = st.Id
                           WHERE 1=1";
            if (targetId.HasValue)
                sql += " AND gd.TargetId = @TargetId";
            sql += " ORDER BY gd.DatePulled DESC";

            using var command = new SqlCommand(sql, connection);
            if (targetId.HasValue)
                command.Parameters.AddWithValue("@TargetId", targetId.Value);

            await connection.OpenAsync();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        // ====================================================================
        // GetFieldMappingsAsync
        // ====================================================================
        public async Task<List<FieldMapping>> GetFieldMappingsAsync(int targetId)
        {
            var mappings = new List<FieldMapping>();
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                "SELECT Id, TargetId, FieldName, NodePath, FieldOrder FROM FieldMappings WHERE TargetId = @TargetId ORDER BY FieldOrder", connection);
            command.Parameters.AddWithValue("@TargetId", targetId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                mappings.Add(new FieldMapping
                {
                    Id = reader.GetInt32(0),
                    TargetId = reader.GetInt32(1),
                    FieldName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    NodePath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    FieldOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }
            return mappings;
        }

        // ====================================================================
        // ExecuteDDLAsync
        // ====================================================================
        public async Task ExecuteDDLAsync(string safeDdlStatement)
        {
            // Basic validation: reject obviously dangerous statements
            string upper = safeDdlStatement.ToUpperInvariant().TrimStart();
            if (!upper.StartsWith("CREATE") && !upper.StartsWith("ALTER") && !upper.StartsWith("DROP") && !upper.StartsWith("DELETE"))
                throw new ArgumentException("Only CREATE, ALTER, DROP, and DELETE statements are allowed.");

            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(safeDdlStatement, connection);
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        // ====================================================================
        // GetCleanTableAsync
        // ====================================================================
        public async Task<DataTable> GetCleanTableAsync(string exactTableName)
        {
            ValidateTableName(exactTableName);

            var dataTable = new DataTable();
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                $"SELECT * FROM {exactTableName} ORDER BY Id DESC", connection);

            await connection.OpenAsync();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        // ====================================================================
        // GetTargetsAsDataTableAsync
        // ====================================================================
        public async Task<DataTable> GetTargetsAsDataTableAsync()
        {
            var dataTable = new DataTable();
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                @"SELECT Id, Url, Label, Frequency, IsActive, SourceType, 
                         PortionType, MaxRecords, CreatedAt
                  FROM ScrapeTargets ORDER BY Id", connection);

            await connection.OpenAsync();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        // ====================================================================
        // GetAllJobsAsync
        // ====================================================================
        public async Task<List<ScrapeJob>> GetAllJobsAsync()
        {
            var jobs = new List<ScrapeJob>();
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                @"SELECT Id, Url, HtmlNodePath, JsonPropertyPath, Label,
                         Frequency, IsActive, SourceType, PaginationNextLinkXPath,
                         MaxRecords, PortionType, RowXPath, CreatedAt
                  FROM ScrapeTargets ORDER BY Id", connection);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                jobs.Add(new ScrapeJob
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Url = reader.IsDBNull(reader.GetOrdinal("Url")) ? string.Empty : reader.GetString(reader.GetOrdinal("Url")),
                    HtmlNodePath = reader.IsDBNull(reader.GetOrdinal("HtmlNodePath")) ? string.Empty : reader.GetString(reader.GetOrdinal("HtmlNodePath")),
                    JsonPropertyPath = reader.IsDBNull(reader.GetOrdinal("JsonPropertyPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("JsonPropertyPath")),
                    Label = reader.IsDBNull(reader.GetOrdinal("Label")) ? string.Empty : reader.GetString(reader.GetOrdinal("Label")),
                    Frequency = reader.IsDBNull(reader.GetOrdinal("Frequency")) ? "daily" : reader.GetString(reader.GetOrdinal("Frequency")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    SourceType = reader.IsDBNull(reader.GetOrdinal("SourceType")) ? "html" : reader.GetString(reader.GetOrdinal("SourceType")),
                    PaginationNextLinkXPath = reader.IsDBNull(reader.GetOrdinal("PaginationNextLinkXPath")) ? null : reader.GetString(reader.GetOrdinal("PaginationNextLinkXPath")),
                    MaxRecords = reader.IsDBNull(reader.GetOrdinal("MaxRecords")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("MaxRecords")),
                    PortionType = reader.IsDBNull(reader.GetOrdinal("PortionType")) ? "scheduling" : reader.GetString(reader.GetOrdinal("PortionType")),
                    RowXPath = reader.IsDBNull(reader.GetOrdinal("RowXPath")) ? null : reader.GetString(reader.GetOrdinal("RowXPath")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            return jobs;
        }

        // ====================================================================
        // GetSummaryStatsAsync
        // ====================================================================
        public async Task<Dictionary<string, object>> GetSummaryStatsAsync()
        {
            var stats = new Dictionary<string, object>();
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM ScrapeTargets WHERE IsActive = 1", connection))
                stats["TotalTargets"] = (await cmd.ExecuteScalarAsync()) ?? 0;

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM GatheredData", connection))
                stats["TotalRecords"] = (await cmd.ExecuteScalarAsync()) ?? 0;

            using (var cmd = new SqlCommand("SELECT MAX(DatePulled) FROM GatheredData", connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                stats["LastRunTime"] = result != null && result != DBNull.Value ? result : "Never";
            }

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM GatheredData WHERE Status = 'error'", connection))
                stats["ErrorCount"] = (await cmd.ExecuteScalarAsync()) ?? 0;

            return stats;
        }

        // ====================================================================
        // ProcessStagingDataAsync (ETL)
        // ====================================================================
        public async Task ProcessStagingDataAsync()
        {
            string? connStr = AppSettings.LoadConnectionString();
            if (string.IsNullOrWhiteSpace(connStr)) return;

            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();

            // Get unprocessed rows joined with target info
            var stagingRows = new List<StagingRow>();
            using (var extractCmd = new SqlCommand(
                @"SELECT gd.Id, gd.TargetId, gd.ExtractedValue, gd.DatePulled, 
                         st.Label, st.SourceType, st.PortionType
                  FROM GatheredData gd
                  INNER JOIN ScrapeTargets st ON gd.TargetId = st.Id
                  WHERE gd.Status = 'success' AND gd.IsProcessed = 0", connection))
            {
                using var reader = await extractCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    stagingRows.Add(new StagingRow
                    {
                        Id = reader.GetInt32(0),
                        TargetId = reader.GetInt32(1),
                        ExtractedValue = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        DatePulled = reader.GetDateTime(3),
                        Label = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        SourceType = reader.IsDBNull(5) ? "html" : reader.GetString(5),
                        PortionType = reader.IsDBNull(6) ? "scheduling" : reader.GetString(6)
                    });
                }
            }

            foreach (var row in stagingRows)
            {
                try
                {
                    string sanitizedLabel = SanitizeLabel(row.Label);
                    string tableName = row.PortionType == "bulk"
                        ? $"Clean_Bulk_{sanitizedLabel}"
                        : $"Clean_Sched_{sanitizedLabel}";

                    ValidateTableName(tableName);

                    // Get field mappings for this target
                    var mappings = await GetFieldMappingsAsync(row.TargetId);

                    // Parse the extracted value and insert into clean table
                    if (mappings.Count > 0 && !string.IsNullOrEmpty(row.ExtractedValue))
                    {
                        var columns = new List<string> { "TargetId", "DatePulled" };
                        var paramNames = new List<string> { "@TargetId", "@DatePulled" };
                        var fieldValues = new Dictionary<string, string>();

                        // Add all field columns upfront
                        foreach (var mapping in mappings)
                        {
                            columns.Add(mapping.FieldName);
                            paramNames.Add($"@F{mapping.FieldOrder}");
                        }

                        // Try JSON extraction first
                        bool parsedAsJson = false;
                        if (row.ExtractedValue.TrimStart().StartsWith("{") || row.ExtractedValue.TrimStart().StartsWith("["))
                        {
                            try
                            {
                                using var jsonDoc = JsonDocument.Parse(row.ExtractedValue);
                                var root = jsonDoc.RootElement;
                                foreach (var mapping in mappings)
                                {
                                    try
                                    {
                                        JsonElement current = root;
                                        string[] pathParts = mapping.NodePath.Split('.');
                                        foreach (var part in pathParts)
                                        {
                                            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out JsonElement child))
                                                current = child;
                                            else { current = default; break; }
                                        }
                                        string val = current.ValueKind switch
                                        {
                                            JsonValueKind.Number => current.GetDouble().ToString(),
                                            JsonValueKind.String => current.GetString() ?? string.Empty,
                                            JsonValueKind.True => "true",
                                            JsonValueKind.False => "false",
                                            JsonValueKind.Undefined => string.Empty,
                                            _ => current.GetRawText()
                                        };
                                        fieldValues[mapping.FieldName] = val;
                                    }
                                    catch { fieldValues[mapping.FieldName] = string.Empty; }
                                }
                                parsedAsJson = true;
                            }
                            catch { }
                        }

                        // If not JSON (or JSON parse failed), treat as HTML and use XPath extraction
                        if (!parsedAsJson)
                        {
                            try
                            {
                                var doc = new HtmlAgilityPack.HtmlDocument();
                                doc.LoadHtml(row.ExtractedValue);
                                foreach (var mapping in mappings)
                                {
                                    try
                                    {
                                        string xpath = mapping.NodePath?.Trim() ?? "";
                                        string val = string.Empty;
                                        if (xpath.Contains("/@"))
                                        {
                                            int atIdx = xpath.LastIndexOf("/@");
                                            string nodeXP = xpath.Substring(0, atIdx);
                                            string attrN  = xpath.Substring(atIdx + 2);
                                            var attrNode = doc.DocumentNode.SelectSingleNode(nodeXP);
                                            val = attrNode?.GetAttributeValue(attrN, "") ?? string.Empty;
                                        }
                                        else if (!string.IsNullOrEmpty(xpath))
                                        {
                                            var node = doc.DocumentNode.SelectSingleNode(xpath);
                                            val = node?.InnerText.Trim() ?? string.Empty;
                                        }
                                        fieldValues[mapping.FieldName] = val;
                                    }
                                    catch { fieldValues[mapping.FieldName] = string.Empty; }
                                }
                            }
                            catch { if (mappings.Count > 0) fieldValues[mappings[0].FieldName] = row.ExtractedValue; }
                        }

                        // Build and execute INSERT
                        string insertSql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)})";
                        using var insertCmd = new SqlCommand(insertSql, connection);
                        insertCmd.Parameters.AddWithValue("@TargetId", row.TargetId);
                        insertCmd.Parameters.AddWithValue("@DatePulled", row.DatePulled);

                        foreach (var mapping in mappings)
                        {
                            string pName = $"@F{mapping.FieldOrder}";
                            string fVal = fieldValues.TryGetValue(mapping.FieldName, out string? v) ? v : string.Empty;
                            insertCmd.Parameters.AddWithValue(pName, (object?)fVal ?? DBNull.Value);
                        }

                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    // Mark as processed
                    using var markCmd = new SqlCommand(
                        "UPDATE GatheredData SET IsProcessed = 1 WHERE Id = @Id", connection);
                    markCmd.Parameters.AddWithValue("@Id", row.Id);
                    await markCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ETL failed for row {row.Id}: {ex.Message}");
                }
            }
        }

        // ====================================================================
        // Helper: Get clean table name from target
        // ====================================================================
        private async Task<string?> GetCleanTableNameAsync(int targetId)
        {
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                "SELECT PortionType, Label FROM ScrapeTargets WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", targetId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string portionType = reader.IsDBNull(0) ? "scheduling" : reader.GetString(0);
                string label = reader.IsDBNull(1) ? "" : reader.GetString(1);
                string sanitized = SanitizeLabel(label);
                return portionType == "bulk" ? $"Clean_Bulk_{sanitized}" : $"Clean_Sched_{sanitized}";
            }
            return null;
        }


        // ====================================================================
        // GetAllJobsByTypeAsync - returns ALL jobs (active AND inactive) for a portion type
        // Used by scheduling grid so toggling IsActive doesn't make rows disappear
        // ====================================================================
        public async Task<List<ScrapeJob>> GetAllJobsByTypeAsync(string portionType)
        {
            var jobs = new List<ScrapeJob>();
            using var connection = new SqlConnection(GetConnectionString());

            string sql = @"SELECT Id, Url, HtmlNodePath, JsonPropertyPath, Label,
                                  Frequency, IsActive, SourceType, PaginationNextLinkXPath,
                                  MaxRecords, PortionType, RowXPath, CreatedAt
                           FROM ScrapeTargets WHERE PortionType = @PortionType ORDER BY Id";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@PortionType", portionType);
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                jobs.Add(new ScrapeJob
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Url = reader.IsDBNull(reader.GetOrdinal("Url")) ? string.Empty : reader.GetString(reader.GetOrdinal("Url")),
                    HtmlNodePath = reader.IsDBNull(reader.GetOrdinal("HtmlNodePath")) ? string.Empty : reader.GetString(reader.GetOrdinal("HtmlNodePath")),
                    JsonPropertyPath = reader.IsDBNull(reader.GetOrdinal("JsonPropertyPath")) ? string.Empty : reader.GetString(reader.GetOrdinal("JsonPropertyPath")),
                    Label = reader.IsDBNull(reader.GetOrdinal("Label")) ? string.Empty : reader.GetString(reader.GetOrdinal("Label")),
                    Frequency = reader.IsDBNull(reader.GetOrdinal("Frequency")) ? "daily" : reader.GetString(reader.GetOrdinal("Frequency")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    SourceType = reader.IsDBNull(reader.GetOrdinal("SourceType")) ? "html" : reader.GetString(reader.GetOrdinal("SourceType")),
                    PaginationNextLinkXPath = reader.IsDBNull(reader.GetOrdinal("PaginationNextLinkXPath")) ? null : reader.GetString(reader.GetOrdinal("PaginationNextLinkXPath")),
                    MaxRecords = reader.IsDBNull(reader.GetOrdinal("MaxRecords")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("MaxRecords")),
                    PortionType = reader.IsDBNull(reader.GetOrdinal("PortionType")) ? "scheduling" : reader.GetString(reader.GetOrdinal("PortionType")),
                    RowXPath = reader.IsDBNull(reader.GetOrdinal("RowXPath")) ? null : reader.GetString(reader.GetOrdinal("RowXPath")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            return jobs;
        }

        // ====================================================================
        // Helper: Sanitize label for use in table names
        // ====================================================================
        public static string SanitizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "Unknown";
            string sanitized = Regex.Replace(label, @"[^A-Za-z0-9_]", "");
            return string.IsNullOrEmpty(sanitized) ? "Unknown" : sanitized;
        }

        // ====================================================================
        // Helper: Check if a table exists
        // ====================================================================
        public async Task<bool> TableExistsAsync(string tableName)
        {
            ValidateTableName(tableName);
            using var connection = new SqlConnection(GetConnectionString());
            using var command = new SqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName", connection);
            command.Parameters.AddWithValue("@TableName", tableName);
            await connection.OpenAsync();
            var scalarResult = await command.ExecuteScalarAsync();
            return scalarResult != null && (int)scalarResult > 0;
        }

        // ====================================================================
        // Inner class: StagingRow
        // ====================================================================
        private class StagingRow
        {
            public int Id { get; set; }
            public int TargetId { get; set; }
            public string ExtractedValue { get; set; } = string.Empty;
            public DateTime DatePulled { get; set; }
            public string Label { get; set; } = string.Empty;
            public string SourceType { get; set; } = "html";
            public string PortionType { get; set; } = "scheduling";
        }

        /// <summary>Returns current Pakistan Standard Time (UTC+5).</summary>
        public static DateTime NowPKT()
        {
            var pkt = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pkt);
        }

    }
}