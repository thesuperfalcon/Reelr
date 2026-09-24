using System.Net;
using System.Net.Http.Json;
using backend.Features.Movies.DTOs;
using backend.Tests.Infrastructure;

namespace backend.Tests.Companies;

public class CompanyControllerTests : IClassFixture<ReelrApiFactory>
{
    private readonly ReelrApiFactory _factory;

    public CompanyControllerTests(ReelrApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchCompanies_ReturnsTmdbResults()
    {
        _factory.Tmdb.SetResponse("search/company", new TmdbCompanySearchResultDto
        {
            Page = 1,
            Results = [new() { Id = 174, Name = "Warner Bros. Pictures", LogoPath = "/wb.png" }]
        });

        var result = await _factory.CreateClient().GetFromJsonAsync<TmdbCompanySearchResultDto>("/api/company/search?query=warner");

        var company = Assert.Single(result!.Results);
        Assert.Equal(174, company.Id);
        Assert.Equal("Warner Bros. Pictures", company.Name);
        Assert.Equal("/wb.png", company.LogoPath);
        Assert.Contains("query=warner", _factory.Tmdb.RequestsTo("search/company").Last().Query);
    }

    [Theory]
    [InlineData("/api/company/search")]
    [InlineData("/api/company/search?query=")]
    [InlineData("/api/company/search?query=%20")]
    public async Task SearchCompanies_WithoutQuery_Returns400(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
