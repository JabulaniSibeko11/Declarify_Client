using Declarify.Models;
using Declarify.Models.ViewModels;

namespace Declarify.Services
{
    public interface IEmployeeService
    {
        Task<EmployeeBulkLoadResult> BulkLoadEmployeesAsync(List<EmployeeImportDto> employees);
        Task<List<Employee>> GetAllEmployeesAsync();
        Task<Employee?> GetEmployeeByIdAsync(int employeeId);
        Task<Employee?> GetEmployeeByEmailAsync(string employeeEmail);
        Task<List<Employee>> GetSubordinatesAsync(int managerId);
        Task<List<Employee>> GetEmployeesByDepartmentAsync(string department);
        Task<Dictionary<string, int>> GetDepartmentEmployeeCountsAsync();
        Task<Employee> CreateOrUpdateEmployeeAsync(EmployeeImportDto dto);
        Task<bool> DeleteEmployeeAsync(int employeeId);
        Task<int> GetTotalEmployeeCountAsync();
        Task<List<EmployeeListItemDto>> GetAllActiveEmployeesAsync();
        Task<List<Employee>> GetPotentialManagersAsync(string position, string department);
        Task<Employee> CreateEmployeeAsync(EmployeeViewModel model);
        Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber);
        Task<bool> IsEmailUniqueAsync(string email);
    }
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

}
