namespace ThiefManager.Models;

public class IgnoredFm
{
    public int Id { get; set; }
    public GameTitle Game { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime IgnoredAt { get; set; }
}
