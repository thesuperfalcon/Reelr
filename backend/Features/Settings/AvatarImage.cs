using SkiaSharp;

namespace backend.Features.Settings
{
    // Checks uploaded profile pictures and draws them again as a new image.
    public static class AvatarImage
    {
        // The client scales pictures down before uploading, so real uploads are far smaller than this.
        public const int MaxBytes = 1024 * 1024;

        // Pictures are shown at most a few hundred pixels wide.
        public const int MaxSide = 512;

        public const string StoredContentType = "image/webp";

        // Refuses pictures whose decoded size would use a lot of memory, whatever their file size.
        private const int MaxSourceSide = 8000;

        private const int Quality = 85;

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

        /// <summary>
        /// Decodes the picture and encodes its pixels as a new WebP image, at most <see cref="MaxSide"/> pixels on a side.
        /// Only pixels are copied, so camera metadata such as GPS position, and anything hidden in the file
        /// besides the picture, is left behind. Null when the bytes are not a picture that can be read.
        /// </summary>
        public static byte[]? Normalize(byte[] data)
        {
            if (DetectContentType(data) == null)
            {
                return null;
            }

            using var codec = SKCodec.Create(new SKMemoryStream(data));

            if (codec == null
                || codec.Info.Width <= 0 || codec.Info.Height <= 0
                || codec.Info.Width > MaxSourceSide || codec.Info.Height > MaxSourceSide)
            {
                return null;
            }

            using var decoded = SKBitmap.Decode(codec);

            if (decoded == null)
            {
                return null;
            }

            // The orientation is kept in the metadata that is dropped, so it is applied to the pixels first.
            using var upright = ApplyOrigin(decoded, codec.EncodedOrigin);

            var scale = Math.Min(1.0, (double)MaxSide / Math.Max(upright.Width, upright.Height));
            var info = new SKImageInfo(
                Math.Max(1, (int)Math.Round(upright.Width * scale)),
                Math.Max(1, (int)Math.Round(upright.Height * scale)),
                SKColorType.Rgba8888,
                SKAlphaType.Premul);

            using var resized = upright.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));

            if (resized == null)
            {
                return null;
            }

            using var encoded = resized.Encode(SKEncodedImageFormat.Webp, Quality);
            return encoded?.ToArray();
        }

        private static SKBitmap ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            var quarterTurns = origin switch
            {
                SKEncodedOrigin.RightTop => 1,
                SKEncodedOrigin.BottomRight => 2,
                SKEncodedOrigin.LeftBottom => 3,
                // Mirrored origins are rare outside selfie cameras; those pictures keep their stored orientation.
                _ => 0
            };

            if (quarterTurns == 0)
            {
                return bitmap.Copy();
            }

            var sideways = quarterTurns % 2 == 1;
            var rotated = new SKBitmap(sideways ? bitmap.Height : bitmap.Width, sideways ? bitmap.Width : bitmap.Height);

            using var canvas = new SKCanvas(rotated);
            canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
            canvas.RotateDegrees(90 * quarterTurns);
            canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
            canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));

            return rotated;
        }
    }
}
