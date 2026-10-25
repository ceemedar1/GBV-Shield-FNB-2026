using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Models;
using SQLite;

namespace GBV_Shield.Services
{
    public class LocalReportsStore
    {
        private readonly SQLiteAsyncConnection _db;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _initialized;

        public LocalReportsStore(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
        }

        private async Task InitializeAsync()
        {
            if (_initialized) return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_initialized) return;
                await _db.CreateTableAsync<Report>();
                _initialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        public async Task<int> AddReportAsync(Report report)
        {
            await InitializeAsync();
            return await _db.InsertAsync(report);
        }

        public async Task<List<Report>> GetPendingReportsAsync()
        {
            await InitializeAsync();
            return await _db.Table<Report>().Where(r => !r.Synced).ToListAsync();
        }

        public async Task<int> RemoveReportAsync(int id)
        {
            await InitializeAsync();
            return await _db.ExecuteAsync("DELETE FROM Report WHERE Id=?", id);
        }
    }
}
