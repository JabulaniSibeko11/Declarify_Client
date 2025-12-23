namespace Declarify.Models.ViewModels
{
    public class ExecutiveDashboardViewModel
    {
        public Employee Employee { get; set; }
        public OverallStatistics OverallStats { get; set; }
        public List<DepartmentStatistics> DepartmentStats { get; set; }
        public List<RegionStatistics> RegionStats { get; set; }
    }

    public class OverallStatistics
    {
        public int TotalEmployees { get; set; }
        public int CompliantCount { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal ComplianceRate { get; set; }
        public int TotalSubmissions { get; set; }
        public decimal AverageSubmissionDays { get; set; }
    }

    public class DepartmentStatistics
    {
        public string DepartmentName { get; set; }
        public int TotalEmployees { get; set; }
        public int CompliantCount { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal ComplianceRate { get; set; }
    }

    public class RegionStatistics
    {
        public string RegionName { get; set; }
        public int TotalEmployees { get; set; }
        public int CompliantCount { get; set; }
        public int OutstandingCount { get; set; }
        public int OverdueCount { get; set; }
        public decimal ComplianceRate { get; set; }
    }
}
