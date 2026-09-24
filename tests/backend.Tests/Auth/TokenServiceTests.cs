using System.IdentityModel.Tokens.Jwt;
using backend.Features.Auth;
using backend.Features.Users;
using Microsoft.Extensions.Configuration;

namespace backend.Tests.Auth;

public class TokenServiceTests
{
    private static TokenService CreateService(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Jwt:Key"] = "test-signing-key-that-is-long-enough-for-hmac-sha256",
        ["Jwt:Issuer"] = "Reelr",
        ["Jwt:Audience"] = "Reelr",
        ["Jwt:ExpiryMinutes"] = "30"
    };

    private static readonly User TestUser = new() { Id = 42, UserName = "neo", Email = "neo@test.local" };

    [Fact]
    public void CreateToken_ContainsUserClaims()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService(ValidSettings()).CreateToken(TestUser));

        Assert.Equal("42", token.Subject);
        Assert.Equal("neo@test.local", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(token.Claims, c => c.Value == "neo");
        Assert.Equal("Reelr", token.Issuer);
        Assert.Equal(["Reelr"], token.Audiences);
    }

    [Fact]
    public void CreateToken_UsesConfiguredExpiry()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService(ValidSettings()).CreateToken(TestUser));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void CreateToken_DefaultsTo60MinutesExpiry()
    {
        var settings = ValidSettings();
        settings.Remove("Jwt:ExpiryMinutes");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(CreateService(settings).CreateToken(TestUser));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void CreateToken_WithoutKey_Throws()
    {
        var settings = ValidSettings();
        settings.Remove("Jwt:Key");

        Assert.Throws<InvalidOperationException>(() => CreateService(settings).CreateToken(TestUser));
    }
}
