using System.ComponentModel.DataAnnotations;
using Declarify.Services.Methods;

namespace Declarify.Services.API
{
    public class CentralHubApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _apiKey;
        private readonly ILicenseService _LS;
        private readonly ILogger<CentralHubApiService> _logger;

        public CentralHubApiService(HttpClient httpClient,IConfiguration configuration,IHttpContextAccessor httpContextAccessor, ILicenseService licenseService, ILogger<CentralHubApiService> logger)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _LS = licenseService;
            _logger = logger;

            // Base URL 
            var baseUrl = configuration["CentralHub:BaseUrl"] ?? throw new InvalidOperationException("CentralHub:BaseUrl is missing in configuration");

            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

        }

        // Adds X-Company-Code from current logged-in user on every request
        private async Task EnsureCompanyCodeHeaderAsync()
        {
            var companyIdStr = await _LS.GetInstanceIdAsync();

            //companyIdStr = "1";

            if (string.IsNullOrEmpty(companyIdStr) || !int.TryParse(companyIdStr, out _))
            {
                throw new UnauthorizedAccessException("Missing or invalid CompanyId claim - must be numeric");
            }

            _httpClient.DefaultRequestHeaders.Remove("X-Company-Code");
            _httpClient.DefaultRequestHeaders.Add("X-Company-Code", companyIdStr);
        }

        public async Task<T> GetAsync<T>(string endpoint) where T: class
        {
            await EnsureCompanyCodeHeaderAsync();

            try
            {
                var response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {

                    return await response.Content.ReadFromJsonAsync<T>();
                }

                _logger.LogWarning("Central GET failed for {Endpoint}: {StatusCode}", endpoint, response.StatusCode);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling central GET {Endpoint}", endpoint);
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout calling central GET {Endpoint}", endpoint);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling central GET {Endpoint}", endpoint);
                return null;
            }
        }



        public async Task<T> PostAsync<T>(string endpoint, object data) where T : class
        {
            await EnsureCompanyCodeHeaderAsync();
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, data);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>();
                }

                _logger.LogWarning("Central POST failed for {Endpoint}: {StatusCode}", endpoint, response.StatusCode);
                return null;

            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error calling central POST {Endpoint}", endpoint);
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout calling central POST {Endpoint}", endpoint);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling central POST {Endpoint}", endpoint);
                return null;
            }
        }

        //  API CALLS
        //1.Test Communication API
        public async Task<object> PingAsync()
        {
            var result = await GetAsync<object>("api/core/ping");

            return result ?? new { Message = "Ping failed - central unreachable" };
        }

        //2. Activate license API
        public async Task<ActivationResponse?> ActivateLicenseAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return null;
            }

            try
            {
                var requestUrl = $"api/core/validate/{Uri.EscapeDataString(licenseKey.Trim())}";

                var response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ActivationResponse>();
                }

                return new ActivationResponse
                {
                    isValid = false,
                    message = response.StatusCode == System.Net.HttpStatusCode.NotFound ? "Invalid license key" : $"Server error: {response.StatusCode}"
                };
            }
            catch (HttpRequestException)
            {
                return new ActivationResponse
                {
                    isValid = false,
                    message = "Cannot reach license server. Check your internet connection."
                };
            }
            catch (Exception ex)
            {
                return new ActivationResponse
                {
                    isValid = false,
                    message = "Unexpected error during activation."
                };
            }
        }

        //3. Check if license is valid API
        public async Task<LicenseCheckResponse> CheckLicenseAsync()
        {
            var result = await GetAsync<LicenseCheckResponse>("api/core/check-license");

            return result ?? new LicenseCheckResponse
            {
                IsValid = false,
                Message = "Cannot reach license server. Please check connection."
            };
        }


        //4. Check company credits API
        public Task<CreditCheckResponse> CheckCreditBalance() => GetAsync<CreditCheckResponse>("api/core/check-credits");
        //public async Task<CreditCheckResponse> CheckCreditBalance()
        //{
        //    var result = await GetAsync<CreditCheckResponse>("api/core/check-credits");

        //    return result ?? new CreditCheckResponse
        //    {
        //        hasCredits = false,
        //        lowCreditWarning = true,
        //        currentBalance = 0,
        //        totalPurchased = 0,
        //        totalUsed = 0
        //    };
        //}

        //5. Consume company credits API
        public async Task<ConsumeCreditsResponse> ConsumeCredits(int creditsToConsume, string? reason = null)
        {
            var request = new CreditConsumptionRequest
            {
                CreditsToConsume = creditsToConsume,
                Reason = reason
            };

            var result = await PostAsync<ConsumeCreditsResponse>("api/core/consume-credits", request);

            return result ?? new ConsumeCreditsResponse
            {
                Success = false,
                Error = "Cannot reach credit server. Please try again.",
                RemainingBalance = 0
            };

        }



    }

    public class ActivationResponse
    {
        public bool isValid { get; set; }
        public int companyId { get; set; }
        public string companyName { get; set; }
        public string emailDomain { get; set; }
        public string message { get; set; }

        public DateTime ExpiryDate { get; set; }
        public int daysUntilExpiry { get; set; }
        public bool isExpired { get; set; }

        //--Admin info --\\
        public string? FirstName { get; set; }
        public string? Surname { get; set; }

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? JobTitle { get; set; }

        public string? Department { get; set; }

        public string? FullName => $"{FirstName} {Surname}";


    }

    public class LicenseCheckResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int MaxUsers { get; set; }
        public string CompanyName { get; set; } = string.Empty;
    }

    public class CreditCheckResponse
    {
        public bool hasCredits { get; set; }
        public bool lowCreditWarning { get; set; }

        public int currentBalance { get; set; }
        public int totalPurchased { get; set; }
        public int totalUsed { get; set; }

    }

    public class CreditConsumptionRequest
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Credits to consume must be at least 1")]
        public int CreditsToConsume { get; set; }

        public string Reason { get; set; } = string.Empty; // optional but useful

    }

    public class ConsumeCreditsResponse
    {
        public bool Success { get; set; }
        public decimal CreditsConsumed { get; set; }
        public decimal RemainingBalance { get; set; }
        public string? Error { get; set; }
    }



}
