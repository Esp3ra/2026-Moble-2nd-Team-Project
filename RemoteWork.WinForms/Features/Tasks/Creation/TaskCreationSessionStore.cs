using System.Runtime.CompilerServices;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Creation;

// Pending requests live only in memory, for one ApiClient and one authenticated session.
// Reopening a page must not discard an uncertain result or bypass a forbidden response.
internal static class TaskCreationSessionStore
{
    private static readonly ConditionalWeakTable<ApiClient, Holder> holders = new();

    public static TaskCreationWorkflow? Get(ApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return holders.GetValue(api, _ => new Holder()).Get(api);
    }

    private sealed class Holder
    {
        private readonly object gate = new();
        private Entry? current;

        public TaskCreationWorkflow? Get(ApiClient api)
        {
            while (true)
            {
                Entry? previous;
                lock (gate) previous = current;

                // Check the captured login, not Workflow.IsCurrent: a 403 disconnects
                // the workflow, but must remain blocked for this same login session.
                if (previous?.Session.IsCurrent == true) return previous.Workflow;

                ApiClient.Session? session;
                try { session = api.CaptureSession(); }
                catch (InvalidOperationException) { session = null; }

                Entry? next = session is null ? null : new Entry(session);
                if (next is not null && !next.Session.IsCurrent)
                {
                    next.Workflow.Dispose();
                    continue;
                }

                bool replaced;
                lock (gate)
                {
                    replaced = ReferenceEquals(current, previous);
                    if (replaced) current = next;
                }

                // Session checks, cancellation and UI notifications are outside gate.
                // A racing page can never replace an entry installed after its read.
                if (replaced)
                {
                    previous?.Workflow.Dispose();
                    return next?.Workflow;
                }
                next?.Workflow.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public Entry(ApiClient.Session session)
        {
            Session = session;
            Workflow = new TaskCreationWorkflow(new HttpTaskCreationService(session), new HttpTaskCreationUploadService(session));
        }

        public ApiClient.Session Session { get; }
        public TaskCreationWorkflow Workflow { get; }
    }
}
