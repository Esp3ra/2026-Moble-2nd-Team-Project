using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TasksControl
{
    /// <summary>
    /// Capture an own-task navigation callback for this login and this control.
    /// Create and invoke it on the control's UI thread after its handle exists.
    /// The caller owns event subscription/disposal; this method starts no reads.
    /// An old callback cannot adopt a subsequent login, even for the same user.
    /// </summary>
    public Func<string, Task<bool>> CaptureOwnTaskNavigator()
    {
        if (queriesDisposed || IsDisposed || !IsHandleCreated || InvokeRequired
            || queryService is not HttpTaskQueryService http)
            throw new InvalidOperationException("로그인된 업무 화면의 UI 스레드에서 탐색을 연결해 주세요.");
        TaskQuerySession captured = http.CaptureViewSession()
            ?? throw new InvalidOperationException("업무 탐색을 연결하려면 로그인이 필요합니다.");
        captured.Session.ThrowIfChanged();
        return taskId => NavigateOwnTaskAsync(taskId, captured);
    }

    private Task<bool> NavigateOwnTaskAsync(string taskId, TaskQuerySession captured)
    {
        // Keep the original string for transport/selection, including IDs > 2^53.
        // Do not change the ordinary query boundary's opaque-ID contract.
        if (queriesDisposed || IsDisposed || !IsHandleCreated || InvokeRequired
            || !captured.Session.IsCurrent || initializing
            || !long.TryParse(taskId, NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            || id <= 0 || id.ToString(CultureInfo.InvariantCulture) != taskId)
            return Task.FromResult(false);

        // This is an explicit move to the own-task screen. A managed-only task
        // is never inserted into this list or used to enable own-task actions.
        initializing = true;
        try
        {
            cmbTaskScope.SelectedIndex = 0;
            cmbTaskStatusFilter.SelectedIndex = 0;
            chkIncludeCancelled.Checked = false;
        }
        finally { initializing = false; }
        preferredTaskId = null;
        createdTaskIdForRefresh = null;
        return RefreshCoreAsync(captured, taskId);
    }
}
