using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;
using backend.Features.People.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.People;

public class PersonControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public PersonControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchPeople_ReturnsTmdbResults()
    {
        _factory.Tmdb.SetResponse("search/person", new TmdbPersonSearchResultDto
        {
            Page = 1,
            Results = [new() { Id = 6384, Name = "Keanu Reeves", KnownForDepartment = "Acting", ProfilePath = "/keanu.jpg" }]
        });

        var result = await _factory.CreateClient().GetFromJsonAsync<TmdbPersonSearchResultDto>("/api/person/search?query=keanu%20reeves");

        var person = Assert.Single(result!.Results);
        Assert.Equal(6384, person.Id);
        Assert.Equal("Keanu Reeves", person.Name);
        Assert.Equal("/keanu.jpg", person.ProfilePath);
        Assert.Contains("query=keanu%20reeves", _factory.Tmdb.RequestsTo("search/person").Last().Query);
    }

    // ---- Details ----

    private static TmdbPersonDetailsDto Person(int id, params object[] credits) => new()
    {
        Id = id,
        Name = "Greta Gerwig",
        Biography = "Writer and director.",
        Birthday = "1983-08-04",
        PlaceOfBirth = "Sacramento, California, USA",
        ProfilePath = "/greta.jpg",
        KnownForDepartment = "Directing",
        MovieCredits = new TmdbPersonMovieCreditsDto
        {
            Cast = credits.OfType<TmdbPersonCastCreditDto>().ToList(),
            Crew = credits.OfType<TmdbPersonCrewCreditDto>().ToList()
        },
        ExternalIds = new TmdbExternalIdsDto { ImdbId = "nm1950086", InstagramId = "greta" }
    };

    private static TmdbPersonCastCreditDto Cast(int movieId, string character, string? date = "2010-01-01", int votes = 10) =>
        new() { Id = movieId, Title = $"Film {movieId}", ReleaseDate = date, VoteCount = votes, Character = character };

    private static TmdbPersonCrewCreditDto Crew(int movieId, string department, string job, string? date = "2010-01-01", int votes = 10) =>
        new() { Id = movieId, Title = $"Film {movieId}", ReleaseDate = date, VoteCount = votes, Department = department, Job = job };

    private async Task<PersonDetailsDto> GetPersonAsync(int personId) =>
        (await _factory.CreateClient().GetFromJsonAsync<PersonDetailsDto>($"/api/person/{personId}"))!;

    [Fact]
    public async Task GetPerson_ReturnsDetailsAndExternalIds()
    {
        _factory.Tmdb.SetResponse("person/101", Person(101));

        var person = await GetPersonAsync(101);

        Assert.Equal(101, person.Id);
        Assert.Equal("Greta Gerwig", person.Name);
        Assert.Equal("1983-08-04", person.Birthday);
        Assert.Equal("Directing", person.KnownForDepartment);
        Assert.Equal("nm1950086", person.ImdbId);
        Assert.Equal("greta", person.InstagramId);
        Assert.Empty(person.Credits);
        Assert.Contains("append_to_response=movie_credits,external_ids", _factory.Tmdb.RequestsTo("person/101").Last().Query);
    }

    [Fact]
    public async Task GetPerson_MergesRolesPerFilmAndDepartment()
    {
        _factory.Tmdb.SetResponse("person/102", Person(102,
            Crew(1, "Production", "Producer"),
            Crew(1, "Production", "Executive Producer"),
            Crew(1, "Directing", "Director"),
            Cast(1, "Jo"),
            Cast(1, "Narrator")));

        var credits = (await GetPersonAsync(102)).Credits;

        Assert.Equal(3, credits.Count);
        Assert.Equal(["Producer", "Executive Producer"], credits.Single(c => c.Department == "Production").Roles);
        Assert.Equal(["Director"], credits.Single(c => c.Department == "Directing").Roles);
        Assert.Equal(["Jo", "Narrator"], credits.Single(c => c.Department == "Acting").Roles);
    }

    [Fact]
    public async Task GetPerson_PutsRolesAsThemselvesInAppearances()
    {
        _factory.Tmdb.SetResponse("person/103", Person(103,
            Cast(1, "Self"),
            Cast(2, "Herself - Guest", votes: 9999),
            Cast(3, "Selfish Neighbour")));

        var person = await GetPersonAsync(103);

        Assert.Equal([1, 2], person.Credits.Where(c => c.Department == "Appearances").Select(c => c.TmdbId).Order());
        Assert.Equal("Acting", person.Credits.Single(c => c.TmdbId == 3).Department);
        Assert.Equal([3], person.KnownFor.Select(c => c.TmdbId));
    }

    [Fact]
    public async Task GetPerson_OrdersUnreleasedFirstThenNewest()
    {
        _factory.Tmdb.SetResponse("person/104", Person(104,
            Crew(1, "Directing", "Director", "2017-11-03"),
            Crew(2, "Directing", "Director", ""),
            Crew(3, "Directing", "Director", "2023-07-21"),
            Crew(4, "Directing", "Director", null)));

        var credits = (await GetPersonAsync(104)).Credits;

        Assert.Equal([2, 4, 3, 1], credits.Select(c => c.TmdbId));
        Assert.Null(credits[0].ReleaseDate);
    }

    [Fact]
    public async Task GetPerson_KnownForPrefersKnownForDepartmentAndListsEachFilmOnce()
    {
        _factory.Tmdb.SetResponse("person/105", Person(105,
            Cast(1, "Lead", votes: 5000),
            Crew(2, "Directing", "Director", votes: 300),
            Crew(3, "Directing", "Director", votes: 900),
            Crew(3, "Writing", "Screenplay", votes: 900)));

        var knownFor = (await GetPersonAsync(105)).KnownFor;

        Assert.Equal([3, 2, 1], knownFor.Select(c => c.TmdbId));
        Assert.Equal("Directing", knownFor[0].Department);
    }

    [Fact]
    public async Task GetPerson_UnknownPerson_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/person/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPerson_TmdbFailure_Returns502()
    {
        _factory.Tmdb.SetResponse("person/106", null, HttpStatusCode.InternalServerError);

        var response = await _factory.CreateClient().GetAsync("/api/person/106");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/person/search")]
    [InlineData("/api/person/search?query=")]
    [InlineData("/api/person/search?query=%20")]
    public async Task SearchPeople_WithoutQuery_Returns400(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
