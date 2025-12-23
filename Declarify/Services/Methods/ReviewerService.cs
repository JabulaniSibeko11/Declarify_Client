using Declarify.Data;
using Declarify.Models;

using Declarify.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Declarify.Services.Methods
{
    public class ReviewerService : IReviewerService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICreditService _creditService;
        /*private readonly IExternalApiService _externalApiService;*/  // Assume this handles API calls
        private readonly ILogger<ReviewerService> _logger;
        public ReviewerService(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));  // This will throw early if null
        }
        public ReviewerService(ApplicationDbContext db, ICreditService creditService, ILogger<ReviewerService> logger)
        {
            _db = db;
            _creditService = creditService;
           
            _logger = logger;
        }

        public async Task<bool> IsLineManagerAsync(int employeeId)
        {
            if (employeeId <= 0) return false;

            return await _db.Employees
                .AnyAsync(e => e.ManagerId == employeeId && e.IsActive);
        }

        public async Task<List<SubordinateComplianceViewModel>> GetSubordinateComplianceAsync(int managerId)
        {
            if (managerId <= 0)
            {
                return new List<SubordinateComplianceViewModel>(); // No valid manager
            }

            // Query: Find all ACTIVE employees who report directly to this managerId
            var subordinates = await _db.Employees
                .Where(e => e.ManagerId == managerId && e.IsActive)
                .Include(e => e.DOITasks)
                .ThenInclude(t => t.Template)
                .Include(e => e.DOITasks) // Ensure FormSubmission is loaded if needed
                .ThenInclude(t => t.FormSubmission)
                .ToListAsync();

            // If no subordinates, return empty list early (common for leaf managers)
            if (!subordinates.Any())
            {
                return new List<SubordinateComplianceViewModel>();
            }

            var now = DateTime.UtcNow;

            return subordinates.Select(sub => new SubordinateComplianceViewModel
            {
                EmployeeId = sub.EmployeeId,
                FullName = sub.Full_Name ?? "Unknown",
                Position = sub.Position,
                Department = sub.Department,
                Tasks = sub.DOITasks.Select(t => new TaskSummary
                {
                    TaskId = t.TaskId,
                    TemplateName = t.Template?.TemplateName ?? "Untitled Task",
                    Status = t.Status,
                    DueDate = t.DueDate,
                    SubmittedDate = t.FormSubmission?.Submitted_Date,
                    IsOverdue =
                                t.DueDate < now &&
                                t.Status != "Submitted" &&
                                t.Status != "Reviewed"

                }).ToList()
            }).ToList();
        }
        public async Task<FormSubmission> GetSubmissionForReviewAsync(int submissionId, int reviewerId)
        {
            var submission = await _db.DOIFormSubmissions
                .Include(s => s.Task)
                .ThenInclude(t => t.Employee)
                .FirstOrDefaultAsync(s => s.SubmissionId == submissionId);

            if (submission == null || submission.Task.Employee.ManagerId != reviewerId)
            {
                _logger.LogWarning("Unauthorized review attempt for submission {SubmissionId} by reviewer {ReviewerId}", submissionId, reviewerId);
                return null;  // Or throw UnauthorizedException
            }

            return submission;
        }

        public async Task<VerificationResult> InitiateVerificationAsync(int submissionId, string verificationType, int reviewerId)
        {
            var submission = await GetSubmissionForReviewAsync(submissionId, reviewerId);
            if (submission == null) return new VerificationResult { Success = false, Message = "Invalid submission" };

            int creditsRequired = verificationType == "CIPC" ? 5 : 10;  // Per FR 4.6.3
            if (!await _creditService.HasSufficientCreditsAsync(creditsRequired))
            {
                return new VerificationResult { Success = false, Message = "Insufficient credits" };
            }

            // Call external API (stubbed here)
           // var apiResult = await _externalApiService.VerifyAsync(submission.FormData, verificationType);

            // Store as attachment (immutable)
            var verification = new VerificationAttachment
            {
                SubmissionId = submissionId,
                Type = verificationType,
                //ResultData = apiResult.Data,  // JSON or string
                VerifiedDate = DateTime.UtcNow
            };
            _db.VerificationAttachments.Add(verification);

            await _creditService.ConsumeCreditsAsync(creditsRequired, $"{verificationType} Verification");
            await _db.SaveChangesAsync();

            return new VerificationResult { Success = true, Message = "Verification complete"/*, Result = apiResult*/ };
        }

        public async Task<SignOffResult> SignOffSubmissionAsync(int submissionId, string signature, string notes, int reviewerId)
        {
            var submission = await GetSubmissionForReviewAsync(submissionId, reviewerId);
            if (submission == null) return new SignOffResult { Success = false, Message = "Invalid submission" };

            if (submission.Task.Status != "Submitted")
            {
                return new SignOffResult { Success = false, Message = "Submission not ready for review" };
            }

            // Update sign-off details
            submission.AssignedManagerId = reviewerId;
            submission.ReviewerSignature = signature;
            submission.ReviewerNotes = notes;
            submission.ReviewedDate = DateTime.UtcNow;
            submission.Task.Status = "Reviewed";

            await _db.SaveChangesAsync();
            _logger.LogInformation("Submission {SubmissionId} signed off by reviewer {ReviewerId}", submissionId, reviewerId);

            return new SignOffResult { Success = true, Message = "Sign-off complete" };
        }
    }
}