// ========== FILE: Models/FieldMapping.cs ==========
namespace WebScrapeCron.Models
{
    /* ========================================================================
     * CLASS: FieldMapping
     * ========================================================================
     * OOP PRINCIPLE: ENCAPSULATION
     * ----------------------------
     * FieldMapping stores the relationship between a user-defined field name
     * and the XPath/JSON path used to extract that field's value. This is
     * the metadata that drives dynamic ETL processing.
     *
     * DATA FLOW:
     * User defines fields in AddScheduleTargetForm / AddBulkTargetForm
     *     → INSERT into FieldMappings table
     *     → Loaded by SqlDataRepository.GetFieldMappingsAsync(targetId)
     *     → Used by BulkHtmlScraper to extract columns per row
     *     → Used by ProcessStagingData() to deserialize JSON into columns
     *     → Used by EditTargetForm to display editable field configurations
     *
     * DESIGN DECISION: Separate table instead of storing paths as JSON in
     * ScrapeTargets because:
     * 1. Each mapping is independently queryable and editable
     * 2. FieldOrder controls column display order in the UI
     * 3. Adding/removing fields is simple INSERT/DELETE operations
     * ======================================================================== */

    /// <summary>
    /// Maps a user-defined field name to its extraction path (XPath or JSON path)
    /// for a specific scrape target.
    /// </summary>
    public class FieldMapping
    {
        public int Id { get; init; }
        public int TargetId { get; init; }
        public string FieldName { get; init; } = string.Empty;
        public string NodePath { get; init; } = string.Empty;
        public int FieldOrder { get; init; }
    }
}
