namespace Declarify.Models.ViewModels
{
    public class EmployeeDashboardViewModel
    {
        public EmployeeProfile Employee { get; set; } = null!;
        public List<FormTask> Tasks { get; set; } = new();
        public ComplianceStats Stats { get; set; } = null!;
        public bool HasPendingTasks { get; set; }
        public bool HasOverdueTasks { get; set; }

        // Existing properties...
        // === New: Reviewer / Line Manager Support ===
        public bool IsLineManager { get; set; }
        public List<SubordinateComplianceViewModel> Subordinates { get; set; } = new();
        public int PendingReviewsCount { get; set; } // For badge on "Team Approvals" tab

        public List<EmployeeTaskDto> AdminTasks { get; set; } = new List<EmployeeTaskDto>();
      
        public double TasksComplianceRate { get; set; }

     

    }
    public class EmployeeProfile
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string? ManagerName { get; set; }

        // Computed role properties based on position title
        public bool IsExecutive { get; set; }
        public bool IsSeniorManagement { get; set; }
        public bool HasManagerTitle { get; set; }

    }
    public class ComplianceStats
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OutstandingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double ComplianceRate { get; set; }
        public int CurrentStreak { get; set; }
    }

    // Submission result

    // Task detail view model
    public class TaskDetailViewModel
    {
        public FormTask Task { get; set; } = null!;
        public Template Template { get; set; } = null!;
        public FormSubmission? DraftSubmission { get; set; }
        public bool CanSubmit { get; set; }
        public string? ErrorMessage { get; set; }

        // New properties for signature handling
        public string EmployeeSignature { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeEmail { get; set; } = string.Empty;
    }
    public class SaveDraftRequest
    {
        public string Token { get; set; } = string.Empty;
        public string FormData { get; set; } = string.Empty;
    }

    public class SubmitFormRequest
    {
        public string Token { get; set; } = string.Empty;
        public string FormData { get; set; } = string.Empty;
        public string AttestationSignature { get; set; } = string.Empty;
    }
}
