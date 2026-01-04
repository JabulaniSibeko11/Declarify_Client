using Declarify.Data;
using Declarify.Helper;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;

namespace Declarify.Services.Methods
{
    public class EmployeeDOIService : IEmployeeDOIService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<EmployeeDOIService> _logger;
        private readonly ICreditService _creditService;
        private readonly IEmailService _emailService;
        private readonly IReviewerService _reviewerService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmployeeDOIService(
            ApplicationDbContext context,
            ILogger<EmployeeDOIService> logger,
            ICreditService creditService,
            IEmailService emailService,IReviewerService reviewerService, IHttpContextAccessor httpContextAccessor)
        {
            _db = context;
            _logger = logger;
            _creditService = creditService;
            _emailService = emailService;
            _reviewerService = reviewerService;
            _httpContextAccessor = httpContextAccessor;
        }

        #region Original Methods (FR 4.3.2, FR 4.4.1, FR 4.4.2, FR 4.4.3)

        // Retrieves the DOI task using the unique, time-bound token (FR 4.3.2)
        public async Task<FormTask?> GetDOITaskByTokenAsync(string token)
        {
            var task = await _db.DOITasks
                .Include(t => t.Employee)
                .Include(t => t.Template)
                .FirstOrDefaultAsync(t => t.AccessToken == token);

            if (task == null || task.TokenExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Invalid or expired token: {Token}", token);
                return null;
            }

            return task;
        }

        // Gets the form template configuration for the given task (FR 4.2.2, FR 4.2.3)
        public async Task<Template?> GetFormTemplateForTaskAsync(int taskId)
        {
            var templateId = await _db.DOITasks
                .Where(tk => tk.TaskId == taskId)
                .Select(tk => tk.TemplateId)
                .FirstOrDefaultAsync();

            if (templateId == 0)
                return null;

            return await _db.Templates
                .FirstOrDefaultAsync(t => t.TemplateId == templateId);
        }

