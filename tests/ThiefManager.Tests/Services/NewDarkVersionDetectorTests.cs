using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class NewDarkVersionDetectorTests
{
    [Fact]
    public void TryDetect_OnAFileWithVersionInfo_ReturnsIt()
    {
        var version = NewDarkVersionDetector.TryDetect(@"C:\Windows\System32\notepad.exe");

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void TryDetect_OnAMissingFile_ReturnsNull()
    {
        Assert.Null(NewDarkVersionDetector.TryDetect(@"C:\does\not\exist.exe"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryDetect_OnBlankPath_ReturnsNull(string? path)
    {
        Assert.Null(NewDarkVersionDetector.TryDetect(path));
    }
}
