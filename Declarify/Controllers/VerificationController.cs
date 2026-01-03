using Declarify.Data;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Declarify.Services;
using Declarify.Services.API;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Controllers
{
    public class VerificationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVerificationService _verificationService;
        private readonly ICreditService _creditService;
      
        private readonly CentralHubApiService _centralHub;

        public VerificationController(
            ApplicationDbContext context,
            IVerificationService verificationService,
            ICreditService creditService,
           CentralHubApiService centralHub)
        {
            _context = context;
            _verificationService = verificationService;
            _creditService = creditService;
           
            _centralHub = centralHub;
        }
        /// <summary>
        /// GET: Display all submissions pending verification
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var pendingSubmissions = await _context.DOIFormSubmissions
                .Include(s => s.Task)
                    .ThenInclude(t => t.Employee)
                .Include(s => s.Task)
                    .ThenInclude(t => t.Template)
                .Include(s => s.VerificationAttachments)
                .Where(s => s.Status == "Pending" || s.Status == "Submitted")
                .OrderByDescending(s => s.Submitted_Date)
                .ToListAsync();

            //var creditBalance = await _creditService.GetCreditBalanceAsync();
            var creditBalanceResult = await _centralHub.CheckCreditBalance();

            if (creditBalanceResult == null || !creditBalanceResult.hasCredits)
            {
                TempData["ErrorCheckCredits"] = creditBalanceResult == null ? "Cannot verify credits — license server unreachable. Please try again later." : "";

            }

            var creditBalance = creditBalanceResult?.currentBalance ?? 0;



            var viewModel = new VerificationIndexViewModel
            {
                PendingSubmissions = pendingSubmissions.Select(s => new VerificationSubmissionViewModel
                {
                    SubmissionId = s.SubmissionId,
                    EmployeeName = s.Task?.Employee?.Full_Name ?? "Unknown",
                    EmployeeId = s.Task.Employee.EmployeeId,
                    Department = s.Task?.Employee?.Department ?? "N/A",
                    Position = s.Task?.Employee?.Position ?? "N/A",
                    TemplateName = s.Task?.Template?.TemplateName ?? "Unknown Template",
                    SubmittedDate = s.Submitted_Date,
                    Status = s.Status ?? "Pending",
                    HasVerifications = s.VerificationAttachments?.Any() ?? false,
                    VerificationCount = s.VerificationAttachments?.Count ?? 0
                }).ToList(),
                CreditBalance = creditBalance,
                LowCreditWarning = creditBalance < 50,
                CriticalCreditWarning = creditBalance < 20
            };

            return View(viewModel);
        }

        /// <summary>
        /// GET: View detailed submission and run verification
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Verify(int id)
        {
            var submission = await _context.DOIFormSubmissions
                .Include(s => s.Task)
                    .ThenInclude(t => t.Employee)
                .Include(s => s.Task)
                    .ThenInclude(t => t.Template)
                .Include(s => s.VerificationAttachments)
                .FirstOrDefaultAsync(s => s.SubmissionId == id);

            if (submission == null)
                return NotFound();

            // Extract suggested entities from form data
            var suggestedEntities = await _verificationService
                .SuggestEntitiesFromFormAsync(submission.FormData ?? "{}");

            //var creditBalance = await _creditService.GetCreditBalanceAsync();
            var creditBalanceResult = await _centralHub.CheckCreditBalance();

            if (creditBalanceResult == null || !creditBalanceResult.hasCredits)
            {
                TempData["ErrorCheckCredits"] = creditBalanceResult == null ? "Cannot verify credits — license server unreachable. Please try again later." : "";

            }

            var creditBalance = creditBalanceResult?.currentBalance ?? 0;

            var viewModel = new VerificationDetailViewModel
            {
                SubmissionId = submission.SubmissionId,
                EmployeeName = submission.Task?.Employee?.Full_Name ?? "Unknown",
                // EmployeeId = submission.Task.Employee.EmployeeId,
                EmployeeEmail = submission.Task?.Employee?.Email_Address ?? "N/A",
                Department = submission.Task?.Employee?.Department ?? "N/A",
                Position = submission.Task?.Employee?.Position ?? "N/A",
                SubmittedDate = submission.Submitted_Date,
                Status = submission.Status ?? "Pending",
                FormData = submission.FormData ?? "{}",
                DigitalAttestation = submission.DigitalAttestation,
                ReviewerNotes = submission.ReviewerNotes ?? "",
                SuggestedEntities = suggestedEntities,
                ExistingVerifications = submission.VerificationAttachments?
                      .Select(v => new VerificationResultViewModel
                      {
                          VerificationId = v.VerificationId,
                          EntityName = ExtractEntityName(v.ResultJson),
                          VerificationType = v.Type ?? ExtractVerificationType(v.ResultJson),
                          Status = ExtractStatus(v.ResultJson),
                          CreatedAt = v.CreatedAt,
                          VerifiedDate = v.VerifiedDate,
                          ResultSummary = ExtractSummary(v.ResultJson),
                          InitiatedByEmployeeId = v.InitiatedByEmployeeId,
                          InitiatedByName = v.InitiatedBy?.Full_Name ?? "System"
                      }).ToList() ?? new List<VerificationResultViewModel>(),
                CreditBalance = creditBalance,
                CipcCheckCost = 5,
                CreditCheckCost = 10,
                CanRunCipcCheck = creditBalance >= 5,
                CanRunCreditCheck = creditBalance >= 10
            };

            return View(viewModel);
        }


        /// <summary>
        /// POST: Run CIPC verification check
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunCipcCheck(int submissionId, string entityName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName))
                    return Json(new { success = false, message = "Entity name is required." });

                // Check credit balance
                if (!await _creditService.HasSufficientCreditsAsync(5))
                    return Json(new { success = false, message = "Insufficient credits. You need 5 credits for a CIPC check." });

                // Perform the verification
                var result = await _verificationService.PerformCipcCheckAsync(submissionId, entityName);

                // Consume credits with reason
                var creditConsumed = await _creditService.ConsumeCreditsAsync(5, $"CIPC verification for {entityName} on submission #{submissionId}");

                if (!creditConsumed)
                    return Json(new { success = false, message = "Failed to consume credits. Please try again." });

                // Store the verification result
                var attachment = new VerificationAttachment
                {
                    SubmissionId = submissionId,
                    Type = "CIPC",
                    ResultJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        VerificationType = "CIPC",
                        EntityName = entityName,
                        Status = "Completed",
                        Result = result,
                        CreditsConsumed = 5
                    }),
                    CreatedAt = DateTime.UtcNow,
                    VerifiedDate = DateTime.UtcNow,
                    InitiatedByEmployeeId = GetCurrentAdminId()
                };

                _context.VerificationAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                var creditBalanceResult = await _centralHub.CheckCreditBalance();

                if (creditBalanceResult == null || !creditBalanceResult.hasCredits)
                {
                    TempData["ErrorCheckCredits"] = creditBalanceResult == null ? "Cannot verify credits — license server unreachable. Please try again later." : "";

                }

                var creditBalance = creditBalanceResult?.currentBalance ?? 0;
                return Json(new
                {
                    success = true,
                    message = $"CIPC verification completed for {entityName}. 5 credits consumed.",
                    verificationId = attachment.VerificationId,
                    remainingCredits = creditBalance
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred during verification: " + ex.Message });
            }
        }

        /// <summary>
        /// POST: Run Credit Check verification
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunCreditCheck(int submissionId, string entityName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entityName))
                    return Json(new { success = false, message = "Entity name is required." });

                // Check credit balance
                if (!await _creditService.HasSufficientCreditsAsync(10))
                    return Json(new { success = false, message = "Insufficient credits. You need 10 credits for a credit check." });

                // Perform the verification
                var result = await _verificationService.PerformCreditCheckAsync(submissionId, entityName);

                // Consume credits
                //await _creditService.ConsumeCreditsAsync(10);
                var creditConsumed = await _creditService.ConsumeCreditsAsync(5, $"CIPC verification for {entityName} on submission #{submissionId}");

                // Store the verification result
                var attachment = new VerificationAttachment
                {
                    SubmissionId = submissionId,
                    Type = "CreditCheck",
                    ResultJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        VerificationType = "CreditCheck",
                        EntityName = entityName,
                        Status = "Completed",
                        Result = result,
                        CreditsConsumed = 10
                    }),
                    CreatedAt = DateTime.UtcNow,
                    VerifiedDate = DateTime.UtcNow,
                    InitiatedByEmployeeId = GetCurrentAdminId() // You'll need to implement this
                };

                _context.VerificationAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                var creditBalanceResult = await _centralHub.CheckCreditBalance();

                if (creditBalanceResult == null || !creditBalanceResult.hasCredits)
                {
                    TempData["ErrorCheckCredits"] = creditBalanceResult == null ? "Cannot verify credits — license server unreachable. Please try again later." : "";

                }

                var creditBalance = creditBalanceResult?.currentBalance ?? 0;
                return Json(new
                {
                    success = true,
                    message = $"Credit check completed for {entityName}. 10 credits consumed.",
                    verificationId = attachment.VerificationId,
                    remainingCredits = creditBalance
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred during verification: " + ex.Message });
            }
        }

        /// <summary>
        /// POST: Add reviewer notes to submission
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNotes(int submissionId, string notes)
        {
            var submission = await _context.DOIFormSubmissions.FindAsync(submissionId);
            if (submission == null)
                return Json(new { success = false, message = "Submission not found." });

            submission.ReviewerNotes = notes;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Notes saved successfully." });
        }

        /// <summary>
        /// GET: View verification result details
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ViewResult(int id)
        {
            var attachment = await _context.VerificationAttachments
                .Include(v => v.Submission)
                    .ThenInclude(s => s.Task)
                        .ThenInclude(t => t.Employee)
                .Include(v => v.InitiatedBy)
                .FirstOrDefaultAsync(v => v.VerificationId == id);

            if (attachment == null)
                return NotFound();

            return View(attachment);
        }

        // Helper method to get current admin's employee ID
        private int? GetCurrentAdminId()
        {
            // TODO: Implement based on your authentication system
            // This could pull from User.Claims, Session, or your auth service
            // For example:
            // var employeeId = User.FindFirst("EmployeeId")?.Value;
            // return employeeId != null ? int.Parse(employeeId) : null;

            return null; // Return null for now, implement based on your auth
        }

        // Helper methods to extract data from JSON
        private string ExtractEntityName(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("EntityName").GetString() ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        private string ExtractVerificationType(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("VerificationType").GetString() ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        private string ExtractStatus(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("Status").GetString() ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        private string ExtractSummary(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Result", out var result))
                    return result.ToString() ?? "No details available";
                return "No details available";
            }
            catch { return "No details available"; }
        }


    }
}
