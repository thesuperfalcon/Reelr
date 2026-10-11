using System.ComponentModel.DataAnnotations;

namespace backend.Features.Paging
{
    // ?cursor=&limit= on every list endpoint. A missing cursor means the first page. A cursor that cannot be
    // read, or a limit out of range, is a 400 like any other invalid input.
    public class PageQuery : IValidatableObject
    {
        public const int DefaultLimit = 30;
        public const int MaxLimit = 100;

        public string? Cursor { get; set; }

        [Range(1, MaxLimit)]
        public int Limit { get; set; } = DefaultLimit;

        // The position to continue after, or null for the first page.
        public PageCursor? After => PageCursor.TryDecode(Cursor, out var cursor) ? cursor : null;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(Cursor) && !PageCursor.TryDecode(Cursor, out _))
            {
                yield return new ValidationResult("Invalid cursor.", [nameof(Cursor)]);
            }
        }
    }
}
