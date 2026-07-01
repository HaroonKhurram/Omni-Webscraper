// ========== FILE: Models/BulkRecord.cs ==========
using System;
using System.Collections.Generic;

namespace WebScrapeCron.Models
{
    /* ========================================================================
     * CLASS: BulkRecord
     * ========================================================================
     * OOP PRINCIPLE: ENCAPSULATION
     * ----------------------------
     * BulkRecord represents a single row extracted by the BulkHtmlScraper.
     * Unlike DataPoint (which stores a single string value), BulkRecord
     * stores a Dictionary of field-name → extracted-value pairs, supporting
     * multi-column data extraction from structured HTML pages.
     *
     * DATA FLOW:
     * BulkHtmlScraper extracts rows from paginated HTML pages
     *     → Each row becomes a BulkRecord with Fields dictionary
     *     → Repository.SaveBulkAsync(List<BulkRecord>)
     *     → SQL INSERT into Clean_Bulk_[Label] with one column per field
     *
     * DESIGN DECISION: Using Dictionary<string,string> instead of a fixed
     * model class allows dynamic column definitions. The user defines
     * field names at runtime via AddBulkTargetForm, so we can't know
     * the columns at compile time.
     * ======================================================================== */

    /// <summary>
    /// A single row of bulk-scraped data with dynamic field names and values.
    /// Used by BulkHtmlScraper for multi-column extraction from HTML pages.
    /// </summary>
    public class BulkRecord
    {
        public int TargetId { get; init; }
        public Dictionary<string, string> Fields { get; init; } = new();
        public DateTime ScrapedAt { get; init; } = DateTime.UtcNow;
    }
}
