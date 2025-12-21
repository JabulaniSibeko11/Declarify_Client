using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class VerificationResult
    {
        [Key]
        public int VerificationId { get; set; }

        [Required]
        public int SubmissionId { get; set; } // FK to DOIFormSubmission

        public string? VerificationType { get; set; } // e.g., "CIPC", "Credit"

        public string? ResultData { get; set; } // Immutable JSON or string

        // Navigation
        public virtual FormSubmission? Submission { get; set; }
    }
}
