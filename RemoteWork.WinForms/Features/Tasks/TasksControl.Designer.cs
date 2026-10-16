#nullable enable

namespace RemoteWork.WinForms.Features.Tasks;

partial class TasksControl
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
        Name = "TasksControl";
        Size = new Size(1100, 700);
        ResumeLayout(false);
    }
}
