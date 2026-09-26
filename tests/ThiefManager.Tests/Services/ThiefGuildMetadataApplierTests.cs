using ThiefManager.Models;
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildMetadataApplierTests
{
    private static ThiefGuildLookupResult FullResult() => new(
        "skacky", 2014, "City, Rain", "https://www.thiefguild.com/fanmissions/2535/endless-rain",
        Rating: 9.02, RatingCount: 229, CampaignMissionCount: 1, Description: "A rainy night.",
        SequelOf: new ThiefGuildLink("Between These Dark Walls", "https://www.thiefguild.com/works/a"),
        HasSequel: new ThiefGuildLink("The Chalice of Souls", "https://www.thiefguild.com/works/b"),
        Notes: "- NewDark 1.22 is required!", RequiredNewDarkVersion: "1.22");

    [Fact]
    public void Apply_CopiesThiefGuildFieldsAndStampsVersion()
    {
        var mission = new FanMission();

        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        Assert.Equal("- NewDark 1.22 is required!", mission.Notes);
        Assert.Equal("1.22", mission.RequiredNewDarkVersion);
        Assert.Equal(9.02, mission.ThiefGuildRating);
        Assert.Equal(229, mission.ThiefGuildRatingCount);
        Assert.Equal(1, mission.CampaignMissionCount);
        Assert.Equal("A rainy night.", mission.Description);
        Assert.Equal("Between These Dark Walls", mission.SequelOfTitle);
        Assert.Equal("https://www.thiefguild.com/works/a", mission.SequelOfUrl);
        Assert.Equal("The Chalice of Souls", mission.HasSequelTitle);
        Assert.Equal("https://www.thiefguild.com/works/b", mission.HasSequelUrl);
        Assert.Equal("https://www.thiefguild.com/fanmissions/2535/endless-rain", mission.ThiefGuildUrl);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, mission.ThiefGuildMetadataVersion);
    }

    [Fact]
    public void Apply_NullFieldsInResult_ClearStoredThiefGuildFields()
    {
        var mission = new FanMission();
        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        ThiefGuildMetadataApplier.Apply(mission, new ThiefGuildLookupResult(null, null, "", "https://www.thiefguild.com/fanmissions/2535/endless-rain"));

        Assert.Null(mission.ThiefGuildRating);
        Assert.Null(mission.ThiefGuildRatingCount);
        Assert.Null(mission.CampaignMissionCount);
        Assert.Null(mission.Description);
        Assert.Null(mission.SequelOfTitle);
        Assert.Null(mission.HasSequelUrl);
        Assert.Null(mission.RequiredNewDarkVersion);
    }

    [Fact]
    public void Apply_FillsOnlyBlankAuthorYearAndTags()
    {
        var mission = new FanMission { Author = "Me", ReleaseYear = 2000, Tags = "" };

        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        Assert.Equal("Me", mission.Author);
        Assert.Equal(2000, mission.ReleaseYear);
        Assert.Equal("City, Rain", mission.Tags);
    }

    [Fact]
    public void Apply_DoesNotOverwriteExistingPersonalNotes()
    {
        var mission = new FanMission { Notes = "My own review notes." };

        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        Assert.Equal("My own review notes.", mission.Notes);
    }

    [Fact]
    public void Apply_FillsBlankNotesFromThiefGuild()
    {
        var mission = new FanMission { Notes = "" };

        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        Assert.Equal("- NewDark 1.22 is required!", mission.Notes);
    }
}
