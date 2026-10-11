namespace backend.Features.Paging
{
    public class PageDto<T>
    {
        public List<T> Items { get; set; } = [];

        // Pass back as ?cursor= for the next page. Null on the last page.
        public string? NextCursor { get; set; }
    }

    public static class PageDto
    {
        // Endpoints read one row more than the limit; that extra row only tells that another page exists.
        public static PageDto<T> From<T>(List<T> rows, int limit, Func<T, PageCursor> cursorOf) =>
            From(rows, limit, cursorOf, row => row);

        // For rows that carry sort values the response leaves out, such as an id the client never sees.
        public static PageDto<T> From<TRow, T>(List<TRow> rows, int limit, Func<TRow, PageCursor> cursorOf, Func<TRow, T> map)
        {
            var hasMore = rows.Count > limit;
            var kept = hasMore ? rows.GetRange(0, limit) : rows;

            return new PageDto<T>
            {
                Items = kept.Select(map).ToList(),
                NextCursor = hasMore ? cursorOf(kept[^1]).Encode() : null
            };
        }
    }
}
