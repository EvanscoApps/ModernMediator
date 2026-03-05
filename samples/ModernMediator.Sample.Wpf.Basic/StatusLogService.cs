namespace ModernMediator.Sample.Wpf.Basic;

public class StatusLogService
{
    public event Action<string>? LogEntryReceived;

    public void AddEntry(string message) => LogEntryReceived?.Invoke(message);
}
