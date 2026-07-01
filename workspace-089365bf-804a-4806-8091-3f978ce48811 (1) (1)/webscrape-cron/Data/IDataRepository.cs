// ========== FILE: Data/IDataRepository.cs ==========
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using WebScrapeCron.Models;

namespace WebScrapeCron.Data
{
    /* ========================================================================
     * INTERFACE: IDataRepository
     * ========================================================================
     * OOP PRINCIPLE: ABSTRACTION (Interface Segregation + Dependency Inversion)
     * -----------------------------------------------------------------------
     * Defines the contract for data access. The scraper classes depend on
     * this abstraction, not on SqlDataRepository. This enables:
     *   1. Decoupling - swap SQL Server for any other store
     *   2. Testability - mock repositories for unit tests
     *   3. Dependency Inversion - high-level modules depend on abstractions
     *
     * DATA FLOW:
     * WebScraper -> Repository.SaveAsync() [interface dispatch]
     * CronScheduler -> Repository.GetActiveJobsAsync()
     * MainForm UI -> Repository.GetHistoryAsync(), GetCleanTableAsync()
     * BulkHtmlScraper -> Repository.SaveBulkAsync(), GetFieldMappingsAsync()
     * ======================================================================== */

    public interface IDataRepository
    {
        Task<List<ScrapeJob>> GetActiveJobsAsync(string? portionType = null);
    Task<List<ScrapeJob>> GetAllJobsByTypeAsync(string portionType);
        Task SaveAsync(DataPoint point);
        Task SaveBulkAsync(List<BulkRecord> records);
        Task<DataTable> GetHistoryAsync(int? targetId = null);
        Task<List<FieldMapping>> GetFieldMappingsAsync(int targetId);
        Task ExecuteDDLAsync(string safeDdlStatement);
        Task<DataTable> GetCleanTableAsync(string exactTableName);
        Task<DataTable> GetTargetsAsDataTableAsync();
        Task<List<ScrapeJob>> GetAllJobsAsync();
        Task<Dictionary<string, object>> GetSummaryStatsAsync();
    }
}
