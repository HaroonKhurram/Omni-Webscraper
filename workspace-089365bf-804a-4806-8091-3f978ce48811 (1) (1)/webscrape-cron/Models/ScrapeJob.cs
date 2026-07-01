// ========== FILE: Models/ScrapeJob.cs ==========
using System;

namespace WebScrapeCron.Models
{
    /* ========================================================================
     * CLASS: ScrapeJob
     * ========================================================================
     * OOP PRINCIPLE: ENCAPSULATION
     * ----------------------------
     * This class encapsulates all configuration for a single scraping target.
     * Using { get; init; } setters enforces immutability after construction,
     * which is critical for thread safety in the multi-threaded scheduler.
     *
     * DATA FLOW:
     * SQL Server ScrapeTargets table → SqlDataReader → ScrapeJob object
     *     → ScraperFactory.Create(job, repo) → stored as field in WebScraper
     *     → used by FetchData() to know WHAT to scrape and WHERE
     *
     * DESIGN DECISIONS:
     * - PortionType field drives the factory pattern: "scheduling" creates
     *   HtmlNodeScraper/JsonApiScraper, "bulk" creates BulkHtmlScraper
     * - PaginationNextLinkXPath and MaxRecords are nullable because only
     *   bulk scrapers need them; null = not applicable
     * - init-only setters prevent accidental modification by other threads
     * ======================================================================== */

    /// <summary>
    /// Represents a single scraping target loaded from ScrapeTargets.
    /// Contains all configuration needed to perform a scrape operation.
    /// </summary>
    public class ScrapeJob
    {
        public int Id { get; init; }
        public string Url { get; init; } = string.Empty;
        public string HtmlNodePath { get; init; } = string.Empty;
        public string JsonPropertyPath { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Frequency { get; init; } = "daily";
        public bool IsActive { get; init; } = true;
        public string SourceType { get; init; } = "html";
        public string? PaginationNextLinkXPath { get; init; }
        public int? MaxRecords { get; init; }
        public string PortionType { get; init; } = "scheduling";
        public string? RowXPath { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }
}
