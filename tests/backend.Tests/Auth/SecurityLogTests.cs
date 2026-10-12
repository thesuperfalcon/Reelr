using System.Net.Http.Json;
using backend.Features.Auth;
using backend.Tests.Infrastructure;

namespace backend.Tests.Auth;

public class SecurityLogTests : IClassFixture<ReelrApiFactory>
{
    private const string Category = "backend.Features.Auth.SecurityLog";

    private readonly ReelrApiFactory _factory;

    public SecurityLogTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private List<TestLogs.LogEntry> EventsFor(int eventId, object? userId) =>
        _factory.Logs.Entries
            .Where(e => e.Category == Category && e.EventId.Id == eventId && Equals(e.State.GetValueOrDefault("UserId"), userId))
            .ToList();

    private Task<HttpResponseMessage> LoginAsync(string userInput, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/auth/login", new { UserInput = userInput, Password = password });

    [Fact]
    public async Task Login_SuccessAndFailuresAreLoggedByUserId()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        await LoginAsync(user.Username, "Wrong-passw0rd!");
        await LoginAsync(user.Username, TestUsers.Password);

        Assert.Equal(LoginFailure.WrongPassword, Assert.Single(EventsFor(1011, user.Id)).State["Reason"]);
        // One login when the test user was made, one here.
        Assert.Equal(2, EventsFor(1010, user.Id).Count);
    }

    [Fact]
    public async Task Login_UnknownAccount_IsLoggedWithoutTheName()
    {
        var name = $"nobody_{Guid.NewGuid():N}"[..20];

        await LoginAsync(name, "Some-passw0rd!");

        Assert.Contains(EventsFor(1011, null), e => Equals(e.State["Reason"], LoginFailure.UnknownAccount));
        Assert.DoesNotContain(_factory.Logs.Entries, e => e.Message.Contains(name));
    }

    [Fact]
    public async Task PasswordChangeAndAccountDeletion_AreLogged()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        await user.Client.PostAsJsonAsync("/api/settings/password", new { CurrentPassword = "Wrong-passw0rd!", NewPassword = "N3w-passw0rd!" });
        var changed = await user.Client.PostAsJsonAsync("/api/settings/password", new { CurrentPassword = TestUsers.Password, NewPassword = "N3w-passw0rd!" });
        var token = (await changed.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
        user.Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        (await user.Client.DeleteAccountAsync(user.Id, "N3w-passw0rd!")).EnsureSuccessStatusCode();

        Assert.Single(EventsFor(1023, user.Id));
        Assert.Single(EventsFor(1022, user.Id));
        Assert.Single(EventsFor(1032, user.Id));
    }

    [Fact]
    public async Task NoLogLine_ContainsAnEmailAddress()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        await LoginAsync($"{user.Username}@test.local", TestUsers.Password);
        await _factory.CreateClient().PostAsJsonAsync("/auth/forgot-password", new { Email = $"{user.Username}@test.local" });

        Assert.DoesNotContain(_factory.Logs.Entries, e => e.Category == Category && e.Message.Contains("@test.local"));
    }

    private record TokenResponse(string Token);
}
