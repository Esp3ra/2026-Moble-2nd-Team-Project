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
        private readonly ITaskPreparationService? preparationService;
        private TaskPreparationSelection? preparedSelection;
        private CancellationTokenRegistration preparationSessionRegistration;
        private bool preparationStopped;
        public TaskPreparationSelection? PreparedSelection => preparationService?.IsCurrent == true ? preparedSelection : null;

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

        public TaskEditForm(bool editing) : this(editing, null, null) { }

        public TaskEditForm(bool editing, ITaskPreparationService? preparationService) : this(editing, null, preparationService) { }

        public TaskEditForm(TaskDetail detail)
            : this(true, detail ?? throw new ArgumentNullException(nameof(detail)), null) { }

        public TaskEditForm(TaskDetail detail, ITaskPreparationService? preparationService)
            : this(true, detail ?? throw new ArgumentNullException(nameof(detail)), preparationService) { }

        private TaskEditForm(bool editing, TaskDetail? detail, ITaskPreparationService? preparationService)
        {
            this.editing = editing;
            this.preparationService = preparationService;
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
            btnPrepareCandidates.Visible = preparationService is not null;
            btnPrepareCandidates.Enabled = preparationService?.IsCurrent == true && (!editing || detail is not null);
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
                        + "로컬 입력 준비 · 서버로 저장·전송되지 않습니다."
                        + (PreparedSelection is { } selection
                            ? $"\r\n선택 담당자: {(selection.Assignee is null ? "미선택" : "#" + selection.Assignee.Id)} · 업무 관리자: {string.Join(", ", selection.ManagerUserIds.Select(id => "#" + id))}"
                            : "\r\n담당자·관리자 선택과 저장 조건은 별도 확인이 필요합니다.");
        }

        private void PrepareCandidates_Click(object? sender, EventArgs e)
        {
            if (preparationService?.IsCurrent != true) return;
            using var form = new TaskPreparationForm(preparationService, sourceDetail?.Summary.TaskId);
            if (form.ShowDialog(this) != DialogResult.OK || !preparationService.IsCurrent || form.Selection is null) return;
            preparedSelection = form.Selection;
            CheckedInput = null;
            cmbAssignee.Items.Clear();
            cmbAssignee.Items.Add(preparedSelection.Assignee?.SelectionLabel ?? "담당자 미선택");
            cmbAssignee.SelectedIndex = 0;
            inputErrors.Clear();
            ResetPreparationStatus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (preparationService is null) return;
            preparationSessionRegistration = preparationService.SessionCancellation.Register(() =>
            {
                if (!preparationStopped && IsHandleCreated && !IsDisposed)
                    try { BeginInvoke((Action)InvalidatePreparation); }
                    catch (InvalidOperationException) { }
            });
            if (!preparationService.IsCurrent) InvalidatePreparation();
        }
        private void InvalidatePreparation()
        {
            if (preparationStopped || IsDisposed) return;
            preparedSelection = null;
            CheckedInput = null;
            cmbAssignee.Items.Clear();
            cmbAssignee.Items.Add("로그인 변경 · 선택 정보 다시 확인 필요");
            cmbAssignee.SelectedIndex = 0;
            btnPrepareCandidates.Enabled = false;
            btnConfirm.Enabled = false;
            lblPreparationStatus.Text = "로그인 접속이 변경되어 이전 선택 정보를 지웠습니다. 창을 닫고 업무를 다시 조회해 주세요.";
        }
        private void StopPreparation()
        {
            if (preparationStopped) return;
            preparationStopped = true;
            preparationSessionRegistration.Dispose();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        { StopPreparation(); base.OnFormClosed(e); }

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
