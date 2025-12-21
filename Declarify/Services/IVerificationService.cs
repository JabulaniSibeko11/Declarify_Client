using Declarify.Models;

namespace Declarify.Services
{
    public interface IVerificationService
    {
        /// <summary>
        /// Performs a CIPC verification check. Consumes 5 credits.
        /// </summary>
        /// <param name="submissionId">The ID of the submission.</param>
        /// <param name="entityName">The entity/company name to verify.</param>
        /// <returns>A VerificationResult object containing the verification outcome.</returns>
        Task<VerificationResult> PerformCipcCheckAsync(int submissionId, string entityName);

        /// <summary>
        /// Performs a credit check verification. Consumes 10 credits.
        /// </summary>
        /// <param name="submissionId">The ID of the submission.</param>
        /// <param name="entityName">The entity/company name to verify.</param>
        /// <returns>A VerificationResult object containing the verification outcome.</returns>
        Task<VerificationResult> PerformCreditCheckAsync(int submissionId, string entityName);

        /// <summary>
        /// Suggests entities extracted from form JSON data.
        /// </summary>
        /// <param name="formDataJson">The form data in JSON format.</param>
        /// <returns>A list of suggested entity names.</returns>
        Task<List<string>> SuggestEntitiesFromFormAsync(string formDataJson);

        /// <summary>
        /// Calls an external API endpoint with the given payload.
        /// </summary>
        /// <param name="endpoint">The relative API endpoint (e.g., "/verification/cipc").</param>
        /// <param name="payload">The payload object to send as JSON.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        Task CallExternalApiAsync(string endpoint, object payload);
    }
}
