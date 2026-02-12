using Declarify.Data;
using Declarify.Models;
using Declarify.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Declarify.Services.Methods
{
    public class EmployeeService : IEmployeeService
    {

        private readonly ApplicationDbContext _db;
        private readonly ILogger<EmployeeService> _logger;
        private readonly IUserService _userService;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeService(
            ApplicationDbContext context,
            ILogger<EmployeeService> logger,
            IUserService userService, UserManager<ApplicationUser> userManager)
        {
            _db = context;
            _logger = logger;
            _userService = userService;
            _userManager = userManager;
        }
        public async Task<List<EmployeeListItemDto>> GetAllActiveEmployeesAsync() {

            return await _db.Employees.Where(e => e.IsActive)
                    .Include(e => e.Manager)
                    .OrderBy(e => e.Full_Name)
                    .Select(e => new EmployeeListItemDto { 
                    
                    EmployeeId=e.EmployeeId,
                    EmployeeNumber=e.EmployeeNumber,
                    FullName=e.Full_Name,
                    Position=e.Position,
                    Department=e.Department,
                    ManagerName=e.Manager !=null?e.Manager.Full_Name : null

                    
                    }).ToListAsync();
        }

        public async Task<List<Employee>> GetPotentialManagersAsync(string position, string department)
        {
            // Define position hierarchy (customize based on your organization)
            var positionHierarchy = new Dictionary<string, int>
            {
                { "Intern", 1 },
                { "Junior", 2 },
                { "Associate", 3 },
                { "Senior", 4 },
                { "Lead", 5 },
                { "Manager", 6 },
                { "Senior Manager", 7 },
                { "Director", 8 },
                { "Senior Director", 9 },
                { "VP", 10 },
                { "Senior VP", 11 },
                { "Executive VP", 12 },
                { "C-Level", 13 },
                { "CEO", 14 }
            };

            // Determine the level of the position being added
            int currentLevel = GetPositionLevel(position, positionHierarchy);

            // Get employees who could be managers (higher level positions)
            var potentialManagers = await _db.Employees
                .Where(e => e.IsActive)
                .Where(e => e.Department == department ||
                            e.Position.Contains("Director") ||
                            e.Position.Contains("VP") ||
                            e.Position.Contains("CEO"))
                .ToListAsync();

            // Filter by position level and sort by relevance
            var filteredManagers = potentialManagers
                .Where(e => GetPositionLevel(e.Position ?? "", positionHierarchy) > currentLevel)
                .OrderBy(e => e.Department == department ? 0 : 1) // Prioritize same department
                .ThenBy(e => GetPositionLevel(e.Position ?? "", positionHierarchy)) // Then by closest level
                .ThenBy(e => e.Full_Name)
                .ToList();

            return filteredManagers;
        }
        public async Task<Employee> CreateEmployeeAsync(EmployeeViewModel model)
        {
            // Validate uniqueness
            if (!await IsEmployeeNumberUniqueAsync(model.EmployeeNumber))
                throw new InvalidOperationException($"Employee number {model.EmployeeNumber} already exists.");

            if (!await IsEmailUniqueAsync(model.Email))
                throw new InvalidOperationException($"Email {model.Email} is already registered.");

            // Validate manager exists if provided
            if (model.ManagerId.HasValue)
            {
                var managerExists = await _db.Employees
                    .AnyAsync(e => e.EmployeeId == model.ManagerId.Value && e.IsActive);

                if (!managerExists)
                    throw new InvalidOperationException("Selected manager does not exist or is inactive.");
            }

            // Validate email domain if configured
            var configuredDomain = await GetConfiguredDomainAsync();
            if (!string.IsNullOrEmpty(configuredDomain) && !IsValidDomain(model.Email, configuredDomain))
                throw new InvalidOperationException($"Email domain must match configured domain ({configuredDomain})");

            try
            {
                // ✅ NO Identity user creation
                // ✅ NO temp password
                // ✅ Just save Employee record

                var employee = new Employee
                {
                    EmployeeNumber = model.EmployeeNumber,
                    Full_Name = $"{model.Full_Name} {model.Surname_Name}".Trim(),
                    Email_Address = model.Email,
                    Position = model.Position,
                    Department = model.Department,
                    ManagerId = model.ManagerId,
                    Region = model.Region,
                    DomainId = model.DomainId,
                    IsActive = true,

                    // leave null because we are NOT creating an ApplicationUser
                    ApplicationUserId = null
                };

                _db.Employees.Add(employee);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Employee created (DB only). EmployeeId: {EmployeeId}, Email: {Email}",
                    employee.EmployeeId, employee.Email_Address);

                return employee;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee (DB only): {Email}", model.Email);
                throw;
            }
        }
        public async Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber)
        {
            return !await _db.Employees
                .AnyAsync(e => e.EmployeeNumber == employeeNumber);
        }

        public async Task<bool> IsEmailUniqueAsync(string email)
        {
            return !await _db.Employees
                .AnyAsync(e => e.Email_Address == email);
        }
        private int GetPositionLevel(string position, Dictionary<string, int> hierarchy)
        {
            // Try exact match first
            if (hierarchy.TryGetValue(position, out int level))
            {
                return level;
            }

            // Try partial match
            foreach (var kvp in hierarchy.OrderByDescending(x => x.Value))
            {
                if (position.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Default to mid-level if unknown
            return 4;
        }

        private string DetermineRoleInCompany(string position)
        {
            position = position.ToLower();

            if (position.Contains("ceo") || position.Contains("president") || position.Contains("chief"))
                return "Executive";
            if (position.Contains("director") || position.Contains("vp") || position.Contains("vice president"))
                return "Executive";
            if (position.Contains("manager") || position.Contains("lead") || position.Contains("head"))
                return "Manager";

            return "Employee";
        }

        private string DetermineUserRole(string position)
        {
            position = position.ToLower();

            if (position.Contains("ceo") || position.Contains("admin"))
                return "Admin";
            if (position.Contains("director") || position.Contains("vp") || position.Contains("manager"))
                return "Manager";

            return "Employee";
        }
        // Bulk load employees from CSV data (FR 4.1.3)
        public async Task<EmployeeBulkLoadResult> BulkLoadEmployeesAsync(List<EmployeeImportDto> employees)
        {
            var result = new EmployeeBulkLoadResult();
            var configuredDomain = await GetConfiguredDomainAsync();

            // We'll collect manager assignments to apply them in a second pass
            var managerAssignments = new List<(string EmployeeNumber, string ManagerEmployeeNumber)>();

            // =======================
            // FIRST PASS: Create / Update employees (without managers)
            // =======================
            foreach (var emp in employees)
            {
                try
                {
                    // Basic validation
                    if (string.IsNullOrWhiteSpace(emp.EmployeeNumber) ||
                        string.IsNullOrWhiteSpace(emp.Email_Address))
                    {
                        result.Errors.Add($"Employee {emp.EmployeeNumber ?? "unknown"}: Missing EmployeeNumber or Email_Address");
                        result.FailedCount++;
                        continue;
                    }

                    // Validate email domain (FR 4.1.2)
                    if (!string.IsNullOrEmpty(configuredDomain) &&
                        !IsValidDomain(emp.Email_Address, configuredDomain))
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
                        // Update existing
                        existingEmployee.Full_Name = emp.Full_Name;
                        existingEmployee.Email_Address = emp.Email_Address;
                        existingEmployee.Position = emp.Position;
                        existingEmployee.Department = emp.Department;
                        // Note: We DON'T set ManagerId here yet
                        result.UpdatedCount++;
                    }
                    else
                    {
                        // Create new
                        var newEmployee = new Employee
                        {
                            EmployeeNumber = emp.EmployeeNumber,
                            Full_Name = emp.Full_Name,
                            Email_Address = emp.Email_Address,
                            Position = emp.Position,
                            Department = emp.Department,
                            // ManagerId = null for now → set in second pass
                            IsActive = true // ← add if you have this property
                        };
                        _db.Employees.Add(newEmployee);
                        result.CreatedCount++;
                    }

                    // Remember manager relationship for second pass (if provided)
                    if (!string.IsNullOrWhiteSpace(emp.ManagerEmployeeNumber))
                    {
                        managerAssignments.Add((emp.EmployeeNumber, emp.ManagerEmployeeNumber));
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Employee {emp.EmployeeNumber}: {ex.Message}");
                    result.FailedCount++;
                    _logger.LogError(ex, "Error processing employee {EmployeeNumber}", emp.EmployeeNumber);
                }
            }

            // Save all employees first → now they all have real EmployeeIds
            await _db.SaveChangesAsync();

            // =======================
            // SECOND PASS: Assign managers using real EmployeeId
            // =======================
            foreach (var (employeeNumber, managerEmployeeNumber) in managerAssignments)
            {
                try
                {
                    var employee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeNumber == employeeNumber);

                    var manager = await _db.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeNumber == managerEmployeeNumber);

                    if (employee == null)
                    {
                        result.Errors.Add($"Cannot assign manager: Employee {employeeNumber} not found after creation/update");
                        result.FailedCount++;
                        continue;
                    }

                    if (manager == null)
                    {
                        result.Errors.Add($"Manager not found for employee {employeeNumber} (manager number: {managerEmployeeNumber})");
                        continue; // don't increment failed - employee was still processed
                    }

                    // This is the important part!
                    employee.ManagerId = manager.EmployeeId;

                    _db.Employees.Update(employee);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to assign manager for {employeeNumber}: {ex.Message}");
                    _logger.LogError(ex, "Manager assignment failed for {EmployeeNumber}", employeeNumber);
                }
            }

            // Final save for all manager relationships
            await _db.SaveChangesAsync();

            result.TotalProcessed = employees.Count;
            result.SuccessCount = result.CreatedCount + result.UpdatedCount;

            _logger.LogInformation(
                "Bulk load completed. Created: {Created}, Updated: {Updated}, Failed: {Failed}",
                result.CreatedCount, result.UpdatedCount, result.FailedCount);

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

        public async Task<Employee?> GetEmployeeByEmailAsync(string employeeEmail)
        {
            return await _db.Employees
                .Include(e => e.Manager)
                .Include(e => e.Subordinates)
                .FirstOrDefaultAsync(e => e.Email_Address == employeeEmail);
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
        /// <summary>
        /// Creates or updates employee WITHOUT setting the ManagerId.
        /// Manager should be assigned in a separate step after all employees exist.
        /// </summary>
        public async Task<Employee> CreateOrUpdateEmployeeAsync(EmployeeImportDto dto)
        {
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeNumber == dto.EmployeeNumber);

            if (employee == null)
            {
                employee = new Employee
                {
                    EmployeeNumber = dto.EmployeeNumber!,
                    Full_Name = dto.Full_Name ?? "",
                    Email_Address = dto.Email_Address ?? "",
                    Position = dto.Position,
                    Department = dto.Department,
                    Region = dto.Region,           // if you have it
                    IsActive = true,
                    // IMPORTANT: ManagerId remains NULL here
                };

                _db.Employees.Add(employee);
            }
            else
            {
                employee.Full_Name = dto.Full_Name ?? employee.Full_Name;
                employee.Email_Address = dto.Email_Address ?? employee.Email_Address;
                employee.Position = dto.Position ?? employee.Position;
                employee.Department = dto.Department ?? employee.Department;
                employee.Region = dto.Region ?? employee.Region;
                employee.IsActive = true;

                // Do NOT touch ManagerId here!
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
