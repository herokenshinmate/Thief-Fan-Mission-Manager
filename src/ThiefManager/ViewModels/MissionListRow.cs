using ThiefManager.Models;

namespace ThiefManager.ViewModels;

/// <summary>One row of the main mission list: either a mission or a series header.</summary>
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
}

public sealed class SeriesHeaderRow : MissionListRow
{
    public SeriesHeaderRow(Series series, int shownCount, int totalCount, int completedCount, GameTitle? commonGame)
    {
        Series = series;
        ShownCount = shownCount;
        TotalCount = totalCount;
        CompletedCount = completedCount;
        CommonGame = commonGame;
    }

    public Series Series { get; }
    public int ShownCount { get; }
    public int TotalCount { get; }
    public int CompletedCount { get; }

    /// <summary>The game every owned member belongs to, or null when they differ.</summary>
    public GameTitle? CommonGame { get; }

    public bool IsExpanded => Series.IsExpanded;
    public string ChevronGlyph => IsExpanded ? "▼" : "▶";

    public string HeaderText => ShownCount == TotalCount
        ? $"{Series.Name} ({TotalCount})"
        : $"{Series.Name} ({ShownCount} of {TotalCount} shown)";

    public string ProgressText => $"{CompletedCount}/{TotalCount} completed";
}
