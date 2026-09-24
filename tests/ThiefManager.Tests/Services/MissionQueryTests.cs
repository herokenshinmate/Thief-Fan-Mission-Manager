using ThiefManager.Models;
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class MissionQueryTests
{
    private static FanMission Mission(string title, GameTitle game, MissionStatus status, int? rating, string tags = "", string? author = null) =>
        new()
        {
            Title = title,
            Game = game,
            Status = status,
            Rating = rating,
            Tags = tags,
            Author = author
        };

    [Fact]
    public void Apply_FiltersByGame()
    {
        var missions = new[]
        {
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null),
            Mission("B", GameTitle.Thief2, MissionStatus.NotPlayed, null)
        };

        var result = MissionQuery.Apply(missions, GameTitle.Thief2, null, null, SortField.Title, true);

        Assert.Equal(new[] { "B" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_FiltersByTagCaseInsensitively()
    {
        var missions = new[]
        {
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null, "Horror, Heist"),
            Mission("B", GameTitle.Thief1, MissionStatus.NotPlayed, null, "Puzzle")
        };

        var result = MissionQuery.Apply(missions, null, null, "horror", SortField.Title, true);

        Assert.Equal(new[] { "A" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_SortsByRatingDescendingWithUnratedLast()
    {
        var missions = new[]
        {
            Mission("Unrated", GameTitle.Thief1, MissionStatus.NotPlayed, null),
            Mission("High", GameTitle.Thief1, MissionStatus.Completed, 5),
            Mission("Low", GameTitle.Thief1, MissionStatus.Completed, 2)
        };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.Rating, false);

        Assert.Equal(new[] { "High", "Low", "Unrated" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_FiltersByAuthorCaseInsensitivelyAndPartially()
    {
        var missions = new[]
        {
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null, author: "Lord Taffer, Aemanyl"),
            Mission("B", GameTitle.Thief1, MissionStatus.NotPlayed, null, author: "skacky"),
            Mission("C", GameTitle.Thief1, MissionStatus.NotPlayed, null)
        };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.Title, true, authorFilter: "taffer");

        Assert.Equal(new[] { "A" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_SortsByGameAscendingWithThief1First()
    {
        var missions = new[]
        {
            Mission("B", GameTitle.Thief2, MissionStatus.NotPlayed, null),
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null)
        };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.Game, true);

        Assert.Equal(new[] { "A", "B" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_SortsByAuthor()
    {
        var missions = new[]
        {
            Mission("B", GameTitle.Thief1, MissionStatus.NotPlayed, null, author: "Zed"),
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null, author: "Abe")
        };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.Author, true);

        Assert.Equal(new[] { "A", "B" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_SortsByInstallStatus()
    {
        var installed = Mission("Installed", GameTitle.Thief1, MissionStatus.NotPlayed, null);
        installed.InstallStatus = InstallStatus.Installed;
        var notInstalled = Mission("NotInstalled", GameTitle.Thief1, MissionStatus.NotPlayed, null);
        notInstalled.InstallStatus = InstallStatus.NotInstalled;
        var missions = new[] { installed, notInstalled };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.InstallStatus, true);

        Assert.Equal(new[] { "NotInstalled", "Installed" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_FiltersByInstallStatus()
    {
        var installed = Mission("Installed", GameTitle.Thief1, MissionStatus.NotPlayed, null);
        installed.InstallStatus = InstallStatus.Installed;
        var notInstalled = Mission("NotInstalled", GameTitle.Thief1, MissionStatus.NotPlayed, null);
        notInstalled.InstallStatus = InstallStatus.NotInstalled;
        var missions = new[] { installed, notInstalled };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.Title, true, InstallStatus.NotInstalled);

        Assert.Equal(new[] { "NotInstalled" }, result.Select(m => m.Title));
    }
}
