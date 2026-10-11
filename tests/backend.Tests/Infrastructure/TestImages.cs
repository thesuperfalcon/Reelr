using SkiaSharp;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Real, decodable pictures for the profile picture tests.
/// </summary>
public static class TestImages
{
    public static byte[] Create(SKEncodedImageFormat format, int width = 40, int height = 30)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
        }

        using var data = bitmap.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>
    /// A JPEG with an EXIF block, like a phone photo, holding <paramref name="secret"/> where the GPS position would be.
    /// </summary>
    public static byte[] JpegWithExif(string secret)
    {
        var jpeg = Create(SKEncodedImageFormat.Jpeg);

        // An APP1 segment right after the start-of-image marker: "Exif\0\0", then a minimal TIFF header and the text.
        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange([0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00]);
        payload.AddRange(System.Text.Encoding.ASCII.GetBytes(secret));

        var length = payload.Count + 2;
        var segment = new List<byte> { 0xFF, 0xE1, (byte)(length >> 8), (byte)length };
        segment.AddRange(payload);

        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    public static (int Width, int Height, SKEncodedImageFormat Format) Describe(byte[] data)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(data));
        return (codec.Info.Width, codec.Info.Height, codec.EncodedFormat);
    }
}
