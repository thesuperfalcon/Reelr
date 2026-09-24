using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Tests.Infrastructure;

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

    private Task<HttpResponseMessage> RegisterAsync(string username, string email, string password = TestUsers.Password) =>
        _client.PostAsJsonAsync("/auth/register", new { Username = username, Email = email, Password = password });

    private Task<HttpResponseMessage> LoginAsync(string userInput, string password = TestUsers.Password) =>
        _client.PostAsJsonAsync("/auth/login", new { UserInput = userInput, Password = password });

    // ---- Register ----

    [Fact]
    public async Task Register_ValidUser_ReturnsUser()
    {
        var username = UniqueName();

        var response = await RegisterAsync(username, $"{username}@test.local");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal(username, body.UserName);
        Assert.Equal($"{username}@test.local", body.Email);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns400()
    {
        var username = UniqueName();
        await RegisterAsync(username, $"{username}@test.local");

        var response = await RegisterAsync(username, $"other_{username}@test.local");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email = $"{UniqueName()}@test.local";
        await RegisterAsync(UniqueName(), email);

        var response = await RegisterAsync(UniqueName(), email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", "valid@test.local", TestUsers.Password)]          // username too short
    [InlineData("validname", "not-an-email", TestUsers.Password)]       // invalid email
    [InlineData("validname", "valid@test.local", "Short1!")]            // password under 8 chars
    [InlineData("validname", "valid@test.local", "alllowercase1!")]     // Identity: no uppercase
    [InlineData("validname", "valid@test.local", "NoDigitsHere!")]      // Identity: no digit
    public async Task Register_InvalidInput_Returns400(string username, string email, string password)
    {
        var response = await RegisterAsync(username, email, password);

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

    private record RegisterResponse(int Id, string UserName, string Email);

    private record LoginResponse(string Token);
}
