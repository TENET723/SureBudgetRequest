namespace SureBudgetRequest.Web.Services;

/// <summary>
/// Circuit-scoped signal that drives the full-screen <c>LoadingOverlay</c>.
///
/// Uses a reference <b>counter</b> rather than a bool so that overlapping guarded
/// operations don't clear the overlay prematurely: the overlay is visible while the
/// count is greater than zero, and only hides once every in-flight operation has
/// called <see cref="End"/>. Callers must always pair <see cref="Begin"/>/<see cref="End"/>
/// in a try/finally so a thrown exception can't leave the overlay stuck on-screen.
///
/// Registered <c>AddScoped</c> in Program.cs — one instance per Blazor circuit, mirroring
/// <see cref="ToastService"/>.
/// </summary>
public sealed class BusyState
{
    private int _count;

    public bool IsBusy => _count > 0;

    /// <summary>Raised whenever the busy state crosses the on/off boundary.</summary>
    public event Action? Changed;

    public void Begin()
    {
        _count++;
        if (_count == 1) Changed?.Invoke();
    }

    public void End()
    {
        if (_count == 0) return;
        _count--;
        if (_count == 0) Changed?.Invoke();
    }
}
