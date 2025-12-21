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

        // Navigation properties
        public virtual FormTask? Task { get; set; }
        public virtual ICollection<VerificationResult>? VerificationResults { get; set; }


    }
}
