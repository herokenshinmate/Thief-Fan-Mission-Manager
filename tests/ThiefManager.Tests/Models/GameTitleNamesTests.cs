using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Models;

public class GameTitleNamesTests
{
    [Theory]
    [InlineData(GameTitle.Thief1, "Thief: The Dark Project")]
    [InlineData(GameTitle.Thief2, "Thief II: The Metal Age")]
    public void ToDisplayName_ReturnsFullTitle(GameTitle game, string expected)
    {
        Assert.Equal(expected, game.ToDisplayName());
    }

    [Theory]
    [InlineData("Thief: The Dark Project", GameTitle.Thief1)]
    [InlineData("Thief II: The Metal Age", GameTitle.Thief2)]
    public void Parse_RoundTripsFromDisplayName(string displayName, GameTitle expected)
    {
        Assert.Equal(expected, GameTitleNames.Parse(displayName));
    }

    [Fact]
    public void Parse_WithUnknownDisplayName_Throws()
    {
        Assert.Throws<ArgumentException>(() => GameTitleNames.Parse("Not a real game"));
    }
}
