namespace SureBudgetRequest.Web.Services;

public enum ToastLevel { Success, Error, Warning, Info }

public record ToastMessage(Guid Id, ToastLevel Level, string Title, string? Message);

public class ToastService
{
    public event Action? OnChange;
    public List<ToastMessage> Toasts { get; } = [];

    public void Show(ToastLevel level, string title, string? message = null)
    {
        var toast = new ToastMessage(Guid.NewGuid(), level, title, message);
        Toasts.Add(toast);
        OnChange?.Invoke();

        _ = Task.Delay(4000).ContinueWith(_ =>
        {
            Dismiss(toast.Id);
        });
    }

    public void Success(string title, string? message = null) => Show(ToastLevel.Success, title, message);
    public void Error(string title, string? message = null)   => Show(ToastLevel.Error,   title, message);
    public void Warning(string title, string? message = null) => Show(ToastLevel.Warning, title, message);
    public void Info(string title, string? message = null)    => Show(ToastLevel.Info,    title, message);

    public void Dismiss(Guid id)
    {
        Toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }
}
