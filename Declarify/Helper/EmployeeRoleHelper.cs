namespace Declarify.Helper
{
    public class EmployeeRoleHelper
    {
        private static readonly string[] ExecutiveTitles = new[]
       {
            "CEO", "CFO", "CTO", "COO", "CIO", "CMO",
            "Chief Executive", "Chief Financial", "Chief Technology",
            "Chief Operating", "Chief Information", "Chief Marketing",
            "Managing Director", "Executive Director"
        };

        // Senior Management positions
        private static readonly string[] SeniorManagementTitles = new[]
        {
            "Director", "VP", "Vice President", "Head of", "General Manager",
            "Regional Manager", "Senior Manager", "Executive Manager"
        };

        // Manager positions (potential line managers)
        private static readonly string[] ManagerTitles = new[]
        {
            "Manager", "Supervisor", "Team Lead", "Team Leader",
            "Section Head", "Unit Manager", "Department Manager"
        };

        /// <summary>
        /// Determines if employee is an executive based on position title
        /// </summary>
        public static bool IsExecutive(string? position)
        {
            if (string.IsNullOrWhiteSpace(position)) return false;

            return ExecutiveTitles.Any(title =>
                position.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines if employee is senior management based on position title
        /// </summary>
        public static bool IsSeniorManagement(string? position)
        {
            if (string.IsNullOrWhiteSpace(position)) return false;

            // Executives are also senior management
            if (IsExecutive(position)) return true;

            return SeniorManagementTitles.Any(title =>
                position.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines if employee has a management position title
        /// (does not check if they actually have subordinates)
        /// </summary>
        public static bool HasManagerTitle(string? position)
        {
            if (string.IsNullOrWhiteSpace(position)) return false;

            // Executives and senior management have manager authority
            if (IsSeniorManagement(position)) return true;

            return ManagerTitles.Any(title =>
                position.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines role level for display purposes
        /// </summary>
        public static string GetRoleLevel(string? position)
        {
            if (IsExecutive(position)) return "Executive";
            if (IsSeniorManagement(position)) return "Senior Management";
            if (HasManagerTitle(position)) return "Manager";
            return "Employee";
        }
    }
}
