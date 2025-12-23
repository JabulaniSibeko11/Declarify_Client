namespace Declarify.Models.ViewModels
{
    public class SubordinateComplianceViewModel
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
        public List<TaskSummary> Tasks { get; set; } = new List<TaskSummary>();
    }
    public class TaskSummary
    {
        public int TaskId { get; set; }
        public string TemplateName { get; set; }
        public string Status { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public bool IsOverdue { get; set; }
    }

 

    public class SignOffResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}

