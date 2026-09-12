using System.Text.Json.Serialization;

namespace backend.Features.Movies.DTOs;

public class TmdbCompanyDto
{
    public int Id { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; set; }
}
