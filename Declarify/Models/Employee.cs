using Microsoft.AspNetCore.Components.Web;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.ComponentModel.DataAnnotations;

namespace Declarify.Models
{
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }

        
        public string? EmployeeNumber { get; set; }

        public int? ManagerId { get; set; } // FK to Employee (self-referencing for hierarchy)

        
        public string? Full_Name { get; set; }

        
        [EmailAddress]
        public string? Email_Address { get; set; }

        public string? Position { get; set; }

        public string? Department { get; set; }
        public string? Signature_Picture { get; set; }
        public DateTime? Signature_Created_Date { get; set; }
        public int? DomainId { get; set; } // FK to OrganizationalDomain if needed
         public string? Region { get; set; }
        public string? ApplicationUserId { get; set; }
        // Navigation properties
        public virtual Employee? Manager { get; set; }

       public bool IsActive { get; set; }=true;
        public virtual ICollection<Employee> Subordinates { get; set; } = new List<Employee>();
        public virtual ICollection<FormTask> DOITasks { get; set; } = new List<FormTask>();
        public virtual ApplicationUser? ApplicationUser { get; set; }
        public virtual OrganizationalDomain? Domain { get; set; }


    }
}
