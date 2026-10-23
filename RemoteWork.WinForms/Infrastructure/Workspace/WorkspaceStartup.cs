using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

namespace RemoteWork.ConnectedWorkspace;

/// <summary>The normal EXE and optional compatibility launcher share this entry point.
/// Saved settings select connections; they never enable server policy or create a device.</summary>
public static class WorkspaceStartup
{
    public const string SettingsRequired = "지문·근무 연결 설정 필요 · 저장된 서버·장치 연결 설정을 확인해 주세요. 일반 업무는 계속 사용할 수 있습니다.";

    public static MainForm CreateMainForm(LoginResult accepted,
        Func<Uri, WorkspaceProfile>? loadProfile = null,
        Func<MainForm, WorkspaceProfile, Task<WorkspaceConnectionReport>>? connect = null)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        var api = AppServices.Api;
        if (!api.IsLoginCurrent(accepted)) throw new OperationCanceledException();
        var session = api.CaptureSession();
        if (session.User.Id != accepted.User.Id) throw new OperationCanceledException();
        var main = new MainForm(accepted.User);
        main.SetWorkspaceConnectionNotice("지문·근무 연결 설정 확인 중…");
        loadProfile ??= server => WorkspaceProfile.Load(server);
        connect ??= (owner, profile) => WorkspaceConnector.ConnectAsync(owner, profile);

        bool started = false;
        main.Shown += (_, _) =>
        {
            if (started) return;
            started = true;
            main.PendingWorkspaceConnection = ConnectAfterShownAsync();
        };
        return main;

        bool Current() => !main.IsDisposed && !main.Disposing && session.IsCurrent && api.IsLoginCurrent(accepted);

        async Task ConnectAfterShownAsync()
        {
            try
            {
                if (!Current()) return;
                var profile = loadProfile(api.ServerUri);
                // Loading a profile must not attach a different login or server.
                if (!Current()) return;
                if (profile.Server != api.ServerUri) throw new InvalidDataException();
                var report = await connect(main, profile);
                if (!Current()) return;
                main.SetWorkspaceConnectionNotice(report.Notices.Length == 0 ? "" : string.Join("\n", report.Notices));
            }
            catch (OperationCanceledException) when (!Current()) { }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException or ApiException or HttpRequestException or OperationCanceledException)
            {
                if (Current()) main.SetWorkspaceConnectionNotice(SettingsRequired);
            }
        }
    }
}
