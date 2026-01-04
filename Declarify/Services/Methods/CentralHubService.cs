using Declarify.Models;

using System.Text.Json;

namespace Declarify.Services
{
    public class CentralHubService : ICentralHubService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CentralHubService> _logger;
        private readonly string _centralHubBaseUrl;
        private readonly string _apiKey;

        public CentralHubService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CentralHubService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Get central hub configuration
            _centralHubBaseUrl = _configuration["CentralHub:BaseUrl"] ?? "https://centralhub.declarify.com/api";
            _apiKey = _configuration["CentralHub:ApiKey"] ?? throw new InvalidOperationException("Central Hub API Key not configured");

            // Configure HttpClient
            _httpClient.BaseAddress = new Uri(_centralHubBaseUrl);
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        public async Task<CreditBalanceResult> CheckCreditBalance()
        {
            try
            {
                var response = await _httpClient.GetAsync("/credits/balance");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CreditBalanceResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new CreditBalanceResult { success = false, message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking credit balance from central hub");
                return new CreditBalanceResult
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    currentBalance = 0
                };
            }
        }

        public async Task<ClientCompanies> GetCompanyInformation()
        {
            try
            {
                var response = await _httpClient.GetAsync("/company/info");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var companyInfo = JsonSerializer.Deserialize<ClientCompanies>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return companyInfo ?? new ClientCompanies { CompanyName = "Unknown Company" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching company information from central hub");
                return new ClientCompanies
                {
                    CompanyName = "Unknown Company",
                    CompanyRegistration = "N/A",
                    Domain = "N/A",
                    RegisteredDate = DateTime.UtcNow
                };
            }
        }

        public async Task<CompanyAdministrators> GetCompanyAdministrators()
        {
            try
            {
                var response = await _httpClient.GetAsync("/admin/info");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var adminInfo = JsonSerializer.Deserialize<CompanyAdministrators>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return adminInfo ?? new CompanyAdministrators { AdminName = "Admin" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching admin information from central hub");
                return new CompanyAdministrators
                {
                    AdminName = "Admin",
                    AdminEmail = "admin@company.com",
                    Role = "Administrator"
                };
            }
        }

        public async Task SyncLicenseAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/license/sync", null);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("License synced successfully with central hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing license with central hub");
                throw;
            }
        }

        public async Task<PurchaseResult> PurchaseCreditsAsync(int amount)
        {
            try
            {
                var requestBody = new { amount };
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("/credits/purchase", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PurchaseResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new PurchaseResult { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purchasing credits from central hub");
                return new PurchaseResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    NewBalance = 0
                };
            }
        }

        public async Task<LicenseResult> GetLicenseInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/license/info");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LicenseResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return result ?? new LicenseResult { IsValid = false, Status = "Unknown" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching license info from central hub");
                return new LicenseResult
                {
                    IsValid = false,
                    Status = "Error",
                    ExpiryDate = DateTime.UtcNow
                };
            }
        }
    }
}