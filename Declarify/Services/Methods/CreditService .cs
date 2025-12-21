using Declarify.Data;
using Declarify.Models;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class CreditService :ICreditService
    {

        private readonly ApplicationDbContext _db;
        private readonly ILogger<CreditService> _logger;

        public CreditService(ApplicationDbContext db, ILogger<CreditService> logger)
        {
            _db = db;
            _logger = logger;
        }
        public async Task<int> GetAvailableCreditsAsync()
        {
            var now = DateTime.UtcNow;
            var totalCredits = await _db.Credits
                .Where(c => c.ExpiryDate > now && c.RemainingAmount > 0)
                .SumAsync(c => c.RemainingAmount);

            return totalCredits;
        }



        // ================================
        // CURRENT CREDIT BALANCE (FR 4.4.3)
        // ================================

        public async Task<int> GetCurrentBalanceAsync()
        {
            await CleanupExpiredCreditsAsync();

            return await _db.Credits
                .SumAsync(c => c.RemainingAmount);
        }

        // ==================================
        // CREDIT BATCHES (FIFO VIEW)
        // ==================================

        public async Task<List<CreditBatchInfo>> GetCreditBatchesAsync()
        {
            var now =DateTime.UtcNow;
            var batches = await _db.Credits
                .OrderByDescending(c => c.LoadDate)
                .Select(c => new CreditBatchInfo
                {
                    CreditId = c.CreditId,
                    BatchAmount = c.BatchAmount,
                    RemainingAmount = c.RemainingAmount,
                    ConsumedAmount = c.BatchAmount - c.RemainingAmount,
                    LoadDate = c.LoadDate,
                    ExpiryDate = c.ExpiryDate,
                    IsExpired = c.ExpiryDate <= now,
                    DaysUntilExpiry = (c.ExpiryDate - now).Days

                }).ToListAsync();

            return batches;
          
        }

        // ==================================
        // CREDIT CHECK (CRITICAL GATE)
        // ==================================
        // Used before DOI submission (FR 4.4.3) and verification calls (FR 4.6.3)
        public async Task<bool> HasSufficientCreditsAsync(int requiredAmount)
        {
            var available = await GetAvailableCreditsAsync();
            return available >= requiredAmount;
        }

        // ==================================
        // CREDIT CONSUMPTION (FIFO)
        // FR 4.4.3 & FR 4.6.3
        // ==================================
        // CRITICAL: Must be called within a transaction with submission/verification

        public async Task<bool> ConsumeCreditsAsync(int amount, string reason)
        {
            if (!await HasSufficientCreditsAsync(amount))
            {
                _logger.LogWarning($"Insufficient credits. Required: {amount}, Available: {await GetAvailableCreditsAsync()}");
                return false;
            }

            var now = DateTime.UtcNow;
            var creditBatches = await _db.Credits
                .Where(c => c.ExpiryDate > now && c.RemainingAmount > 0)
                .OrderBy(c => c.LoadDate) // FIFO
                .ToListAsync();

            int remainingToConsume = amount;

            foreach (var batch in creditBatches)
            {
                if (remainingToConsume <= 0) break;

                int consumed = Math.Min(batch.RemainingAmount, remainingToConsume);
                batch.RemainingAmount -= consumed;
                remainingToConsume -= consumed;

                _logger.LogInformation($"Consumed {consumed} credits from batch {batch.CreditId} for: {reason}");
            }

            await _db.SaveChangesAsync();
            return true;
        }

        // ==================================
        // CENTRAL HUB SYNC (NFR 5.2.5)
        // ==================================

        public async Task<Credit> LoadCreditBatchAsync(int amount, DateTime? customExpiryDate = null)
        {
            var loadDate = DateTime.UtcNow;
            var expiryDate = customExpiryDate ?? loadDate.AddYears(1); // 12 months expiry (NFR 5.2.4)

            var creditBatch = new Credit
            {
                BatchAmount = amount,
                RemainingAmount = amount,
                LoadDate = loadDate,
                ExpiryDate = expiryDate
            };

            _db.Credits.Add(creditBatch);
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Loaded new credit batch: {amount} credits, expires on {expiryDate:yyyy-MM-dd}");
            return creditBatch;
        }
        // ==================================
        // CREDIT EXPIRY CLEANUP (NFR 5.2.4)
        // ==================================

        public async Task CleanupExpiredCreditsAsync()
        {
            var now = DateTime.UtcNow;

            var expiredCredits = await _db.Credits
                .Where(c => c.ExpiryDate < now && c.RemainingAmount > 0)
                .ToListAsync();

            if (!expiredCredits.Any())
                return;

            foreach (var credit in expiredCredits)
            {
                credit.RemainingAmount = 0;
            }

            await _db.SaveChangesAsync();
        }

        // Get credits expiring within the next N days for proactive alerts

        public async Task<List<Credit>> GetExpiringCreditsAsync(int daysThreshold = 30)
        {
            var now = DateTime.UtcNow;
            var thresholdDate = now.AddDays(daysThreshold);

            return await _db.Credits
                .Where(c => c.ExpiryDate > now && c.ExpiryDate <= thresholdDate && c.RemainingAmount > 0)
                .OrderBy(c => c.ExpiryDate)
                .ToListAsync();
        }

        /// <summary>
        /// Clean up expired credit batches (optional maintenance task)
        /// </summary>
        public async Task<int> ArchiveExpiredCreditsAsync()
        {
            var now = DateTime.UtcNow;
            var expiredBatches = await _db.Credits
                .Where(c => c.ExpiryDate <= now)
                .ToListAsync();

            // Optionally move to archive table or just log
            var expiredCount = expiredBatches.Count;
            _logger.LogInformation($"Found {expiredCount} expired credit batches");

            return expiredCount;
        }

    }
}
