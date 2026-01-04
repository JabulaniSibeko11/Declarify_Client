namespace Declarify.Models.ViewModels
{
    public class SettingsViewModel
    {
        public List<UserRoleManagementItem> Users { get; set; } = new List<UserRoleManagementItem>();
        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int ExecutiveCount { get; set; }
        public int EmployeeCount { get; set; }
    }

    // Individual User for Role Management
    public class UserRoleManagementItem
    {
        public string UserId { get; set; }
        public int? EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
        public string CurrentRole { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    // Update Role Request
    public class UpdateUserRoleRequest
    {
        
        public string UserId { get; set; }

        
        public string NewRole { get; set; }

        public string? Reason { get; set; }
    }

    // Response Model
    public class UpdateUserRoleResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string UserName { get; set; }
        public string NewRole { get; set; }
    }
}