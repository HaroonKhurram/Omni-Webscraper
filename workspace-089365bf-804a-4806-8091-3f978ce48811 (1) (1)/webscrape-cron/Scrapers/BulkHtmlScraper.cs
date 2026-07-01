// ========== FILE: Scrapers/BulkHtmlScraper.cs ==========
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using WebScrapeCron.Models;
using WebScrapeCron.Data;

namespace WebScrapeCron.Scrapers
{
    /* ========================================================================
     * CLASS: BulkHtmlScraper
     * ========================================================================
     * OOP PRINCIPLES: INHERITANCE + POLYMORPHISM (with RunAsync override)
     * ------------------------------------------------------------------
     * Unlike HtmlNodeScraper/JsonApiScraper which use the base class Template
     * Method, BulkHtmlScraper overrides RunAsync() entirely because its
     * workflow is fundamentally different:
     *   - It paginates across multiple pages
     *   - It extracts MULTIPLE records per page (not a single value)
     *   - It saves via SaveBulkAsync() instead of SaveAsync()
     *   - It respects MaxRecords limit and CancellationToken
     *
     * DATA FLOW:
     * RunAsync() [OVERRIDDEN - does NOT call base.RunAsync()]
     *     -> Loop: fetch page HTML -> extract rows -> save batch
     *     -> Find "next page" link via PaginationNextLinkXPath
     *     -> Continue until no next page OR record count >= MaxRecords
     *     -> Each page: Repository.SaveBulkAsync(List<BulkRecord>)
     *
     * DESIGN DECISIONS:
     * - CancellationToken checked after each page for clean cancellation
     * - OnProgress fires after each page for UI progress updates
     * - HttpClient is static to avoid socket exhaustion
     * - Relative URL resolution for pagination links
     * ======================================================================== */

    public class BulkHtmlScraper : WebScraper
    {
        static BulkHtmlScraper() { /* Intentionally left blank - base handles shared client */ }

        public BulkHtmlScraper(ScrapeJob job, IDataRepository repo) : base(job, repo) { }

        /// <summary>
        /// Overrides the base Template Method entirely. Bulk scraping has
        /// a fundamentally different workflow: pagination, multi-row extraction,
        /// and bulk saving.
        /// </summary>
        public override async Task RunAsync(CancellationToken ct)
        {
            string? currentUrl = Job.Url;
            int totalRecords = 0;
            var fieldMappings = await Repository.GetFieldMappingsAsync(Job.Id);

            try
            {
                while (!string.IsNullOrEmpty(currentUrl))
                {
                    ct.ThrowIfCancellationRequested();

                    ReportProgress($"Fetching page: {currentUrl}");

                    // Random delay between pages
                    await RandomDelaySecondsAsync(3, 7, ct);

                    var response = await SendWithRetriesAsync(currentUrl, ct);
                    response.EnsureSuccessStatusCode();

                    string html = await response.Content.ReadAsStringAsync(ct);
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);

                    // Extract rows using Job.RowXPath
                    var rowNodes = doc.DocumentNode.SelectNodes(Job.RowXPath ?? "//body");
                    if (rowNodes != null)
                    {
                        var records = new List<BulkRecord>();

                        foreach (var rowNode in rowNodes)
                        {
                            ct.ThrowIfCancellationRequested();

                            var fields = new Dictionary<string, string>();
                            foreach (var mapping in fieldMappings.OrderBy(m => m.FieldOrder))
                            {
                                try
                                {
                                    string colXPath = mapping.NodePath?.Trim() ?? "";
                                    string colValue = string.Empty;

                                    if (colXPath.Contains("/@"))
                                    {
                                        // Attribute XPath: split node path and attribute name
                                        int atIdx = colXPath.LastIndexOf("/@");
                                        string nodeXP = colXPath.Substring(0, atIdx);
                                        string attrN  = colXPath.Substring(atIdx + 2);
                                        var attrNode = rowNode.SelectSingleNode(nodeXP);
                                        colValue = attrNode?.GetAttributeValue(attrN, "") ?? string.Empty;
                                    }
                                    else
                                    {
                                        var fieldNode = rowNode.SelectSingleNode(colXPath);
                                        colValue = fieldNode?.InnerText.Trim() ?? string.Empty;
                                    }
                                    fields[mapping.FieldName] = colValue;
                                }
                                catch
                                {
                                    fields[mapping.FieldName] = string.Empty;
                                }
                            }

                            records.Add(new BulkRecord
                            {
                                TargetId = Job.Id,
                                Fields = fields,
                                ScrapedAt = NowPKT()
                            });

                            // Small delay between records to avoid aggressive scraping (1-2s)
                            await RandomDelaySecondsAsync(1, 2, ct);
                        }

                        await Repository.SaveBulkAsync(records);
                        totalRecords += records.Count;
                        ReportProgress($"Page scraped - {totalRecords} records so far");
                    }

                    // Check max records limit
                    if (Job.MaxRecords.HasValue && totalRecords >= Job.MaxRecords.Value)
                    {
                        ReportProgress($"Max records limit ({Job.MaxRecords}) reached.");
                        break;
                    }

                    // Find next page link
                    // Handle both: //a[@rel='next'] (returns element, read href attr)
                    // and //a[@rel='next']/@href (returns attribute node, read Value)
                    if (!string.IsNullOrEmpty(Job.PaginationNextLinkXPath))
                    {
                        string paginXPath = Job.PaginationNextLinkXPath.Trim();
                        string? nextHref = null;

                        if (paginXPath.EndsWith("/@href"))
                        {
                            // XPath points to attribute directly - select parent node and read href
                            string nodeXPath = paginXPath.Substring(0, paginXPath.LastIndexOf("/@href"));
                            var nextNode = doc.DocumentNode.SelectSingleNode(nodeXPath);
                            nextHref = nextNode?.GetAttributeValue("href", null);
                        }
                        else
                        {
                            // XPath points to element - read href attribute
                            var nextLinkNode = doc.DocumentNode.SelectSingleNode(paginXPath);
                            nextHref = nextLinkNode?.GetAttributeValue("href", null);
                        }

                        if (!string.IsNullOrEmpty(nextHref))
                        {
                            currentUrl = nextHref.StartsWith("http")
                                ? nextHref
                                : new Uri(new Uri(currentUrl ?? Job.Url), nextHref).ToString();
                        }
                        else
                        {
                            currentUrl = null;
                        }
                    }
                    else
                    {
                        currentUrl = null;
                    }
                }

                ReportProgress($"Complete - {totalRecords} records saved.");
            }
            catch (OperationCanceledException)
            {
                ReportProgress($"Bulk scrape cancelled after {totalRecords} records.");
                throw;
            }
            catch (Exception ex)
            {
                ReportProgress($"Bulk scrape failed after {totalRecords} records: {ex.Message}");
                throw;
            }
        }

        // Not used for bulk scraping but required by base class
        protected override Task<string> FetchData(CancellationToken ct)
            => throw new NotSupportedException("BulkHtmlScraper does not use FetchData.");

        /// <summary>
        /// Returns current time in Pakistan Standard Time (UTC+5).
        /// Used for all DatePulled timestamps throughout the app.
        /// </summary>
        private static DateTime NowPKT()
        {
            var pkt = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pkt);
        }

    }
}