using System.Numerics;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class HistoryControl : UserControl
    {
        private readonly ApiClient.Session? session;
        private readonly ResultsApi? results;
        private readonly CancellationTokenSource closed = new();
        private CancellationTokenSource? queryCancellation;
        private CancellationTokenRegistration sessionRegistration;
        private int queryNumber;
        private bool updating;
        private List<HistoryRow> history = new();

        private sealed record TaskOption(string? Id, string Text)
        {
            public override string ToString() => Text;
        }
        private sealed record HistoryRow(TaskSummary Task, ResultHistoryItem Event);

        public HistoryControl()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            if (AppServices.Api.CurrentUser != null)
            {
                try
                {
                    session = AppServices.Api.CaptureSession();
                    results = new ResultsApi(session, ResultsConnection.Files);
                }
                catch (InvalidOperationException) { }
            }
            updating = true;
            cmbOptions.Items.Clear();
            cmbOptions.Items.Add(new TaskOption(null, "전체 업무"));
            cmbOptions.SelectedIndex = 0;
            cmbOptions2.Items.Clear();
            cmbOptions2.Items.AddRange(new object[] { "전체 변경 유형", "생성", "담당자 변경",
                "상태 변경", "진행률 변경", "결과 제출", "반려", "승인", "기타" });
            cmbOptions2.SelectedIndex = 0;
            updating = false;
            ClearView("조회할 업무 이력이 없습니다.");
            cmbOptions.SelectedIndexChanged += (_, _) => ApplyFilter();
            cmbOptions2.SelectedIndexChanged += (_, _) => ApplyFilter();
            gridHistory.SelectionChanged += (_, _) => ShowSelectedHistory();
            Load += async (_, _) =>
            {
                if (session != null)
                    sessionRegistration = session.Cancellation.Register(PostClear);
                await RefreshHistoryAsync();
            };
            Disposed += (_, _) =>
            {
                closed.Cancel();
                queryCancellation?.Cancel();
                queryCancellation?.Dispose();
                sessionRegistration.Dispose();
            };
        }

        private async Task RefreshHistoryAsync()
        {
            int request = ++queryNumber;
            queryCancellation?.Cancel();
            queryCancellation?.Dispose();
            queryCancellation = null;
            ClearView("업무 이력을 조회하고 있습니다.");
            if (session == null || results == null || !session.IsCurrent)
            {
                ClearView("로그인 후 업무 이력을 조회해 주세요.");
                return;
            }
            if (session.User.Role != "admin")
            {
                ClearView("관리자만 업무 이력을 조회할 수 있습니다.");
                return;
            }
            queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation, closed.Token);
            CancellationToken token = queryCancellation.Token;
            string? selected = (cmbOptions.SelectedItem as TaskOption)?.Id;
            try
            {
                var tasks = await results.HistoryTasksAsync(token);
                var rows = new List<HistoryRow>();
                foreach (TaskSummary task in tasks)
                {
                    string? cursor = null;
                    var seen = new HashSet<string>();
                    do
                    {
                        token.ThrowIfCancellationRequested();
                        var page = await results.HistoryAsync(task.TaskId, cursor, token, true);
                        foreach (var item in page.Items)
                        {
                            if (!seen.Add(item.EventId))
                                throw new InvalidDataException("업무 이력에 중복된 기록이 있습니다.");
                            rows.Add(new HistoryRow(task, item));
                        }
                        string? next = page.NextBeforeEventId;
                        if (next != null && (page.Items.Count == 0 ||
                            (cursor != null && BigInteger.Parse(next) >= BigInteger.Parse(cursor))))
                            throw new InvalidDataException("업무 이력의 다음 조회 위치를 확인해 주세요.");
                        cursor = next;
                    } while (cursor != null);
                }
                if (!CanDisplay(request, token)) return;
                history = rows.OrderByDescending(r => r.Event.OccurredAt)
                    .ThenByDescending(r => BigInteger.Parse(r.Event.EventId)).ToList();
                updating = true;
                try
                {
                    cmbOptions.Items.Clear();
                    cmbOptions.Items.Add(new TaskOption(null, "전체 업무"));
                    foreach (TaskSummary task in tasks)
                        cmbOptions.Items.Add(new TaskOption(task.TaskId, $"{task.TaskId} · {task.Title}"));
                    cmbOptions.SelectedIndex = 0;
                    for (int i = 1; i < cmbOptions.Items.Count; i++)
                        if (cmbOptions.Items[i] is TaskOption option && option.Id == selected)
                            cmbOptions.SelectedIndex = i;
                }
                finally { updating = false; }
                ApplyFilter();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is ApiException or InvalidDataException or
                InvalidOperationException or FormatException or IOException)
            {
                if (CanDisplay(request, token)) ClearView(error.Message);
            }
        }

        private async void Unavailable_Click(object? sender, EventArgs e)
        {
            await RefreshHistoryAsync();
        }

        private bool CanDisplay(int request, CancellationToken token) =>
            !IsDisposed && !Disposing && !closed.IsCancellationRequested &&
            !token.IsCancellationRequested && session?.IsCurrent == true && request == queryNumber;

        private void ApplyFilter()
        {
            if (updating || IsDisposed || Disposing) return;
            if (session?.IsCurrent != true) { ClearView("다시 로그인해 주세요."); return; }
            string? taskId = (cmbOptions.SelectedItem as TaskOption)?.Id;
            string? kind = cmbOptions2.SelectedIndex > 0 ? cmbOptions2.Text : null;
            updating = true;
            try
            {
                gridHistory.Rows.Clear();
                foreach (HistoryRow row in history)
                {
                    if (taskId != null && row.Task.TaskId != taskId) continue;
                    if (kind != null && KindText(row.Event.Kind) != kind) continue;
                    int index = gridHistory.Rows.Add(row.Task.TaskId, row.Event.ActorName,
                        row.Event.OccurredAt.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss"),
                        KindText(row.Event.Kind), row.Event.Summary);
                    gridHistory.Rows[index].Tag = row;
                }
                if (gridHistory.Rows.Count > 0) gridHistory.CurrentCell = gridHistory.Rows[0].Cells[0];
                lblText2.Text = $"업무 변경 이력 · {gridHistory.Rows.Count}건";
            }
            finally { updating = false; }
            ShowSelectedHistory();
        }

        private void ShowSelectedHistory()
        {
            if (updating || IsDisposed || Disposing) return;
            if (session?.IsCurrent != true) { ClearView("다시 로그인해 주세요."); return; }
            if (gridHistory.CurrentRow?.Tag is not HistoryRow row)
            {
                txtContent.Text = "조회 조건에 맞는 업무 이력이 없습니다.";
                return;
            }
            txtContent.Text = $"{row.Task.TaskId} · {row.Task.Title}\r\n수행자: {row.Event.ActorName}" +
                "   |   변경 시각: " + row.Event.OccurredAt.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss") +
                $"\r\n변경 유형: {KindText(row.Event.Kind)}\r\n변경 내용: {row.Event.Summary}";
        }

        private static string KindText(string kind) => kind switch
        {
            "created" => "생성", "assignee_changed" => "담당자 변경", "status_changed" => "상태 변경",
            "progress_changed" => "진행률 변경", "submitted" => "결과 제출", "returned" => "반려",
            "approved" => "승인", _ => "기타"
        };

        private void ClearView(string message)
        {
            updating = true;
            try
            {
                history.Clear();
                gridHistory.Rows.Clear();
                lblText2.Text = "업무 변경 이력 · 0건";
                txtContent.Text = message;
            }
            finally { updating = false; }
        }

        private void PostClear()
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing) return;
                    ++queryNumber;
                    queryCancellation?.Cancel();
                    ClearView("접속이 변경되었습니다. 다시 로그인해 주세요.");
                    updating = true;
                    cmbOptions.Items.Clear();
                    cmbOptions.Items.Add(new TaskOption(null, "전체 업무"));
                    cmbOptions.SelectedIndex = 0;
                    updating = false;
                }));
            }
            catch (InvalidOperationException) { }
        }
    }
}
