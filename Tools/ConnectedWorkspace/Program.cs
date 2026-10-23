using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

namespace RemoteWork.ConnectedWorkspace;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        return Run(args, server => WorkspaceProfile.Load(server));
    }

    internal static int Run(string[] args, Func<Uri, WorkspaceProfile> loadProfile)
    {
        try
        {
            if (args.Length != 1 || args[0] is not ("--run" or "--check-config"))
                throw new InvalidDataException("명시 실행(--run) 또는 설정 검사(--check-config)를 선택해 주세요.");
            if (args[0] == "--check-config")
            {
                _ = loadProfile(AppServices.Api.ServerUri);
                Console.WriteLine("PASS: local topology/options only; no login/camera/server/device changes.");
                return 0;
            }
            using var login = new LoginForm(api: null, openWorkspace: accepted =>
            {
                using var main = WorkspaceStartup.CreateMainForm(accepted, loadProfile);
                main.ShowDialog();
            });
            Application.Run(login); return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            if (args.Contains("--check-config")) Console.Error.WriteLine("NOT_READY: local workspace configuration needs review.");
            else MessageBox.Show("통합 연결 설정 파일을 확인해 주세요. 기존 일반 프로그램의 설정은 바꾸지 않았습니다.", "통합 실행 준비");
            return 1;
        }
    }
}
