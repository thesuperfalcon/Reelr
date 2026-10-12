using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Features.Users;
using Microsoft.IdentityModel.Tokens;

namespace backend.Features.Auth;

public class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            // No email: the page keeps the token in memory, where any script on the page could read it.
            // The short standard name, which the page reads to show who is logged in. ClaimTypes.Name would be
            // written as its long URI.
            new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
            // Checked on every request, so changing the stamp revokes the token (see SecurityStamps).
            new(SecurityStamps.ClaimType, user.SecurityStamp ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key not configured.")));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Short, since it cannot be taken back before it runs out; the refresh cookie gets the next one.
        var expiryMinutes = _configuration.GetValue("Jwt:ExpiryMinutes", 15);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
