// ========== FILE: Models/DataPoint.cs ==========
using System;

namespace WebScrapeCron.Models
{
    /* ========================================================================
     * CLASS: DataPoint
     * ========================================================================
     * OOP PRINCIPLE: ENCAPSULATION (Value Object / Immutable Record)
     * ---------------------------------------------------------------
     * DataPoint encapsulates a single observation from a scheduling-type
     * scrape operation. It's immutable after construction — a historical
     * fact that should never be modified.
     *
     * DATA FLOW:
     * WebScraper.RunAsync() creates DataPoint
     *     → Repository.SaveAsync(dataPoint)
     *     → SQL INSERT into GatheredData
     *     → ProcessStagingData() reads unprocessed rows
     *     → Transforms into Clean_Sched_[Label] tables
     *     → UI reads GatheredData for History tab display
     *
     * DESIGN: Status="success" rows are processed by ETL; Status="error"
     * rows are skipped but preserved for debugging/auditing.
     * ======================================================================== */

    /// <summary>
    /// A single data observation from a scheduling-type scrape operation.
    /// Immutable — represents a historical fact at a point in time.
    /// </summary>
    public class DataPoint
    {
        public int TargetId { get; init; }
        public string ExtractedValue { get; init; } = string.Empty;
        public DateTime DatePulled { get; init; } = DateTime.UtcNow;
        public string Status { get; init; } = "success";
        public string ErrorMessage { get; init; } = string.Empty;
    }
}
