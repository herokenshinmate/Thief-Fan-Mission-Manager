using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class FmNameMatcherTests
{
    [Theory]
    [InlineData("A New Job", "A New Job", true)]
    [InlineData("A New Job", "a new job", true)]
    [InlineData("A_New_Job", "A New Job", true)]
    [InlineData("A-New-Job", "A New Job", true)]
    [InlineData("A New Job_v2", "A New Job", true)]
    [InlineData("A New Job v1.2", "A New Job", true)]
    [InlineData("A New Job (fixed)", "A New Job", true)]
    [InlineData("A New Job [repack]", "A New Job", true)]
    [InlineData("A New Job", "A Different Job", false)]
    public void AreSimilar_MatchesNamesDifferingOnlyByFormatting(string a, string b, bool expected)
    {
        Assert.Equal(expected, FmNameMatcher.AreSimilar(a, b));
    }

    [Fact]
    public void AreSimilar_ReturnsFalseWhenEitherNameIsEmptyAfterNormalization()
    {
        Assert.False(FmNameMatcher.AreSimilar("", "A New Job"));
        Assert.False(FmNameMatcher.AreSimilar("(v2)", "A New Job"));
    }
}
