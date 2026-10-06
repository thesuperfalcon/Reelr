using System.ComponentModel.DataAnnotations;

namespace backend.Features.Diary.DTOs
{
    // Replaces what one diary entry records. The film's current rating and status stay as they are.
    public class UpdateDiaryEntryDto
    {
        [Required]
        public DateOnly? WatchedOn { get; set; }

        // Null logs the viewing without a rating.
        [Range(typeof(decimal), "0.5", "5")]
        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
