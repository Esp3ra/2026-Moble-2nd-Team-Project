using System.Collections;
using System.Reflection;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static int scenarios;
    private static Form host = null!;
    private static readonly string[] Targets = { "전 직원", "기획팀", "개발팀", "디자인팀" };
    private static readonly List<Notice> Store = new();
    private static readonly TestNoticeService Notices = new(Store);

    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = true;
        var saved = Store.ToArray();
        int result = 1;
        using var coordinator = new Form
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000), Size = new Size(1100, 760)
        };
        host = coordinator;
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                Check(Application.MessageLoop &&
                    SynchronizationContext.Current is WindowsFormsSynchronizationContext,
                    "Real WinForms message loop and UI synchronization context");
                Run("authoring targets and exact administrator/employee filters", TargetFilters);
                Run("registration outside the current filter", RegistrationWhileFiltered);
                Run("pinned order, column sorting and query-only detail", SortingAndDetails);
                Run("empty list and administrator/employee deletion", EmptyAndDeletion);
                Run("local chat and store lifetime across navigation", ChatRegression);
                Console.WriteLine($"PASS: {checks} assertions; {scenarios} scenarios. " +
                    "Actual WinForms controls and explicitly injected test service; no server, DB or network.");
                result = 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"FAIL after {checks} assertions: {error}");
            }
            finally
            {
                Store.Clear();
                foreach (var notice in saved) Store.Add(notice);
                coordinator.Close();
            }
        }));
        Application.Run(coordinator);
        return result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void Run(string name, Action test)
    {
        Store.Clear();
        test();
        scenarios++;
        Console.WriteLine("PASS " + name);
    }

    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static object? Invoke(object owner, string name, params object?[] args) =>
        owner.GetType().GetMethod(name, Hidden | BindingFlags.Public)!.Invoke(owner, args);
    private static void Mount(Control page)
    {
        host.Controls.Clear();
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        page.CreateControl();
    }
    private static DataGridView Grid(NoticesControl page) => Field<DataGridView>(page, "gridNotices");
    private static ComboBox Filter(NoticesControl page) => Field<ComboBox>(page, "cmbOptions2");
    private static Notice[] Rows(NoticesControl page) =>
        Grid(page).Rows.Cast<DataGridViewRow>().Select(row => (Notice)row.Tag!).ToArray();

    // Drive the real new-notice handler and its modal editor. No file picker or filesystem I/O.
    private static Notice Publish(NoticesControl page, string target, string title, bool pinned = false)
    {
        Notice? published = null;
        Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 10 };
        timer.Tick += (_, _) =>
        {
            var editor = Application.OpenForms.OfType<NoticeEditForm>().SingleOrDefault();
            if (editor is null) return;
            timer.Stop();
            try
            {
                Field<TextBox>(editor, "txtNoticeTitle").Text = title;
                Field<TextBox>(editor, "txtNoticeBody").Text = "본문: " + title;
                Field<ComboBox>(editor, "cmbNoticeAudience").SelectedItem = target;
                Field<CheckBox>(editor, "chkPinToTop").Checked = pinned;
                Field<Button>(editor, "btnPublishNotice").PerformClick();
                published = editor.CreatedNotice;
            }
            catch (Exception error)
            {
                failure = error;
                editor.DialogResult = DialogResult.Cancel;
                editor.Close();
            }
        };
        timer.Start();
        Invoke(page, "btnNewNoticePreview_Click", null, EventArgs.Empty);
        timer.Stop();
        if (failure is not null) throw new InvalidOperationException("Modal notice registration", failure);
        Check(published is not null && Store.Contains(published), "Registered notice reaches the explicit test service");
        Check(published!.Target == target && published.Title == title, "Published target/title preserved");
        return published;
    }

    private static void TargetFilters()
    {
        using var administrator = new NoticesControl(PreviewRole.Administrator, Notices);
        Mount(administrator);
        using (var editor = new NoticeEditForm())
        {
            var authored = Field<ComboBox>(editor, "cmbNoticeAudience").Items.Cast<string>().ToArray();
            var filtered = Filter(administrator).Items.Cast<string>().Skip(1).ToArray();
            Check(authored.SequenceEqual(Targets), "Known authoring audience set");
            Check(authored.SequenceEqual(filtered), "Every authoring audience must have a list filter");
        }
        foreach (string target in Targets) Publish(administrator, target, target + " 공지", target == "디자인팀");
        Filter(administrator).SelectedItem = "전체";
        Check(Rows(administrator).Length == Targets.Length, "All notices visible under 전체");
        Check(Field<Label>(administrator, "lblText4").Text == "공지사항 상세", "Registration waits for query");

        foreach (var role in new[] { PreviewRole.Administrator, PreviewRole.Employee })
        {
            using var page = new NoticesControl(role, Notices);
            Mount(page);
            Check(Filter(page).Items.Cast<string>().SequenceEqual(new[] { "전체" }.Concat(Targets)),
                "Administrator and employee use the same audiences");
            foreach (string target in Targets)
            {
                Filter(page).SelectedItem = target;
                Check(Rows(page).Length == 1 && Rows(page).Single().Target == target,
                    "Exact target filtering without cross-team notices: " + target);
                Field<Button>(page, "btnQuery").PerformClick();
                Check(Field<Label>(page, "lblText4").Text == target + " 공지",
                    "Query shows the selected team's notice");
                Check(Field<TextBox>(page, "txtContent").Text.Contains("본문: " + target),
                    "Selected notice body is displayed");
            }
            Filter(page).SelectedItem = "전체";
            Check(Rows(page).Length == Targets.Length, "전체 restores all audience notices");
        }
    }

    private static void RegistrationWhileFiltered()
    {
        using var page = new NoticesControl(PreviewRole.Administrator, Notices);
        Mount(page);
        var development = Publish(page, "개발팀", "개발팀 기존 공지");
        Filter(page).SelectedItem = "개발팀";
        var design = Publish(page, "디자인팀", "필터 밖 디자인팀 공지");
        Check(Filter(page).Text == "개발팀", "Registration preserves the current filter");
        Check(Rows(page).SequenceEqual(new[] { development }), "New design notice does not leak into development");
        Filter(page).SelectedItem = "디자인팀";
        Check(Rows(page).SequenceEqual(new[] { design }), "New design notice is available under its own filter");
        Check(Field<Label>(page, "lblText4").Text == "공지사항 상세", "Filter change clears stale detail");
        Field<Button>(page, "btnQuery").PerformClick();
        Check(Field<Label>(page, "lblText4").Text == design.Title, "Design notice opens using query");
    }

    private static void ClickHeader(NoticesControl page, int column)
    {
        var args = new DataGridViewCellMouseEventArgs(column, -1, 0, 0,
            new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
        typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick", Hidden)!
            .Invoke(Grid(page), new object[] { args });
    }

    private static void SortingAndDetails()
    {
        var pinned = new Notice { Id = 1, Target = "디자인팀", Title = "고정", Author = "다", IsPinned = true,
            CreatedAt = new DateTime(2026, 1, 1), Content = "고정 본문" };
        var first = new Notice { Id = 2, Target = "디자인팀", Title = "가", Author = "나",
            CreatedAt = new DateTime(2026, 1, 3), Content = "선택한 상세 본문" };
        var second = new Notice { Id = 3, Target = "디자인팀", Title = "나", Author = "가",
            CreatedAt = new DateTime(2026, 1, 2), Content = "다른 본문" };
        foreach (var notice in new[] { second, pinned, first }) Store.Add(notice);
        Store.Add(new Notice { Id = 4, Target = "개발팀", Title = "다른 팀", IsPinned = true });
        using var page = new NoticesControl(PreviewRole.Administrator, Notices);
        Mount(page);
        Filter(page).SelectedItem = "디자인팀";
        Check(ReferenceEquals(Rows(page)[0], pinned), "Pinned design notice leads the filtered list");
        ClickHeader(page, 1);
        Check(Rows(page).SequenceEqual(new[] { pinned, first, second }), "Title ascending within the pinned group");
        ClickHeader(page, 1);
        Check(Rows(page).SequenceEqual(new[] { pinned, second, first }), "Title descending within the pinned group");
        foreach (int column in new[] { 0, 2, 3 })
        {
            for (int direction = 0; direction < 2; direction++)
            {
                ClickHeader(page, column);
                Check(ReferenceEquals(Rows(page)[0], pinned), "Pinned order survives column sort");
                Check(Rows(page).Length == 3 && Rows(page).All(n => n.Target == "디자인팀"),
                    "Column sort keeps the target filter");
            }
        }
        var selected = Grid(page).Rows.Cast<DataGridViewRow>().Single(row => ReferenceEquals(row.Tag, first));
        Grid(page).CurrentCell = selected.Cells[0];
        Check(Field<Label>(page, "lblText4").Text == "공지사항 상세", "Row selection alone does not open detail");
        Field<Button>(page, "btnQuery").PerformClick();
        Check(Field<Label>(page, "lblText4").Text == first.Title &&
            Field<TextBox>(page, "txtContent").Text.Contains(first.Content), "Query opens the selected design notice");
        ClickHeader(page, 1);
        Check(ReferenceEquals(Grid(page).CurrentRow!.Tag, first), "Sorting preserves the selected notice");
        Field<Button>(page, "btnQuery").PerformClick();
        Check(Field<TextBox>(page, "txtContent").Text.Contains(first.Content), "Selection remains queryable after sorting");
    }

    private static void EmptyAndDeletion()
    {
        var notice = new Notice { Id = 5, Target = "디자인팀", Title = "삭제할 디자인 공지", Content = "본문" };
        Store.Add(notice);
        using var employee = new NoticesControl(PreviewRole.Employee, Notices);
        Mount(employee);
        Check(!Field<Button>(employee, "btnNewNoticePreview").Visible, "Employee has no notice authoring button");
        Invoke(employee, "DeleteNotice", notice);
        Check(Store.Contains(notice), "Employee deletion is rejected");
        using var administrator = new NoticesControl(PreviewRole.Administrator, Notices);
        Mount(administrator);
        Filter(administrator).SelectedItem = "디자인팀";
        Check(Field<Button>(administrator, "btnNewNoticePreview").Visible, "Administrator can author notices");
        Field<Button>(administrator, "btnQuery").PerformClick();
        Invoke(administrator, "DeleteNotice", notice);
        Check(Store.Count == 0 && Rows(administrator).Length == 0, "Administrator deletes the design notice");
        Check(Grid(administrator).CurrentRow is null, "Empty result clears the selection");
        Check(Field<Label>(administrator, "lblText4").Text == "공지사항 상세" &&
            Field<TextBox>(administrator, "txtContent").Text == "조회할 공지사항이 없습니다.",
            "Empty result clears the former notice detail");
        foreach (string target in Targets)
        {
            Filter(administrator).SelectedItem = target;
            Check(Rows(administrator).Length == 0 && Grid(administrator).CurrentRow is null,
                "Empty target remains empty: " + target);
        }
    }

    // Existing chat types remain internal; exercise their real store without changing production access.
    private static object New(Type type, params object[] args) => Activator.CreateInstance(type,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;
    private static object? Result(object owner, string method, params object?[] args)
    {
        var task = (Task)Invoke(owner, method, args)!;
        task.GetAwaiter().GetResult();
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }
    private static object Property(object owner, string property) =>
        owner.GetType().GetProperty(property)!.GetValue(owner)!;

    private static void ChatRegression()
    {
        var assembly = typeof(NoticesControl).Assembly;
        Type Type(string name) => assembly.GetType("RemoteWork.WinForms.Features.Collaboration." + name)!;
        var participant = New(Type("ChatParticipant"), "test-user", "검증 사용자", "디자인팀");
        var service = New(Type("InMemoryChatService"), participant);
        var request = New(Type("CreateChatRoomRequest"), "로컬 검증 그룹", true,
            new[] { "sample-kim", "sample-lee" });
        var room = Result(service, "CreateRoomAsync", request, CancellationToken.None)!;
        var id = (string)Property(room, "Id");
        Check(((IEnumerable)Property(room, "Participants")).Cast<object>().Count() == 3, "Group participants retained");
        Result(service, "SendMessageAsync", id, "로컬 메시지", CancellationToken.None);
        var history = ((IEnumerable)Result(service, "GetMessagesAsync", id, CancellationToken.None)!).Cast<object>().ToArray();
        Check(history.Length == 1 && (string)Property(history[0], "Content") == "로컬 메시지",
            "Local chat send/read still works");
        Result(service, "DeleteRoomAsync", id, CancellationToken.None);
        Check(!((IEnumerable)Result(service, "GetRoomsAsync", CancellationToken.None)!).Cast<object>()
            .Any(candidate => (string)Property(candidate, "Id") == id), "Local room deletion still works");

        using var main = new MainForm(PreviewRole.Administrator);
        var shared = Field<object>(main, "chatService");
        Invoke(main, "btnChat_Click", null, EventArgs.Empty);
        Check(ReferenceEquals(Field<object>(Field<object>(main, "currentPage"), "chatService"), shared),
            "Chat page receives the MainForm store");
        Result(shared, "SendMessageAsync", "planning", "메뉴 왕복 보존", CancellationToken.None);
        Invoke(main, "btnNotices_Click", null, EventArgs.Empty);
        Invoke(main, "btnChat_Click", null, EventArgs.Empty);
        Check(ReferenceEquals(Field<object>(Field<object>(main, "currentPage"), "chatService"), shared),
            "Navigation retains the same store");
        Check(((IEnumerable)Result(shared, "GetMessagesAsync", "planning", CancellationToken.None)!)
            .Cast<object>().Any(message => (string)Property(message, "Content") == "메뉴 왕복 보존"),
            "Navigation retains the message history");
    }
}


// Test-only storage; production controls always use HttpNoticeService by default.
internal sealed class TestNoticeService(List<Notice> store) : INoticeService
{
    private int nextId = 100;
    public Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Notice>>(store.ToArray());
    public Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(store.Single(n => n.Id == id));
    public Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default)
    {
        var notice = new Notice { Id = ++nextId, AuthorId = 1, Author = "테스트 관리자",
            Title = request.Title, Content = request.Content, Target = request.Target,
            IsPinned = request.IsPinned, AttachmentNames = request.AttachmentNames };
        store.Add(notice);
        return Task.FromResult(notice);
    }
    public Task DeleteNoticeAsync(int id, CancellationToken ct = default)
    {
        store.RemoveAll(n => n.Id == id);
        return Task.CompletedTask;
    }
}
