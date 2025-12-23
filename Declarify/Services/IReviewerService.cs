using Declarify.Models;
using Declarify.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Declarify.Services
{
    public interface IReviewerService
    {
        /// <summary>
        /// Checks if the employee is a line manager (has direct subordinates).
        /// </summary>
        Task<bool> IsLineManagerAsync(int employeeId);

        /// <summary>
        /// Gets compliance data for direct subordinates of the manager.
        /// </summary>
        Task<List<SubordinateComplianceViewModel>> GetSubordinateComplianceAsync(int managerId);

        /// <summary>
        /// Gets a specific submission for review.
        /// </summary>
        Task<FormSubmission> GetSubmissionForReviewAsync(int submissionId, int reviewerId);  // Ensures reviewer owns it

        /// <summary>
        /// Initiates an external API verification (e.g., CIPC or Credit check), consuming credits.
        /// </summary>
        Task<VerificationResult> InitiateVerificationAsync(int submissionId, string verificationType, int reviewerId);

        /// <summary>
        /// Digitally signs off a submission, updating status to "Reviewed".
        /// </summary>
        Task<SignOffResult> SignOffSubmissionAsync(int submissionId, string signature, string notes, int reviewerId);
    }
}
