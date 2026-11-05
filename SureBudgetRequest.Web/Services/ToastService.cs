using System.Collections.Concurrent;

namespace SureBudgetRequest.Web.Services;

public enum ToastLevel { Success, Error, Warning, Info }

public record ToastMessage(
    Guid Id,
    ToastLevel Level,
    string Title,
    string? Message,
    int DurationMs = 4000);

public class ToastService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();
    public event Action? OnChange;
    public List<ToastMessage> Toasts { get; } = [];

    public void Show(ToastLevel level, string title, string? message = null, int durationMs = 4000)
    {
        var toast = new ToastMessage(Guid.NewGuid(), level, title, message, durationMs);
        Toasts.Add(toast);
        OnChange?.Invoke();

        if (durationMs > 0)
        {
            var cts = new CancellationTokenSource();
            _timers[toast.Id] = cts;
            _ = Task.Delay(durationMs, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Dismiss(toast.Id);
                }
            });
        }
    }

    public void Success(string title, string? message = null, int durationMs = 3000) => Show(ToastLevel.Success, title, message, durationMs);
    public void Error(string title, string? message = null, int durationMs = 4000)   => Show(ToastLevel.Error,   title, message, durationMs);
    public void Warning(string title, string? message = null, int durationMs = 4000) => Show(ToastLevel.Warning, title, message, durationMs);
    public void Info(string title, string? message = null, int durationMs = 3000)    => Show(ToastLevel.Info,    title, message, durationMs);

    public void Dismiss(Guid id)
    {
        if (_timers.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        Toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }
}
