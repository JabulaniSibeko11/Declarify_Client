namespace Declarify.Models.ViewModels
{
    public class ReviewSubmissionViewModel
    {
        public int SubmissionId { get; set; }
        public string EmployeeName { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string SubmissionStatus { get; set; }

        // Reviewer Information
        public string ReviewerName { get; set; }
        public string ReviewerPosition { get; set; }
        public string? DigitalAttestation { get; set; } // JSON containing employee's signature


        // Form Content
        public List<FormSectionViewModel> FormSections { get; set; }

        // Digital Attestation
        public string EmployeeAttestation { get; set; }
        public string EmployeeSignature { get; set; }

        public string TemplateConfig { get; set; }  // Raw JSON from Template table
        public string FormData { get; set; }        // Raw JSON from FormSubmission table
    }
    public class FormSectionViewModel
    {
        public string SectionTitle { get; set; } = string.Empty;
        public string? Disclaimer { get; set; }
        public int SectionOrder { get; set; }
        public List<FormFieldViewModel> Fields { get; set; } = new();

    }

    public class FormFieldViewModel
    {
        public string FieldId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string FieldType { get; set; } = "text";
        public bool IsRequired { get; set; }
        public int Order { get; set; }
        public List<string>? Options { get; set; }

    }

    // Request model for review processing
    public class ProcessReviewRequest
    {
        public int SubmissionId { get; set; }
        public string Action { get; set; } // "approve" or "reject"
        public string ReviewerNotes { get; set; }
        public string ReviewerSignature { get; set; } // Base64 signature
        public string ReviewerName { get; set; }
        public string ReviewerPosition { get; set; }
    }
}
