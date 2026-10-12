using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using backend.Features.Email;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Keeps sent emails instead of sending them. Emails go out from a background queue, so tests wait for them.
/// </summary>
public partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IEnumerable<EmailMessage> SentTo(string address) =>
        _sent.Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The newest email to the address whose subject contains the given text, once it has been sent.
    /// </summary>
    public async Task<EmailMessage> WaitForAsync(string address, string subjectContains)
    {
        for (var waited = 0; waited < 5000; waited += 20)
        {
            var message = SentTo(address).LastOrDefault(m => m.Subject.Contains(subjectContains));

            if (message != null)
            {
                return message;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"No email \"{subjectContains}\" was sent to {address}.");
    }

    /// <summary>
    /// The user id and token from the link in an account email ("...#userId=1&amp;token=...").
    /// </summary>
    public static (int UserId, string Token) ReadLink(EmailMessage message)
    {
        var match = LinkPattern().Match(message.Body);
        Assert.True(match.Success, $"No link in: {message.Body}");
        return (int.Parse(match.Groups["id"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }

    [GeneratedRegex(@"#userId=(?<id>\d+)&token=(?<token>\S+)")]
    private static partial Regex LinkPattern();
}
