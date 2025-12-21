using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class OrganizationalDomain
    {
        [Key]
        public int DomainId { get; set; }

        [Required]
        [MaxLength(100)]
        public string DomainName { get; set; } = string.Empty; // e.g., "cityofjoburg.org.za"

        [MaxLength(200)]
        public string? OrganizationName { get; set; } // e.g., "City of Johannesburg"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    }
}
