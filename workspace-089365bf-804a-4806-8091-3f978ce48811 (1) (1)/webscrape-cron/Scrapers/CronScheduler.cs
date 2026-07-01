// ========== FILE: Scrapers/CronScheduler.cs ==========
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebScrapeCron.Data;

namespace WebScrapeCron.Scrapers
{
    /* ========================================================================
     * CLASS: CronScheduler
     * ========================================================================
     * OOP PRINCIPLES: ENCAPSULATION + OBSERVER PATTERN (via C# Events)
     * ----------------------------------------------------------------
     * The scheduler encapsulates the timer, scraper list, and cancellation
     * token management. External code only sees Start()/Stop() and the
     * TickCompleted event.
     *
     * DATA FLOW:
     * Start() -> Timer begins -> OnTimerElapsed (ThreadPool thread)
     *     -> foreach scraper: RunAsync(ct)
     *     -> ProcessStagingDataAsync() (ETL)
     *     -> Raise TickCompleted event -> UI refreshes via Invoke()
     *
     * DESIGN DECISIONS:
     * - CancellationTokenSource per run allows clean mid-scrape cancellation
     * - Re-entrance guard prevents overlapping ticks
     * - System.Timers.Timer fires on ThreadPool (not UI thread)
     * - Uses fully qualified System.Timers.Timer to avoid WinForms ambiguity
     * ======================================================================== */

    public class CronScheduler
    {
        private readonly System.Timers.Timer _timer;
        private readonly List<WebScraper> _scrapers = new();
        private readonly IDataRepository _repository;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// Fires after each tick completes with a summary message.
        /// UI subscribes and marshals to UI thread via Invoke().
        /// </summary>
        public event Action<string>? TickCompleted;

        public CronScheduler(IDataRepository repository, double intervalMs = 300000)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _timer = new System.Timers.Timer(intervalMs)
            {
                AutoReset = true,
                Enabled = false
            };
            _timer.Elapsed += OnTimerElapsed;
        }

        public void AddScraper(WebScraper scraper)
        {
            if (scraper != null) _scrapers.Add(scraper);
        }

        public void ClearScrapers() => _scrapers.Clear();

        public bool IsRunning => _timer.Enabled;

        public void Start()
        {
            _timer.Enabled = true;
            _ = ExecuteTickAsync();
        }

        public void Stop()
        {
            _timer.Enabled = false;
            _cts?.Cancel();
        }

        public async Task ManualTriggerAsync()
        {
            await ExecuteTickAsync();
        }

        private async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await ExecuteTickAsync();
        }

        private async Task ExecuteTickAsync()
        {
            if (_isRunning) return;
            _isRunning = true;

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            int successCount = 0;
            int errorCount = 0;

            try
            {
                var tasks = _scrapers.Select(async s =>
                {
                    try
                    {
                        await s.RunAsync(ct);
                        Interlocked.Increment(ref successCount);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { Interlocked.Increment(ref errorCount); }
                }).ToArray();

                await Task.WhenAll(tasks);

                // ETL: process staging data
                if (_repository is SqlDataRepository sqlRepo)
                    await sqlRepo.ProcessStagingDataAsync();

                string summary = $"Tick complete: {successCount} succeeded, {errorCount} failed";
                TickCompleted?.Invoke(summary);
            }
            catch (OperationCanceledException)
            {
                TickCompleted?.Invoke("Tick cancelled by user.");
            }
            catch (Exception ex)
            {
                TickCompleted?.Invoke($"Tick error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}
