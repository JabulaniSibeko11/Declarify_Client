using System.Net;
using System.Net.Mail;

namespace Declarify.Services.Methods
{
    // ============================================================================
    // EMAIL SERVICE IMPLEMENTATION
    // Handles all email communications (FR 4.3.3, FR 4.3.4)
    // ============================================================================

    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SmtpClient _smtpClient;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Configure SMTP client from appsettings
            var smtpHost = _configuration["Email:SmtpHost"] ?? "localhost";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];

            _smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(username, password)
            };
        }
        // Send magic link email to employee (FR 4.3.3)
        // CRITICAL: This is sent after bulk task creation

        public async Task SendMagicLinkAsync(string email, string uniqueLink, string employeeName)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@declarify.local";
                var fromName = _configuration["Email:FromName"] ?? "Declarify - Compliance & Disclosure Hub";

                var subject = "Action Required: Complete Your Declaration of Interest";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0066cc; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 24px; 
            background-color: #0066cc; 
            color: white; 
            text-decoration: none; 
            border-radius: 4px; 
            margin: 20px 0;
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .important {{ background-color: #fff3cd; padding: 15px; border-left: 4px solid #ffc107; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Declaration of Interest Required</h1>
        </div>
        <div class=""content"">
            <p>Dear {employeeName},</p>
            
            <p>You are required to complete your annual Declaration of Interest (DOI) form. This is a mandatory requirement for all employees as per organizational policy.</p>
            
            <div class=""important"">
                <strong>Important:</strong> Please complete this declaration by the due date to remain compliant.
            </div>
            
            <p>To access your personalized declaration form, click the button below:</p>
            
            <p style=""text-align: center;"">
                <a href=""{uniqueLink}"" class=""button"">Complete My Declaration</a>
            </p>
            
            <p style=""font-size: 12px; color: #666;"">
                Or copy and paste this link into your browser:<br>
                {uniqueLink}
            </p>
            
            <p><strong>What you'll need:</strong></p>
            <ul>
                <li>Details of any shares or financial interests</li>
                <li>Directorship or partnership information</li>
                <li>Gifts or hospitality received (value exceeding R500)</li>
            </ul>
            
            <p>You can save your progress and return later using the same link. The form will auto-save your entries as you complete each section.</p>
            
            <p>If you have any questions or experience technical difficulties, please contact your Compliance Officer or HR department.</p>
            
            <p>Thank you for your cooperation in maintaining transparency and ethical standards.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message from the Declarify Compliance & Disclosure Hub.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>
";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(email);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Magic link email sent successfully to {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send magic link email to {email}");
                throw;
            }
        }
        // Send reminder email to employee (FR 4.3.4)
        // Called 7 days before due date and on due date
        public async Task SendReminderAsync(string email, string employeeName, DateTime dueDate)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@declarify.local";
                var fromName = _configuration["Email:FromName"] ?? "Declarify - Compliance & Disclosure Hub";

                var daysUntilDue = (dueDate.Date - DateTime.UtcNow.Date).Days;
                var urgencyLevel = daysUntilDue <= 0 ? "URGENT" : "REMINDER";
                var urgencyColor = daysUntilDue <= 0 ? "#dc3545" : "#ffc107";

                var subject = daysUntilDue <= 0
                    ? "URGENT: Declaration of Interest Overdue"
                    : $"REMINDER: Declaration of Interest Due in {daysUntilDue} Days";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {urgencyColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .button {{ 
            display: inline-block; 
            padding: 12px 24px; 
            background-color: {urgencyColor}; 
            color: white; 
            text-decoration: none; 
            border-radius: 4px; 
            margin: 20px 0;
        }}
        .urgent-box {{ 
            background-color: #f8d7da; 
            border: 2px solid #dc3545; 
            padding: 15px; 
            margin: 15px 0; 
            border-radius: 4px;
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{urgencyLevel}: Declaration of Interest</h1>
        </div>
        <div class=""content"">
            <p>Dear {employeeName},</p>
            
            {(daysUntilDue <= 0 ?
                @"<div class=""urgent-box"">
                    <strong>⚠️ OVERDUE NOTICE</strong><br>
                    Your Declaration of Interest form is now overdue. Please complete it immediately to maintain compliance.
                </div>" :
                $@"<p><strong>This is a reminder that your Declaration of Interest form is due in {daysUntilDue} days.</strong></p>
                   <p>Due Date: <strong>{dueDate:MMMM d, yyyy}</strong></p>"
            )}
            
            <p>If you have already started your declaration, you can return to it using your unique link. If you haven't started yet, please do so as soon as possible.</p>
            
            <p style=""text-align: center;"">
                <a href=""#"" class=""button"">Complete My Declaration Now</a>
            </p>
            
            <p><strong>Why this is important:</strong></p>
            <ul>
                <li>Mandatory regulatory compliance requirement</li>
                <li>Maintains transparency and ethical standards</li>
                <li>Prevents potential conflicts of interest</li>
                <li>Required for employment compliance status</li>
            </ul>
            
            {(daysUntilDue <= 0 ?
                "<p style=\"color: #dc3545;\"><strong>Please note:</strong> Failure to complete your DOI may result in escalation to management and potential compliance consequences.</p>" :
                ""
            )}
            
            <p>If you need assistance or have questions about completing the form, please contact your Compliance Officer or HR department immediately.</p>
            
            <p>Thank you for your prompt attention to this matter.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated reminder from the Declarify Compliance & Disclosure Hub.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>
";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    Priority = daysUntilDue <= 0 ? MailPriority.High : MailPriority.Normal
                };
                mailMessage.To.Add(email);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Reminder email sent to {email} ({daysUntilDue} days until due)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send reminder email to {email}");
                throw;
            }
        }
        // Send bulk completion notification to admin
        // Called after bulk request is sent
        public async Task SendBulkCompleteNotificationAsync(string adminEmail, int totalSent)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@declarify.local";
                var fromName = _configuration["Email:FromName"] ?? "Declarify - Compliance & Disclosure Hub";

                var subject = $"Bulk DOI Request Completed - {totalSent} Employees Notified";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .stats-box {{ 
            background-color: #d4edda; 
            border-left: 4px solid #28a745; 
            padding: 15px; 
            margin: 15px 0; 
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>✓ Bulk DOI Request Completed</h1>
        </div>
        <div class=""content"">
            <p>Dear Administrator,</p>
            
            <p>Your bulk Declaration of Interest request has been successfully processed and distributed.</p>
            
            <div class=""stats-box"">
                <h3>Request Summary</h3>
                <p><strong>Total Employees Notified:</strong> {totalSent}</p>
                <p><strong>Timestamp:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            </div>
            
            <p>All employees have been sent unique access links to complete their declarations. The system will automatically track completion status and send reminders as configured.</p>
            
            <p><strong>Next Steps:</strong></p>
            <ul>
                <li>Monitor compliance dashboard for submission progress</li>
                <li>Review submitted declarations as they come in</li>
                <li>System will send automatic reminders 7 days before and on due date</li>
                <li>Follow up with non-compliant employees after the due date</li>
            </ul>
            
            <p>You can access the compliance dashboard at any time to view real-time statistics and drill down into department-level compliance.</p>
            
            <p>Thank you for using Declarify.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated notification from the Declarify Compliance & Disclosure Hub.</p>
        </div>
    </div>
</body>
</html>
";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(adminEmail);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Bulk completion notification sent to admin: {adminEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send bulk completion notification to {adminEmail}");
                throw;
            }
        }
        // Send license expiry warning to admin
        public async Task SendLicenseExpiryWarningAsync(string adminEmail, DateTime expiryDate, int daysRemaining)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@declarify.local";
                var fromName = _configuration["Email:FromName"] ?? "Declarify - Compliance & Disclosure Hub";

                var subject = $"License Expiry Warning - {daysRemaining} Days Remaining";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #ffc107; color: #000; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .warning-box {{ 
            background-color: #fff3cd; 
            border-left: 4px solid #ffc107; 
            padding: 15px; 
            margin: 15px 0; 
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>⚠️ License Expiry Warning</h1>
        </div>
        <div class=""content"">
            <p>Dear Administrator,</p>
            
            <div class=""warning-box"">
                <p><strong>Your Declarify license will expire in {daysRemaining} days.</strong></p>
                <p>Expiry Date: <strong>{expiryDate:MMMM d, yyyy}</strong></p>
            </div>
            
            <p>After the license expires, the system will block all viewing and submission functionality. Only the login and licensing screens will remain accessible.</p>
            
            <p><strong>Action Required:</strong></p>
            <ul>
                <li>Contact your vendor to renew your annual license</li>
                <li>Ensure payment is processed before the expiry date</li>
                <li>Allow 1-2 business days for license activation after payment</li>
            </ul>
            
            <p>To avoid service interruption, we recommend renewing at least 5 business days before the expiry date.</p>
            
            <p>If you have any questions about renewal, please contact your Declarify vendor or support team.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated notification from the Declarify Compliance & Disclosure Hub.</p>
        </div>
    </div>
