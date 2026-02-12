namespace Declarify.Services.Email
{
    public sealed class EmailOptions
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 25;
        public bool EnableSsl { get; set; } = false;

        public string FromAddress { get; set; } = "";

        // Optional BCCs
        public string? DefaultBcc { get; set; }
        public string? BccAddress { get; set; }

        // SMTP auth (optional for port 25 internal relay)
        public string? Username { get; set; }
        public string? Password { get; set; }

        // Testing
        public bool TestMode { get; set; } = false;
        public string? TestToAddress { get; set; }
    }
}
