using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class CollaborationRequestForm : Form
    {
        private readonly ICollaborationDirectory? directory;
        private readonly ICollaborationService? delivery;
        private CollaborationOperation? pendingSend;
        private CollaborationNotificationCoordinator? notifications;
        private readonly Button inboxButton = new() { Text = "받은·보낸 요청", AutoSize = true };
        private readonly Button resolveSendButton = new() { Text = "전송 결과 확인", AutoSize = true, Visible = false };
        private bool sending, retrySend;
        private string deliveryStatus = "";
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenSource? refresh;
        private CancellationTokenRegistration identityEnded;
        private readonly Label directoryStatus = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
        private readonly Button refreshButton = new() { Text = "목록 새로고침", AutoSize = true };
        private string taskStatus = "업무 목록을 불러오는 중입니다.";
        private string userStatus = "직원 목록을 불러오는 중입니다.";
        private bool taskBusy, userBusy, stopped;
        private int generation;
        private sealed record TaskItem(TaskSummary Task)
        {
            public override string ToString() => $"{Task.Title} (업무 {Task.TaskId})";
        }

        public CollaborationRequestForm() : this(null) { }
        public CollaborationRequestForm(ICollaborationDirectory? source, ICollaborationService? service = null)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            try { directory = source ?? new HttpCollaborationDirectory(AppServices.Api); }
            catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { }
            try { delivery = service ?? (source is null ? new HttpCollaborationService(AppServices.Api.CaptureSession()) : null); }
            catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException) { }
            layoutContent.RowStyles[0].Height = 130;
            layoutContent2.Controls.Add(directoryStatus, 0, 1);
            flowActions.Controls.Add(refreshButton);
            flowActions.Controls.Add(inboxButton);
            flowActions.Controls.Add(resolveSendButton);
            refreshButton.Click += async (_, _) => await ReloadDirectoryAsync();
            resolveSendButton.Click += async (_, _) => await ResolveSendAsync();
            inboxButton.Enabled = delivery is not null;
            inboxButton.Click += (_, _) => { if (delivery?.IsCurrent == true) new CollaborationInboxForm(delivery).Show(this); };
            txtContent.MaxLength = 200; txtCollaborationReason.MaxLength = 4000;
            dtpDate.Value = DateTime.Today;
            cmbOptions.SelectedIndexChanged += (_, _) => SetState();
            cmbOptions2.SelectedIndexChanged += (_, _) => SetState();
            txtContent.TextChanged += (_, _) => SetState();
            txtCollaborationReason.TextChanged += (_, _) => SetState();
            Load += async (_, _) => await ReloadDirectoryAsync();
            Shown += async (_, _) =>
            {
                if (delivery?.IsCurrent != true) return;
                // Local UI proposal, not a team-approved polling policy.
                notifications = new(this, delivery, TimeSpan.FromSeconds(10));
                notifications.StatusChanged += message => { deliveryStatus = message; SetState(); };
                await notifications.StartAsync();
            };
            Disposed += (_, _) => EndView();
            btnSendRequest.Enabled = false;
            SetState();
            if (directory is not null)
                identityEnded = directory.Cancellation.Register(() =>
                {
                    if (stopped || !IsHandleCreated) return;
                    try { BeginInvoke((Action)(() =>
                    {
                        if (!stopped) ClearForIdentityChange();
                    })); }
                    catch (InvalidOperationException) { }
                });
        }

        private bool Current(int request) => !stopped && !IsDisposed &&
            generation == request && directory?.IsCurrent == true;

        public async Task ReloadDirectoryAsync()
        {
            if (stopped || IsDisposed) return;
            if (directory?.IsCurrent != true) { ClearForIdentityChange(); return; }
            int request = ++generation;
            refresh?.Cancel();
            refresh?.Dispose();
            refresh = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, directory.Cancellation);
            var ct = refresh.Token;
            cmbOptions.Items.Clear();
            cmbOptions2.Items.Clear();
            taskBusy = userBusy = true;
            taskStatus = "업무 목록을 불러오는 중입니다.";
            userStatus = "직원 목록을 불러오는 중입니다.";
            SetState();
            // An unavailable directory must not hide a successful task list.
            await Task.WhenAll(LoadTasksAsync(request, ct), LoadUsersAsync(request, ct));
        }

        private async Task LoadTasksAsync(int request, CancellationToken ct)
        {
            try
            {
                var tasks = await directory!.GetOwnTasksAsync(ct);
                if (!Current(request) || ct.IsCancellationRequested) return;
                var available = tasks.Where(t => t.Status == TaskDisplayStatus.InProgress && !t.IsCancelled).ToArray();
                cmbOptions.Items.AddRange(available.Select(t => new TaskItem(t)).ToArray());
                cmbOptions.SelectedIndex = -1;
                taskStatus = available.Length == 0 ? "요청 가능한 본인 진행중 업무가 없습니다."
                    : $"본인 진행중 업무 {available.Length}건 · 요청할 업무를 선택해 주세요.";
            }
            catch (OperationCanceledException) { if (Current(request)) taskStatus = "업무 조회가 취소됐습니다. 다시 조회해 주세요."; }
            catch (Exception ex) when (ex is ApiException or TaskQueryException)
            { if (Current(request)) taskStatus = "업무 조회 실패 · 업무 조회 권한과 서버 연결을 확인해 주세요."; }
            finally { if (Current(request)) { taskBusy = false; SetState(); } }
        }

        private async Task LoadUsersAsync(int request, CancellationToken ct)
        {
            try
            {
                var users = await directory!.GetRecipientsAsync(ct);
                if (!Current(request) || ct.IsCancellationRequested) return;
                var available = users.Where(u => u.UserId > 0 && u.UserId != directory.CurrentUserId)
                    .GroupBy(u => u.UserId).Select(g => g.First()).ToArray();
                cmbOptions2.Items.AddRange(available);
                cmbOptions2.SelectedIndex = -1;
                userStatus = available.Length == 0 ? "요청할 수 있는 다른 활성 사용자가 없습니다."
                    : $"활성 사용자 {available.Length}명 · 요청 대상을 선택해 주세요.";
            }
            catch (OperationCanceledException) { if (Current(request)) userStatus = "직원 조회가 취소됐습니다. 다시 조회해 주세요."; }
            catch (ApiException ex)
            {
                if (Current(request)) userStatus = ex.StatusCode == 404
                    ? "직원 목록 API가 준비되지 않았습니다. 서버 준비 후 새로고침해 주세요. " + ex.Message
                    : "직원 목록 조회 실패: " + ex.Message;
            }
            finally { if (Current(request)) { userBusy = false; SetState(); } }
        }

        private void ClearForIdentityChange()
        {
            notifications?.Dispose();
            foreach (var child in OwnedForms) child.Close();
            pendingSend = null;
            txtContent.Clear(); txtCollaborationReason.Clear();
            deliveryStatus = ""; resolveSendButton.Visible = false;
            cmbOptions.Items.Clear();
            cmbOptions2.Items.Clear();
            taskBusy = userBusy = false;
            taskStatus = "로그인한 접속이 필요합니다. 다시 로그인한 후 협업 요청을 열어 주세요.";
            userStatus = "";
            SetState();
        }

        private void SetState()
        {
            bool current = !stopped && directory?.IsCurrent == true;
            bool editable = !sending && pendingSend is null;
            cmbOptions.Enabled = current && editable && !taskBusy && cmbOptions.Items.Count > 0;
            cmbOptions2.Enabled = current && editable && !userBusy && cmbOptions2.Items.Count > 0;
            refreshButton.Enabled = current && editable && !taskBusy && !userBusy;
            txtContent.ReadOnly = txtCollaborationReason.ReadOnly = !editable;
            dtpDate.Enabled = current && editable;
            btnSendRequest.Enabled = current && editable && delivery?.IsCurrent == true && !taskBusy && !userBusy
                && cmbOptions.SelectedItem is TaskItem && cmbOptions2.SelectedItem is CollaborationRecipient
                && !string.IsNullOrWhiteSpace(txtContent.Text) && !string.IsNullOrWhiteSpace(txtCollaborationReason.Text);
            resolveSendButton.Enabled = !sending && delivery?.IsCurrent == true;
            directoryStatus.Text = taskStatus + Environment.NewLine + userStatus + Environment.NewLine + deliveryStatus;
        }

        private void EndView()
        {
            if (stopped) return;
            stopped = true;
            notifications?.Dispose();
            generation++;
            identityEnded.Dispose();
            lifetime.Cancel();
            refresh?.Cancel();
            refresh?.Dispose();
            lifetime.Dispose();
        }

        private async void Unavailable_Click(object? sender, EventArgs e) => await SendRequestAsync();

        public async Task SendRequestAsync()
        {
            if (sending || pendingSend is not null || delivery?.IsCurrent != true
                || cmbOptions.SelectedItem is not TaskItem task || cmbOptions2.SelectedItem is not CollaborationRecipient person) return;
            sending = true; SetState();
            try
            {
                pendingSend = await delivery.PrepareCreateAsync(task.Task.TaskId, person.UserId,
                    txtContent.Text, txtCollaborationReason.Text, DateOnly.FromDateTime(dtpDate.Value), lifetime.Token);
                if (!delivery.IsCurrent || stopped) return;
                await delivery.ExecuteAsync(pendingSend, lifetime.Token);
                Sent();
            }
            catch (CollaborationOutcomeUnknownException ex) { if (!stopped) { deliveryStatus = ex.Message; resolveSendButton.Visible = true; } }
            catch (OperationCanceledException) { }
            catch (ApiException ex) { if (!stopped) { pendingSend = null; deliveryStatus = "요청 전송 실패: " + ex.Message; } }
            finally { if (!stopped) { sending = false; SetState(); } }
        }

        private void Sent()
        {
            if (stopped || delivery?.IsCurrent != true) return;
            pendingSend = null; retrySend = false; resolveSendButton.Visible = false;
            txtContent.Clear(); txtCollaborationReason.Clear();
            deliveryStatus = "협업 요청이 서버 DB에 저장됐습니다. 보낸 요청에서 상대방의 응답을 확인할 수 있습니다.";
        }

        private async Task ResolveSendAsync()
        {
            if (sending || pendingSend is null || delivery?.IsCurrent != true) return;
            sending = true; SetState();
            try
            {
                if (retrySend) { await delivery.ExecuteAsync(pendingSend, lifetime.Token); Sent(); return; }
                var result = await delivery.ResolveAsync(pendingSend, lifetime.Token);
                if (stopped || !delivery.IsCurrent) return;
                if (result is not null) { Sent(); return; }
                retrySend = true; resolveSendButton.Text = "동일 요청 재전송";
                deliveryStatus = "저장된 결과가 없습니다. 같은 요청을 다시 보내거나 내역을 확인해 주세요.";
            }
            catch (OperationCanceledException) { }
            catch (CollaborationOutcomeUnknownException ex) { if (!stopped) { retrySend = false; resolveSendButton.Text = "전송 결과 확인"; deliveryStatus = ex.Message; } }
            catch (ApiException ex) { if (!stopped) { retrySend = false; resolveSendButton.Text = "전송 결과 확인"; deliveryStatus = ex.Message; } }
            finally { if (!stopped) { sending = false; SetState(); } }
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
