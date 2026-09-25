using System.Globalization;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

/// <summary>One row of the main mission list: a game banner, a mission, a series header, or a series part the user doesn't own.</summary>
public abstract class MissionListRow
{
    public virtual AccentKind Accent => AccentKind.None;
}

/// <summary>Which highlight a row's accent bar shows: packed campaigns win over series.</summary>
public enum AccentKind
{
    None,
    Campaign,
    Series
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

    /// <summary>One FM that bundles several missions (Thief Guild's "Campaign of N missions").</summary>
    public bool IsPackedCampaign => Mission.CampaignMissionCount > 1;

    public string? CampaignBadgeText => IsPackedCampaign ? $"CAMPAIGN · {Mission.CampaignMissionCount}" : null;

    public override AccentKind Accent => IsPackedCampaign ? AccentKind.Campaign
        : IsSeriesMember ? AccentKind.Series
        : AccentKind.None;
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

    public override AccentKind Accent => AccentKind.Series;
}

/// <summary>A part of a series the user doesn't own, shown as a dimmed placeholder under its header.</summary>
public sealed class MissingPartRow : MissionListRow
{
    public MissingPartRow(SeriesPart part) => Part = part;

    public SeriesPart Part { get; }

    public string DisplayTitle => $"#{Part.Position} · {Part.Title} — not in library";

    public override AccentKind Accent => AccentKind.Series;
}

/// <summary>A game's banner at the top of its section of the list.</summary>
public sealed class GameHeaderRow : MissionListRow
{
    public GameHeaderRow(GameTitle game, bool isExpanded, int shownCount, int totalCount, int completedCount, int installedCount)
    {
        Game = game;
        IsExpanded = isExpanded;
        ShownCount = shownCount;
        TotalCount = totalCount;
        CompletedCount = completedCount;
        InstalledCount = installedCount;
    }

    public GameTitle Game { get; }
    public bool IsExpanded { get; }

    /// <summary>Missions of this game passing the current filters.</summary>
    public int ShownCount { get; }

    /// <summary>All missions of this game in the library.</summary>
    public int TotalCount { get; }

    public int CompletedCount { get; }
    public int InstalledCount { get; }

    public string ChevronGlyph => IsExpanded ? "▼" : "▶";

    public string StatsText
    {
        get
        {
            var missions = ShownCount == TotalCount
                ? Plural(TotalCount, "mission")
                : $"{ShownCount} of {Plural(TotalCount, "mission")} shown";
            return $"{missions} · {CompletedCount} completed · {InstalledCount} installed";
        }
    }

    private static string Plural(int count, string word) => $"{count} {word}{(count == 1 ? string.Empty : "s")}";
}
