namespace Declarify.Services
{
    public interface IEmailService
    {
        Task SendMagicLinkAsync(string email, string uniqueLink, string employeeName);
        Task SendReminderAsync(string email, string employeeName, DateTime dueDate);
        Task SendBulkCompleteNotificationAsync(string adminEmail, int totalSent);
    }
}
