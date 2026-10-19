using System.Diagnostics;
using System.Runtime.InteropServices;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance;

public partial class ActivityPresenceForm : Form
{
    private readonly IActivityCamera camera;
    private ActivityCameraFrame? shownFrame;
    private long waitingSince;
    private bool active;
    private bool stopping;
    private bool closing;
    private bool allowClose;

    public ActivityPresenceForm() : this(new ProcessActivityCamera()) { }

    public ActivityPresenceForm(IActivityCamera camera)
    {
        this.camera = camera;
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        cmbCamera.SelectedIndex = 0;
        cmbBackend.SelectedIndex = 0;
    }

    private void ActivityPresenceForm_Shown(object? sender, EventArgs e) => refreshTimer.Start();

    private void btnStart_Click(object? sender, EventArgs e)
    {
        if (active || stopping || closing) return;
        try
        {
            var options = new ActivityCameraOptions(cmbCamera.SelectedIndex, cmbBackend.Text,
                (int)numLeft.Value, (int)numTop.Value, (int)numRight.Value, (int)numBottom.Value);
            options.Validate();
            camera.Start(options);
            active = true;
            waitingSince = Stopwatch.GetTimestamp();
            shownFrame = null;
            SetStatus("확인 대기", "카메라를 준비하고 있습니다.", true);
            SetControls();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            SetStatus("카메라 확인 필요", error.Message, true);
        }
    }

    private async void btnStop_Click(object? sender, EventArgs e) => await StopCameraAsync();

    private async Task StopCameraAsync()
    {
        if (stopping) return;
        active = false;
        stopping = true;
        shownFrame = null;
        SetStatus("카메라 종료 중", "영상 처리를 종료하고 있습니다.", true);
        SetControls();
        try { await camera.StopAsync(); }
        finally
        {
            stopping = false;
            if (!IsDisposed)
            {
                SetStatus("카메라 꺼짐", "카메라 시작을 누르면 확인합니다.", true);
                SetControls();
            }
        }
    }

    private void SetControls()
    {
        bool editable = !active && !stopping && !closing;
        btnStart.Enabled = editable;
        btnStop.Enabled = active && !stopping;
        cameraOptions.Enabled = editable;
        regionOptions.Enabled = editable;
    }

    private void refreshTimer_Tick(object? sender, EventArgs e)
    {
        if (!active || stopping || closing) return;
        var frame = camera.TakeLatest();
        if (frame is not null)
        {
            shownFrame = frame;
            if (frame.State == "CAMERA_ERROR")
            {
                active = false;
                SetStatus(frame.Error?.Contains("영상이 끊겼") == true ? "카메라 연결 끊김" : "카메라 확인 필요",
                    frame.Error ?? "카메라 연결을 확인해 주세요.", true);
                _ = FinishErrorAsync();
                return;
            }
            if (frame.State == "STALE" || frame.CurrentAge > 5)
            {
                SetStatus("카메라 연결 지연", "최신 영상을 기다리고 있습니다.", true);
                return;
            }
            try
            {
                if (frame.Jpeg is null) throw new InvalidDataException("미리보기 영상이 없습니다.");
                using var stream = new MemoryStream(frame.Jpeg, writable: false);
                using var decoded = Image.FromStream(stream);
                ReplaceImage(new Bitmap(decoded));
                bool detected = frame.State == "HAND_DETECTED";
                SetStatus(detected ? "활동 중" : "자리 비움", detected ? "화면에서 손이 확인됩니다." :
                    $"손이 확인되지 않은 시간: {frame.SecondsWithoutHand ?? 0:F1}초", false);
            }
            catch (Exception error) when (error is ArgumentException or ExternalException or InvalidDataException)
            {
                SetStatus("카메라 확인 필요", "미리보기 영상을 표시할 수 없습니다. 중지 후 다시 시작해 주세요.", true);
            }
        }
        else if (shownFrame is not null ? shownFrame.CurrentAge > 5 : Stopwatch.GetElapsedTime(waitingSince).TotalSeconds > 5)
            SetStatus("카메라 연결 지연", "최신 영상을 기다리고 있습니다.", true);
    }

    private async Task FinishErrorAsync()
    {
        stopping = true;
        SetControls();
        try { await camera.StopAsync(); }
        finally { stopping = false; if (!IsDisposed) SetControls(); }
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
        e.Cancel = true;
        if (closing) return;
        closing = true;
        refreshTimer.Stop();
        SetControls();
        await camera.StopAsync();
        allowClose = true;
        // A synchronously completed Stop must not re-enter a modal FormClosing event.
        if (!IsDisposed) BeginInvoke((Action)Close);
    }

    private void ReleaseCamera()
    {
        active = false;
        camera.Dispose();
        ReplaceImage(null);
    }
}
