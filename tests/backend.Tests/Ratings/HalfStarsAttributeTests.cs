using backend.Features.Ratings;

namespace backend.Tests.Ratings;

public class HalfStarsAttributeTests
{
    [Theory]
    [InlineData("0.5", true)]
    [InlineData("3", true)]
    [InlineData("4.5", true)]
    [InlineData("5", true)]
    [InlineData("0", false)]
    [InlineData("3.3", false)]
    [InlineData("5.5", false)]
    [InlineData("-1", false)]
    public void Accepts_OnlyHalfStarsFromHalfToFive(string score, bool valid)
    {
        Assert.Equal(valid, new HalfStarsAttribute().IsValid(decimal.Parse(score, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Accepts_NoScore()
    {
        Assert.True(new HalfStarsAttribute().IsValid(null));
    }
}
