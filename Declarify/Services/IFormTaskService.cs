using Declarify.Models;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services
{
    public interface IFormTaskService
    {
        Task BulkCreateTasksAsync(int templateId, DateTime dueDate, List<int> employeeIds); // FR 4.3.1
        Task GenerateAndSendMagicLinksAsync(int bulkRequestId); // FR 4.3.2, FR 4.3.3
        Task SendRemindersAsync(); // Scheduled: on due date & 7 days before (FR 4.3.4)
        Task<int> GetNonCompliantCountAsync();
        Task<IEnumerable<FormTask>> GetTasksForEmployeeAsync(int employeeId);
        Task<IEnumerable<FormTask>> GetTasksForReviewerAsync(int reviewerId); // Subordinates only (FR 4.5.2)

        // Dashboard metrics (FR 4.5.1, FR 4.5.3)
        Task<int> GetTotalEmployeesAsync();
        Task<int> GetOutstandingCountAsync();
        Task<int> GetOverdueCountAsync();
        Task<int> GetSubmittedCountAsync();
        Task<int> GetReviewedCountAsync();
        Task<bool> CancelTaskAsync(int taskId);
        Task<int> BulkExtendDueDateAsync(List<int> taskIds, DateTime newDueDate);
       
        Task<double> GetCompliancePercentageAsync();
        Task<List<FormTask>> GetTasksByStatusAsync(string status);
        Task<List<FormTask>> GetTasksByTemplateAsync(int templateId);
        Task<List<FormTask>> GetTasksDueInRangeAsync(DateTime StateDate ,DateTime EndDate);
        Task<ComplianceDashboardData> GetComplianceDashboardDataAsync();
        // New: Department breakdown (uncomment and add this)
        Task<Dictionary<string, DepartmentComplianceStats>> GetDepartmentBreakdownAsync();
        //Task<Dictionary<string, ComplianceBreakdown>> GetDepartmentBreakdownAsync();
    }
}
