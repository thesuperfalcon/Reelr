using backend.Features.Auth;
using backend.Features.Users;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace backend.Tests.Auth;

public class TokenNameClaimTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public TokenNameClaimTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidatedToken_ExposesTheUsernameAsIdentityName()
    {
        using var scope = _factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<TokenService>()
            .CreateToken(new User { Id = 7, UserName = "trinity", SecurityStamp = "stamp" });
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // The same handler and parameters the API uses, without the security stamp check.
        var result = await options.TokenHandlers.First().ValidateTokenAsync(token, options.TokenValidationParameters);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("trinity", result.ClaimsIdentity.Name);
    }
}
