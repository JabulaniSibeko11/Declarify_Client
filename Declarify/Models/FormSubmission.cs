using Declarify.Models.ViewModels;
using Microsoft.Build.Framework;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Declarify.Models
{
    public class FormSubmission
    {
        [Key]
        public int SubmissionId { get; set; }

        
        public int FormTaskId { get; set; } // FK

        public DateTime Submitted_Date { get; set; }

        // Status: "Pending", "Approved", "Rejected", "Draft"
        
        [MaxLength(50)]
        public string? Status { get; set; } = "Pending";


        [Column(TypeName = "jsonb")] // PostgreSQL JSONB
        public string? FormData { get; set; } // JSON string for submitted answers

        public string? DigitalAttestation { get; set; } // e.g., JSON or string for declaration


        public int? AssignedManagerId { get; set; }   // New: who should review/approve this
        public string? AssignedManagerName { get; set; }  // Optional: for display purposes

        public string ReviewerNotes { get; set; } = string.Empty; // New: notes from the reviewer/manager
        public string? ReviewerSignature { get; set; } // New: digital signature of the reviewer
        public DateTime? ReviewedDate { get; set; } // New: when the review was completed

     

        public string? PdfFileName { get; set; }
        public string? PdfFilePath { get; set; }
        public DateTime? PdfGeneratedUtc { get; set; }

        // ✅ Amendment / Resubmission (FR 4.2.5)
        public int VersionNo { get; set; } = 1;
   
        public int? AmendmentOfSubmissionId { get; set; }
        public virtual FormSubmission? AmendsSubmission { get; set; }

        // Navigation properties
        public virtual FormTask? Task { get; set; }
        public virtual ICollection<VerificationResult>? VerificationResults { get; set; }

        public virtual ICollection<VerificationAttachment> VerificationAttachments { get; set; }
         = new List<VerificationAttachment>();
    }
}
