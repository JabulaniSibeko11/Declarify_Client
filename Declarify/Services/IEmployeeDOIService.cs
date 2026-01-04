using Declarify.Models;
using Declarify.Models.ViewModels;
using Microsoft.Build.Framework;
using System.Text.Json;

namespace Declarify.Services
{
    public interface IEmployeeDOIService
    {
        // Existing methods from PRD
        Task<FormTask?> GetDOITaskByTokenAsync(string token);
        Task<Template?> GetFormTemplateForTaskAsync(int taskId);
        Task SaveDraftAsync(string token, JsonDocument formData);
        Task<SubmissionResult> SubmitDOIAsync(string token, JsonDocument formData, string attestationSignature);
        Task SendReminderEmailsAsync();

        Task<Employee> GetEmployeeByIdAsync(int employeeId);
        // Additional methods for Employee Dashboard
        Task<EmployeeDashboardViewModel> GetEmployeeDashboardAsync(int employeeId);

        Task<EmployeeDashboardViewModel> GetAdminDashboardAsync(int employeeId);
        Task<List<FormTask>> GetEmployeeTasksAsync(int employeeId);
        Task<FormSubmission?> GetDraftSubmissionAsync(int taskId);
        Task<EmployeeProfile> GetEmployeeProfileAsync(int employeeId);
        Task<bool> ValidateAccessTokenAsync(string token);
        Task<ComplianceStats> GetEmployeeComplianceStatsAsync(int employeeId);

        Task<ExecutiveDashboardViewModel> GetExecutiveDashboardAsync();
    }
}
