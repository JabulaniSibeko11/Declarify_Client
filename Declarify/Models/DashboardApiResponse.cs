namespace Declarify.Models
{
    public class DashboardApiResponse
    {
        public ComplianceMetrics Metrics { get; set; } = new();
        public List<DepartmentStats> DepartmentBreakdown { get; set; } = new();
        public CreditInfo Credits { get; set; } = new();
        public LicenseInfo License { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
    public class ComplianceMetrics
    {
        public int TotalEmployees { get; set; }
        public int TotalTasks { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmittedCount { get; set; }
        public int ReviewedCount { get; set; }
        public int NonCompliantCount { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class DepartmentStats
    {
        public string Department { get; set; } = "";
        public int TotalTasks { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmittedCount { get; set; }
        public int ReviewedCount { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class CreditInfo
    {
        public int Balance { get; set; }
        public bool LowBalanceWarning { get; set; }
        public bool CriticalBalanceWarning { get; set; }
    }

    public class LicenseInfo
    {
        public string Status { get; set; } = "";
        public DateTime ExpiryDate { get; set; }
        public int DaysUntilExpiry { get; set; }
        public bool IsValid { get; set; }
    }

    public class BulkRequestApiRequest
    {
        public int TemplateId { get; set; }
        public DateTime DueDate { get; set; }
        public List<int> EmployeeIds { get; set; } = new();
    }

    public class BulkRequestApiResponse
    {
        public bool Success { get; set; }
        public int EmployeeCount { get; set; }
        public int TemplateId { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Message { get; set; } = "";
    }

    public class EmployeeApiResponse
    {
        public int EmployeeId { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public string? Department { get; set; }
        public int? ManagerId { get; set; }
    }

    public class EmployeeDetailApiResponse : EmployeeApiResponse
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int PendingTasks { get; set; }
        public double ComplianceRate { get; set; }
        public int SubordinateCount { get; set; }
    }

    public class EmployeeImportApiResponse
    {
        public int TotalProcessed { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool Success { get; set; }
    }

    public class TemplateApiResponse
    {
        public int TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class TemplateDetailApiResponse : TemplateApiResponse
    {
        public TemplateConfig Config { get; set; } = new();
    }

    public class TaskApiResponse
    {
        public int TaskId { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public int TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public DateTime DueDate { get; set; }
        public string? Status { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class ExtendDueDateApiRequest
    {
        public List<int> TaskIds { get; set; } = new();
        public DateTime NewDueDate { get; set; }
    }

    public class TaskExtendApiResponse
    {
        public bool Success { get; set; }
        public int TasksUpdated { get; set; }
        public DateTime NewDueDate { get; set; }
        public string Message { get; set; } = "";
    }

    public class CreditApiResponse
    {
        public int Balance { get; set; }
        public bool LowBalanceWarning { get; set; }
        public bool CriticalBalanceWarning { get; set; }
        public List<CreditBatchApiResponse> Batches { get; set; } = new();
        public int ExpiringCreditsCount { get; set; }
    }

    public class CreditBatchApiResponse
    {
        public int CreditId { get; set; }
        public int BatchAmount { get; set; }
        public int RemainingAmount { get; set; }
        public int ConsumedAmount { get; set; }
        public DateTime LoadDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsExpired { get; set; }
        public int DaysUntilExpiry { get; set; }
    }

    public class SyncApiResponse
    {
        public bool Success { get; set; }
        public DateTime SyncedAt { get; set; }
        public string Message { get; set; } = "";
    }

    public class ComplianceReportApiResponse
    {
        public DateTime GeneratedAt { get; set; }
        public double OverallCompliance { get; set; }
        public int TotalEmployees { get; set; }
        public int TotalTasks { get; set; }
        public List<DepartmentStats> DepartmentData { get; set; } = new();
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = "";
        public string Message { get; set; } = "";
        public string Code { get; set; } = "";
    }
}


