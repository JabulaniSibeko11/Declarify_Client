using Declarify.Models;

namespace Declarify.Services
{
    public interface ISubmissionService
    {
        Task<FormSubmission> SaveDraftAsync(int taskId, string formDataJson);
        Task<FormSubmission> SubmitAsync(int taskId, string formDataJson); // Consumes 1 credit (FR 4.4.3)
        Task<FormSubmission> GetByTaskIdAsync(int taskId);
        Task<FormSubmission> GetByIdAsync(int submissionId);
        Task<bool> ReviewerSignOffAsync(int submissionId, string reviewerSignatureData); // FR 4.5.4
    }
}
