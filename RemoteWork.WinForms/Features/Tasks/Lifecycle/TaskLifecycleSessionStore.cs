using System.Runtime.CompilerServices;

using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Lifecycle;

// Weakly owned by ApiClient. Pending work survives navigation, never a login change.
public static class TaskLifecycleSessionStore
{
    private static readonly ConditionalWeakTable<ApiClient, Holder> holders = new();
    public static TaskLifecycleWorkflow? Get(ApiClient api, TaskLifecycleKind kind, string taskId, string? extensionId = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        if (!Enum.IsDefined(kind) || !TaskLifecycleFormat.IsId(taskId))
            throw new ArgumentException("유효한 업무 변경 대상이 필요합니다.");
        if (kind is TaskLifecycleKind.Cancel or TaskLifecycleKind.RequestExtension
            ? extensionId is not null : !TaskLifecycleFormat.IsId(extensionId))
            throw new ArgumentException("현재 연장 신청 대상을 확인해 주세요.");
        return holders.GetValue(api, _ => new Holder()).Get(api, kind, taskId, extensionId);
    }
    public static TaskLifecycleWorkflow? FindPending(ApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return holders.TryGetValue(api, out var holder) ? holder.FindPending() : null;
    }

    private sealed class Holder
    {
        private readonly object gate = new();
        private Entry? current;
        public TaskLifecycleWorkflow? Get(ApiClient api, TaskLifecycleKind kind, string taskId, string? extensionId = null)
        {
            while (true)
            {
                Entry? previous;
                lock (gate) previous = current;
                // Do not use workflow.IsCurrent: 403 stays blocked for the same login.
                if (previous?.Session.IsCurrent == true) return previous.Get(kind, taskId, extensionId);
                ApiClient.Session? session;
                try { session = api.CaptureSession(); }
                catch (InvalidOperationException) { session = null; }
                var next = session is null ? null : new Entry(session);
                if (next is not null && !next.Session.IsCurrent) { next.Dispose(); continue; }
                bool replaced;
                lock (gate)
                {
                    replaced = ReferenceEquals(current, previous);
                    if (replaced) current = next;
                }
                if (replaced)
                {
                    previous?.Dispose();
                    return next?.Get(kind, taskId, extensionId);
                }
                next?.Dispose();
            }
        }
        public TaskLifecycleWorkflow? FindPending()
        {
            Entry? entry;
            lock (gate) entry = current;
            if (entry?.Session.IsCurrent != true) return null;
            return entry.FindPending();
        }
    }
    private sealed class Entry(ApiClient.Session session) : IDisposable
    {
        private readonly object gate = new();
        private readonly Dictionary<(TaskLifecycleKind Kind, string TaskId, string? ExtensionId), TaskLifecycleWorkflow> workflows = new();
        private bool disposed;
        public ApiClient.Session Session { get; } = session;
        public TaskLifecycleWorkflow? Get(TaskLifecycleKind kind, string taskId, string? extensionId = null)
        {
            lock (gate)
            {
                if (disposed) return null;
                if (!workflows.TryGetValue((kind, taskId, extensionId), out var workflow))
                {
                    workflow = new TaskLifecycleWorkflow(new HttpTaskLifecycleService(Session), kind, taskId, extensionId);
                    workflows.Add((kind, taskId, extensionId), workflow);
                }
                return workflow;
            }
        }
        public TaskLifecycleWorkflow? FindPending()
        {
            TaskLifecycleWorkflow[] copy;
            lock (gate) copy = workflows.Values.ToArray();
            foreach (var workflow in copy)
            {
                var snapshot = workflow.Snapshot;
                if (snapshot.PendingRequest is not null && snapshot.State is TaskLifecycleState.Sending
                    or TaskLifecycleState.NeedsResultCheck or TaskLifecycleState.RetryAllowed or TaskLifecycleState.Succeeded or TaskLifecycleState.Blocked)
                    return workflow;
            }
            return null;
        }
        public void Dispose()
        {
            TaskLifecycleWorkflow[] copy;
            lock (gate) { disposed = true; copy = workflows.Values.ToArray(); workflows.Clear(); }
            foreach (var workflow in copy) workflow.Dispose();
        }
    }
}
