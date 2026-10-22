using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

namespace RemoteWork.ConnectedWorkspace;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length != 1 || args[0] is not ("--run" or "--check-config"))
                throw new InvalidDataException("명시 실행(--run) 또는 설정 검사(--check-config)를 선택해 주세요.");
            var profile = WorkspaceProfile.Load(AppServices.Api.ServerUri);
            if (args[0] == "--check-config")
            {
                Console.WriteLine("PASS: local topology/options only; no login/camera/server/device changes.");
                return 0;
            }
            using var login = new LoginForm(api: null, openWorkspace: accepted =>
            {
                var acceptedSession = AppServices.Api.CaptureSession();
                if (acceptedSession.User.Id != accepted.User.Id) throw new OperationCanceledException();
                using var main = new MainForm(accepted.User);
                main.Shown += async (_, _) =>
                {
                    try
                    {
                        if (!acceptedSession.IsCurrent) return;
                        var report = await WorkspaceConnector.ConnectAsync(main, profile);
                        if (!main.IsDisposed && acceptedSession.IsCurrent && report.Notices.Length != 0)
                            MessageBox.Show(main, string.Join("\n", report.Notices), "통합 연결 확인");
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or OperationCanceledException)
                    { if (!main.IsDisposed) MessageBox.Show(main, "로그인·통합 연결 설정을 다시 확인해 주세요.", "통합 연결 확인"); }
                };
                main.ShowDialog();
            });
            Application.Run(login); return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            if (args.Contains("--check-config")) Console.Error.WriteLine("NOT_READY: local workspace configuration needs review.");
            else MessageBox.Show("통합 연결 설정 파일을 확인해 주세요. 기존 일반 프로그램의 설정은 바꾸지 않았습니다.", "통합 실행 준비");
            return 1;
        }
    }
}