</body>
</html>
";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    Priority = daysRemaining <= 7 ? MailPriority.High : MailPriority.Normal
                };
                mailMessage.To.Add(adminEmail);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"License expiry warning sent to admin: {adminEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send license expiry warning to {adminEmail}");
                throw;
            }
        }
        // Send credit low balance alert to admin
        public async Task SendCreditLowBalanceAlertAsync(string adminEmail, int remainingCredits, int threshold)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@declarify.local";
                var fromName = _configuration["Email:FromName"] ?? "Declarify - Compliance & Disclosure Hub";

                var subject = $"Credit Balance Alert - Only {remainingCredits} Credits Remaining";

                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .alert-box {{ 
            background-color: #f8d7da; 
            border-left: 4px solid #dc3545; 
            padding: 15px; 
            margin: 15px 0; 
        }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🔔 Low Credit Balance Alert</h1>
        </div>
        <div class=""content"">
            <p>Dear Administrator,</p>
            
            <div class=""alert-box"">
                <p><strong>Your credit balance is running low.</strong></p>
                <p>Current Balance: <strong>{remainingCredits} credits</strong></p>
            </div>
            
            <p>Credits are consumed when:</p>
            <ul>
                <li>Employees submit DOI forms (1 credit per submission)</li>
                <li>CIPC verification checks are performed (5 credits each)</li>
                <li>Credit worthiness checks are performed (10 credits each)</li>
            </ul>
            
            <p><strong>Important:</strong> When credits are exhausted, employees will not be able to submit their DOI forms, and you will not be able to perform verification checks.</p>
            
            <p><strong>Action Required:</strong></p>
            <ul>
                <li>Contact your vendor to purchase additional credits</li>
                <li>Credits are loaded through the central hub application</li>
                <li>Allow 1-2 hours for credit synchronization after purchase</li>
            </ul>
            
            <p>We recommend maintaining a buffer of at least 50 credits to avoid service disruption during peak submission periods.</p>
        </div>
        <div class=""footer"">
            <p>This is an automated notification from the Declarify Compliance & Disclosure Hub.</p>
        </div>
    </div>
</body>
</html>
";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                    Priority = remainingCredits <= 10 ? MailPriority.High : MailPriority.Normal
                };
                mailMessage.To.Add(adminEmail);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Credit low balance alert sent to admin: {adminEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send credit alert to {adminEmail}");
                throw;
            }
        }
    }
}


