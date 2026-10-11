using System.Threading.Channels;

namespace backend.Features.Email;

// Emails wait here and EmailDispatcher sends them in the background. The request that asked for an email
// therefore answers at once whether or not one is sent, so its timing does not tell whether an account
// exists, and a slow or failing mail server does not slow down or fail the request.
// Queued emails are lost if the app stops before they are sent; the user can ask for the email again.
public class EmailQueue
{
    // Far more than account emails need. When full, new emails are dropped rather than holding up requests.
    private const int Capacity = 1000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
