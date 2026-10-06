namespace backend.Features.People.DTOs;

public class PersonDetailsDto
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Biography { get; set; }

    public string? Birthday { get; set; }

    public string? Deathday { get; set; }

    public string? PlaceOfBirth { get; set; }

    public string? ProfilePath { get; set; }

    public string? KnownForDepartment { get; set; }

    public string? Homepage { get; set; }

    public string? ImdbId { get; set; }

    public string? InstagramId { get; set; }

    public string? TwitterId { get; set; }

    public string? FacebookId { get; set; }

    // The person's best-known films: most voted first in their known-for department, then other work.
    public List<PersonCreditDto> KnownFor { get; set; } = [];

    // One entry per film and department, newest first; films without a release date come first.
    public List<PersonCreditDto> Credits { get; set; } = [];
}
