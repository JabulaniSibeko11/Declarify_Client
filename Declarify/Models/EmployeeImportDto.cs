using System.ComponentModel.DataAnnotations.Schema;

namespace Declarify.Models
{
    public class EmployeeImportDto
    {
        public string? EmployeeNumber { get; set; }
        public int? ManagerId { get; set; }
        public string? Full_Name { get; set; }
        public string? Email_Address { get; set; }
        public string? Position { get; set; }
        public string? Department { get; set; }
    }

    public class EmployeeBulkLoadResult
    {
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class CreditBatchInfo
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

    public class LicenseSyncResponse
    {
        public string? LicenseKey { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public List<CreditBatchSync>? NewCreditBatches { get; set; }
    }

    public class CreditBatchSync
    {
        public int ExternalCreditId { get; set; }
        public int Amount { get; set; }
        public DateTime LoadDate { get; set; }
        public DateTime ExpiryDate { get; set; }
    }

    public class TemplateDefinition
    {
        public string TemplateName { get; set; } = "";
        public string? Description { get; set; }
        public TemplateConfig Config { get; set; } = new();
    }
    public class TemplateConfig
    {
        public List<TemplateSection> Sections { get; set; } = new();
    }
    public class TemplateSection
    {
        public string SectionId { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public int SectionOrder { get; set; }
        public string? Disclaimer { get; set; }
        public List<TemplateField> Fields { get; set; } = new();
    }
    public class TemplateField
    {
        public string FieldId { get; set; } = "";
        public string FieldLabel { get; set; } = "";
        public string FieldType { get; set; } = "text"; // text, textarea, boolean, checkbox, dropdown, date
        public bool Required { get; set; }
        public int Order { get; set; }
        public string? ConditionalOn { get; set; } // Field ID that controls visibility
        public List<string>? Options { get; set; } // For dropdown fields
    }
    public class DepartmentComplianceStats
    {
        public string Department { get; set; } = "";
        public int TotalTasks { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmittedCount { get; set; }
        public int ReviewedCount { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class ComplianceDashboardData
    {
        public int TotalEmployees { get; set; }
        public int TotalTasks { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmittedCount { get; set; }
        public int ReviewedCount { get; set; }
        public int NonCompliantCount { get; set; }
        public double CompliancePercentage { get; set; }
        public Dictionary<string, DepartmentComplianceStats> DepartmentBreakdown { get; set; } = new();
    }

    public class ReviewerComplianceSummary
    {
        public int TotalSubordinates { get; set; }
        public int TotalTasks { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public int SubmittedCount { get; set; }
        public int ReviewedCount { get; set; }
        public double CompliancePercentage { get; set; }
    }
    public class VerificationAttachment {

        public int VerificationId { get; set; }
        public int SubmissionId { get; set; }
        public string? Type { get; set; }  // "CIPC", "Credit"
        public string? ResultJson { get; set; }
     public DateTime   CreatedAt { get; set; }

        public DateTime VerifiedDate { get; set; }

        // Optional: Who initiated the verification (Admin or Reviewer)
        public int? InitiatedByEmployeeId { get; set; }

        // Navigation properties
        [ForeignKey(nameof(SubmissionId))]
        public virtual FormSubmission? Submission { get; set; }

        [ForeignKey(nameof(InitiatedByEmployeeId))]
        public virtual Employee? InitiatedBy { get; set; }
    }
}
