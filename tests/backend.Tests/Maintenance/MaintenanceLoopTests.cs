using backend.Features.Maintenance;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace backend.Tests.Maintenance;

public class MaintenanceLoopTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public MaintenanceLoopTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private Task<int> TokenCountAsync(int userId) =>
        _factory.WithContextAsync(context => context.RefreshTokens.CountAsync(t => t.UserId == userId));

    [Fact]
    public async Task Run_DeletesExpiredTokensOfEveryUser_AndKeepsValidOnes()
    {
        // Each login leaves a refresh token. The inactive member's token is moved past its end date.
        var active = await _factory.CreateAuthenticatedAsync();
        var inactive = await _factory.CreateAuthenticatedAsync();
        await _factory.WithContextAsync(context => context.RefreshTokens
            .Where(t => t.UserId == inactive.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddDays(-1))));

        var loop = _factory.Services.GetServices<IHostedService>().OfType<MaintenanceLoop>().Single();
        await loop.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, await TokenCountAsync(inactive.Id));
        Assert.True(await TokenCountAsync(active.Id) > 0);
    }
}
