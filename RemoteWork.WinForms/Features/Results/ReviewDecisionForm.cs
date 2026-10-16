using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class ReviewDecisionForm : Form
    {
        public ReviewDecisionForm() : this(true) { }

        public ReviewDecisionForm(bool approve)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(approve);
        }

        private void InitializeSampleData(bool approve)
        {
            if (!approve)
            {
                Text = "결과 반려";
                lblPageTitle.Text = "결과 반려";
                lblText2.Text = "반려 내용";
                lblDecisionReason.Text = "반려 사유";
                txtDecisionReason.Text = "누락된 운영 지표와 예외 상황 설명을 보완해 주세요.";
                btnDecision.Text = "반려";
            }
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
