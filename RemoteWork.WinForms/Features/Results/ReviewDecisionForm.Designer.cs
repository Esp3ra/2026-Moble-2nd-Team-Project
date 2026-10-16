#nullable enable
namespace RemoteWork.WinForms.Features.Results;

partial class ReviewDecisionForm
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
        ClientSize = new Size(900, 590);
        MinimumSize = new Size(800, 540);
        Name = "ReviewDecisionForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "결과 검토 · 화면 미리보기";
        ResumeLayout(false);
    }
}
