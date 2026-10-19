using backend.Data;
using backend.Features.Auth;
using backend.Features.Invites.DTOs;
using backend.Features.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Invites
{
    // Invites for admins (see AdminAccess): make one per person, see which are used, take back unused ones.
    [ApiController]
    [Authorize(Policy = AdminAccess.Policy)]
    [Route("api/admin/invites")]
    public class InvitesController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly InviteCodes _inviteCodes;
        private readonly SecurityLog _securityLog;
        private readonly string _baseUrl;

        public InvitesController(ReelrContext context, InviteCodes inviteCodes, SecurityLog securityLog, IConfiguration configuration)
        {
            _context = context;
            _inviteCodes = inviteCodes;
            _securityLog = securityLog;
            _baseUrl = configuration["App:BaseUrl"]!.TrimEnd('/');
        }

        [HttpGet]
        [EndpointSummary("Get every invite, the newest first")]
        public async Task<ActionResult<List<InviteDto>>> GetInvites()
        {
            var invites = await _context.Invites
                .OrderByDescending(i => i.Id)
                .Select(i => new InviteDto
                {
                    Id = i.Id,
                    Note = i.Note,
                    CreatedAt = i.CreatedAt,
                    UsedAt = i.UsedAt,
                    UsedBy = i.UsedBy == null ? null : i.UsedBy.UserName,
                    RevokedAt = i.RevokedAt
                })
                .ToListAsync();

            return Ok(invites);
        }

        [HttpPost]
        [EndpointSummary("Make an invite. The response is the only time its code is shown")]
        public async Task<ActionResult<CreatedInviteDto>> CreateInvite(CreateInviteDto dto)
        {
            var (invite, code) = await _inviteCodes.CreateAsync(dto.Note);
            _securityLog.InviteCreated(User.GetUserId(), invite.Id);

            var created = new CreatedInviteDto
            {
                Id = invite.Id,
                Note = invite.Note,
                CreatedAt = invite.CreatedAt,
                Code = code,
                Link = $"{_baseUrl}/register?invite={Uri.EscapeDataString(code)}"
            };

            return Created($"/api/admin/invites/{invite.Id}", created);
        }

        [HttpDelete("{id:int}")]
        [EndpointSummary("Take back an unused invite, so its code no longer works")]
        public async Task<IActionResult> RevokeInvite(int id)
        {
            var revoked = await _context.Invites
                .Where(i => i.Id == id && i.UsedAt == null && i.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.RevokedAt, DateTime.UtcNow));

            if (revoked == 1)
            {
                _securityLog.InviteRevoked(User.GetUserId(), id);
                return NoContent();
            }

            var invite = await _context.Invites.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);

            return invite switch
            {
                null => NotFound(),
                { RevokedAt: not null } => NoContent(),
                _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "This invite has already been used.")
            };
        }
    }
}
