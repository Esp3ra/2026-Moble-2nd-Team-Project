using System.Globalization;
using System.Text;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TaskEditForm : Form
    {
        private readonly bool editing;
        private readonly TaskDetail? sourceDetail;
        private bool initializing = true;
        private bool datesSupported = true;
        private TaskBasicInputDraft? checkedInput;

        // A local snapshot of the last successful input check; it does not authorize a write.
        public TaskBasicInputDraft? CheckedInput
        {
            get
            {
                // Programmatic checkbox changes may not raise ValueChanged.
                if (checkedInput is not null
                    && (!datesSupported || (editing && sourceDetail is null)
                        || !dtpStartDate.Checked || !dtpDeadline.Checked
                        || checkedInput.TaskId != sourceDetail?.Summary.TaskId
                        || checkedInput.Title != txtTaskTitle.Text
                        || checkedInput.Description != txtTaskDescription.Text
                        || checkedInput.StartDate != DateOnly.FromDateTime(dtpStartDate.Value)
                        || checkedInput.DueDate != DateOnly.FromDateTime(dtpDeadline.Value)))
                    checkedInput = null;
                return checkedInput;
            }
            private set => checkedInput = value;
        }

        public TaskEditForm() : this(false) { }

        public TaskEditForm(bool editing) : this(editing, null) { }

        public TaskEditForm(TaskDetail detail)
            : this(true, detail ?? throw new ArgumentNullException(nameof(detail))) { }

        private TaskEditForm(bool editing, TaskDetail? detail)
        {
            this.editing = editing;
            sourceDetail = detail;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;

            Text = editing ? "업무 수정 입력 준비" : "업무 등록 입력 준비";
            lblPageTitle.Text = editing ? "업무 수정" : "업무 등록";
            txtTaskTitle.Clear();
            txtTaskDescription.Clear();
            cmbAssignee.Items.Clear();
            cmbAssignee.Items.Add(detail?.Summary.AssigneeName ?? "담당자 조회·배정 규약 확인 필요");
            cmbAssignee.SelectedIndex = 0;
            cmbAssignee.Enabled = false;
            txtAttachments.Text = "첨부파일 연결 규약 확인 필요";
            dtpStartDate.Checked = false;
            dtpDeadline.Checked = false;

            if (detail is not null)
            {
                txtTaskTitle.Text = detail.Summary.Title;
                txtTaskDescription.Text = detail.Description;
                bool startSupported = SetSourceDate(dtpStartDate, detail.Summary.StartDate);
                bool dueSupported = SetSourceDate(dtpDeadline, detail.Summary.DueDate);
                datesSupported = startSupported && dueSupported;
            }

            btnConfirm.Enabled = (!editing || detail is not null) && datesSupported;
            initializing = false;
            ResetPreparationStatus();
        }

        private static bool SetSourceDate(DateTimePicker picker, DateOnly date)
        {
            if (date < DateOnly.FromDateTime(picker.MinDate) || date > DateOnly.FromDateTime(picker.MaxDate))
            {
                // Keep the server date visible rather than replacing it with a picker boundary.
                picker.CustomFormat = "'" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'";
                picker.ShowCheckBox = false;
                picker.Enabled = false;
                picker.Checked = false;
                return false;
            }

            picker.Value = date.ToDateTime(TimeOnly.MinValue);
            picker.Checked = true;
            return true;
        }

        private void InputChanged(object? sender, EventArgs e)
        {
            CheckedInput = null;
            if (initializing) return;
            inputErrors.Clear();
            ResetPreparationStatus();
        }

        private void ResetPreparationStatus()
        {
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = !datesSupported
                ? $"원본 시작일: {sourceDetail!.Summary.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} · 마감일: {sourceDetail.Summary.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\r\n날짜 입력기의 지원 범위 밖입니다. 입력 확인·저장·전송은 하지 않습니다."
                : editing && sourceDetail is null
                    ? "목록에서 업무 상세를 조회한 뒤 수정 대상을 선택해 주세요. 저장·전송되지 않습니다."
                    : (sourceDetail is null ? "" : $"수정 대상: {sourceDetail.Summary.TaskId}\r\n")
                        + "로컬 입력 준비 · 담당자·저장 규약 확인 필요 · 서버로 전송되지 않습니다.";
        }

        private void ConfirmInput_Click(object? sender, EventArgs e)
        {
            CheckedInput = null;
            inputErrors.Clear();
            if (!btnConfirm.Enabled || (editing && sourceDetail is null) || !datesSupported) return;
            Control? firstInvalid = null;
            void Reject(Control control, string message)
            {
                inputErrors.SetError(control, message);
                firstInvalid ??= control;
            }

            if (string.IsNullOrWhiteSpace(txtTaskTitle.Text))
                Reject(txtTaskTitle, "업무 제목을 입력해 주세요.");
            else if (txtTaskTitle.Text.EnumerateRunes().Count() > 200)
                Reject(txtTaskTitle, "업무 제목은 유니코드 문자 200개 이내로 입력해 주세요.");
            if (!dtpStartDate.Checked) Reject(dtpStartDate, "시작일을 선택해 주세요.");
            if (!dtpDeadline.Checked) Reject(dtpDeadline, "마감일을 선택해 주세요.");
            if (dtpStartDate.Checked && dtpDeadline.Checked
                && DateOnly.FromDateTime(dtpDeadline.Value) < DateOnly.FromDateTime(dtpStartDate.Value))
                Reject(dtpDeadline, "마감일은 시작일보다 빠를 수 없습니다.");

            if (firstInvalid is not null)
            {
                lblPreparationStatus.ForeColor = Color.Firebrick;
                lblPreparationStatus.Text = inputErrors.GetError(firstInvalid) + "\r\n저장·전송되지 않았습니다.";
                firstInvalid.Focus();
                return;
            }

            CheckedInput = new TaskBasicInputDraft(
                sourceDetail?.Summary.TaskId,
                txtTaskTitle.Text,
                txtTaskDescription.Text,
                DateOnly.FromDateTime(dtpStartDate.Value),
                DateOnly.FromDateTime(dtpDeadline.Value));
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = "입력 형식 확인 · 저장·전송 안 됨\r\n담당자 조회·배정 및 업무 저장 규약을 연결한 뒤 서버에서 다시 검증해야 합니다.";
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
