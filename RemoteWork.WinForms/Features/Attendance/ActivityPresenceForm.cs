using System.Diagnostics;
using System.Runtime.InteropServices;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance;

public partial class ActivityPresenceForm : Form
{
    private readonly IActivityMonitor monitor;
    private readonly bool ownsMonitor;
    private ActivityMonitorSubscription? subscription;
    private bool stopping, closing, allowClose;
    private Form? closingOwner;

    public ActivityPresenceForm() : this(new ProcessActivityCamera()) { }
    public ActivityPresenceForm(IActivityCamera camera) : this(camera, new PollingLightMotionSource()) { }
    public ActivityPresenceForm(IActivityCamera camera, ILightMotionSource light)
        : this(camera, light, () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency) { }
    internal ActivityPresenceForm(IActivityCamera camera, ILightMotionSource light, Func<double> nowSeconds)
        : this(new ActivityMonitor(camera, light, nowSeconds, automatic: false), ownsMonitor: true) { }

    /// <summary>A view of an owner-managed monitor. Closing this view only unsubscribes.</summary>
    public ActivityPresenceForm(IActivityMonitor monitor) : this(monitor, ownsMonitor: false) { }

    private ActivityPresenceForm(IActivityMonitor monitor, bool ownsMonitor)
    {
        this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        this.ownsMonitor = ownsMonitor;
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        cmbCamera.SelectedIndex = 0;
        cmbBackend.SelectedIndex = 0;
        SetControls();
    }

    private void ActivityPresenceForm_Shown(object? sender, EventArgs e)
    {
        if (!ownsMonitor)
            subscription = new(monitor, SynchronizationContext.Current!, () => !closing && !IsDisposed, Render);
        refreshTimer.Start();
        Render(monitor.Latest);
    }

    private async void btnStart_Click(object? sender, EventArgs e)
    {
        if (!ownsMonitor || monitor.IsRunning || stopping || closing) return;
        try
        {
            var options = new ActivityCameraOptions(cmbCamera.SelectedIndex, cmbBackend.Text,
                (int)numLeft.Value, (int)numTop.Value, (int)numRight.Value, (int)numBottom.Value);
            var piUri = LightMotionClient.ParseBaseUri(txtPiAddress.Text);
            // Explicit independent check mode; this is not a fabricated server work session.
            await monitor.StartAsync(new ActivityMonitorContext("manual-check", () => !closing), options, piUri);
            Render(monitor.Latest);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { if (!IsDisposed) SetStatus("카메라 확인 필요", error.Message, true); }
        finally { if (!IsDisposed) SetControls(); }
    }

    private async void btnStop_Click(object? sender, EventArgs e)
    {
        if (!ownsMonitor || stopping) return;
        stopping = true;
        SetStatus("카메라 종료 중", "영상 처리를 종료하고 있습니다.", true);
        SetControls();
        try { await monitor.StopAsync(); }
        finally
        {
            stopping = false;
            if (!IsDisposed && !closing) { Render(monitor.Latest); SetControls(); }
        }
    }

    private void SetControls()
    {
        bool editable = ownsMonitor && !monitor.IsRunning && !monitor.IsStopping && !stopping && !closing;
        btnStart.Enabled = editable;
        btnStop.Enabled = ownsMonitor && monitor.IsRunning && !stopping && !closing;
        cameraOptions.Enabled = regionOptions.Enabled = piOptions.Enabled = editable;
    }

    private void refreshTimer_Tick(object? sender, EventArgs e)
    {
        if (stopping || closing) return;
        Render(ownsMonitor ? ((ActivityMonitor)monitor).Refresh() : monitor.Latest);
    }

    private void Render(ActivityMonitorUpdate update)
    {
        if (closing || IsDisposed) return;
        if (update.Phase is ActivityMonitorPhase.Running or ActivityMonitorPhase.Starting && !monitor.IsCurrent(update))
        {
            SetStatus("확인 필요", "현재 로그인 접속의 감지 상태를 기다리고 있습니다.", true);
            return;
        }
        if (update.Frame is { } frame && frame.CurrentAge > 5)
        {
            SetStatus("카메라 연결 지연", "최신 영상을 기다리고 있습니다.", true);
            return;
        }
        try
        {
            if (update.Frame?.Jpeg is { } jpeg)
            {
                using var stream = new MemoryStream(jpeg, writable: false);
                using var decoded = Image.FromStream(stream);
                ReplaceImage(new Bitmap(decoded));
            }
            SetStatus(update.Status, update.Detail, update.Frame is null);
        }
        catch (Exception error) when (error is ArgumentException or ExternalException)
        { SetStatus("카메라 확인 필요", "미리보기 영상을 표시할 수 없습니다. 중지 후 다시 시작해 주세요.", true); }
        SetControls();
    }

    private void SetStatus(string status, string details, bool clear)
    {
        lblStatus.Text = status;
        lblDetails.Text = details;
        if (clear) ReplaceImage(null);
        lblEmpty.Text = status;
        lblEmpty.Visible = preview.Image is null;
    }

    private void ReplaceImage(Image? image)
    {
        var previous = preview.Image;
        preview.Image = image;
        previous?.Dispose();
    }

    private async void ActivityPresenceForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose) return;
        if (!ownsMonitor)
        {
            closing = true;
            refreshTimer.Stop(); subscription?.Dispose();
            return;
        }
        if (e.CloseReason == CloseReason.FormOwnerClosing) closingOwner ??= Owner;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        refreshTimer.Stop();
        SetControls();
        await monitor.StopAsync();
        allowClose = true;
        if (!IsDisposed) BeginInvoke((Action)(() =>
        {
            var owner = closingOwner;
            closingOwner = null;
            if (!IsDisposed) Close();
            if (owner is { IsDisposed: false, Disposing: false }) owner.Close();
        }));
    }

    private void ReleaseCamera()
    {
        subscription?.Dispose();
        if (ownsMonitor) monitor.Dispose();
        ReplaceImage(null);
    }
}
