using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;
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
