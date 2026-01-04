using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Declarify.Models.ViewModels
{
    public class ProfileViewModel
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Employee number is required")]
        [Display(Name = "Employee Number")]
        public string EmployeeNumber { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email Address")]
        public string EmailAddress { get; set; }

        [Required(ErrorMessage = "Position is required")]
        [StringLength(100, ErrorMessage = "Position cannot exceed 100 characters")]
        public string Position { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters")]
        public string Department { get; set; }

        [StringLength(100, ErrorMessage = "Region cannot exceed 100 characters")]
        public string? Region { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Manager")]
        public int? ManagerId { get; set; }

        public string? ManagerName { get; set; }

        [Display(Name = "Current Signature")]
        public string? CurrentSignaturePath { get; set; }

        [Display(Name = "Upload New Signature")]
        public IFormFile? SignatureFile { get; set; }

        public DateTime? SignatureCreatedDate { get; set; }

        public bool IsActive { get; set; }

        // Password Change Fields
        [Display(Name = "Current Password")]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "New Password")]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Display(Name = "Confirm New Password")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }

        // For dropdown population
        public List<ManagerDropdownItem>? AvailableManagers { get; set; }
    }

    public class ManagerDropdownItem
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
    }
}