using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Features.Users.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Settings;

public class SettingsControllerTests : IClassFixture<ReelrApiFactory>
{
    // The smallest files each format is recognised by: its signature, padded out.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];
    private static readonly byte[] Webp = [.. "RIFF"u8, 4, 0, 0, 0, .. "WEBP"u8];

    private readonly ReelrApiFactory _factory;

    public SettingsControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    private static Task<HttpResponseMessage> UploadAsync(TestUser user, byte[] data, string contentType = "image/png")
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", "picture" } };
        return user.Client.PutAsync("/api/settings/avatar", form);
    }

    private static async Task<UserSettingsDto> UploadOkAsync(TestUser user, byte[] data)
    {
        var response = await UploadAsync(user, data);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserSettingsDto>())!;
    }

    private static async Task<UserSettingsDto> GetSettingsAsync(TestUser user) =>
        (await user.Client.GetFromJsonAsync<UserSettingsDto>("/api/settings"))!;

    // ---- Settings ----

    [Fact]
    public async Task GetSettings_ReturnsDefaults()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var settings = await GetSettingsAsync(user);

        Assert.Equal(user.Username, settings.UserName);
        Assert.Equal($"{user.Username}@test.local", settings.Email);
        Assert.Null(settings.ProfileImageUrl);
        Assert.Equal(WatchlistVisibility.Followers, settings.WatchlistVisibility);
        Assert.True(settings.ShowFriendReviews);
    }

    [Fact]
    public async Task Settings_WithoutToken_Return401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/settings/avatar")).StatusCode);
    }

    [Fact]
    public async Task UpdateSettings_ChangesOnlyTheFieldsSent()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { ShowFriendReviews = false });
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<UpdatedSettingsDto>())!;

        Assert.False(updated.ShowFriendReviews);
        Assert.Equal(WatchlistVisibility.Followers, updated.WatchlistVisibility);
        Assert.Null(updated.Token);

        (await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { WatchlistVisibility = WatchlistVisibility.Public }))
            .EnsureSuccessStatusCode();

        var settings = await GetSettingsAsync(user);
        Assert.False(settings.ShowFriendReviews);
        // Public is the enum's default value, so this also checks that it is really saved.
        Assert.Equal(WatchlistVisibility.Public, settings.WatchlistVisibility);
    }

    [Fact]
    public async Task UpdateSettings_Rename_ReturnsANewTokenWithTheNewName()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var newName = $"renamed_{Guid.NewGuid():N}"[..20];

        var response = await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { UserName = newName });
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<UpdatedSettingsDto>())!;

        Assert.Equal(newName, updated.UserName);
        Assert.False(string.IsNullOrEmpty(updated.Token));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", updated.Token);
        Assert.Equal(newName, (await client.GetFromJsonAsync<UserSettingsDto>("/api/settings"))!.UserName);
    }

    [Fact]
    public async Task UpdateSettings_TakenName_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { UserName = other.Username });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(user.Username, (await GetSettingsAsync(user)).UserName);
    }

    [Fact]
    public async Task UpdateSettings_UnknownVisibility_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PatchAsJsonAsync("/api/settings", new { WatchlistVisibility = "Everyone" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Password ----

    [Fact]
    public async Task ChangePassword_NeedsTheCurrentPassword_ThenOnlyTheNewOneLogsIn()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        const string newPassword = "N3w-Passw0rd!";

        var wrong = await user.Client.PostAsJsonAsync("/api/settings/password",
            new ChangePasswordDto { CurrentPassword = "not-it", NewPassword = newPassword });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var changed = await user.Client.PostAsJsonAsync("/api/settings/password",
            new ChangePasswordDto { CurrentPassword = TestUsers.Password, NewPassword = newPassword });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var client = _factory.CreateClient();
        var oldLogin = await client.PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = TestUsers.Password });
        var newLogin = await client.PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = newPassword });
        Assert.False(oldLogin.IsSuccessStatusCode);
        Assert.True(newLogin.IsSuccessStatusCode);
    }

    // ---- Profile picture ----

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public async Task UploadAvatar_StoresThePicture_AndServesItPublicly(string format, string expectedType)
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var data = format switch { "png" => Png, "jpeg" => Jpeg, _ => Webp };

        // The type the client claims is ignored; the bytes decide.
        var response = await UploadAsync(user, data, "application/octet-stream");
        response.EnsureSuccessStatusCode();
        var settings = (await response.Content.ReadFromJsonAsync<UserSettingsDto>())!;

        Assert.StartsWith($"/api/users/{user.Id}/avatar?v=", settings.ProfileImageUrl);
        var profile = await _factory.CreateClient().GetFromJsonAsync<UserProfileDto>($"/api/users/{user.Id}");
        Assert.Equal(settings.ProfileImageUrl, profile!.ProfileImageUrl);

        var image = await _factory.CreateClient().GetAsync(settings.ProfileImageUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(expectedType, image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(data, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadAvatar_Again_ChangesTheUrl()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var first = await UploadOkAsync(user, Png);
        await Task.Delay(10);
        var second = await UploadOkAsync(user, Jpeg);

        Assert.NotEqual(first.ProfileImageUrl, second.ProfileImageUrl);
        var image = await _factory.CreateClient().GetAsync(second.ProfileImageUrl);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UploadAvatar_NotAnImage_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await UploadAsync(user, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray(), "image/svg+xml");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await GetSettingsAsync(user)).ProfileImageUrl);
    }

    [Fact]
    public async Task UploadAvatar_TooLarge_IsRejected()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var data = new byte[2 * 1024 * 1024];
        Png.CopyTo(data, 0);

        var response = await UploadAsync(user, data);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Null((await GetSettingsAsync(user)).ProfileImageUrl);
    }

    [Fact]
    public async Task RemoveAvatar_ClearsThePicture()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var uploaded = await UploadOkAsync(user, Png);

        (await user.Client.DeleteAsync("/api/settings/avatar")).EnsureSuccessStatusCode();

        Assert.Null((await GetSettingsAsync(user)).ProfileImageUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(uploaded.ProfileImageUrl)).StatusCode);
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesThePicture()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var uploaded = await UploadOkAsync(user, Png);

        (await user.Client.DeleteAsync($"/api/users/{user.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(uploaded.ProfileImageUrl)).StatusCode);
    }
}
