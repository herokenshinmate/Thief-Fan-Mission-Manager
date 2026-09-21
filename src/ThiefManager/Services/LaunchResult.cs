namespace ThiefManager.Services;

public record LaunchResult(bool Ok, string? Error)
{
    public static LaunchResult Success() => new(true, null);
    public static LaunchResult Failure(string error) => new(false, error);
}
