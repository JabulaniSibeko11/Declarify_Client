using Declarify.Services.API;

namespace Declarify.Models.ViewModels
{
    public class DashboardViewModel
    {
        public string CompanyName { get; set; }
        public string AdminName { get; set; }
        public string? AdminEmail { get; set; }
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

        public Template? template { get; set; }

        public Employee? employee { get; set; }

        public BulkRequestViewModel BulkData { get; set; } = new BulkRequestViewModel();
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
       // public List<int> EmployeeIds { get; set; } = new List<int>(); // ← was int before
        public List<int> EmployeeIds { get; set; }
        public string EmployeeIdsJson { get; set; }
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
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = "";
        public string? Description { get; set; }
         //public TemplateConfig? Config { get; set; } = new();
       public string? Config { get; set; }
        public bool IsPublished { get; set; }
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
        public CreditCheckResponse CreditBalance { get; set; }
        public List<CreditRequestResponse> CreditRequests { get; set; } = new();
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
    public class DOITask
    {
        public int TaskId { get; set; }
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;
        public int TemplateId { get; set; }
        public DOIFormTemplate Template { get; set; } = null!;
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Outstanding"; // Outstanding, Submitted, Reviewed
        public string? AccessToken { get; set; }
        public DateTime? TokenExpiry { get; set; }

        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? SubmittedAt { get; set; }




    }

    public class DOIFormTemplate
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateConfig { get; set; } = string.Empty; // JSONB for sections/fields
    }

    public class DOIFormSubmission
    {
        public int SubmissionId { get; set; }
        public int TaskId { get; set; }
        public string FormData { get; set; } = string.Empty; // JSONB
        public DateTime? SubmittedAt { get; set; }
        public string? AttestationSignature { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Status { get; set; } = "Draft";
    }

    public class SubmissionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
