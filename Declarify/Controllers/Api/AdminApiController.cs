using Declarify.Models;
using Declarify.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Declarify.Controllers.Api
{
    /// <summary>
    /// Admin Dashboard REST API Controller
    /// Provides programmatic access to all admin dashboard functionality
    /// Used for: Single Page Applications, mobile apps, external integrations
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class AdminApiController : ControllerBase
    {
        private readonly IFormTaskService _formTaskService;
        private readonly ICreditService _creditService;
        private readonly ILicenseService _licenseService;
        private readonly IEmployeeService _employeeService;
        private readonly ITemplateService _templateService;
        private readonly ILogger<AdminApiController> _logger;

        public AdminApiController(
            IFormTaskService formTaskService,
            ICreditService creditService,
            ILicenseService licenseService,
            IEmployeeService employeeService,
            ITemplateService templateService,
            ILogger<AdminApiController> logger)
        {
            _formTaskService = formTaskService;
            _creditService = creditService;
            _licenseService = licenseService;
            _employeeService = employeeService;
            _templateService = templateService;
            _logger = logger;
        }

        // ============================================================================
        // DASHBOARD METRICS API
        // ============================================================================

        /// <summary>
        /// GET /api/admin/dashboard
        /// Get complete dashboard data including compliance metrics, credits, and license status
        /// </summary>
        /// <response code="200">Returns dashboard data</response>
        /// <response code="401">Unauthorized - Admin role required</response>
        /// <response code="403">Forbidden - License expired</response>
        /// 
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(DashboardApiResponse), 200)]
        public async Task<ActionResult<DashboardApiResponse>> GetDashboard()
        {
            try
            {
                // Check license validity
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse
                    {
                        Error = "License Expired",
                        Message = "Account requires renewal. Please contact your vendor.",
                        Code = "LICENSE_EXPIRED"
                    });
                }

                var dashboardData = await _formTaskService.GetComplianceDashboardDataAsync();
                var creditBalance = await _creditService.GetAvailableCreditsAsync();
                var licenseStatus = await _licenseService.GetLicenseStatusMessageAsync();
                var licenseExpiryDate = await _licenseService.GetExpiryDateAsync();

                var response = new DashboardApiResponse
                {
                    Metrics = new ComplianceMetrics
                    {
                        TotalEmployees = dashboardData.TotalEmployees,
                        TotalTasks = dashboardData.TotalTasks,
                        OutstandingCount = dashboardData.OutstandingCount,
                        OverdueCount = dashboardData.OverdueCount,
                        SubmittedCount = dashboardData.SubmittedCount,
                        ReviewedCount = dashboardData.ReviewedCount,
                        NonCompliantCount = dashboardData.NonCompliantCount,
                        CompliancePercentage = dashboardData.CompliancePercentage
                    },
                    DepartmentBreakdown = dashboardData.DepartmentBreakdown.Select(d => new DepartmentStats
                    {
                        Department = d.Key,
                        TotalTasks = d.Value.TotalTasks,
                        OutstandingCount = d.Value.OutstandingCount,
                        OverdueCount = d.Value.OverdueCount,
                        SubmittedCount = d.Value.SubmittedCount,
                        ReviewedCount = d.Value.ReviewedCount,
                        CompliancePercentage = d.Value.CompliancePercentage
                    }).ToList(),
                    Credits = new CreditInfo
                    {
                        Balance = creditBalance,
                        LowBalanceWarning = creditBalance < 50,
                        CriticalBalanceWarning = creditBalance < 20
                    },
                    License = new LicenseInfo
                    {
                        Status = licenseStatus,
                        ExpiryDate = licenseExpiryDate,
                        DaysUntilExpiry = (licenseExpiryDate - DateTime.UtcNow).Days,
                        IsValid = await _licenseService.IsLicenseValidAsync()
                    },
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard data");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Internal Server Error",
                    Message = "Failed to fetch dashboard data",
                    Code = "DASHBOARD_ERROR"
                });
            }
        }

        /// GET /api/admin/metrics
        /// Get quick compliance metrics (lightweight endpoint for polling)
        [HttpGet("metrics")]
        [ProducesResponseType(typeof(ComplianceMetrics), 200)]
        public async Task<ActionResult<ComplianceMetrics>> GetMetrics()
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var metrics = new ComplianceMetrics
                {
                    TotalEmployees = await _formTaskService.GetTotalEmployeesAsync(),
                    OutstandingCount = await _formTaskService.GetOutstandingCountAsync(),
                    OverdueCount = await _formTaskService.GetOverdueCountAsync(),
                    SubmittedCount = await _formTaskService.GetSubmittedCountAsync(),
                    ReviewedCount = await _formTaskService.GetReviewedCountAsync(),
                    CompliancePercentage = await _formTaskService.GetCompliancePercentageAsync()
                };

                metrics.TotalTasks = metrics.OutstandingCount + metrics.OverdueCount +
                                    metrics.SubmittedCount + metrics.ReviewedCount;
                metrics.NonCompliantCount = metrics.OutstandingCount + metrics.OverdueCount;

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metrics");
                return StatusCode(500, new ErrorResponse { Code = "METRICS_ERROR" });
            }
        }

        // ============================================================================
        // BULK REQUEST API
        // ============================================================================
        /// POST /api/admin/bulk-request
        /// Create bulk DOI request for multiple employees
        /// <param name="request">Bulk request parameters</param>
        /// <response code="201">Bulk request created successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="403">License expired</response>
        [HttpPost("bulk-request")]
        [ProducesResponseType(typeof(BulkRequestApiResponse), 201)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<ActionResult<BulkRequestApiResponse>> CreateBulkRequest([FromBody] BulkRequestApiRequest request)
        {
            try
            {
                // Validate license
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                // Validate request
                if (request.EmployeeIds == null || !request.EmployeeIds.Any())
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Invalid Request",
                        Message = "At least one employee must be selected",
                        Code = "INVALID_EMPLOYEES"
                    });
                }

                if (request.DueDate <= DateTime.UtcNow)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Invalid Request",
                        Message = "Due date must be in the future",
                        Code = "INVALID_DUE_DATE"
                    });
                }

                // Create tasks
                await _formTaskService.BulkCreateTasksAsync(
                    request.TemplateId,
                    request.DueDate,
                    request.EmployeeIds
                );

                var response = new BulkRequestApiResponse
                {
                    Success = true,
                    EmployeeCount = request.EmployeeIds.Count,
                    TemplateId = request.TemplateId,
                    DueDate = request.DueDate,
                    CreatedAt = DateTime.UtcNow,
                    Message = $"DOI requests sent to {request.EmployeeIds.Count} employees"
                };

                return CreatedAtAction(nameof(GetMetrics), response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bulk request");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Internal Server Error",
                    Message = ex.Message,
                    Code = "BULK_REQUEST_ERROR"
                });
            }
        }
        // ============================================================================
        // EMPLOYEE API
        // ============================================================================
        /// GET /api/admin/employees
        /// Get all employees
        /// [HttpGet("employees")]
        [ProducesResponseType(typeof(List<EmployeeApiResponse>), 200)]
        public async Task<ActionResult<List<EmployeeApiResponse>>> GetEmployees([FromQuery] string? department = null)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var employees = string.IsNullOrEmpty(department)
                    ? await _employeeService.GetAllEmployeesAsync()
                    : await _employeeService.GetEmployeesByDepartmentAsync(department);

                var response = employees.Select(e => new EmployeeApiResponse
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeNumber = e.EmployeeNumber,
                    FullName = e.Full_Name,
                    Email = e.Email_Address,
                    Position = e.Position,
                    Department = e.Department,
                    ManagerId = e.ManagerId
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employees");
                return StatusCode(500, new ErrorResponse { Code = "EMPLOYEES_ERROR" });
            }
        }
        // GET /api/admin/employees/{id}
        // Get employee by ID with compliance details
        [HttpGet("employees/{id}")]
        [ProducesResponseType(typeof(EmployeeDetailApiResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<EmployeeDetailApiResponse>> GetEmployee(int id)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var employee = await _employeeService.GetEmployeeByIdAsync(id);
                if (employee == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        Error = "Not Found",
                        Message = $"Employee with ID {id} not found",
                        Code = "EMPLOYEE_NOT_FOUND"
                    });
                }

                var tasks = await _formTaskService.GetTasksForEmployeeAsync(id);
                var subordinates = await _employeeService.GetSubordinatesAsync(id);

                var response = new EmployeeDetailApiResponse
                {
                    EmployeeId = employee.EmployeeId,
                    EmployeeNumber = employee.EmployeeNumber,
                    FullName = employee.Full_Name,
                    Email = employee.Email_Address,
                    Position = employee.Position,
                    Department = employee.Department,
                    ManagerId = employee.ManagerId,
                    TotalTasks = tasks.Count(),
                    CompletedTasks = tasks.Count(t => t.Status == "Submitted" || t.Status == "Reviewed"),
                    PendingTasks = tasks.Count(t => t.Status == "Outstanding" || t.Status == "Overdue"),
                    ComplianceRate = tasks.Any()
                        ? Math.Round((double)tasks.Count(t => t.Status == "Submitted" || t.Status == "Reviewed") / tasks.Count() * 100, 2)
                        : 100.0,
                    SubordinateCount = subordinates.Count
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching employee {id}");
                return StatusCode(500, new ErrorResponse { Code = "EMPLOYEE_ERROR" });
            }
        }
        // POST /api/admin/employees/import
        // Bulk import employees from JSON array
        [HttpPost("employees/import")]
        [ProducesResponseType(typeof(EmployeeImportApiResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<ActionResult<EmployeeImportApiResponse>> ImportEmployees([FromBody] List<EmployeeImportDto> employees)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                if (employees == null || !employees.Any())
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Invalid Request",
                        Message = "Employee list cannot be empty",
                        Code = "EMPTY_IMPORT"
                    });
                }

                var result = await _employeeService.BulkLoadEmployeesAsync(employees);

                var response = new EmployeeImportApiResponse
                {
                    TotalProcessed = result.TotalProcessed,
                    CreatedCount = result.CreatedCount,
                    UpdatedCount = result.UpdatedCount,
                    FailedCount = result.FailedCount,
                    Errors = result.Errors,
                    Success = result.FailedCount == 0
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing employees");
                return StatusCode(500, new ErrorResponse { Code = "IMPORT_ERROR" });
            }
        }

        // ============================================================================
        // TEMPLATE API
        // ============================================================================

        /// <summary>
        /// GET /api/admin/templates
        /// Get all templates
        /// </summary>
        [HttpGet("templates")]
        [ProducesResponseType(typeof(List<TemplateApiResponse>), 200)]
        public async Task<ActionResult<List<TemplateApiResponse>>> GetTemplates([FromQuery] string? status = null)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var templates = status?.ToLower() == "active"
                    ? await _templateService.GetActiveTemplatesAsync()
                    : await _templateService.GetAllAsync();

                var response = templates.Select(t => new TemplateApiResponse
                {
                    TemplateId = t.TemplateId,
                    TemplateName = t.TemplateName,
                    Description = t.Description,
                    Status = t.Status,
                    CreatedDate = t.CreatedDate
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching templates");
                return StatusCode(500, new ErrorResponse { Code = "TEMPLATES_ERROR" });
            }
        }

        /// <summary>
        /// GET /api/admin/templates/{id}
        /// Get template by ID with full configuration
        /// </summary>
        [HttpGet("templates/{id}")]
        [ProducesResponseType(typeof(TemplateDetailApiResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<TemplateDetailApiResponse>> GetTemplate(int id)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var template = await _templateService.GetByIdAsync(id);
                var definition = await _templateService.GetDefinitionAsync(id);

                var response = new TemplateDetailApiResponse
                {
                    TemplateId = template.TemplateId,
                    TemplateName = definition.TemplateName,
                    Description = definition.Description,
                    Status = template.Status,
                    CreatedDate = template.CreatedDate,
                    Config = definition.Config
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching template {id}");
                return NotFound(new ErrorResponse { Code = "TEMPLATE_NOT_FOUND" });
            }
        }

        // ============================================================================
        // TASK API
        // ============================================================================

        /// <summary>
        /// GET /api/admin/tasks
        /// Get tasks with optional filtering
        /// </summary>
        [HttpGet("tasks")]
        [ProducesResponseType(typeof(List<TaskApiResponse>), 200)]
        public async Task<ActionResult<List<TaskApiResponse>>> GetTasks(
            [FromQuery] string? status = null,
            [FromQuery] int? templateId = null,
            [FromQuery] int? employeeId = null)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                IEnumerable<FormTask> tasks;

                if (!string.IsNullOrEmpty(status))
                {
                    tasks = await _formTaskService.GetTasksByStatusAsync(status);
                }
                else if (templateId.HasValue)
                {
                    tasks = await _formTaskService.GetTasksByTemplateAsync(templateId.Value);
                }
                else if (employeeId.HasValue)
                {
                    tasks = await _formTaskService.GetTasksForEmployeeAsync(employeeId.Value);
                }
                else
                {
                    // Return recent tasks (last 3 months to next month)
                    var startDate = DateTime.UtcNow.AddMonths(-3);
                    var endDate = DateTime.UtcNow.AddMonths(1);
                    tasks = await _formTaskService.GetTasksDueInRangeAsync(startDate, endDate);
                }

                var response = tasks.Select(t => new TaskApiResponse
                {
                    TaskId = t.TaskId,
                    EmployeeId = t.EmployeeId,
                    EmployeeName = t.Employee?.Full_Name,
                    TemplateId = t.TemplateId,
                    TemplateName = t.Template?.TemplateName,
                    DueDate = t.DueDate,
                    Status = t.Status,
                    IsOverdue = t.Status == "Overdue" || (t.Status == "Outstanding" && t.DueDate < DateTime.UtcNow)
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks");
                return StatusCode(500, new ErrorResponse { Code = "TASKS_ERROR" });
            }
        }

        /// <summary>
        /// POST /api/admin/tasks/extend-due-date
        /// Extend due date for multiple tasks
        /// </summary>
        [HttpPost("tasks/extend-due-date")]
        [ProducesResponseType(typeof(TaskExtendApiResponse), 200)]
        [ProducesResponseType(typeof(ErrorResponse), 400)]
        public async Task<ActionResult<TaskExtendApiResponse>> ExtendDueDate([FromBody] ExtendDueDateApiRequest request)
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                if (request.TaskIds == null || !request.TaskIds.Any())
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Invalid Request",
                        Message = "At least one task must be selected",
                        Code = "INVALID_TASKS"
                    });
                }

                if (request.NewDueDate <= DateTime.UtcNow)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Invalid Request",
                        Message = "New due date must be in the future",
                        Code = "INVALID_DUE_DATE"
                    });
                }

                var count = await _formTaskService.BulkExtendDueDateAsync(request.TaskIds, request.NewDueDate);

                var response = new TaskExtendApiResponse
                {
                    Success = true,
                    TasksUpdated = count,
                    NewDueDate = request.NewDueDate,
                    Message = $"Extended due date for {count} tasks"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending due dates");
                return StatusCode(500, new ErrorResponse { Code = "EXTEND_ERROR" });
            }
        }

        // ============================================================================
        // CREDIT API
        // ============================================================================

        /// <summary>
        /// GET /api/admin/credits
        /// Get credit balance and batches
        /// </summary>
        [HttpGet("credits")]
        [ProducesResponseType(typeof(CreditApiResponse), 200)]
        public async Task<ActionResult<CreditApiResponse>> GetCredits()
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var balance = await _creditService.GetAvailableCreditsAsync();
                var batches = await _creditService.GetCreditBatchesAsync();
                var expiringCredits = await _creditService.GetExpiringCreditsAsync(30);

                var response = new CreditApiResponse
                {
                    Balance = balance,
                    LowBalanceWarning = balance < 50,
                    CriticalBalanceWarning = balance < 20,
                    Batches = batches.Select(b => new CreditBatchApiResponse
                    {
                        CreditId = b.CreditId,
                        BatchAmount = b.BatchAmount,
                        RemainingAmount = b.RemainingAmount,
                        ConsumedAmount = b.ConsumedAmount,
                        LoadDate = b.LoadDate,
                        ExpiryDate = b.ExpiryDate,
                        IsExpired = b.IsExpired,
                        DaysUntilExpiry = b.DaysUntilExpiry
                    }).ToList(),
                    ExpiringCreditsCount = expiringCredits.Sum(c => c.RemainingAmount)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching credits");
                return StatusCode(500, new ErrorResponse { Code = "CREDITS_ERROR" });
            }
        }

        /// <summary>
        /// POST /api/admin/credits/sync
        /// Sync credits and license with central hub
        /// </summary>
        [HttpPost("credits/sync")]
        [ProducesResponseType(typeof(SyncApiResponse), 200)]
        public async Task<ActionResult<SyncApiResponse>> SyncWithCentralHub()
        {
            try
            {
                await _licenseService.SyncWithCentralHubAsync();

                var response = new SyncApiResponse
                {
                    Success = true,
                    SyncedAt = DateTime.UtcNow,
                    Message = "Successfully synced with central hub"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing with central hub");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Sync Failed",
                    Message = ex.Message,
                    Code = "SYNC_ERROR"
                });
            }
        }

        // ============================================================================
        // REPORTS API
        // ============================================================================

        /// <summary>
        /// GET /api/admin/reports/compliance
        /// Get compliance report data
        /// </summary>
        [HttpGet("reports/compliance")]
        [ProducesResponseType(typeof(ComplianceReportApiResponse), 200)]
        public async Task<ActionResult<ComplianceReportApiResponse>> GetComplianceReport()
        {
            try
            {
                if (!await _licenseService.IsLicenseValidAsync())
                {
                    return StatusCode(403, new ErrorResponse { Code = "LICENSE_EXPIRED" });
                }

                var dashboardData = await _formTaskService.GetComplianceDashboardDataAsync();

                var response = new ComplianceReportApiResponse
                {
                    GeneratedAt = DateTime.UtcNow,
                    OverallCompliance = dashboardData.CompliancePercentage,
                    TotalEmployees = dashboardData.TotalEmployees,
                    TotalTasks = dashboardData.TotalTasks,
                    DepartmentData = dashboardData.DepartmentBreakdown.Select(d => new DepartmentStats
                    {
                        Department = d.Key,
                        TotalTasks = d.Value.TotalTasks,
                        OutstandingCount = d.Value.OutstandingCount,
                        OverdueCount = d.Value.OverdueCount,
                        SubmittedCount = d.Value.SubmittedCount,
                        ReviewedCount = d.Value.ReviewedCount,
                        CompliancePercentage = d.Value.CompliancePercentage
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance report");
                return StatusCode(500, new ErrorResponse { Code = "REPORT_ERROR" });
            }
        }
    
}

}
