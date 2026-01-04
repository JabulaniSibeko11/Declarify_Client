using System;
using System.Collections.Generic;

namespace Declarify.Models.ViewModels
{
    public class UserRoleManagementViewModel
    {
        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int ExecutiveCount { get; set; }
        public int EmployeeCount { get; set; }
        public List<UserRoleItem> Users { get; set; } = new List<UserRoleItem>();
    }

    public class UserRoleItem
    {
        public string UserId { get; set; }
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
        public string CurrentRole { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public string PhoneNumber { get; set; }
        public int? CurrentManagerId { get; set; }
        public string CurrentManagerName { get; set; }
    }
}