namespace backend.Features.Movies.DTOs;

public class MovieDetailsImageDto
{
    public string? FilePath { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public double AspectRatio { get; set; }
}
