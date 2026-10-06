namespace backend.Features.People.DTOs;

public class PersonCreditDto
{
    public int TmdbId { get; set; }

    public string? Title { get; set; }

    public string? PosterPath { get; set; }

    public string? ReleaseDate { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public decimal Popularity { get; set; }

    // "Acting", a TMDB crew department such as "Directing", or "Appearances" for roles as themselves.
    public string Department { get; set; } = string.Empty;

    // Characters played, or crew jobs held, on this film in this department.
    public List<string> Roles { get; set; } = [];
}
