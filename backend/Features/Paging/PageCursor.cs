using System.Buffers.Text;
using System.Text.Json;

namespace backend.Features.Paging
{
    // Position in a list: the sort values of the last item a page ended on. Every list sorts by a date or a name,
    // then by an id, so each item has exactly one place and paging never skips or repeats one, even when items
    // are added while someone pages. Endpoints use the parts their order needs and leave the rest at the default.
    // Clients pass the encoded text back unchanged and never read it.
    public readonly record struct PageCursor(DateTime At, string Text, long Id)
    {
        public PageCursor(DateTime at, long id) : this(at, string.Empty, id)
        {
        }

        public PageCursor(string text, long id) : this(default, text, id)
        {
        }

        public string Encode() =>
            Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Payload(At.Ticks, Text, Id)));

        public static bool TryDecode(string? text, out PageCursor cursor)
        {
            cursor = default;

            if (string.IsNullOrEmpty(text) || text.Length > MaxEncodedLength)
            {
                return false;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<Payload>(Base64Url.DecodeFromChars(text));

                if (payload == null || payload.T == null
                    || payload.A < DateTime.MinValue.Ticks || payload.A > DateTime.MaxValue.Ticks)
                {
                    return false;
                }

                cursor = new PageCursor(new DateTime(payload.A, DateTimeKind.Utc), payload.T, payload.I);
                return true;
            }
            catch (Exception e) when (e is FormatException or JsonException)
            {
                return false;
            }
        }

        // Room for the longest sort text (a username) plus the numbers, encoded.
        private const int MaxEncodedLength = 512;

        private sealed record Payload(long A, string T, long I);
    }
}
