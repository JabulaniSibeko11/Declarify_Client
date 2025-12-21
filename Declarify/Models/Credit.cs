using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class Credit
    {
        [Key]
        public int CreditId { get; set; }

        [Required]
        public int BatchAmount { get; set; }

        public int RemainingAmount { get; set; }
        [Required]
        public DateTime LoadDate { get; set; }

        public DateTime ExpiryDate { get; set; }
    }
}
