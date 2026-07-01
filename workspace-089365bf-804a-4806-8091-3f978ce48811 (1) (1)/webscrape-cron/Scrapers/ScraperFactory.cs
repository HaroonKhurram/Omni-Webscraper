// ========== FILE: Scrapers/ScraperFactory.cs ==========
using WebScrapeCron.Models;
using WebScrapeCron.Data;

namespace WebScrapeCron.Scrapers
{
    /* ========================================================================
     * STATIC CLASS: ScraperFactory
     * ========================================================================
     * OOP PRINCIPLE: FACTORY METHOD PATTERN (Creational Design Pattern)
     * ------------------------------------------------------------------
     * Encapsulates object creation logic. The caller (CronScheduler or UI)
     * only knows about the abstract WebScraper type - it never directly
     * references HtmlNodeScraper, JsonApiScraper, or BulkHtmlScraper.
     *
     * ROUTING LOGIC:
     *   PortionType "bulk"   -> BulkHtmlScraper (checked FIRST)
     *   SourceType "json"    -> JsonApiScraper
     *   Default (anything)   -> HtmlNodeScraper
     *
     * DESIGN: PortionType is checked first because bulk scrapers always
     * have SourceType="html" but need BulkHtmlScraper, not HtmlNodeScraper.
     * ======================================================================== */

    public static class ScraperFactory
    {
        public static WebScraper Create(ScrapeJob job, IDataRepository repo)
        {
            // Check PortionType first - bulk overrides SourceType
            if (job.PortionType?.ToLower() == "bulk")
                return new BulkHtmlScraper(job, repo);

            return job.SourceType?.ToLower() switch
            {
                "json" => new JsonApiScraper(job, repo),
                _ => new HtmlNodeScraper(job, repo)
            };
        }
    }
}
