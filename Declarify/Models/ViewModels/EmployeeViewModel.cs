using System.ComponentModel.DataAnnotations;

namespace Declarify.Models.ViewModels
{
    public class EmployeeViewModel
    {
        [Required(ErrorMessage = "Employee Number is required")]
        [Display(Name = "Employee Number")]
        public string EmployeeNumber { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [MaxLength(100)]
        public string Full_Name { get; set; }

        [Required(ErrorMessage = "Surname is required")]
        [Display(Name = "Surname")]
        [MaxLength(100)]
        public string Surname_Name { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Position is required")]
        [Display(Name = "Position")]
        [MaxLength(100)]
        public string Position { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [Display(Name = "Department")]
        [MaxLength(100)]
        public string Department { get; set; }

        [Display(Name = "Line Manager")]
        public int? ManagerId { get; set; }

        [Display(Name = "Region")]
        [MaxLength(100)]
        public string? Region { get; set; }

        [Display(Name = "Domain")]
        public int? DomainId { get; set; }
    }

    public class EmployeeListItemDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeNumber { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
        public string? ManagerName { get; set; }
    }
}

