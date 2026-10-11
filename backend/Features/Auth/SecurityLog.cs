namespace backend.Features.Auth;

/// <summary>
/// Account and admin events, all under one log category (backend.Features.Auth.SecurityLog) so they can be
/// searched and alerted on together, for example many failed logins for one account. Lines carry user ids
/// only, never email addresses or passwords; the request's CorrelationId links them to the nginx log.
/// </summary>
public partial class SecurityLog
{
    private readonly ILogger<SecurityLog> _logger;

    public SecurityLog(ILogger<SecurityLog> logger)
    {
        _logger = logger;
    }

    [LoggerMessage(1000, LogLevel.Information, "Account {UserId} registered")]
    public partial void Registered(int userId);

    [LoggerMessage(1001, LogLevel.Information, "Registration with an email address that already has an account")]
    public partial void RegisteredWithTakenEmail();

    [LoggerMessage(1002, LogLevel.Information, "Account {UserId} confirmed its email address")]
    public partial void EmailConfirmed(int userId);

    [LoggerMessage(1010, LogLevel.Information, "Account {UserId} logged in")]
    public partial void LoginSucceeded(int userId);

    // Reason is one of the LoginFailure values. UserId is null when the name matches no account.
    [LoggerMessage(1011, LogLevel.Warning, "Login failed for account {UserId}: {Reason}")]
    public partial void LoginFailed(int? userId, string reason);

    [LoggerMessage(1020, LogLevel.Information, "Account {UserId} asked for a password reset")]
    public partial void PasswordResetRequested(int userId);

    [LoggerMessage(1021, LogLevel.Information, "Account {UserId} reset its password")]
    public partial void PasswordReset(int userId);

    [LoggerMessage(1022, LogLevel.Information, "Account {UserId} changed its password")]
    public partial void PasswordChanged(int userId);

    [LoggerMessage(1023, LogLevel.Warning, "Account {UserId} gave a wrong current password when changing it")]
    public partial void PasswordChangeRefused(int userId);

    [LoggerMessage(1030, LogLevel.Information, "Account {UserId} signed out everywhere")]
    public partial void SignedOutEverywhere(int userId);

    [LoggerMessage(1031, LogLevel.Information, "Account {UserId} changed its username")]
    public partial void UsernameChanged(int userId);

    [LoggerMessage(1032, LogLevel.Information, "Account {UserId} was deleted")]
    public partial void AccountDeleted(int userId);

    [LoggerMessage(1040, LogLevel.Information, "Admin {AdminId} resolved reports about {TargetType} {TargetId}: {Outcome}")]
    public partial void ReportsResolved(int adminId, string targetType, int targetId, string outcome);
}

public static class LoginFailure
{
    public const string UnknownAccount = "unknown account";
    public const string WrongPassword = "wrong password";
    public const string LockedOut = "locked out";
    public const string Throttled = "too many attempts";
    public const string EmailNotConfirmed = "email not confirmed";
}
