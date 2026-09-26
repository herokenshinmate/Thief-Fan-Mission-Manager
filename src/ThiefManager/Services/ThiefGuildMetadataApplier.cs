using ThiefManager.Models;

namespace ThiefManager.Services;

public static class ThiefGuildMetadataApplier
{
    /// <summary>
    /// Copies a lookup onto a mission in memory (the caller persists it). Rating, type,
    /// description and sequel links mirror Thief Guild and are read-only in the app, so they're
    /// always overwritten — a field the page no longer shows is cleared. Author, year, tags and
    /// notes may have been typed by the user, so they only fill blanks (notes is the author's own
    /// warnings/requirements, not the mission's story synopsis in Description). Series assignment
    /// is SeriesAssigner's.
    /// </summary>
    public static void Apply(FanMission mission, ThiefGuildLookupResult result)
    {
        mission.ThiefGuildRating = result.Rating;
        mission.ThiefGuildRatingCount = result.RatingCount;
        mission.CampaignMissionCount = result.CampaignMissionCount;
        mission.Description = result.Description;
        mission.SequelOfTitle = result.SequelOf?.Title;
        mission.SequelOfUrl = result.SequelOf?.Url;
        mission.HasSequelTitle = result.HasSequel?.Title;
        mission.HasSequelUrl = result.HasSequel?.Url;
        mission.RequiredNewDarkVersion = result.RequiredNewDarkVersion;

        if (string.IsNullOrWhiteSpace(mission.Author))
            mission.Author = result.Author;
        if (mission.ReleaseYear is null)
            mission.ReleaseYear = result.ReleaseYear;
        if (string.IsNullOrWhiteSpace(mission.Tags))
            mission.Tags = result.Tags;
        if (string.IsNullOrWhiteSpace(mission.Notes))
            mission.Notes = result.Notes;

        mission.ThiefGuildUrl = result.Url;
        mission.ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion;
    }
}
