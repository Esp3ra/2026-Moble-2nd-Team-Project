using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public sealed record EnrollmentLaunchResult(bool Completed, bool AttemptStarted);

public interface ICreatedUserEnrollmentLauncher
{
    Task<EnrollmentLaunchResult> ShowAsync(IWin32Window owner, CreatedUserEnrollmentTarget target, Uri server);
}

/// <summary>Owns the borrowed member-5 view/channel and releases transport after pending work.</summary>
public sealed class CreatedUserEnrollmentLauncher : ICreatedUserEnrollmentLauncher
{
    private readonly Func<Uri, EnrollmentEndpoints> load;
    private readonly Func<Uri, IEnrollmentPiChannel> channelFactory;
    public CreatedUserEnrollmentLauncher(Func<Uri, EnrollmentEndpoints>? load = null,
        Func<Uri, IEnrollmentPiChannel>? channelFactory = null)
    {
        this.load = load ?? (server => EnrollmentEndpoints.Load(server));
        this.channelFactory = channelFactory ?? (address => new HttpEnrollmentPiChannel(address.AbsoluteUri));
    }

    public async Task<EnrollmentLaunchResult> ShowAsync(IWin32Window owner, CreatedUserEnrollmentTarget target, Uri server)
    {
        target.ThrowIfChanged();
        var endpoints = load(server);
        target.ThrowIfChanged();
        using var channel = new EnrollmentDeviceChannel(target, endpoints, channelFactory);
        var borrowed = new EnrollmentTarget(target.UserId, target.AdministratorSession, () => target.IsCurrent, target.Cancellation);
        using var view = new FingerprintEnrollmentForm(borrowed, channel, channel.ValidateDevice,
            target.RequiredRecoveryId, channel.ReleaseUnissuedDevice);
        try { view.ShowDialog(owner); }
        finally { await view.PendingOperation; }
        target.ThrowIfChanged();
        return new(view.RegistrationCompleted, view.RegistrationStarted);
    }
}

/// <summary>Routes only the device selected before issue; never falls back to a previous Pi.</summary>
public sealed class EnrollmentDeviceChannel(CreatedUserEnrollmentTarget target, EnrollmentEndpoints endpoints,
    Func<Uri, IEnrollmentPiChannel> create) : IEnrollmentPiChannel, IDisposable
{
    private string? selected;
    private IEnrollmentPiChannel? channel;
    private bool disposed;
    private bool transmissionStarted;

    // Called only for the view's exact pre-issue NO_FREE rejection. Merely
    // choosing another row never releases a prepared/uncertain device binding.
    public bool ReleaseUnissuedDevice(string deviceId)
    {
        if (disposed || !target.IsCurrent || transmissionStarted || selected != deviceId) return false;
        try { (channel as IDisposable)?.Dispose(); }
        catch (Exception) { return false; }
        channel = null;
        selected = null;
        return true;
    }

    public void ValidateDevice(string deviceId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        target.ThrowIfChanged();
        var address = endpoints.Resolve(deviceId);
        if (selected is not null && selected != deviceId) throw new InvalidDataException("진행 중인 등록 장치를 바꿀 수 없습니다.");
        if (channel is null)
        {
            channel = create(address) ?? throw new InvalidDataException("장치 연결을 준비하지 못했습니다.");
            selected = deviceId;
        }
    }

    public async Task<string> TransferAsync(EnrollmentJob job, bool readOnly,
        Func<JsonElement, CancellationToken, Task<string>> approve, IProgress<string>? progress, CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        target.ThrowIfChanged();
        cancellation.ThrowIfCancellationRequested();
        if (channel is null || selected != job.DeviceId) throw new InvalidDataException("등록 전에 선택한 장치 주소를 확인해 주세요.");
        transmissionStarted = true;
        var result = await channel.TransferAsync(job, readOnly, approve, progress, cancellation).ConfigureAwait(false);
        target.ThrowIfChanged();
        cancellation.ThrowIfCancellationRequested();
        return result;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        (channel as IDisposable)?.Dispose();
    }
}
