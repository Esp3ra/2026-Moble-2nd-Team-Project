namespace RemoteWork.LeavePreview;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length is 1 or 2 && args[0] == "--check") return PreviewChecks.Run(args.Length == 2 ? args[1] : null);
        Application.Run(new LeavePreviewForm());
        return 0;
    }
}
