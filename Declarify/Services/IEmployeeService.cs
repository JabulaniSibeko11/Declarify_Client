using Declarify.Models;

namespace Declarify.Services
{
    public interface IEmployeeService
    {
        Task<EmployeeBulkLoadResult> BulkLoadEmployeesAsync(List<EmployeeImportDto> employees);
        Task<List<Employee>> GetAllEmployeesAsync();
        Task<Employee?> GetEmployeeByIdAsync(int employeeId);
        Task<List<Employee>> GetSubordinatesAsync(int managerId);
        Task<List<Employee>> GetEmployeesByDepartmentAsync(string department);
        Task<Dictionary<string, int>> GetDepartmentEmployeeCountsAsync();
        Task<Employee> CreateOrUpdateEmployeeAsync(EmployeeImportDto dto);
        Task<bool> DeleteEmployeeAsync(int employeeId);
        Task<int> GetTotalEmployeeCountAsync();
    }
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

}
