using Declarify.Data;
using Declarify.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Declarify.Services.Methods
{
    public class VerificationService : IVerificationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICreditService _creditService;
        private readonly HttpClient _httpClient;
        private readonly string _centralServerBaseUrl;

        public VerificationService(
            ApplicationDbContext dbContext,
            ICreditService creditService,
            IHttpClientFactory httpClientFactory,
            IOptions<VerificationSettings> settings) // Inject options
        {
            _dbContext = dbContext;
            _creditService = creditService;
            _httpClient = httpClientFactory.CreateClient("CentralServerClient");
            _centralServerBaseUrl = settings.Value.CentralServerBaseUrl
                                    ?? throw new ArgumentNullException(nameof(settings.Value.CentralServerBaseUrl));
        }

        public async Task<VerificationResult> PerformCipcCheckAsync(int submissionId, string entityName)
        {
            const int creditsRequired = 5;

            if (!await _creditService.HasSufficientCreditsAsync(creditsRequired))
                throw new InvalidOperationException("Insufficient credits for CIPC check.");

            var payload = new { SubmissionId = submissionId, EntityName = entityName };
            await CallExternalApiAsync("/verification/cipc", payload);

            var verificationResult = new VerificationResult(); // Replace with actual deserialization
            // await _creditService.ConsumeCreditsAsync(creditsRequired);
            // await StoreVerificationResultAsync(submissionId, verificationResult);

            return verificationResult;
        }

        public async Task<VerificationResult> PerformCreditCheckAsync(int submissionId, string entityName)
        {
            const int creditsRequired = 10;

            if (!await _creditService.HasSufficientCreditsAsync(creditsRequired))
                throw new InvalidOperationException("Insufficient credits for credit check.");

            var payload = new { SubmissionId = submissionId, EntityName = entityName };
            await CallExternalApiAsync("/verification/credit", payload);

            var verificationResult = new VerificationResult(); // Replace with actual deserialization
            // await _creditService.ConsumeCreditsAsync(creditsRequired);
            await StoreVerificationResultAsync(submissionId, verificationResult);

            return verificationResult;
        }

        public async Task<List<string>> SuggestEntitiesFromFormAsync(string formDataJson)
        {
            var formData = JsonSerializer.Deserialize<Dictionary<string, object>>(formDataJson);
            var suggestedEntities = new List<string>();

            if (formData.TryGetValue("Directorships", out var directorshipsObj) && directorshipsObj is JsonElement directorshipsElem)
            {
                if (directorshipsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in directorshipsElem.EnumerateArray())
                    {
                        if (item.TryGetProperty("CompanyName", out var companyName))
                            suggestedEntities.Add(companyName.GetString());
                    }
                }
            }

            if (formData.TryGetValue("SharesAndSecurities", out var sharesObj) && sharesObj is JsonElement sharesElem)
            {
                if (sharesElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in sharesElem.EnumerateArray())
                    {
                        if (item.TryGetProperty("EntityName", out var entityName))
                            suggestedEntities.Add(entityName.GetString());
                    }
                }
            }

            return suggestedEntities.Distinct().ToList();
        }

        public async Task CallExternalApiAsync(string endpoint, object payload)
        {
            var fullUrl = $"{_centralServerBaseUrl}{endpoint}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(fullUrl, content);
            response.EnsureSuccessStatusCode();
        }

        private async Task StoreVerificationResultAsync(int submissionId, VerificationResult result)
        {
            var attachment = new VerificationAttachment
            {
                SubmissionId = submissionId,
                ResultJson = JsonSerializer.Serialize(result),
                CreatedAt = DateTime.UtcNow,
            };

            // _dbContext.VerificationAttachments.Add(attachment);
            await _dbContext.SaveChangesAsync();
        }
    }

    // Strongly-typed options class
    public class VerificationSettings
    {
        public string CentralServerBaseUrl { get; set; } = string.Empty;
    }
}
