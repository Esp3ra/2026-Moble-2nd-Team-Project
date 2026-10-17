using System.Globalization;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static void RegistrationPreparation()
    {
        using var form = new TaskEditForm();
        ShowPreparation(form);
        var title = Field<TextBox>(form, "txtTaskTitle");
        var description = Field<TextBox>(form, "txtTaskDescription");
        var start = Field<DateTimePicker>(form, "dtpStartDate");
        var due = Field<DateTimePicker>(form, "dtpDeadline");
        var check = Field<Button>(form, "btnConfirm");
        var errors = Field<ErrorProvider>(form, "inputErrors");
        var status = Field<Label>(form, "lblPreparationStatus");
        Need(title.Text == "" && description.Text == "" && !start.Checked && !due.Checked,
            "registration starts without fabricated task values or chosen dates");
        Need(form.CheckedInput is null, "unchecked registration has no reusable input snapshot");
        Need(!Field<ComboBox>(form, "cmbAssignee").Enabled
            && Field<TextBox>(form, "txtAttachments").Text.Contains("연결 규약 확인 필요"),
            "unagreed assignee lookup and attachments are not fabricated");
        title.Text = "   ";
        check.PerformClick();
        Need(errors.GetError(title).Length > 0 && !form.IsDisposed,
            "blank title is rejected without closing the draft");
        Need(form.CheckedInput is null, "failed input check does not expose a draft");
        title.Text = new string('가', 201);
        check.PerformClick();
        Need(errors.GetError(title).Length > 0, "title beyond the current 200-character storage limit is rejected locally");
        title.Text = "등록 입력 초안";
        check.PerformClick();
        Need(errors.GetError(start).Length > 0 && errors.GetError(due).Length > 0,
            "unselected dates are not silently treated as submitted values");
        start.Value = new DateTime(2026, 10, 20);
        due.Value = new DateTime(2026, 10, 19);
        start.Checked = due.Checked = true;
        check.PerformClick();
        Need(errors.GetError(due).Length > 0, "due date before start is rejected");
        due.Value = start.Value;
        description.Text = "입력은 이 창에서만 유지됩니다.";
        check.PerformClick();
        Need(errors.GetError(title) == "" && errors.GetError(due) == ""
            && !form.IsDisposed && form.DialogResult != DialogResult.OK,
            "valid basic input leaves the unsaved form open");
        Need(status.Text.Contains("전송") && status.Text.Contains("저장"),
            "local input check explains the unconnected persistence boundary");
        var checkedRegistration = form.CheckedInput;
        Need(checkedRegistration is not null && checkedRegistration.TaskId is null
            && checkedRegistration.Title == title.Text && checkedRegistration.Description == description.Text
            && checkedRegistration.StartDate == new DateOnly(2026, 10, 20)
            && checkedRegistration.DueDate == new DateOnly(2026, 10, 20),
            "valid registration exposes only exact local values without inventing a task ID");
        var completedMessage = status.Text;
        title.Text += " 수정";
        Need(status.Text != completedMessage, "editing a checked draft invalidates its previous check message");
        Need(form.CheckedInput is null && checkedRegistration!.Title == "등록 입력 초안",
            "changing input invalidates the current draft and cannot mutate an earlier snapshot");
        check.PerformClick();
        start.Checked = false;
        Need(form.CheckedInput is null, "unchecking a required date invalidates the captured input");
        start.Checked = true;
        // A MySQL VARCHAR character is not the same as a UTF-16 code unit.
        title.Text = string.Concat(Enumerable.Repeat("😀", 200));
        check.PerformClick();
        Need(errors.GetError(title) == "", "200 Unicode code points fit the local title limit");
        title.Text += "😀";
        check.PerformClick();
        Need(errors.GetError(title).Length > 0, "201 Unicode code points exceed the local title limit");
        Need(form.CheckedInput is null, "rejected long title cannot reuse an earlier checked snapshot");
        Capture(form, "Registration-Input-Validation");
        form.Size = form.MinimumSize;
        Application.DoEvents();
        Capture(form, "Registration-Input-Minimum");
        form.Close();
        using var reopened = new TaskEditForm();
        Need(Field<TextBox>(reopened, "txtTaskTitle").Text == "", "closing does not persist a registration draft");

        TaskDetail original = Fixtures[2];
        using var editing = new TaskEditForm(original);
        ShowPreparation(editing);
        Need(Field<TextBox>(editing, "txtTaskTitle").Text == original.Summary.Title
            && Field<TextBox>(editing, "txtTaskDescription").Text == original.Description,
            "edit draft starts from the selected detail instead of a hard-coded example");
        Need(Field<ComboBox>(editing, "cmbAssignee").Text == original.Summary.AssigneeName
            && DateOnly.FromDateTime(Field<DateTimePicker>(editing, "dtpDeadline").Value) == original.Summary.DueDate,
            "selected assignee display and exact DATE populate the edit draft");
        Field<TextBox>(editing, "txtTaskTitle").Text = "로컬에서만 바꾼 제목";
        Field<Button>(editing, "btnConfirm").PerformClick();
        Need(editing.CheckedInput?.TaskId == original.Summary.TaskId
            && editing.CheckedInput.Description == original.Description,
            "edit snapshot retains the opaque source ID and exact source description");
        Need(original.Summary.Title == Fixtures[2].Summary.Title && original.Description == Fixtures[2].Description,
            "local editing never mutates the immutable queried task");
        Capture(editing, "Selected-Task-Edit-Draft");

        var outsidePicker = new TaskDetail(original.Summary with
        { StartDate = new DateOnly(1000, 1, 1), DueDate = new DateOnly(9999, 12, 31) }, original.Description);
        using var unsupported = new TaskEditForm(outsidePicker);
        ShowPreparation(unsupported);
        Need(!Field<Button>(unsupported, "btnConfirm").Enabled
            && Field<DateTimePicker>(unsupported, "dtpStartDate").CustomFormat?.Contains("1000-01-01") == true
            && Field<DateTimePicker>(unsupported, "dtpDeadline").CustomFormat?.Contains("9999-12-31") == true,
            "valid server dates outside picker support are preserved visibly without clamping");
        Need(unsupported.CheckedInput is null, "unsupported source dates never become a checked edit draft");
        Capture(unsupported, "Date-Picker-Outside-Range");
    }

    private static void DeadlineAndAcceptancePreparation()
    {
        using var unconfigured = new DeadlineExtensionForm();
        Need(!Field<Button>(unconfigured, "btnConfirm").Enabled
            && !Field<TextBox>(unconfigured, "txtTaskSummary").Text.Contains("TASK-001"),
            "extension without a selected task has no example target or enabled confirmation");
        Need(unconfigured.CheckedInput is null, "extension without a source task exposes no local snapshot");
        TaskDetail original = Fixtures[3];
        using var form = new DeadlineExtensionForm(PreviewRole.Administrator, original);
        ShowPreparation(form);
        Need(Field<TextBox>(form, "txtTaskSummary").Text.Contains(original.Summary.TaskId)
            && Field<TextBox>(form, "txtCurrentDeadline").Text == original.Summary.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "extension identifies the actual task and exact current due date");
        var date = Field<DateTimePicker>(form, "dtpRequestedDeadline");
        var reason = Field<TextBox>(form, "txtExtensionReason");
        var errors = Field<ErrorProvider>(form, "inputErrors");
        var status = Field<Label>(form, "lblPreparationStatus");
        Field<Button>(form, "btnConfirm").PerformClick();
        Need(errors.GetError(date).Length > 0 && errors.GetError(reason).Length > 0,
            "extension requires an explicit desired date and nonblank reason");
        date.Value = original.Summary.DueDate.ToDateTime(TimeOnly.MinValue);
        date.Checked = true;
        reason.Text = "   ";
        Field<Button>(form, "btnConfirm").PerformClick();
        Need(errors.GetError(reason).Length > 0, "whitespace extension reason is rejected");
        reason.Text = "  마감 연장 입력 형식만 점검합니다.  ";
        Field<Button>(form, "btnConfirm").PerformClick();
        Need(errors.GetError(reason) == "" && errors.GetError(date) == ""
            && !form.IsDisposed && form.DialogResult != DialogResult.OK,
            "input check does not invent date/state eligibility or accept a request");
        Need(status.Text.Contains("접수") && original.Summary.DueDate == Fixtures[3].Summary.DueDate,
            "local extension preview leaves the actual deadline unchanged");
        var checkedExtension = form.CheckedInput;
        Need(checkedExtension is not null && checkedExtension.TaskId == original.Summary.TaskId
            && checkedExtension.OriginalDueDate == original.Summary.DueDate
            && checkedExtension.RequestedDueDate == original.Summary.DueDate
            && checkedExtension.Reason == reason.Text,
            "extension snapshot preserves the source deadline and verbatim reason without setting eligibility");
        var oldMessage = status.Text;
        reason.Text += " 변경";
        Need(status.Text != oldMessage, "changed extension input invalidates its previous check");
        Need(form.CheckedInput is null && checkedExtension!.Reason.EndsWith("  ", StringComparison.Ordinal),
            "extension invalidation leaves the earlier immutable snapshot unchanged");
        Field<Button>(form, "btnConfirm").PerformClick();
        date.Checked = false;
        Need(form.CheckedInput is null, "unchecking an extension date invalidates its snapshot");
        date.Checked = true;
        Capture(form, "Deadline-Input-Preparation");
        form.Size = form.MinimumSize;
        Application.DoEvents();
        Capture(form, "Deadline-Input-Minimum");
        foreach (TaskDetail task in Fixtures)
        {
            using var acceptance = new TaskAcceptanceForm(task);
            ShowPreparation(acceptance);
            var text = AllText(acceptance);
            Need(text.Contains(task.Summary.TaskId) && text.Contains(task.Summary.Title)
                && text.Contains(task.Summary.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                "acceptance target displays the selected task across all five states");
            Need(Descendants(acceptance).OfType<Button>().All(button => button.Text == "닫기"),
                "acceptance preparation offers only a close action");
            if (task.Summary.Status == TaskDisplayStatus.NotStarted) Capture(acceptance, "Acceptance-Target-Preparation");
        }
    }

    private static void PreparationContextLifetime()
    {
        var service = PopulatedProbe();
        using var page = new TasksControl(PreviewRole.Administrator, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Need(PreparationId(page) == Fixtures[0].Summary.TaskId && PreparationButtons(page).All(button => button.Enabled),
            "only a successful selected detail enables target preparation");
        Select(page, 2);
        Until(() => PreparationId(page) == Fixtures[2].Summary.TaskId);
        int lists = service.ListCalls, details = service.DetailCalls;
        WithModal<TaskAcceptanceForm>(() => Field<Button>(page, "btnAcceptTask").PerformClick(),
            form => Need(AllText(form).Contains(Fixtures[2].Summary.TaskId), "acceptance button forwards the selected successful detail"));
        WithModal<DeadlineExtensionForm>(() => Field<Button>(page, "btnDeadlineExtensionPreview").PerformClick(),
            form => Need(Field<TextBox>(form, "txtTaskSummary").Text.Contains(Fixtures[2].Summary.TaskId),
                "extension button forwards the selected successful detail"));
        Need(service.ListCalls == lists && service.DetailCalls == details, "opening preparation dialogs performs no extra queries");
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
        Until(() => PreparationId(page) == Fixtures[0].Summary.TaskId);
        WithModal<TaskEditForm>(() => Field<Button>(page, "btnEditTaskPreview").PerformClick(),
            form => Need(Field<TextBox>(form, "txtTaskTitle").Text == Fixtures[0].Summary.Title,
                "managed edit button forwards the actual selected detail"));

        var pending = new Dictionary<string, TaskCompletionSource<TaskDetail>>();
        service.Detail = (id, _) =>
        {
            var source = new TaskCompletionSource<TaskDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = source;
            return source.Task;
        };
        Select(page, 1);
        Until(() => pending.ContainsKey("opaque:1"));
        Need(PreparationId(page) is null && PreparationButtons(page).All(button => !button.Enabled),
            "selection and detail loading invalidate the old successful target");
        Select(page, 2);
        Until(() => pending.ContainsKey("opaque:2"));
        pending["opaque:2"].SetResult(Fixtures[2]);
        Until(() => PreparationId(page) == "opaque:2");
        pending["opaque:1"].SetResult(Fixtures[1]);
        Pump();
        Need(PreparationId(page) == "opaque:2", "late detail cannot become a new preparation target");
        service.Detail = (_, _) => Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.AuthenticationExpired));
        Select(page, 3);
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text.StartsWith("상세 조회 실패"));
        Need(PreparationId(page) is null && PreparationButtons(page).All(button => !button.Enabled),
            "failed/expired detail cannot reuse a previous target");
        service.Detail = (id, _) => Task.FromResult(Fixtures.Single(task => task.Summary.TaskId == id));
        Wait(page.RefreshAsync());
        var pendingList = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.List = (_, _) => pendingList.Task;
        var refresh = page.RefreshAsync();
        Need(PreparationId(page) is null && PreparationButtons(page).All(button => !button.Enabled),
            "full refresh invalidates the selected preparation context immediately");
        pendingList.SetResult(Array.Empty<TaskSummary>());
        Wait(refresh);
        Need(PreparationId(page) is null && PreparationButtons(page).All(button => !button.Enabled),
            "empty list leaves no preparation target");
    }

    private static string? PreparationId(TasksControl page) =>
        typeof(TasksControl).GetField("currentTaskDetail", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(page) is TaskDetail detail ? detail.Summary.TaskId : null;

    private static Button[] PreparationButtons(TasksControl page) =>
        new[] { "btnEditTaskPreview", "btnAcceptTask", "btnDeadlineExtensionPreview" }.Select(name => Field<Button>(page, name)).ToArray();

    private static IEnumerable<Control> Descendants(Control owner) => owner.Controls.Cast<Control>()
        .SelectMany(control => new[] { control }.Concat(Descendants(control)));

    private static void ShowPreparation(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.Show();
        Application.DoEvents();
    }

    private static void WithModal<T>(Action open, Action<T> inspect) where T : Form
    {
        bool found = false;
        Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            if (Application.OpenForms.OfType<T>().FirstOrDefault() is not { } dialog) return;
            timer.Stop();
            found = true;
            try { inspect(dialog); }
            catch (Exception error) { failure = error; }
            finally { dialog.Close(); }
        };
        timer.Start();
        open();
        timer.Stop();
        if (failure is not null) throw failure;
        Need(found, "the expected preparation dialog opened from its real button");
    }
}
