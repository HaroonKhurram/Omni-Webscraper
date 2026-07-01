// ========== FILE: Scrapers/JsonApiScraper.cs ==========
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebScrapeCron.Models;
using WebScrapeCron.Data;

namespace WebScrapeCron.Scrapers
{
    /* ========================================================================
     * CLASS: JsonApiScraper
     * ========================================================================
     * OOP PRINCIPLES: INHERITANCE + POLYMORPHISM
     * Same pattern as HtmlNodeScraper but for JSON APIs.
     * Uses System.Text.Json (built into .NET 8) for high-performance parsing.
     *
     * DATA FLOW:
     * RunAsync() [inherited] -> FetchData() [this implementation]
     *     -> HttpClient.GetAsync() -> JSON string
     *     -> JsonDocument.Parse() -> navigate dot-notation path -> value
     *
     * JSON PATH EXAMPLE: "current.temperature_2m"
     *   -> root["current"]["temperature_2m"] -> 28.5
     * ======================================================================== */

    public class JsonApiScraper : WebScraper
    {
        public JsonApiScraper(ScrapeJob job, IDataRepository repo) : base(job, repo) { }

        protected override async Task<string> FetchData(CancellationToken ct)
        {
            // Random delay between pages to reduce detection (3-7s)
            await RandomDelaySecondsAsync(3, 7, ct);

            var response = await SendWithRetriesAsync(Job.Url, ct);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct);

            using var jsonDoc = JsonDocument.Parse(json);
            JsonElement current = jsonDoc.RootElement;

            string[] segments = Job.JsonPropertyPath.Split(
                new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
                current = current.GetProperty(segment);

            return current.ValueKind switch
            {
                JsonValueKind.Object => current.GetRawText(),
                JsonValueKind.Array => current.GetRawText(),
                JsonValueKind.String => current.GetString() ?? string.Empty,
                JsonValueKind.Number => current.GetDouble().ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => current.GetRawText()
            };
        }
    }
}
