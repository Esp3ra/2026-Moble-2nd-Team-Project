#nullable enable
namespace RemoteWork.WinForms.Features.Results;

partial class SubmissionForm
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
        ClientSize = new Size(960, 630);
        MinimumSize = new Size(850, 600);
        Name = "SubmissionForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "업무 결과 제출 · 화면 미리보기";
        ResumeLayout(false);
    }
}
