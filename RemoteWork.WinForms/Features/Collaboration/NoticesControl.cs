using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

public partial class NoticesControl : UserControl
{
    private readonly PreviewRole _previewRole;

    public NoticesControl() : this(PreviewRole.Employee) { }

    public NoticesControl(PreviewRole role)
    {
        _previewRole = role;
        InitializeComponent();
    }
}
