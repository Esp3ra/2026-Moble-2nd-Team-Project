using System.Globalization;
using System.Text;
using RemoteWork.WinForms.Features.Tasks.Creation;
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
        private readonly TaskCreationWorkflow? creationWorkflow;
        private readonly CancellationTokenSource creationLifetime = new();
        private TaskCreationRequest? checkedCreationRequest;
        private bool creationOperation;
        public TaskCreationResult? CreatedResult { get; private set; }
        public TaskPreparationSelection? PreparedSelection => preparationService?.IsCurrent == true
            && (creationWorkflow is null || creationWorkflow.IsCurrent) ? preparedSelection : null;

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

        public TaskEditForm(bool editing) : this(editing, null, null, null) { }

        public TaskEditForm(bool editing, ITaskPreparationService? preparationService) : this(editing, null, preparationService, null) { }

        public TaskEditForm(bool editing, ITaskPreparationService? preparationService, TaskCreationWorkflow? creationWorkflow)
            : this(editing, null, preparationService, creationWorkflow) { }

        public TaskEditForm(ITaskPreparationService? preparationService, TaskCreationWorkflow creationWorkflow)
            : this(false, null, preparationService, creationWorkflow) { }

        public TaskEditForm(TaskDetail detail)
            : this(true, detail ?? throw new ArgumentNullException(nameof(detail)), null, null) { }

        public TaskEditForm(TaskDetail detail, ITaskPreparationService? preparationService)
            : this(true, detail ?? throw new ArgumentNullException(nameof(detail)), preparationService, null) { }

        private TaskEditForm(bool editing, TaskDetail? detail, ITaskPreparationService? preparationService, TaskCreationWorkflow? creationWorkflow)
        {
            this.editing = editing;
            this.preparationService = preparationService;
            this.creationWorkflow = editing ? null : creationWorkflow;
            sourceDetail = detail;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;

            Text = editing ? "업무 수정 입력 준비" : this.creationWorkflow is null ? "업무 등록 입력 준비" : "업무 등록";
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
            btnCreateTask.Visible = btnCheckCreationResult.Visible = btnRetryCreation.Visible = this.creationWorkflow is not null;
            btnCancel.Visible = this.creationWorkflow is null;
            if (this.creationWorkflow is not null)
            {
                RestoreCreationDraft();
                this.creationWorkflow.StateChanged += CreationStateChanged;
            }
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
            checkedCreationRequest = null;
            if (initializing) return;
            inputErrors.Clear();
            ResetPreparationStatus();
        }

        private void ResetPreparationStatus()
        {
            DateOnly? originalStart = sourceDetail?.Summary.StartDate ?? creationWorkflow?.PendingRequest?.StartDate;
            DateOnly? originalDue = sourceDetail?.Summary.DueDate ?? creationWorkflow?.PendingRequest?.DueDate;
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = !datesSupported
                ? $"원본 시작일: {originalStart:yyyy-MM-dd} · 마감일: {originalDue:yyyy-MM-dd}\r\n날짜 입력기의 지원 범위 밖입니다. 입력 확인·새 등록은 하지 않습니다."
                : editing && sourceDetail is null
                    ? "목록에서 업무 상세를 조회한 뒤 수정 대상을 선택해 주세요. 저장·전송되지 않습니다."
                    : (sourceDetail is null ? "" : $"수정 대상: {sourceDetail.Summary.TaskId}\r\n")
                        + (creationWorkflow is null
                            ? "로컬 입력 준비 · 서버로 저장·전송되지 않습니다."
                            : "신규 등록 서버 연결 · 입력 확인 후 ‘등록’을 눌러야 저장됩니다.")
                        + (PreparedSelection is { } selection
                            ? $"\r\n선택 담당자: {(selection.Assignee is null ? "미선택" : "#" + selection.Assignee.Id)} · 업무 관리자: {string.Join(", ", selection.ManagerUserIds.Select(id => "#" + id))}"
                            : creationWorkflow is null
                                ? "\r\n담당자·관리자 선택과 저장 조건은 별도 확인이 필요합니다."
                                : "\r\n담당자와 업무 관리자를 선택해 주세요. 등록자는 필수 관리자입니다.");
            UpdateCreationButtons();
        }

        private void PrepareCandidates_Click(object? sender, EventArgs e)
        {
            if (preparationService?.IsCurrent != true || !CanEditCreation()) return;
            using var form = new TaskPreparationForm(preparationService, sourceDetail?.Summary.TaskId);
            if (form.ShowDialog(this) != DialogResult.OK || !preparationService.IsCurrent || form.Selection is null) return;
            preparedSelection = form.Selection;
            CheckedInput = null;
            checkedCreationRequest = null;
            cmbAssignee.Items.Clear();
            cmbAssignee.Items.Add(preparedSelection.Assignee?.SelectionLabel ?? "담당자 미선택");
            cmbAssignee.SelectedIndex = 0;
            inputErrors.Clear();
            ResetPreparationStatus();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (preparationService is not null)
            {
                preparationSessionRegistration = preparationService.SessionCancellation.Register(() =>
                {
                    if (!preparationStopped && IsHandleCreated && !IsDisposed)
                        try { BeginInvoke((Action)InvalidatePreparation); }
                        catch (InvalidOperationException) { }
                });
                if (!preparationService.IsCurrent) InvalidatePreparation();
            }
            ShowCreationState();
        }
        private void InvalidatePreparation()
        {
            if (preparationStopped || IsDisposed) return;
            preparedSelection = null;
            CheckedInput = null;
            checkedCreationRequest = null;
            cmbAssignee.Items.Clear();
            cmbAssignee.Items.Add("로그인 변경 · 선택 정보 다시 확인 필요");
            cmbAssignee.SelectedIndex = 0;
            btnPrepareCandidates.Enabled = false;
            btnConfirm.Enabled = false;
            lblPreparationStatus.Text = creationWorkflow?.State == TaskCreationState.Disconnected
                ? creationWorkflow.Message
                : "로그인 접속이 변경되어 이전 선택 정보를 지웠습니다. 창을 닫고 업무를 다시 조회해 주세요.";
            UpdateCreationButtons();
        }
        private void StopPreparation()
        {
            if (preparationStopped) return;
            preparationStopped = true;
            preparationSessionRegistration.Dispose();
            if (creationWorkflow is not null) creationWorkflow.StateChanged -= CreationStateChanged;
            creationLifetime.Cancel();
            creationLifetime.Dispose();
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        { StopPreparation(); base.OnFormClosed(e); }

        private void ConfirmInput_Click(object? sender, EventArgs e)
        {
            CheckedInput = null;
            checkedCreationRequest = null;
            inputErrors.Clear();
            UpdateCreationButtons();
            if (!btnConfirm.Enabled || !CanEditCreation() || (editing && sourceDetail is null) || !datesSupported) return;
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

            var input = new TaskBasicInputDraft(
                sourceDetail?.Summary.TaskId,
                txtTaskTitle.Text,
                txtTaskDescription.Text,
                DateOnly.FromDateTime(dtpStartDate.Value),
                DateOnly.FromDateTime(dtpDeadline.Value));
            CheckedInput = input;
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            if (creationWorkflow is not null)
            {
                if (HasCreationSelection())
                {
                    TaskPreparationSelection selection = PreparedSelection!;
                    try
                    {
                        checkedCreationRequest = new TaskCreationRequest(input.Title, input.Description,
                            input.StartDate, input.DueDate, selection.Assignee!.Id, selection.ManagerUserIds);
                    }
                    catch (ArgumentException error)
                    {
                        CheckedInput = null;
                        checkedCreationRequest = null;
                        inputErrors.SetError(txtTaskDescription, error.Message);
                        lblPreparationStatus.ForeColor = Color.Firebrick;
                        lblPreparationStatus.Text = error.Message + "\r\n저장·전송되지 않았습니다.";
                        UpdateCreationButtons();
                        return;
                    }
                }
                lblPreparationStatus.Text = checkedCreationRequest is null
                    ? "입력 형식 확인 · 아직 저장되지 않았습니다.\r\n담당자와 등록자를 포함한 업무 관리자를 선택한 뒤 입력을 다시 확인해 주세요."
                    : "입력·선택 확인 완료 · 아직 저장되지 않았습니다.\r\n‘등록’을 누르면 신규 업무를 서버에 저장합니다. 첨부파일은 이 등록에 포함되지 않습니다.";
            }
            else
                lblPreparationStatus.Text = "입력 형식 확인 · 저장·전송 안 됨\r\n담당자 조회·배정 및 업무 저장 규약을 연결한 뒤 서버에서 다시 검증해야 합니다.";
            UpdateCreationButtons();
        }

        private bool CanEditCreation() => creationWorkflow is null
            || (!creationOperation && !creationWorkflow.IsBusy && creationWorkflow.IsCurrent
                && creationWorkflow.CanCreate && creationWorkflow.State == TaskCreationState.Editing);

        private bool HasCreationSelection()
        {
            if (creationWorkflow is null || preparationService?.IsCurrent != true
                || !preparationService.CanSelectCandidates || preparationService.UserId != creationWorkflow.UserId
                || PreparedSelection is not { Context: null, Assignee: { Id: > 0 } } selection)
                return false;
            return selection.ManagerUserIds.Count >= 1 && selection.ManagerUserIds.Contains(creationWorkflow.UserId)
                && selection.ManagerUserIds.All(id => id > 0)
                && selection.ManagerUserIds.Distinct().Count() == selection.ManagerUserIds.Count;
        }

        private bool HasCheckedCreationRequest()
        {
            if (checkedCreationRequest is not { } request || CheckedInput is not { } input || !HasCreationSelection())
                return false;
            return request.Title == input.Title && request.Description == input.Description
                && request.StartDate == input.StartDate && request.DueDate == input.DueDate
                && request.AssigneeId == PreparedSelection!.Assignee!.Id
                && request.ManagerUserIds.SequenceEqual(PreparedSelection.ManagerUserIds);
        }

        private void RestoreCreationDraft()
        {
            if (creationWorkflow?.PendingRequest is not { } request) return;
            txtTaskTitle.Text = request.Title;
            txtTaskDescription.Text = request.Description;
            datesSupported = SetSourceDate(dtpStartDate, request.StartDate) & SetSourceDate(dtpDeadline, request.DueDate);
            preparedSelection = new TaskPreparationSelection(null,
                new TaskUserCandidate(request.AssigneeId, "이전 선택 담당자", "이전 선택 담당자 · #" + request.AssigneeId),
                Array.AsReadOnly(request.ManagerUserIds.ToArray()));
            cmbAssignee.Items.Clear();
            cmbAssignee.Items.Add(preparedSelection.Assignee!.SelectionLabel);
            cmbAssignee.SelectedIndex = 0;
        }

        private void UpdateCreationButtons()
        {
            if (creationWorkflow is null) return;
            bool current = !preparationStopped && creationWorkflow.IsCurrent && preparationService?.IsCurrent == true;
            bool editable = current && CanEditCreation();
            txtTaskTitle.Enabled = txtTaskDescription.Enabled = editable;
            dtpStartDate.Enabled = dtpDeadline.Enabled = editable && datesSupported;
            btnPrepareCandidates.Enabled = editable && preparationService!.CanSelectCandidates;
            btnConfirm.Enabled = editable && datesSupported;
            btnCreateTask.Enabled = editable && HasCheckedCreationRequest();
            btnCheckCreationResult.Enabled = current && !creationOperation && !creationWorkflow.IsBusy && creationWorkflow.CanCheckResult;
            btnRetryCreation.Enabled = current && !creationOperation && !creationWorkflow.IsBusy && creationWorkflow.CanRetry;
            btnSelectAttachment.Enabled = !creationOperation && !creationWorkflow.IsBusy;
            btnClose.Enabled = !current || (!creationOperation && !creationWorkflow.IsBusy);
        }

        private void CreationStateChanged(object? sender, EventArgs e)
        {
            if (preparationStopped || IsDisposed || !IsHandleCreated) return;
            try
            {
                if (InvokeRequired) BeginInvoke((Action)ShowCreationState);
                else ShowCreationState();
            }
            catch (InvalidOperationException) { /* The owned window is closing. */ }
        }

        private void ShowCreationState()
        {
            if (creationWorkflow is null || preparationStopped || IsDisposed) return;
            if (!creationWorkflow.IsCurrent || creationWorkflow.State == TaskCreationState.Disconnected)
            {
                InvalidatePreparation();
                lblPreparationStatus.Text = creationWorkflow.Message;
                return;
            }
            UpdateCreationButtons();
            if (creationWorkflow.State == TaskCreationState.Succeeded && creationWorkflow.CreatedResult is { } result)
            {
                CreatedResult = result;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            if (creationWorkflow.State != TaskCreationState.Editing || creationWorkflow.Failure is not null)
            {
                lblPreparationStatus.ForeColor = creationWorkflow.Failure is null ? Color.FromArgb(30, 42, 61) : Color.Firebrick;
                lblPreparationStatus.Text = creationWorkflow.Message;
            }
        }

        private async Task RunCreationActionAsync(Func<CancellationToken, Task> action)
        {
            if (creationWorkflow is null || creationOperation || creationWorkflow.IsBusy || preparationStopped) return;
            creationOperation = true;
            UpdateCreationButtons();
            try
            {
                await action(creationLifetime.Token);
                if (!preparationStopped && !IsDisposed) ShowCreationState();
            }
            catch (OperationCanceledException) when (preparationStopped || IsDisposed) { }
            catch (Exception error)
            {
                if (!preparationStopped && !IsDisposed)
                {
                    lblPreparationStatus.ForeColor = Color.Firebrick;
                    lblPreparationStatus.Text = error is ArgumentException ? error.Message : "등록 처리 상태를 확인해 주세요. 결과를 모르는 요청은 새로 등록하지 마세요.";
                }
            }
            finally
            {
                creationOperation = false;
                if (!preparationStopped && !IsDisposed) UpdateCreationButtons();
            }
        }

        private async void CreateTask_Click(object? sender, EventArgs e)
        {
            if (!CanEditCreation() || !HasCheckedCreationRequest() || creationWorkflow is null) return;
            TaskCreationRequest request = checkedCreationRequest!;
            await RunCreationActionAsync(ct => creationWorkflow.SubmitAsync(request, ct));
        }

        private async void CheckCreationResult_Click(object? sender, EventArgs e)
        {
            if (!btnCheckCreationResult.Enabled || creationWorkflow is null) return;
            await RunCreationActionAsync(creationWorkflow.CheckResultAsync);
        }

        private async void RetryCreation_Click(object? sender, EventArgs e)
        {
            if (!btnRetryCreation.Enabled || creationWorkflow is null) return;
            await RunCreationActionAsync(creationWorkflow.RetryAsync);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (CreatedResult is null && creationWorkflow?.IsCurrent == true
                && (creationOperation || creationWorkflow.IsBusy) && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                lblPreparationStatus.Text = "등록 처리 중입니다. 완료되거나 처리 결과 확인 상태가 될 때까지 기다려 주세요.";
            }
            base.OnFormClosing(e);
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
