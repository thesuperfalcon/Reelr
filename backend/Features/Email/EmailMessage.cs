namespace backend.Features.Email;

// A plain-text email. Account emails need no formatting, and plain text cannot hide a link behind other text.
public record EmailMessage(string To, string Subject, string Body);
