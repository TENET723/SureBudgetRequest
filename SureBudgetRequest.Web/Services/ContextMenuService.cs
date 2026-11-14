using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SureBudgetRequest.Web.Services;

public class ContextMenuService
{
    private Action? _onCloseCallback;

    public bool IsOpen { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public bool IsButton { get; private set; }
    public string? Title { get; private set; }
    public RenderFragment? Content { get; private set; }
    public string? CustomClass { get; private set; }

    public event Action? OnChange;

    public void Show(MouseEventArgs e, RenderFragment content, string? title = null, bool isButton = false, string? customClass = null, Action? onClose = null)
    {
        Show(e.ClientX, e.ClientY, content, title, isButton, customClass, onClose);
    }

    public void Show(double clientX, double clientY, RenderFragment content, string? title = null, bool isButton = false, string? customClass = null, Action? onClose = null)
    {
        if (IsOpen)
        {
            var oldCb = _onCloseCallback;
            _onCloseCallback = null;
            oldCb?.Invoke();
        }

        X = Math.Max(10, clientX);
        Y = Math.Max(10, clientY);
        Content = content;
        Title = title;
        IsButton = isButton;
        CustomClass = customClass;
        _onCloseCallback = onClose;
        IsOpen = true;
        OnChange?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Content = null;
        Title = null;
        CustomClass = null;
        var cb = _onCloseCallback;
        _onCloseCallback = null;
        cb?.Invoke();
        OnChange?.Invoke();
    }
}
