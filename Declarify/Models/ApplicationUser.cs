using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class ApplicationUser :IdentityUser
    {
        // Link to Employee table (FR 4.1.3)
        public int? EmployeeId { get; set; }
        [MaxLength(100)]
        public string? Full_Name { get; set; }

        [MaxLength(100)]
        public string? Position { get; set; }

        [MaxLength(100)]
        public string? Department { get; set; }

        // Role in company (Admin, Manager, Employee, Executive)
        [MaxLength(50)]
        public string? roleInCompany { get; set; }
        public string? Role { get; set; } // e.g., "Admin", "Reviewer", "Employee"

        public string? Signature { get; set; } // Digital signature (e.g., base64 image or cert)
                                               // Track if this is first login (FR 4.1.1)
        public bool IsFirstLogin { get; set; } = true;

        // Track password setup date
        public DateTime? PasswordSetupDate { get; set; }

        // Navigation to Employee
        public virtual Employee? Employee { get; set; }
    }
}
