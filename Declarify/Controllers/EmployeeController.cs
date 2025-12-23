using Declarify.Data;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Declarify.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Declarify.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeDOIService _doiService;
        private readonly ILogger<EmployeeController> _logger;
        private readonly IReviewHelperService _reviewHelper;

        private readonly ApplicationDbContext _db;

        public EmployeeController(
          IEmployeeDOIService doiService,
          ILogger<EmployeeController> logger, ApplicationDbContext db, IReviewHelperService reviewHelper)
        {
            _doiService = doiService;
            _logger = logger;
            _db = db;
            _reviewHelper = reviewHelper;
        }

        // GET: /employee/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Check if user is authenticated
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    _logger.LogWarning("Unauthenticated access attempt to employee dashboard");
                    return RedirectToAction("Login", "Home", new { returnUrl = Url.Action("Dashboard", "Employee") });
                }

                var employeeId = GetCurrentEmployeeId();
                var viewModel = await _doiService.GetEmployeeDashboardAsync(employeeId);

              

                return View(viewModel);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Employee ID not found"))
            {
                _logger.LogError(ex, "Employee ID not found in claims");
                TempData["Error"] = "Your account setup is incomplete. Please contact your administrator.";
                return RedirectToAction("Login", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee dashboard");
                TempData["Error"] = "Unable to load dashboard. Please try again.";
                return RedirectToAction("Error", "Home");
            }
        }

        // Additional helper method for authorization checks
        private bool IsAuthorizedForExecutiveDashboard()
        {
            var employeeId = GetCurrentEmployeeId();
            var employee = _doiService.GetEmployeeDashboardAsync(employeeId).GetAwaiter().GetResult();

            return employee.Employee.IsExecutive || employee.Employee.IsSeniorManagement;
        }

        // Additional helper method for reviewer authorization
        private bool IsAuthorizedAsReviewer()
        {
            var employeeId = GetCurrentEmployeeId();
            var employee = _doiService.GetEmployeeDashboardAsync(employeeId).GetAwaiter().GetResult();

            // Reviewers are line managers or senior management
            return employee.IsLineManager || employee.Employee.IsSeniorManagement;
        }
        // GET: /employee/tasks
        [AllowAnonymous] // Accessed via unique link
        [HttpGet("task")]
        public async Task<IActionResult> Task([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest("Invalid access link");
                }

                var task = await _doiService.GetDOITaskByTokenAsync(token);
                if (task == null)
                {
                    TempData["Error"] = "This link has expired or is invalid. Please request a new one from your administrator.";
                    return View("TokenExpired");
                }

                var template = await _doiService.GetFormTemplateForTaskAsync(task.TaskId);
                if (template == null)
                {
                    return NotFound("Form template not found");
                }

                var draft = await _doiService.GetDraftSubmissionAsync(task.TaskId);

                // Fetch employee signature from database
                var employee = await _doiService.GetEmployeeByIdAsync(task.EmployeeId);
                string employeeSignature = employee?.Signature_Picture ?? string.Empty;
                string employeeName = $"{employee?.Full_Name}";
                string employeeEmail = employee?.Email_Address ?? string.Empty;

                var viewModel = new TaskDetailViewModel
                {
                    Task = task,
                    Template = template,
                    DraftSubmission = draft,
                    CanSubmit = task.Status == "Outstanding",
                    EmployeeSignature = employeeSignature,
                    EmployeeName = employeeName,
                    EmployeeEmail = employeeEmail
                };

                return View("CompleteForm", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading task with token");
                return StatusCode(500, "Error loading form");
            }
        }


        // GET: /employee/tasks
        [HttpGet("tasks")]
        public async Task<IActionResult> Tasks()
        {
            try
            {
                var employeeId = GetCurrentEmployeeId();
                var tasks = await _doiService.GetEmployeeTasksAsync(employeeId);

                return View(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee tasks");
                return StatusCode(500, "Error loading tasks");
            }
        }


        // GET: /employee/task/{token}
        //[AllowAnonymous] // Accessed via unique link
        //[HttpGet("task")]
        public async Task<IActionResult> Task1([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest("Invalid access link");
                }

                var task = await _doiService.GetDOITaskByTokenAsync(token);
                if (task == null)
                {
                    TempData["Error"] = "This link has expired or is invalid. Please request a new one from your administrator.";
                    return View("TokenExpired");
                }

                var template = await _doiService.GetFormTemplateForTaskAsync(task.TaskId);
                if (template == null)
                {
                    return NotFound("Form template not found");
                }

                var draft = await _doiService.GetDraftSubmissionAsync(task.TaskId);

                var viewModel = new TaskDetailViewModel
                {
                    Task = task,
                    Template = template,
                    DraftSubmission = draft,
                    CanSubmit = task.Status == "Outstanding"
                };

                return View("CompleteForm", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading task with token");
                return StatusCode(500, "Error loading form");
            }
        }

        [AllowAnonymous]
        [HttpPost("save-draft")]
        public async Task<IActionResult> SaveDraft([FromBody] SaveDraftRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token) || request.FormData == null)
                {
                    return BadRequest(new { success = false, message = "Invalid request" });
                }

                var formData = JsonDocument.Parse(request.FormData);
                await _doiService.SaveDraftAsync(request.Token, formData);

                return Ok(new
                {
                    success = true,
                    message = "Draft saved successfully",
                    savedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving draft");
                return StatusCode(500, new { success = false, message = "Error saving draft" });
            }
        }

        // POST: /employee/submit

        // POST: /employee/submit
        [AllowAnonymous]
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] SubmitFormRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token) ||
                    request.FormData == null ||
                    string.IsNullOrEmpty(request.AttestationSignature))
                {
                    return BadRequest(new { success = false, message = "Invalid submission" });
                }

                var formData = JsonDocument.Parse(request.FormData);
                var result = await _doiService.SubmitDOIAsync(
                    request.Token,
                    formData,
                    request.AttestationSignature
                );

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        redirectUrl = Url.Action("SubmissionSuccess", "Employee")
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = result.Message
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting form");
                return StatusCode(500, new { success = false, message = "Error submitting form" });
            }
        }
        // GET: /employee/submission-success
        [AllowAnonymous]
        [HttpGet("submission-success")]
        public IActionResult SubmissionSuccess()
        {
            return View();
        }

        // GET: /employee/profile
        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var employeeId = GetCurrentEmployeeId();
                var profile = await _doiService.GetEmployeeProfileAsync(employeeId);

                return View(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee profile");
                return StatusCode(500, "Error loading profile");
            }
        }
        // GET: /employee/compliance-stats
        [HttpGet("compliance-stats")]
        public async Task<IActionResult> GetComplianceStats()
        {
            try
            {
                var employeeId = GetCurrentEmployeeId();
                var stats = await _doiService.GetEmployeeComplianceStatsAsync(employeeId);

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading compliance stats");
                return StatusCode(500, "Error loading statistics");
            }
        }
        private int GetCurrentEmployeeId1()
        {
            // Get the currently logged-in user's username/email
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                throw new InvalidOperationException("User is not logged in");

            // Check Employee table
            var employee = _db.Employees
                .FirstOrDefault(e => e.Email_Address == username );

            if (employee == null)
                throw new InvalidOperationException("Employee not found in database");

            return employee.EmployeeId; // or Id depending on your model
        }

        private int GetCurrentEmployeeId()
        {
            var claim = User.FindFirst("EmployeeId")
                     ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (claim == null || !int.TryParse(claim.Value, out int id) || id <= 0)
            {
                throw new InvalidOperationException("Employee ID not found in claims.");
            }

            return id;
        }
        [HttpGet]
        public async Task<IActionResult> Review(int? taskId, int? submissionId)
        {
            try
            {
                // Get the current user's employee ID (adjust based on your auth system)
                var currentEmployeeId = GetCurrentEmployeeId();

                FormSubmission submission = null;

                // If taskId is provided, find the submission by taskId
                if (taskId.HasValue)
                {
                    submission = await _db.DOIFormSubmissions
                        .Include(s => s.Task)
                            .ThenInclude(t => t.Employee)
                        .Include(s => s.Task)
                            .ThenInclude(t => t.Template)
                        .FirstOrDefaultAsync(s => s.FormTaskId == taskId.Value);
                }
                // Otherwise, look up by submissionId
                else if (submissionId.HasValue)
                {
                    submission = await _db.DOIFormSubmissions
                        .Include(s => s.Task)
                            .ThenInclude(t => t.Employee)
                        .Include(s => s.Task)
                            .ThenInclude(t => t.Template)
                        .FirstOrDefaultAsync(s => s.SubmissionId == submissionId.Value);
                }
                else
                {
                    TempData["Error"] = "Invalid request. Task ID or Submission ID is required.";
                    return RedirectToAction("Dashboard");
                }

                if (submission == null)
                {
                    TempData["Error"] = "Submission not found. The employee may not have submitted this declaration yet.";
                    return RedirectToAction("Dashboard");
                }

                // Verify the current user is the assigned manager
                if (submission.AssignedManagerId != currentEmployeeId)
                {
                    TempData["Error"] = "You are not authorized to review this submission.";
                    _logger.LogWarning(
                        "Unauthorized review attempt: Employee {CurrentId} tried to access submission {SubmissionId} assigned to {AssignedManagerId}",
                        currentEmployeeId,
                        submission.SubmissionId,
                        submission.AssignedManagerId);
                    return RedirectToAction("Dashboard");
                }

                // Check if already reviewed
                if (submission.Status == "Reviewed" || submission.Status == "Approved")
                {
                    TempData["Warning"] = "This submission has already been reviewed.";
                    return RedirectToAction("Dashboard");
                }

                // Check if submission is in correct status for review
                if (submission.Status != "Submitted" && submission.Status != "Pending")
                {
                    TempData["Warning"] = $"This submission cannot be reviewed in its current status: {submission.Status}";
                    return RedirectToAction("Dashboard");
                }

                // Get the reviewer (current user) information
                var reviewer = await _db.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == currentEmployeeId);

                // Create view model - pass RAW JSON strings just like CompleteForm does
                var viewModel = new ReviewSubmissionViewModel
                {
                    SubmissionId = submission.SubmissionId,
                    EmployeeName = submission.Task.Employee.Full_Name,
                    Position = submission.Task.Employee.Position,
                    Department = submission.Task.Employee.Department,
                    SubmittedDate = submission.Submitted_Date,
                    SubmissionStatus = submission.Status,
                    ReviewerName = reviewer?.Full_Name ?? "Unknown",
                    ReviewerPosition = reviewer?.Position ?? "Manager",

                    // Pass raw JSON - let the view parse it like CompleteForm does
                    TemplateConfig = submission.Task.Template.TemplateConfig ?? "{}",
                    FormData = submission.FormData ?? "{}",

                    DigitalAttestation = submission.DigitalAttestation
                };

                _logger.LogInformation(
                    "Manager {ManagerId} accessed review page for submission {SubmissionId} (Task {TaskId})",
                    currentEmployeeId,
                    submission.SubmissionId,
                    submission.FormTaskId);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading review page for taskId {TaskId}, submissionId {SubmissionId}",
                    taskId, submissionId);
                TempData["Error"] = "An error occurred while loading the submission.";
                return RedirectToAction("Dashboard");
            }
        }
        // POST: Process review (approve or reject)
        [HttpPost]
        public async Task<IActionResult> ProcessReview([FromBody] ProcessReviewRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Action))
                {
                    return BadRequest(new { success = false, message = "Invalid request" });
                }

                var currentEmployeeId = GetCurrentEmployeeId();

                var submission = await _db.DOIFormSubmissions
                    .Include(s => s.Task)
                        .ThenInclude(t => t.Employee)
                    .FirstOrDefaultAsync(s => s.SubmissionId == request.SubmissionId);

                if (submission == null)
                {
                    return NotFound(new { success = false, message = "Submission not found" });
                }

                // Verify authorization
                if (submission.AssignedManagerId != currentEmployeeId)
                {
                    _logger.LogWarning(
                        "Unauthorized review attempt: Employee {CurrentId} tried to process submission {SubmissionId}",
                        currentEmployeeId,
                        request.SubmissionId);
                    return Forbid();
                }

                // Check if already reviewed
                if (submission.Status == "Reviewed" || submission.Status == "Approved")
                {
                    return Ok(new
                    {
                        success = false,
                        message = "This submission has already been reviewed."
                    });
                }

                // Process based on action
                if (request.Action.ToLower() == "approve")
                {
                    // Update submission
                    submission.Status = "Reviewed"; // or "Approved" based on your workflow
                    submission.ReviewerNotes = request.ReviewerNotes ?? string.Empty;
                    submission.ReviewedDate = DateTime.UtcNow;

                    // Update task status
                    submission.Task.Status = "Reviewed";

                    _db.DOIFormSubmissions.Update(submission);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation(
                        "Submission {SubmissionId} approved by Manager {ManagerId}",
                        request.SubmissionId,
                        currentEmployeeId);

                    // Optional: Send notification email to employee
                    // await _emailService.SendApprovalNotificationAsync(submission.Task.Employee.Email_Address);

                    return Ok(new
                    {
                        success = true,
                        message = "Declaration approved successfully.",
                        redirectUrl = Url.Action("Dashboard", "Employee")
                    });
                }
                else if (request.Action.ToLower() == "reject")
                {
                    // Validate notes are provided for rejection
                    if (string.IsNullOrWhiteSpace(request.ReviewerNotes))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Notes are required when requesting revision."
                        });
                    }

                    // Update submission - send back for revision
                    submission.Status = "Revision Required"; // or "Rejected"
                    submission.ReviewerNotes = request.ReviewerNotes;
                    submission.ReviewedDate = DateTime.UtcNow;

                    // Update task status - reopen for employee
                    submission.Task.Status = "Outstanding";

                    // Regenerate access token for employee to resubmit
                    submission.Task.AccessToken = Convert.ToBase64String(
                        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                    submission.Task.TokenExpiry = DateTime.UtcNow.AddDays(7);

                    _db.DOIFormSubmissions.Update(submission);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation(
                        "Submission {SubmissionId} sent back for revision by Manager {ManagerId}",
                        request.SubmissionId,
                        currentEmployeeId);

                    // Optional: Send notification email to employee with revision notes
                    // await _emailService.SendRevisionRequestAsync(
                    //     submission.Task.Employee.Email_Address, 
                    //     request.ReviewerNotes,
                    //     submission.Task.AccessToken);

                    return Ok(new
                    {
                        success = true,
                        message = "Revision request sent to employee.",
                        redirectUrl = Url.Action("Dashboard", "Employee")
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid action specified."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing review for submission {SubmissionId}", request?.SubmissionId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing your review."
                });
            }
        }

        // STEP 1: Parse template configuration with submitted form data
        // This method reconstructs the form structure and populates it with employee responses
        private async Task<List<FormSectionViewModel>> ParseTemplateWithFormData(Template template, string formDataJson)
        {
            var sections = new List<FormSectionViewModel>();

            try
            {
                // STEP 2: Parse the template configuration (JSONB from Template table)
                var templateConfig = JsonDocument.Parse(template.TemplateConfig ?? "{}");

                // STEP 3: Parse the submitted form data (JSONB from FormSubmission table)
                var formData = JsonDocument.Parse(formDataJson);

                // STEP 4: Check if template has sections array
                if (!templateConfig.RootElement.TryGetProperty("sections", out JsonElement sectionsElement))
                {
                    _logger.LogWarning("Template {TemplateId} has no sections property", template.TemplateId);
                    return CreateFallbackSection(formDataJson);
                }

                // STEP 5: Iterate through each section in the template
                foreach (var sectionElement in sectionsElement.EnumerateArray())
                {
                    var sectionViewModel = new FormSectionViewModel
                    {
                        SectionTitle = GetStringProperty(sectionElement, "sectionTitle")
                                    ?? GetStringProperty(sectionElement, "title")
                                    ?? "Section",
                        SectionOrder = GetIntProperty(sectionElement, "sectionOrder", 0),
                        Disclaimer = GetStringProperty(sectionElement, "disclaimer"),
                        Fields = new List<FormFieldViewModel>()
                    };

                    // STEP 6: Check if section has fields array
                    if (!sectionElement.TryGetProperty("fields", out JsonElement fieldsElement))
                    {
                        sections.Add(sectionViewModel);
                        continue;
                    }

                    // STEP 7: Iterate through each field in the section
                    foreach (var fieldElement in fieldsElement.EnumerateArray())
                    {
                        // STEP 8: Extract field metadata from template
                        var fieldId = GetStringProperty(fieldElement, "fieldId")
                                   ?? GetStringProperty(fieldElement, "id");
                        var fieldLabel = GetStringProperty(fieldElement, "fieldLabel")
                                      ?? GetStringProperty(fieldElement, "label")
                                      ?? "Field";
                        var fieldType = GetStringProperty(fieldElement, "fieldType")
                                     ?? GetStringProperty(fieldElement, "type")
                                     ?? "text";
                        var isRequired = GetBoolProperty(fieldElement, "required", false);
                        var fieldOrder = GetIntProperty(fieldElement, "order", 0);

                        // STEP 9: Get the submitted value for this field from FormData
                        string fieldValue = ExtractFieldValue(formData, fieldId);

                        // STEP 10: Get options for select/radio fields
                        List<string>? options = null;
                        if (fieldElement.TryGetProperty("options", out JsonElement optionsElement)
                            && optionsElement.ValueKind == JsonValueKind.Array)
                        {
                            options = optionsElement.EnumerateArray()
                                .Select(o => o.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                        }

                        // STEP 11: Create field view model
                        var fieldViewModel = new FormFieldViewModel
                        {
                            FieldId = fieldId ?? "",
                            Label = fieldLabel,
                            Value = fieldValue,
                            FieldType = fieldType.ToLower(),
                            IsRequired = isRequired,
                            Order = fieldOrder,
                            Options = options
                        };

                        sectionViewModel.Fields.Add(fieldViewModel);
                    }

                    // STEP 12: Sort fields by order
                    sectionViewModel.Fields = sectionViewModel.Fields
                        .OrderBy(f => f.Order)
                        .ToList();

                    sections.Add(sectionViewModel);
                }

                // STEP 13: Sort sections by order
                sections = sections.OrderBy(s => s.SectionOrder).ToList();

                _logger.LogInformation(
                    "Successfully parsed template {TemplateId} with {SectionCount} sections",
                    template.TemplateId,
                    sections.Count);

                return sections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing template {TemplateId} with form data", template.TemplateId);
                return CreateFallbackSection(formDataJson);
            }
        }

        // STEP 14: Helper method to extract field value from FormData JSON
        private string ExtractFieldValue(JsonDocument formData, string? fieldId)
        {
            if (string.IsNullOrEmpty(fieldId))
                return "";

            try
            {
                if (formData.RootElement.TryGetProperty(fieldId, out JsonElement valueElement))
                {
                    // Handle different JSON value types
                    return valueElement.ValueKind switch
                    {
                        JsonValueKind.String => valueElement.GetString() ?? "",
                        JsonValueKind.True => "True",
                        JsonValueKind.False => "False",
                        JsonValueKind.Number => valueElement.ToString(),
                        JsonValueKind.Array => string.Join(", ", valueElement.EnumerateArray()
                            .Select(e => e.GetString() ?? e.ToString())),
                        JsonValueKind.Null => "",
                        _ => valueElement.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting value for field {FieldId}", fieldId);
            }

            return "";
        }

        // STEP 15: Helper methods for safe property extraction
        private string? GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement prop)
                ? prop.GetString()
                : null;
        }

        private int GetIntProperty(JsonElement element, string propertyName, int defaultValue)
        {
            if (element.TryGetProperty(propertyName, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetInt32();
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out int result))
                    return result;
            }
            return defaultValue;
        }

        private bool GetBoolProperty(JsonElement element, string propertyName, bool defaultValue)
        {
            if (element.TryGetProperty(propertyName, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
                if (prop.ValueKind == JsonValueKind.String)
                {
                    var strValue = prop.GetString()?.ToLower();
                    if (strValue == "true") return true;
                    if (strValue == "false") return false;
                }
            }
            return defaultValue;
        }

        // STEP 16: Fallback section when parsing fails
        private List<FormSectionViewModel> CreateFallbackSection(string formDataJson)
        {
            return new List<FormSectionViewModel>
    {
        new FormSectionViewModel
        {
            SectionTitle = "Submission Data",
            Fields = new List<FormFieldViewModel>
            {
                new FormFieldViewModel
                {
                    Label = "Raw Form Data",
                    Value = formDataJson,
                    FieldType = "text"
                }
            }
        }
    };
        }

        // Request model for processing reviews
        public class ProcessReviewRequest
        {
            public int SubmissionId { get; set; }
            public string Action { get; set; } = string.Empty; // "approve" or "reject"
            public string? ReviewerNotes { get; set; }
            public string ReviewerName { get; set; } = string.Empty;
            public string ReviewerPosition { get; set; } = string.Empty;
        }

    }
}
