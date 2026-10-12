using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Features.Users.DTOs;
using backend.Tests.Infrastructure;
using SkiaSharp;

namespace backend.Tests.Settings;

public class SettingsControllerTests : IClassFixture<ReelrApiFactory>
{
    private static readonly byte[] Png = TestImages.Create(SKEncodedImageFormat.Png);
    private static readonly byte[] Jpeg = TestImages.Create(SKEncodedImageFormat.Jpeg);
    private static readonly byte[] Webp = TestImages.Create(SKEncodedImageFormat.Webp);

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
        Assert.True(settings.ShowOwnActivity);
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
        Assert.True(updated.ShowOwnActivity);
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

    [Theory]
    [InlineData("ab")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 51 characters
    [InlineData("someone@test.local")]
    public async Task UpdateSettings_InvalidName_Returns400(string userName)
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PatchAsJsonAsync("/api/settings", new UpdateUserSettingsDto { UserName = userName });

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
        // Derived from the shared test password so no credential-like literals live in this file.
        var newPassword = TestUsers.Password + "-new";

        var wrong = await user.Client.PostAsJsonAsync("/api/settings/password",
            new ChangePasswordDto { CurrentPassword = TestUsers.Password + "-wrong", NewPassword = newPassword });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var changed = await user.Client.PostAsJsonAsync("/api/settings/password",
            new ChangePasswordDto { CurrentPassword = TestUsers.Password, NewPassword = newPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var client = _factory.CreateClient();
        var oldLogin = await client.PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = TestUsers.Password });
        var newLogin = await client.PostAsJsonAsync("/auth/login", new { UserInput = user.Username, Password = newPassword });
        Assert.False(oldLogin.IsSuccessStatusCode);
        Assert.True(newLogin.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ChangePassword_LockoutAttemptsWrongCurrentPasswords_LocksTheAccount()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var newPassword = TestUsers.Password + "-new";

        // Same lockout as login, so a stolen token cannot be used to guess the password here.
        for (var i = 0; i < TestUsers.LockoutAttempts; i++)
        {
            var wrong = await user.Client.PostAsJsonAsync("/api/settings/password",
                new ChangePasswordDto { CurrentPassword = TestUsers.Password + "-wrong", NewPassword = newPassword });
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        var right = await user.Client.PostAsJsonAsync("/api/settings/password",
            new ChangePasswordDto { CurrentPassword = TestUsers.Password, NewPassword = newPassword });
        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login",
            new { UserInput = user.Username, Password = TestUsers.Password });

        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // ---- Data export ----

    [Fact]
    public async Task Export_ContainsTheUsersOwnData_AsADownload()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var tmdbId = _factory.Tmdb.AddMovie(9401, "Exported film");
        (await user.Client.PostAsJsonAsync($"/api/movies/{tmdbId}/diary",
            new { Score = 4.5m, Review = "My exported review" })).EnsureSuccessStatusCode();
        (await user.Client.PostAsync($"/api/users/{other.Id}/follow", null)).EnsureSuccessStatusCode();

        var response = await user.Client.GetAsync("/api/settings/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(user.Username, json.GetProperty("account").GetProperty("userName").GetString());
        Assert.Equal($"{user.Username}@test.local", json.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal("Exported film", json.GetProperty("diary")[0].GetProperty("title").GetString());
        Assert.Equal(4.5m, json.GetProperty("ratings")[0].GetProperty("score").GetDecimal());
        Assert.Equal("My exported review", json.GetProperty("reviews")[0].GetProperty("text").GetString());
        Assert.Equal(other.Username, json.GetProperty("following")[0].GetString());
    }

    [Fact]
    public async Task Export_WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/settings/export")).StatusCode);
    }

    // ---- Profile picture ----

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public async Task UploadAvatar_StoresThePictureAsWebP_AndServesItPublicly(string format)
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
        Assert.Equal("image/webp", image.Content.Headers.ContentType!.MediaType);
        var stored = TestImages.Describe(await image.Content.ReadAsByteArrayAsync());
        Assert.Equal((40, 30, SKEncodedImageFormat.Webp), stored);
    }

    [Fact]
    public async Task UploadAvatar_DropsCameraMetadata()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        var upload = TestImages.JpegWithExif("GPS-59.3293N-18.0686E");
        Assert.Contains("GPS-59.3293N", System.Text.Encoding.ASCII.GetString(upload));

        var settings = await UploadOkAsync(user, upload);

        var stored = await (await _factory.CreateClient().GetAsync(settings.ProfileImageUrl)).Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.ASCII.GetString(stored);
        Assert.DoesNotContain("GPS-59.3293N", text);
        Assert.DoesNotContain("Exif", text);
    }

    [Fact]
    public async Task UploadAvatar_LargePicture_IsScaledDown()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var settings = await UploadOkAsync(user, TestImages.Create(SKEncodedImageFormat.Png, 1600, 800));

        var stored = await (await _factory.CreateClient().GetAsync(settings.ProfileImageUrl)).Content.ReadAsByteArrayAsync();
        Assert.Equal((512, 256, SKEncodedImageFormat.Webp), TestImages.Describe(stored));
    }

    [Fact]
    public async Task UploadAvatar_RightSignatureButNotAPicture_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();
        byte[] fake = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. "<script>alert(1)</script>"u8];

        var response = await UploadAsync(user, fake);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await GetSettingsAsync(user)).ProfileImageUrl);
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
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
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

        (await user.Client.DeleteAccountAsync(user.Id)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(uploaded.ProfileImageUrl)).StatusCode);
    }
}
