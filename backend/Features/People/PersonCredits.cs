using System.Text.RegularExpressions;
using backend.Features.People.DTOs;

namespace backend.Features.People;

// Turns TMDB's flat credit lists into one entry per film and department.
public static partial class PersonCredits
{
    public const string Acting = "Acting";
    public const string Appearances = "Appearances";
    private const int MaxKnownFor = 8;

    // Talk shows and documentaries list people as "Self", "Himself - Host" and so on.
    [GeneratedRegex(@"^(self|himself|herself|themselves|themself)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SelfRole();

    public static PersonDetailsDto ToDetails(TmdbPersonDetailsDto person)
    {
        var credits = Merge(person.MovieCredits ?? new TmdbPersonMovieCreditsDto());

        return new PersonDetailsDto
        {
            Id = person.Id,
            Name = person.Name,
            Biography = person.Biography,
            Birthday = person.Birthday,
            Deathday = person.Deathday,
            PlaceOfBirth = person.PlaceOfBirth,
            ProfilePath = person.ProfilePath,
            KnownForDepartment = person.KnownForDepartment,
            Homepage = person.Homepage,
            ImdbId = person.ExternalIds?.ImdbId,
            InstagramId = person.ExternalIds?.InstagramId,
            TwitterId = person.ExternalIds?.TwitterId,
            FacebookId = person.ExternalIds?.FacebookId,
            KnownFor = KnownFor(credits, person.KnownForDepartment),
            Credits = credits
        };
    }

    private static List<PersonCreditDto> Merge(TmdbPersonMovieCreditsDto credits)
    {
        var cast = credits.Cast.Select(c => (
            Movie: (TmdbPersonMovieCreditDto)c,
            Department: c.Character != null && SelfRole().IsMatch(c.Character) ? Appearances : Acting,
            Role: c.Character));

        var crew = credits.Crew.Select(c => (
            Movie: (TmdbPersonMovieCreditDto)c,
            Department: string.IsNullOrWhiteSpace(c.Department) ? "Crew" : c.Department,
            Role: c.Job));

        return cast.Concat(crew)
            .GroupBy(c => (c.Movie.Id, c.Department))
            .Select(group =>
            {
                var movie = group.First().Movie;
                return new PersonCreditDto
                {
                    TmdbId = movie.Id,
                    Title = movie.Title,
                    PosterPath = movie.PosterPath,
                    ReleaseDate = string.IsNullOrEmpty(movie.ReleaseDate) ? null : movie.ReleaseDate,
                    VoteAverage = movie.VoteAverage,
                    VoteCount = movie.VoteCount,
                    Popularity = movie.Popularity,
                    Department = group.Key.Department,
                    Roles = group
                        .Select(c => c.Role?.Trim())
                        .Where(role => !string.IsNullOrEmpty(role))
                        .Select(role => role!)
                        .Distinct()
                        .ToList()
                };
            })
            // ISO dates sort as text. Unreleased films without a date come first.
            .OrderBy(c => c.ReleaseDate == null ? 0 : 1)
            .ThenByDescending(c => c.ReleaseDate)
            .ThenBy(c => c.Title)
            .ToList();
    }

    private static List<PersonCreditDto> KnownFor(List<PersonCreditDto> credits, string? knownForDepartment) =>
        credits
            .Where(c => c.Department != Appearances)
            .OrderBy(c => c.Department == knownForDepartment ? 0 : 1)
            .ThenByDescending(c => c.VoteCount)
            .DistinctBy(c => c.TmdbId)
            .Take(MaxKnownFor)
            .ToList();
}
