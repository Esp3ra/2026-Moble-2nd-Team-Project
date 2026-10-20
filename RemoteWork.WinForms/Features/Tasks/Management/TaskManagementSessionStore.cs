using System.Runtime.CompilerServices;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Management;

// Weakly owned by ApiClient. Pending work survives navigation, never a login change.
public static class TaskManagementSessionStore
{
    private static readonly ConditionalWeakTable<ApiClient, Holder> holders = new();
    public static TaskManagementWorkflow? Get(ApiClient api, TaskManagementKind kind, string taskId)
    {
        ArgumentNullException.ThrowIfNull(api);
        if (!Enum.IsDefined(kind) || !TaskManagementFormat.IsId(taskId))
            throw new ArgumentException("유효한 업무 변경 대상이 필요합니다.");
        return holders.GetValue(api, _ => new Holder()).Get(api, kind, taskId);
    }
    public static TaskManagementWorkflow? FindPending(ApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return holders.TryGetValue(api, out var holder) ? holder.FindPending() : null;
    }

    private sealed class Holder
    {
        private readonly object gate = new();
        private Entry? current;
        public TaskManagementWorkflow? Get(ApiClient api, TaskManagementKind kind, string taskId)
        {
            while (true)
            {
                Entry? previous;
                lock (gate) previous = current;
                // Do not use workflow.IsCurrent: 403 stays blocked for the same login.
                if (previous?.Session.IsCurrent == true) return previous.Get(kind, taskId);
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
                    return next?.Get(kind, taskId);
                }
                next?.Dispose();
            }
        }
        public TaskManagementWorkflow? FindPending()
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
        private readonly Dictionary<(TaskManagementKind Kind, string TaskId), TaskManagementWorkflow> workflows = new();
        private bool disposed;
        public ApiClient.Session Session { get; } = session;
        public TaskManagementWorkflow? Get(TaskManagementKind kind, string taskId)
        {
            lock (gate)
            {
                if (disposed) return null;
                if (!workflows.TryGetValue((kind, taskId), out var workflow))
                {
                    workflow = new TaskManagementWorkflow(new HttpTaskManagementService(Session), kind, taskId);
                    workflows.Add((kind, taskId), workflow);
                }
                return workflow;
            }
        }
        public TaskManagementWorkflow? FindPending()
        {
            TaskManagementWorkflow[] copy;
            lock (gate) copy = workflows.Values.ToArray();
            foreach (var workflow in copy)
            {
                var snapshot = workflow.Snapshot;
                if (snapshot.PendingRequest is not null && snapshot.State is TaskManagementState.Sending
                    or TaskManagementState.NeedsResultCheck or TaskManagementState.RetryAllowed or TaskManagementState.Succeeded or TaskManagementState.Blocked)
                    return workflow;
            }
            return null;
        }
        public void Dispose()
        {
            TaskManagementWorkflow[] copy;
            lock (gate) { disposed = true; copy = workflows.Values.ToArray(); workflows.Clear(); }
            foreach (var workflow in copy) workflow.Dispose();
        }
    }
}
