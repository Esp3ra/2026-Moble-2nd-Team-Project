using System.Diagnostics;
using System.Reflection;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Starting;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static readonly DateTimeOffset Stamp = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        int result = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), Size = new(1, 1), StartPosition = FormStartPosition.Manual };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                ExplicitStart(); RecoveryAndRetry(); ClosingAndSession(); ForbiddenNotice(); PendingOutsideFilter();
                Console.WriteLine($"PASS TaskStart UI: {checks} checks; actual WinForms, explicit service doubles; no server/DB.");
                result = 0;
            }
            catch (Exception error) { Console.WriteLine($"FAIL after {checks}: {error.GetType().Name}: {error.Message}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return result;
    }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { Application.DoEvents(); Thread.Sleep(1); if (clock.ElapsedMilliseconds > 8000) throw new TimeoutException("UI state did not settle"); }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static TaskDetail Detail(TaskDisplayStatus status = TaskDisplayStatus.NotStarted) =>
        new(new("101", "확인한 업무", "담당자", status, new(2026, 10, 19), new(2026, 10, 21)), "설명");
    private static TaskStartRequest Request(TaskStartKind kind = TaskStartKind.Accept)
    {
        var detail = Detail(kind == TaskStartKind.Rework ? TaskDisplayStatus.Returned : TaskDisplayStatus.NotStarted);
        return TaskStartRequest.FromCheckedTarget(TaskStartTargetPreparation.Check(kind, detail,
            new("101", 7, new[] { 8 }, "7", 1, detail.Summary.Status, Stamp), 7).Target!);
    }
    private static void Ready(TaskAcceptanceForm form)
    { form.Show(); Until(() => Field<Button>(form, "btnConfirmTarget").Enabled); }
    private static void Execute(TaskAcceptanceForm form)
    {
        Field<Button>(form, "btnConfirmTarget").PerformClick();
        Field<Button>(form, "btnConfirmTarget").PerformClick();
        Application.DoEvents();
    }
    private static void ExplicitStart()
    {
        using var service = new StartDouble(); using var workflow = new TaskStartWorkflow(service);
        var prep = new PreparationDouble();
        using var form = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, prep, workflow);
        Ready(form);
        Check(service.Keys == 0 && service.Writes == 0, "Opening a connected form only reads context");
        Field<Button>(form, "btnConfirmTarget").PerformClick();
        Check(form.CheckedTarget is not null && service.Writes == 0, "First click confirms without writing");
        Field<Button>(form, "btnConfirmTarget").PerformClick(); Until(() => workflow.State == TaskStartState.Succeeded);
        Check(service.Keys == 1 && service.Writes == 1 && form.ShouldRefreshList, "Separate execution writes once and requests a read refresh");
        Check(!Field<Button>(form, "btnConfirmTarget").Enabled, "Successful form cannot execute another start");
        form.Close(); Check(workflow.IsCurrent && workflow.PendingRequest is not null, "Closing borrows workflow and retains success until parent refresh");
    }
    private static void RecoveryAndRetry()
    {
        using var service = new StartDouble { FailFirstWrite = true }; using var workflow = new TaskStartWorkflow(service);
        var prep = new PreparationDouble(TaskDisplayStatus.Returned);
        using (var first = new TaskAcceptanceForm(Detail(TaskDisplayStatus.Returned), TaskStartKind.Rework, prep, workflow))
        {
            Ready(first); Execute(first); Until(() => workflow.State == TaskStartState.NeedsResultCheck);
            Check(service.Writes == 1 && !workflow.CanRetry, "Uncertain response does not automatically retry");
            first.Close();
        }
        int contextReads = prep.Reads;
        using var recovered = new TaskAcceptanceForm(workflow, prep);
        recovered.Show(); Application.DoEvents();
        Check(prep.Reads == contextReads && Field<TextBox>(recovered, "txtTaskId").Text == "101", "Recovery uses the retained request without a new or invented task detail");
        Field<Button>(recovered, "btnReloadContext").PerformClick(); Until(() => workflow.CanRetry);
        Check(service.Writes == 1 && service.Lookups == 1, "Not-recorded result alone enables explicit same-request retry");
        Field<Button>(recovered, "btnConfirmTarget").PerformClick(); Until(() => workflow.State == TaskStartState.Succeeded);
        Check(service.Keys == 1 && service.Writes == 2 && ReferenceEquals(service.Requests[0], service.Requests[1])
            && ReferenceEquals(service.RequestKeys[0], service.RequestKeys[1]), "Retry preserves the exact request and key");
        recovered.Close();
    }
    private static void ClosingAndSession()
    {
        using var service = new StartDouble { Deferred = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var workflow = new TaskStartWorkflow(service);
        using var form = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, new PreparationDouble(), workflow);
        Ready(form); Execute(form); Until(() => workflow.IsBusy);
        form.Close(); Check(!form.IsDisposed && form.Visible, "Ordinary close waits while a mutation is in flight");
        service.EndSession(); Application.DoEvents();
        Check(workflow.PendingRequest is null && form.CheckedTarget is null, "Session change discards prior request and target");
        Check(Field<TextBox>(form, "txtTaskTitle").Text == "" && Field<TextBox>(form, "txtTaskId").Text == "", "Previous login fields are cleared");
        service.Deferred.SetResult(StartDouble.Success()); Application.DoEvents();
        Check(workflow.State == TaskStartState.Disconnected && !form.ShouldRefreshList, "Late response cannot mark a replaced login successful");
        form.Close();
        using var racedRecovery = new TaskAcceptanceForm(workflow, new PreparationDouble());
        racedRecovery.Show(); Application.DoEvents();
        Check(Field<TextBox>(racedRecovery, "txtTaskId").Text == ""
            && !Field<Button>(racedRecovery, "btnConfirmTarget").Enabled,
            "Session cancellation between pending selection and recovery construction is safe");
        Check(service.Keys == 1 && service.Writes == 1, "Expired recovery creates no replacement request");
        racedRecovery.Close();
    }
    private static void ForbiddenNotice()
    {
        using var service = new StartDouble { FailFirstWrite = true, FirstFailure = TaskStartFailure.Forbidden };
        using var workflow = new TaskStartWorkflow(service);
        using var form = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, new PreparationDouble(), workflow);
        Ready(form); Execute(form); Until(() => workflow.State == TaskStartState.Disconnected);
        Check(Field<Label>(form, "lblPreparationStatus").Text.Contains("권한이 없습니다")
            && !Field<Label>(form, "lblPreparationStatus").Text.Contains("만료"), "A definite 403 keeps its permission-denied explanation");
        Check(!Field<Button>(form, "btnConfirmTarget").Enabled && form.CheckedTarget is null,
            "Permission denial blocks the old target and execution");
        form.Close();
    }
    private static void PendingOutsideFilter()
    {
        using var service = new StartDouble { FailFirstWrite = true, LookupCompleted = true };
        using var workflow = new TaskStartWorkflow(service);
        workflow.SubmitAsync(Request()).GetAwaiter().GetResult();
        var source = new QueryDouble(workflow);
        using var host = new Form { Size = new(1200, 850) };
        using var control = new TasksControl(PreviewRole.Employee, source, new PreparationDouble()) { Dock = DockStyle.Fill };
        host.Controls.Add(control); host.Show();
        var filter = Field<ComboBox>(control, "cmbTaskStatusFilter"); filter.SelectedIndex = 1;
        Until(() => Field<Button>(control, "btnAcceptTask").Enabled);
        Check(Field<DataGridView>(control, "gridTasks").Rows.Count == 0, "Pending recovery is available with no selected or visible row");
        Exception? modalError = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            var modal = Application.OpenForms.OfType<TaskAcceptanceForm>().FirstOrDefault();
            if (modal is null) return;
            try
            {
                if (workflow.State == TaskStartState.NeedsResultCheck)
                    Field<Button>(modal, "btnReloadContext").PerformClick();
                if (workflow.State == TaskStartState.Succeeded) { timer.Stop(); modal.Close(); }
            }
            catch (Exception error) { modalError = error; timer.Stop(); modal.Close(); }
        };
        timer.Start(); Field<Button>(control, "btnAcceptTask").PerformClick();
        Until(() => workflow.State == TaskStartState.Editing);
        if (modalError is not null) throw modalError;
        Check(source.Reads >= 2 && filter.SelectedIndex == 1, "Success refreshes the existing scope/filter before acknowledgement");
        Check(service.Writes == 1 && service.Lookups == 1 && workflow.PendingRequest is null, "Recovery does not create another write and parent acknowledges known success");
        host.Close();
    }
    private sealed class PreparationDouble(TaskDisplayStatus status = TaskDisplayStatus.NotStarted) : ITaskPreparationService
    {
        public int Reads; public bool IsCurrent => true; public bool CanSelectCandidates => false; public int UserId => 7;
        public CancellationToken SessionCancellation => CancellationToken.None;
        public Task<TaskWriteContext> GetContextAsync(string id, CancellationToken ct = default)
        { Reads++; return Task.FromResult(new TaskWriteContext(id, 7, new[] { 8 }, "7", 1, status, Stamp)); }
        public Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery query, CancellationToken ct = default) => throw new InvalidOperationException("No candidate reads expected");
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? id, CancellationToken ct = default) => throw new InvalidOperationException("No preparation key calls expected");
        public Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default) => throw new InvalidOperationException("No preparation result calls expected");
    }
    private sealed class StartDouble : ITaskStartService, IDisposable
    {
        private readonly CancellationTokenSource session = new();
        public int Keys, Writes, Lookups; public bool FailFirstWrite, LookupCompleted;
        public TaskStartFailure FirstFailure = TaskStartFailure.Unavailable;
        public TaskCompletionSource<TaskStartResult>? Deferred;
        public List<TaskStartRequest> Requests = []; public List<TaskStartKey> RequestKeys = [];
        public bool IsCurrent => !session.IsCancellationRequested; public int UserId => 7; public CancellationToken SessionCancellation => session.Token;
        public Task<TaskStartKey> GetRequestKeyAsync(TaskStartRequest request, CancellationToken ct = default)
        { Keys++; return Task.FromResult(new TaskStartKey(request.Kind, request.TaskId, new string('x', 40), Stamp.AddDays(1))); }
        public Task<TaskStartResult> StartAsync(TaskStartRequest request, TaskStartKey key, CancellationToken ct = default)
        {
            Writes++; Requests.Add(request); RequestKeys.Add(key);
            if (Deferred is not null) return Deferred.Task;
            if (FailFirstWrite && Writes == 1) throw new TaskStartException(FirstFailure);
            return Task.FromResult(Success());
        }
        public Task<TaskStartLookup> GetResultAsync(TaskStartRequest request, TaskStartKey key, CancellationToken ct = default)
        { Lookups++; return Task.FromResult(new TaskStartLookup(LookupCompleted, LookupCompleted ? Success() : null)); }
        public static TaskStartResult Success() => new("101", "8", 1, TaskDisplayStatus.InProgress, Stamp);
        public void EndSession() => session.Cancel(); public void Dispose() => session.Dispose();
    }
    private sealed class QueryDouble(TaskStartWorkflow workflow) : ITaskQueryService, ITaskStartSource
    {
        public int Reads; public string SourceDescription => "명시적 목록 테스트 대역";
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken ct = default)
        { Reads++; return Task.FromResult<IReadOnlyList<TaskSummary>>(Array.Empty<TaskSummary>()); }
        public Task<TaskDetail> GetTaskAsync(string id, CancellationToken ct = default) => throw new InvalidOperationException("Empty list has no detail");
        public TaskStartWorkflow? GetStartWorkflow(TaskStartKind kind, string id) => workflow;
        public TaskStartWorkflow? GetPendingStartWorkflow() => workflow.PendingRequest is null ? null : workflow;
    }
}
