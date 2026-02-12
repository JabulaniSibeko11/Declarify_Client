using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class FormTask
    {
        [Key]
        public int TaskId { get; set; }

        [Required]
        public int EmployeeId { get; set; } // FK

        [Required]
        public int TemplateId { get; set; } // FK

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public string? Status { get; set; } // e.g., "Outstanding", "Submitted", "Reviewed"

        public string? AccessToken { get; set; }

        public DateTime TokenExpiry { get; set; }
        // Navigation properties
        public virtual Employee? Employee { get; set; }
        public virtual Template? Template { get; set; }


        // ✅ Amendment / Resubmission (FR 4.2.5)
        public bool IsAmendmentRequired { get; set; } = false;
        public string? AmendmentReason { get; set; }
        public DateTime? AmendmentRequestedAt { get; set; }
        public string? AmendmentRequestedBy { get; set; } // name/email/role (simple)

        public virtual FormSubmission? FormSubmission { get; set; }
    }
}
