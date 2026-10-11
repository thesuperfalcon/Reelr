namespace backend.Features.Email;

// Sends the queued emails one at a time, trying each a few times before giving up.
public class EmailDispatcher : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private readonly EmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.ReadAllAsync(stoppingToken))
        {
            await SendWithRetriesAsync(message, stoppingToken);
        }
    }

    private async Task SendWithRetriesAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        // Only the domain is logged; the full address is personal data and the body holds a sign-in link.
        var domain = message.To[(message.To.IndexOf('@') + 1)..];

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _sender.SendAsync(message, stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                if (attempt == RetryDelays.Length)
                {
                    _logger.LogError(ex, "Gave up sending \"{Subject}\" to an address at {Domain}", message.Subject, domain);
                    return;
                }

                _logger.LogWarning(ex, "Sending \"{Subject}\" to an address at {Domain} failed; trying again", message.Subject, domain);
                await Task.Delay(RetryDelays[attempt], stoppingToken);
            }
        }
    }
}
