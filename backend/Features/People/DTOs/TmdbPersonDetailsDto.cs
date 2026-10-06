using System.Text.Json.Serialization;

namespace backend.Features.People.DTOs;

// Raw TMDB answer for person/{id}?append_to_response=movie_credits,external_ids.
public class TmdbPersonDetailsDto
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Biography { get; set; }

    public string? Birthday { get; set; }

    public string? Deathday { get; set; }

    [JsonPropertyName("place_of_birth")]
    public string? PlaceOfBirth { get; set; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; set; }

    [JsonPropertyName("known_for_department")]
    public string? KnownForDepartment { get; set; }

    public string? Homepage { get; set; }

    [JsonPropertyName("movie_credits")]
    public TmdbPersonMovieCreditsDto? MovieCredits { get; set; }

    [JsonPropertyName("external_ids")]
    public TmdbExternalIdsDto? ExternalIds { get; set; }
}

public class TmdbPersonMovieCreditsDto
{
    public List<TmdbPersonCastCreditDto> Cast { get; set; } = [];

    public List<TmdbPersonCrewCreditDto> Crew { get; set; } = [];
}

public class TmdbPersonMovieCreditDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("vote_average")]
    public decimal VoteAverage { get; set; }

    [JsonPropertyName("vote_count")]
    public int VoteCount { get; set; }

    public decimal Popularity { get; set; }
}

public class TmdbPersonCastCreditDto : TmdbPersonMovieCreditDto
{
    public string? Character { get; set; }
}

public class TmdbPersonCrewCreditDto : TmdbPersonMovieCreditDto
{
    public string? Department { get; set; }

    public string? Job { get; set; }
}

public class TmdbExternalIdsDto
{
    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("instagram_id")]
    public string? InstagramId { get; set; }

    [JsonPropertyName("twitter_id")]
    public string? TwitterId { get; set; }

    [JsonPropertyName("facebook_id")]
    public string? FacebookId { get; set; }
}
