using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Declarify.Models
{
    public class FormFieldResponse
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FormSubmissionId { get; set; }

        [Required]
        public int FieldId { get; set; }

        // Stores the actual response value (text, number, date, etc.)
        public string? ResponseValue { get; set; }

        // For table responses - stores JSON array of rows
        public string? TableData { get; set; }

        // For signature - stores file path or base64 string?
        public string? SignatureData { get; set; }

        public DateTime ResponseDate { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("FormSubmissionId")]
        public virtual FormSubmission FormSubmission { get; set; }

        [ForeignKey("FieldId")]
        public virtual TemplateField Field { get; set; }
    }
}
