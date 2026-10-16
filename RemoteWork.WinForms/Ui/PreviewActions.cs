using System.ComponentModel;

namespace RemoteWork.WinForms.Ui;

/// <summary>UI preview only. Does not authenticate, transfer or store data.</summary>
internal static class PreviewActions
{
    public static bool IsDesignMode(Control control) =>
        LicenseManager.UsageMode == LicenseUsageMode.Designtime || control.Site?.DesignMode == true;

    public static void ShowUnavailable(Control owner) => MessageBox.Show(owner.FindForm(),
        "준비 중인 기능입니다.\n현재는 화면 기본 틀만 제공하며, 데이터를 처리하거나 저장·전송하지 않습니다.",
        "기능 준비 중", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Open(Control owner, Form preview)
    {
        using (preview) preview.ShowDialog(owner.FindForm());
    }
}
