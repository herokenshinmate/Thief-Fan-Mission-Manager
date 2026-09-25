using System.Globalization;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

/// <summary>One row of the main mission list: a mission, a series header, or a series part the user doesn't own.</summary>
public abstract class MissionListRow
{
}

public sealed class MissionRow : MissionListRow
{
    public MissionRow(FanMission mission, bool isSeriesMember)
    {
        Mission = mission;
        IsSeriesMember = isSeriesMember;
    }

    public FanMission Mission { get; }
    public bool IsSeriesMember { get; }

    public string DisplayTitle => IsSeriesMember && Mission.SeriesPosition is int position
        ? $"#{position} · {Mission.Title}"
        : Mission.Title;

    public string? ThiefGuildRatingDisplay => Mission.ThiefGuildRating is double rating && Mission.ThiefGuildRatingCount is int count
        ? $"★ {rating.ToString("0.00", CultureInfo.InvariantCulture)} ({count})"
        : null;

    public string? MissionTypeDisplay => Mission.CampaignMissionCount is int missions && missions > 1
        ? $"Campaign · {missions}"
        : null;
}

public sealed class SeriesHeaderRow : MissionListRow
{
    public SeriesHeaderRow(Series series, int shownCount, int totalCount, int completedCount, GameTitle? commonGame, int? partCount = null)
    {
        Series = series;
        ShownCount = shownCount;
        TotalCount = totalCount;
        CompletedCount = completedCount;
        CommonGame = commonGame;
        PartCount = partCount;
    }

    public Series Series { get; }
    public int ShownCount { get; }
    public int TotalCount { get; }
    public int CompletedCount { get; }

    /// <summary>The game every owned member belongs to, or null when they differ.</summary>
    public GameTitle? CommonGame { get; }

    /// <summary>How many parts Thief Guild lists for this series, or null when unknown.</summary>
    public int? PartCount { get; }

    public bool IsExpanded => Series.IsExpanded;
    public string ChevronGlyph => IsExpanded ? "▼" : "▶";

    public string HeaderText => ShownCount != TotalCount
        ? $"{Series.Name} ({ShownCount} of {TotalCount} shown)"
        : PartCount is int parts && parts >= TotalCount
            ? $"{Series.Name} ({TotalCount} of {parts} owned)"
            : $"{Series.Name} ({TotalCount})";

    public string ProgressText => $"{CompletedCount}/{TotalCount} completed";
}

/// <summary>A part of a series the user doesn't own, shown as a dimmed placeholder under its header.</summary>
public sealed class MissingPartRow : MissionListRow
{
    public MissingPartRow(SeriesPart part) => Part = part;

    public SeriesPart Part { get; }

    public string DisplayTitle => $"#{Part.Position} · {Part.Title} — not in library";
}
