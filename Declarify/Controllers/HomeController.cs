using CsvHelper;
using CsvHelper.Configuration;
using Declarify.Data;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Declarify.Services;
using Declarify.Services.Methods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using LicenseContext = OfficeOpenXml.LicenseContext;
namespace Declarify.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly IEmployeeService _ES;
        private readonly ITemplateService _TS;
        private readonly IFormTaskService _FS;
        private readonly ICreditService _CS;
        private readonly ILicenseService _LS;
        private readonly IVerificationService _VS;
        private readonly IUserService _US;
        private readonly ISubmissionService _SS;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        public HomeController(ILogger<HomeController> logger,ApplicationDbContext db, IEmployeeService eS, ITemplateService tS, IFormTaskService fS, ICreditService cS, ILicenseService lS, IVerificationService vS, IUserService uS, ISubmissionService sS, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _logger = logger;
            _ES = eS;
            _TS = tS;
            _FS = fS;
            _CS = cS;
            _LS = lS;
            _VS = vS;
            _US = uS;
            _SS = sS;
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
        }


        public IActionResult LandingPage()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            // Check license before allowing login (NFR 5.2.3)
            // If user is already logged in, redirect to their appropriate dashboard
            if (!await _LS.IsLicenseValidAsync())
            {
                return View("LicenseExpired");
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    return RedirectToDashboardByRole(currentUser);
                }
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
       
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            // Check license first (NFR 5.2.3)
            if (!await _LS.IsLicenseValidAsync())
            {
                return View("LicenseExpired");
            }
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            try
            {
                // Step 1: Find user by email (works for all roles: Admin, Manager, Employee, Executive)
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                // Step 2: CRITICAL — Check if user has NO password ? force InitialSetup (FR 4.1.1)
                var hasPassword = await _userManager.HasPasswordAsync(user);
                if (!hasPassword || user.IsFirstLogin)
                {
                    _logger.LogInformation("User {Email} requires initial password setup.", model.Email);

                    // Generate a password reset token
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                    // Redirect to InitialSetup with token
                    return RedirectToAction("InitialSetup", new
                    {
                        email = model.Email,
                        token = token
                    });
                }

                // Step 3: Attempt password login
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Email} ({Role}) logged in successfully.",
                        model.Email, user.roleInCompany ?? "Unknown");

                    TempData["Success"] = $"Welcome back, {user.Full_Name ?? user.Email}!";

                    // Check for returnUrl first
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // Redirect based on role (FR 4.5.1, 4.5.2, 4.5.3)
                    return RedirectToDashboardByRole(user);
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction("LoginWith2fa", new { returnUrl, model.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User {Email} account locked out.", model.Email);
                    return View("Lockout");
                }

                // If we get here: wrong password
                _logger.LogWarning("Failed login attempt for {Email}.", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
            }
            return View(model);
        }

        // Initial password setup for users loaded via CSV (FR 4.1.1)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> InitialSetup(string email, string? token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Invalid password setup link.";
                return RedirectToAction("Login");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Login");
            }

            var model = new InitialPasswordSetupViewModel
            {
                Email = email,
                Token = token,
                FullName = user.Full_Name ?? "User"
            };

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitialSetup(InitialPasswordSetupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate signature
            if (string.IsNullOrEmpty(model.SignatureData))
            {
                ModelState.AddModelError("SignatureData", "Please provide your signature.");
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                return View(model);
            }

            // Reset password using the token
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                // Save the signature
                user.Signature = model.SignatureData; // Store base64 signature
                user.IsFirstLogin = false;
                user.PasswordSetupDate = DateTime.UtcNow;

                await _userManager.UpdateAsync(user);

                // Also update the Employee record if linked
                if (user.EmployeeId.HasValue)
                {
                    var employee = await _db.Employees.FindAsync(user.EmployeeId.Value);
                    if (employee != null)
                    {
                        employee.Signature_Picture = model.SignatureData;
                        employee.Signature_Created_Date = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }
                }

                _logger.LogInformation("User {Email} completed initial password and signature setup.", model.Email);
                TempData["Success"] = "Your account has been set up successfully! You can now log in.";

                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // Redirects users to their appropriate dashboard based on role (FR 4.5.1, 4.5.2, 4.5.3)

        private IActionResult RedirectToDashboardByRole(ApplicationUser user)
        {
            if (string.IsNullOrEmpty(user.roleInCompany))
            {
                _logger.LogWarning("User {Email} has no role assigned. Defaulting to Employee dashboard.", user.Email);
                return RedirectToAction("Dashboard", "Employee");
            }

            return user.roleInCompany.ToLower().Trim() switch
            {
                "admin" => RedirectToAction("Index", "Home"), // Admin Dashboard (FR 4.5.1)
                "manager" or "reviewer" => RedirectToAction("Dashboard", "Manager"), // Manager/Reviewer Dashboard (FR 4.5.2)
                "employee" => RedirectToAction("Dashboard", "Employee"), // Employee Dashboard
                "executive" or "it deputy director" or "senior management" => RedirectToAction("ExecutiveDashboard", "Executive"), // Executive Dashboard (FR 4.5.3)
                _ => RedirectToAction("Dashboard", "Employee") // Default fallback
            };
        }     // Main dashboard view - displays compliance overview (FR 4.5.1)
        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            try {
                // Check license first (NFR 5.2)
                if (!await _LS.IsLicenseValidAsync())
                {
                    return View("LicenseExpired");
                }

                var dashboardData = await _FS.GetComplianceDashboardDataAsync();
            var creditBalance = await _CS.GetAvailableCreditsAsync();
            var creditBatches = await _CS.GetCreditBatchesAsync();
            var licenseStatus = await _LS.GetLicenseStatusMessageAsync();
            var licenseExpiryDate = await _LS.GetExpiryDateAsync();

            // Check for low credit balance
            var lowCreditWarning = creditBalance < 50;
            var criticalCreditWarning = creditBalance < 20;

            // Check for expiring credits
            var expiringCredits = await _CS.GetExpiringCreditsAsync(30);

            var viewModel = new DashboardViewModel
            {
                // Compliance Metrics (FR 4.5.1)
                TotalEmployees = dashboardData.TotalEmployees,
                TotalTasks = dashboardData.TotalTasks,
                OutstandingCount = dashboardData.OutstandingCount,
                OverdueCount = dashboardData.OverdueCount,
                SubmittedCount = dashboardData.SubmittedCount,
                ReviewedCount = dashboardData.ReviewedCount,
                NonCompliantCount = dashboardData.NonCompliantCount,
                CompliancePercentage = dashboardData.CompliancePercentage,

                // Department Breakdown (FR 4.5.3)
                DepartmentBreakdown = dashboardData.DepartmentBreakdown,

                // Credit Information
                CreditBalance = creditBalance,
                CreditBatches = creditBatches,
                ExpiringCredits = expiringCredits,
                LowCreditWarning = lowCreditWarning,
                CriticalCreditWarning = criticalCreditWarning,

                // License Information
                LicenseStatus = licenseStatus,
                LicenseExpiryDate = licenseExpiryDate,
                DaysUntilLicenseExpiry = (licenseExpiryDate - DateTime.UtcNow).Days,

                // Goal Tracking (G1: 95% compliance)
                GoalComplianceRate = 95.0,
                IsGoalAchieved = dashboardData.CompliancePercentage >= 95.0
            };
                return View(viewModel);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["Error"] = "Failed to load dashboard data. Please try again.";
                return View(new DashboardViewModel());

            }
          
        }

        // License expired view (NFR 5.2.3)
        // Blocks all functionality when license is expired
        [HttpGet("license-expired")]
        [AllowAnonymous] // Allow access even without valid license
        public async Task<IActionResult> LicenseExpired()
        {
            var licenseExpiryDate = await _LS.GetExpiryDateAsync();
            var viewModel = new LicenseExpiredViewModel
            {
                ExpiryDate = licenseExpiryDate,
                Message = "Account requires renewal. Please contact your vendor."
            };

            return View(viewModel);
        }

        // ============================================================================
        // BULK REQUEST MANAGEMENT (FR 4.3.1)
        // ============================================================================
        // Show bulk request form

        [HttpGet("bulk-request")]
        public async Task<IActionResult> BulkRequest()
        {
            if (!await _LS.IsLicenseValidAsync())
            {
                return RedirectToAction("LicenseExpired");
            }

            var templates = await _TS.GetActiveTemplatesAsync();
            var employees = await _ES.GetAllEmployeesAsync();
            var departments = await _ES.GetDepartmentEmployeeCountsAsync();

            var viewModel = new BulkRequestViewModel
            {
                Templates = templates.ToList(),
                Employees = employees,
                Departments = departments,
                SuggestedDueDate = DateTime.UtcNow.AddDays(30) // Default 30 days
            };

            return View(viewModel);
        }

        // Process bulk request - create tasks and send emails (FR 4.3.1, 4.3.2, 4.3.3)
        [HttpPost("bulk-request")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkRequest(BulkRequestFormModel model)
        {
            try
            {
                // Validate license
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                // Validate model
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please correct the errors and try again.";
                    return RedirectToAction("BulkRequest");
                }

                // Validate employee selection
                if (model.EmployeeIds == null || !model.EmployeeIds.Any())
                {
                    TempData["Error"] = "Please select at least one employee.";
                    return RedirectToAction("BulkRequest");
                }

                // Validate due date
                if (model.DueDate <= DateTime.UtcNow)
                {
                    TempData["Error"] = "Due date must be in the future.";
                    return RedirectToAction("BulkRequest");
                }

                _logger.LogInformation($"Admin initiating bulk request: Template={model.TemplateId}, Employees={model.EmployeeIds.Count}, DueDate={model.DueDate:yyyy-MM-dd}");

                // Create tasks and send emails
                await _FS.BulkCreateTasksAsync(
                    model.TemplateId,
                    model.DueDate,
                    model.EmployeeIds
                );

                TempData["Success"] = $"Successfully sent DOI requests to {model.EmployeeIds.Count} employees. Due date: {model.DueDate:MMMM d, yyyy}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk request");
                TempData["Error"] = $"Failed to process bulk request: {ex.Message}";
                return RedirectToAction("BulkRequest");
            }
        }

        // Get employees by department (AJAX endpoint for dynamic filtering)
        [HttpGet("employees/by-department/{department}")]
        public async Task<IActionResult> GetEmployeesByDepartment(string department)
        {
            try
            {
                var employees = await _ES.GetEmployeesByDepartmentAsync(department);
                return Json(employees.Select(e => new
                {
                    employeeId = e.EmployeeId,
                    fullName = e.Full_Name,
                    position = e.Position,
                    email = e.Email_Address
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching employees for department: {department}");
                return StatusCode(500, "Error fetching employees");
            }
        }
        // ============================================================================
        // EMPLOYEE MANAGEMENT (FR 4.1.3)
        // ============================================================================
        // Employee management view

        [HttpGet("employees")]
        public async Task<IActionResult> Employees()
        {
            if (!await _LS.IsLicenseValidAsync())
            {
                return RedirectToAction("LicenseExpired");
            }

            var employees = await _ES.GetAllEmployeesAsync();
            var departments = await _ES.GetDepartmentEmployeeCountsAsync();

            var viewModel = new EmployeeManagementViewModel
            {
                Employees = employees,
                TotalEmployees = employees.Count,
                DepartmentCounts = departments
            };

            return View(viewModel);
        }

        /// Show bulk import form
        [HttpGet("employees/import")]
        public async Task<IActionResult> ImportEmployees()
        {
            if (!await _LS.IsLicenseValidAsync())
            {
                return RedirectToAction("LicenseExpired");
            }

            return View(new EmployeeImportViewModel());
        }
        // Process CSV import (FR 4.1.3)
        //Expected CSV format: EmployeeNumber, Full_Name, Email_Address, Position, Department, ManagerId

        [HttpPost("employees/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEmployees(IFormFile csvFile)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                if (csvFile == null || csvFile.Length == 0)
                {
                    TempData["Error"] = "Please select a CSV file to upload.";
                    return RedirectToAction("ImportEmployees");
                }

                // Validate file extension
                if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "Please upload a valid CSV file.";
                    return RedirectToAction("ImportEmployees");
                }

                _logger.LogInformation($"Processing employee import: {csvFile.FileName}");

                // Parse CSV
                var employees = new List<EmployeeImportDto>();
                using (var reader = new StreamReader(csvFile.OpenReadStream()))
                using (var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture))

                {
                    employees = csv.GetRecords<EmployeeImportDto>().ToList();
                }

                if (!employees.Any())
                {
                    TempData["Error"] = "CSV file is empty or invalid format.";
                    return RedirectToAction("ImportEmployees");
                }

                // Process bulk load
                var result = await _ES.BulkLoadEmployeesAsync(employees);

                // Build result message
                var successMessage = $"Import completed: {result.CreatedCount} created, {result.UpdatedCount} updated";
                if (result.FailedCount > 0)
                {
                    successMessage += $", {result.FailedCount} failed";
                }

                TempData["Success"] = successMessage;

                // Store errors in TempData if any
                if (result.Errors.Any())
                {
                    TempData["ImportErrors"] = string.Join("\n", result.Errors);
                }

                _logger.LogInformation($"Employee import completed: Created={result.CreatedCount}, Updated={result.UpdatedCount}, Failed={result.FailedCount}");

                return RedirectToAction("Employees");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing employees");
                TempData["Error"] = $"Failed to import employees: {ex.Message}";
                return RedirectToAction("ImportEmployees");
            }
        }
      
        /// Download CSV template for employee import
        [HttpGet("employees/download-template")]
        public IActionResult DownloadEmployeeTemplate()
        {
            var csv = "EmployeeNumber,Full_Name,Email_Address,Position,Department,ManagerId\n";
            csv += "EMP001,John Doe,john.doe@cityofjoburg.org.za,Senior Manager,Finance,\n";
          

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "employee_import_template.csv");
        }
        // View employee details
        [HttpGet("employees/{id}")]
        public async Task<IActionResult> EmployeeDetails(int id)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                var employee = await _ES.GetEmployeeByIdAsync(id);
                if (employee == null)
                {
                    TempData["Error"] = "Employee not found.";
                    return RedirectToAction("Employees");
                }

                var tasks = await _FS.GetTasksForEmployeeAsync(id);
                var subordinates = await _ES.GetSubordinatesAsync(id);

                var viewModel = new EmployeeDetailsViewModel
                {
                    Employee = employee,
                    Tasks = tasks.ToList(),
                    Subordinates = subordinates,
                    ComplianceRate = CalculateEmployeeComplianceRate(tasks.ToList())
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading employee details: {id}");
                TempData["Error"] = "Failed to load employee details.";
                return RedirectToAction("Employees");
            }
        }

        // ============================================================================
        // TEMPLATE MANAGEMENT (FR 4.2.1, 4.2.2)
        // ============================================================================
        // Template management view

        [HttpGet("templates")]
        public async Task<IActionResult> Templates()
        {
            if (!await _LS.IsLicenseValidAsync())
            {
                return RedirectToAction("LicenseExpired");
            }

            var templates = await _TS.GetAllAsync();

            var viewModel = new TemplateManagementViewModel
            {
                Templates = templates.ToList()
            };

            return View(viewModel);
        }

        //Create new template form
        [HttpGet("templates/create")]
        public async Task<IActionResult> CreateTemplate()
        {
            if (!await _LS.IsLicenseValidAsync())
            {
                return RedirectToAction("LicenseExpired");
            }

            return View(new TemplateCreateViewModel());
        }

        // Save new template
        [HttpPost("templates/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTemplate(TemplateCreateViewModel model)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var definition = new TemplateDefinition
                {
                    TemplateName = model.TemplateName,
                    Description = model.Description,
                    Config = model.Config
                };

                var template = await _TS.CreateAsync(definition);

                TempData["Success"] = $"Template '{template.TemplateName}' created successfully.";
                return RedirectToAction("Templates");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating template");
                TempData["Error"] = $"Failed to create template: {ex.Message}";
                return View(model);
            }
        }
        // Edit template form
        [HttpGet("templates/edit/{id}")]
        public async Task<IActionResult> EditTemplate(int id)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                var template = await _TS.GetByIdAsync(id);
                var definition = await _TS.GetDefinitionAsync(id);

                var viewModel = new TemplateEditViewModel
                {
                    TemplateId = template.TemplateId,
                    TemplateName = definition.TemplateName,
                    Description = definition.Description,
                    Config = definition.Config,
                    Status = template.Status
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading template: {id}");
                TempData["Error"] = "Template not found.";
                return RedirectToAction("Templates");
            }
        }

        // Update template
        [HttpPost("templates/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTemplate(int id, TemplateEditViewModel model)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var definition = new TemplateDefinition
                {
                    TemplateName = model.TemplateName,
                    Description = model.Description,
                    Config = model.Config
                };

                await _TS.UpdateAsync(id, definition);

                TempData["Success"] = "Template updated successfully.";
                return RedirectToAction("Templates");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating template: {id}");
                TempData["Error"] = $"Failed to update template: {ex.Message}";
                return View(model);
            }
        }

        // Publish template (change status to Active)
        [HttpPost("templates/publish/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishTemplate(int id)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                await _TS.PublishTemplateAsync(id);

                TempData["Success"] = "Template published successfully.";
                return RedirectToAction("Templates");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing template: {id}");
                TempData["Error"] = $"Failed to publish template: {ex.Message}";
                return RedirectToAction("Templates");
            }
        }

        /// Archive template
        [HttpPost("templates/archive/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveTemplate(int id)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                await _TS.ArchiveTemplateAsync(id);

                TempData["Success"] = "Template archived successfully.";
                return RedirectToAction("Templates");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error archiving template: {id}");
                TempData["Error"] = $"Failed to archive template: {ex.Message}";
                return RedirectToAction("Templates");
            }
        }

        // ============================================================================
        // TASK MANAGEMENT
        // ============================================================================
        // View all tasks with filtering

        [HttpGet("tasks")]
        public async Task<IActionResult> Tasks(string status = null, int? templateId = null)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                IEnumerable<FormTask> tasks;

                if (!string.IsNullOrEmpty(status))
                {
                    tasks = await _FS.GetTasksByStatusAsync(status);
                }
                else if (templateId.HasValue)
                {
                    tasks = await _FS.GetTasksByTemplateAsync(templateId.Value);
                }
                else
                {
                    // Get all recent tasks
                    var startDate = DateTime.UtcNow.AddMonths(-3);
                    var endDate = DateTime.UtcNow.AddMonths(1);
                    tasks = await _FS.GetTasksDueInRangeAsync(startDate, endDate);
                }

                var templates = await _TS.GetAllAsync();

                var viewModel = new TaskManagementViewModel
                {
                    Tasks = tasks.ToList(),
                    Templates = templates.ToList(),
                    SelectedStatus = status,
                    SelectedTemplateId = templateId
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks");
                TempData["Error"] = "Failed to load tasks.";
                return View(new TaskManagementViewModel());
            }
        }
        // Extend due date for tasks
        [HttpPost("tasks/extend-due-date")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtendDueDate(List<int> taskIds, DateTime newDueDate)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                if (taskIds == null || !taskIds.Any())
                {
                    TempData["Error"] = "Please select at least one task.";
                    return RedirectToAction("Tasks");
                }

                if (newDueDate <= DateTime.UtcNow)
                {
                    TempData["Error"] = "New due date must be in the future.";
                    return RedirectToAction("Tasks");
                }

                var count = await _FS.BulkExtendDueDateAsync(taskIds, newDueDate);

                TempData["Success"] = $"Extended due date for {count} tasks to {newDueDate:MMMM d, yyyy}";
                return RedirectToAction("Tasks");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending due dates");
                TempData["Error"] = $"Failed to extend due dates: {ex.Message}";
                return RedirectToAction("Tasks");
            }
        }
        // Cancel task
        [HttpPost("tasks/cancel/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTask(int id)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                var success = await _FS.CancelTaskAsync(id);

                if (success)
                {
                    TempData["Success"] = "Task cancelled successfully.";
                }
                else
                {
                    TempData["Error"] = "Task could not be cancelled (may already be submitted/reviewed).";
                }

                return RedirectToAction("Tasks");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling task: {id}");
                TempData["Error"] = $"Failed to cancel task: {ex.Message}";
                return RedirectToAction("Tasks");
            }
        }

        // ============================================================================
        // CREDIT MANAGEMENT
        // ============================================================================
        // Credit management view
        [HttpGet("credits")]
        public async Task<IActionResult> Credits()
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                var balance = await _CS.GetAvailableCreditsAsync();
                var batches = await _CS.GetCreditBatchesAsync();
                var expiringCredits = await _CS.GetExpiringCreditsAsync(30);

                var viewModel = new CreditManagementViewModel
                {
                    CurrentBalance = balance,
                    CreditBatches = batches,
                    ExpiringCredits = expiringCredits,
                    LowBalanceWarning = balance < 50,
                    CriticalBalanceWarning = balance < 20
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading credit information");
                TempData["Error"] = "Failed to load credit information.";
                return View(new CreditManagementViewModel());
            }
        }
        /// Sync credits with central hub (NFR 5.2.5)
        [HttpPost("credits/sync")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncCredits()
        {
            try
            {
                await _LS.SyncWithCentralHubAsync();

                TempData["Success"] = "Successfully synced with central hub. Credits and license updated.";
                return RedirectToAction("Credits");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing with central hub");
                TempData["Error"] = $"Failed to sync with central hub: {ex.Message}";
                return RedirectToAction("Credits");
            }
        }

        // ============================================================================
        // REPORTS AND ANALYTICS
        // ============================================================================
        // Compliance reports view
        [HttpGet("reports")]
        public async Task<IActionResult> Reports()
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                var dashboardData = await _FS.GetComplianceDashboardDataAsync();

                var viewModel = new ReportsViewModel
                {
                    ComplianceData = dashboardData,
                    ReportGeneratedAt = DateTime.UtcNow
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports");
                TempData["Error"] = "Failed to load reports.";
                return View(new ReportsViewModel());
            }
        }
        // Export compliance report to CSV
        [HttpGet("reports/export")]
        public async Task<IActionResult> ExportComplianceReport()
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return RedirectToAction("LicenseExpired");
                }

                var dashboardData = await _FS.GetComplianceDashboardDataAsync();

                var csv = "Department,Total Tasks,Outstanding,Overdue,Submitted,Reviewed,Compliance %\n";

                foreach (var dept in dashboardData.DepartmentBreakdown)
                {
                    csv += $"{dept.Key},{dept.Value.TotalTasks},{dept.Value.OutstandingCount},{dept.Value.OverdueCount},{dept.Value.SubmittedCount},{dept.Value.ReviewedCount},{dept.Value.CompliancePercentage:F2}\n";
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                var fileName = $"Compliance_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                TempData["Error"] = "Failed to export report.";
                return RedirectToAction("Reports");
            }
        }

        // ============================================================================
        // API ENDPOINTS (For AJAX calls)
        // ============================================================================
        /// Get real-time compliance metrics (AJAX)
        [HttpGet("api/metrics")]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                var dashboardData = await _FS.GetComplianceDashboardDataAsync();
                var creditBalance = await _CS.GetAvailableCreditsAsync();

                return Json(new
                {
                    totalEmployees = dashboardData.TotalEmployees,
                    totalTasks = dashboardData.TotalTasks,
                    outstandingCount = dashboardData.OutstandingCount,
                    overdueCount = dashboardData.OverdueCount,
                    submittedCount = dashboardData.SubmittedCount,
                    reviewedCount = dashboardData.ReviewedCount,
                    compliancePercentage = dashboardData.CompliancePercentage,
                    creditBalance = creditBalance,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metrics");
                return StatusCode(500, "Error fetching metrics");
            }
        }
        // Get department breakdown (AJAX)
        [HttpGet("api/department-breakdown")]
        public async Task<IActionResult> GetDepartmentBreakdown()
        {
            try
            {
                var breakdown = await _FS.GetDepartmentBreakdownAsync();

                return Json(breakdown.Select(d => new
                {
                    department = d.Key,
                    totalTasks = d.Value.TotalTasks,
                    outstandingCount = d.Value.OutstandingCount,
                    overdueCount = d.Value.OverdueCount,
                    submittedCount = d.Value.SubmittedCount,
                    reviewedCount = d.Value.ReviewedCount,
                    compliancePercentage = d.Value.CompliancePercentage
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching department breakdown");
                return StatusCode(500, "Error fetching department breakdown");
            }
        }
        // ============================================================================
        // HELPER METHODS
        // ============================================================================

        // Calculate compliance rate for a single employee
        private double CalculateEmployeeComplianceRate(List<FormTask> tasks)
        {
            if (!tasks.Any()) return 100.0;

            var compliant = tasks.Count(t => t.Status == "Submitted" || t.Status == "Reviewed");
            return Math.Round((double)compliant / tasks.Count * 100, 2);
        }


        [HttpGet]

        public async Task<IActionResult> InitialSetup()
        {

            return View();
        }

        // POST: Initial Setup - Set password and signature
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitialSetup(InitalSetupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await _US.CreateOrUpdateAuthUserAsync(model.Email, model.Password, model.SignatureData);
              return RedirectToAction("Dashboard");
            }


            return View(model);
        }
       
        

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
