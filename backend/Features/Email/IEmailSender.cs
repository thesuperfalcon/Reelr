namespace backend.Features.Email;

// Sends one email. SmtpEmailSender is the real one; tests replace it with one that keeps the messages.
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
