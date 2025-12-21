using Declarify.Data;
using Declarify.Models;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class EmployeeService : IEmployeeService
    {

        private readonly ApplicationDbContext _db;
        private readonly ILogger<EmployeeService> _logger;
        private readonly IUserService _userService;

        public EmployeeService(
            ApplicationDbContext context,
            ILogger<EmployeeService> logger,
            IUserService userService)
        {
            _db = context;
            _logger = logger;
            _userService = userService;
        }
        // Bulk load employees from CSV data (FR 4.1.3)
        public async Task<EmployeeBulkLoadResult> BulkLoadEmployeesAsync(List<EmployeeImportDto> employees)
        {
            var result = new EmployeeBulkLoadResult();
            var configuredDomain = await GetConfiguredDomainAsync();

            foreach (var emp in employees)
            {
                try
                {
                    // Validate email domain (FR 4.1.2)
                    if (!string.IsNullOrEmpty(configuredDomain) && !IsValidDomain(emp.Email_Address, configuredDomain))
                    {
                        result.Errors.Add($"Employee {emp.EmployeeNumber}: Email domain does not match configured domain ({configuredDomain})");
                        result.FailedCount++;
                        continue;
                    }

                    // Check if employee already exists
                    var existingEmployee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeNumber == emp.EmployeeNumber);

                    if (existingEmployee != null)
                    {
                        // Update existing employee
                        existingEmployee.Full_Name = emp.Full_Name;
                        existingEmployee.Email_Address = emp.Email_Address;
                        existingEmployee.Position = emp.Position;
                        existingEmployee.Department = emp.Department;
                        existingEmployee.ManagerId = emp.ManagerId;

                        result.UpdatedCount++;
                    }
                    else
                    {
                        // Create new employee
                        var newEmployee = new Employee
                        {
                            EmployeeNumber = emp.EmployeeNumber,
                            Full_Name = emp.Full_Name,
                            Email_Address = emp.Email_Address,
                            Position = emp.Position,
                            Department = emp.Department,
                            ManagerId = emp.ManagerId
                        };

                        _db.Employees.Add(newEmployee);
                        result.CreatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Employee {emp.EmployeeNumber}: {ex.Message}");
                    result.FailedCount++;
                    _logger.LogError(ex, $"Error importing employee {emp.EmployeeNumber}");
                }
            }

            await _db.SaveChangesAsync();
            result.TotalProcessed = employees.Count;
            result.SuccessCount = result.CreatedCount + result.UpdatedCount;

            _logger.LogInformation($"Bulk load completed. Created: {result.CreatedCount}, Updated: {result.UpdatedCount}, Failed: {result.FailedCount}");
            return result;
        }
        // Get all employees for admin dashboard
        public async Task<List<Employee>> GetAllEmployeesAsync()
        {
            return await _db.Employees
                .Include(e => e.Manager)
                .OrderBy(e => e.Department)
                .ThenBy(e => e.Full_Name)
                .ToListAsync();
        }

        // Get employee by ID with manager information
        public async Task<Employee?> GetEmployeeByIdAsync(int employeeId)
        {
            return await _db.Employees
                .Include(e => e.Manager)
                .Include(e => e.Subordinates)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        }

        // Get all subordinates for a manager (used by Reviewer module)
        public async Task<List<Employee>> GetSubordinatesAsync(int managerId)
        {
            return await _db.Employees
                .Where(e => e.ManagerId == managerId)
                .OrderBy(e => e.Full_Name)
                .ToListAsync();
        }

        // Get employees by department for filtered views
        public async Task<List<Employee>> GetEmployeesByDepartmentAsync(string department)
        {
            return await _db.Employees
                .Where(e => e.Department == department)
                .OrderBy(e => e.Full_Name)
                .ToListAsync();
        }

        // Get department breakdown for compliance reporting (FR 4.5.3)
        public async Task<Dictionary<string, int>> GetDepartmentEmployeeCountsAsync()
        {
            return await _db.Employees
                .GroupBy(e => e.Department ?? "Unassigned")
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Department, x => x.Count);
        }
        // Create or update single employee record
        public async Task<Employee> CreateOrUpdateEmployeeAsync(EmployeeImportDto dto)
        {
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeNumber == dto.EmployeeNumber);

            if (employee == null)
            {
                employee = new Employee
                {
                    EmployeeNumber = dto.EmployeeNumber,
                    Full_Name = dto.Full_Name,
                    Email_Address = dto.Email_Address,
                    Position = dto.Position,
                    Department = dto.Department,
                    ManagerId = dto.ManagerId
                };
                _db.Employees.Add(employee);
            }
            else
            {
                employee.Full_Name = dto.Full_Name;
                employee.Email_Address = dto.Email_Address;
                employee.Position = dto.Position;
                employee.Department = dto.Department;
                employee.ManagerId = dto.ManagerId;
            }

            await _db.SaveChangesAsync();
            return employee;
        }

        // Delete employee (soft delete or hard delete based on business rules)
        public async Task<bool> DeleteEmployeeAsync(int employeeId)
        {
            var employee = await _db.Employees.FindAsync(employeeId);
            if (employee == null) return false;

            // Check if employee has any DOI tasks
            var hasTasks = await _db.DOITasks.AnyAsync(t => t.EmployeeId == employeeId);

            if (hasTasks)
            {
                _logger.LogWarning($"Cannot delete employee {employeeId} - has associated DOI tasks");
                return false;
            }

            _db.Employees.Remove(employee);
            await _db.SaveChangesAsync();
            return true;
        }

        // Validate email domain against configured organizational domain
        private bool IsValidDomain(string? email, string configuredDomain)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(configuredDomain))
                return false;

            var emailDomain = email.Split('@').LastOrDefault();
            return emailDomain?.Equals(configuredDomain, StringComparison.OrdinalIgnoreCase) ?? false;
        }
        // Get configured organizational domain from settings
        private async Task<string?> GetConfiguredDomainAsync()
        {
            var domain = await _db.OrganizationalDomains
                .Select(d => d.DomainName)
                .FirstOrDefaultAsync();

            return domain;
        }

        // Get total employee count for dashboard (FR 4.5.1)
        public async Task<int> GetTotalEmployeeCountAsync()
        {
            return await _db.Employees.CountAsync();
        }
    }
}
