using Declarify.Data;
using Declarify.Helper;
using Declarify.Models;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class LicenseService : ILicenseService
    {

        private readonly ApplicationDbContext _db;
        private readonly ILogger<LicenseService> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public LicenseService(
             ApplicationDbContext context,
             IHttpClientFactory httpClientFactory,
             ILogger<LicenseService> logger,
             IConfiguration configuration)
        {
            _db = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = configuration;
        }

        // Check if current license is valid (NFR 5.2.1)
        public async Task<bool> IsLicenseValidAsync()
        {
            var license = await GetCurrentLicenseAsync();

            if (license == null)
            {
                _logger.LogWarning("No license found in database");
                return false;
            }

            var now = DateTime.UtcNow;
            var isValid = license.IsActive && license.ExpiryDate > now;

            if (!isValid)
            {
                _logger.LogWarning($"License is invalid. Active: {license.IsActive}, Expiry: {license.ExpiryDate:yyyy-MM-dd}");
            }

            return isValid;
        }


        // Get expiry date of current license (NFR 5.2.2)

        public async Task<DateTime> GetExpiryDateAsync()
        {
            var license = await GetCurrentLicenseAsync();
            return license?.ExpiryDate ?? DateTime.MinValue;
        }

        // Sync license and credit information with central hub (NFR 5.2.5)
        public async Task SyncWithCentralHubAsync()
        {
            try
            {
                var hubUrl = _config["CentralHub:BaseUrl"];
                var instanceId = _config["Instance:Id"];
                var apiKey = _config["CentralHub:ApiKey"];

                if (string.IsNullOrEmpty(hubUrl) || string.IsNullOrEmpty(instanceId))
                {
                    _logger.LogError("Central hub configuration is missing");
                    return;
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("X-Instance-Id", instanceId);
                httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

                // Fetch license status from central hub
                var licenseResponse = await httpClient.GetAsync($"{hubUrl}/api/licensing/instance/{instanceId}");

                if (!licenseResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to sync with central hub. Status: {licenseResponse.StatusCode}");
                    return;
                }

                var licenseData = await licenseResponse.Content.ReadFromJsonAsync<LicenseSyncResponse>();

                if (licenseData == null)
                {
                    _logger.LogError("Failed to deserialize license data from central hub");
                    return;
                }

                // Update local license record
                var currentLicense = await GetCurrentLicenseAsync();

                if (currentLicense == null)
                {
                    currentLicense = new License
                    {
                        LicenseKey = licenseData.LicenseKey,
                        ExpiryDate = licenseData.ExpiryDate,
                        IsActive = licenseData.IsActive
                    };
                    _db.Licenses.Add(currentLicense);
                }
                else
                {
                    currentLicense.LicenseKey = licenseData.LicenseKey;
                    currentLicense.ExpiryDate = licenseData.ExpiryDate;
                    currentLicense.IsActive = licenseData.IsActive;
                }

                // Sync credit batches if provided
                if (licenseData.NewCreditBatches != null && licenseData.NewCreditBatches.Any())
                {
                    foreach (var batch in licenseData.NewCreditBatches)
                    {
                        // Check if batch already exists
                        var existingBatch = await _db.Credits
                            .FirstOrDefaultAsync(c => c.CreditId == batch.ExternalCreditId);

                        if (existingBatch == null)
                        {
                            var newBatch = new Credit
                            {
                                BatchAmount = batch.Amount,
                                RemainingAmount = batch.Amount,
                                LoadDate = batch.LoadDate,
                                ExpiryDate = batch.ExpiryDate
                            };
                            _db.Credits.Add(newBatch);
                            _logger.LogInformation($"Synced new credit batch: {batch.Amount} credits");
                        }
                    }
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Successfully synced with central hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing with central hub");
                throw;
            }
        }

        // Get license status message for display when expired (NFR 5.2.3)
        public async Task<string> GetLicenseStatusMessageAsync()
        {
            var isValid = await IsLicenseValidAsync();

            if (isValid)
            {
                var expiryDate = await GetExpiryDateAsync();
                var daysRemaining = (expiryDate - DateTime.UtcNow).Days;

                if (daysRemaining <= 30)
                {
                    return $"License expires in {daysRemaining} days. Please renew soon.";
                }

                return $"License active until {expiryDate:MMMM d, yyyy}";
            }

            // License expired or inactive
            return "Account requires renewal. Please contact your vendor.";
        }

        // Get current license record from database
        private async Task<License?> GetCurrentLicenseAsync()
        {
            // Assuming single license per instance
            return await _db.Licenses
                .OrderByDescending(l => l.ExpiryDate)
                .FirstOrDefaultAsync();
        }

        // Set license as expired (called by scheduled job or manual override)
        public async Task MarkLicenseAsExpiredAsync()
        {
            var license = await GetCurrentLicenseAsync();
            if (license != null)
            {
                license.IsActive = false;
                await _db.SaveChangesAsync();
                _logger.LogWarning("License marked as expired");
            }
        }
        //Save the license on the client local db
        public async Task SyncLicenseFromCentralAsync(string licenseKey, int companyId, DateTime expiryDate, bool isActive)
        {
            var currentLicense = await GetCurrentLicenseAsync();

            if (currentLicense == null)
            {
                currentLicense = new License
                {
                    LicenseKey = DataProtection.Encrypt(licenseKey),
                    InstanceId = DataProtection.Encrypt(companyId.ToString()),
                    ExpiryDate = expiryDate,
                    IsActive = isActive
                };
                _db.Licenses.Add(currentLicense);
            }
            else
            {
                currentLicense.LicenseKey = DataProtection.Encrypt(licenseKey);
                currentLicense.InstanceId = DataProtection.Encrypt(companyId.ToString());
                currentLicense.ExpiryDate = expiryDate;
                currentLicense.IsActive = isActive;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("License record updated from central activation");
        }
        public async Task<string> GetInstanceIdAsync()
        {
            var license = await _db.Licenses.FirstOrDefaultAsync();
            return license != null ? DataProtection.Decrypt(license.InstanceId) : string.Empty;
        }




        //Save the license on the client local db
        public async Task SyncLicenseFromCentralAsync(string licenseKey, int companyId, DateTime expiryDate, bool isActive)
        {
            var currentLicense = await GetCurrentLicenseAsync();

            if (currentLicense == null)
            {
                currentLicense = new License
                {
                    LicenseKey = DataProtection.Encrypt(licenseKey),
                    InstanceId = DataProtection.Encrypt(companyId.ToString()),
                    ExpiryDate = expiryDate,
                    IsActive = isActive
                };
                _db.Licenses.Add(currentLicense);
            }
            else
            {
                currentLicense.LicenseKey = DataProtection.Encrypt(licenseKey);
                currentLicense.InstanceId = DataProtection.Encrypt(companyId.ToString());
                currentLicense.ExpiryDate = expiryDate;
                currentLicense.IsActive = isActive;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("License record updated from central activation");
        }


        public async Task<string> GetLicenseKeyAsync()
        {
            var license = await _db.Licenses.FirstOrDefaultAsync();
            return license != null ? DataProtection.Decrypt(license.LicenseKey) : string.Empty;
        }

        public async Task<string> GetInstanceIdAsync()
        {
            var license = await _db.Licenses.FirstOrDefaultAsync();
            return license != null ? DataProtection.Decrypt(license.InstanceId) : string.Empty;
        }

    }
}
