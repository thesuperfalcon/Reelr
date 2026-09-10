namespace backend.Features.Movies.DTOs;

public class MovieDetailsCastDto
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? OriginalName { get; set; }

    public string? Character { get; set; }

    public string? ProfilePath { get; set; }

    public int Order { get; set; }
}
