using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly CentralHubApiService _centralHub;
        private readonly ICentralHubService _centralHubService;
        public HomeController(ILogger<HomeController> logger,  CentralHubApiService centralHub, RoleManager<IdentityRole>  roleManager,IEmailService email,ApplicationDbContext db, IEmployeeService eS, ITemplateService tS, IFormTaskService fS, ICreditService cS, ILicenseService lS, IVerificationService vS, IUserService uS, ISubmissionService sS, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
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
            _centralHub = centralHub;
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _EmailS= email;
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
            await _LS.SyncLicenseFromCentralAsync(licenseKey, result.companyId, result.ExpiryDate, result.isValid);

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

                await _db.SaveChangesAsync();

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
                daysUntilExpiry = result.daysUntilExpiry,
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
        //[ValidateAntiForgeryToken]
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

            return user.Role.ToLower().Trim() switch
            {
                "Admin" => RedirectToAction("Index", "Home"), // Admin Dashboard (FR 4.5.1)
                "manager" or "reviewer" => RedirectToAction("Dashboard", "Manager"), // Manager/Reviewer Dashboard (FR 4.5.2)
                "employee" => RedirectToAction("Dashboard", "Employee"), // Employee Dashboard
                "executive" or "it deputy director" or "senior management" => RedirectToAction("ExecutiveDashboard", "Executive"), // Executive Dashboard (FR 4.5.3)
                _ => RedirectToAction("Index", "Home") // Default fallback
            };
        }

        // Main dashboard view - displays compliance overview (FR 4.5.1)
        [HttpGet("")]
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            try
            {
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
                if (creditBalanceResult == null || !creditBalanceResult.hasCredits)
                {
                    TempData["ErrorCheckCredits"] = creditBalanceResult == null ? "Cannot verify credits — license server unreachable. Please try again later." : "";
                }

                var companyData = await _centralHub.GetCompanyInformation();
                if (companyData == null || companyData.CompanyName == "Unknown Company")
                {
                    TempData["Error"] = "Cannot verify company data — Company server unreachable. Please try again later.";
                }


                var employee = await _ES.GetEmployeeByEmailAsync(User.Identity.Name);
                if (employee == null)
                {
                    TempData["Error"] = "Employee not found.";
                    return RedirectToAction("Employees");
                }


                var dashboardData = await _FS.GetComplianceDashboardDataAsync();
                var creditBalance = await _CS.GetAvailableCreditsAsync();
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
                    //Company Info
                    CompanyName = companyData.CompanyName,
                    AdminName = employee.Full_Name ?? "Administrator",
                    AdminEmail = employee.Email_Address,

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

                    //// Credit Information
                    CreditBalance = creditBalanceResult?.currentBalance ?? 0,
                    CreditBatches = creditBatches,
                    ExpiringCredits = expiringCredits,
                    //LowCreditWarning = creditBalanceResult?.currentBalance < 50,
                    //CriticalCreditWarning = creditBalanceResult?.currentBalance < 20,

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["Error"] = "Failed to load dashboard data. Please try again.";
                // return View(new DashboardViewModel());
                return RedirectToAction("Login");
            }

        }

        public async Task<IActionResult> Template() {
            return View();
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

            if (!employeeIds.Any()) return 0;

            int employeeCount = employeeIds.Count;

            // 1. Check if organization has enough credits (1 credit per employee)
            CreditCheckResponse? creditCheck = await _centralHub.CheckCreditBalance();
            if (creditCheck == null)
            {
                throw new Exception("Cannot verify credits — license server unreachable.");
            }

            if (!creditCheck.hasCredits || creditCheck.currentBalance < employeeCount)
            {
                throw new Exception("Insufficient credits. Required: " + employeeCount +
                                    $", Available: {creditCheck.currentBalance}");
            }
            var createdTaskIds = new List<int>();
            int successCount = 0;
            var template = await _TS.GetByIdAsync(templateId);

            if (template == null)
            {
                throw new Exception($"Template with ID {templateId} not found");
            }

            //2. 
            var consumeResult = await _centralHub.ConsumeCredits(employeeCount,$"DOI Task Creation - Template {templateId}, Due {dueDate:yyyy-MM-dd}, Employees: {string.Join(",", employeeIds)}");
            if (!consumeResult.Success)
            {
                throw new Exception("Failed to create Task for Employees — insufficient credits or server error");
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
                    var createdTasks = await _FS.BulkCreateTasksAsync(task.TemplateId, task.DueDate, employeeIds);

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
                    createdTaskIds.Add(task.TaskId);
                    successCount++;

                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        $"Failed to create task for employee {employeeId}"
                    );
                    // Continue processing other employees

                    //Refun for that employee
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
      

        

        // 2. Update the ImportEmployees method - Excel parsing section
        [HttpPost("employees/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEmployees(IFormFile excelFile)
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

            //check if email matches with domain
            var companyData = await _centralHub.GetCompanyInformation();
            if (companyData == null || companyData.CompanyName == "Unknown Company")
            {
                TempData["Error"] =  "Cannot verify company data — Company server unreachable. Please try again later.";
                return RedirectToAction("ImportEmployees");
            }



            var employees = new List<EmployeeImportDto>();

            // =======================
            // READ EXCEL
            // =======================
            using (var stream = new MemoryStream())
            {
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension.Rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    if (string.IsNullOrWhiteSpace(worksheet.Cells[row, 1].Text))
                        continue;

                    employees.Add(new EmployeeImportDto
                    {
                        EmployeeNumber = worksheet.Cells[row, 1].Text.Trim(),
                        Full_Name = worksheet.Cells[row, 2].Text.Trim(),
                        Email_Address = worksheet.Cells[row, 3].Text.Trim(),
                        Position = worksheet.Cells[row, 4].Text.Trim(),
                        Department = worksheet.Cells[row, 5].Text.Trim(),
                        ManagerEmployeeNumber = worksheet.Cells[row, 6].Text.Trim(),
                        Region = worksheet.Cells[row, 7].Text.Trim()
                    });
                }
            }

            if (!employees.Any())
            {
                TempData["Error"] = "Excel file contains no valid data.";
                return RedirectToAction("ImportEmployees");
            }

            var errors = new List<string>();
            int created = 0, updated = 0, failed = 0;

            // =======================
            // FIRST PASS: CREATE / UPDATE EMPLOYEES
            // =======================
            foreach (var dto in employees)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dto.EmployeeNumber) ||
                        string.IsNullOrWhiteSpace(dto.Email_Address))
                    {
                        failed++;
                        errors.Add("Missing EmployeeNumber or Email");
                        continue;
                    }

                   if (!dto.Email_Address.EndsWith(companyData.Domain, StringComparison.OrdinalIgnoreCase))
                    {
                        failed++;
                        errors.Add("Employee email is not part of the org domain");
                        continue;
                   }

                    var employee = await _db.Employees.Include(e => e.ApplicationUser).FirstOrDefaultAsync(e => e.EmployeeNumber == dto.EmployeeNumber);

                    if (employee == null)
                    {
                        employee = new Employee
                        {
                            EmployeeNumber = dto.EmployeeNumber,
                            Full_Name = dto.Full_Name,
                            Email_Address = dto.Email_Address,
                            Position = dto.Position,
                            Department = dto.Department,
                            Region = dto.Region,
                            IsActive = true
                        };
                        _db.Employees.Add(employee);
                        created++;
                    }
                    else
                    {
                        employee.Full_Name = dto.Full_Name;
                        employee.Email_Address = dto.Email_Address;
                        employee.Position = dto.Position;
                        employee.Department = dto.Department;
                        employee.Region = dto.Region;
                        employee.IsActive = true;
                        updated++;
                    }

                    await _db.SaveChangesAsync();

                    // =======================
                    // USER SYNC
                    // =======================
                    var user = await _userManager.FindByEmailAsync(dto.Email_Address);
                    if (user == null)
                    {
                        user = new ApplicationUser
                        {
                            UserName = dto.Email_Address,
                            Email = dto.Email_Address,
                            EmailConfirmed = true,
                            EmployeeId = employee.EmployeeId,
                            Full_Name = dto.Full_Name,
                            Position = dto.Position,
                            Department = dto.Department,
                            IsFirstLogin = true
                        };
                        await _userManager.CreateAsync(user);
                    }

                    employee.ApplicationUserId = user.Id;
                    _db.Employees.Update(employee);
                    await _db.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"Failed employee {dto.EmployeeNumber}: {ex.Message}");
                }
            }

            // =======================
            // SECOND PASS: ASSIGN MANAGERS
            // =======================
            foreach (var dto in employees)
            {
                if (!dto.Email_Address.EndsWith(companyData.Domain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                if (string.IsNullOrWhiteSpace(dto.ManagerEmployeeNumber))
                    continue;

                var employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeNumber == dto.EmployeeNumber);

                var manager = await _db.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeNumber == dto.ManagerEmployeeNumber);

                if (employee == null || manager == null)
                {
                    errors.Add($"Manager not found for {dto.EmployeeNumber}");
                    continue;
                }

                employee.ManagerId = manager.EmployeeId;
                _db.Employees.Update(employee);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Import complete: {created} created, {updated} updated, {failed} failed";
            if (errors.Any())
                TempData["ImportErrors"] = string.Join("\n", errors);

            return RedirectToAction("Employees");
        }
        // 3. Update the template download method
        [HttpGet("employees/download-template")]
        public IActionResult DownloadEmployeeTemplate()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Employee Import Template");

            // Headers - Updated column name
            string[] headers = new[]
            {
        "EmployeeNumber",
        "Full_Name",
        "Email_Address",
        "Position",
        "Department",
        "ManagerEmployeeNumber", // Changed from ManagerId
        "Region"
    };

            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];

            // Style header
            using var headerRange = ws.Cells[1, 1, 1, headers.Length];
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);

            // Sample data - Updated to use employee numbers
            var samples = new[]
            {
        new object[] { "EMP001", "John Doe",   "john.doe@cityofjoburg.org.za",   "Senior Manager", "Finance", "",        "Gauteng" },
        new object[] { "EMP002", "Jane Smith", "jane.smith@cityofjoburg.org.za", "Manager",        "IT",      "EMP001",  "Western Cape" },
        new object[] { "EMP003", "Bob Johnson","bob.johnson@cityofjoburg.org.za","Developer",      "IT",      "EMP002",  "Gauteng" }
    };

            for (int i = 0; i < samples.Length; i++)
            {
                for (int j = 0; j < samples[i].Length; j++)
                    ws.Cells[i + 2, j + 1].Value = samples[i][j];
            }

            ws.Cells.AutoFitColumns();

            // Instructions sheet - Updated description
            var instr = package.Workbook.Worksheets.Add("Instructions");
            instr.Cells[1, 1].Value = "Employee Import Template Instructions";
            instr.Cells[1, 1].Style.Font.Bold = true;
            instr.Cells[1, 1].Style.Font.Size = 14;

            instr.Cells[3, 1].Value = "Columns:";
            instr.Cells[4, 1].Value = "• EmployeeNumber (required, unique)";
            instr.Cells[5, 1].Value = "• Full_Name (required)";
            instr.Cells[6, 1].Value = "• Email_Address (required, unique)";
            instr.Cells[7, 1].Value = "• Position";
            instr.Cells[8, 1].Value = "• Department";
            instr.Cells[9, 1].Value = "• ManagerEmployeeNumber – Employee Number of manager (e.g., EMP001). Leave blank for top-level employees.";
            instr.Cells[10, 1].Value = "• Region – e.g. Gauteng, Western Cape, KwaZulu-Natal...";

            instr.Cells[12, 1].Value = "Notes:";
            instr.Cells[12, 1].Style.Font.Bold = true;
            instr.Cells[13, 1].Value = "• Ensure all managers are included in the import file before their subordinates";
            instr.Cells[14, 1].Value = "• The ManagerEmployeeNumber must match an existing or imported EmployeeNumber";
            instr.Cells[15, 1].Value = "• You can import employees in any order - manager assignments happen in a second pass";

            instr.Cells.AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Employee_Import_Template.xlsx");
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

        [HttpGet]
        public async Task<IActionResult> GetPotentialManagers(string position, string department)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(position) || string.IsNullOrWhiteSpace(department))
                {
                    return Json(new { success = false, message = "Position and department are required" });
                }

                var managers = await _ES.GetPotentialManagersAsync(position, department);

                var managerList = managers.Select(m => new
                {
                    id = m.EmployeeId,
                    name = m.Full_Name,
                    position = m.Position,
                    department = m.Department,
                    employeeNumber = m.EmployeeNumber
                }).ToList();

                return Json(new { success = true, managers = managerList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching potential managers for position: {Position}, department: {Department}",
                    position, department);
                return Json(new { success = false, message = "Failed to load managers" });
            }
        }

        /// <summary>
        /// GET: Employee/GetAllManagers - Get all potential managers for dropdown
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllManagers()
        {
            try
            {
                var employees = await _ES.GetAllActiveEmployeesAsync();

                var managerList = employees.Select(e => new
                {
                    id = e.EmployeeId,
                    name = e.FullName,
                    position = e.Position,
                    department = e.Department,
                    employeeNumber = e.EmployeeNumber
                }).ToList();

                return Json(new { success = true, managers = managerList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all managers");
                return Json(new { success = false, message = "Failed to load managers" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(EmployeeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                TempData["Error"] = $"Validation failed: {string.Join(", ", errors)}";
                return RedirectToAction("Index");
            }

            try
            {
                //check if email matches with domain
                var companyData = await _centralHub.GetCompanyInformation();
                if (companyData == null || companyData.CompanyName == "Unknown Company")
                {
                    throw new Exception("Cannot verify company data — Company server unreachable. Please try again later.");
                }

                if (!model.Email.EndsWith(companyData.Domain, StringComparison.OrdinalIgnoreCase)) throw new Exception("Employee email is not part of the company");


                var employee = await _ES.CreateEmployeeAsync(model);

                TempData["Success"] = $"Employee {employee.Full_Name} created successfully! " +
                    $"A welcome email with login credentials has been sent to {employee.Email_Address}.";

                return RedirectToAction("Employees");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee: {Email}", model.Email);
                TempData["Error"] = "An unexpected error occurred while creating the employee. Please try again.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// GET: Employee/CheckEmployeeNumber - AJAX endpoint to check if employee number is unique
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CheckEmployeeNumber(string employeeNumber)
        {
            if (string.IsNullOrWhiteSpace(employeeNumber))
            {
                return Json(new { isUnique = false, message = "Employee number is required" });
            }

            try
            {
                var isUnique = await _ES.IsEmployeeNumberUniqueAsync(employeeNumber);
                return Json(new
                {
                    isUnique = isUnique,
                    message = isUnique ? "Employee number is available" : "Employee number already exists"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking employee number: {EmployeeNumber}", employeeNumber);
                return Json(new { isUnique = false, message = "Error checking employee number" });
            }
        }

        /// <summary>
        /// GET: Employee/CheckEmail - AJAX endpoint to check if email is unique
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CheckEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { isUnique = false, message = "Email is required" });
            }

            try
            {
                var isUnique = await _ES.IsEmailUniqueAsync(email);
                return Json(new
                {
                    isUnique = isUnique,
                    message = isUnique ? "Email is available" : "Email already exists"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email: {Email}", email);
                return Json(new { isUnique = false, message = "Error checking email" });
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
        public async Task<IActionResult> CreateTemplate([FromBody] TemplateCreateViewModel model)
        {
            try
            {
                _logger.LogInformation("CreateTemplate called");

                // Log the incoming Config JSON for debugging
                _logger.LogInformation("Received Config JSON: {Config}", model.Config);

                //Central Hub API Calls
                //1. Check if org has enough credits 
                CreditCheckResponse? creditCheck = null;

                creditCheck = await _centralHub.CheckCreditBalance();

                if (creditCheck == null || !creditCheck.hasCredits || creditCheck.currentBalance <= 0)
                {
                    TempData["ErrorCheckCredits"] = creditCheck == null ? "Cannot verify credits — license server unreachable. Please try again later." : "Your organization has no remaining credits. Please contact your administrator to top up.";
                    return Ok(new
                    {
                        success = false,
                        message = creditCheck == null ? "Cannot verify credits — license server unreachable. Please try again later." : "Your organization has no remaining credits. Please contact your administrator to top up."
                    });
                }

                //2. Deduct credits via central hub API
                var consumeResult = await _centralHub.ConsumeCredits(1, $"Creted Template");
                if (!consumeResult.Success)
                {
                    TempData["ErrorCheckCredits"] = consumeResult.Error ?? "Failed to record submission — insufficient credits or server error";

                    return Ok(new
                    {
                        success = false,
                        message = consumeResult.Error ?? "Failed to record submission — insufficient credits or server error"
                    });
                }


                if (model == null)
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                if (!await _LS.IsLicenseValidAsync())
                {
                    return Json(new { success = false, message = "License expired" });
                }

                if (string.IsNullOrWhiteSpace(model.TemplateName))
                {
                    return Json(new { success = false, message = "Template name is required" });
                }

                if (string.IsNullOrWhiteSpace(model.Config))
                {
                    return Json(new { success = false, message = "Configuration is required" });
                }

                TemplateConfig config;
                try
                {
                    // Use case-insensitive deserialization options
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    config = System.Text.Json.JsonSerializer.Deserialize<TemplateConfig>(model.Config, options);

                    if (config == null || config.Sections == null || config.Sections.Count == 0)
                    {
                        _logger.LogWarning("Deserialized config has no sections. Config JSON: {Config}", model.Config);
                        return Json(new { success = false, message = "Template must have at least one section" });
                    }

                    _logger.LogInformation("Successfully deserialized {Count} sections", config.Sections.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse config. Config JSON: {Config}", model.Config);
                    return Json(new { success = false, message = "Invalid configuration format: " + ex.Message });
                }

                var templateDefinition = new TemplateDefinition
                {
                    TemplateName = model.TemplateName.Trim(),
                    Description = model.Description?.Trim() ?? string.Empty,
                    Config = config,
                    Status = model.IsPublished ? TemplateStatus.Active : TemplateStatus.Draft,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var template = await _TS.CreateAsync(templateDefinition);

                _logger.LogInformation("Template created successfully: {TemplateId} with {SectionCount} sections",
                    template.TemplateId, config.Sections.Count);

                return Json(new
                {
                    success = true,
                    message = "Template saved successfully",
                    templateId = template.TemplateId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating template");

                //refund credits

                return Json(new { success = false, message = ex.Message });
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
            //auth access
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Home");
            }

            if (!await _LS.IsLicenseValidAsync())
            {
                // return View("LicenseExpired");
                return View("LandingPage");
            }

            try
            {
                //Collect credits and historical requests
                //API Calls
                var creditBalanceResult = await _centralHub.CheckCreditBalance();
                if (creditBalanceResult == null || !creditBalanceResult.hasCredits)
                {
                    TempData["ErrorCheckCredits"] = creditBalanceResult == null ? "Cannot verify credits — license server unreachable. Please try again later." : "";
                }

                var creditRequestResult = await _centralHub.CollectCreditRequests();
                //

                var viewModel = new CreditManagementViewModel
                {
                    CreditBalance = creditBalanceResult,
                    CreditRequests = creditRequestResult,
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCreditRequest(int RequestedCredits,string? Reason)
        {
            //auth access
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Home");
            }

            if (!await _LS.IsLicenseValidAsync())
            {
                // return View("LicenseExpired");
                return View("LandingPage");
            }



            try
            {

                var user = await _db.Employees.Where(em => em.Email_Address == User.Identity.Name.ToString()).FirstOrDefaultAsync();

                string email;
                string fullName;

                if (user == null)
                {
                    var appUser = await _userManager.FindByEmailAsync(User.Identity.Name);
                    if (appUser == null)
                    {
                        throw new Exception("Failed to Request Credits, Requester Details not found");
                    }

                    email = appUser.Email;
                    fullName = appUser.Full_Name;
                }
                else
                {
                    email = user.Email_Address;
                    fullName = user.Full_Name;
                }


                var result = await _centralHub.RequestCredits(RequestedCredits, Reason, fullName, email);

                if (!result.success)
                {
                    TempData["Error"] = "Failed to Request Credits.";
                }
                else
                {
                    TempData["Sucess"] = "Credits sucessfully requested";
                }

                return RedirectToAction("Credits");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting credits");
                TempData["Error"] = "Failed to Request Credits.";
                return RedirectToAction("Credits");
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
