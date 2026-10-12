using System.ComponentModel.DataAnnotations;
using backend.Features.Ratings;

namespace backend.Features.Diary.DTOs
{
    // Replaces what one diary entry records. The film's current rating and status stay as they are.
    public class UpdateDiaryEntryDto
    {
        [Required]
        public DateOnly? WatchedOn { get; set; }

        // Null logs the viewing without a rating.
        [HalfStars]
        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
