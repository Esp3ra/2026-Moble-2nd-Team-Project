#nullable enable

namespace RemoteWork.WinForms.Features.Tasks;

partial class TaskEditForm
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
        Name = "TaskEditForm";
        Text = "업무 입력 창";
        ClientSize = new Size(780, 720);
        ResumeLayout(false);
    }
}
