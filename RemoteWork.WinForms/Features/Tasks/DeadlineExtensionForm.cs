using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class DeadlineExtensionForm : Form
    {
        private readonly TaskDetail? sourceDetail;
        private bool initializing = true;

        public DeadlineExtensionForm() : this(PreviewRole.Employee) { }

        public DeadlineExtensionForm(PreviewRole role) : this(role, null, true) { }

        public DeadlineExtensionForm(PreviewRole role, TaskDetail detail)
            : this(role, detail ?? throw new ArgumentNullException(nameof(detail)), true) { }

        private DeadlineExtensionForm(PreviewRole role, TaskDetail? detail, bool preparing)
        {
            sourceDetail = detail;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            Text = "마감 연장 입력 준비";
            txtTaskSummary.Text = detail is null ? "대상 업무 선택 필요"
                : $"{detail.Summary.TaskId} · {detail.Summary.Title}";
            txtCurrentDeadline.Text = detail?.Summary.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";
            txtExtensionReason.Clear();
            dtpRequestedDeadline.Checked = false;
            dtpRequestedDeadline.Enabled = detail is not null;
            txtExtensionReason.Enabled = detail is not null;
            btnConfirm.Enabled = detail is not null;
            initializing = false;
            ResetPreparationStatus();
            UpdateDateNotice();
        }

        private void InputChanged(object? sender, EventArgs e)
        {
            if (initializing) return;
            inputErrors.Clear();
            ResetPreparationStatus();
            UpdateDateNotice();
        }

        private void ResetPreparationStatus()
        {
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = sourceDetail is null
                ? "목록에서 업무 상세를 조회한 뒤 연장 대상을 선택해 주세요. 요청이 접수되지 않습니다."
                : "로컬 입력 준비 · 신청 조건·마감 규약 확인 필요 · 요청 접수·마감 변경은 하지 않습니다.";
        }

        private void UpdateDateNotice()
        {
            txtExtensionNotice.Text = "현재 마감일: " + txtCurrentDeadline.Text
                + "\r\n희망 마감일: " + (dtpRequestedDeadline.Checked
                    ? DateOnly.FromDateTime(dtpRequestedDeadline.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "선택 필요");
        }

        private void ConfirmInput_Click(object? sender, EventArgs e)
        {
            inputErrors.Clear();
            if (sourceDetail is null) return;
            Control? firstInvalid = null;
            if (!dtpRequestedDeadline.Checked)
            {
                inputErrors.SetError(dtpRequestedDeadline, "희망 마감일을 선택해 주세요.");
                firstInvalid = dtpRequestedDeadline;
            }
            if (string.IsNullOrWhiteSpace(txtExtensionReason.Text))
            {
                inputErrors.SetError(txtExtensionReason, "연장 사유를 입력해 주세요.");
                firstInvalid ??= txtExtensionReason;
            }
            if (firstInvalid is not null)
            {
                lblPreparationStatus.ForeColor = Color.Firebrick;
                lblPreparationStatus.Text = inputErrors.GetError(firstInvalid) + "\r\n요청 접수·마감 변경은 하지 않았습니다.";
                firstInvalid.Focus();
                return;
            }

            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = "입력 확인 · 접수·마감 변경 안 됨\r\n날짜·업무 상태별 신청 자격은 합의된 서버 규약으로 다시 검증해야 합니다.";
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
