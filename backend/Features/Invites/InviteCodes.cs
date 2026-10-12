using System.Security.Cryptography;
using System.Text;
using backend.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace backend.Features.Invites;

/// <summary>
/// Makes and redeems invite codes. With "Registration:InviteOnly" on (the default), an account can only be
/// created with an unused code from an admin.
/// </summary>
public class InviteCodes
{
    private readonly ReelrContext _context;

    public InviteCodes(ReelrContext context, IConfiguration configuration)
    {
        _context = context;
        Required = configuration.GetValue("Registration:InviteOnly", true);
    }

    public bool Required { get; }

    // Registration claims the invite and creates the account in one transaction.
    public Task<IDbContextTransaction> BeginTransactionAsync() => _context.Database.BeginTransactionAsync();

    /// <summary>Stores a new invite and returns it with its code, which is not stored and cannot be shown again.</summary>
    public async Task<(Invite Invite, string Code)> CreateAsync(string? note)
    {
        // 15 random bytes: 20 URL-safe characters, short enough to type, far too many to guess.
        var code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(15));
        var invite = new Invite
        {
            CodeHash = Hash(code),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Invites.Add(invite);
        await _context.SaveChangesAsync();
        return (invite, code);
    }

    /// <summary>
    /// Marks the invite with this code as used, if it is unused and not revoked, and returns its id; null otherwise.
    /// Run it in the transaction that creates the account, so a failed registration gives the code back.
    /// Two registrations racing for one code cannot both claim it: the update only matches an unused invite.
    /// </summary>
    public async Task<int?> ClaimAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var hash = Hash(code.Trim());
        var id = await _context.Invites
            .Where(i => i.CodeHash == hash)
            .Select(i => (int?)i.Id)
            .SingleOrDefaultAsync();

        if (id == null)
        {
            return null;
        }

        var claimed = await _context.Invites
            .Where(i => i.Id == id && i.UsedAt == null && i.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.UsedAt, DateTime.UtcNow));

        return claimed == 1 ? id : null;
    }

    public Task SetUserAsync(int inviteId, int userId) =>
        _context.Invites
            .Where(i => i.Id == inviteId)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.UsedById, userId));

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}
