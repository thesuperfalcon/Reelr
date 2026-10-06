namespace backend.Features.Activity.DTOs
{
    public class ActivityPageDto
    {
        public List<ActivityItemDto> Items { get; set; } = [];

        // Pass back as ?cursor= for the next page. Null on the last page.
        public string? NextCursor { get; set; }
    }
}
