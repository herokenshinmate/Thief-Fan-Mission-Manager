namespace ThiefManager.Models;

public class Series
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ThiefGuildSeriesId { get; set; }
    public bool IsExpanded { get; set; } = true;
}
