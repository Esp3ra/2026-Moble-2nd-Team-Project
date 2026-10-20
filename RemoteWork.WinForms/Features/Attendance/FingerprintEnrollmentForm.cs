using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

/// <summary>Member-5 borrowed registration view. Member-1 opens it for the fixed created-user ID.</summary>
public sealed class FingerprintEnrollmentForm : Form
{
    private readonly EnrollmentTarget target;
    private readonly FingerprintEnrollmentRelay relay;
    private readonly FeatureUiScope scope;
    private readonly CancellationTokenSource viewLifetime;
    private readonly CancellationTokenRegistration lifetimeRegistration;
    private readonly ComboBox devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly Button start = new() { Text = "지문 등록", AutoSize = true };
    private readonly Button stop = new() { Text = "취소 요청", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Text = "등록 가능한 장치를 확인해 주세요.", AutoSize = true, MaximumSize = new Size(530, 0) };
    private EnrollmentAttempt? attempt;
    private CancellationTokenSource? operation;
    private bool busy;
    private bool released;
    public bool RegistrationCompleted { get; private set; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    public FingerprintEnrollmentForm(EnrollmentTarget target, IEnrollmentPiChannel pi)
    {
        this.target = target;
        relay = new(target, pi);
        Text = "새 회원 지문 등록";
        ClientSize = new Size(600, 290);
        StartPosition = FormStartPosition.CenterParent;
        scope = new(target.AdministratorSession, action => BeginInvoke(action), () => !IsDisposed && !Disposing);
        viewLifetime = CancellationTokenSource.CreateLinkedTokenSource(target.Cancellation, target.AdministratorSession.Cancellation);
        lifetimeRegistration = viewLifetime.Token.Register(() =>
        {
            // Never synchronously Invoke from a common identity/cancellation lock.
            try { if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed) Close(); })); }
            catch (InvalidOperationException) { }
        });
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = $"등록 대상 회원 ID: {target.UserId}", AutoSize = true });
        layout.Controls.Add(new Label { Text = "등록 장치", AutoSize = true, Margin = new Padding(3, 18, 3, 3) });
        layout.Controls.Add(devices);
        var actions = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 18, 0, 12) };
        actions.Controls.AddRange([start, stop]);
        layout.Controls.Add(actions);
        layout.Controls.Add(status);
        layout.Controls.Add(new Label { Text = "같은 손가락을 두 번 읽습니다. 서버 계정 연결까지 확인한 뒤 완료로 표시합니다.", AutoSize = true, MaximumSize = new Size(530, 0), Margin = new Padding(3, 15, 3, 3) });
        Controls.Add(layout);
        Shown += async (_, _) => { if (viewLifetime.IsCancellationRequested) Close(); else await LoadDevicesAsync(); };
        start.Click += async (_, _) => { PendingOperation = StartAsync(); await PendingOperation; };
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
                start.Enabled = available.Count > 0;
                status.Text = available.Count > 0 ? "장치를 선택하고 지문 등록을 시작해 주세요." : "등록 가능한 장치가 없습니다. 관리자 권한과 장치 설정을 확인해 주세요.";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is ApiException or InvalidOperationException or InvalidDataException)
        { scope.TryPost(view, () => status.Text = "장치 목록을 확인하지 못했습니다. 서버 연결과 등록 기능 설정을 확인해 주세요."); }
    }

    private async Task StartAsync()
    {
        if (busy || devices.SelectedItem is not EnrollmentDevice device) return;
        FeatureUiScope.Operation view;
        try { view = scope.BeginOperation(); }
        catch (OperationCanceledException) { return; }
        operation?.Dispose();
        operation = CancellationTokenSource.CreateLinkedTokenSource(view.Cancellation, target.Cancellation);
        busy = true;
        start.Enabled = devices.Enabled = false;
        stop.Enabled = true;
        try
        {
            attempt ??= relay.CreateAttempt(device.DeviceId);
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
        catch (Exception error) when (error is ApiException or InvalidOperationException or InvalidDataException or HttpRequestException)
        { scope.TryPost(view, () => status.Text = "연결 확인 필요. 같은 작업의 저장 결과를 다시 확인해 주세요."); }
        finally
        {
            busy = false;
            scope.TryPost(view, () =>
            {
                stop.Enabled = false;
                if (!RegistrationCompleted)
                { start.Text = "같은 작업 다시 확인"; start.Enabled = true; }
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
