using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class ImportantNoticeForm : Form
    {
        public ImportantNoticeForm()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