        // Saves a draft of the DOI form (FR 4.4.1)
        public async Task SaveDraftAsync(string token, JsonDocument formData)
        {
            var task = await GetDOITaskByTokenAsync(token);
            if (task == null) throw new InvalidOperationException("Invalid task token");

            var submission = await _db.DOIFormSubmissions
                .FirstOrDefaultAsync(s => s.FormTaskId == task.TaskId && s.Status == "Draft");

            if (submission == null)
            {
                submission = new FormSubmission
                {
                    FormTaskId = task.TaskId,
                    FormData = formData.RootElement.ToString(),
                    Status = "Draft",
                    Submitted_Date = DateTime.UtcNow
                };
                _db.DOIFormSubmissions.Add(submission);
            }
            else
            {
                submission.FormData = formData.RootElement.ToString();
                submission.Submitted_Date = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Draft saved for task {TaskId}", task.TaskId);
        }

        // Submits the completed DOI form (FR 4.4.2, FR 4.4.3)
        public async Task<SubmissionResult> SubmitDOIAsync1(string token, JsonDocument formData, string attestationSignature)
        {
            var task = await GetDOITaskByTokenAsync(token);
            if (task == null) return new SubmissionResult { Success = false, Message = "Invalid task token" };

            // Check credits (FR 4.4.3)
            //if (!await _creditService.HasSufficientCreditsAsync(1))
            //{
            //    return new SubmissionResult
            //    {
            //        Success = false,
            //        Message = "Insufficient credits. Please contact your Admin."
            //    };
            //}

            // Consume credit
            //await _creditService.ConsumeCreditsAsync(1, "DOI Submission");

            // Check if a submission already exists for this task
            bool submissionExists = await _db.DOIFormSubmissions
                .AnyAsync(s => s.FormTaskId == task.TaskId);

            if (!submissionExists)
            {
                // Only create and save the submission if it doesn't exist
                var submission = new FormSubmission
                {
                    FormTaskId = task.TaskId,
                    FormData = formData.RootElement.ToString(),
                    Submitted_Date = DateTime.UtcNow,
                    DigitalAttestation = attestationSignature,
                    Status = "Submitted",

                    // === NEW: Assign the line manager here ===
            AssignedManagerId = task.Employee.ManagerId,
                    AssignedManagerName = task.Employee.Manager?.Full_Name  // If navigation property exists
                };
                if (task.Employee.ManagerId != null && submission.AssignedManagerName == null)
                {
                    var manager = await _db.Employees
                        .Where(e => e.EmployeeId == task.Employee.ManagerId)
                        .Select(e => e.Full_Name)
                        .FirstOrDefaultAsync();

                    submission.AssignedManagerName = manager;
                }
                _db.DOIFormSubmissions.Add(submission);
                _logger.LogInformation("New DOI submission created for task {TaskId}", task.TaskId);
            }
            else
            {
                _logger.LogInformation("Duplicate submission attempt for task {TaskId} - skipping insert", task.TaskId);
            }
            // Update task status
            task.Status = "Submitted";
            task.AccessToken = null; // Invalidate token after submission (NFR 5.3.2)

            await _db.SaveChangesAsync();
            _logger.LogInformation("DOI submitted for task {TaskId}", task.TaskId);

            // Notify reviewer/manager
            //if (task.Employee.ManagerId != null )
            //{
            //    var manager = await _db.Employees.FindAsync(task.Employee.ManagerId);
            //    if (manager != null)
            //    {
            //        //await _emailService.SendManagerNotificationAsync(
            //        //    manager.Email_Address,
            //        //    task.Employee.Full_Name,
            //        //    task.TaskId
            //        //);
            //    }
            //}

            return new SubmissionResult { Success = true, Message = "Submission successful" };
        }

        public async Task<SubmissionResult> SubmitDOIAsync(string token, JsonDocument formData, string attestationSignature)
        {
            var task = await GetDOITaskByTokenAsync(token);
            if (task == null)
                return new SubmissionResult { Success = false, Message = "Invalid task token" };


            // Check credits (FR 4.4.3)
            //if (!await _creditService.HasSufficientCreditsAsync(1))
            //{
            //    return new SubmissionResult
            //    {
            //        Success = false,
            //        Message = "Insufficient credits. Please contact your Admin."
            //    };
            //}

            // Consume credit
            //await _creditService.ConsumeCreditsAsync(1, "DOI Submission");


            // Check if a submission already exists
            bool submissionExists = await _db.DOIFormSubmissions
                .AnyAsync(s => s.FormTaskId == task.TaskId);

            FormSubmission submission;

            if (!submissionExists)
            {
                // Create new submission
                submission = new FormSubmission
                {
                    FormTaskId = task.TaskId,
                    FormData = formData.RootElement.ToString(),
                    Submitted_Date = DateTime.UtcNow,
                    DigitalAttestation = attestationSignature,
                    Status = "Submitted"
                };

                _db.DOIFormSubmissions.Add(submission);
                _logger.LogInformation("New DOI submission created for task {TaskId}", task.TaskId);
            }
            else
            {
                // Load existing submission so we can update it
                submission = await _db.DOIFormSubmissions
                    .FirstAsync(s => s.FormTaskId == task.TaskId);

                _logger.LogInformation("Duplicate submission attempt for task {TaskId} - updating existing record", task.TaskId);
            }

            // === ALWAYS assign/update the manager details (both new and existing submissions) ===
            submission.AssignedManagerId = task.Employee.ManagerId;

            // If we don't have the manager name via navigation property, fetch it
            if (task.Employee.ManagerId != null)
            {
                if (task.Employee.Manager?.Full_Name != null)
                {
                    submission.AssignedManagerName = task.Employee.Manager.Full_Name;
                }
                else
                {
                    // Fallback: query the name if not already loaded
                    var managerName = await _db.Employees
                        .Where(e => e.EmployeeId == task.Employee.ManagerId)
                        .Select(e => e.Full_Name)
                        .FirstOrDefaultAsync();

                    submission.AssignedManagerName = managerName;
                }
            }
            else
            {
                submission.AssignedManagerName = null;
            }

            // Always update task status and invalidate token
            task.Status = "Submitted";
            task.AccessToken = null; // Invalidate token (NFR 5.3.2)

            await _db.SaveChangesAsync();

            _logger.LogInformation("DOI submitted/processed for task {TaskId}", task.TaskId);

            return new SubmissionResult
            {
                Success = true,
                Message = submissionExists
                    ? "Submission already processed (updated manager assignment)"
                    : "Submission successful"
            };
        }

        // Sends automated reminders (FR 4.3.4)
        public async Task SendReminderEmailsAsync()
        {
            var now = DateTime.UtcNow;

            // Tasks on due date
            var dueTodayTasks = await _db.DOITasks
                .Include(t => t.Employee)
                .Where(t => t.DueDate.Date == now.Date && t.Status == "Outstanding")
                .ToListAsync();

            foreach (var task in dueTodayTasks)
            {
                var accessLink = await GenerateAccessLinkAsync(task);
                await _emailService.SendReminderAsync(
                    task.Employee.Email_Address,
                    task.Employee.Full_Name,
                    task.DueDate
                );
            }

            // Overdue within 7 days
            var overdueRecent = await _db.DOITasks
                .Include(t => t.Employee)
                .Where(t => t.DueDate < now && (now - t.DueDate).TotalDays <= 7 && t.Status == "Outstanding")
                .ToListAsync();

            foreach (var task in overdueRecent)
            {
                var accessLink = await GenerateAccessLinkAsync(task);
                await _emailService.SendReminderAsync(
                    task.Employee.Email_Address,
                    task.Employee.Full_Name,
                    task.DueDate
                );
            }

            _logger.LogInformation("Sent {Count} reminder emails", dueTodayTasks.Count + overdueRecent.Count);
        }

        public async Task<Employee?> GetEmployeeByIdAsync(int employeeId)
        {
            try
            {
                return await _db.Employees
                    .Where(e => e.EmployeeId == employeeId)
                    .Select(e => new Employee
                    {
                        EmployeeId = e.EmployeeId,
                        Full_Name = e.Full_Name,
                       
                        Email_Address = e.Email_Address,
                        Signature_Picture = e.Signature_Picture // Assuming Signature is stored as base64 string in the database
                    })
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee by ID: {EmployeeId}", employeeId);
                throw;
            }
        }

        #endregion

        #region Dashboard Methods

        // Gets comprehensive dashboard data for employee
        public async Task<EmployeeDashboardViewModel> GetEmployeeDashboardAsync(int employeeId)
        {
            // Load core employee data
            var employee = await GetEmployeeProfileAsync(employeeId);
            if (employee == null)
            {
                throw new InvalidOperationException("Employee not found");
            }

            var tasks = await GetEmployeeTasksAsync(employeeId);
            var stats = await GetEmployeeComplianceStatsAsync(employeeId);

            // Build the base view model
            var viewModel = new EmployeeDashboardViewModel
            {

                Employee = new EmployeeProfile {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName ?? string.Empty,
                    Email = employee.Email ?? string.Empty,
                    Position = employee.Position ?? string.Empty,
                    Department = employee.Department ?? string.Empty,
                    ManagerName = employee.ManagerName?? string.Empty,

                    // Determine roles based on position title
                    IsExecutive = EmployeeRoleHelper.IsExecutive(employee.Position),
                    IsSeniorManagement = EmployeeRoleHelper.IsSeniorManagement(employee.Position),
                    HasManagerTitle = EmployeeRoleHelper.HasManagerTitle(employee.Position)

                },

                Tasks = tasks,
                Stats = stats,
                HasPendingTasks = tasks.Any(t => t.Status == "Outstanding"),
                HasOverdueTasks = tasks.Any(t => t.Status == "Outstanding" && t.DueDate < DateTime.UtcNow)
            };

            // === Line Manager / Reviewer Functionality (PRD: FR 4.5.2) ===
            // Check if current employee has direct subordinates → they are a Reviewer/Line Manager
            viewModel.IsLineManager = await _reviewerService.IsLineManagerAsync(employeeId);

            if (viewModel.IsLineManager)
            {
                // Load compliance data for all direct subordinates
                viewModel.Subordinates = await _reviewerService.GetSubordinateComplianceAsync(employeeId);

                // Optional: Calculate how many submissions are awaiting review
                viewModel.PendingReviewsCount = viewModel.Subordinates
                    .SelectMany(s => s.Tasks)
                    .Count(t => t.Status == "Submitted"); // Only "Submitted" tasks need review
            }
            else
            {
                viewModel.Subordinates = new List<SubordinateComplianceViewModel>();
                viewModel.PendingReviewsCount = 0;
            }

            return viewModel;
        }   // Gets all tasks for an employee

        public async Task<EmployeeDashboardViewModel> GetAdminDashboardAsync(int employeeId)
        {
            // Load core employee data
            var employee = await GetEmployeeProfileAsync(employeeId);
            if (employee == null)
            {
                throw new InvalidOperationException("Employee not found");
            }

            var tasks = await GetEmployeeTasksAsync(employeeId);
            var stats = await GetEmployeeComplianceStatsAsync(employeeId);

            // Build the base view model
            var viewModel = new EmployeeDashboardViewModel
            {

                Employee = new EmployeeProfile
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName ?? string.Empty,
                    Email = employee.Email ?? string.Empty,
                    Position = employee.Position ?? string.Empty,
                    Department = employee.Department ?? string.Empty,
                    ManagerName = employee.ManagerName ?? string.Empty,

                    // Determine roles based on position title
                    IsExecutive = EmployeeRoleHelper.IsExecutive(employee.Position),
                    IsSeniorManagement = EmployeeRoleHelper.IsSeniorManagement(employee.Position),
                    HasManagerTitle = EmployeeRoleHelper.HasManagerTitle(employee.Position)

                },

                Tasks = tasks,
                Stats = stats,
                HasPendingTasks = tasks.Any(t => t.Status == "Outstanding"),
                HasOverdueTasks = tasks.Any(t => t.Status == "Outstanding" && t.DueDate < DateTime.UtcNow)
            };

            // === Line Manager / Reviewer Functionality (PRD: FR 4.5.2) ===
            // Check if current employee has direct subordinates → they are a Reviewer/Line Manager
            viewModel.IsLineManager = await _reviewerService.IsLineManagerAsync(employeeId);

            if (viewModel.IsLineManager)
            {
                // Load compliance data for all direct subordinates
                viewModel.Subordinates = await _reviewerService.GetSubordinateComplianceAsync(employeeId);

                // Optional: Calculate how many submissions are awaiting review
                viewModel.PendingReviewsCount = viewModel.Subordinates
                    .SelectMany(s => s.Tasks)
                    .Count(t => t.Status == "Submitted"); // Only "Submitted" tasks need review
            }
            else
            {
                viewModel.Subordinates = new List<SubordinateComplianceViewModel>();
                viewModel.PendingReviewsCount = 0;
            }

            return viewModel;
        }   // Gets all tasks for an employee



