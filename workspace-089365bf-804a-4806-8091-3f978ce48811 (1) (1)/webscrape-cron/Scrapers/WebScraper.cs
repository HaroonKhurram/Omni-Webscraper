// ========== FILE: Scrapers/WebScraper.cs ==========
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebScrapeCron.Models;
using WebScrapeCron.Data;

namespace WebScrapeCron.Scrapers
{
    /* ========================================================================
     * ABSTRACT CLASS: WebScraper
     * ========================================================================
     * OOP PRINCIPLES: ABSTRACTION + TEMPLATE METHOD PATTERN
     * ----------------------------------------------------
     * This abstract class defines WHAT a web scraper does (fetch data, save it)
     * without specifying HOW. RunAsync() is the Template Method: it defines
     * the algorithm skeleton (fetch -> wrap -> save -> handle errors), while
     * FetchData() is the abstract hook that derived classes override.
     *
     * DATA FLOW:
     * CronScheduler tick -> scraper.RunAsync(ct)
     *     -> this.FetchData(ct) [polymorphic dispatch to concrete class]
     *     -> wrap in DataPoint -> Repository.SaveAsync()
     *
     * DESIGN DECISIONS:
     * - CancellationToken support for clean shutdown
     * - OnProgress event decouples scraper from UI (Observer pattern)
     * - Protected fields for derived class access, private for external code
     * - RunAsync is virtual - BulkHtmlScraper overrides it with a completely different algorithm
     * ======================================================================== */

    public abstract class WebScraper
    {
        protected readonly ScrapeJob Job;
        protected readonly IDataRepository Repository;

        // Shared HttpClient configured with a handler to enable
        // automatic decompression, cookies, and redirects.
        // Also exposes helpers for rotating User-Agents, delays, and retries.
        protected static readonly HttpClient _sharedHttpClient;
        private static readonly string[] _userAgents;
        private static readonly Random _rng;
        private static readonly object _rngLock = new object();

        /// <summary>
        /// Fires progress/status messages. UI subscribes to display progress
        /// without tight coupling (Observer pattern).
        /// </summary>
        public event Action<string>? OnProgress;

        protected WebScraper(ScrapeJob job, IDataRepository repo)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            Repository = repo ?? throw new ArgumentNullException(nameof(repo));
        }

        static WebScraper()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            _sharedHttpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(45)
            };

            // Default headers that are safe to share across requests
            _sharedHttpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _sharedHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
            _sharedHttpClient.DefaultRequestHeaders.Connection.Add("keep-alive");

            // A small set of real browser User-Agent strings to rotate
            _userAgents = new[]
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 13_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Safari/605.1.15",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:118.0) Gecko/20100101 Firefox/118.0",
                "Mozilla/5.0 (iPhone; CPU iPhone OS 16_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.5 Mobile/15E148 Safari/604.1"
            };

            _rng = new Random();
        }

        /// <summary>
        /// Waits a random number of seconds between minSeconds and maxSeconds (inclusive).
        /// </summary>
        protected static Task RandomDelaySecondsAsync(int minSeconds, int maxSeconds, CancellationToken ct)
        {
            int delaySecs;
            lock (_rngLock)
            {
                delaySecs = _rng.Next(minSeconds, maxSeconds + 1);
            }
            return Task.Delay(TimeSpan.FromSeconds(delaySecs), ct);
        }

        /// <summary>
        /// Sends an HTTP GET with rotating User-Agent, required headers and retry logic.
        /// Retries on 403, 429 or timeouts up to 3 attempts with a 10s backoff.
        /// </summary>
        protected async Task<HttpResponseMessage> SendWithRetriesAsync(string url, CancellationToken ct)
        {
            const int maxAttempts = 3;
            int attempt = 0;
            Exception? lastEx = null;
            HttpResponseMessage? lastResponse = null;

            while (attempt < maxAttempts)
            {
                attempt++;
                ct.ThrowIfCancellationRequested();

                string ua;
                lock (_rngLock)
                {
                    ua = _userAgents[_rng.Next(_userAgents.Length)];
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", ua);
                req.Headers.TryAddWithoutValidation("Referer", "https://www.google.com/");
                req.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");

                try
                {
                    var resp = await _sharedHttpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
                    lastResponse = resp;

                    if (resp.StatusCode == HttpStatusCode.Forbidden || (int)resp.StatusCode == 429)
                    {
                        // Throttled/blocked - wait and retry
                        await Task.Delay(TimeSpan.FromSeconds(10), ct);
                        continue;
                    }

                    return resp;
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    // Timeout - treat like a retryable failure
                    lastEx = ex;
                    await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
                    continue;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
                    continue;
                }
            }

            if (lastResponse != null)
                return lastResponse;

            throw lastEx ?? new HttpRequestException($"Failed to fetch '{url}' after {maxAttempts} attempts.");
        }

        /// <summary>
        /// Template Method - the fixed algorithm for scheduling-type scrapers.
        /// Calls abstract FetchData(), wraps result in DataPoint, saves via repo.
        /// On failure, saves an error DataPoint instead of crashing.
        /// </summary>
        public virtual async Task RunAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                ReportProgress($"Scraping '{Job.Label}'...");

                string rawData = await FetchData(ct);

                var dataPoint = new DataPoint
                {
                    TargetId = Job.Id,
                    ExtractedValue = rawData ?? string.Empty,
                    DatePulled = NowPKT(),
                    Status = "success",
                    ErrorMessage = string.Empty
                };

                await Repository.SaveAsync(dataPoint);
                ReportProgress($"'{Job.Label}' scraped successfully.");
            }
            catch (OperationCanceledException)
            {
                ReportProgress($"'{Job.Label}' was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                var errorPoint = new DataPoint
                {
                    TargetId = Job.Id,
                    ExtractedValue = string.Empty,
                    DatePulled = NowPKT(),
                    Status = "error",
                    ErrorMessage = ex.Message
                };

                try { await Repository.SaveAsync(errorPoint); } catch { }
                ReportProgress($"'{Job.Label}' failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Abstract hook - each concrete scraper provides its own fetch logic.
        /// </summary>
        protected abstract Task<string> FetchData(CancellationToken ct);

        protected void ReportProgress(string message)
        {
            OnProgress?.Invoke(message);
        }

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