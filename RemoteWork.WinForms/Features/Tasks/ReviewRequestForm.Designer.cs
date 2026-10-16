#nullable enable

namespace RemoteWork.WinForms.Features.Tasks;

partial class ReviewRequestForm
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
        Name = "ReviewRequestForm";
        Text = "검토 요청 창";
        ClientSize = new Size(740, 640);
        ResumeLayout(false);
    }
}
