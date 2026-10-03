using System.ComponentModel.DataAnnotations;

namespace backend.Features.Diary.DTOs
{
    public class LogDiaryEntryDto
    {
        // Null leaves the current rating as it is.
        [Range(typeof(decimal), "0", "5")]
        public decimal? Score { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
