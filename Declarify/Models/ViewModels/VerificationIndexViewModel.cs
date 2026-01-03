namespace Declarify.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the verification index page listing all pending submissions
    /// </summary>
    public class VerificationIndexViewModel
    {
        public List<VerificationSubmissionViewModel> PendingSubmissions { get; set; } = new();
        public int CreditBalance { get; set; }
        public bool LowCreditWarning { get; set; }
        public bool CriticalCreditWarning { get; set; }
        public int TotalPendingCount => PendingSubmissions.Count;
        public int VerifiedCount => PendingSubmissions.Count(s => s.HasVerifications);
        public int UnverifiedCount => PendingSubmissions.Count(s => !s.HasVerifications);
    }

    /// <summary>
    /// ViewModel for individual submission in the list
    /// </summary>
    public class VerificationSubmissionViewModel
    {
        public int SubmissionId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public DateTime SubmittedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool HasVerifications { get; set; }
        public int VerificationCount { get; set; }

        public int DaysSinceSubmission => (DateTime.UtcNow - SubmittedDate).Days;
        public bool IsUrgent => DaysSinceSubmission > 7;
    }

    /// <summary>
    /// ViewModel for detailed verification page
    /// </summary>
    public class VerificationDetailViewModel
    {
        // Employee Information
        public int SubmissionId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int EmployeeId { get; set; }
        public string EmployeeEmail { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;

        // Submission Information
        public DateTime SubmittedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string FormData { get; set; } = string.Empty;
        public string? DigitalAttestation { get; set; }
        public string ReviewerNotes { get; set; } = string.Empty;

        // Verification Data
        public List<string> SuggestedEntities { get; set; } = new();
        public List<VerificationResultViewModel> ExistingVerifications { get; set; } = new();

        // Credit Information
        public int CreditBalance { get; set; }
        public int CipcCheckCost { get; set; } = 5;
        public int CreditCheckCost { get; set; } = 10;
        public bool CanRunCipcCheck { get; set; }
        public bool CanRunCreditCheck { get; set; }

        // Computed Properties
        public bool HasSuggestedEntities => SuggestedEntities.Any();
        public bool HasExistingVerifications => ExistingVerifications.Any();
        public int TotalVerificationCount => ExistingVerifications.Count;
    }

    /// <summary>
    /// ViewModel for displaying verification results
    /// </summary>
    public class VerificationResultViewModel
    {
        public int VerificationId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string VerificationType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime VerifiedDate { get; set; }
        public string ResultSummary { get; set; } = string.Empty;
        public int? InitiatedByEmployeeId { get; set; }
        public string InitiatedByName { get; set; } = string.Empty;

        public string VerificationBadgeClass => VerificationType switch
        {
            "CIPC" => "cipc-badge",
            "CreditCheck" => "credit-badge",
            _ => "default-badge"
        };

        public string StatusBadgeClass => Status switch
        {
            "Completed" => "success",
            "Failed" => "danger",
            "Pending" => "warning",
            _ => "secondary"
        };
    }
}