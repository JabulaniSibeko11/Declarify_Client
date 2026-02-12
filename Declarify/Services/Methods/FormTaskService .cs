using Declarify.Data;
using Declarify.Models;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class FormTaskService : IFormTaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<FormTaskService> _logger;
        private readonly IConfiguration _configuration;

        public FormTaskService(
           ApplicationDbContext context,
           IEmailService emailService,
           ILogger<FormTaskService> logger,
           IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }
        // Bulk create DOI tasks for multiple employees (FR 4.3.1)
        // CRITICAL: This is the primary workflow for Admin to initiate DOI collection
        // Bulk create DOI tasks for multiple employees (FR 4.3.1, 4.3.2, 4.3.3)
        // CRITICAL: This is the primary workflow for Admin to initiate DOI collection
        public async Task<List<FormTask>> BulkCreateTasksAsync(
       int templateId,
       DateTime dueDate,
       List<int> employeeIds)
        {
            // Validate template exists and is active
            var template = await _context.Templates.FindAsync(templateId);
            if (template == null || template.Status != "Active")
            {
                throw new InvalidOperationException($"Template {templateId} is not available for use");
            }

            // Validate employees exist
            var employees = await _context.Employees
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToListAsync();

            if (employees.Count != employeeIds.Count)
            {
                _logger.LogWarning(
                    $"Some employee IDs not found. Requested: {employeeIds.Count}, Found: {employees.Count}");
            }

            var createdTasks = new List<FormTask>();
            var baseUrl = _configuration["Application:BaseUrl"] ?? "https://declarify.local";

            foreach (var employee in employees)
            {
                // Check if employee already has an outstanding task for this template
                var existingTask = await _context.DOITasks
                    .FirstOrDefaultAsync(t =>
                        t.EmployeeId == employee.EmployeeId &&
                        t.TemplateId == templateId &&
                        (t.Status == "Outstanding" || t.Status == "Overdue"));

                if (existingTask != null)
                {
                    _logger.LogInformation(
                        $"Employee {employee.EmployeeId} already has outstanding task {existingTask.TaskId}, skipping");
                    continue;
                }

                // FR 4.3.2: Generate unique, non-guessable, time-bound access token
                var accessToken = GenerateSecureToken();
                var tokenExpiry = dueDate.AddDays(1); // Token valid until 1 day after due date

                var task = new FormTask
                {
                    EmployeeId = employee.EmployeeId,
                    TemplateId = templateId,
                    DueDate = dueDate,
                    Status = "Outstanding",
                    AccessToken = accessToken,
                    TokenExpiry = tokenExpiry,
                    // CreatedAt = DateTime.UtcNow
                };

                _context.DOITasks.Add(task);
                createdTasks.Add(task);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                $"Bulk created {createdTasks.Count} DOI tasks for template {templateId} with due date {dueDate:yyyy-MM-dd}");

            // FR 4.3.3: Send unique email links to each employee
            await SendTaskNotificationsAsync(createdTasks, template);

            // ✅ REQUIRED: return the created tasks
            return createdTasks;
        }


        // FR 4.3.3: Send unique email with access link to employees
        private async Task SendTaskNotificationsAsync(List<FormTask> tasks, Template template)
        {
            var baseUrl = _configuration["Application:BaseUrl"] ?? "https://declarify.local";
            int successCount = 0;
            int failureCount = 0;

            foreach (var task in tasks)
            {
                try
                {
                    // Load employee details if not already loaded
                    if (task.Employee == null)
                    {
                        task.Employee = await _context.Employees.FindAsync(task.EmployeeId);
                    }

                    if (task.Employee == null || string.IsNullOrEmpty(task.Employee.Email_Address))
                    {
                        _logger.LogWarning($"Cannot send email for task {task.TaskId} - employee not found or no email");
                        failureCount++;
                        continue;
                    }

                    // FR 4.3.2: Generate unique access link using the stored token
                    var accessLink = $"{baseUrl}/employee/task?token={task.AccessToken}";

                    // FR 4.3.3: Send email with unique access link
                    await SendDOIRequestEmail(task.Employee, template, task.DueDate, accessLink, task.TokenExpiry);

                    successCount++;
                    _logger.LogInformation($"Sent DOI request to {task.Employee.Email_Address} for task {task.TaskId}");
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, $"Failed to send email for task {task.TaskId}");
                }
            }

            _logger.LogInformation($"Email notification complete. Success: {successCount}, Failed: {failureCount}");
        }
        // FR 4.3.3: Send DOI request email to employee with unique access link
        private async Task SendDOIRequestEmail(
            Employee employee,
            Template template,
            DateTime dueDate,
            string accessLink,
            DateTime tokenExpiry)
        {
            var subject = $"Action Required: Complete your {template.TemplateName}";

            var body = $@"
<html>
<body style='font-family: Arial, sans-serif; color: #333;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px; background: #F8FAFC;'>
        <!-- Header with branding -->
        <div style='background: linear-gradient(135deg, #081B38 0%, #0D2B5E 100%); padding: 2rem; border-radius: 12px 12px 0 0; text-align: center;'>
            <h1 style='color: #00C2CB; margin: 0; font-size: 2rem; font-weight: 700;'>
                Declar<span style='color: #00E5FF;'>ify</span>
            </h1>
            <p style='color: rgba(255,255,255,0.8); margin: 0.5rem 0 0 0; font-size: 0.875rem;'>
                Compliance & Disclosure Hub
            </p>
        </div>

        <!-- Content -->
        <div style='background: white; padding: 2rem; border-radius: 0 0 12px 12px; box-shadow: 0 4px 20px rgba(8, 27, 56, 0.08);'>
            <h2 style='color: #081B38; margin-top: 0; font-size: 1.5rem;'>Declaration of Interest Required</h2>
            
            <p style='color: #1E293B; line-height: 1.6;'>Dear <strong>{employee.Full_Name}</strong>,</p>
            
            <p style='color: #1E293B; line-height: 1.6;'>
                You are required to complete your <strong>{template.TemplateName}</strong> 
                by <strong style='color: #081B38;'>{dueDate:MMMM d, yyyy} at {dueDate:h:mm tt}</strong>.
            </p>
            
            <p style='color: #1E293B; line-height: 1.6;'>
                Please click the button below to access your personalized declaration form:
            </p>
            
            <!-- CTA Button -->
            <div style='text-align: center; margin: 2rem 0;'>
                <a href='{accessLink}' 
                   style='background-color: #00C2CB; color: #081B38; padding: 14px 32px; 
                          text-decoration: none; border-radius: 50px; display: inline-block;
                          font-weight: 600; font-size: 0.875rem; box-shadow: 0 4px 12px rgba(0, 194, 203, 0.3);'>
                    Complete Declaration Now
                </a>
            </div>
            
            <!-- Important Info Box -->
            <div style='background: #FEF3C7; border-left: 4px solid #F59E0B; padding: 1rem; border-radius: 8px; margin: 1.5rem 0;'>
                <p style='margin: 0; color: #1E293B; font-weight: 600; font-size: 0.875rem;'>
                    ⚠️ Important Information:
                </p>
                <ul style='margin: 0.5rem 0 0 0; padding-left: 1.25rem; color: #1E293B; font-size: 0.875rem;'>
                    <li>This link is unique to you and should not be shared</li>
                    <li>You can save your progress and return later using the same link</li>
                    <li>The link expires on <strong>{tokenExpiry:MMMM d, yyyy}</strong></li>
                    <li>Automated reminders will be sent if the declaration is not completed</li>
                </ul>
            </div>

            <!-- Direct Link (fallback) -->
            <div style='background: #F8FAFC; padding: 1rem; border-radius: 8px; margin-top: 1.5rem;'>
                <p style='margin: 0 0 0.5rem 0; color: #64748B; font-size: 0.75rem; font-weight: 600; text-transform: uppercase;'>
                    Direct Link (if button doesn't work):
                </p>
                <p style='margin: 0; word-break: break-all;'>
                    <a href='{accessLink}' style='color: #00C2CB; font-size: 0.75rem;'>{accessLink}</a>
                </p>
            </div>
        </div>
        
        <!-- Footer -->
        <div style='text-align: center; padding: 1.5rem 0; color: #64748B; font-size: 0.75rem;'>
            <p style='margin: 0 0 0.5rem 0;'>
                If you have any questions or need assistance, please contact your HR or Compliance department.
            </p>
            <p style='margin: 0; color: #94A3B8;'>
                This is an automated message from the Compliance & Disclosure Hub (Declarify).
                <br>Do not reply to this email.
            </p>
        </div>
    </div>
</body>
</html>
";

            await _emailService.SendMagicLinkAsync(employee.Email_Address, subject, body);
        }

        // DEPRECATED: Legacy method - use BulkCreateTasksAsync instead
     
        // DEPRECATED: Legacy method
        [Obsolete("Use BulkCreateTasksAsync which includes token generation")]
        public async Task GenerateAndSendMagicLinksAsync(int bulkRequestId)
        {
            _logger.LogWarning("GenerateAndSendMagicLinksAsync(int) is deprecated.");
        }


        // Generate unique magic links and send emails to employees (FR 4.3.2, FR 4.3.3)
        // CRITICAL: Links must be non-guessable and time-bound
        public async Task GenerateAndSendMagicLinksAsync(List<FormTask> tasks)
        {
            var baseUrl = _configuration["Application:BaseUrl"] ?? "https://declarify.local";
            int successCount = 0;
            int failureCount = 0;

            foreach (var task in tasks)
            {
                try
                {
                    // Load employee details
                    var employee = await _context.Employees.FindAsync(task.EmployeeId);
                    if (employee == null || string.IsNullOrEmpty(employee.Email_Address))
                    {
                        _logger.LogWarning($"Cannot send email for task {task.TaskId} - employee not found or no email");
                        failureCount++;
                        continue;
                    }

                    // Generate unique, non-guessable token (FR 4.3.2)
                    var uniqueToken = GenerateSecureToken();
                    var magicLink = $"{baseUrl}/doi/complete/{task.TaskId}?token={uniqueToken}";

                    // Store token hash in database for verification
                    // Note: In production, store hashed token and implement token expiry
                    // For now, we'll use the TaskId + token combination

                    // Send email with magic link (FR 4.3.3)
                    await _emailService.SendMagicLinkAsync(
                        employee.Email_Address,
                        magicLink,
                        employee.Full_Name ?? "Employee"
                    );

                    successCount++;
                    _logger.LogInformation($"Sent magic link to {employee.Email_Address} for task {task.TaskId}");
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, $"Failed to send magic link for task {task.TaskId}");
                }
            }

            _logger.LogInformation($"Magic link generation complete. Success: {successCount}, Failed: {failureCount}");
        }
        // Overload for bulk request ID (legacy support)
        //public async Task GenerateAndSendMagicLinksAsync(int bulkRequestId)
        //{
        //    // In a more complex system, you might have a BulkRequest entity
        //    // For now, we'll retrieve tasks by template and date range
        //    var tasks = await _context.DOITasks
        //        .Where(t => t.Status == "Outstanding")
        //        .OrderByDescending(t => t.TaskId)
        //        .Take(100) // Reasonable batch size
        //        .ToListAsync();

        //    await GenerateAndSendMagicLinksAsync(tasks);
        //}
        // Send automated reminders to non-compliant employees (FR 4.3.4)
        // CRITICAL: Should be called by scheduled job
        // - 7 days before due date
        // - On due date
        public async Task SendRemindersAsync()
        {
            var now = DateTime.UtcNow.Date;
            var reminderDate7Days = now.AddDays(7);

            // Get tasks that need reminders
            var tasksNeedingReminders = await _context.DOITasks
                .Include(t => t.Employee)
                .Where(t =>
                    (t.Status == "Outstanding" || t.Status == "Overdue") &&
                    (t.DueDate.Date == now || t.DueDate.Date == reminderDate7Days))
                .ToListAsync();

            int remindersSent = 0;

            foreach (var task in tasksNeedingReminders)
            {
                if (task.Employee == null || string.IsNullOrEmpty(task.Employee.Email_Address))
                    continue;

                try
                {
                    await _emailService.SendReminderAsync(
                        task.Employee.Email_Address,
                        task.Employee.Full_Name ?? "Employee",
                        task.DueDate
                    );

                    remindersSent++;
                    _logger.LogInformation($"Sent reminder to {task.Employee.Email_Address} for task {task.TaskId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send reminder for task {task.TaskId}");
                }
            }

            _logger.LogInformation($"Reminder batch complete. Sent {remindersSent} reminders");
        }
        // Update task statuses based on due dates (called by scheduled job)
        public async Task UpdateOverdueTasksAsync()
        {
            var now = DateTime.UtcNow;

            var overdueTasks = await _context.DOITasks
                .Where(t => t.Status == "Outstanding" && t.DueDate < now)
                .ToListAsync();

            if (!overdueTasks.Any())
                return;

            foreach (var task in overdueTasks)
            {
                task.Status = "Overdue";
            }

            if (overdueTasks.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Marked {overdueTasks.Count} tasks as overdue");
            }
        }
        // ============================================================================
        // DASHBOARD AND COMPLIANCE METRICS (FR 4.5.1)
        // ============================================================================
        // Get total number of employees with issued DOI tasks
        public async Task<int> GetTotalEmployeesAsync()
        {
            return await _context.Employees.CountAsync();
        }
        // Get count of outstanding tasks (not yet submitted)
        public async Task<int> GetOutstandingCountAsync()
        {
            return await _context.DOITasks
                .Where(t => t.Status == "Outstanding")
                .CountAsync();
        }
        // Get count of overdue tasks (past due date and not submitted)
        public async Task<int> GetOverdueCountAsync()
        {
            return await _context.DOITasks
                .Where(t => t.Status == "Overdue")
                .CountAsync();
        }
        // Get count of submitted tasks (awaiting review)
        public async Task<int> GetSubmittedCountAsync()
        {
            return await _context.DOITasks
                .Where(t => t.Status == "Submitted")
                .CountAsync();
        }
        //Get count of reviewed tasks (completed workflow)
        public async Task<int> GetReviewedCountAsync()
        {
            return await _context.DOITasks
                .Where(t => t.Status == "Reviewed")
                .CountAsync();
        }
        // Get non-compliant count (Outstanding + Overdue)
        // Used for goal measurement (G1: <5% after deadline)
        public async Task<int> GetNonCompliantCountAsync()
        {
            return await _context.DOITasks
                .Where(t => t.Status == "Outstanding" || t.Status == "Overdue")
                .CountAsync();
        }

        // Calculate overall compliance percentage (FR 4.5.1)
        // Compliance = (Submitted + Reviewed) / Total Tasks * 100
        public async Task<double> GetCompliancePercentageAsync()
        {
            var totalTasks = await _context.DOITasks.CountAsync();
            if (totalTasks == 0) return 100.0;

            var compliantTasks = await _context.DOITasks
                .Where(t => t.Status == "Submitted" || t.Status == "Reviewed")
                .CountAsync();

            return Math.Round((double)compliantTasks / totalTasks * 100, 2);
        }
        // Get department-level compliance breakdown (FR 4.5.3)
        public async Task<Dictionary<string, DepartmentComplianceStats>> GetDepartmentBreakdownAsync()
        {
            var departmentStats = await _context.DOITasks
                .Include(t => t.Employee)
                .GroupBy(t => t.Employee!.Department ?? "Unassigned")
                .Select(g => new DepartmentComplianceStats
                {
                    Department = g.Key,
                    TotalTasks = g.Count(),
                    OutstandingCount = g.Count(t => t.Status == "Outstanding"),
                    OverdueCount = g.Count(t => t.Status == "Overdue"),
                    SubmittedCount = g.Count(t => t.Status == "Submitted"),
                    ReviewedCount = g.Count(t => t.Status == "Reviewed"),
                    CompliancePercentage = g.Count() > 0
                        ? Math.Round((double)g.Count(t => t.Status == "Submitted" || t.Status == "Reviewed") / g.Count() * 100, 2)
                        : 100.0
                })
                .ToDictionaryAsync(x => x.Department);

            return departmentStats;
        }
        // Get detailed compliance dashboard data
        public async Task<ComplianceDashboardData> GetComplianceDashboardDataAsync()
        {
            return new ComplianceDashboardData
            {
                TotalEmployees = await GetTotalEmployeesAsync(),

                TotalTasks = await _context.DOITasks.CountAsync(),
                OutstandingCount = await GetOutstandingCountAsync(),
                OverdueCount = await GetOverdueCountAsync(),
                SubmittedCount = await GetSubmittedCountAsync(),
                ReviewedCount = await GetReviewedCountAsync(),
                NonCompliantCount = await GetNonCompliantCountAsync(),
                CompliancePercentage = await GetCompliancePercentageAsync(),

                AwaitingReviewCount = await GetAwaitingReviewCountAsync(),
                AmendmentRequiredCount = await GetAmendmentRequiredCountAsync(),
                RevisionRequiredCount = await GetRevisionRequiredCountAsync(),
                PendingVerificationCount = await GetPendingVerificationCountAsync(),

                DepartmentBreakdown = await GetDepartmentBreakdownAsync()
            };
        }
        // Get all tasks for a specific employee
        public async Task<IEnumerable<FormTask>> GetTasksForEmployeeAsync(int employeeId)
        {
            return await _context.DOITasks
                .Include(t => t.Template)
                .Include(t => t.FormSubmission)
                .Where(t => t.EmployeeId == employeeId)
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();
        }

        // Get tasks for reviewer - only their subordinates (FR 4.5.2)
        public async Task<IEnumerable<FormTask>> GetTasksForReviewerAsync(int reviewerId)
        {
            // Get all subordinates of this reviewer
            var subordinateIds = await _context.Employees
                .Where(e => e.ManagerId == reviewerId)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            return await _context.DOITasks
                .Include(t => t.Template)
                .Include(t => t.Employee)
                .Include(t => t.FormSubmission)
                .Where(t => subordinateIds.Contains(t.EmployeeId))
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();
        }
        // Get reviewer compliance summary for their subordinates
        public async Task<ReviewerComplianceSummary> GetReviewerComplianceSummaryAsync(int reviewerId)
        {
            var subordinateIds = await _context.Employees
                .Where(e => e.ManagerId == reviewerId)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            var tasks = await _context.DOITasks
                .Where(t => subordinateIds.Contains(t.EmployeeId))
                .ToListAsync();

            return new ReviewerComplianceSummary
            {
                TotalSubordinates = subordinateIds.Count,
                TotalTasks = tasks.Count,
                OutstandingCount = tasks.Count(t => t.Status == "Outstanding"),
                OverdueCount = tasks.Count(t => t.Status == "Overdue"),
                SubmittedCount = tasks.Count(t => t.Status == "Submitted"),
                ReviewedCount = tasks.Count(t => t.Status == "Reviewed"),
                CompliancePercentage = tasks.Count > 0
                    ? Math.Round((double)tasks.Count(t => t.Status == "Submitted" || t.Status == "Reviewed") / tasks.Count * 100, 2)
                    : 100.0
            };
        }
        // Get task by ID with full details
        public async Task<FormTask?> GetTaskByIdAsync(int taskId)
        {
            return await _context.DOITasks
                .Include(t => t.Template)
                .Include(t => t.Employee)
                .Include(t => t.FormSubmission)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
        }

        // Get tasks by status for admin filtering
        public async Task<List<FormTask>> GetTasksByStatusAsync(string status)
        {
            return await _context.DOITasks
                .Include(t => t.Template)
                .Include(t => t.Employee)
                .Where(t => t.Status == status)
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();
        }
        // Get tasks by template for reporting
        public async Task<List<FormTask>> GetTasksByTemplateAsync(int templateId)
        {
            return await _context.DOITasks
                .Include(t => t.Employee)
                .Include(t => t.FormSubmission)
                .Where(t => t.TemplateId == templateId)
                .OrderByDescending(t => t.DueDate)
                .ToListAsync();
        }

        // Get tasks due within a date range
        public async Task<List<FormTask>> GetTasksDueInRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.DOITasks
                .Include(t => t.Template)
                .Include(t => t.Employee)
                .Where(t => t.DueDate >= startDate && t.DueDate <= endDate)
                .OrderBy(t => t.DueDate)
                .ToListAsync();
        }
        // Cancel/delete task (admin function)
        public async Task<bool> CancelTaskAsync(int taskId)
        {
            var task = await _context.DOITasks
                .Include(t => t.FormSubmission)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);

            if (task == null) return false;

            // Don't allow deletion of submitted or reviewed tasks
            if (task.Status == "Submitted" || task.Status == "Reviewed")
            {
                _logger.LogWarning($"Cannot cancel task {taskId} - already submitted/reviewed");
                return false;
            }

            _context.DOITasks.Remove(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Cancelled task {taskId}");
            return true;
        }

        // Extend due date for task or multiple tasks
        public async Task<bool> ExtendDueDateAsync(int taskId, DateTime newDueDate)
        {
            var task = await _context.DOITasks.FindAsync(taskId);
            if (task == null) return false;

            task.DueDate = newDueDate;

            // Update status if previously overdue
            if (task.Status == "Overdue" && newDueDate > DateTime.UtcNow)
            {
                task.Status = "Outstanding";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Extended due date for task {taskId} to {newDueDate:yyyy-MM-dd}");

            return true;
        }

        // Bulk extend due dates
        public async Task<int> BulkExtendDueDateAsync(List<int> taskIds, DateTime newDueDate)
        {
            var tasks = await _context.DOITasks
                .Where(t => taskIds.Contains(t.TaskId))
                .ToListAsync();

            foreach (var task in tasks)
            {
                task.DueDate = newDueDate;
                if (task.Status == "Overdue" && newDueDate > DateTime.UtcNow)
                {
                    task.Status = "Outstanding";
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Bulk extended due date for {tasks.Count} tasks to {newDueDate:yyyy-MM-dd}");

            return tasks.Count;
        }
        // Generate cryptographically secure random token
        private string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
        private Task<int> GetAwaitingReviewCountAsync()
        {
            // "Submitted" or "Pending" but NOT reviewed/approved yet
            return _context.DOIFormSubmissions.CountAsync(s =>
                (s.Status == "Submitted" || s.Status == "Pending") &&
                s.ReviewedDate == null);
        }

        private Task<int> GetAmendmentRequiredCountAsync()
        {
            // You already set: task.Status = "AmendmentRequired" and task.IsAmendmentRequired = true
            return _context.DOITasks.CountAsync(t =>
                t.Status == "AmendmentRequired" || t.IsAmendmentRequired == true);
        }

        private Task<int> GetRevisionRequiredCountAsync()
        {
            // Your controller sets: submission.Status = "Revision Required"
            return _context.DOIFormSubmissions.CountAsync(s => s.Status == "Revision Required");
        }

        private Task<int> GetPendingVerificationCountAsync()
        {
            // Your controller sets: submission.Status = "Pending Verification"
            return _context.DOIFormSubmissions.CountAsync(s => s.Status == "Pending Verification");
        }


    }

}
