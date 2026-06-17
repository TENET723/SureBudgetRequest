using Microsoft.AspNetCore.Components;
using SureBudgetRequest.Web.Services;

namespace SureBudgetRequest.Web.Components.Shared;

/// <summary>
/// Base class for pages with multiple co-located mutating actions (e.g. RequestDetail's
/// Approve / Reject / Send-back / Record-payment). A single page-level <see cref="IsBusy"/>
/// flag means firing one action disables them all — bind <c>disabled="@IsBusy"</c> on every
/// mutating control.
///
/// <see cref="Guard"/> wraps a handler with the same synchronous re-entrancy guard +
/// try/finally pattern used by AsyncButton, so duplicate clicks are dropped before the
/// first await. Pass <c>global: true</c> to also raise the full-screen overlay (use this
/// for genuinely blocking operations such as recording a payment).
/// </summary>
public abstract class BusyComponentBase : ComponentBase
{
    [Inject] protected BusyState Busy { get; set; } = default!;

    protected bool IsBusy { get; private set; }

    protected async Task Guard(Func<Task> action, bool global = false)
    {
        if (IsBusy) return;             // synchronous guard — before any await
        IsBusy = true;
        if (global) Busy.Begin();
        StateHasChanged();              // flush the disabled state to the client
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
            if (global) Busy.End();
            StateHasChanged();
        }
    }
}