        public async Task<List<FormTask>> GetEmployeeTasksAsync(int employeeId)
        {
            return await _db.DOITasks
                .Include(t => t.Template)
                .Include(t => t.Employee)
                .Where(t => t.EmployeeId == employeeId)
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();
        }

        // Gets draft submission if exists
        public async Task<FormSubmission?> GetDraftSubmissionAsync(int taskId)
        {
            return await _db.DOIFormSubmissions
                .FirstOrDefaultAsync(s => s.FormTaskId == taskId && s.Status == "Draft");
        }

        // Gets employee profile information
        public async Task<EmployeeProfile> GetEmployeeProfileAsync(int employeeId)
        {
            var employee = await _db.Employees
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            if (employee == null)
                throw new InvalidOperationException($"Employee {employeeId} not found");

            return new EmployeeProfile
            {
                EmployeeId = employee.EmployeeId,
                FullName = employee.Full_Name,
                Email = employee.Email_Address,
                Position = employee.Position,
                Department = employee.Department,
                ManagerName = employee.Manager?.Full_Name
            };
        }

        // Validates access token
        public async Task<bool> ValidateAccessTokenAsync(string token)
        {
            var task = await GetDOITaskByTokenAsync(token);
            return task != null;
        }

        // Gets employee compliance statistics
        public async Task<ComplianceStats> GetEmployeeComplianceStatsAsync(int employeeId)
        {
            var tasks = await GetEmployeeTasksAsync(employeeId);
            var now = DateTime.UtcNow;

            var totalTasks = tasks.Count;
            var completedTasks = tasks.Count(t => t.Status == "Submitted" || t.Status == "Reviewed");
            var outstandingTasks = tasks.Count(t => t.Status == "Outstanding");
            var overdueTasks = tasks.Count(t => t.Status == "Outstanding" && t.DueDate < now);

            var complianceRate = totalTasks > 0
                ? (double)completedTasks / totalTasks * 100
                : 0;

            return new ComplianceStats
            {
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                OutstandingTasks = outstandingTasks,
                OverdueTasks = overdueTasks,
                ComplianceRate = complianceRate,
                CurrentStreak = CalculateComplianceStreak(tasks)
            };
        }

