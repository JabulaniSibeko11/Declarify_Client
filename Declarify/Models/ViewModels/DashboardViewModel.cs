namespace Declarify.Models.ViewModels
{
    public class DashboardViewModel
    {
        // Compliance Metrics
        public int TotalEmployees { get; set; }
        public int TotalTasks { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmittedCount { get; set; }
        public int ReviewedCount { get; set; }
        public int NonCompliantCount { get; set; }
        public double CompliancePercentage { get; set; }

        // Department Breakdown
        public Dictionary<string, DepartmentComplianceStats> DepartmentBreakdown { get; set; } = new();
        // Credit Information
        public int CreditBalance { get; set; }
        public List<CreditBatchInfo> CreditBatches { get; set; } = new();
        public List<Credit> ExpiringCredits { get; set; } = new();
        public bool LowCreditWarning { get; set; }
        public bool CriticalCreditWarning { get; set; }

        // License Information
        public string LicenseStatus { get; set; } = "";
        public DateTime LicenseExpiryDate { get; set; }
        public int DaysUntilLicenseExpiry { get; set; }

        // Goal Tracking
        public double GoalComplianceRate { get; set; } = 95.0;
        public bool IsGoalAchieved { get; set; }
    }

    public class BulkRequestViewModel
    {
        public List<Template> Templates { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public Dictionary<string, int> Departments { get; set; } = new();
        public DateTime SuggestedDueDate { get; set; }
    }
    public class BulkRequestFormModel
    {
        public int TemplateId { get; set; }
        public DateTime DueDate { get; set; }
        public List<int> EmployeeIds { get; set; } = new();
    }

    public class EmployeeManagementViewModel
    {
        public List<Employee> Employees { get; set; } = new();
        public int TotalEmployees { get; set; }
        public Dictionary<string, int> DepartmentCounts { get; set; } = new();
    }

    public class EmployeeImportViewModel
    {
        public string Instructions { get; set; } = "Upload a CSV file with columns: EmployeeNumber, Full_Name, Email_Address, Position, Department, ManagerId";
    }
    public class EmployeeDetailsViewModel
    {
        public Employee Employee { get; set; } = new();
        public List<FormTask> Tasks { get; set; } = new();
        public List<Employee> Subordinates { get; set; } = new();
        public double ComplianceRate { get; set; }
    }

    public class TemplateManagementViewModel
    {
        public List<Template> Templates { get; set; } = new();
    }

    public class TemplateCreateViewModel
    {
        public string TemplateName { get; set; } = "";
        public string? Description { get; set; }
        public TemplateConfig Config { get; set; } = new();
    }

    public class TemplateEditViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = "";
        public string? Description { get; set; }
        public TemplateConfig Config { get; set; } = new();
        public string? Status { get; set; }
    }

    public class TaskManagementViewModel
    {
        public List<FormTask> Tasks { get; set; } = new();
        public List<Template> Templates { get; set; } = new();
        public string? SelectedStatus { get; set; }
        public int? SelectedTemplateId { get; set; }
    }

    public class CreditManagementViewModel
    {
        public int CurrentBalance { get; set; }
        public List<CreditBatchInfo> CreditBatches { get; set; } = new();
        public List<Credit> ExpiringCredits { get; set; } = new();
        public bool LowBalanceWarning { get; set; }
        public bool CriticalBalanceWarning { get; set; }
    }

    public class ReportsViewModel
    {
        public ComplianceDashboardData ComplianceData { get; set; } = new();
        public DateTime ReportGeneratedAt { get; set; }
    }

    public class LicenseExpiredViewModel
    {
        public DateTime ExpiryDate { get; set; }
        public string Message { get; set; } = "";
    }
}
