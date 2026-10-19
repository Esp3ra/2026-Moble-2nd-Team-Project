using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Starting;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskAcceptanceForm
{
    private readonly TaskStartWorkflow? startWorkflow;
    private readonly bool recoveryOnly;
    private bool startAttached;

    private void InitializeStartMode()
    {
        if (startWorkflow is null) return;
        lblSourceNotice.Text = "위 제목·담당자명·상태·날짜는 상세 조회 당시 정보입니다. 현재 실행 권한은 서버가 판단합니다.\r\n"
            + "수락·재작업 시작은 한국 시간 시작일~마감일 당일에만 허용됩니다.\r\n"
            + "응답이 불분명하면 같은 요청의 처리 결과를 확인하세요. 결과를 확인하기 전 새 요청을 만들지 않습니다.";
        if (recoveryOnly)
        {
            Text = lblPageTitle.Text = "수락·재작업 처리 결과 확인";
            txtTaskId.Clear(); txtTaskTitle.Clear(); txtAssignee.Clear(); txtTaskStatus.Clear();
            txtStartDate.Clear(); txtDeadline.Clear(); txtWriteContext.Clear();
            lblPreparationStatus.Text = "현재 로그인에서 확인할 이전 요청이 없습니다. 목록을 다시 조회해 주세요.";
            if (startWorkflow.PendingRequest is { } pending)
            {
                txtTaskId.Text = pending.TaskId;
                txtTaskTitle.Text = txtAssignee.Text = txtTaskStatus.Text = "이전 요청 · 처리 결과 확인 필요";
                txtStartDate.Text = txtDeadline.Text = "최신 업무 목록에서 확인해 주세요.";
            }
            ShowStartWorkflow();
        }
    }

    private void AttachStartWorkflow()
    {
        if (startWorkflow is null || startAttached) return;
        startAttached = true;
        startWorkflow.Changed += StartWorkflowChanged;
        if (preparationService is null)
            sessionRegistration = startWorkflow.SessionCancellation.Register(() => StartWorkflowChanged(this, EventArgs.Empty));
    }

    private void DetachStartWorkflow()
    {
        if (startWorkflow is not null && startAttached)
            startWorkflow.Changed -= StartWorkflowChanged;
        startAttached = false;
    }

    private void StartWorkflowChanged(object? sender, EventArgs e)
    {
        if (stopped || IsDisposed || !IsHandleCreated) return;
        try
        {
            if (InvokeRequired) BeginInvoke((Action)ShowStartWorkflow);
            else ShowStartWorkflow();
        }
        catch (InvalidOperationException) { }
    }

    private void ShowStartWorkflow()
    {
        if (startWorkflow is null || stopped || IsDisposed) return;
        if (!HasCurrentSession) { InvalidateSession(); return; }
        var state = startWorkflow.Snapshot;
        if (state.State == TaskStartState.Editing)
        {
            if (state.Failure is not null)
            {
                readyTarget = checkedTarget = null;
                ShouldRefreshList = true;
                lblPreparationStatus.Text = state.Message + "\r\n최신 배정·상태를 다시 조회하고 대상을 확인해 주세요.";
            }
        }
        else
        {
            readyTarget = checkedTarget = null;
            lblPreparationStatus.Text = state.Message;
            if (state.PendingRequest is { } request)
                txtWriteContext.Text = $"요청 업무 {request.TaskId} · {(request.Kind == Preparation.TaskStartKind.Rework ? "재작업 시작" : "업무 수락")}\r\n"
                    + $"전송한 업무 버전 {request.ExpectedRowVersion} · 배정 차수 {request.ExpectedAssignmentRevision}";
        }
        if (state.Result is { } result && state.State == TaskStartState.Succeeded)
        {
            ShouldRefreshList = true;
            txtWriteContext.Text = $"업무 {result.TaskId} · 진행중\r\n"
                + $"업무 버전 {result.RowVersion} · 배정 차수 {result.AssignmentRevision}\r\n"
                + $"서버 변경 시각 {result.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}";
            lblPreparationStatus.Text = "서버에서 수락·재작업 시작 성공을 확인했습니다. 창을 닫으면 목록·상세를 다시 조회합니다.";
        }
        SetStartButtons();
    }

    private void SetStartButtons()
    {
        if (startWorkflow is null || stopped) return;
        var state = startWorkflow.Snapshot;
        bool current = HasCurrentSession;
        bool idle = !busy && !state.IsBusy;
        btnReloadContext.Text = state.CanCheckResult ? "처리 결과 확인" : "최신 정보 다시 조회";
        btnReloadContext.Enabled = current && idle && (state.CanCheckResult
            || state.State == TaskStartState.Editing && source is not null);
        btnConfirmTarget.Text = state.CanRetry ? "같은 요청 재전송"
            : checkedTarget is null ? "대상 확인"
            : kind == Preparation.TaskStartKind.Rework ? "재작업 시작" : "업무 수락";
        btnConfirmTarget.Enabled = current && idle && (state.CanRetry
            || state.State == TaskStartState.Editing && readyTarget is not null && !recoveryOnly);
    }

    private async Task ConfirmStartAsync()
    {
        if (startWorkflow is null || stopped || busy || !HasCurrentSession || startWorkflow.IsBusy) return;
        if (startWorkflow.CanRetry)
        {
            await RunStartOperationAsync(() => startWorkflow.RetrySameRequestAsync());
            return;
        }
        if (startWorkflow.State != TaskStartState.Editing || readyTarget is null || recoveryOnly) return;
        if (checkedTarget is null)
        {
            checkedTarget = readyTarget;
            lblPreparationStatus.Text = "대상을 확인했습니다. 별도 실행 버튼을 눌러 서버에 요청하세요. 아직 상태를 변경하지 않았습니다.";
            SetStartButtons();
            return;
        }
        var request = TaskStartRequest.FromCheckedTarget(checkedTarget);
        await RunStartOperationAsync(() => startWorkflow.SubmitAsync(request));
    }

    private async Task RunStartOperationAsync(Func<Task> operation)
    {
        if (startWorkflow is null || stopped || IsDisposed || !HasCurrentSession) return;
        try { await operation(); }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!stopped && !IsDisposed)
                lblPreparationStatus.Text = "처리 결과를 확인해 주세요. 결과가 불분명한 요청을 새로 실행하지 마세요.";
        }
        finally { if (!stopped && !IsDisposed) ShowStartWorkflow(); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (startWorkflow?.IsBusy == true && HasCurrentSession
            && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            lblPreparationStatus.Text = "요청 처리 중입니다. 응답 또는 처리 결과 확인 안내를 기다려 주세요.";
        }
        base.OnFormClosing(e);
    }
}
