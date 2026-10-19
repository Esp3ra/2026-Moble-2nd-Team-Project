namespace RemoteWork.WinForms.Infrastructure;

/// <summary>
/// Optional view helper. Does not own sensors, change authentication or start work.
/// A view supplies its UI dispatcher and lifetime predicate, and disposes this scope.
/// </summary>
public sealed class FeatureUiScope : IDisposable
{
    private readonly ApiClient.Session session;
    private readonly Action<Action> post;
    private readonly Func<bool> isViewAlive;
    private readonly object gate = new();
    private CancellationTokenSource? active;
    private long generation;
    private bool disposed;

    public FeatureUiScope(ApiClient.Session session, Action<Action> post, Func<bool> isViewAlive)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.post = post ?? throw new ArgumentNullException(nameof(post));
        this.isViewAlive = isViewAlive ?? throw new ArgumentNullException(nameof(isViewAlive));
    }

    /// <summary>One load/selection/work-session generation; cancels the previous one.</summary>
    public Operation BeginOperation()
    {
        session.ThrowIfChanged();
        CancellationTokenSource? previous;
        Operation operation;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            previous = active;
            active = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation);
            operation = new Operation(this, ++generation, active.Token);
        }
        // No ApiClient lock or UI dispatch is held while callbacks run.
        CancelAndDispose(previous);
        return operation;
    }

    private bool IsCurrent(Operation operation)
    {
        bool current;
        lock (gate)
            current = !disposed && ReferenceEquals(operation.Owner, this)
                && operation.Generation == generation && !operation.Cancellation.IsCancellationRequested;
        // Avoid gate -> ApiClient lock inversion with auth-expired callbacks.
        return current && session.IsCurrent;
    }

    /// <summary>
    /// False means stale before dispatch. True means queued, not necessarily applied.
    /// Rechecks after the callback reaches the UI queue. Call from the view's UI
    /// thread for updates that change its selection/lifetime.
    /// </summary>
    public bool TryPost(Operation operation, Action update)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(update);
        if (!IsCurrent(operation)) return false;
        bool updating = false;
        try
        {
            post(() =>
            {
                if (IsCurrent(operation) && isViewAlive())
                {
                    updating = true;
                    update();
                }
            });
            return true;
        }
        catch (ObjectDisposedException) when (!updating) { return false; }
        catch (InvalidOperationException) when (!updating) { return false; } // Destroyed dispatcher, not user-update bugs.
    }

    public void Dispose()
    {
        CancellationTokenSource? source;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            generation++;
            source = active;
            active = null;
        }
        CancelAndDispose(source);
    }

    private static void CancelAndDispose(CancellationTokenSource? source)
    {
        if (source is null) return;
        // Invalidate first, cancel outside locks; caller callbacks must not throw.
        try { source.Cancel(); }
        catch (ObjectDisposedException) { /* A concurrent Dispose already released it. */ }
        finally { source.Dispose(); }
    }

    public sealed class Operation
    {
        internal FeatureUiScope Owner { get; }
        internal long Generation { get; }
        public CancellationToken Cancellation { get; }
        internal Operation(FeatureUiScope owner, long generation, CancellationToken cancellation)
        { Owner = owner; Generation = generation; Cancellation = cancellation; }
        public bool IsCurrent => Owner.IsCurrent(this);
    }
}
