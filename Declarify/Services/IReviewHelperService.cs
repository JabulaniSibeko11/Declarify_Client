using Declarify.Models;

namespace Declarify.Services
{
    public interface IReviewHelperService
    {

        Task<bool> CanManagerReviewAsync(int employeeId, int taskId);
        Task<FormSubmission?> GetSubmissionByTaskIdAsync(int taskId);
        Task<bool> IsSubmissionReadyForReviewAsync(int taskId);
        Task<List<int>> GetPendingReviewTaskIdsForManagerAsync(int managerId);
    }
}
