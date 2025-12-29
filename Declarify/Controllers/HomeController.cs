using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Declarify.Data;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Declarify.Services;
using Declarify.Services.API;
using Declarify.Services.Methods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Database;
using OfficeOpenXml.Style;
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
        private readonly IEmailService _EmailS;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly CentralHubApiService _centralHub;
        public HomeController(ILogger<HomeController> logger, IEmailService email, ApplicationDbContext db, IEmployeeService eS, ITemplateService tS, IFormTaskService fS, ICreditService cS, ILicenseService lS, IVerificationService vS, IUserService uS, ISubmissionService sS, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, CentralHubApiService centralHub)
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
            _EmailS = email;
            _centralHub = centralHub;
        }

        public async Task<IActionResult> TestPing()
        {
            try
            {
                var result = await _centralHub.PingAsync();

                ViewBag.PingResult = result; // Will show { Message: "Pong", CompanyCode: "ACME001" }
                ViewBag.Status = "Success";
            }
            catch (HttpRequestException ex)
            {
                ViewBag.Status = "Failed";
                ViewBag.Error = ex.Message;
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    ViewBag.Error = "Invalid or missing CompanyCode/ApiKey - License check failed";
                }
            }
            catch (Exception ex)
            {
                ViewBag.Status = "Error";
                ViewBag.Error = ex.Message;
            }

            return View();
        }


        public async Task<IActionResult> LandingPage()
        {
            if (await _LS.IsLicenseValidAsync())
            {
                TempData["Info"] = "Your license is already active. Please log in to continue.";
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return Json(new { isValid = false, message = "Please enter a license key" });
            }

            // Call activation api
            var result = await _centralHub.ActivateLicenseAsync(licenseKey);

            if (result == null || !result.isValid)
            {
                return Json(new
                {
                    isValid = false,
                    message = result?.message ?? "Unable to validate license. Please try again.",
                    IsExpired = result?.isValid ?? false
                });
            }

            //1. SUCCESS! Save the company details for future use
            await _LS.SyncLicenseFromCentralAsync(licenseKey,result.companyId, result.ExpiryDate, result.isValid);

            //2. Collect admin initial
            if (!string.IsNullOrWhiteSpace(result.Email))
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(result.Email);

                ApplicationUser adminUser;

                if (existingUser == null)
                {
                    // Create new Identity user
                    adminUser = new ApplicationUser
                    {
                        UserName = result.Email,
                        Email = result.Email,
                        Full_Name = result.FullName ?? "System Administrator",
                        PhoneNumber = result.PhoneNumber,
                        roleInCompany = "Admin",
                        Role = "Admin",
                        Department = result.Department,
                        IsFirstLogin = true,
                        EmailConfirmed = true
                    };

                    var createResult = await _userManager.CreateAsync(adminUser);

                    if (!createResult.Succeeded)
                    {
                        // Log errors
                        return Json(new { isValid = false, message = "Failed to create admin account" });
                    }

                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    adminUser = existingUser;
                    // Update details if changed
                    adminUser.Full_Name = result.FullName ?? adminUser.Full_Name;
                    adminUser.PhoneNumber = result.PhoneNumber ?? adminUser.PhoneNumber;
                    await _userManager.UpdateAsync(adminUser);
                }

                // 3. Create or update Employee record
                var employee = await _db.Employees.Include(e => e.ApplicationUser).FirstOrDefaultAsync(e => e.Email_Address == result.Email);

                if (employee == null)
                {
                    employee = new Employee
                    {
                        Full_Name = result.FullName ?? "System Administrator",
                        Email_Address = result.Email,
                        Position = result.JobTitle ?? "Administrator",
                        Department = result.Department,
                        IsActive = true,
                        ApplicationUserId = adminUser.Id,
                        ApplicationUser = adminUser
                    };
                    _db.Employees.Add(employee);
                }
                else
                {
                    employee.Full_Name = result.FullName ?? employee.Full_Name;
                    employee.Position = result.JobTitle ?? employee.Position;
                    employee.Department = result.Department ?? employee.Department;
                    employee.ApplicationUserId = adminUser.Id;
                }

                // Link back
                //adminUser.EmployeeId = employee.EmployeeId;

                await _db.SaveChangesAsync();

                // Update user with EmployeeId
                adminUser.EmployeeId = employee.EmployeeId;
                await _userManager.UpdateAsync(adminUser);
            }


            return Json(new
            {
                isValid = true,
                companyName = result.companyName,
                LicenseKey = licenseKey,
                emailDomain = result.emailDomain ?? "",
                expiryDate = result.ExpiryDate,
                daysUntilExpiry = result.daysUntilExpiry ,
                IsExpired = result.isValid,
                message = "License activated successfully"
            });
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
       
        //[HttpPost]
        //[AllowAnonymous]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Login1(LoginViewModel model, string? returnUrl = null)
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
                // Step 1: Find ApplicationUser by email
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", model.Email);
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return View(model);
                }

                // Step 2: Force initial setup if no password or first login
                var hasPassword = await _userManager.HasPasswordAsync(user);
                if (!hasPassword || user.IsFirstLogin)
                {
                    _logger.LogInformation("User {Email} requires initial password setup.", model.Email);
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    return RedirectToAction("InitialSetup", new { email = model.Email, token });
                }

                // Step 3: Attempt sign-in
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // === NEW: Load the related Employee record to get EmployeeId ===
                    // Assuming ApplicationUser has a navigation property or foreign key to Employee
                    // Common patterns: user.EmployeeId or user.ApplicationUserId in Employee table

                    Employee? employee = null;

                    // Option A: If ApplicationUser has EmployeeId property (recommended)
                    // if (user.EmployeeId.HasValue) { ... }

                    // Option B: Most common — query Employee by ApplicationUserId
                    employee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id && e.IsActive);

                    // If not found, log but still allow login (or handle differently based on your rules)
                    if (employee == null)
                    {
                        _logger.LogWarning("Logged in user {Email} has no linked active Employee record.", model.Email);
                        // You could still proceed, or force setup — your choice
                        // For now, proceed without EmployeeId (dashboard may fail gracefully later)
                    }

                    // === NEW: Add custom claims including EmployeeId ===
                    var claims = new List<Claim>();

                    if (employee != null)
                    {
                        claims.Add(new Claim("EmployeeId", employee.EmployeeId.ToString()));
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, employee.EmployeeId.ToString())); // Override if you want
                        claims.Add(new Claim(ClaimTypes.GivenName, employee.Full_Name ?? ""));
                        // Optional: add other useful info
                        // claims.Add(new Claim("Department", employee.Department ?? ""));
                    }

                    // Add or update claims
                    if (claims.Any())
                    {
                        var identity = (ClaimsIdentity)User.Identity!;
                        identity.AddClaims(claims);

                        // Important: Re-sign in with additional claims
                        await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, claims);
                    }
                    else
                    {
                        // Normal sign-in if no extra claims
                        // (But we already signed in above — this block only if you skip claims)
                    }

                    _logger.LogInformation("User {Email} logged in successfully. EmployeeId: {Id}",
                        model.Email, employee?.EmployeeId ?? -1);

                    TempData["Success"] = $"Welcome back, {employee?.Full_Name ?? user.Email}!";

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToDashboardByRole(user);
                }

                // Handle 2FA, lockout, etc. (unchanged)
                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction("LoginWith2fa", new { returnUrl, model.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User {Email} account locked out.", model.Email);
                    return View("Lockout");
                }

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
            try {

                _logger.LogInformation("Signature length: {Length}", model.SignatureData?.Length);

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

           
            }
            catch (Exception ex) { 
            
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
                   // return View("LicenseExpired");
                    return View("LandingPage");
                }

                if (!User.Identity.IsAuthenticated)
                {
                    // Redirect to login page (adjust the route if needed)
                    return RedirectToAction("Login", "Home");
                }

                //API Calls
                var creditBalanceResult = await _centralHub.CheckCreditBalance();




                var dashboardData = await _FS.GetComplianceDashboardDataAsync();
               // var creditBalance = await _CS.GetAvailableCreditsAsync();
                var creditBatches = await _CS.GetCreditBatchesAsync();
                var licenseStatus = await _LS.GetLicenseStatusMessageAsync();
                var licenseExpiryDate = await _LS.GetExpiryDateAsync();
                var templates = (await _TS.GetActiveTemplatesAsync()).ToList();


                // Check for low credit balance
                //var lowCreditWarning = creditBalance < 50;
                //var criticalCreditWarning = creditBalance < 20;

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
                    CreditBalance = creditBalanceResult?.currentBalance ?? 0,
                    CreditBatches = creditBatches,
                    ExpiringCredits = expiringCredits,
                    LowCreditWarning = creditBalanceResult?.currentBalance < 50,
                    CriticalCreditWarning = creditBalanceResult?.currentBalance < 20,

                    // License Information
                    LicenseStatus = licenseStatus,
                    LicenseExpiryDate = licenseExpiryDate,
                    DaysUntilLicenseExpiry = (licenseExpiryDate - DateTime.UtcNow).Days,

                    // Goal Tracking (G1: 95% compliance)
                    GoalComplianceRate = 95.0,
                    IsGoalAchieved = dashboardData.CompliancePercentage >= 95.0,
                    // === POPULATE BULK REQUEST DATA FOR MODAL (FR 4.3.1) ===

                    BulkData = new BulkRequestViewModel
                    {
                          Templates = templates,
                       // Templates = (await _TS.GetActiveTemplatesAsync()).ToList(),
                        Employees = await _ES.GetAllEmployeesAsync(),
                        Departments = await _ES.GetDepartmentEmployeeCountsAsync(),
                        SuggestedDueDate = DateTime.UtcNow.AddDays(30)
                    },
                    // In your Index method, after populating BulkData

                };

             
                // Populate bulk request data for the modal
                // === POPULATE BULK REQUEST DATA FOR MODAL (FR 4.3.1) ===



                return View(viewModel);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["Error"] = "Failed to load dashboard data. Please try again.";
                // return View(new DashboardViewModel());
                return RedirectToAction("Login");
            }
          
        }

        [HttpGet]
        public async Task<IActionResult> GetTemplate(int id)
        {
            try
            {
                var template = await _TS.GetByIdAsync(id);
                if (template == null)
                {
                    return NotFound(new { success = false, message = "Template not found" });
                }

                return Json(new
                {
                    success = true,
                    template = new
                    {
                        templateId = template.TemplateId,
                        templateName = template.TemplateName,
                        description = template.Description,
                        //fields = template.field,
                        // Add any other properties you need
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching template {TemplateId}", id);
                return Json(new { success = false, message = "Error fetching template" });
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

        [HttpGet]
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
        [HttpPost]
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

                // Parse employee selection first
                List<int> employeeIds = await ParseEmployeeSelection(model.EmployeeIdsJson);

                // Validate employee selection
                if (employeeIds == null || !employeeIds.Any())
                {
                    TempData["Error"] = "Please select at least one employee.";
                    return RedirectToAction("BulkRequest");
                }

                // FR 4.3.1: Validate due date is in the future
                if (model.DueDate <= DateTime.UtcNow)
                {
                    TempData["Error"] = "Due date must be in the future.";
                    return RedirectToAction("BulkRequest");
                }

                _logger.LogInformation($"Admin initiating bulk request: Template={model.TemplateId}, Employees={employeeIds.Count}, DueDate={model.DueDate:yyyy-MM-dd}");

                // Create tasks and send emails
                await CreateTasksAndSendEmails(
                    model.TemplateId,
                    model.DueDate,
                    employeeIds
                );

                TempData["Success"] = $"Successfully sent DOI requests to {employeeIds.Count} employees. Due date: {model.DueDate:MMMM d, yyyy}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk request");
                TempData["Error"] = $"Failed to process bulk request: {ex.Message}";
                return RedirectToAction("BulkRequest");
            }
        }


        // Parse employee selection from frontend (handles 'ALL' and 'DEPT:XXX' markers)
        private async Task<List<int>> ParseEmployeeSelection(string employeeIdsJson)
        {
            if (string.IsNullOrWhiteSpace(employeeIdsJson))
                return new List<int>();

            try
            {
                var selections = JsonConvert.DeserializeObject<List<string>>(employeeIdsJson);
                var employeeIds = new List<int>();

                foreach (var selection in selections)
                {
                    if (selection == "ALL")
                    {
                        // Get all employee IDs
                        var allEmployees = await _ES.GetAllEmployeesAsync();
                        return allEmployees.Select(e => e.EmployeeId).ToList();
                    }
                    else if (selection.StartsWith("DEPT:"))
                    {
                        // Get employees from specific department
                        var department = selection.Substring(5);
                        var deptEmployees = await _ES.GetEmployeesByDepartmentAsync(department);
                        employeeIds.AddRange(deptEmployees.Select(e => e.EmployeeId));
                    }
                    else if (int.TryParse(selection, out int empId))
                    {
                        // Direct employee ID
                        employeeIds.Add(empId);
                    }
                }

                // Remove duplicates
                return employeeIds.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing employee selection");
                return new List<int>();
            }
        }

        // FR 4.3.1, 4.3.2, 4.3.3: Create DOI tasks with unique access tokens and send email notifications

        // FR 4.3.1, 4.3.2, 4.3.3: Create DOI tasks with unique access tokens and send email notifications
        private async Task<int> CreateTasksAndSendEmails(
            int templateId,
            DateTime dueDate,
            List<int> employeeIds)
        {
            int successCount = 0;
            var template = await _TS.GetByIdAsync(templateId);

            if (template == null)
            {
                throw new Exception($"Template with ID {templateId} not found");
            }

            foreach (var employeeId in employeeIds)
            {
                try
                {
                    var employee = await _ES.GetEmployeeByIdAsync(employeeId);
                    if (employee == null)
                    {
                        _logger.LogWarning($"Employee {employeeId} not found, skipping");
                        continue;
                    }

                    // FR 4.3.2: Generate unique, non-guessable, time-bound access token
                    var accessToken = GenerateSecureAccessToken();
                    var tokenExpiry = dueDate.AddDays(1); // Token valid until 1 day after due date

                    // Create DOI Task record
                    var task = new DOITask
                    {
                        EmployeeId = employeeId,
                        TemplateId = templateId,
                        DueDate = dueDate,
                        Status = "Outstanding", // Initial status
                        AccessToken = accessToken,
                        TokenExpiry = tokenExpiry,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = User.Identity.Name ?? "Admin"
                    };

                    // Save task to database (this should return the created task with TaskId)
                    var createdTasks = await _FS.BulkCreateTasksAsync(
    task.TemplateId,
    task.DueDate,
    employeeIds
);

                    if (createdTasks == null)
                    {
                        _logger.LogError($"Failed to create task for employee {employeeId}");
                        continue;
                    }

                    //// FR 4.3.3: Send unique email with access link
                    //var accessLink = GenerateAccessLink(createdTasks.);
                    //await SendDOIRequestEmail(employee, template, dueDate, accessLink);

                    //_logger.LogInformation(
                    //    $"Created DOI task {createdTasks.TaskId} for employee {employee.Full_Name} ({employee.Email_Address}) with token"
                    //);

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        $"Failed to create task for employee {employeeId}"
                    );
                    // Continue processing other employees
                }
            }

            return successCount;
        }

        // FR 4.3.2: Generate cryptographically secure, non-guessable access token
        // Token format: Base64-encoded 32-byte random value (43 characters)
        private string GenerateSecureAccessToken()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] tokenBytes = new byte[32]; // 256 bits
                rng.GetBytes(tokenBytes);

                // Convert to URL-safe Base64
                return Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');
            }
        }

        /// <summary>
        /// Generate unique access link for employee
        /// Format: https://yourapp.com/employee/task?token={accessToken}
        /// </summary>
        private string GenerateAccessLink(string accessToken)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/employee/task?token={accessToken}";
        }

        /// <summary>
        /// FR 4.3.3: Send DOI request email to employee with unique access link
        /// </summary>
        private async Task SendDOIRequestEmail(
            Employee employee,
            Template template,
            DateTime dueDate,
            string accessLink)
        {
            var subject = $"Action Required: Complete your {template.TemplateName}";

            var body = $@"
<html>
<body style='font-family: Arial, sans-serif; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2 style='color: #081B38;'>Declaration of Interest Required</h2>
        
        <p>Dear {employee.Full_Name},</p>
        
        <p>You are required to complete your <strong>{template.TemplateName}</strong> 
           by <strong>{dueDate:MMMM d, yyyy h:mm tt}</strong>.</p>
        
        <p>Please click the button below to access your personalized declaration form:</p>
        
        <div style='text-align: center; margin: 30px 0;'>
            <a href='{accessLink}' 
               style='background-color: #00C2CB; color: #081B38; padding: 12px 30px; 
                      text-decoration: none; border-radius: 50px; display: inline-block;
                      font-weight: 600;'>
                Complete Declaration
            </a>
        </div>
        
        <p style='color: #64748B; font-size: 14px;'>
            <strong>Important:</strong>
            <ul>
                <li>This link is unique to you and should not be shared</li>
                <li>You can save your progress and return later using the same link</li>
                <li>The link expires on {dueDate.AddDays(1):MMMM d, yyyy}</li>
            </ul>
        </p>
        
        <p style='color: #999; font-size: 12px; margin-top: 30px; border-top: 1px solid #ddd; padding-top: 20px;'>
            If you have any questions or need assistance, please contact your HR or Compliance department.
            <br><br>
            <strong>Direct link:</strong> <a href='{accessLink}'>{accessLink}</a>
            <br><br>
            This is an automated message from the Compliance & Disclosure Hub (Declarify).
        </p>
    </div>
</body>
</html>
";

            await _EmailS.SendMagicLinkAsync(employee.Email_Address, subject, body);
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

      // [HttpPost("employees/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEmployees1(IFormFile csvFile)
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

        [HttpPost("employees/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEmployees(IFormFile excelFile)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                    return RedirectToAction("LicenseExpired");

                if (excelFile == null || excelFile.Length == 0)
                {
                    TempData["Error"] = "Please select an Excel file to upload.";
                    return RedirectToAction("ImportEmployees");
                }

                var extension = Path.GetExtension(excelFile.FileName).ToLowerInvariant();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    TempData["Error"] = "Please upload a valid Excel file (.xlsx or .xls).";
                    return RedirectToAction("ImportEmployees");
                }

                _logger.LogInformation($"Processing employee import: {excelFile.FileName}");

                // Parse Excel file
                var employees = new List<EmployeeImportDto>();

                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    stream.Position = 0;

                    try
                    {
                        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                        using (var package = new ExcelPackage(stream))
                        {
                            var worksheet = package.Workbook.Worksheets[0]; // First worksheet
                            var rowCount = worksheet.Dimension?.Rows ?? 0;

                            if (rowCount < 2) // Must have at least header + 1 data row
                            {
                                TempData["Error"] = "Excel file is empty or contains no data rows.";
                                return RedirectToAction("ImportEmployees");
                            }

                            // Read data starting from row 2 (row 1 is header)
                            for (int row = 2; row <= rowCount; row++)
                            {
                                // Skip empty rows
                                if (string.IsNullOrWhiteSpace(worksheet.Cells[row, 1].Text))
                                    continue;

                                var empDto = new EmployeeImportDto
                                {
                                    EmployeeNumber = worksheet.Cells[row, 1].Text?.Trim(),
                                    Full_Name = worksheet.Cells[row, 2].Text?.Trim(),
                                    Email_Address = worksheet.Cells[row, 3].Text?.Trim(),
                                    Position = worksheet.Cells[row, 4].Text?.Trim(),
                                    Department = worksheet.Cells[row, 5].Text?.Trim(),
                                    ManagerId = null
                                };

                                // Parse ManagerId if present
                                var managerIdText = worksheet.Cells[row, 6].Text?.Trim();
                                if (!string.IsNullOrWhiteSpace(managerIdText) && int.TryParse(managerIdText, out int managerId))
                                {
                                    empDto.ManagerId = managerId;
                                }

                                employees.Add(empDto);
                            }
                        }
                    }
                    catch (Exception excelEx)
                    {
                        _logger.LogError(excelEx, "Excel parsing error");
                        TempData["Error"] = $"Excel format error: {excelEx.Message}. Please ensure your Excel file is properly formatted.";
                        return RedirectToAction("ImportEmployees");
                    }
                }

                if (!employees.Any())
                {
                    TempData["Error"] = "Excel file contains no valid employee data.";
                    return RedirectToAction("ImportEmployees");
                }

                // Process each employee
                int createdCount = 0, updatedCount = 0, failedCount = 0;
                var errors = new List<string>();

                // First pass: Create/update all employees without manager assignments
                foreach (var empDto in employees)
                {
                    try
                    {
                        // Skip if essential data is missing
                        if (string.IsNullOrWhiteSpace(empDto.Email_Address) || string.IsNullOrWhiteSpace(empDto.EmployeeNumber))
                        {
                            errors.Add($"Skipped row: Missing employee number or email address");
                            failedCount++;
                            continue;
                        }

                        // Check if employee already exists by EmployeeNumber or Email
                        var existingEmployee = await _db.Employees
                            .Include(e => e.ApplicationUser)
                            .FirstOrDefaultAsync(e => e.EmployeeNumber == empDto.EmployeeNumber || e.Email_Address == empDto.Email_Address);

                        Employee employee;
                        if (existingEmployee != null)
                        {
                            // Update existing
                            existingEmployee.Full_Name = empDto.Full_Name;
                            existingEmployee.Position = empDto.Position;
                            existingEmployee.Department = empDto.Department;
                            existingEmployee.Email_Address = empDto.Email_Address;
                            // Don't set ManagerId yet - will do in second pass
                            _db.Employees.Update(existingEmployee);
                            employee = existingEmployee;
                            updatedCount++;
                        }
                        else
                        {
                            // Create new employee
                            employee = new Employee
                            {
                                EmployeeNumber = empDto.EmployeeNumber,
                                Full_Name = empDto.Full_Name,
                                Email_Address = empDto.Email_Address,
                                Position = empDto.Position,
                                Department = empDto.Department,
                                IsActive = true
                                // Don't set ManagerId yet - will do in second pass
                            };
                            await _db.Employees.AddAsync(employee);
                            createdCount++;
                        }

                        // Save changes to ensure EmployeeId is generated
                        await _db.SaveChangesAsync();

                        // Create or update ApplicationUser
                        if (employee.ApplicationUser == null)
                        {
                            var user = new ApplicationUser
                            {
                                UserName = empDto.Email_Address,
                                Email = empDto.Email_Address,
                                EmployeeId = employee.EmployeeId,
                                Full_Name = empDto.Full_Name,
                                Position = empDto.Position,
                                Department = empDto.Department,
                                IsFirstLogin = true
                            };

                            var password = "DefaultPassword123!"; // You may want to generate a secure random password
                            var result = await _userManager.CreateAsync(user, password);
                            if (!result.Succeeded)
                            {
                                errors.Add($"Failed to create user for {empDto.Email_Address}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                                failedCount++;
                                continue;
                            }

                            // Optionally assign role
                            await _userManager.AddToRoleAsync(user, "Employee");

                            // Link back to Employee
                            employee.ApplicationUserId = user.Id;
                            employee.ApplicationUser = user;
                            _db.Employees.Update(employee);
                            await _db.SaveChangesAsync();
                        }
                        else
                        {
                            // Update existing user
                            var user = employee.ApplicationUser;
                            user.Full_Name = employee.Full_Name;
                            user.Position = employee.Position;
                            user.Department = employee.Department;
                            _db.Update(user);
                            await _db.SaveChangesAsync();
                        }
                    }
                    catch (Exception innerEx)
                    {
                        errors.Add($"Failed processing {empDto.Email_Address}: {innerEx.Message}");
                        failedCount++;
                    }
                }

                // Second pass: Assign managers now that all employees exist
                foreach (var empDto in employees)
                {
                    try
                    {
                        if (empDto.ManagerId.HasValue && empDto.ManagerId.Value > 0)
                        {
                            var employee = await _db.Employees
                                .FirstOrDefaultAsync(e => e.EmployeeNumber == empDto.EmployeeNumber || e.Email_Address == empDto.Email_Address);

                            if (employee != null)
                            {
                                // Find manager by ManagerId
                                var manager = await _db.Employees.FindAsync(empDto.ManagerId.Value);

                                if (manager != null)
                                {
                                    employee.ManagerId = manager.EmployeeId;
                                    _db.Employees.Update(employee);
                                    await _db.SaveChangesAsync();

                                    _logger.LogInformation($"Assigned manager {manager.Full_Name} (ID: {manager.EmployeeId}) to employee {employee.Full_Name}");
                                }
                                else
                                {
                                    errors.Add($"Manager with ID {empDto.ManagerId} not found for {empDto.Email_Address}");
                                }
                            }
                        }
                    }
                    catch (Exception innerEx)
                    {
                        errors.Add($"Failed assigning manager for {empDto.Email_Address}: {innerEx.Message}");
                    }
                }

                TempData["Success"] = $"Import completed: {createdCount} created, {updatedCount} updated, {failedCount} failed";
                if (errors.Any())
                {
                    TempData["ImportErrors"] = string.Join("\n", errors);
                }

                _logger.LogInformation($"Employee import completed: Created={createdCount}, Updated={updatedCount}, Failed={failedCount}");

                return RedirectToAction("Employees");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing employees");
                TempData["Error"] = $"Failed to import employees: {ex.Message}";
                return RedirectToAction("ImportEmployees");
            }
        }

        [HttpGet("employees/download-template")]
        public IActionResult DownloadEmployeeTemplate()
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage())
                {
                

                    var worksheet = package.Workbook.Worksheets.Add("Employee Import Template");

                    // Add headers
                    worksheet.Cells[1, 1].Value = "EmployeeNumber";
                    worksheet.Cells[1, 2].Value = "Full_Name";
                    worksheet.Cells[1, 3].Value = "Email_Address";
                    worksheet.Cells[1, 4].Value = "Position";
                    worksheet.Cells[1, 5].Value = "Department";
                    worksheet.Cells[1, 6].Value = "ManagerId";

                    // Style headers
                    using (var range = worksheet.Cells[1, 1, 1, 6])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thick;
                    }

                    // Add sample data
                    worksheet.Cells[2, 1].Value = "EMP001";
                    worksheet.Cells[2, 2].Value = "John Doe";
                    worksheet.Cells[2, 3].Value = "john.doe@cityofjoburg.org.za";
                    worksheet.Cells[2, 4].Value = "Senior Manager";
                    worksheet.Cells[2, 5].Value = "Finance";
                    worksheet.Cells[2, 6].Value = ""; // No manager (top-level)

                    worksheet.Cells[3, 1].Value = "EMP002";
                    worksheet.Cells[3, 2].Value = "Jane Smith";
                    worksheet.Cells[3, 3].Value = "jane.smith@cityofjoburg.org.za";
                    worksheet.Cells[3, 4].Value = "Manager";
                    worksheet.Cells[3, 5].Value = "IT";
                    worksheet.Cells[3, 6].Value = 1; // Reports to EmployeeId 1

                    worksheet.Cells[4, 1].Value = "EMP003";
                    worksheet.Cells[4, 2].Value = "Bob Johnson";
                    worksheet.Cells[4, 3].Value = "bob.johnson@cityofjoburg.org.za";
                    worksheet.Cells[4, 4].Value = "Developer";
                    worksheet.Cells[4, 5].Value = "IT";
                    worksheet.Cells[4, 6].Value = 2; // Reports to EmployeeId 2

                    worksheet.Cells[5, 1].Value = "EMP004";
                    worksheet.Cells[5, 2].Value = "Alice Williams";
                    worksheet.Cells[5, 3].Value = "alice.williams@cityofjoburg.org.za";
                    worksheet.Cells[5, 4].Value = "Analyst";
                    worksheet.Cells[5, 5].Value = "Finance";
                    worksheet.Cells[5, 6].Value = 1; // Reports to EmployeeId 1

                    // Auto-fit columns
                    worksheet.Cells.AutoFitColumns();

                    // Add instructions in a second sheet
                    var instructionsSheet = package.Workbook.Worksheets.Add("Instructions");
                    instructionsSheet.Cells[1, 1].Value = "Employee Import Instructions";
                    instructionsSheet.Cells[1, 1].Style.Font.Bold = true;
                    instructionsSheet.Cells[1, 1].Style.Font.Size = 14;

                    instructionsSheet.Cells[3, 1].Value = "Column Descriptions:";
                    instructionsSheet.Cells[3, 1].Style.Font.Bold = true;

                    instructionsSheet.Cells[4, 1].Value = "• EmployeeNumber: Unique identifier for the employee (required)";
                    instructionsSheet.Cells[5, 1].Value = "• Full_Name: Employee's full name (required)";
                    instructionsSheet.Cells[6, 1].Value = "• Email_Address: Employee's email address (required, must be unique)";
                    instructionsSheet.Cells[7, 1].Value = "• Position: Job title/position";
                    instructionsSheet.Cells[8, 1].Value = "• Department: Department name";
                    instructionsSheet.Cells[9, 1].Value = "• ManagerId: Database ID of the manager (leave empty for top-level managers)";

                    instructionsSheet.Cells[11, 1].Value = "Important Notes:";
                    instructionsSheet.Cells[11, 1].Style.Font.Bold = true;

                    instructionsSheet.Cells[12, 1].Value = "• The first import will create employees with sequential IDs (1, 2, 3, etc.)";
                    instructionsSheet.Cells[13, 1].Value = "• For subsequent imports, reference the actual EmployeeId from the database";
                    instructionsSheet.Cells[14, 1].Value = "• Leave ManagerId empty for employees without a manager";
                    instructionsSheet.Cells[15, 1].Value = "• All employees are imported first, then manager relationships are assigned";
                    instructionsSheet.Cells[16, 1].Value = "• Default password for new users: DefaultPassword123!";

                    instructionsSheet.Cells.AutoFitColumns();

                    var bytes = package.GetAsByteArray();
                    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "employee_import_template.xlsx");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel template");
                TempData["Error"] = "Failed to generate template file.";
                return RedirectToAction("ImportEmployees");
            }
        }



        // Alternative method using CsvHelper for guaranteed compatibility
        [HttpGet("employees/download-template-csvhelper")]
        public IActionResult DownloadEmployeeTemplateCsvHelper()
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new StreamWriter(memoryStream, System.Text.Encoding.UTF8))
            using (var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // Write header
                csv.WriteField("EmployeeNumber");
                csv.WriteField("Full_Name");
                csv.WriteField("Email_Address");
                csv.WriteField("Position");
                csv.WriteField("Department");
                csv.WriteField("ManagerId");
                csv.NextRecord();

                // Write sample rows
                var sampleData = new[]
                {
            new { EmployeeNumber = "EMP001", Full_Name = "John Doe", Email_Address = "john.doe@cityofjoburg.org.za", Position = "Senior Manager", Department = "Finance", ManagerId = "" },
            new { EmployeeNumber = "EMP002", Full_Name = "Jane Smith", Email_Address = "jane.smith@cityofjoburg.org.za", Position = "Manager", Department = "IT", ManagerId = "1" },
            new { EmployeeNumber = "EMP003", Full_Name = "Bob Johnson", Email_Address = "bob.johnson@cityofjoburg.org.za", Position = "Developer", Department = "IT", ManagerId = "2" },
            new { EmployeeNumber = "EMP004", Full_Name = "Alice Williams", Email_Address = "alice.williams@cityofjoburg.org.za", Position = "Analyst", Department = "Finance", ManagerId = "1" }
        };

                foreach (var row in sampleData)
                {
                    csv.WriteField(row.EmployeeNumber);
                    csv.WriteField(row.Full_Name);
                    csv.WriteField(row.Email_Address);
                    csv.WriteField(row.Position);
                    csv.WriteField(row.Department);
                    csv.WriteField(row.ManagerId);
                    csv.NextRecord();
                }

                writer.Flush();
                var bytes = memoryStream.ToArray();

                return File(bytes, "text/csv", "employee_import_template.csv");
            }
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

        [HttpGet]
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
        [HttpGet]
        public async Task<IActionResult> CreateTemplate()
        {
            if (!await _LS.IsLicenseValidAsync())
            {
                return RedirectToAction("LicenseExpired");
            }

            return View(new TemplateCreateViewModel());
        }

        // Save new template
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateTemplate([FromBody] TemplateCreateViewModel model)
        {
            try
            {
                if (!await _LS.IsLicenseValidAsync())
                {
                    return Json(new { success = false, message = "License expired" });
                }

                var definition = new TemplateDefinition
                {
                    TemplateName = model.TemplateName,
                    Description = model.Description,
                    Config = model.Config
                };

                var template = await _TS.CreateAsync(definition);

                return Json(new { success = true, message = $"Template '{template.TemplateName}' created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating template");
                return Json(new { success = false, message = $"Failed to create template: {ex.Message}" });
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

        [HttpGet]
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
