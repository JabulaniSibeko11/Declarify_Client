using Declarify.Models;
using Declarify.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Declarify.Services
{
    public class SubmissionService : ISubmissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICreditService _creditService;
        private readonly ILogger<SubmissionService> _logger;

        public SubmissionService(
            ApplicationDbContext context,
            ICreditService creditService,
            ILogger<SubmissionService> logger)
        {
            _context = context;
            _creditService = creditService;
            _logger = logger;
        }

        /// <summary>
        /// FR 4.4.1: Save draft without consuming credits
        /// Employees can save progress without submitting
        /// </summary>
        public async Task<FormSubmission> SaveDraftAsync(int taskId, string formDataJson)
        {
            try
            {
                // Validate task exists
                var task = await _context.DOITasks
                    .Include(t => t.Employee)
                    .FirstOrDefaultAsync(t => t.TaskId == taskId);

                if (task == null)
                {
                    throw new ArgumentException($"Task with ID {taskId} not found.");
                }

                // Check if draft already exists
                var existingSubmission = await _context.DOIFormSubmissions
                    .FirstOrDefaultAsync(s => s.FormTaskId == taskId && s.Status == "Draft");

                if (existingSubmission != null)
                {
                    // Update existing draft
                    existingSubmission.FormData = formDataJson;
                    existingSubmission.Submitted_Date = DateTime.UtcNow;
                    _context.DOIFormSubmissions.Update(existingSubmission);

                    _logger.LogInformation(
                        "Draft updated for TaskId: {TaskId}, Employee: {EmployeeId}",
                        taskId,
                        task.EmployeeId);
                }
                else
                {
                    // Create new draft
                    existingSubmission = new FormSubmission
                    {
                        FormTaskId = taskId,
                        FormData = formDataJson,
                        Status = "Draft",
                        Submitted_Date = DateTime.UtcNow,
                        DigitalAttestation = null // Not attested yet
                    };

                    await _context.DOIFormSubmissions.AddAsync(existingSubmission);

                    _logger.LogInformation(
                        "New draft created for TaskId: {TaskId}, Employee: {EmployeeId}",
                        taskId,
                        task.EmployeeId);
                }

                await _context.SaveChangesAsync();
                return existingSubmission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving draft for TaskId: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// FR 4.4.3: Submit form and consume 1 credit
        /// CRITICAL: No submission allowed if credit balance < 1
        /// FR 4.4.2: Record digital attestation
        /// </summary>
        public async Task<FormSubmission> SubmitAsync(int taskId, string formDataJson)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // FR 4.4.3: CRITICAL - Check credit balance BEFORE allowing submission
                var availableCredits = await _creditService.GetAvailableCreditsAsync();

                if (availableCredits < 1)
                {
                    _logger.LogWarning(
                        "Submission blocked for TaskId: {TaskId}. Insufficient credits: {Credits}",
                        taskId,
                        availableCredits);

                    throw new InvalidOperationException(
                        "Insufficient credits. Your organization has no remaining submission credits. " +
                        "Please contact your administrator to purchase additional credits before submitting.");
                }

                // Validate task exists and is not already submitted
                var task = await _context.DOITasks
                    .Include(t => t.Employee)
                    .Include(t => t.Template)
                    .FirstOrDefaultAsync(t => t.TaskId == taskId);

                if (task == null)
                {
                    throw new ArgumentException($"Task with ID {taskId} not found.");
                }

                if (task.Status == "Submitted" || task.Status == "Reviewed")
                {
                    throw new InvalidOperationException(
                        "This declaration has already been submitted and cannot be modified.");
                }

                // Check for existing submission (draft or otherwise)
                var submission = await _context.DOIFormSubmissions
                    .FirstOrDefaultAsync(s => s.FormTaskId == taskId);

                if (submission != null && submission.Status != "Draft")
                {
                    throw new InvalidOperationException(
                        "This declaration has already been submitted.");
                }

                // FR 4.4.2: Create digital attestation record
                var attestation = new
                {
                    EmployeeName = task.Employee.Full_Name,
                    EmployeeId = task.Employee.EmployeeId,
                    SubmittedAt = DateTime.UtcNow,
                    IpAddress = "Captured by calling controller", // Should be passed from controller
                    Declaration = "I hereby declare that the information provided is true, complete, and accurate to the best of my knowledge."
                };
                var attestationJson = JsonSerializer.Serialize(attestation);

                if (submission == null)
                {
                    // Create new submission
                    submission = new FormSubmission
                    {
                        FormTaskId = taskId,
                        FormData = formDataJson,
                        Status = "Pending",
                        Submitted_Date = DateTime.UtcNow,
                        DigitalAttestation = attestationJson
                    };
                    await _context.DOIFormSubmissions.AddAsync(submission);
                }
                else
                {
                    // Update existing draft to submitted
                    submission.FormData = formDataJson;
                    submission.Status = "Pending";
                    submission.Submitted_Date = DateTime.UtcNow;
                    submission.DigitalAttestation = attestationJson;
                    _context.DOIFormSubmissions.Update(submission);
                }

                // Update task status
                task.Status = "Submitted";
               
                _context.DOITasks.Update(task);

                // FR 4.4.3: CONSUME 1 CREDIT (FIFO principle)
                var creditConsumed = await _creditService.ConsumeCreditsAsync(
                    1,
                    $"DOI Submission - Task {taskId}");

                if (!creditConsumed)
                {
                    throw new InvalidOperationException(
                        "Credit consumption failed. Please try again or contact your administrator.");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Submission successful for TaskId: {TaskId}, Employee: {EmployeeId}. Credit consumed.",
                    taskId,
                    task.EmployeeId);

                return submission;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error submitting form for TaskId: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// Get submission by task ID (includes draft submissions)
        /// </summary>
        public async Task<FormSubmission> GetByTaskIdAsync(int taskId)
        {
            try
            {
                var submission = await _context.DOIFormSubmissions
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Employee)
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Template)
                    .Include(s => s.VerificationResults)
                    .FirstOrDefaultAsync(s => s.FormTaskId == taskId);

                if (submission == null)
                {
                    throw new ArgumentException($"No submission found for Task ID {taskId}.");
                }

                return submission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving submission for TaskId: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// Get submission by submission ID
        /// </summary>
        public async Task<FormSubmission> GetByIdAsync(int submissionId)
        {
            try
            {
                var submission = await _context.DOIFormSubmissions
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Employee)
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Template)
                    .Include(s => s.VerificationResults)
                    .FirstOrDefaultAsync(s => s.SubmissionId == submissionId);

                if (submission == null)
                {
                    throw new ArgumentException($"Submission with ID {submissionId} not found.");
                }

                return submission;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving submission: {SubmissionId}", submissionId);
                throw;
            }
        }

        /// <summary>
        /// FR 4.5.4: Reviewer digital sign-off
        /// Allows reviewer to approve/review submitted declarations
        /// </summary>
        public async Task<bool> ReviewerSignOffAsync(int submissionId, string reviewerSignatureData)
        {
            try
            {
                var submission = await _context.DOIFormSubmissions
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Employee)
                    .FirstOrDefaultAsync(s => s.SubmissionId == submissionId);

                if (submission == null)
                {
                    throw new ArgumentException($"Submission with ID {submissionId} not found.");
                }

                if (submission.Status != "Pending")
                {
                    throw new InvalidOperationException(
                        "Only submissions with 'Pending' status can be reviewed.");
                }

                // Parse and validate reviewer signature data
                // Expected format: { reviewerName, position, date, signatureBase64 }
                var signatureObj = JsonSerializer.Deserialize<JsonElement>(reviewerSignatureData);

                if (!signatureObj.TryGetProperty("reviewerName", out _) ||
                    !signatureObj.TryGetProperty("position", out _))
                {
                    throw new ArgumentException(
                        "Invalid signature data. Must include reviewerName and position.");
                }

                // Update submission status
                submission.Status = "Reviewed";

                // Store reviewer signature in digital attestation (append to existing)
                var existingAttestation = JsonSerializer.Deserialize<JsonElement>(
                    submission.DigitalAttestation ?? "{}");

                var updatedAttestation = new
                {
                    Employee = existingAttestation,
                    Reviewer = new
                    {
                        ReviewedBy = signatureObj.GetProperty("reviewerName").GetString(),
                        Position = signatureObj.GetProperty("position").GetString(),
                        ReviewDate = DateTime.UtcNow,
                        Signature = signatureObj.GetProperty("signatureBase64").GetString()
                    }
                };

                submission.DigitalAttestation = JsonSerializer.Serialize(updatedAttestation);
                _context.DOIFormSubmissions.Update(submission);

                // Update task status
                var task = submission.Task;
                task.Status = "Reviewed";
               
                _context.DOITasks.Update(task);

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Reviewer sign-off completed for SubmissionId: {SubmissionId}, Employee: {EmployeeId}",
                    submissionId,
                    task.EmployeeId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during reviewer sign-off for SubmissionId: {SubmissionId}",
                    submissionId);
                throw;
            }
        }

        /// <summary>
        /// Additional helper: Get all submissions for a reviewer's subordinates
        /// Supports FR 4.5.2 - Reviewer subordinate view
        /// </summary>
        public async Task<List<FormSubmission>> GetSubmissionsForReviewerAsync(int reviewerEmployeeId)
        {
            try
            {
                var submissions = await _context.DOIFormSubmissions
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Employee)
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Template)
                    .Where(s => s.Task.Employee.ManagerId == reviewerEmployeeId)
                    .OrderByDescending(s => s.Submitted_Date)
                    .ToListAsync();

                return submissions;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving submissions for reviewer: {ReviewerEmployeeId}",
                    reviewerEmployeeId);
                throw;
            }
        }

        /// <summary>
        /// Additional helper: Check if task has existing submission
        /// </summary>
        public async Task<bool> HasSubmissionAsync(int taskId)
        {
            try
            {
                return await _context.DOIFormSubmissions
                    .AnyAsync(s => s.FormTaskId == taskId && s.Status != "Draft");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error checking submission existence for TaskId: {TaskId}",
                    taskId);
                throw;
            }
        }

        /// <summary>
        /// Additional helper: Get submission statistics for reporting
        /// Supports dashboard metrics
        /// </summary>
        public async Task<SubmissionStatistics> GetStatisticsAsync()
        {
            try
            {
                var totalSubmissions = await _context.DOIFormSubmissions
                    .Where(s => s.Status != "Draft")
                    .CountAsync();

                var pendingReview = await _context.DOIFormSubmissions
                    .Where(s => s.Status == "Pending")
                    .CountAsync();

                var reviewed = await _context.DOIFormSubmissions
                    .Where(s => s.Status == "Reviewed")
                    .CountAsync();

              
                return new SubmissionStatistics
                {
                    TotalSubmissions = totalSubmissions,
                    PendingReview = pendingReview,
                    Reviewed = reviewed,
                  
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating submission statistics");
                throw;
            }
        }
    }

    /// <summary>
    /// Helper class for submission statistics
    /// </summary>
    public class SubmissionStatistics
    {
        public int TotalSubmissions { get; set; }
        public int PendingReview { get; set; }
        public int Reviewed { get; set; }
        public double AverageSubmissionDays { get; set; }
    }
}