using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Queries.TestDoubles;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static int assertions;
    private static readonly List<string> completed = new();
    private static readonly TaskDetail[] Fixtures = Enum.GetValues<TaskDisplayStatus>()
        .Select((status, index) => new TaskDetail(
            new TaskSummary($"opaque:{index}", $"조회 검증 업무 {index}", "테스트 담당자", status,
                new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 20 + index)),
            $"선택 업무 {index}의 상세 내용")).ToArray();
    private static string? captureDirectory;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("맑은 고딕", 9));
        Control.CheckForIllegalCrossThreadCalls = true;
        captureDirectory = args.Length == 0 ? null : Path.GetFullPath(args[0]);
        if (captureDirectory != null) Directory.CreateDirectory(captureDirectory);
        var exitCode = 1;
        using var coordinator = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000),
            Size = new Size(1, 1)
        };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            exitCode = RunScenarios();
            coordinator.Close();
        }));
        Application.Run(coordinator);
        return exitCode;
    }

    private static int RunScenarios()
    {
        try
        {
            Need(Application.MessageLoop && SynchronizationContext.Current is WindowsFormsSynchronizationContext,
                "tests run under the application's WinForms synchronization context");
            Run("query boundary and explicit test double", QueryContracts);
            Run("default application remains unconfigured", DefaultBoundary);
            Run("selection, all five filters, and scope", FiltersAndSelection);
            Run("empty list and distinct failures", EmptyAndFailures);
            Run("loading and latest list response", LatestListWins);
            Run("latest selected detail and disposal", LatestDetailWins);
            Run("same-query refresh preserves selection and changed views reset it", RefreshSelection);
            Run("failed selected detail retries without reloading the list", RetrySelectedDetail);
            Run("fresh detail differences explain list freshness and explicitly refresh", DetailFreshness);
            Run("service cancellation is distinct and clears previous session data", ServiceCancellation);
            Run("registration and selected-task drafts validate locally", RegistrationPreparation);
            Run("deadline inputs and acceptance target do not mutate tasks", DeadlineAndAcceptancePreparation);
            Run("preparation dialogs use only the latest successful selected detail", PreparationContextLifetime);
            Console.WriteLine($"PASS: {assertions} assertions; {completed.Count} scenarios. Test doubles only; HTTP, authentication, server authorization and DB were not exercised.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"FAIL after {assertions} assertions: {error}");
            return 1;
        }
    }

    private static void QueryContracts()
    {
        var service = new TaskQueryTestDouble(Fixtures, new[] { Extra("managed") });
        Need(service.SourceDescription.Contains("테스트 대역"), "test source explicitly identified");
        Need(service.GetTasksAsync(new(TaskQueryScope.Own)).Result.Count == 5, "all fixture statuses available");
        foreach (var status in Enum.GetValues<TaskDisplayStatus>())
        {
            var result = service.GetTasksAsync(new(TaskQueryScope.Own, status)).Result;
            Need(result.Count == 1 && result[0].Status == status, "status-specific query");
        }
        Need(service.GetTasksAsync(new(TaskQueryScope.Managed)).Result.Single().TaskId == "managed", "separate managed fixture");
        Need(service.GetTaskAsync("opaque:3").Result.Description == Fixtures[3].Description, "opaque detail key");
        ExpectFailure(() => service.GetTaskAsync("missing").GetAwaiter().GetResult(), TaskQueryFailure.NotFound);
        var boundary = new UnconfiguredTaskQueryService();
        ExpectFailure(() => boundary.GetTasksAsync(new(TaskQueryScope.Own)).GetAwaiter().GetResult(), TaskQueryFailure.NotConfigured);
        ExpectFailure(() => boundary.GetTaskAsync("opaque:0").GetAwaiter().GetResult(), TaskQueryFailure.NotConfigured);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            service.GetTasksAsync(new(TaskQueryScope.Own), cancelled.Token).GetAwaiter().GetResult();
            throw new InvalidOperationException("Cancelled query returned data.");
        }
        catch (OperationCanceledException) { Need(true, "cancelled fixture query"); }
        try
        {
            _ = new TaskQueryTestDouble(new[] { Fixtures[0], Fixtures[0] with { Description = "conflicting detail" } });
            throw new InvalidOperationException("Conflicting fixture IDs accepted.");
        }
        catch (ArgumentException) { Need(true, "ambiguous fixture IDs rejected"); }
    }

    private static void DefaultBoundary()
    {
        using var page = new TasksControl();
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Need(Grid(page).Rows.Count == 0, "default never falls back to samples");
        Need(Field<Label>(page, "lblTaskQueryStatus").Text.Contains("연결"), "unconfigured message");
        Need(Field<DataGridView>(page, "gridTaskAttachments").Rows.Count == 0, "no fake attachment data");
        Capture(page, "Default-Unconfigured");
        foreach (var role in new[] { PreviewRole.Employee, PreviewRole.Administrator })
        {
            using var main = new MainForm(role)
            {
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000)
            };
            main.Show();
            Field<Button>(main, "btnTasks").PerformClick();
            var tasks = Field<Panel>(main, "contentPanel").Controls.OfType<TasksControl>().Single();
            Wait(tasks.RefreshAsync());
            Need(Grid(tasks).Rows.Count == 0, "main composition never injects fake data");
            Capture(main, role + "-Main-Own-Unconfigured");
            if (role == PreviewRole.Administrator)
            {
                Field<ComboBox>(tasks, "cmbTaskScope").SelectedIndex = 1;
                Wait(tasks.RefreshAsync());
                Capture(main, "Administrator-Main-Managed-Unconfigured");
            }
        }
    }

    private static void FiltersAndSelection()
    {
        using var page = new TasksControl(PreviewRole.Administrator,
            new TaskQueryTestDouble(Fixtures, new[] { Extra("managed") }));
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Need(Grid(page).Rows.Count == 5, "own list bound");
        Need(Field<Label>(page, "lblTaskQuerySource").Text.Contains("실제 서버 연결 아님"), "test source remains visible");
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        Need(Detail(page).Contains("opaque:2"), "selected row drives detail");
        var filter = Field<ComboBox>(page, "cmbTaskStatusFilter");
        for (var index = 1; index <= 5; index++)
        {
            filter.SelectedIndex = index;
            Until(() => Grid(page).Rows.Count == 1 && RowId(page, 0) == Fixtures[index - 1].Summary.TaskId);
            Until(() => Detail(page).Contains(Fixtures[index - 1].Description));
            Need(Grid(page).Rows.Count == 1, "filter changes returned list");
        }
        filter.SelectedIndex = 0;
        Until(() => Grid(page).Rows.Count == 5);
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
        Until(() => Grid(page).Rows.Count == 1 && RowId(page, 0) == "managed");
        Until(() => Detail(page).Contains("managed"));
        Need(true, "managed view uses separate query");
        Capture(page, "Admin-Managed-TestDouble");
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 0;
        Until(() => Grid(page).Rows.Count == 5);
        Until(() => Detail(page).Contains(Fixtures[0].Description));
        Capture(page, "Admin-Own-TestDouble");
        host.ClientSize = new Size(850, 620);
        Application.DoEvents();
        Need(page.Width == 850 && page.Height == 620, "minimum capture uses resized host");
        Capture(page, "Admin-Own-Minimum-TestDouble");
    }

    private static void EmptyAndFailures()
    {
        var service = new ProbeService();
        using var page = new TasksControl(PreviewRole.Employee, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Need(Grid(page).Rows.Count == 0 && NoFixtureDetail(page), "empty list clears detail data");
        Need(AllText(page).Contains("없"), "empty is explicit");
        Capture(page, "Empty-TestDouble");
        foreach (var failure in Enum.GetValues<TaskQueryFailure>())
        {
            service.List = (_, _) => Task.FromException<IReadOnlyList<TaskSummary>>(new TaskQueryException(failure));
            Wait(page.RefreshAsync());
            var message = Field<Label>(page, "lblTaskQueryStatus").Text;
            Need(message.Length > 0 && Grid(page).Rows.Count == 0 && NoFixtureDetail(page), "failed list clears data");
            FailureMessages.Add(message);
            Capture(page, "Failure-" + failure);
        }
        Need(FailureMessages.Count == 5, "five failures have distinct presentation");
        service.List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(new[] { Fixtures[0].Summary });
        service.Detail = (_, _) => Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.AuthenticationExpired));
        Wait(page.RefreshAsync());
        Need(Grid(page).Rows.Count == 1 && NoFixtureDetail(page)
            && Field<Label>(page, "lblTaskQueryStatus").Text.Contains("인증"), "detail authentication failure clears old detail");
        service.Detail = (_, _) => Task.FromResult(Fixtures[0]);
        Field<Button>(page, "btnRetryQuery").PerformClick();
        Until(() => Detail(page).Contains(Fixtures[0].Description));
        Need(true, "retry recovers detail");
        service.List = (_, _) => Task.FromException<IReadOnlyList<TaskSummary>>(new InvalidOperationException("secret-internal-detail"));
        Wait(page.RefreshAsync());
        Need(!AllText(page).Contains("secret-internal-detail"), "internal exception text not shown");
    }

    private static readonly HashSet<string> FailureMessages = new();

    private static void LatestListWins()
    {
        var service = new ProbeService();
        using var page = new TasksControl(PreviewRole.Employee, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        var pending = new List<TaskCompletionSource<IReadOnlyList<TaskSummary>>>();
        service.List = (_, _) =>
        {
            var result = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(result);
            return result.Task; // Deliberately ignores cancellation to simulate a late transport response.
        };
        var older = page.RefreshAsync();
        Until(() => pending.Count == 1);
        Need(Field<Label>(page, "lblTaskQueryStatus").Text.Contains("조회"), "loading message");
        Need(Grid(page).Rows.Count == 0 && NoFixtureDetail(page), "loading clears stale data");
        var newer = page.RefreshAsync();
        Until(() => pending.Count == 2);
        pending[1].SetResult(new[] { Extra("newer").Summary });
        Wait(newer);
        pending[0].SetResult(new[] { Extra("older").Summary });
        Wait(older);
        Need(Grid(page).Rows.Count == 1 && RowId(page, 0) == "newer", "late list response cannot replace latest query");
    }

    private static void LatestDetailWins()
    {
        var service = new ProbeService
        {
            List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Fixtures.Select(x => x.Summary).ToArray()),
            Detail = (key, _) => Task.FromResult(Fixtures.Single(x => x.Summary.TaskId == key))
        };
        using var page = new TasksControl(PreviewRole.Employee, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        var pending = new Dictionary<string, TaskCompletionSource<TaskDetail>>();
        service.Detail = (key, _) =>
        {
            var result = new TaskCompletionSource<TaskDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[key] = result;
            return result.Task;
        };
        Select(page, 1);
        Until(() => pending.ContainsKey("opaque:1"));
        Select(page, 2);
        Until(() => pending.ContainsKey("opaque:2"));
        pending["opaque:2"].SetResult(Fixtures[2]);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        pending["opaque:1"].SetResult(Fixtures[1]);
        Pump();
        Need(Detail(page).Contains(Fixtures[2].Description) && !Detail(page).Contains(Fixtures[1].Description), "late detail cannot replace selected row");
        service.Detail = (_, _) => Task.FromResult(Extra("wrong-task"));
        Select(page, 3);
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text.Contains("실패"));
        Need(NoFixtureDetail(page) && !Detail(page).Contains("wrong-task"), "mismatched detail ID rejected");
        var disposedRequest = new TaskCompletionSource<TaskDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Detail = (_, _) => disposedRequest.Task;
        Select(page, 4);
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text.Contains("조회 중"));
        try { page.Dispose(); }
        catch
        {
            DumpControlState(page);
            throw;
        }
        disposedRequest.SetResult(Fixtures[4]);
        Pump();
        Need(page.IsDisposed, "pending detail safely completes after disposal");
    }

    private static void RefreshSelection()
    {
        var service = PopulatedProbe();
        using var page = new TasksControl(PreviewRole.Administrator, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        Wait(page.RefreshAsync());
        Need(SelectedId(page) == "opaque:2" && Detail(page).Contains(Fixtures[2].Description),
            "same query refresh restores selected row and its detail");

        var pending = new List<TaskCompletionSource<IReadOnlyList<TaskSummary>>>();
        service.List = (_, _) =>
        {
            var source = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(source);
            return source.Task;
        };
        var older = page.RefreshAsync();
        Until(() => pending.Count == 1);
        var newer = page.RefreshAsync();
        Until(() => pending.Count == 2);
        pending[1].SetResult(Fixtures.Reverse().Select(task => task.Summary).ToArray());
        Wait(newer);
        pending[0].SetResult(new[] { Fixtures[0].Summary });
        Wait(older);
        Need(SelectedId(page) == "opaque:2" && Detail(page).Contains(Fixtures[2].Description),
            "selection survives overlapping same-query refresh and changed row order");

        service.List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(new[] { Fixtures[0].Summary, Fixtures[1].Summary });
        Wait(page.RefreshAsync());
        Need(SelectedId(page) == "opaque:0" && Detail(page).Contains(Fixtures[0].Description),
            "missing previous selection falls back to current first row");
        service.List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Array.Empty<TaskSummary>());
        Wait(page.RefreshAsync());
        Need(Grid(page).Rows.Count == 0 && NoFixtureDetail(page) && Detail(page).Contains("없"),
            "empty refreshed list clears the previous selection and detail");

        // Repeat IDs across views deliberately: query changes must reset selection even when IDs overlap.
        service.List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Fixtures.Select(task => task.Summary).ToArray());
        Wait(page.RefreshAsync());
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 1;
        Until(() => SelectedId(page) == "opaque:0" && Detail(page).Contains(Fixtures[0].Description));
        Need(SelectedId(page) == "opaque:0", "changed status query starts at its first row");
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
        Until(() => SelectedId(page) == "opaque:0" && Detail(page).Contains(Fixtures[0].Description));
        Need(SelectedId(page) == "opaque:0", "changed own/managed scope does not carry selection");

        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        pending.Clear();
        service.List = (_, _) =>
        {
            var source = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(source);
            return source.Task;
        };
        older = page.RefreshAsync();
        Until(() => pending.Count == 1);
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 0;
        Until(() => pending.Count == 2);
        pending[1].SetResult(Fixtures.Select(task => task.Summary).ToArray());
        Until(() => SelectedId(page) == "opaque:0" && Detail(page).Contains(Fixtures[0].Description));
        pending[0].SetResult(Fixtures.Reverse().Select(task => task.Summary).ToArray());
        Wait(older);
        Need(SelectedId(page) == "opaque:0" && Detail(page).Contains(Fixtures[0].Description),
            "late old-scope response cannot restore an old-scope selection");
    }

    private static void RetrySelectedDetail()
    {
        var service = PopulatedProbe();
        using var page = new TasksControl(PreviewRole.Administrator, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        var retry = Field<Button>(page, "btnRetryQuery");
        var status = Field<Label>(page, "lblTaskQueryStatus");
        foreach (TaskQueryFailure failure in Enum.GetValues<TaskQueryFailure>())
        {
            service.Detail = (_, _) => Task.FromException<TaskDetail>(new TaskQueryException(failure));
            Select(page, 0);
            Until(() => status.Text.StartsWith("상세 조회 실패"));
            Select(page, 2);
            Until(() => status.Text.StartsWith("상세 조회 실패"));
            Need(SelectedId(page) == "opaque:2" && retry.Text == "상세 재시도"
                && status.Text.StartsWith("상세 조회 실패") && Grid(page).Rows.Count == Fixtures.Length,
                "typed detail failure retains the selected list and identifies detail retry");
            if (failure == TaskQueryFailure.NotFound)
            {
                Capture(page, "Detail-Retry-NotFound");
                host.ClientSize = new Size(850, 620);
                Application.DoEvents();
                Capture(page, "Detail-Retry-Minimum");
                host.ClientSize = new Size(1320, 780);
                Application.DoEvents();
            }
            int listCalls = service.ListCalls;
            int detailCalls = service.DetailCalls;
            string? requestedId = null;
            service.Detail = (key, _) =>
            {
                requestedId = key;
                return Task.FromResult(Fixtures.Single(task => task.Summary.TaskId == key));
            };
            retry.PerformClick();
            Until(() => Detail(page).Contains(Fixtures[2].Description));
            Need(service.ListCalls == listCalls && service.DetailCalls == detailCalls + 1
                && requestedId == "opaque:2" && SelectedId(page) == "opaque:2",
                "detail retry calls only the selected detail service again for every typed failure");
            Need(retry.Text == "다시 조회", "successful detail retry restores list-refresh mode");
        }

        int previousLists = service.ListCalls;
        retry.PerformClick();
        Until(() => service.ListCalls == previousLists + 1 && Detail(page).Contains(Fixtures[2].Description));
        Need(SelectedId(page) == "opaque:2", "button after success refreshes the list and preserves selection");

        service.Detail = (key, _) => key == "opaque:2"
            ? Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.Unavailable))
            : Task.FromResult(Fixtures.Single(task => task.Summary.TaskId == key));
        Wait(page.RefreshAsync());
        Select(page, 3);
        Until(() => Detail(page).Contains(Fixtures[3].Description));
        previousLists = service.ListCalls;
        retry.PerformClick();
        Until(() => service.ListCalls == previousLists + 1 && Detail(page).Contains(Fixtures[3].Description));
        Need(retry.Text == "다시 조회" && SelectedId(page) == "opaque:3",
            "selecting another row discards the failed detail retry target");

        Select(page, 2);
        Until(() => status.Text.StartsWith("상세 조회 실패"));
        Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 1;
        Until(() => Detail(page).Contains(Fixtures[0].Description));
        Need(retry.Text == "다시 조회" && SelectedId(page) == "opaque:0",
            "filter changes discard the failed detail retry target");
        Select(page, 2);
        Until(() => status.Text.StartsWith("상세 조회 실패"));
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
        Until(() => Detail(page).Contains(Fixtures[0].Description));
        Need(retry.Text == "다시 조회" && SelectedId(page) == "opaque:0",
            "scope changes discard the failed detail retry target");

        service.Detail = (_, _) => Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.NotFound));
        Select(page, 2);
        Until(() => status.Text.StartsWith("상세 조회 실패"));
        previousLists = service.ListCalls;
        service.List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Array.Empty<TaskSummary>());
        var arguments = new object[] { Message.Create(IntPtr.Zero, 0x100, (IntPtr)Keys.F5, IntPtr.Zero), Keys.F5 };
        bool handled = (bool)typeof(Control).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Field<TextBox>(page, "txtContent"), arguments)!;
        Until(() => service.ListCalls == previousLists + 1 && Grid(page).Rows.Count == 0 && Detail(page).Contains("없"));
        Need(handled && retry.Text == "다시 조회" && NoFixtureDetail(page),
            "F5 from a child control refreshes the whole list after missing detail and clears the removed task");
        Need(retry.AccessibleDescription?.Contains("F5") == true, "list-refresh shortcut has an accessible explanation");
    }

    private static void DetailFreshness()
    {
        TaskSummary[] listed = Fixtures.Take(3)
            .Select(task => task.Summary with { Status = TaskDisplayStatus.InProgress }).ToArray();
        TaskSummary original = listed[1];
        TaskDetail fresh = new(original, "Current detail from the fixture");
        TaskListQuery? lastQuery = null;
        var service = new ProbeService
        {
            List = (query, _) =>
            {
                lastQuery = query;
                return Task.FromResult<IReadOnlyList<TaskSummary>>(listed);
            },
            Detail = (id, _) => Task.FromResult(id == original.TaskId
                ? fresh : new TaskDetail(listed.Single(task => task.TaskId == id), "Other current detail"))
        };
        using var page = new TasksControl(PreviewRole.Administrator, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
        Until(() => lastQuery?.Scope == TaskQueryScope.Managed && !Field<Label>(page, "lblTaskQueryStatus").Text.Contains("조회 중"));
        Select(page, 1);
        Until(() => PreparationId(page) == original.TaskId);
        var status = Field<Label>(page, "lblTaskQueryStatus");
        var retry = Field<Button>(page, "btnRetryQuery");
        Need(status.Text == "상세 조회 완료" && retry.Text == "다시 조회",
            "matching list and detail retain the ordinary refresh presentation");
        const string changedNotice = "상세 조회 완료 · 목록 정보 변경 감지 · 목록을 다시 조회해 주세요.";
        string[] oldCells = Grid(page).CurrentRow!.Cells.Cast<DataGridViewCell>()
            .Select(cell => cell.Value?.ToString() ?? "").ToArray();
        foreach (var changed in new[]
        {
            original with { Title = "Fresh title" },
            original with { AssigneeName = "Fresh assignee label" },
            original with { Status = TaskDisplayStatus.Submitted },
            original with { StartDate = original.StartDate.AddDays(-1) },
            original with { DueDate = original.DueDate.AddDays(1) }
        })
        {
            fresh = new(changed, "Current detail from the fixture");
            int lists = service.ListCalls, details = service.DetailCalls;
            Wait(page.RefreshAsync());
            Need(status.Text == changedNotice && retry.Text == "목록 새로고침" && retry.Enabled,
                "each changed summary field explains the old list snapshot and offers explicit list refresh");
            Need(service.ListCalls == lists + 1 && service.DetailCalls == details + 1,
                "detecting a difference does not silently fetch another list or detail");
            Need(Grid(page).CurrentRow!.Tag is TaskSummary stillListed && stillListed == original
                && oldCells.SequenceEqual(Grid(page).CurrentRow.Cells.Cast<DataGridViewCell>()
                    .Select(cell => cell.Value?.ToString() ?? "")),
                "fresh detail does not rewrite the list row or its snapshot tag");
            Need(SelectedId(page) == original.TaskId && lastQuery == new TaskListQuery(TaskQueryScope.Managed),
                "detecting a difference preserves the selection, scope and all-status condition");
        }

        fresh = new(original, "Only the detail description has changed");
        Wait(page.RefreshAsync());
        Need(status.Text == "상세 조회 완료" && retry.Text == "다시 조회"
            && Detail(page).Contains(fresh.Description),
            "description-only changes do not falsely mark unchanged summary fields as stale");

        Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 2;
        Until(() => lastQuery == new TaskListQuery(TaskQueryScope.Managed, TaskDisplayStatus.InProgress));
        Select(page, 1);
        Until(() => PreparationId(page) == original.TaskId);
        fresh = new(original with { Title = "Fresh preparation title", AssigneeName = "Fresh preparation assignee",
            StartDate = original.StartDate.AddDays(-1), DueDate = original.DueDate.AddDays(1) },
            "Fresh preparation description");
        Wait(page.RefreshAsync());
        Need(status.Text == changedNotice, "a filtered view can explain changed detail without changing its filter");
        WithModal<TaskEditForm>(() => Field<Button>(page, "btnEditTaskPreview").PerformClick(), form =>
        {
            Need(Field<TextBox>(form, "txtTaskTitle").Text == fresh.Summary.Title
                && Field<ComboBox>(form, "cmbAssignee").Text == fresh.Summary.AssigneeName
                && Field<TextBox>(form, "txtTaskDescription").Text == fresh.Description
                && DateOnly.FromDateTime(Field<DateTimePicker>(form, "dtpStartDate").Value) == fresh.Summary.StartDate
                && DateOnly.FromDateTime(Field<DateTimePicker>(form, "dtpDeadline").Value) == fresh.Summary.DueDate,
                "preparation opens from the fresh successful detail while the list snapshot stays intact");
        });

        Select(page, 0);
        Until(() => PreparationId(page) == listed[0].TaskId);
        Need(status.Text == "상세 조회 완료" && retry.Text == "다시 조회",
            "another matching selection clears the previous freshness notice");
        Select(page, 1);
        Until(() => PreparationId(page) == original.TaskId);
        Need(status.Text == changedNotice && retry.Text == "목록 새로고침",
            "reselecting the changed task restores its own freshness notice");

        var successfulDetail = service.Detail;
        service.Detail = (_, _) => Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.Unavailable));
        Wait(page.RefreshAsync());
        Need(status.Text.StartsWith("상세 조회 실패") && retry.Text == "상세 재시도",
            "detail failure replaces the freshness action with the existing detail retry");
        int beforeLists = service.ListCalls, beforeDetails = service.DetailCalls;
        service.Detail = successfulDetail;
        retry.PerformClick();
        Until(() => status.Text == changedNotice);
        Need(service.ListCalls == beforeLists && service.DetailCalls == beforeDetails + 1
            && retry.Text == "목록 새로고침", "successful detail retry returns to explicit list refresh when still different");

        listed = listed.Select(summary => summary.TaskId == original.TaskId ? fresh.Summary : summary).ToArray();
        beforeLists = service.ListCalls; beforeDetails = service.DetailCalls;
        retry.PerformClick();
        Until(() => service.ListCalls == beforeLists + 1 && service.DetailCalls == beforeDetails + 1
            && status.Text == "상세 조회 완료");
        Need(retry.Text == "다시 조회" && SelectedId(page) == original.TaskId
            && Field<ComboBox>(page, "cmbTaskScope").SelectedIndex == 1
            && Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex == 2
            && lastQuery == new TaskListQuery(TaskQueryScope.Managed, TaskDisplayStatus.InProgress),
            "explicit list refresh reloads list and detail once, preserving matching selection and current conditions");
        Need(Grid(page).CurrentRow!.Tag is TaskSummary refreshed && refreshed == fresh.Summary,
            "only an explicit list refresh replaces the old row snapshot");
    }

    private static void ServiceCancellation()
    {
        var service = PopulatedProbe();
        using var page = new TasksControl(PreviewRole.Employee, service);
        using var host = Host(page);
        Wait(page.RefreshAsync());
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        var status = Field<Label>(page, "lblTaskQueryStatus");
        var retry = Field<Button>(page, "btnRetryQuery");
        bool localTokenWasCancelled = false;
        service.List = (_, token) =>
        {
            localTokenWasCancelled = token.IsCancellationRequested;
            return Task.FromCanceled<IReadOnlyList<TaskSummary>>(new CancellationToken(true));
        };
        Wait(page.RefreshAsync());
        Need(!localTokenWasCancelled && status.Text.Contains("취소")
            && !status.Text.Contains("서버 오류") && Grid(page).Rows.Count == 0 && NoFixtureDetail(page),
            "service-level list cancellation is distinct and clears prior session data");
        Need(retry.Enabled && retry.Text == "다시 조회", "cancelled list allows explicit list retry");
        service.List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Fixtures.Select(task => task.Summary).ToArray());
        retry.PerformClick();
        Until(() => Detail(page).Contains(Fixtures[0].Description));
        Need(SelectedId(page) == "opaque:0", "cancelled session does not restore its previous selection");

        service.Detail = (_, token) =>
        {
            localTokenWasCancelled = token.IsCancellationRequested;
            return Task.FromCanceled<TaskDetail>(new CancellationToken(true));
        };
        Select(page, 2);
        Until(() => status.Text.Contains("취소"));
        Need(!localTokenWasCancelled && Grid(page).Rows.Count == 0 && NoFixtureDetail(page)
            && !status.Text.Contains("서버 오류") && retry.Enabled && retry.Text == "다시 조회",
            "service-level detail cancellation clears list and detail rather than retaining old identity data");
        Capture(page, "Detail-Service-Cancellation");
        service.Detail = (key, _) => Task.FromResult(Fixtures.Single(task => task.Summary.TaskId == key));
        int previousLists = service.ListCalls;
        retry.PerformClick();
        Until(() => service.ListCalls == previousLists + 1 && Detail(page).Contains(Fixtures[0].Description));
        Need(SelectedId(page) == "opaque:0", "detail cancellation retry reloads the list and rechecks the service");

        var pending = new List<TaskCompletionSource<IReadOnlyList<TaskSummary>>>();
        service.List = (_, _) =>
        {
            var source = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(source);
            return source.Task;
        };
        var older = page.RefreshAsync();
        Until(() => pending.Count == 1);
        var newer = page.RefreshAsync();
        Until(() => pending.Count == 2);
        pending[1].SetResult(Fixtures.Select(task => task.Summary).ToArray());
        Wait(newer);
        pending[0].SetCanceled(new CancellationToken(true));
        Wait(older);
        Need(Grid(page).Rows.Count == Fixtures.Length && Detail(page).Contains(Fixtures[0].Description)
            && status.Text == "상세 조회 완료", "superseded local cancellation cannot clear a newer successful query");

        var cancelledDetail = new TaskCompletionSource<TaskDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Detail = (key, _) => key == "opaque:1" ? cancelledDetail.Task
            : Task.FromResult(Fixtures.Single(task => task.Summary.TaskId == key));
        Select(page, 1);
        Until(() => status.Text.Contains("조회 중"));
        Select(page, 2);
        Until(() => Detail(page).Contains(Fixtures[2].Description));
        cancelledDetail.SetCanceled(new CancellationToken(true));
        Pump();
        Need(Grid(page).Rows.Count == Fixtures.Length && Detail(page).Contains(Fixtures[2].Description)
            && status.Text == "상세 조회 완료", "superseded detail cancellation cannot clear the latest selected detail");
    }

    private static ProbeService PopulatedProbe() => new()
    {
        List = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Fixtures.Select(task => task.Summary).ToArray()),
        Detail = (key, _) => Task.FromResult(Fixtures.Single(task => task.Summary.TaskId == key))
    };

    private static TaskDetail Extra(string key) => new(new(key, key, "테스트 담당자", TaskDisplayStatus.InProgress,
        new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 30)), key);
    private static Form Host(TasksControl page)
    {
        var host = new Form { ClientSize = new Size(1320, 780), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        host.Show();
        Application.DoEvents();
        return host;
    }
    private static T Field<T>(object target, string name) where T : class =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException("Missing field " + name));
    private static DataGridView Grid(TasksControl page) => Field<DataGridView>(page, "gridTasks");
    private static string Detail(TasksControl page) => Field<TextBox>(page, "txtContent").Text;
    private static bool NoFixtureDetail(TasksControl page) => !Fixtures.Any(x => Detail(page).Contains(x.Description));
    private static string RowId(TasksControl page, int index) => Grid(page).Rows[index].Cells[0].Value?.ToString() ?? "";
    private static string SelectedId(TasksControl page) => Grid(page).CurrentRow?.Cells[0].Value?.ToString() ?? "";
    private static void Select(TasksControl page, int index) => Grid(page).CurrentCell = Grid(page).Rows[index].Cells[0];
    private static string AllText(Control control) => control.Text + " " + string.Join(" ", control.Controls.Cast<Control>().Select(AllText));
    private static void Wait(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("UI/query condition did not complete.");
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Application.DoEvents();
    }
    private static void Pump()
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 120) { Application.DoEvents(); Thread.Sleep(5); }
    }
    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
    private static void ExpectFailure(Action action, TaskQueryFailure expected)
    {
        try { action(); throw new InvalidOperationException("Expected " + expected); }
        catch (TaskQueryException error) { Need(error.Failure == expected, "typed query failure"); }
    }
    private static void Run(string name, Action action) { action(); completed.Add(name); Console.WriteLine("PASS " + name); }
    private static void Capture(Control control, string name)
    {
        if (captureDirectory == null) return;
        using var image = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(image, new Rectangle(Point.Empty, control.Size));
        image.Save(Path.Combine(captureDirectory, name + ".png"), ImageFormat.Png);
    }
    private static void DumpControlState(Control control)
    {
        var state = typeof(Control).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(control);
        Console.Error.WriteLine($"STATE {control.Name}: {state}");
        foreach (Control child in control.Controls) DumpControlState(child);
    }
    private sealed class ProbeService : ITaskQueryService
    {
        public string SourceDescription => "테스트 대역 · 실제 서버 연결 아님";
        public int ListCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public Func<TaskListQuery, CancellationToken, Task<IReadOnlyList<TaskSummary>>> List { get; set; }
            = (_, _) => Task.FromResult<IReadOnlyList<TaskSummary>>(Array.Empty<TaskSummary>());
        public Func<string, CancellationToken, Task<TaskDetail>> Detail { get; set; }
            = (key, _) => Task.FromResult(Extra(key));
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return List(query, cancellationToken);
        }
        public Task<TaskDetail> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Detail(taskId, cancellationToken);
        }
    }
}
