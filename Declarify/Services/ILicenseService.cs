namespace Declarify.Services
{
    public interface ILicenseService
    {
        Task<bool> IsLicenseValidAsync(); // Daily check or on Admin login (NFR 5.2.1)
        Task<DateTime> GetExpiryDateAsync(); // February 1st logic (NFR 5.2.2)
        Task SyncWithCentralHubAsync(); // Pulls latest status (NFR 5.2.5)
        Task<string> GetLicenseStatusMessageAsync(); // For display when expired (NFR 5.2.3)

        Task SyncLicenseFromCentralAsync(string licenseKey, int companyId, DateTime expiryDate, bool isActive);

        Task<string> GetInstanceIdAsync();
    }
}
