namespace backend.Features.Settings
{
    // Recognises the image formats accepted as profile pictures by their first bytes.
    public static class AvatarImage
    {
        // The client scales pictures down before uploading, so real uploads are far smaller than this.
        public const int MaxBytes = 1024 * 1024;

        public static string? DetectContentType(ReadOnlySpan<byte> data)
        {
            if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            {
                return "image/jpeg";
            }

            if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            {
                return "image/png";
            }

            // "RIFF", four size bytes, then "WEBP".
            if (data.Length >= 12
                && data[..4].SequenceEqual("RIFF"u8)
                && data[8..12].SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            return null;
        }
    }
}
