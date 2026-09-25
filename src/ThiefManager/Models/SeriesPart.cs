namespace ThiefManager.Models;

/// <summary>One entry of a series' complete part list as Thief Guild lists it, owned or not.</summary>
public class SeriesPart
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThiefGuildUrl { get; set; }
}
