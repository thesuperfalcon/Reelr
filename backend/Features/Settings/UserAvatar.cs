namespace backend.Features.Settings
{
    // An uploaded profile picture. Kept in its own table so loading a user never loads the image bytes.
    public class UserAvatar
    {
        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public byte[] Data { get; set; } = [];

        // Detected from the file's first bytes, never taken from the upload.
        public string ContentType { get; set; } = string.Empty;

        // Part of the image URL, so browsers can cache an image forever and still see a new one.
        public DateTime UpdatedAt { get; set; }
    }
}
