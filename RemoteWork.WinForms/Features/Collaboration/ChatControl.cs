using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class ChatControl : UserControl
    {
        public ChatControl()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            cmbOptions.SelectedIndex = 0;
            gridRooms.Rows.Add(new object[] { "그룹", "기획팀 업무 공유", "4명" });
            gridRooms.Rows.Add(new object[] { "개인", "김민수", "2명" });
            gridRooms.Rows.Add(new object[] { "그룹", "개발팀 협업", "3명" });
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

    }
}
