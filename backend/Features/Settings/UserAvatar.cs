namespace backend.Features.Settings
{
    // An uploaded profile picture. Kept in its own table so loading a user never loads the image bytes.
    public class UserAvatar
    {
        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public byte[] Data { get; set; } = [];

        // Always WebP: uploads are drawn again before they are stored (see AvatarImage.Normalize).
        public string ContentType { get; set; } = string.Empty;

        // Part of the image URL, so browsers can cache an image forever and still see a new one.
        public DateTime UpdatedAt { get; set; }
    }
}
