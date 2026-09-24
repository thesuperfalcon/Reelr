using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace backend.Tests.Infrastructure;

public record TestUser(int Id, string Username, HttpClient Client);

public static class TestUsers
{
    public const string Password = "Passw0rd!";

    /// <summary>
    /// Registers a new user with a unique name, logs in and returns a client that sends the JWT.
    /// </summary>
    public static async Task<TestUser> CreateAuthenticatedAsync(this ReelrApiFactory factory)
    {
        var username = $"user_{Guid.NewGuid():N}"[..20];
        var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password
        });
        registerResponse.EnsureSuccessStatusCode();
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            UserInput = username,
            Password
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return new TestUser(registered!.Id, username, client);
    }

    private record RegisterResponse(int Id, string UserName, string Email);

    private record LoginResponse(string Token);
}
