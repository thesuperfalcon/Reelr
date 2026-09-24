using System.Net;
using System.Net.Http.Json;
using backend.Features.MovieLists.DTOs;
using backend.Features.Movies.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.MovieLists;

public class MovieListControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public MovieListControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    // Every test uses its own TMDB ids, since the Movies table is shared within the class.
    private int AddTmdbMovie(int tmdbId, string title)
    {
        _factory.Tmdb.AddMovie(new TmdbMovieDto
        {
            Id = tmdbId,
            Title = title,
            Overview = $"{title} overview",
            ReleaseDate = "1999-03-31",
            Runtime = 136,
            PosterPath = $"/{tmdbId}.jpg"
        });

        return tmdbId;
    }

    private static async Task<MovieListDto> CreateListAsync(HttpClient client, string name = "My list", bool isPublic = true)
    {
        var response = await client.PostAsJsonAsync("/api/lists", new CreateMovieListDto
        {
            Name = name,
            IsPublic = isPublic
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MovieListDto>())!;
    }

    private static async Task AddMovieAsync(HttpClient client, int listId, int tmdbId)
    {
        var response = await client.PostAsJsonAsync($"/api/lists/{listId}/movies", new AddMovieToListDto { TmdbId = tmdbId });
        response.EnsureSuccessStatusCode();
    }

    // ---- Create ----

    [Fact]
    public async Task CreateList_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/lists", new CreateMovieListDto { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateList_ReturnsCreatedListWithLocation()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto
        {
            Name = "Favorites",
            Description = "Best ones",
            IsPublic = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<MovieListDto>();
        Assert.NotNull(list);
        Assert.Equal("Favorites", list.Name);
        Assert.Equal("Best ones", list.Description);
        Assert.False(list.IsPublic);
        Assert.Equal(user.Id, list.UserId);
        Assert.Equal(user.Username, list.Username);
        Assert.Equal(0, list.MovieCount);
        Assert.Empty(list.TopMovies);
        Assert.EndsWith($"/api/lists/{list.Id}", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateList_WithoutName_Returns400(string? name)
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync("/api/lists", new { Name = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateList_WithTooLongName_Returns400()
    {
        var user = await _factory.CreateAuthenticatedAsync();

        var response = await user.Client.PostAsJsonAsync("/api/lists", new { Name = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Read ----

    [Fact]
    public async Task GetLists_ReturnsOnlyCurrentUsersLists()
    {
        var alice = await _factory.CreateAuthenticatedAsync();
        var bob = await _factory.CreateAuthenticatedAsync();
        await CreateListAsync(alice.Client, "Alice 1");
        await CreateListAsync(alice.Client, "Alice 2", isPublic: false);
        await CreateListAsync(bob.Client, "Bob 1");

        var lists = await alice.Client.GetFromJsonAsync<List<MovieListSummaryDto>>("/api/lists");

        Assert.NotNull(lists);
        Assert.Equal(["Alice 2", "Alice 1"], lists.Select(l => l.Name));
    }

    [Fact]
    public async Task GetList_PublicList_IsVisibleWithoutToken()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var created = await CreateListAsync(owner.Client, isPublic: true);

        var response = await _factory.CreateClient().GetAsync($"/api/lists/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetList_PrivateList_IsVisibleToOwnerOnly()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var created = await CreateListAsync(owner.Client, isPublic: false);

        var ownerResponse = await owner.Client.GetAsync($"/api/lists/{created.Id}");
        var otherResponse = await other.Client.GetAsync($"/api/lists/{created.Id}");
        var anonymousResponse = await _factory.CreateClient().GetAsync($"/api/lists/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task GetListMovies_PrivateList_Returns404ForOtherUser()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var created = await CreateListAsync(owner.Client, isPublic: false);

        var response = await other.Client.GetAsync($"/api/lists/{created.Id}/movies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetList_UnknownId_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/lists/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetList_ReturnsMovieCountAndThreeNewestMovies()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        var tmdbIds = new[]
        {
            AddTmdbMovie(1001, "First"),
            AddTmdbMovie(1002, "Second"),
            AddTmdbMovie(1003, "Third"),
            AddTmdbMovie(1004, "Fourth")
        };
        foreach (var tmdbId in tmdbIds)
        {
            await AddMovieAsync(owner.Client, list.Id, tmdbId);
        }

        var result = await owner.Client.GetFromJsonAsync<MovieListDto>($"/api/lists/{list.Id}");

        Assert.NotNull(result);
        Assert.Equal(4, result.MovieCount);
        Assert.Equal(["Fourth", "Third", "Second"], result.TopMovies.Select(m => m.Title));
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateList_OnlyChangesProvidedFields()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var created = await owner.Client.PostAsJsonAsync("/api/lists", new CreateMovieListDto
        {
            Name = "Original",
            Description = "Keep me",
            IsPublic = true
        });
        var list = (await created.Content.ReadFromJsonAsync<MovieListDto>())!;

        var response = await owner.Client.PutAsJsonAsync($"/api/lists/{list.Id}", new UpdateMovieListDto { Name = "Renamed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<MovieListDto>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Keep me", updated.Description);
        Assert.True(updated.IsPublic);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateList_OtherUsersList_Returns404AndDoesNotChangeIt()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client, "Mine");

        var response = await other.Client.PutAsJsonAsync($"/api/lists/{list.Id}", new UpdateMovieListDto { Name = "Hijacked" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await owner.Client.GetFromJsonAsync<MovieListDto>($"/api/lists/{list.Id}");
        Assert.Equal("Mine", unchanged!.Name);
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteList_Owner_RemovesListAndItsItems()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        await AddMovieAsync(owner.Client, list.Id, AddTmdbMovie(2001, "Doomed"));

        var response = await owner.Client.DeleteAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var getResponse = await owner.Client.GetAsync($"/api/lists/{list.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteList_OtherUsersList_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);

        var response = await other.Client.DeleteAsync($"/api/lists/{list.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var getResponse = await owner.Client.GetAsync($"/api/lists/{list.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    // ---- Add movie ----

    [Fact]
    public async Task AddMovie_NewMovie_IsFetchedFromTmdbAndAddedToList()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        var tmdbId = AddTmdbMovie(3001, "The Matrix");

        var response = await owner.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new AddMovieToListDto { TmdbId = tmdbId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var movies = await owner.Client.GetFromJsonAsync<List<MovieListItemDto>>($"/api/lists/{list.Id}/movies");
        var movie = Assert.Single(movies!);
        Assert.Equal(tmdbId, movie.TmdbId);
        Assert.Equal("The Matrix", movie.Title);
        Assert.Equal("/3001.jpg", movie.PosterUrl);
    }

    [Fact]
    public async Task AddMovie_MovieAlreadyStored_DoesNotCallTmdbAgain()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var firstList = await CreateListAsync(owner.Client, "First");
        var secondList = await CreateListAsync(owner.Client, "Second");
        var tmdbId = AddTmdbMovie(3002, "Cached");

        await AddMovieAsync(owner.Client, firstList.Id, tmdbId);
        await AddMovieAsync(owner.Client, secondList.Id, tmdbId);

        Assert.Equal(1, _factory.Tmdb.RequestsForMovie(tmdbId));
    }

    [Fact]
    public async Task AddMovie_AlreadyInList_Returns409()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        var tmdbId = AddTmdbMovie(3003, "Twice");
        await AddMovieAsync(owner.Client, list.Id, tmdbId);

        var response = await owner.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new AddMovieToListDto { TmdbId = tmdbId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AddMovie_UnknownTmdbMovie_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);

        var response = await owner.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new AddMovieToListDto { TmdbId = 3999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddMovie_OtherUsersList_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        var tmdbId = AddTmdbMovie(3004, "Intruder");

        var response = await other.Client.PostAsJsonAsync($"/api/lists/{list.Id}/movies", new AddMovieToListDto { TmdbId = tmdbId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _factory.Tmdb.RequestsForMovie(tmdbId));
    }

    // ---- Remove movie ----

    [Fact]
    public async Task RemoveMovie_RemovesItFromList()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        var keepId = AddTmdbMovie(4001, "Keep");
        var removeId = AddTmdbMovie(4002, "Remove");
        await AddMovieAsync(owner.Client, list.Id, keepId);
        await AddMovieAsync(owner.Client, list.Id, removeId);

        var response = await owner.Client.DeleteAsync($"/api/lists/{list.Id}/movies/{removeId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var movies = await owner.Client.GetFromJsonAsync<List<MovieListItemDto>>($"/api/lists/{list.Id}/movies");
        Assert.Equal([keepId], movies!.Select(m => m.TmdbId));
    }

    [Fact]
    public async Task RemoveMovie_NotInList_Returns404()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);

        var response = await owner.Client.DeleteAsync($"/api/lists/{list.Id}/movies/4999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMovie_OtherUsersList_Returns404AndKeepsMovie()
    {
        var owner = await _factory.CreateAuthenticatedAsync();
        var other = await _factory.CreateAuthenticatedAsync();
        var list = await CreateListAsync(owner.Client);
        var tmdbId = AddTmdbMovie(4003, "Protected");
        await AddMovieAsync(owner.Client, list.Id, tmdbId);

        var response = await other.Client.DeleteAsync($"/api/lists/{list.Id}/movies/{tmdbId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var movies = await owner.Client.GetFromJsonAsync<List<MovieListItemDto>>($"/api/lists/{list.Id}/movies");
        Assert.Single(movies!);
    }
}
