#nullable enable

namespace RemoteWork.WinForms.Features.Tasks;

partial class DeadlineExtensionForm
{
    private System.ComponentModel.IContainer? components;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Name = "DeadlineExtensionForm";
        Text = "마감 연장 요청 창";
        ClientSize = new Size(740, 640);
        ResumeLayout(false);
    }
}
