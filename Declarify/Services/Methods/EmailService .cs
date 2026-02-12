using Declarify.Services.Email;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Declarify.Services.Methods
{
    public sealed class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly EmailOptions _opt;

        public EmailService(ILogger<EmailService> logger, IOptions<EmailOptions> opt)
        {
            _logger = logger;
            _opt = opt.Value;
        }

        // ============================================================
        // Core send helper (TestMode + BCC + config-correct SMTP)
        // ============================================================
        private async Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            MailPriority priority = MailPriority.Normal,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opt.Host))
                throw new InvalidOperationException("Email:Host is not configured.");

            if (string.IsNullOrWhiteSpace(_opt.FromAddress))
                throw new InvalidOperationException("Email:FromAddress is not configured.");

            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email is required.", nameof(toEmail));

            var actualTo = toEmail.Trim();
            var testBanner = "";

            if (_opt.TestMode)
            {
                if (string.IsNullOrWhiteSpace(_opt.TestToAddress))
                    throw new InvalidOperationException("Email:TestMode is true but Email:TestToAddress is missing.");

                testBanner =
                    $"<div style='padding:10px;border:1px solid #f59e0b;background:#fff7ed;color:#7c2d12;margin-bottom:12px;'>" +
                    $"<strong>TEST MODE:</strong> Original recipient: {WebUtility.HtmlEncode(actualTo)}" +
                    $"</div>";

                actualTo = _opt.TestToAddress.Trim();
            }

            using var msg = new MailMessage
            {
                From = new MailAddress(_opt.FromAddress, "Declarify"),
                Subject = subject ?? "",
                Body = testBanner + (htmlBody ?? ""),
                IsBodyHtml = true,
                Priority = priority
            };

            msg.To.Add(actualTo);

            // ✅ NO BCC in TestMode
            if (!_opt.TestMode)
            {
                AddBcc(msg, _opt.DefaultBcc);
                AddBcc(msg, _opt.BccAddress);
            }


            using var smtp = new SmtpClient(_opt.Host, _opt.Port)
            {
                EnableSsl = _opt.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            // Only apply credentials if provided
            if (!string.IsNullOrWhiteSpace(_opt.Username))
            {
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(_opt.Username, _opt.Password ?? "");
            }
            else
            {
                smtp.UseDefaultCredentials = true;
            }

            ct.ThrowIfCancellationRequested();
            await smtp.SendMailAsync(msg);
        }

        private static void AddBcc(MailMessage msg, string? bcc)
        {
            if (string.IsNullOrWhiteSpace(bcc)) return;

            foreach (var email in bcc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(email))
                    msg.Bcc.Add(email);
            }
        }

        // ============================================================
        // Existing interface methods (kept as-is)
        // ============================================================

        public async Task SendMagicLinkAsync(string email, string uniqueLink, string employeeName)
        {
            try
            {
                var subject = "Action Required: Complete Your Declaration of Interest";

                var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; line-height:1.6; color:#333;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:#081B38;color:#fff;padding:18px;text-align:center;border-radius:10px 10px 0 0;'>
      <h2 style='margin:0;'>Declaration of Interest Required</h2>
    </div>
    <div style='background:#f9f9f9;padding:20px;border-radius:0 0 10px 10px;'>
      <p>Dear {WebUtility.HtmlEncode(employeeName)},</p>

      <p>You are required to complete your annual Declaration of Interest (DOI) form.</p>

      <div style='background:#fff3cd;padding:12px;border-left:4px solid #00C2CB;margin:16px 0;'>
        <strong>Important:</strong> Please complete this declaration by the due date to remain compliant.
      </div>

      <p style='text-align:center;margin:22px 0;'>
        <a href='{uniqueLink}'
           style='display:inline-block;padding:12px 22px;background:#00C2CB;color:#081B38;text-decoration:none;border-radius:8px;font-weight:700;'>
           Complete My Declaration
        </a>
      </p>

      <p style='font-size:12px;color:#666;'>If the button doesn’t work, copy and paste this link:<br/>{uniqueLink}</p>

      <p>Thank you.</p>
      <p style='font-size:12px;color:#666;'>This is an automated message. Please do not reply.</p>
    </div>
  </div>
</body>
</html>";

                await SendAsync(email, subject, body, MailPriority.Normal);
                _logger.LogInformation("Magic link email queued to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send magic link email to {Email}", email);
                throw;
            }
        }

        public async Task SendReminderAsync(string email, string employeeName, DateTime dueDate)
        {
            try
            {
                var daysUntilDue = (dueDate.Date - DateTime.UtcNow.Date).Days;
                var urgent = daysUntilDue <= 0;

                var subject = urgent
                    ? "URGENT: Declaration of Interest Overdue"
                    : $"REMINDER: Declaration of Interest Due in {daysUntilDue} Days";

                var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; line-height:1.6; color:#333;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:{(urgent ? "#dc3545" : "#081B38")};color:#fff;padding:18px;text-align:center;border-radius:10px 10px 0 0;'>
      <h2 style='margin:0;'>{(urgent ? "Overdue Declaration" : "Declaration Reminder")}</h2>
    </div>
    <div style='background:#f9f9f9;padding:20px;border-radius:0 0 10px 10px;'>
      <p>Dear {WebUtility.HtmlEncode(employeeName)},</p>
      <p>{(urgent ? "<strong>Your declaration is overdue.</strong> Please complete it immediately." : $"Your declaration is due on <strong>{dueDate:dd MMM yyyy}</strong>.")}</p>
      <p style='font-size:12px;color:#666;'>This is an automated reminder. Please do not reply.</p>
    </div>
  </div>
</body>
</html>";

                await SendAsync(email, subject, body, urgent ? MailPriority.High : MailPriority.Normal);
                _logger.LogInformation("Reminder email queued to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder email to {Email}", email);
                throw;
            }
        }

        public async Task SendBulkCompleteNotificationAsync(string adminEmail, int totalSent)
        {
            try
            {
                var subject = $"Bulk DOI Request Completed - {totalSent} Employees Notified";

                var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; line-height:1.6; color:#333;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:#081B38;color:#fff;padding:18px;text-align:center;border-radius:10px 10px 0 0;'>
      <h2 style='margin:0;'>Bulk DOI Request Completed</h2>
    </div>
    <div style='background:#f9f9f9;padding:20px;border-radius:0 0 10px 10px;'>
      <p><strong>Total Employees Notified:</strong> {totalSent}</p>
      <p><strong>Timestamp:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
    </div>
  </div>
</body>
</html>";

                await SendAsync(adminEmail, subject, body, MailPriority.Normal);
                _logger.LogInformation("Bulk completion email queued to {Email}", adminEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send bulk completion notification to {Email}", adminEmail);
                throw;
            }
        }

        public async Task SendLicenseExpiryWarningAsync(string adminEmail, DateTime expiryDate, int daysRemaining)
        {
            try
            {
                var subject = $"License Expiry Warning - {daysRemaining} Days Remaining";

                var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; line-height:1.6; color:#333;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:#ffc107;color:#000;padding:18px;text-align:center;border-radius:10px 10px 0 0;'>
      <h2 style='margin:0;'>License Expiry Warning</h2>
    </div>
    <div style='background:#f9f9f9;padding:20px;border-radius:0 0 10px 10px;'>
      <p>Expiry Date: <strong>{expiryDate:dd MMM yyyy}</strong></p>
      <p>Days Remaining: <strong>{daysRemaining}</strong></p>
    </div>
  </div>
</body>
</html>";

                await SendAsync(adminEmail, subject, body, daysRemaining <= 7 ? MailPriority.High : MailPriority.Normal);
                _logger.LogInformation("License expiry warning queued to {Email}", adminEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send license expiry warning to {Email}", adminEmail);
                throw;
            }
        }

        public async Task SendCreditLowBalanceAlertAsync(string adminEmail, int remainingCredits, int threshold)
        {
            try
            {
                var subject = $"Credit Balance Alert - Only {remainingCredits} Credits Remaining";

                var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; line-height:1.6; color:#333;'>
  <div style='max-width:600px;margin:0 auto;padding:20px;'>
    <div style='background:#dc3545;color:#fff;padding:18px;text-align:center;border-radius:10px 10px 0 0;'>
      <h2 style='margin:0;'>Low Credit Balance Alert</h2>
    </div>
    <div style='background:#f9f9f9;padding:20px;border-radius:0 0 10px 10px;'>
      <p>Remaining Credits: <strong>{remainingCredits}</strong></p>
      <p>Threshold: <strong>{threshold}</strong></p>
    </div>
  </div>
</body>
</html>";

                await SendAsync(adminEmail, subject, body, remainingCredits <= 10 ? MailPriority.High : MailPriority.Normal);
                _logger.LogInformation("Credit alert queued to {Email}", adminEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send credit low balance alert to {Email}", adminEmail);
                throw;
            }
        }
    }
}
