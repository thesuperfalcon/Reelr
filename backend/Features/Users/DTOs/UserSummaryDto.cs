namespace backend.Features.Users.DTOs
{
    public class UserSummaryDto
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }
    }
}
