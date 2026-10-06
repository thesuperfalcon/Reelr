using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace backend.Features.Activity
{
    // Position in a feed: the last item a page ended on. Items sort by time, then kind, then key, all newest first,
    // so every item has exactly one place and paging never skips or repeats one.
    // Clients pass the encoded text back unchanged and never read it.
    public readonly record struct ActivityCursor(DateTime OccurredAt, int Rank, long Key)
    {
        public string Encode() =>
            Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"{OccurredAt.Ticks}.{Rank}.{Key}")));

        public static bool TryDecode(string? text, out ActivityCursor cursor)
        {
            cursor = default;

            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            try
            {
                var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(text)).Split('.');

                if (parts.Length == 3
                    && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                    && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks
                    && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rank)
                    && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var key))
                {
                    cursor = new ActivityCursor(new DateTime(ticks, DateTimeKind.Utc), rank, key);
                    return true;
                }
            }
            catch (FormatException)
            {
            }

            return false;
        }

        // True when an item comes after this cursor in feed order, that is, belongs on a later page.
        public bool IsBefore(DateTime occurredAt, int rank, long key) =>
            occurredAt < OccurredAt
            || (occurredAt == OccurredAt && (rank < Rank || (rank == Rank && key < Key)));
    }
}
