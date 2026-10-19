using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Auth;

public class AuthControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueName() => $"user_{Guid.NewGuid():N}"[..20];

    private Task<HttpResponseMessage> PostRegisterAsync(string username, string email, string password = TestUsers.Password) =>
        _client.PostAsJsonAsync("/auth/register", new { Username = username, Email = email, Password = password, AcceptTerms = true });

    // Registers and confirms the email, so the account can log in.
    private async Task<HttpResponseMessage> RegisterAsync(string username, string email, string password = TestUsers.Password)
    {
        var response = await PostRegisterAsync(username, email, password);

        if (response.IsSuccessStatusCode)
        {
            await _factory.ConfirmEmailAsync(username);
        }

        return response;
    }

    private Task<HttpResponseMessage> LoginAsync(string userInput, string password = TestUsers.Password) =>
        _client.PostAsJsonAsync("/auth/login", new { UserInput = userInput, Password = password });

    // ---- Register ----

    [Fact]
    public async Task Register_ValidUser_SendsConfirmationEmail_AndCannotLogInUntilConfirmed()
    {
        var username = UniqueName();
        var email = $"{username}@test.local";

        var response = await PostRegisterAsync(username, email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = await _factory.Emails.WaitForAsync(email, "Confirm your email");
        Assert.Contains("http://reelr.test/confirm-email#userId=", message.Body);

        var beforeConfirming = await LoginAsync(username);
        Assert.Equal(HttpStatusCode.Forbidden, beforeConfirming.StatusCode);

        var (userId, token) = FakeEmailSender.ReadLink(message);
        var confirmed = await _client.PostAsJsonAsync("/auth/confirm-email", new { UserId = userId, Token = token });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(username)).StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WrongToken_Returns400()
    {
        var username = UniqueName();
        await PostRegisterAsync(username, $"{username}@test.local");
        var (userId, _) = FakeEmailSender.ReadLink(await _factory.Emails.WaitForAsync($"{username}@test.local", "Confirm"));

        var response = await _client.PostAsJsonAsync("/auth/confirm-email", new { UserId = userId, Token = "forged" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutAcceptingTerms_Returns400_AndStoresTheTimeWhenAccepted()
    {
        var refused = UniqueName();
        var accepted = UniqueName();

        var response = await _client.PostAsJsonAsync("/auth/register",
            new { Username = refused, Email = $"{refused}@test.local", TestUsers.Password, AcceptTerms = false });
        await PostRegisterAsync(accepted, $"{accepted}@test.local");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await _factory.WithContextAsync(c => c.Users.AnyAsync(u => u.UserName == refused)));
        var acceptedAt = await _factory.WithContextAsync(c => c.Users.Where(u => u.UserName == accepted).Select(u => u.TermsAcceptedAt).SingleAsync());
        Assert.NotNull(acceptedAt);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        var response = await PostRegisterAsync(username, $"other_{username}@test.local");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_TakenEmail_AnswersLikeANewAccount_AndTellsTheOwnerByEmail()
    {
        var email = $"{UniqueName()}@test.local";
        await RegisterAsync(UniqueName(), email);
        var newcomer = UniqueName();

        var response = await PostRegisterAsync(newcomer, email);

        // Same status and body as a new account, so the form does not reveal that the address is registered.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain("taken", await response.Content.ReadAsStringAsync());
        await _factory.Emails.WaitForAsync(email, "already have a Reelr account");
        Assert.False(await _factory.WithContextAsync(c => c.Users.AnyAsync(u => u.UserName == newcomer)));
    }

    [Fact]
    public async Task Register_TakenEmailAndWeakPassword_GetsThePasswordError()
    {
        // The password is checked first, so this answers the same as for a free address.
        var email = $"{UniqueName()}@test.local";
        await RegisterAsync(UniqueName(), email);

        var response = await PostRegisterAsync(UniqueName(), email, "alllowercase1!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("taken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResendConfirmation_SendsANewLink_AndAnswersTheSameForUnknownAddresses()
    {
        var username = UniqueName();
        var email = $"{username}@test.local";
        await PostRegisterAsync(username, email);
        await _factory.Emails.WaitForAsync(email, "Confirm");

        var known = await _client.PostAsJsonAsync("/auth/resend-confirmation", new { Email = email });
        var unknown = await _client.PostAsJsonAsync("/auth/resend-confirmation", new { Email = $"{UniqueName()}@test.local" });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        await WaitForCountAsync(email, "Confirm", 2);
    }

    // ---- Password reset ----

    [Fact]
    public async Task ForgotPassword_ThenReset_ChangesThePassword_AndRevokesOldTokens()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var email = $"{user.Username}@test.local";
        var newPassword = TestUsers.Password + "-reset";

        var forgot = await _client.PostAsJsonAsync("/auth/forgot-password", new { Email = email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        var message = await _factory.Emails.WaitForAsync(email, "Reset your Reelr password");
        Assert.Contains("http://reelr.test/reset-password#userId=", message.Body);
        var (userId, token) = FakeEmailSender.ReadLink(message);

        var reset = await _client.PostAsJsonAsync("/auth/reset-password", new { UserId = userId, Token = token, NewPassword = newPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Username)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Username, newPassword)).StatusCode);
        // The token from before the reset no longer works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.Client.GetAsync("/api/settings")).StatusCode);

        // A link works once.
        var again = await _client.PostAsJsonAsync("/auth/reset-password", new { UserId = userId, Token = token, NewPassword = newPassword + "2" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithoutKeyVault_StoresTheKeyRingUnencrypted()
    {
        // The tests set no DataProtection:KeyVaultKeyUri, as in local development and Docker.
        var user = await _factory.CreateAuthenticatedAsync();
        var email = $"{user.Username}@test.local";

        await _client.PostAsJsonAsync("/auth/forgot-password", new { Email = email });
        await _factory.Emails.WaitForAsync(email, "Reset your Reelr password");

        var keys = await _factory.WithContextAsync(context => context.DataProtectionKeys.Select(k => k.Xml).ToListAsync());
        Assert.NotEmpty(keys);
        Assert.All(keys, xml => Assert.DoesNotContain("encryptedSecret", xml));
    }

    [Fact]
    public async Task ForgotPassword_UnknownAddress_AnswersTheSame_AndSendsNothing()
    {
        var email = $"{UniqueName()}@test.local";

        var response = await _client.PostAsJsonAsync("/auth/forgot-password", new { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Task.Delay(200);
        Assert.Empty(_factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task ResetPassword_ForgedToken_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await _client.PostAsJsonAsync("/auth/reset-password",
            new { UserId = user.Id, Token = "forged", NewPassword = TestUsers.Password + "-x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Username)).StatusCode);
    }

    [Fact]
    public async Task AccountEmails_ToOneAddress_AreCappedPerHour()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var email = $"{user.Username}@test.local";

        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/auth/forgot-password", new { Email = email });
        }

        // The confirmation email from signing up counts too: 1 confirmation and 2 of the 5 reset emails go out.
        await WaitForCountAsync(email, "Reset", 2);
        await Task.Delay(200);
        Assert.Equal(3, _factory.Emails.SentTo(email).Count());
    }

    private async Task WaitForCountAsync(string email, string subjectContains, int count)
    {
        for (var waited = 0; waited < 5000 && _factory.Emails.SentTo(email).Count(m => m.Subject.Contains(subjectContains)) < count; waited += 20)
        {
            await Task.Delay(20);
        }

        Assert.Equal(count, _factory.Emails.SentTo(email).Count(m => m.Subject.Contains(subjectContains)));
    }

    [Theory]
    [InlineData("ab", "valid@test.local", TestUsers.Password)]          // username too short
    [InlineData("validname", "not-an-email", TestUsers.Password)]       // invalid email
    [InlineData("validname", "valid@test.local", "Short1!")]            // password under 8 chars
    [InlineData("validname", "valid@test.local", "alllowercase1!")]     // Identity: no uppercase
    [InlineData("validname", "valid@test.local", "NoDigitsHere!")]      // Identity: no digit
    [InlineData("someone@test.local", "valid@test.local", TestUsers.Password)] // "@" would let a name look like an email
    public async Task Register_InvalidInput_Returns400(string username, string email, string password)
    {
        var response = await PostRegisterAsync(username, email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Login ----

    [Fact]
    public async Task Login_WithUsername_ReturnsToken()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        var response = await LoginAsync(username);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(body?.Token));
    }

    [Fact]
    public async Task Login_WithEmail_ReturnsToken()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        var response = await LoginAsync($"{username}@test.local");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_IgnoresSpacesAroundTheNameOrEmail()
    {
        // Phone keyboards add a space after an autocompleted word.
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync($"{username} ")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync($" {username}@test.local ")).StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        var response = await LoginAsync(username, "WrongPassw0rd!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var response = await LoginAsync(UniqueName());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterLockoutAttemptsWrongPasswords_LocksAccount()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        for (var i = 0; i < TestUsers.LockoutAttempts; i++)
        {
            await LoginAsync(username, "WrongPassw0rd!");
        }

        var response = await LoginAsync(username);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_SuccessResetsFailedAttempts()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        for (var i = 0; i < TestUsers.LockoutAttempts - 1; i++)
        {
            await LoginAsync(username, "WrongPassw0rd!");
        }
        await LoginAsync(username);
        await LoginAsync(username, "WrongPassw0rd!");

        var response = await LoginAsync(username);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Token ----

    [Fact]
    public async Task Token_FromLogin_GrantsAccessToProtectedEndpoint()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Token_Invalid_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await client.GetAsync("/api/lists");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record LoginResponse(string Token);
}
