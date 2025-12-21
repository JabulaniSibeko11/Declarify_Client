using Declarify.Models;

namespace Declarify.Services
{
    public interface ICreditService
    {
        Task<int> GetAvailableCreditsAsync();
        Task<bool> HasSufficientCreditsAsync(int requiredAmount);
        Task<bool> ConsumeCreditsAsync(int amount, string reason);
        Task<Credit> LoadCreditBatchAsync(int amount, DateTime? customExpiryDate = null);
        Task<List<CreditBatchInfo>> GetCreditBatchesAsync();
        Task<List<Credit>> GetExpiringCreditsAsync(int daysThreshold = 30);
        Task<int> ArchiveExpiredCreditsAsync();
    }
}
