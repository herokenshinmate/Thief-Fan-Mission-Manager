using ThiefManager.Services;

namespace ThiefManager.Tests.Fakes;

public class FakeUpdateService : IUpdateService
{
    public bool IsInstalled { get; set; } = true;
    public AvailableUpdate? NextResult { get; set; }
    public Exception? ThrowOnCheck { get; set; }
    public int CheckCount { get; private set; }
    public int ApplyCount { get; private set; }

    public Task<AvailableUpdate?> CheckAndDownloadAsync()
    {
        CheckCount++;
        return ThrowOnCheck is not null
            ? Task.FromException<AvailableUpdate?>(ThrowOnCheck)
            : Task.FromResult(NextResult);
    }

    public void ApplyAndRestart() => ApplyCount++;
}
