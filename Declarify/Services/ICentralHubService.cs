using Declarify.Models;
using System.Threading.Tasks;

namespace Declarify.Services
{
    public interface ICentralHubService
    {
        /// <summary>
        /// Check available credit balance from Central Hub
        /// </summary>
        Task<CreditBalanceResult> CheckCreditBalance();

        /// <summary>
        /// Get company information registered in Central Hub
        /// </summary>
        Task<ClientCompanies> GetCompanyInformation();

        /// <summary>
        /// Get company administrator details from Central Hub
        /// </summary>
        Task<CompanyAdministrators> GetCompanyAdministrators();

        /// <summary>
        /// Sync license with Central Hub
        /// </summary>
        Task SyncLicenseAsync();

        /// <summary>
        /// Purchase credits via Central Hub
        /// </summary>
        /// <param name="amount">Number of credits to purchase</param>
        Task<PurchaseResult> PurchaseCreditsAsync(int amount);

        /// <summary>
        /// Retrieve current license information
        /// </summary>
        Task<LicenseResult> GetLicenseInfoAsync();
    }


// Supporting result classes
public class CreditBalanceResult
    {
        public int currentBalance { get; set; }
        public bool success { get; set; }
        public string message { get; set; }
    }

    public class PurchaseResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int NewBalance { get; set; }
    }

    public class LicenseResult
    {
        public bool IsValid { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; }
    }
}