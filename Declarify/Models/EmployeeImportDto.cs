using System.ComponentModel.DataAnnotations.Schema;

namespace Declarify.Models
{
    public class EmployeeImportDto
    {
        public string? EmployeeNumber { get; set; }
        public string? ManagerEmployeeNumber { get; set; }
        public string? Full_Name { get; set; }
        public string? Email_Address { get; set; }
        public string? Position { get; set; }
        public string? Department { get; set; }
        public string? Region { get; set; }
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

        public int TemplateId { get; set; }


        public TemplateStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public enum TemplateStatus
    {
        Draft = 0,
        Active = 1,
        Archived = 2
    }

    public class TemplateConfig
    {
        public List<TemplateSection> Sections { get; set; } = new();
        public int DefaultDueDays { get; set; }
        public ReminderConfig? Reminders { get; set; }
        public bool EmployeeDetailsIncluded { get; set; }
        public DateTime? PublishedDate { get; set; }
        public int? TotalSections { get; set; }
        public int? TotalFields { get; set; }
    }

    public class ReminderConfig
    {
        public bool SevenDays { get; set; }
        public bool DueDate { get; set; }
    }

    public class TemplateSection
    {
        public string SectionId { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public int SectionOrder { get; set; }
        public string? Disclaimer { get; set; }
        public List<TemplateField> Fields { get; set; } = new();
    }

    // ✅ UPDATED: Added all missing properties for table support
    public class TemplateField
    {
        public string FieldId { get; set; } = "";
        public string FieldLabel { get; set; } = "";
        public string FieldType { get; set; } = "text"; // text, textarea, boolean, checkbox, dropdown, date, table, advancedTable
        public bool Required { get; set; }
        public int Order { get; set; }
        public string? Placeholder { get; set; }
        public string? HelpText { get; set; }
        public string? ConditionalOn { get; set; } // Field ID that controls visibility
        public List<string>? Options { get; set; } // For dropdown fields
        public Dictionary<string, object>? Validation { get; set; }

        // ✅ Simple Table Support
        public TableConfigDefinition? TableConfig { get; set; }

        // ✅ Advanced Table Support
        public List<ColumnDefinition>? Columns { get; set; }
        public int? Rows { get; set; }
        public int? GridColumns { get; set; }
        public List<CellDefinition>? Cells { get; set; }
    }

    // ============================================================================
    // TABLE CONFIGURATION CLASSES
    // ============================================================================

    /// <summary>
    /// Configuration for simple tables with basic column headers
    /// </summary>
    public class TableConfigDefinition
    {
        public List<string> Columns { get; set; } = new();
        public int MinRows { get; set; }
        public bool AllowAddRows { get; set; }
    }

    /// <summary>
    /// Column definition for advanced tables
    /// </summary>
    public class ColumnDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "text"; // text, number, date, email
    }

    /// <summary>
    /// Cell definition for advanced tables with merge support
    /// </summary>
    public class CellDefinition
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public int Rowspan { get; set; } = 1;
        public int Colspan { get; set; } = 1;
        public string? ColumnId { get; set; }
        public string? ColumnName { get; set; }
        public bool IsHeader { get; set; }
        public bool IsMerged { get; set; }
        public bool Hidden { get; set; }
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

       

        // ✅ Add these for FR 4.3.1 completeness
        public int AwaitingReviewCount { get; set; }           // Submitted but not reviewed
        public int AmendmentRequiredCount { get; set; }         // tasks needing amendment
        public int RevisionRequiredCount { get; set; }          // submissions rejected back
        public int PendingVerificationCount { get; set; }       // submissions flagged
    

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

    public class VerificationAttachment
    {

        public int VerificationId { get; set; }
        public int SubmissionId { get; set; }
        public string? Type { get; set; }  // "CIPC", "Credit"
        public string? ResultJson { get; set; }
        public DateTime CreatedAt { get; set; }
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