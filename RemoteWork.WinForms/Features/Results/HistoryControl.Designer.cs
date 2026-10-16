#nullable enable
namespace RemoteWork.WinForms.Features.Results;

partial class HistoryControl
{
    private System.ComponentModel.IContainer? components;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Name = "HistoryControl";
        Size = new Size(1140, 720);
        ResumeLayout(false);
    }
}
