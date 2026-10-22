using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static string? capture;
    private static readonly DateOnly Day = new(2026, 10, 22);
    private static readonly TaskDetail Detail = new(new("9", "원래 업무", "직원", TaskDisplayStatus.NotStarted, Day, Day.AddDays(3)), "원래 설명");
    private static TaskWriteContext Context(string version = "1") => new("9", 7, new[] { 2 }, version, 3,
        TaskDisplayStatus.NotStarted, new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero));

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        capture = args.FirstOrDefault();
        int result = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000),
            StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                Run(PreviewAndExistingExclusion); Run(UploadAndLayout); Run(ExplicitDeleteTarget);
                Run(ParentConfirmationAndRecovery); Run(ParentExplicitEmptySave); Run(BaselineFenceAndProviderFailure);
                Run(DirtyVersionReset); Run(ExplicitSameVersionReset); Run(ParentAuthorityRevocation); Run(BusySessionLateResponse);
                Run(DefiniteFailurePreservesNewInput);
                Run(StaleGenerationContextDenied); Run(ClosedFormContextDenied);
                Console.WriteLine($"PASS management attachment UI: {checks} checks; actual WinForms and explicit HTTP/dialog doubles.");
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL management attachment UI after {checks}: {error.GetType().Name}: {error.Message}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return result;
    }
    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
    private static void Run(Action action) { action(); Console.WriteLine("PASS " + action.Method.Name); }
    private static T Field<T>(object target, string name)
    {
        for (Type? type = target.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) is { } field) return (T)field.GetValue(target)!;
        throw new MissingFieldException(name);
    }
    private static Button Button(object form, string name) => Field<Button>(form, name);
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new(-16000, -16000); form.Show(); Application.DoEvents(); }
    private static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Controlled UI operation did not finish");
        task.GetAwaiter().GetResult(); Application.DoEvents();
    }
    private static T Wait<T>(Task<T> task) { Wait((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> ready)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!ready() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), "UI reaches expected stable state"); Application.DoEvents();
    }
    private static void Idle(Picker form, Fixture f) => Until(() => !f.Selection.IsBusy && !Field<bool>(form, "operating"));
    private static void Select(Picker form, Guid key)
    {
        var grid = Field<DataGridView>(form, "gridFiles");
        grid.CurrentCell = grid.Rows.Cast<DataGridViewRow>().Single(x => Equals(x.Tag, key)).Cells[0];
        Application.DoEvents();
    }
    private static Guid AddUpload(Picker form, Fixture f, string name)
    {
        int count = f.Selection.Items.Count, before = f.Handler.Requests.Count;
        form.FilePath = f.File(name); Button(form, "btnAdd").PerformClick();
        Until(() => f.Selection.Items.Count == count + 1); Idle(form, f);
        Check(f.Handler.Requests.Count == before, "file choice does not upload automatically");
        Guid key = f.Selection.Items.Last().Key; Select(form, key);
        Check(Button(form, "btnUpload").Enabled && !f.Selection.IsReady, "unuploaded selection blocks task save and enables explicit upload");
        Button(form, "btnUpload").PerformClick();
        Until(() => f.Selection.Items.Single(x => x.Key == key).State == TaskCreationUploadState.Completed); Idle(form, f);
        return key;
    }
    private static void Capture(Form form, string name)
    {
        if (capture is null) return;
        Directory.CreateDirectory(capture);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(Path.Combine(capture, name));
    }
    private static void PreviewAndExistingExclusion()
    {
        using (var preview = new TaskManagementAttachmentsForm())
        {
            Show(preview);
            Check(!Button(preview, "btnAdd").Enabled && !Button(preview, "btnDelete").Enabled && Button(preview, "btnClose").Enabled,
                "designer preview has no upload or delete capability");
        }
        using var f = new Fixture(); f.Bind(); using var picker = new Picker(f.Selection); Show(picker);
        Check(f.Selection.CompletedIds is null && f.Selection.Items.Single().IsExisting, "unchanged current files retain omission semantics");
        Check(Button(picker, "btnRemove").Enabled && Button(picker, "btnRemove").Text == "업무에서 제외" && !Button(picker, "btnDelete").Enabled,
            "existing reference offers task exclusion and never server DELETE");
        int before = f.Handler.Requests.Count;
        picker.AcceptRemoval = false; Button(picker, "btnRemove").PerformClick();
        Check(f.Selection.Items.Count == 1 && f.Selection.CompletedIds is null, "cancelled task exclusion preserves original omission");
        picker.AcceptRemoval = true; Button(picker, "btnRemove").PerformClick();
        Check(f.Selection.Items.Count == 0 && f.Selection.CompletedIds is { Count: 0 } && picker.ConfirmedExisting,
            "confirmed exclusion becomes an explicit empty final list");
        Check(f.Handler.Requests.Count == before && f.Handler.Deletes.Count == 0, "reference exclusion is local until task PATCH and sends no DELETE");
        picker.Close(); Check(f.Handler.Deletes.Count == 0, "closing excluded selection does not delete server files");
    }
    private static void UploadAndLayout()
    {
        using var f = new Fixture(); f.Bind();
        using (var picker = new Picker(f.Selection))
        {
            Show(picker); Guid key = AddUpload(picker, f, "업무수정_긴파일명_첨부상태와최종목록검증.bin");
            var start = f.Handler.Requests.Single(x => x.Path.EndsWith("/uploads/start"));
            using var body = JsonDocument.Parse(start.Body);
            Check(body.RootElement.GetProperty("task_id").GetString() == "9", "new file upload remains bound to the edited task");
            var finalIds = f.Selection.CompletedIds;
            Check(finalIds is { Count: 2 } && finalIds.First() == Handler.ExistingId,
                "existing reference and completed upload form one ordered final list");
            Select(picker, key);
            Check(Button(picker, "btnDelete").Enabled && !Button(picker, "btnRemove").Enabled, "completed temporary file uses explicit deletion instead of silent drop");
            Capture(picker, "management-attachments-standard.png");
            picker.Size = picker.MinimumSize; picker.PerformLayout(); Application.DoEvents();
            var actions = Field<FlowLayoutPanel>(picker, "actions");
            Check(Button(picker, "btnClose").Bottom <= actions.ClientSize.Height && Field<DataGridView>(picker, "gridFiles").Height >= 70,
                "minimum picker size keeps actions and file rows visible");
            Capture(picker, "management-attachments-minimum.png");
            picker.Close();
        }
        using var reopened = new Picker(f.Selection); Show(reopened);
        Check(Field<DataGridView>(reopened, "gridFiles").Rows.Count == 2 && f.Handler.Deletes.Count == 0,
            "same task picker reopens with retained selection and no automatic discard");
    }
    private static void ExplicitDeleteTarget()
    {
        using var f = new Fixture(); f.Bind(); using var picker = new Picker(f.Selection); Show(picker);
        Guid first = AddUpload(picker, f, "first.bin"), second = AddUpload(picker, f, "second.bin");
        string firstId = f.Selection.CompletedIds![1]; Select(picker, first);
        picker.AcceptDeletion = false; Button(picker, "btnDelete").PerformClick();
        Check(f.Handler.Deletes.Count == 0, "cancelled temporary delete sends no request");
        picker.AcceptDeletion = true; picker.DuringConfirmation = () => Select(picker, second);
        Button(picker, "btnDelete").PerformClick(); Until(() => f.Selection.Items.Count == 2); Idle(picker, f);
        Check(picker.ConfirmedKey == first && f.Handler.Deletes.Single().Path.EndsWith("/" + firstId),
            "changing selected row during confirmation cannot redirect DELETE");
        Check(f.Selection.Items.Any(x => x.IsExisting) && f.Selection.Items.Any(x => x.Key == second),
            "delete preserves linked file and other temporary selection");
    }
    private static void ParentConfirmationAndRecovery()
    {
        using var f = new Fixture(); f.Bind();
        using (var picker = new Picker(f.Selection)) { Show(picker); AddUpload(picker, f, "pending.bin"); }
        using (var main = new MainEditor(f))
        {
            Show(main); Until(() => Button(main, "btnConfirm").Enabled);
            Button(main, "btnConfirm").PerformClick(); Until(() => Button(main, "btnConfirm").Text == "변경 실행");
            Check(f.Handler.Changes.Count == 0, "input confirmation does not mutate task");
            main.PickerAction = selection => selection.RemoveLocal(selection.Items.Single(x => x.IsExisting).Key);
            Button(main, "btnAttachments").PerformClick();
            Check(main.PickerCalls == 1 && Button(main, "btnConfirm").Text == "입력·대상 확인", "attachment change invalidates a previously checked task request");
            Button(main, "btnConfirm").PerformClick(); Until(() => Button(main, "btnConfirm").Text == "변경 실행");
            Capture(main, "management-update-attachments-standard.png");
            main.Size = main.MinimumSize; main.PerformLayout(); Application.DoEvents();
            foreach (string name in new[] { "btnAttachments", "btnReload", "btnConfirm", "btnCheckResult", "btnAcknowledge", "btnClose" })
            {
                var button = Button(main, name);
                Rectangle shown = main.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                Check(button.Visible && main.ClientRectangle.Contains(shown), "main minimum layout keeps button inside actual form: " + name);
            }
            var host = Field<Panel>(main, "fieldsHost");
            var inputs = new[] { "txtTitle", "txtDescription", "txtStartDate", "txtDueDate" }
                .Select(name => Field<TextBox>(main, name)).ToArray();
            Check(host.AutoScroll && host.VerticalScroll.Visible, "minimum input viewport exposes vertical scrolling");
            Console.WriteLine($"LAYOUT minimum form={main.Size} fields viewport={host.ClientSize} vertical max={host.VerticalScroll.Maximum} large_change={host.VerticalScroll.LargeChange}");
            for (int i = 0; i < inputs.Length; i++)
            {
                TextBox input = inputs[i];
                host.ScrollControlIntoView(input); Application.DoEvents();
                Rectangle shown = host.RectangleToClient(input.RectangleToScreen(input.ClientRectangle));
                Console.WriteLine($"LAYOUT {input.Name} size={input.Size} visible={Rectangle.Intersect(host.ClientRectangle, shown).Size}");
                Check(Rectangle.Intersect(host.ClientRectangle, shown).Height >= Math.Min(24, input.ClientSize.Height),
                    "minimum viewport can scroll to each input: " + input.Name);
                for (int j = 0; j < i; j++)
                    Check(!input.Bounds.IntersectsWith(inputs[j].Bounds), "input rows do not overlap: " + input.Name + "/" + inputs[j].Name);
            }
            Capture(main, "management-update-attachments-minimum-dates.png");
            host.AutoScrollPosition = Point.Empty; Application.DoEvents();
            Capture(main, "management-update-attachments-minimum.png");
            f.Handler.DropChange = true; Button(main, "btnConfirm").PerformClick(); Until(() => f.Workflow.State == TaskManagementState.NeedsResultCheck);
            Check(f.Workflow.PendingRequest?.AttachmentIds?.Count == 1 && !Button(main, "btnConfirm").Enabled,
                "uncertain PATCH retains exactly the checked final attachment list");
            main.Close();
        }
        using var frozen = new Picker(f.Selection); Show(frozen);
        Check(!Button(frozen, "btnAdd").Enabled && !Button(frozen, "btnDelete").Enabled && Field<DataGridView>(frozen, "gridFiles").Rows.Count == 1,
            "reopened unknown-result picker displays original file but locks all mutations");
        using var recovery = new TaskManagementForm(f.Workflow, f.Queries, f.Preparation); Show(recovery);
        Check(Field<TextBox>(recovery, "txtTitle").Text == Detail.Summary.Title && Field<TextBox>(recovery, "txtDescription").Text == Detail.Description
            && Field<TextBox>(recovery, "txtTitle").ReadOnly, "recovery window restores original checked body as read-only");
        f.Handler.NotRecorded = true; Button(recovery, "btnCheckResult").PerformClick(); Until(() => f.Workflow.CanRetry);
        Check(!Button(frozen, "btnDelete").Enabled, "not_recorded does not unlock file selection before the original retry");
        Button(recovery, "btnConfirm").PerformClick(); Until(() => f.Workflow.State == TaskManagementState.Succeeded);
        Check(f.Handler.Changes.Count == 2 && f.Handler.Changes[0].Body == f.Handler.Changes[1].Body && f.Handler.Changes[0].Key == f.Handler.Changes[1].Key,
            "parent explicit retry sends identical IDs body and key");
        Check(f.Handler.Deletes.Count == 0 && f.Selection.Items.Count == 0, "confirmed success clears local selection without server deletion");
    }
    private static void ParentExplicitEmptySave()
    {
        using var f = new Fixture(); f.Bind(); using var main = new MainEditor(f); Show(main);
        Until(() => Button(main, "btnAttachments").Enabled);
        main.PickerAction = selection => selection.RemoveLocal(selection.Items.Single().Key);
        Button(main, "btnAttachments").PerformClick();
        Button(main, "btnConfirm").PerformClick(); Until(() => Button(main, "btnConfirm").Text == "변경 실행");
        Check(f.Handler.Changes.Count == 0 && f.Selection.CompletedIds is { Count: 0 }, "empty final list still requires separate task execution");
        Button(main, "btnConfirm").PerformClick(); Until(() => f.Workflow.State == TaskManagementState.Succeeded);
        using var body = JsonDocument.Parse(f.Handler.Changes.Single().Body);
        Check(body.RootElement.GetProperty("attachment_ids").GetArrayLength() == 0 && f.Handler.Deletes.Count == 0,
            "parent sends explicit empty final list by PATCH and never deletes linked server files");
    }
    private static void BaselineFenceAndProviderFailure()
    {
        using var f = new Fixture(); using var main = new MainEditor(f); Show(main);
        Until(() => Button(main, "btnAttachments").Enabled);
        f.Preparation.Versions.Enqueue("1"); f.Preparation.Versions.Enqueue("2");
        Button(main, "btnAttachments").PerformClick(); Until(() => !Field<bool>(main, "busy"));
        Check(!f.Selection.Loaded && main.PickerCalls == 0 && f.Handler.Changes.Count == 0,
            "context change around attachment read prevents stale files from binding to newer task version");
        f.Handler.ListUnavailable = true;
        Button(main, "btnAttachments").PerformClick(); Until(() => !Field<bool>(main, "busy"));
        Check(!f.Selection.Loaded && Field<Label>(main, "lblStatus").Text.Contains("기존 첨부를 유지"),
            "provider failure explains omitted attachment fallback without claiming empty files");
        Field<TextBox>(main, "txtTitle").Text = "본문만 수정";
        Button(main, "btnConfirm").PerformClick(); Until(() => Button(main, "btnConfirm").Text == "변경 실행");
        Button(main, "btnConfirm").PerformClick(); Until(() => f.Workflow.State == TaskManagementState.Succeeded);
        using var body = JsonDocument.Parse(f.Handler.Changes.Single().Body);
        Check(!body.RootElement.TryGetProperty("attachment_ids", out _), "unavailable optional picker never becomes accidental empty-list removal");
    }
    private static void DirtyVersionReset()
    {
        using var f = new Fixture(); f.Bind(); using var main = new MainEditor(f); Show(main);
        Until(() => Button(main, "btnAttachments").Enabled);
        Check(f.Selection.RemoveLocal(f.Selection.Items.Single().Key), "preparing explicit exclusion changes final list");
        f.Preparation.Version = "2"; main.AcceptReset = false; Wait(main.ReloadAsync());
        Check(main.ResetPrompts == 1 && f.Selection.HasChanges && f.Selection.MatchesContext(Context()),
            "declining reload reset keeps original changed attachment snapshot");
        main.AcceptReset = true; Wait(main.ReloadAsync());
        Check(main.ResetPrompts == 2 && !f.Selection.Loaded && f.Handler.Deletes.Count == 0,
            "confirmed newer-version reset clears local selection without DELETE");
    }
    private static void BusySessionLateResponse()
    {
        using var f = new Fixture(); f.Bind(); using var picker = new Picker(f.Selection); Show(picker);
        Guid key = AddUpload(picker, f, "late.bin"); string id = f.Selection.CompletedIds!.Last();
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Handler.DelayedDelete = gate; picker.AcceptDeletion = true;
        Button(picker, "btnDelete").PerformClick(); Until(() => f.Selection.IsBusy);
        Check(!Button(picker, "btnClose").Enabled && !Button(picker, "btnAdd").Enabled, "in-flight delete disables duplicate operations and close");
        picker.Close(); Check(!picker.IsDisposed, "current in-flight file action refuses closing");
        f.Api.ClearIdentity(); Until(() => Field<DataGridView>(picker, "gridFiles").Rows.Count == 0);
        Check(Button(picker, "btnClose").Enabled && !Button(picker, "btnAdd").Enabled, "session invalidation clears names and permits closing");
        gate.SetResult(Handler.Success(new { attachment_id = id })); Until(() => !Field<bool>(picker, "operating"));
        Check(f.Selection.Items.Count == 0 && f.Handler.Deletes.Count == 1, "late deletion response cannot revive previous login selection");
    }
    private static void ExplicitSameVersionReset()
    {
        using var f = new Fixture(); f.Bind(); using var main = new MainEditor(f); Show(main);
        Until(() => Button(main, "btnAttachments").Enabled);
        Check(f.Selection.RemoveLocal(f.Selection.Items.Single().Key), "same-version final list can be changed before explicit reset");
        main.AcceptReset = false; Button(main, "btnReload").PerformClick(); Until(() => !Field<bool>(main, "busy"));
        Check(main.ResetPrompts == 1 && f.Selection.HasChanges && f.Selection.CompletedIds is { Count: 0 },
            "declining explicit reload retains same-version final list");
        main.AcceptReset = true; Button(main, "btnReload").PerformClick(); Until(() => !Field<bool>(main, "busy"));
        Check(main.ResetPrompts == 2 && !f.Selection.Loaded && f.Selection.CompletedIds is null && f.Handler.Deletes.Count == 0,
            "confirmed same-version reload restores omission semantics without server deletion");
    }
    private static void ParentAuthorityRevocation()
    {
        foreach (int status in new[] { 401, 403 })
        {
            using var f = new Fixture(); using var main = new MainEditor(f); Show(main);
            Until(() => Button(main, "btnAttachments").Enabled);
            f.Handler.ListDenied = status; Button(main, "btnAttachments").PerformClick();
            Until(() => !f.Workflow.IsCurrent);
            Check(!f.Selection.Loaded && f.Selection.Items.Count == 0 && Field<TextBox>(main, "txtTitle").Text.Length == 0,
                "authority failure clears stale task and attachment data: " + status);
            Check(!Button(main, "btnConfirm").Enabled && !Button(main, "btnAttachments").Enabled && f.Handler.Changes.Count == 0,
                "authority failure blocks subsequent write and old picker: " + status);
        }
    }
    private static void DefiniteFailurePreservesNewInput()
    {
        foreach (string code in new[] { "ATTACHMENT_UNAVAILABLE", "TASK_CHANGE_NOT_NEEDED" })
        {
            using var f = new Fixture(); f.Bind(); using var main = new MainEditor(f); Show(main);
            Until(() => Button(main, "btnConfirm").Enabled);
            f.Handler.ChangeErrorCode = code;
            Button(main, "btnConfirm").PerformClick(); Until(() => Button(main, "btnConfirm").Text == "변경 실행");
            Button(main, "btnConfirm").PerformClick();
            Until(() => f.Workflow.State == TaskManagementState.Editing && f.Workflow.Failure is not null);
            var title = Field<TextBox>(main, "txtTitle");
            var description = Field<TextBox>(main, "txtDescription");
            title.Text = "오류 확인 뒤 새 제목"; description.Text = "사용자가 새로 고친 내용";
            main.PickerAction = selection => selection.RemoveLocal(selection.Items.Single().Key);
            Button(main, "btnAttachments").PerformClick();
            Check(f.Selection.CompletedIds is { Count: 0 } && title.Text == "오류 확인 뒤 새 제목"
                && description.Text == "사용자가 새로 고친 내용" && !title.ReadOnly,
                "attachment change after definitive failure preserves new editable input: " + code);
            Check(f.Handler.Changes.Count == 1 && f.Handler.Deletes.Count == 0,
                "correcting input after definitive failure does not retry or delete automatically: " + code);
        }
    }
    private static void StaleGenerationContextDenied()
    {
        using var f = new Fixture(); f.Bind(); using var main = new MainEditor(f); Show(main);
        Until(() => Button(main, "btnConfirm").Enabled);
        var delayed = new TaskCompletionSource<TaskWriteContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Preparation.ContextReplies.Enqueue(delayed.Task);
        Task oldRead = main.ReloadAsync();
        Check(!oldRead.IsCompleted && Field<bool>(main, "busy"), "first context read is deliberately waiting");
        Wait(main.ReloadAsync());
        Field<TextBox>(main, "txtTitle").Text = "새 조회 뒤 현재 입력";
        delayed.SetException(new TaskPreparationException(TaskPreparationFailure.Forbidden)); Wait(oldRead);
        Check(f.Workflow.IsCurrent && f.Workflow.State == TaskManagementState.Editing && f.Selection.Loaded,
            "previous generation context403 cannot revoke current workflow or selected files");
        Check(Field<TextBox>(main, "txtTitle").Text == "새 조회 뒤 현재 입력"
            && Button(main, "btnConfirm").Enabled && Button(main, "btnAttachments").Enabled,
            "previous generation context403 preserves latest input and available actions");
        Check(f.Handler.Changes.Count == 0 && f.Handler.Deletes.Count == 0,
            "superseded context403 does not trigger writes or deletes");
    }
    private static void ClosedFormContextDenied()
    {
        using var f = new Fixture(); f.Bind();
        var delayed = new TaskCompletionSource<TaskWriteContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task oldRead;
        using (var previous = new MainEditor(f))
        {
            Show(previous); Until(() => Button(previous, "btnConfirm").Enabled);
            f.Preparation.ContextReplies.Enqueue(delayed.Task); oldRead = previous.ReloadAsync();
            Check(!oldRead.IsCompleted, "old form still has a pending context read before closing");
            previous.Close();
        }
        Check(ReferenceEquals(TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9"), f.Workflow),
            "reopening the same task retains its captured session workflow");
        using var reopened = new MainEditor(f); Show(reopened);
        Until(() => Button(reopened, "btnConfirm").Enabled);
        Field<TextBox>(reopened, "txtTitle").Text = "재진입 뒤 현재 입력";
        delayed.SetException(new TaskPreparationException(TaskPreparationFailure.Forbidden)); Wait(oldRead);
        Check(f.Workflow.IsCurrent && f.Selection.Loaded && Field<TextBox>(reopened, "txtTitle").Text == "재진입 뒤 현재 입력",
            "closed form late context403 cannot revoke or clear reopened task input");
        Check(Button(reopened, "btnConfirm").Enabled && Button(reopened, "btnAttachments").Enabled
            && f.Handler.Changes.Count == 0 && f.Handler.Deletes.Count == 0,
            "closed form late context403 preserves current actions with zero writes");
    }

    private sealed class Picker(TaskManagementAttachmentSelection selection) : TaskManagementAttachmentsForm(selection)
    {
        public string? FilePath; public bool AcceptRemoval, AcceptDeletion, ConfirmedExisting;
        public Guid? ConfirmedKey; public Action? DuringConfirmation;
        protected override string? ChooseFile() => FilePath;
        protected override bool ConfirmFileDeletion(TaskManagementAttachmentItem item)
        { ConfirmedKey = item.Key; DuringConfirmation?.Invoke(); return AcceptDeletion; }
        protected override bool ConfirmLocalRemoval(TaskManagementAttachmentItem item)
        { ConfirmedExisting = item.IsExisting; return AcceptRemoval; }
    }
    private sealed class MainEditor(Fixture fixture) : TaskManagementForm(TaskManagementKind.Update, Detail, fixture.Queries, fixture.Preparation, fixture.Workflow)
    {
        public int PickerCalls, ResetPrompts; public bool AcceptReset; public Action<TaskManagementAttachmentSelection>? PickerAction;
        protected override void ShowAttachmentPicker(TaskManagementAttachmentSelection selection) { PickerCalls++; PickerAction?.Invoke(selection); }
        protected override bool ConfirmAttachmentReset() { ResetPrompts++; return AcceptReset; }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "RemoteWork-management-attachment-ui-" + Guid.NewGuid().ToString("N"));
        public Handler Handler { get; } = new(); public ApiClient Api { get; } public TaskManagementWorkflow Workflow { get; }
        public TaskManagementAttachmentSelection Selection => Workflow.Attachments!;
        public PreparationFixture Preparation { get; } public QueryFixture Queries { get; } = new();
        public Fixture()
        {
            Directory.CreateDirectory(directory); Api = new ApiClient("http://management-attachment-ui.invalid/", Handler);
            Wait(Api.LoginAsync("synthetic", "synthetic-password"));
            Preparation = new(Api.CaptureSession()); Workflow = TaskManagementSessionStore.Get(Api, TaskManagementKind.Update, "9")!;
        }
        public void Bind() => Check(Selection.BindSnapshot(Context(Preparation.Version), Wait(Selection.ReadFilesAsync())), "explicit fixture binds checked original file snapshot");
        public string File(string name) { string path = Path.Combine(directory, name); System.IO.File.WriteAllBytes(path, Enumerable.Range(0, 129).Select(x => (byte)x).ToArray()); return path; }
        public void Dispose()
        {
            Workflow.Dispose(); Api.Dispose();
            foreach (string file in Directory.GetFiles(directory)) System.IO.File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private sealed class QueryFixture : ITaskQueryService
    {
        public string SourceDescription => "Explicit UI test double";
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskSummary>>([Detail.Summary]);
        public Task<TaskDetail> GetTaskAsync(string id, CancellationToken ct = default) => Task.FromResult(Detail);
    }
    private sealed class PreparationFixture(ApiClient.Session session) : ITaskPreparationService
    {
        public Queue<string> Versions { get; } = new(); public string Version = "1";
        // Intentionally ignores cancellation to exercise a response already in flight
        // or a service implementation that completes after the UI generation changes.
        public Queue<Task<TaskWriteContext>> ContextReplies { get; } = new();
        public bool IsCurrent => session.IsCurrent; public bool CanSelectCandidates => IsCurrent; public int UserId => 2;
        public CancellationToken SessionCancellation => session.Cancellation;
        public Task<TaskWriteContext> GetContextAsync(string id, CancellationToken ct = default) => ContextReplies.Count > 0
            ? ContextReplies.Dequeue() : Task.FromResult(Context(Versions.Count > 0 ? Versions.Dequeue() : Version));
        public Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery query, CancellationToken ct = default) => throw new InvalidOperationException("Unexpected candidate read");
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? taskId, CancellationToken ct = default) => throw new InvalidOperationException("Workflow uses real common HTTP adapter");
        public Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default) => throw new InvalidOperationException("Workflow uses real common HTTP adapter");
    }
    private sealed record Request(string Method, string Path, string Body, string? Key);
    private sealed class Handler : HttpMessageHandler
    {
        public const string ExistingId = "11111111-1111-4111-8111-111111111111";
        public List<Request> Requests { get; } = new(); public Dictionary<string, object> Uploaded { get; } = new();
        public List<Request> Deletes => Requests.Where(x => x.Method == "DELETE").ToList();
        public List<Request> Changes => Requests.Where(x => x.Method == "PATCH" && x.Path == "/api/v1/tasks/9").ToList();
        public bool DropChange, NotRecorded, ListUnavailable; public int ListDenied; public string? ChangeErrorCode;
        public TaskCompletionSource<HttpResponseMessage>? DelayedDelete;
        public static HttpResponseMessage Success(object data, int status = 200) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "synthetic" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        private static HttpResponseMessage Error(int status, string code) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "synthetic" }), Encoding.UTF8, "application/json") };
        private static object Result() => new { task_id = "9", row_version = "2", assignment_revision = 3, task_status = "not_started", updated_at = "2026-10-22T00:00:00Z" };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath, body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new(request.Method.Method, path, body, request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));
            if (path.EndsWith("/auth/login")) return Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
                user = new { id = 2, login_id = "admin", display_name = "관리자", employee_no = "A2", department = "팀", role = "admin", is_active = true } });
            if (path == "/api/v1/results/shared/tasks/9") return ListDenied > 0 ? Error(ListDenied, "AUTH_REQUIRED") : ListUnavailable ? Error(503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED") : Success(new { items = new[] {
                new { attachment_id = ExistingId, name = "기존_업무_첨부.txt", size = 39, sha256 = new string('a', 64) } } });
            if (path.EndsWith("/uploads/start"))
            {
                using var json = JsonDocument.Parse(body); string id = Guid.NewGuid().ToString("D");
                var file = new { attachment_id = id, name = json.RootElement.GetProperty("name").GetString(), size = json.RootElement.GetProperty("size").GetInt64(), sha256 = json.RootElement.GetProperty("sha256").GetString() };
                Uploaded.Add(id, file); return Success(file, 201);
            }
            if (path.Contains("/results/shared/uploads/"))
            {
                string id = path.Split('/')[6];
                if (request.Method == HttpMethod.Delete) return DelayedDelete is null ? Success(new { attachment_id = id }) : await DelayedDelete.Task;
                if (path.EndsWith("/complete")) return Success(Uploaded[id]);
                if (path.EndsWith("/chunks"))
                {
                    using var json = JsonDocument.Parse(body);
                    return Success(new { attachment_id = id, next_offset = json.RootElement.GetProperty("offset").GetInt64() + Convert.FromBase64String(json.RootElement.GetProperty("data").GetString()!).Length });
                }
            }
            if (path.EndsWith("/write-requests/keys")) return Success(new { request_key = new string('k', 40), expires_at = "2099-01-01T00:00:00Z" }, 201);
            if (path.EndsWith("/write-requests/result")) return NotRecorded ? Success(new { state = "not_recorded" }) : Success(new { state = "completed", http_status = 200, result = Result(), expires_at = "2099-01-01T00:00:00Z" });
            if (path == "/api/v1/tasks/9" && request.Method == HttpMethod.Patch)
            { if (DropChange) { DropChange = false; throw new HttpRequestException("explicit response loss"); }
                return ChangeErrorCode is { } errorCode ? Error(409, errorCode) : Success(Result()); }
            throw new InvalidOperationException("Unexpected route in explicit controlled UI fixture");
        }
    }
}
