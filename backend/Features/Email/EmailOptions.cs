using MailKit.Security;

namespace backend.Features.Email;

// Bound from the "Email" configuration section. Any SMTP server works: Mailpit locally, Brevo or similar in production.
public class EmailOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    // StartTls for a provider on port 587; None for Mailpit, which has no TLS.
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;

    // Leave empty for a server without login, such as Mailpit.
    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Reelr";
}
