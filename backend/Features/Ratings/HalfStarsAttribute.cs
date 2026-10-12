using System.ComponentModel.DataAnnotations;

namespace backend.Features.Ratings
{
    /// <summary>
    /// A score from half a star (0.5) to five stars in half steps, the same range the star picker offers and
    /// the database checks (CK_Ratings_Score, CK_DiaryEntries_Rating). Null passes, for optional scores.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class HalfStarsAttribute : ValidationAttribute
    {
        public const decimal Min = 0.5m;
        public const decimal Max = 5m;

        public HalfStarsAttribute()
            : base("Score must be from 0.5 to 5 in steps of 0.5.")
        {
        }

        public override bool IsValid(object? value) =>
            value is null || (value is decimal score && IsHalfStars(score));

        public static bool IsHalfStars(decimal score) =>
            score is >= Min and <= Max && decimal.Remainder(score * 2, 1) == 0;
    }
}
