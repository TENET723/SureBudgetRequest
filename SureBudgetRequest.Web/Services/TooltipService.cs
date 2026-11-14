using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SureBudgetRequest.Web.Services;

public class TooltipService
{
    public bool IsVisible { get; private set; }
    public string? Text { get; private set; }
    public RenderFragment? Content { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double AnchorW { get; private set; }
    public double AnchorH { get; private set; }
    public TooltipPlacement Placement { get; private set; } = TooltipPlacement.Top;
    public string? CustomClass { get; private set; }

    public event Action? OnChange;

    public void Show(string text, MouseEventArgs e, TooltipPlacement placement = TooltipPlacement.Top, string? customClass = null)
    {
        Show(text, e.ClientX, e.ClientY, placement, customClass);
    }

    public void Show(RenderFragment content, MouseEventArgs e, TooltipPlacement placement = TooltipPlacement.Top, string? customClass = null)
    {
        Show(content, e.ClientX, e.ClientY, placement, customClass);
    }

    public void Show(string text, double x, double y, TooltipPlacement placement = TooltipPlacement.Top, string? customClass = null, double anchorW = 0, double anchorH = 0)
    {
        Text = text;
        Content = null;
        X = x;
        Y = y;
        AnchorW = anchorW;
        AnchorH = anchorH;
        Placement = placement;
        CustomClass = customClass;
        IsVisible = true;
        OnChange?.Invoke();
    }

    public void Show(RenderFragment content, double x, double y, TooltipPlacement placement = TooltipPlacement.Top, string? customClass = null, double anchorW = 0, double anchorH = 0)
    {
        Text = null;
        Content = content;
        X = x;
        Y = y;
        AnchorW = anchorW;
        AnchorH = anchorH;
        Placement = placement;
        CustomClass = customClass;
        IsVisible = true;
        OnChange?.Invoke();
    }

    public void Hide()
    {
        if (!IsVisible) return;
        IsVisible = false;
        Text = null;
        Content = null;
        OnChange?.Invoke();
    }
}
