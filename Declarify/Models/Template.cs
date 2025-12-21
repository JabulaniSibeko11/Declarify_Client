using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class Template
    {
        public int TemplateId { get; set; }

        [Required]
        [MaxLength(200)]
        public string? TemplateName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
        [Required]
        public string? TemplateConfig { get; set; } // nvarchar(max) → later becomes jsonb

        // Status: "Draft", "Active", "Archived"
        [Required]
        [MaxLength(50)]
        public string? Status { get; set; } = "Draft";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

       
    }
}