        #endregion

        #region Helper Methods

        private async Task<string> GenerateAccessLinkAsync(FormTask task)
        {
            if (string.IsNullOrEmpty(task.AccessToken))
            {
                task.AccessToken = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32));

                task.TokenExpiry = DateTime.UtcNow.AddDays(30);

                _db.DOITasks.Update(task);
                await _db.SaveChangesAsync();
            }

            return $"https://declarify.app/doi/task?token={HttpUtility.UrlEncode(task.AccessToken)}";
        }

        private int CalculateComplianceStreak(List<FormTask> tasks)
        {
            var orderedTasks = tasks
                .Where(t => t.Status == "Submitted" || t.Status == "Reviewed")
                .OrderByDescending(t => t.DueDate)
                .ToList();

            int streak = 0;
            foreach (var task in orderedTasks)
            {
                if (task.Status == "Submitted" || task.Status == "Reviewed")
                    streak++;
                else
                    break;
            }

            return streak;
        }
        // In ExecutiveService.cs or EmployeeDOIService.cs
        public async Task<ExecutiveDashboardViewModel> GetExecutiveDashboardAsync()
        {
            // Get current user's employee ID from session/claims
            var employeeId = GetCurrentEmployeeId(); // Implement this based on your auth

            var employee = await _db.Employees
           .Include(e => e.DOITasks)           // Optional: if you need tasks for the current user
           .Include(e => e.Manager)            // Optional: if you display manager info
           .Include(e => e.Domain)             // Optional: if you use OrganizationalDomain
           .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (employee == null)
            {
                throw new UnauthorizedAccessException("Employee not found");
            }

            // Get all tasks across the organization
            var allTasks = await _db.DOITasks
                .Include(t => t.Employee)
               
                .Include(t => t.Template)
                .ToListAsync();

            // Calculate overall statistics
            var totalEmployees = await _db.Employees.CountAsync();
            var compliantTasks = allTasks.Where(t => t.Status == "Submitted" || t.Status == "Reviewed").ToList();
            var outstandingTasks = allTasks.Where(t => t.Status == "Outstanding" && t.DueDate >= DateTime.UtcNow).ToList();
            var overdueTasks = allTasks.Where(t => t.Status == "Outstanding" && t.DueDate < DateTime.UtcNow).ToList();

            var overallStats = new OverallStatistics
            {
                TotalEmployees = totalEmployees,
                CompliantCount = compliantTasks.Count,
                OutstandingCount = outstandingTasks.Count,
                OverdueCount = overdueTasks.Count,
                ComplianceRate = totalEmployees > 0
                    ? Math.Round((decimal)compliantTasks.Count / totalEmployees * 100, 1)
                    : 0,
                TotalSubmissions = compliantTasks.Count,
                //AverageSubmissionDays = compliantTasks.Any()
                //    ? (decimal)compliantTasks.Average(t =>
                //        t.SubmittedDate.HasValue
                //            ? (t.SubmittedDate.Value - t.CreatedDate).TotalDays
                //            : 0)
                //    : 0
            };

            // Calculate department statistics
            var departmentStats = await _db.Employees
     .GroupBy(e => e.Department)  // Replace with your actual department property, e.g., e.Department or e.DepartmentId if it's a foreign key string
     .Select(g => new DepartmentStatistics
     {
         DepartmentName = g.Key,
         TotalEmployees = g.Count(),
         CompliantCount = g.SelectMany(e => e.DOITasks)
                           .Count(t => t.Status == "Submitted" || t.Status == "Reviewed"),
         OutstandingCount = g.SelectMany(e => e.DOITasks)
                             .Count(t => t.Status == "Outstanding" && t.DueDate >= DateTime.UtcNow),
         OverdueCount = g.SelectMany(e => e.DOITasks)
                         .Count(t => t.Status == "Outstanding" && t.DueDate < DateTime.UtcNow),
         ComplianceRate = g.Count() > 0
             ? Math.Round((decimal)g.SelectMany(e => e.DOITasks)
                                 .Count(t => t.Status == "Submitted" || t.Status == "Reviewed")
                          / g.Count() * 100, 1)
             : 0
     })
     .Where(d => d.TotalEmployees > 0)  // This filters out any empty groups (e.g., null department names)
     .ToListAsync();

            // Calculate region statistics (if regions are used)
            var regionStats = await _db.Employees
     .GroupBy(e => e.Region)  // Replace with your actual region property name, e.g., e.Region, e.RegionName, etc.
     .Select(g => new RegionStatistics
     {
         RegionName = g.Key,
         TotalEmployees = g.Count(),
         CompliantCount = g.SelectMany(e => e.DOITasks)
                           .Count(t => t.Status == "Submitted" || t.Status == "Reviewed"),
         OutstandingCount = g.SelectMany(e => e.DOITasks)
                             .Count(t => t.Status == "Outstanding" && t.DueDate >= DateTime.UtcNow),
         OverdueCount = g.SelectMany(e => e.DOITasks)
                         .Count(t => t.Status == "Outstanding" && t.DueDate < DateTime.UtcNow),
         ComplianceRate = g.Count() > 0
             ? Math.Round((decimal)g.SelectMany(e => e.DOITasks)
                                 .Count(t => t.Status == "Submitted" || t.Status == "Reviewed")
                          / g.Count() * 100, 1)
             : 0
     })
     .Where(r => r.TotalEmployees > 0)  // Excludes any groups with no employees (e.g., null regions)
     .ToListAsync();

            return new ExecutiveDashboardViewModel
            {
                Employee = employee,
                OverallStats = overallStats,
                DepartmentStats = departmentStats,
                RegionStats = regionStats
            };
        }

        // Helper method to get current employee ID
        private int GetCurrentEmployeeId()
        {
            // Implement based on your authentication system
            // Example using HttpContext (inject IHttpContextAccessor):
            var userEmail = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            var employee = _db.Employees.FirstOrDefault(e => e.Email_Address == userEmail);
            return employee?.EmployeeId ?? 0;
        }


















        #endregion
    }
}