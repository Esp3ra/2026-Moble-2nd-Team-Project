using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

// Real WinForms controls and WM_CHAR input with explicit HTTP response doubles.
// No production server, accounts, result writes or file uploads are used.
internal static class Program
{
    private static int checks;
    private static string? capture;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static int Main(string[] args)
    {
        capture = args.FirstOrDefault();
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using var dispatcher = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual };
        int result = 0;
        dispatcher.Shown += (_, _) => dispatcher.BeginInvoke((Action)(() =>
        {
            try
            {
                RetryAndNativeInput();
                BlockedContexts();
                LoadingAndSessionChange();
                PendingRequestCannotRefresh();
                Console.WriteLine($"PASS {checks} submission UI checks (HTTP doubles; native WM_CHAR input; no business writes).");
            }
            catch (Exception error) { Console.WriteLine(error); result = 1; }
            finally { dispatcher.Close(); }
        }));
        Application.Run(dispatcher);
        return result;
    }

    private static void RetryAndNativeInput()
    {
        using var fixture = new Fixture("unavailable");
        using var form = fixture.Open();
        var description = Field<TextBox>(form, "txtResultDescription");
        var status = Field<TextBox>(form, "txtSubmissionStatus");
        var retry = Field<Button>(form, "btnRetryTask");
        Until(() => retry.Enabled);
        Check(Field<TabControl>(form, "tabDetails").SelectedIndex == 0 && status.Visible
            && status.Text.Contains("검토자") && status.Text.Contains("REVIEWER_UNAVAILABLE"),
            "initial provider error is visible outside the unselected previous-submission tab");
        Check(Field<TextBox>(form, "txtTaskSummary").Text.Contains("검증 업무")
            && Field<TextBox>(form, "txtSubmissionVersion").Text.Contains("검증 직원"),
            "authorized task and assignee remain visible when reviewer lookup fails");
        Type(description, "차단");
        Check(description.ReadOnly && description.Text == "" && !Field<Button>(form, "btnSubmit").Enabled,
            "native character input and submit stay blocked before a valid context");
        CheckLayout(form, "error-minimum", scale: 1);
        fixture.Mode = "ready";
        retry.PerformClick();
        Until(() => Field<Button>(form, "btnSubmit").Enabled);
        Type(description, "한글 결과\r두 번째 줄");
        Check(!description.ReadOnly && description.Text.Replace("\r\n", "\n") == "한글 결과\n두 번째 줄",
            "valid context permits real native edit input including Korean and newline");
        Check(status.Text.Contains("제출 준비 완료") && Field<TextBox>(form, "txtSubmissionVersion").Text.Contains("1차"),
            "ready status and actual round are visible");
        string draft = description.Text;
        fixture.Mode = "unavailable";
        retry.PerformClick();
        Until(() => retry.Enabled);
        Check(description.Text == draft && description.ReadOnly && !Field<Button>(form, "btnSubmit").Enabled,
            "retry failure preserves draft but invalidates the old ready context");
        Type(description, "변경 금지");
        Check(description.Text == draft, "readonly failure cannot accept native characters");
        fixture.Mode = "ready";
        retry.PerformClick();
        Until(() => Field<Button>(form, "btnSubmit").Enabled);
        Check(description.Text == draft, "successful GET-only retry preserves the draft");
        CheckLayout(form, "ready-scaled", scale: 1.5f);
        Check(fixture.BusinessWrites == 0, "context retry and typing issue no business writes");
    }

    private static void BlockedContexts()
    {
        foreach (string mode in new[] { "past", "server-past", "returned", "extension", "server-error", "wrong-version", "forbidden" })
        {
            using var fixture = new Fixture(mode);
            using var form = fixture.Open();
            Until(() => !Field<bool>(form, "busy"));
            var description = Field<TextBox>(form, "txtResultDescription");
            var status = Field<TextBox>(form, "txtSubmissionStatus");
            Type(description, "입력 금지");
            Check(description.ReadOnly && description.Text == "" && !Field<Button>(form, "btnSubmit").Enabled,
                mode + " cannot enable typing or submission");
            Check(status.Visible && status.Text.Length > 10 && !status.Text.Contains("준비 완료"),
                mode + " exposes its blocking reason without a tab switch");
            if (mode == "forbidden")
                Check(!Field<Button>(form, "btnRetryTask").Enabled && Field<TextBox>(form, "txtTaskSummary").Text == "",
                    "authorization loss clears task data and disables retry");
            Check(fixture.BusinessWrites == 0, mode + " makes no writes");
        }
    }

    private static void LoadingAndSessionChange()
    {
        using var fixture = new Fixture("loading");
        using var form = fixture.Open();
        Until(() => fixture.Pending is not null);
        var description = Field<TextBox>(form, "txtResultDescription");
        Check(Field<TextBox>(form, "txtSubmissionStatus").Text.Contains("확인하고")
            && description.ReadOnly && !Field<Button>(form, "btnRetryTask").Enabled, "loading is visible and cannot restart concurrently");
        fixture.Api.ClearIdentity();
        Until(() => Field<TextBox>(form, "txtSubmissionStatus").Text.Contains("접속이 변경")
            || Field<TextBox>(form, "txtSubmissionStatus").Text.Contains("취소"));
        Type(description, "누출 금지");
        Check(description.Text == "" && Field<TextBox>(form, "txtTaskSummary").Text == ""
            && !Field<Button>(form, "btnRetryTask").Enabled, "session cancellation clears data and leaves input blocked");
    }

    private static void PendingRequestCannotRefresh()
    {
        using var fixture = new Fixture("ready");
        var api = new ResultsApi(fixture.Api.CaptureSession());
        var pending = api.Begin("submission.submitted", "1", "results/tasks/1/submissions", "1",
            new SubmitResultBody("1", 1, "아직 확인되지 않은 기존 설명", []), []);
        typeof(PendingResultWrite).GetProperty("Stage")!.SetValue(pending, "check");
        using var form = fixture.Open();
        Until(() => !Field<bool>(form, "busy"));
        int gets = fixture.Gets;
        Field<Button>(form, "btnRetryTask").PerformClick();
        typeof(SubmissionForm).GetMethod("RetryTask_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [null, EventArgs.Empty]);
        Application.DoEvents();
        Check(!Field<Button>(form, "btnRetryTask").Enabled && fixture.Gets == gets
            && ReferenceEquals(api.Pending("submission.submitted", "1"), pending),
            "ambiguous pending submission cannot be overwritten by retry or direct retry handler");
        Check(Field<TextBox>(form, "txtResultDescription").ReadOnly
            && Field<Button>(form, "btnSubmit").Text == "처리 결과 확인", "pending flow retains its existing result-recovery action");
    }

    private static void CheckLayout(SubmissionForm form, string label, float scale)
    {
        form.Size = form.MinimumSize;
        if (scale != 1) form.Scale(new SizeF(scale, scale));
        form.PerformLayout(); Application.DoEvents();
        var status = Field<TextBox>(form, "txtSubmissionStatus");
        var retry = Field<Button>(form, "btnRetryTask");
        foreach (Control control in new Control[] { status, retry, Field<Button>(form, "btnClose") })
        {
            Rectangle bounds = form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
            Check(bounds.Width > 20 && bounds.Height > 20 && form.ClientRectangle.Contains(bounds),
                label + " keeps " + control.Name + " inside the form");
        }
        Check(status.Height >= status.Font.Height * 2 && status.ScrollBars == ScrollBars.Vertical,
            label + " keeps long status messages readable and scrollable");
        if (capture is null) return;
        Directory.CreateDirectory(capture);
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine(capture, label + ".png"));
    }

    private static void Type(TextBox target, string value)
    {
        target.Focus();
        foreach (char character in value) SendMessageW(target.Handle, 0x0102, (IntPtr)character, IntPtr.Zero); // WM_CHAR
        Application.DoEvents();
    }
    private static T Field<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks++; }
    private static void Until(Func<bool> ready)
    {
        var timer = Stopwatch.StartNew();
        while (!ready()) { if (timer.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Submission UI wait"); Application.DoEvents(); Thread.Sleep(1); }
        Application.DoEvents();
    }

    private sealed class Fixture : HttpMessageHandler
    {
        public ApiClient Api { get; }
        public string Mode;
        public int Gets, BusinessWrites;
        public TaskCompletionSource<HttpResponseMessage>? Pending;
        private bool disposed;
        public Fixture(string mode)
        {
            Mode = mode;
            Api = new ApiClient("http://submission-ui-fixture.invalid/", this);
            var login = Api.LoginAsync("fixture", "fixture-password"); Until(() => login.IsCompleted); login.GetAwaiter().GetResult();
        }
        public SubmissionForm Open()
        {
            var queries = new HttpTaskQueryService(Api).CaptureReadSession();
            var form = (SubmissionForm)Activator.CreateInstance(typeof(SubmissionForm), BindingFlags.Instance | BindingFlags.NonPublic,
                null, [PreviewRole.Employee, Api.CaptureSession(), queries], null)!;
            form.Tag = new Dictionary<string, string> { ["TaskId"] = "1" };
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.Show(); return form;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/auth/login") return Ok(new { access_token = "owned-ui-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
                user = new { id = 1, login_id = "fixture", display_name = "검증 직원", employee_no = "TEST-1", department = "TEST", role = "employee", is_active = true } });
            if (request.Method != HttpMethod.Get) { BusinessWrites++; throw new InvalidOperationException("Unexpected business write in read-only UI fixture"); }
            Gets++;
            string status = Mode == "returned" ? "returned" : "in_progress";
            string due = Mode == "past" ? "2000-01-01" : "2099-01-01";
            if (path == "/api/v1/tasks/1") return Ok(new { task = new { task_id = "1", title = "검증 업무", assignee_id = 1, assignee_name = "검증 직원", task_status = status,
                start_date = "2000-01-01", due_date = due, description = "Controlled fixture" } });
            if (path == "/api/v1/tasks/1/write-context") return Ok(new { context = new { task_id = "1", assignee_id = 1, manager_user_ids = new[] { 2 }, row_version = "1",
                assignment_revision = 1, task_status = status, updated_at = "2026-10-26T00:00:00Z" } });
            if (path == "/api/v1/results/tasks/1/context")
            {
                if (Mode == "unavailable") return Error(409, "REVIEWER_UNAVAILABLE", "현재 지정된 검토자가 없습니다.");
                if (Mode == "server-past") return Error(409, "SUBMISSION_AFTER_DUE_DATE", "마감일이 지났습니다. 연장 승인 후 다시 제출해 주세요.");
                if (Mode == "extension") return Error(409, "DEADLINE_EXTENSION_PENDING", "연장 승인 대기 중에는 제출할 수 없습니다.");
                if (Mode == "server-error") return Error(503, "DATABASE_UNAVAILABLE", "데이터베이스 연결 상태를 확인해 주세요.");
                if (Mode == "forbidden") return Error(403, "FORBIDDEN", "현재 업무 권한을 확인해 주세요.");
                if (Mode == "loading") { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); token.Register(() => Pending.TrySetCanceled(token)); return Pending.Task; }
                return Ok(new { task_id = "1", title = "검증 업무", assignee_id = 1, row_version = Mode == "wrong-version" ? "2" : "1", assignment_revision = 1,
                    task_status = status, due_date = due, reviewer_id = 2, extension_pending = false, next_round_no = "1" });
            }
            if (path == "/api/v1/results/tasks/1/submissions") return Ok(new { items = Array.Empty<object>() });
            throw new InvalidOperationException("Unexpected fixture GET: " + path);
        }
        private static Task<HttpResponseMessage> Ok(object data) => Reply(200, new { ok = true, data, request_id = "owned-ui-check" });
        private static Task<HttpResponseMessage> Error(int status, string code, string message) => Reply(status, new { ok = false, error = new { code, message }, request_id = "owned-ui-check" });
        private static Task<HttpResponseMessage> Reply(int status, object body) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
        protected override void Dispose(bool disposing)
        { if (disposing && !disposed) { disposed = true; Api.Dispose(); } base.Dispose(disposing); }
    }
}
