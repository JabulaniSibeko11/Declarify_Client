namespace Declarify.Models
{
    public class ProcessReviewRequest
    {
        public int SubmissionId { get; set; }
        public string Action { get; set; } = string.Empty; // "approve" or "reject"
        public string? ReviewerNotes { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerPosition { get; set; } = string.Empty;

        public string? VerificationType { get; set; }
        public string? ReviewerSignature { get; set; } // Base64 signature image (FR 4.5.4)

    }
}
