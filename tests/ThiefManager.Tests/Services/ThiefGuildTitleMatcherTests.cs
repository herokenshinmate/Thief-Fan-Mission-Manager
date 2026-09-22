using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildTitleMatcherTests
{
    [Theory]
    [InlineData("Between Hammer and Anvil", "Between Hammer and Anvil", true)]
    [InlineData("Between Hammer and Anvil", "between hammer and anvil", true)]
    [InlineData("Between_Hammer_and_Anvil", "Between Hammer and Anvil", true)]
    [InlineData("Between-Hammer-and-Anvil-v1.1", "Between Hammer and Anvil", false)]
    [InlineData("The Hammer Skull", "Between Hammer and Anvil", false)]
    public void IsMatch_ComparesNormalizedTitles(string candidate, string search, bool expected)
    {
        Assert.Equal(expected, ThiefGuildTitleMatcher.IsMatch(candidate, search));
    }

    [Theory]
    [InlineData("Between Hammer and Anvil", "Hammer")]
    [InlineData("Hammered", "Hammer")]
    [InlineData("The Seven Shades of Mercury", "Mercur")]
    [InlineData("Cathedral", "Cathed")]
    public void ExtractSearchKeyword_PicksLongestSignificantWordTruncatedToSixChars(string title, string expected)
    {
        Assert.Equal(expected, ThiefGuildTitleMatcher.ExtractSearchKeyword(title));
    }
}
