using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class License
    {
        [Key]
        public int LicenseId { get; set; }

        public string? LicenseKey { get; set; }
        public DateTime ExpiryDate { get; set; } // Annual, e.g., February 1st

        public bool IsActive { get; set; }
    }
}
