// ========== FILE: Scrapers/HtmlNodeScraper.cs ==========
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using WebScrapeCron.Models;
using WebScrapeCron.Data;

namespace WebScrapeCron.Scrapers
{
    /* ========================================================================
     * CLASS: HtmlNodeScraper
     * ========================================================================
     * OOP PRINCIPLES: INHERITANCE + POLYMORPHISM
     * ------------------------------------------
     * Extends WebScraper. When RunAsync() calls FetchData(), the CLR
     * dispatches to THIS implementation. Any code holding a WebScraper
     * reference can call RunAsync() without knowing the concrete type.
     *
     * DATA FLOW:
     * RunAsync() [inherited] -> FetchData() [this implementation]
     *     -> HttpClient.GetAsync(Job.Url) -> HTML string
     *     -> HtmlAgilityPack.HtmlDocument -> XPath select -> InnerText
     *
     * DESIGN: Static HttpClient prevents socket exhaustion.
     * HtmlAgilityPack.HtmlDocument is fully qualified to avoid
     * ambiguity with System.Windows.Forms.HtmlDocument.
     * ======================================================================== */

    public class HtmlNodeScraper : WebScraper
    {
        public HtmlNodeScraper(ScrapeJob job, IDataRepository repo) : base(job, repo) { }
        protected override async Task<string> FetchData(CancellationToken ct)
        {
            // Random delay between pages to reduce detection (3-7s)
            await RandomDelaySecondsAsync(3, 7, ct);

            var response = await SendWithRetriesAsync(Job.Url, ct);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync(ct);
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            string xpath = Job.HtmlNodePath?.Trim() ?? "";

            // If no HtmlNodePath is set (scheduling targets store paths in FieldMappings),
            // return the full page HTML so ETL can extract individual fields
            if (string.IsNullOrEmpty(xpath))
            {
                // Return body InnerText as raw content for ETL processing
                var body = doc.DocumentNode.SelectSingleNode("//body");
                return body?.InnerHtml ?? html;
            }

            // CASE 1: XPath ends with /@attributeName (e.g. //a/@href, //p/@class)
            if (xpath.Contains("/@"))
            {
                int atIdx = xpath.LastIndexOf("/@");
                string nodeXPath = xpath.Substring(0, atIdx);
                string attrName  = xpath.Substring(atIdx + 2);
                HtmlNode? attrNode = doc.DocumentNode.SelectSingleNode(nodeXPath);
                if (attrNode == null)
                    throw new InvalidOperationException($"XPath '{nodeXPath}' matched no node on '{Job.Url}'. Response snippet: { (html?.Length > 500 ? html.Substring(0,500) : html) }");
                string attrVal = attrNode.GetAttributeValue(attrName, "");
                if (string.IsNullOrEmpty(attrVal))
                    throw new InvalidOperationException($"Attribute '@{attrName}' not found on matched node. Response snippet: { (html?.Length > 500 ? html.Substring(0,500) : html) }");
                return attrVal.Trim();
            }

            // CASE 2: Standard XPath - return InnerHtml so ETL can parse HTML fragments
            HtmlNode? node = doc.DocumentNode.SelectSingleNode(xpath);
            if (node == null)
                throw new InvalidOperationException($"XPath '{xpath}' did not match any node on '{Job.Url}'. Response snippet: { (html?.Length > 500 ? html.Substring(0,500) : html) }");
            return node.InnerHtml.Trim();
        }
    }
}
