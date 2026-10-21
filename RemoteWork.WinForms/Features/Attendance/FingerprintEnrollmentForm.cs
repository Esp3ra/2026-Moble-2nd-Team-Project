using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

/// <summary>Member-5 borrowed registration view. Member-1 opens it for the fixed created-user ID.</summary>
public sealed class FingerprintEnrollmentForm : Form
{
    private readonly EnrollmentTarget target;
    private readonly FingerprintEnrollmentRelay relay;
    private readonly Action<string>? validateDevice;
    private bool manualReviewRequired;
    private readonly FeatureUiScope scope;
    private readonly CancellationTokenSource viewLifetime;
    private readonly CancellationTokenRegistration lifetimeRegistration;
    private readonly ComboBox devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly Button start = new() { Text = "지문 등록", AutoSize = true };
    private readonly Button stop = new() { Text = "취소 요청", AutoSize = true, Enabled = false };
    private readonly CheckBox recoveryMode = new() { Text = "운영자가 승인한 복구 작업으로 등록", AutoSize = true };
    private readonly TextBox recoveryId = new() { Width = 320, MaxLength = 36, PlaceholderText = "운영자가 전달한 복구 작업 ID" };
    private readonly Button checkRecovery = new() { Text = "복구 작업 확인", AutoSize = true };
    private readonly FlowLayoutPanel recoveryPanel = new() { AutoSize = true, Visible = false, MaximumSize = new Size(550, 0) };
    private EnrollmentRecoverySelection? preparedRecovery;
    private readonly Label status = new() { Text = "등록 가능한 장치를 확인해 주세요.", AutoSize = true, MaximumSize = new Size(530, 0) };
    private EnrollmentAttempt? attempt;
    private CancellationTokenSource? operation;
    private bool busy;
    private bool released;
    public bool RegistrationCompleted { get; private set; }
    public bool RegistrationStarted => attempt is not null;
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    public FingerprintEnrollmentForm(EnrollmentTarget target, IEnrollmentPiChannel pi, Action<string>? validateDevice = null)
    {
        this.target = target;
        this.validateDevice = validateDevice;
        relay = new(target, pi);
        Text = "새 회원 지문 등록";
        ClientSize = new Size(600, 460);
        StartPosition = FormStartPosition.CenterParent;
        scope = new(target.AdministratorSession, action => BeginInvoke(action), () => !IsDisposed && !Disposing);
        viewLifetime = CancellationTokenSource.CreateLinkedTokenSource(target.Cancellation, target.AdministratorSession.Cancellation);
        lifetimeRegistration = viewLifetime.Token.Register(() =>
        {
            // Never synchronously Invoke from a common identity/cancellation lock.
            try { if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed) Close(); })); }
            catch (InvalidOperationException) { }
        });
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        layout.Controls.Add(new Label { Text = $"등록 대상 회원 ID: {target.UserId}", AutoSize = true });
        layout.Controls.Add(new Label { Text = "등록 장치", AutoSize = true, Margin = new Padding(3, 18, 3, 3) });
        layout.Controls.Add(devices);
        layout.Controls.Add(recoveryMode);
        recoveryPanel.Controls.Add(recoveryId);
        recoveryPanel.Controls.Add(checkRecovery);
        layout.Controls.Add(recoveryPanel);
        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 18, 0, 12) };
        actions.Controls.AddRange([start, stop]);
        layout.Controls.Add(actions);
        layout.Controls.Add(status);
        layout.Controls.Add(new Label { Text = "같은 손가락을 두 번 읽습니다. 서버 계정 연결까지 확인한 뒤 완료로 표시합니다.", AutoSize = true, MaximumSize = new Size(530, 0), Margin = new Padding(3, 15, 3, 3) });
        Controls.Add(layout);
        Shown += async (_, _) => { if (viewLifetime.IsCancellationRequested) Close(); else await LoadDevicesAsync(); };
        start.Click += async (_, _) => { PendingOperation = StartAsync(); await PendingOperation; };
        checkRecovery.Click += async (_, _) => { PendingOperation = CheckRecoveryAsync(); await PendingOperation; };
        recoveryMode.CheckedChanged += (_, _) =>
        {
            if (busy || attempt is not null) return;
            preparedRecovery = null;
            recoveryPanel.Visible = recoveryMode.Checked;
            recoveryId.ReadOnly = false;
            start.Enabled = !recoveryMode.Checked && devices.SelectedItem is EnrollmentDevice;
            start.Text = "지문 등록";
            status.Text = recoveryMode.Checked ? "운영자의 종료 승인 후 복구 작업 ID를 확인해 주세요. 확인만으로 지문 등록을 시작하지 않습니다."
                : "장치를 선택하고 지문 등록을 시작해 주세요.";
        };
        devices.SelectedIndexChanged += (_, _) =>
        {
            if (!recoveryMode.Checked || busy || attempt is not null) return;
            preparedRecovery = null;
            start.Enabled = false;
            recoveryId.ReadOnly = false;
        };
        stop.Click += async (_, _) => await CancelAsync();
        FormClosed += (_, _) => ReleaseLifetime();
    }

    private void ReleaseLifetime()
    {
        if (released) return;
        released = true;
        operation?.Cancel(); operation?.Dispose(); scope.Dispose();
        lifetimeRegistration.Dispose(); viewLifetime.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ReleaseLifetime();
        base.Dispose(disposing);
    }

    private async Task LoadDevicesAsync()
    {
        start.Enabled = false;
        FeatureUiScope.Operation view;
        try { view = scope.BeginOperation(); }
        catch (OperationCanceledException) { return; }
        try
        {
            var available = await relay.GetDevicesAsync(view.Cancellation);
            scope.TryPost(view, () =>
            {
                devices.DisplayMember = nameof(EnrollmentDevice.DisplayName);
                devices.DataSource = available.ToList();
                start.Enabled = available.Count > 0 && !recoveryMode.Checked;
                status.Text = available.Count > 0 ? "장치를 선택하고 지문 등록을 시작해 주세요." : "등록 가능한 장치가 없습니다. 관리자 권한과 장치 설정을 확인해 주세요.";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is ApiException or InvalidOperationException or InvalidDataException)
        { scope.TryPost(view, () => status.Text = "장치 목록을 확인하지 못했습니다. 서버 연결과 등록 기능 설정을 확인해 주세요."); }
    }

    private async Task CheckRecoveryAsync()
    {
        if (busy || attempt is not null || !recoveryMode.Checked || devices.SelectedItem is not EnrollmentDevice device) return;
        FeatureUiScope.Operation view;
        try { view = scope.BeginOperation(); }
        catch (OperationCanceledException) { return; }
        busy = true;
        preparedRecovery = null;
        start.Enabled = devices.Enabled = recoveryMode.Enabled = checkRecovery.Enabled = false;
        recoveryId.ReadOnly = true;
        try
        {
            var selection = await relay.GetRecoveryAsync(recoveryId.Text.Trim(), view.Cancellation);
            if (selection.Recovery.DeviceId != device.DeviceId)
                throw new InvalidDataException("선택한 장치와 복구 작업이 다릅니다.");
            scope.TryPost(view, () =>
            {
                preparedRecovery = selection;
                start.Text = "복구 승인으로 지문 등록";
                status.Text = $"복구 작업 확인 완료. 아래 버튼을 누르면 현재 회원 ID {target.UserId}의 새 등록을 요청하고 같은 손가락을 두 번 읽습니다.";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is ApiException or InvalidOperationException or InvalidDataException or HttpRequestException)
        {
            scope.TryPost(view, () => status.Text = "복구 작업을 사용할 수 없습니다. 운영자 승인·현재 관리자 로그인·선택 장치를 확인해 주세요. 일반 등록으로 자동 전환하지 않습니다.");
        }
        finally
        {
            busy = false;
            scope.TryPost(view, () =>
            {
                devices.Enabled = recoveryMode.Enabled = checkRecovery.Enabled = true;
                recoveryId.ReadOnly = preparedRecovery is not null;
                start.Enabled = preparedRecovery is not null;
            });
        }
    }

    private async Task StartAsync()
    {
        if (busy || devices.SelectedItem is not EnrollmentDevice device || recoveryMode.Checked && preparedRecovery is null) return;
        FeatureUiScope.Operation view;
        try { view = scope.BeginOperation(); }
        catch (OperationCanceledException) { return; }
        operation?.Dispose();
        operation = CancellationTokenSource.CreateLinkedTokenSource(view.Cancellation, target.Cancellation);
        busy = true;
        start.Enabled = devices.Enabled = false;
        recoveryMode.Enabled = checkRecovery.Enabled = false;
        recoveryId.ReadOnly = true;
        stop.Enabled = true;
        try
        {
            // The owner resolves the selected device before any issue POST/claim.
            validateDevice?.Invoke(device.DeviceId);
            attempt ??= recoveryMode.Checked ? relay.CreateRecoveryAttempt(preparedRecovery!, device) : relay.CreateAttempt(device.DeviceId);
            var progress = new Progress<string>(text => scope.TryPost(view, () => status.Text = text));
            var result = await relay.RunAsync(attempt, progress, operation.Token);
            scope.TryPost(view, () =>
            {
                RegistrationCompleted = result.State == "completed";
                status.Text = RegistrationCompleted ? "지문 등록 완료" : "연결 확인 필요";
                start.Enabled = false;
            });
        }
        catch (OperationCanceledException)
        { scope.TryPost(view, () => status.Text = "등록을 중단했습니다. 저장 여부는 확인이 필요할 수 있습니다."); }
        catch (EnrollmentPiFailureException error) when (error.AlreadyRegistered)
        {
            manualReviewRequired = true;
            scope.TryPost(view, () => status.Text = error.Message);
        }
        catch (Exception error) when (error is ApiException or InvalidOperationException or InvalidDataException or HttpRequestException)
        { scope.TryPost(view, () => status.Text = attempt is null
            ? "등록을 시작하지 못했습니다. 선택한 장치의 주소 설정을 확인해 주세요."
            : "연결 확인 필요. 같은 작업의 저장 결과를 다시 확인해 주세요."); }
        finally
        {
            busy = false;
            scope.TryPost(view, () =>
            {
                stop.Enabled = false;
                if (!RegistrationCompleted)
                {
                    devices.Enabled = attempt is null;
                    recoveryMode.Enabled = checkRecovery.Enabled = attempt is null;
                    recoveryId.ReadOnly = attempt is not null || preparedRecovery is not null;
                    start.Text = manualReviewRequired ? "현재 작업 확인 필요" : attempt is not null ? "같은 작업 다시 확인"
                        : recoveryMode.Checked && preparedRecovery is not null ? "복구 승인으로 지문 등록" : "지문 등록";
                    start.Enabled = !manualReviewRequired;
                }
            });
        }
    }

    private async Task CancelAsync()
    {
        FeatureUiScope.Operation view;
        try { view = scope.BeginOperation(); }
        catch (OperationCanceledException) { return; }
        operation?.Cancel();
        stop.Enabled = false;
        status.Text = "취소 요청 중입니다. 지문 삭제를 뜻하지 않습니다.";
        if (attempt is null) return;
        try
        {
            await relay.CancelAsync(attempt);
            scope.TryPost(view, () => { status.Text = "서버에 취소를 요청했습니다. 저장 여부가 불확실하면 확인이 필요합니다."; start.Enabled = false; });
        }
        catch (Exception error) when (error is ApiException or OperationCanceledException or InvalidOperationException or InvalidDataException)
        { scope.TryPost(view, () => status.Text = "취소 결과 확인 필요. 새 등록을 시작하지 말고 기존 작업을 확인해 주세요."); }
    }
}
