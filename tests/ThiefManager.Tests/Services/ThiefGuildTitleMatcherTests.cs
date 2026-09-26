using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildTitleMatcherTests
{
    [Theory]
    [InlineData("Between Hammer and Anvil", "Between Hammer and Anvil", true)]
    [InlineData("Between Hammer and Anvil", "between hammer and anvil", true)]
    [InlineData("Between_Hammer_and_Anvil", "Between Hammer and Anvil", true)]
    [InlineData("The Hammer Skull", "Between Hammer and Anvil", false)]
    // A local archive/folder name's trailing version tag shouldn't block a match against
    // Thief Guild's clean title, regardless of which side carries it.
    [InlineData("Between Hammer and Anvil", "Between-Hammer-and-Anvil-v1.1", true)]
    [InlineData("Between-Hammer-and-Anvil-v1.1", "Between Hammer and Anvil", true)]
    [InlineData("Ambush in the Dark", "Ambush_in_the_Dark_v2", true)]
    [InlineData("Ambush in the Dark", "AmbushInTheDark_rev3", true)]
    public void IsMatch_ComparesNormalizedTitles(string candidate, string search, bool expected)
    {
        Assert.Equal(expected, ThiefGuildTitleMatcher.IsMatch(candidate, search));
    }

    [Theory]
    [InlineData("Between Hammer and Anvil", "Hammer")]
    [InlineData("Hammered", "Hammer")]
    [InlineData("The Seven Shades of Mercury", "Mercur")]
    [InlineData("Cathedral", "Cathed")]
    // No separators at all between words: split at each camelCase boundary first so a real
    // word (not an arbitrary six-character slice spanning two words) is picked as the keyword.
    [InlineData("TheBafordsManor", "Baford")]
    [InlineData("AmbushInTheDark_v2", "Ambush")]
    public void ExtractSearchKeyword_PicksLongestSignificantWordTruncatedToSixChars(string title, string expected)
    {
        Assert.Equal(expected, ThiefGuildTitleMatcher.ExtractSearchKeyword(title));
    }
}
