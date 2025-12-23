using Declarify.Data;
using Declarify.Models;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class ReviewHelperService : IReviewHelperService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ReviewHelperService> _logger;

        public ReviewHelperService(ApplicationDbContext db, ILogger<ReviewHelperService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Checks if the given employee (manager) can review a specific task
        /// </summary>
        public async Task<bool> CanManagerReviewAsync(int employeeId, int taskId)
        {
            try
            {
                var submission = await _db.DOIFormSubmissions
                    .Include(s => s.Task)
                    .FirstOrDefaultAsync(s => s.FormTaskId == taskId);

                if (submission == null)
                {
                    _logger.LogWarning("No submission found for taskId {TaskId}", taskId);
                    return false;
                }

                // Check if the employee is the assigned manager
                if (submission.AssignedManagerId != employeeId)
                {
                    _logger.LogWarning(
                        "Employee {EmployeeId} is not authorized to review task {TaskId}. Assigned to {AssignedManagerId}",
                        employeeId, taskId, submission.AssignedManagerId);
                    return false;
                }

                // Check if status allows review
                if (submission.Status != "Submitted" && submission.Status != "Pending")
                {
                    _logger.LogWarning(
                        "Submission {SubmissionId} for task {TaskId} has status {Status}, cannot be reviewed",
                        submission.SubmissionId, taskId, submission.Status);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking review authorization for employee {EmployeeId} and task {TaskId}",
                    employeeId, taskId);
                return false;
            }
        }

        /// <summary>
        /// Gets the submission associated with a task ID
        /// </summary>
        public async Task<FormSubmission?> GetSubmissionByTaskIdAsync(int taskId)
        {
            try
            {
                return await _db.DOIFormSubmissions
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Employee)
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Template)
                    .FirstOrDefaultAsync(s => s.FormTaskId == taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving submission for task {TaskId}", taskId);
                return null;
            }
        }

        /// <summary>
        /// Checks if a submission exists and is ready for review
        /// </summary>
        public async Task<bool> IsSubmissionReadyForReviewAsync(int taskId)
        {
            try
            {
                var submission = await _db.DOIFormSubmissions
                    .FirstOrDefaultAsync(s => s.FormTaskId == taskId);

                if (submission == null)
                {
                    _logger.LogDebug("No submission found for task {TaskId}", taskId);
                    return false;
                }

                // Valid statuses for review
                var reviewableStatuses = new[] { "Submitted", "Pending" };
                return reviewableStatuses.Contains(submission.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking submission readiness for task {TaskId}", taskId);
                return false;
            }
        }

        /// <summary>
        /// Gets all task IDs that have pending submissions for a specific manager
        /// Used to show review counts in dashboard
        /// </summary>
        public async Task<List<int>> GetPendingReviewTaskIdsForManagerAsync(int managerId)
        {
            try
            {
                var taskIds = await _db.DOIFormSubmissions
                    .Where(s => s.AssignedManagerId == managerId &&
                               (s.Status == "Submitted" || s.Status == "Pending"))
                    .Select(s => s.FormTaskId)
                    .ToListAsync();

                _logger.LogInformation(
                    "Manager {ManagerId} has {Count} submissions pending review",
                    managerId, taskIds.Count);

                return taskIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending review tasks for manager {ManagerId}", managerId);
                return new List<int>();
            }
        }
    }
}
