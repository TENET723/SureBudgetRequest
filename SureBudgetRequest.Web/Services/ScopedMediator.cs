using System.Runtime.CompilerServices;
using MediatR;
using SureBudgetRequest.Application.Abstractions;

namespace SureBudgetRequest.Web.Services;

/// <summary>
/// IMediator decorator that runs every Send/Publish in its OWN DI scope.
///
/// Why: in Blazor Server a DI scope == the SignalR circuit, so the scoped AppDbContext
/// is a single instance for the whole browser session. When the user navigates quickly,
/// one page's OnInitializedAsync is still awaiting queries while the next page starts
/// new ones — both handlers hit the SAME DbContext and EF Core throws
/// "A second operation was started on this context instance...".
///
/// By creating a fresh scope per operation, every command/query handler resolves its
/// own AppDbContext, eliminating the collision. All MediatR pipeline behaviors still
/// run — the inner Mediator resolves handlers and behaviors from the child scope.
///
/// Notes:
/// - The inner mediator is constructed directly (new Mediator(scope.ServiceProvider))
///   rather than resolved as IMediator, because IMediator in the container maps back
///   to THIS decorator and would recurse. Requires MediatR 12+.
/// - The circuit's current user is bridged into the child scope via CurrentUserSnapshot,
///   since the child scope has no circuit AuthenticationStateProvider.
/// - A nested Send inside a handler gets its own scope (and own DbContext); handlers
///   must not rely on sharing the caller's context/transaction across nested sends.
/// </summary>
public sealed class ScopedMediator : IMediator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUser _circuitUser;

    public ScopedMediator(IServiceScopeFactory scopeFactory, ICurrentUser circuitUser)
    {
        _scopeFactory = scopeFactory;
        _circuitUser = circuitUser;
    }

    private (AsyncServiceScope Scope, IMediator Inner) CreateOperationScope()
    {
        var scope = _scopeFactory.CreateAsyncScope();

        // Bridge the circuit user into the child scope BEFORE any handler
        // resolves ICurrentUser (the ICurrentUser factory registration in
        // Program.cs prefers the snapshot whenever it has been populated).
        scope.ServiceProvider.GetRequiredService<CurrentUserSnapshot>().CopyFrom(_circuitUser);

        return (scope, new Mediator(scope.ServiceProvider));
    }

    // ── ISender ────────────────────────────────────────────────────────────────

    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            return await inner.Send(request, cancellationToken);
        }
    }

    public async Task Send<TRequest>(
        TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            await inner.Send(request, cancellationToken);
        }
    }

    public async Task<object?> Send(
        object request, CancellationToken cancellationToken = default)
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            return await inner.Send(request, cancellationToken);
        }
    }

    // ── IPublisher ─────────────────────────────────────────────────────────────

    public async Task Publish(
        object notification, CancellationToken cancellationToken = default)
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            await inner.Publish(notification, cancellationToken);
        }
    }

    public async Task Publish<TNotification>(
        TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            await inner.Publish(notification, cancellationToken);
        }
    }

    // ── Streams ────────────────────────────────────────────────────────────────
    // The scope must stay alive for the lifetime of the stream, so these are
    // implemented as async iterators that dispose the scope when iteration ends.

    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            await foreach (var item in inner.CreateStream(request, cancellationToken))
            {
                yield return item;
            }
        }
    }

    public async IAsyncEnumerable<object?> CreateStream(
        object request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (scope, inner) = CreateOperationScope();
        await using (scope)
        {
            await foreach (var item in inner.CreateStream(request, cancellationToken))
            {
                yield return item;
            }
        }
    }
}
