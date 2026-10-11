using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Infrastructure;

public record TestUser(int Id, string Username, HttpClient Client);

public static class TestUsers
{
    public const string Password = "Passw0rd!";

    // Wrong passwords in a row that lock an account (Lockout.MaxFailedAccessAttempts in Program.cs).
    public const int LockoutAttempts = 20;

    /// <summary>
    /// Registers a new user with a unique name, confirms the email, logs in and returns a client that sends the JWT.
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

        var id = await factory.ConfirmEmailAsync(username);

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            UserInput = username,
            Password
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return new TestUser(id, username, client);
    }

    /// <summary>
    /// Marks a registered user's email as confirmed, as the link in the confirmation email would, and returns the user's id.
    /// AuthControllerTests covers the real confirmation flow.
    /// </summary>
    public static Task<int> ConfirmEmailAsync(this ReelrApiFactory factory, string username) =>
        factory.WithContextAsync(async context =>
        {
            var user = await context.Users.SingleAsync(u => u.UserName == username);
            user.EmailConfirmed = true;
            await context.SaveChangesAsync();
            return user.Id;
        });

    /// <summary>
    /// Sends DELETE /api/users/{userId} with the password confirmation the endpoint requires.
    /// </summary>
    public static Task<HttpResponseMessage> DeleteAccountAsync(this HttpClient client, int userId, string password = Password) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{userId}")
        {
            Content = JsonContent.Create(new { Password = password })
        });

    private record LoginResponse(string Token);
}
