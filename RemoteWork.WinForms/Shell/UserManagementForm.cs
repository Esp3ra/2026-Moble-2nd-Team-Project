using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public sealed class UserManagementForm : Form
{
    private readonly ApiClient.Session? session;
    private readonly FeatureUiScope? requests;
    private readonly ICreatedUserEnrollmentLauncher enrollmentLauncher;
    private readonly Uri server;
    private bool enrolling, enrollmentAttempted;
    private CancellationTokenRegistration sessionCancellation;
    private CreatedUserEnrollmentTarget? enrollmentTarget;
    private UserInfo? selectedUser;
    private readonly Func<UserInfo, string, bool> confirmRecovery;
    private readonly Func<UserInfo, bool> confirmExistingEnrollment;
    private long selectionGeneration;
    private int ended;
    private readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false,
        AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false };
    private readonly TextBox login = new(), name = new(), number = new(), department = new();
    private readonly TextBox password = new() { UseSystemPasswordChar = true, MaxLength = 128 };
    private readonly ComboBox role = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox active = new() { Text = "활성 계정", Checked = true, AutoSize = true };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(850, 0) };
    private readonly Button save = new() { Text = "계정 생성", AutoSize = true };
    private readonly Button reset = new() { Text = "비밀번호 초기화", AutoSize = true };
    private readonly Button previous = new() { Text = "이전", AutoSize = true };
    private readonly Button next = new() { Text = "다음", AutoSize = true };
    private readonly Label pageLabel = new() { AutoSize = true, Padding = new Padding(4, 8, 4, 0) };
    private int page = 1, selectedId;
    private bool loading, writing;
    private int total;
    private readonly Button create = new() { Text = "새 계정 입력", AutoSize = true };
    private readonly Button reload = new() { Text = "새로고침", AutoSize = true };
    private readonly Button enroll = new() { Text = "생성한 회원 지문 등록", AutoSize = true, Enabled = false };
    private readonly Button enrollExisting = new() { Text = "선택한 회원 지문 신규 등록", AutoSize = true, Enabled = false };
    private readonly TextBox recoveryId = new() { Width = 265, MaxLength = 36, PlaceholderText = "운영자가 전달한 복구 번호" };
    private readonly Button recover = new() { Text = "선택한 회원 복구 등록", AutoSize = true, Enabled = false };
    private readonly Label recoveryTarget = new() { AutoSize = true, MaximumSize = new Size(270, 0), Text = "복구 등록할 기존 회원을 목록에서 선택하세요." };

    /// <summary>The explicit enrollment button borrows this target. Account save never starts a scan.</summary>
    public CreatedUserEnrollmentTarget? EnrollmentTarget => enrollmentTarget?.IsCurrent == true ? enrollmentTarget : null;
    public event EventHandler? EnrollmentTargetChanged;

    public UserManagementForm() : this(AppServices.Api) { }

    public UserManagementForm(ApiClient api) : this(api, new CreatedUserEnrollmentLauncher()) { }

    public UserManagementForm(ApiClient api, ICreatedUserEnrollmentLauncher enrollmentLauncher,
        Func<UserInfo, string, bool>? confirmRecovery = null, Func<UserInfo, bool>? confirmExistingEnrollment = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(enrollmentLauncher);
        this.enrollmentLauncher = enrollmentLauncher;
        this.confirmExistingEnrollment = confirmExistingEnrollment ?? (user => MessageBox.Show(this,
            $"회원 ID: {user.Id}\n아이디: {user.LoginId}\n이름: {user.DisplayName}\n사번: {user.EmployeeNo}\n\n이 회원의 새 지문 등록창을 열까요?\n계정 정보는 변경하지 않습니다. 이미 등록된 지문은 자동으로 교체하지 않습니다.",
            "기존 회원 지문 신규 등록", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        this.confirmRecovery = confirmRecovery ?? ((user, id) => MessageBox.Show(this,
            $"회원 ID: {user.Id}\n아이디: {user.LoginId}\n이름: {user.DisplayName}\n사번: {user.EmployeeNo}\n\n이 회원의 복구 등록창을 열까요?\n일반 등록은 열리지 않으며 복구 조회와 별도의 시작이 필요합니다.",
            "복구 대상 회원 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        server = api.ServerUri;
        try { session = api.CaptureSession(); }
        catch (InvalidOperationException) { /* A disconnected view never starts requests. */ }
        if (session is not null)
            requests = new FeatureUiScope(session, action => BeginInvoke(action), () => IsUsable);
        Text = "사용자 관리"; ClientSize = new Size(1060, 760); MinimumSize = new Size(960, 650);
        StartPosition = FormStartPosition.CenterParent; Font = new Font("맑은 고딕", 9F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        foreach (var (key, title) in new[] { ("EmployeeNo", "사번"), ("LoginId", "아이디"), ("DisplayName", "이름"), ("Department", "부서"), ("Role", "권한"), ("IsActive", "활성") })
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = key, HeaderText = title, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        layout.Controls.Add(grid, 0, 0);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(16, 0, 0, 0), AutoScroll = true };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        role.Items.AddRange(new object[] { "직원", "관리자" }); role.SelectedIndex = 0;
        login.MaxLength = 50; name.MaxLength = department.MaxLength = 80; number.MaxLength = 40;
        foreach (var (caption, input) in new (string, Control)[] { ("아이디 (생성 후 변경 불가)", login), ("이름", name), ("사번", number), ("부서", department), ("권한", role), ("비밀번호 (10~128자)", password) })
        {
            var row = editor.RowCount++; editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            editor.Controls.Add(new Label { Text = caption, AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 0, row);
            row = editor.RowCount++; editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            input.Dock = DockStyle.Fill; editor.Controls.Add(input, 0, row);
        }
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
        actions.Controls.AddRange(new Control[] { active, create, save, reset, enroll, enrollExisting, recoveryTarget, recoveryId, recover });
        var actionRow = editor.RowCount++; editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 300)); editor.Controls.Add(actions, 0, actionRow);
        layout.Controls.Add(editor, 1, 0); layout.SetRowSpan(editor, 2);
        var paging = new FlowLayoutPanel { Dock = DockStyle.Fill };
        paging.Controls.AddRange(new Control[] { previous, pageLabel, next, reload }); layout.Controls.Add(paging, 0, 1);
        layout.Controls.Add(status, 0, 2); layout.SetColumnSpan(status, 2); Controls.Add(layout);
        // SelectionChanged may run while CurrentRow still points at the previous row.
        grid.CurrentCellChanged += (_, _) => { if (!loading && grid.CurrentRow?.DataBoundItem is UserInfo user) Fill(user); };
        grid.CellFormatting += (_, e) =>
        {
            var property = grid.Columns[e.ColumnIndex].DataPropertyName;
            if (property == "Role" && e.Value is string value) e.Value = value == "admin" ? "관리자" : "직원";
            if (property == "IsActive" && e.Value is bool enabled) e.Value = enabled ? "활성" : "비활성";
        };
        Shown += async (_, _) => await LoadUsers();
        reload.Click += async (_, _) => await LoadUsers();
        previous.Click += async (_, _) => { if (page > 1) { page--; await LoadUsers(); } };
        next.Click += async (_, _) => { page++; await LoadUsers(); };
        create.Click += (_, _) => ClearEditor();
        save.Click += async (_, _) => await Save();
        reset.Click += async (_, _) => await ResetPassword();
        enroll.Click += async (_, _) => await EnrollCreatedUser();
        enrollExisting.Click += async (_, _) => await EnrollExistingUser();
        recover.Click += async (_, _) => await RecoverSelectedUser();
        ClearEditor();
        FormClosing += (_, _) => EndLifetime();
        Disposed += (_, _) => EndLifetime();
        if (session is not null)
            sessionCancellation = session.Cancellation.Register(() =>
            {
                Interlocked.Exchange(ref ended, 1);
                // Never synchronously Invoke a UI while ApiClient is expiring.
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)ClearEndedView); }
                catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
            });
    }

    private bool IsUsable => Volatile.Read(ref ended) == 0 && !IsDisposed && !Disposing && session?.IsCurrent == true;

    private void InvalidateTarget()
    {
        Interlocked.Increment(ref selectionGeneration);
        var previousTarget = enrollmentTarget;
        enrollmentTarget = null;
        enrollmentAttempted = false;
        previousTarget?.Invalidate();
        if (previousTarget is not null) EnrollmentTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearEndedView()
    {
        if (IsDisposed) return;
        InvalidateTarget();
        grid.DataSource = null;
        ClearEditor();
        status.Text = "관리자 접속이 변경되었습니다. 창을 닫고 다시 열어 주세요.";
        SetControls();
    }

    private void EndLifetime()
    {
        Interlocked.Exchange(ref ended, 1);
        sessionCancellation.Unregister();
        InvalidateTarget();
        requests?.Dispose();
    }

    private void SetControls()
    {
        if (IsDisposed) return;
        bool enabled = IsUsable && !loading && !writing && !enrolling;
        grid.Enabled = create.Enabled = reload.Enabled = save.Enabled = enabled;
        foreach (var control in new Control[] { name, number, department, role, password }) control.Enabled = enabled;
        login.Enabled = enabled && selectedId == 0;
        active.Enabled = enabled && selectedId != 0;
        reset.Enabled = enabled && selectedId != 0 && selectedId != session?.User.Id;
        previous.Enabled = enabled && page > 1;
        next.Enabled = enabled && page * 50 < total;
        enroll.Enabled = enabled && !enrollmentAttempted && EnrollmentTarget is { RequiredRecoveryId: null, IsExistingMember: false } && session?.User.Role == "admin";
        enrollExisting.Enabled = enabled && !enrollmentAttempted && selectedUser is { IsActive: true }
            && selectedId == selectedUser.Id && session?.User.Role == "admin";
        recover.Enabled = enabled && !enrollmentAttempted && selectedUser is { IsActive: true }
            && selectedId == selectedUser.Id && session?.User.Role == "admin";
        recoveryId.ReadOnly = !enabled || enrollmentAttempted;
    }

    private void ClearEditor()
    {
        InvalidateTarget();
        selectedUser = null;
        recoveryTarget.Text = "복구 등록할 기존 회원을 목록에서 선택하세요.";
        selectedId = 0; login.Enabled = true; login.Clear(); name.Clear(); number.Clear(); department.Clear(); password.Clear();
        role.SelectedIndex = 0; active.Checked = true; active.Enabled = false; save.Text = "계정 생성"; reset.Enabled = false;
        SetControls();
    }

    private void Fill(UserInfo user, bool invalidate = true)
    {
        if (invalidate) InvalidateTarget();
        selectedUser = user;
        recoveryTarget.Text = $"복구 대상 ID {user.Id} · {user.DisplayName} · {user.EmployeeNo}";
        selectedId = user.Id; login.Text = user.LoginId; login.Enabled = false; name.Text = user.DisplayName;
        number.Text = user.EmployeeNo; department.Text = user.Department; role.SelectedIndex = user.Role == "admin" ? 1 : 0;
        active.Checked = user.IsActive; active.Enabled = true; password.Clear(); save.Text = "사용자 정보 저장";
        SetControls();
    }

    private async Task LoadUsers(UserInfo? preservedUser = null, string? successMessage = null)
    {
        if (loading || !IsUsable) return;
        loading = true; SetControls();
        FeatureUiScope.Operation? operation = null;
        long generation = Interlocked.Read(ref selectionGeneration);
        int requestedPage = page;
        try
        {
            operation = requests!.BeginOperation();
            var result = await session!.GetAsync<PageResult<UserInfo>>($"users?page={requestedPage}&limit=50", operation.Cancellation);
            if (!IsUsable || !operation.IsCurrent || generation != Interlocked.Read(ref selectionGeneration)) return;
            grid.DataSource = result.Items;
            total = result.Total;
            pageLabel.Text = $"{page}쪽 · 총 {result.Total}명";
            if (preservedUser is not null)
            {
                // A created member may be outside this page. Never substitute row 1.
                var visible = result.Items.FirstOrDefault(user => user.Id == preservedUser.Id);
                grid.ClearSelection(); grid.CurrentCell = null;
                Fill(visible ?? preservedUser, invalidate: visible is not null && visible != preservedUser);
                if (visible is not null)
                    foreach (DataGridViewRow row in grid.Rows)
                        if (row.DataBoundItem is UserInfo user && user.Id == visible.Id)
                        { grid.CurrentCell = row.Cells[0]; row.Selected = true; break; }
            }
            else if (result.Items.Count > 0) Fill(result.Items[0]); else ClearEditor();
            status.Text = successMessage ?? "계정을 비활성화하거나 권한을 변경하면 해당 사용자의 기존 접속이 종료됩니다.";
        }
        catch (OperationCanceledException) { /* The owning login session has ended. */ }
        catch (ApiException ex)
        { if (IsUsable && operation?.IsCurrent == true && generation == Interlocked.Read(ref selectionGeneration)) status.Text = (successMessage is null ? "" : successMessage + " 목록 갱신 실패: ") + ex.Message; }
        finally { loading = false; SetControls(); }
    }

    private async Task Save()
    {
        if (!IsUsable || loading || writing) return;
        writing = true; SetControls();
        int targetId = selectedId;
        long generation = Interlocked.Read(ref selectionGeneration);
        FeatureUiScope.Operation? operation = null;
        try
        {
            operation = requests!.BeginOperation();
            UserResult result = targetId == 0
                ? await session!.PostAsync<UserResult>("users", new { login_id = login.Text.Trim(), display_name = name.Text.Trim(),
                    employee_no = number.Text.Trim(), department = department.Text.Trim(), role = role.SelectedIndex == 1 ? "admin" : "employee", password = password.Text }, operation.Cancellation)
                : await session!.PatchAsync<UserResult>($"users/{targetId}", new { display_name = name.Text.Trim(), employee_no = number.Text.Trim(),
                    department = department.Text.Trim(), role = role.SelectedIndex == 1 ? "admin" : "employee", is_active = active.Checked }, operation.Cancellation);
            if (!IsUsable || !operation.IsCurrent || generation != Interlocked.Read(ref selectionGeneration)) return;
            if (result.User is null || result.User.Id <= 0 || (targetId != 0 && result.User.Id != targetId))
                throw new ApiException("서버가 반환한 회원 ID를 확인할 수 없습니다. 목록에서 생성/저장 여부를 확인해 주세요.");
            password.Clear(); Fill(result.User);
            long savedGeneration = Interlocked.Read(ref selectionGeneration);
            if (targetId == 0 && result.User.IsActive)
            {
                long createdGeneration = Interlocked.Read(ref selectionGeneration);
                enrollmentTarget = new(result.User, session!, () => Volatile.Read(ref ended) == 0
                    && Interlocked.Read(ref selectionGeneration) == createdGeneration);
                EnrollmentTargetChanged?.Invoke(this, EventArgs.Empty);
            }
            if (!IsUsable || savedGeneration != Interlocked.Read(ref selectionGeneration)) return;
            string message = targetId == 0
                ? "계정 생성 완료. 생성한 회원 지문 등록 버튼으로 별도 등록을 시작할 수 있습니다."
                : "사용자 정보 저장 완료.";
            status.Text = message;
            await LoadUsers(result.User, message);
        }
        catch (OperationCanceledException) { /* The owning login session has ended. */ }
        catch (ApiException ex)
        { if (IsUsable && operation?.IsCurrent == true && generation == Interlocked.Read(ref selectionGeneration)) status.Text = ex.Message + (targetId == 0 && ex.StatusCode == 0 ? " 생성 여부를 목록에서 확인한 뒤 다시 시도해 주세요." : ""); }
        finally { writing = false; SetControls(); }
    }

    private async Task EnrollCreatedUser()
    {
        var target = EnrollmentTarget;
        if (!IsUsable || loading || writing || enrolling || enrollmentAttempted || target is null || session?.User.Role != "admin") return;
        enrolling = true; SetControls();
        try
        {
            var result = await enrollmentLauncher.ShowAsync(this, target, server);
            if (!IsUsable || !ReferenceEquals(enrollmentTarget, target) || !target.IsCurrent) return;
            enrollmentAttempted = result.AttemptStarted;
            status.Text = result.Completed ? "지문 등록 완료. 서버에서 생성한 회원과 장치 연결을 확인했습니다."
                : result.AttemptStarted ? "계정은 유지됩니다. 등록은 완료되지 않았습니다. 기존 작업 확인이 필요하므로 새 등록을 시작하지 않습니다."
                : "계정은 유지됩니다. 지문 등록을 시작하지 않았습니다.";
        }
        catch (OperationCanceledException) { /* Ended target/session cannot publish a late result. */ }
        catch (InvalidDataException)
        { if (IsUsable && target.IsCurrent) status.Text = "지문 등록 주소 설정을 확인해 주세요. 현재 서버와 선택한 장치의 Pi 주소가 필요합니다."; }
        catch (Exception error) when (error is ApiException or InvalidOperationException or HttpRequestException)
        {
            if (IsUsable && target.IsCurrent)
            {
                enrollmentAttempted = true;
                status.Text = "등록 결과 확인이 필요합니다. 계정은 유지되며 새 등록은 자동으로 시작하지 않습니다.";
            }
        }
        finally { enrolling = false; SetControls(); }
    }

    private Task RecoverSelectedUser() => EnrollSelectedUser(recovery: true);

    private Task EnrollExistingUser() => EnrollSelectedUser(recovery: false);

    private async Task EnrollSelectedUser(bool recovery)
    {
        var selected = selectedUser;
        if (!IsUsable || loading || writing || enrolling || enrollmentAttempted
            || selected is not { IsActive: true } || selectedId != selected.Id || session?.User.Role != "admin") return;
        string id = recovery ? recoveryId.Text.Trim() : "";
        if (recovery && (!Guid.TryParseExact(id, "D", out var parsed) || parsed.ToString("D") != id))
        { status.Text = "운영자가 전달한 정규 복구 번호를 입력해 주세요."; return; }
        int targetId = selected.Id, requestedPage = page;
        long generation = Interlocked.Read(ref selectionGeneration);
        FeatureUiScope.Operation? operation = null;
        CreatedUserEnrollmentTarget? target = null;
        bool launched = false;
        enrolling = true; SetControls();
        try
        {
            operation = requests!.BeginOperation();
            // Reuse the existing administrator directory; no new API or write.
            var result = await session.GetAsync<PageResult<UserInfo>>($"users?page={requestedPage}&limit=50", operation.Cancellation);
            if (!IsUsable || !operation.IsCurrent || selectedId != targetId
                || generation != Interlocked.Read(ref selectionGeneration)) return;
            var matches = result.Items?.Where(user => user.Id == targetId).ToArray();
            if (result.Page != requestedPage || result.Limit != 50 || matches?.Length != 1
                || !matches[0].IsActive || matches[0].Role is not ("employee" or "admin")
                || matches[0].LoginId != selected.LoginId)
                throw new ApiException("현재 선택한 활성 회원을 다시 확인하지 못했습니다. 목록을 새로 조회해 주세요.");
            UserInfo verified = matches[0];
            if (!(recovery ? confirmRecovery(verified, id) : confirmExistingEnrollment(verified)))
            { status.Text = "지문 등록창을 열지 않았습니다. 계정은 유지됩니다."; return; }
            if (!IsUsable || !operation.IsCurrent || selectedId != targetId
                || generation != Interlocked.Read(ref selectionGeneration)) return;
            InvalidateTarget();
            long recoveryGeneration = Interlocked.Read(ref selectionGeneration);
            Func<bool> isCurrent = () => Volatile.Read(ref ended) == 0 && selectedId == targetId
                && Interlocked.Read(ref selectionGeneration) == recoveryGeneration;
            target = recovery ? CreatedUserEnrollmentTarget.ForExplicitRecovery(verified, session, isCurrent, id)
                : CreatedUserEnrollmentTarget.ForExistingMember(verified, session, isCurrent);
            enrollmentTarget = target;
            EnrollmentTargetChanged?.Invoke(this, EventArgs.Empty);
            launched = true;
            var answer = await enrollmentLauncher.ShowAsync(this, target, server);
            if (!IsUsable || !ReferenceEquals(enrollmentTarget, target) || !target.IsCurrent) return;
            enrollmentAttempted = answer.AttemptStarted;
            status.Text = answer.Completed ? "지문 등록 완료. 선택한 기존 회원과 서버 연결을 확인했습니다."
                : answer.AttemptStarted ? "계정은 유지됩니다. 지문 등록 결과 확인이 필요하므로 새 시도를 시작하지 않습니다."
                : recovery ? "계정은 유지됩니다. 복구 번호 조회와 명시 시작 전에는 등록을 실행하지 않습니다."
                : "계정은 유지됩니다. 등록창에서 별도로 시작하기 전에는 새 지문을 등록하지 않습니다.";
        }
        catch (OperationCanceledException) { }
        catch (InvalidDataException)
        { if (IsUsable && (target is null ? generation == Interlocked.Read(ref selectionGeneration) : target.IsCurrent))
                status.Text = "지문 등록 주소 설정을 확인해 주세요. 현재 서버와 선택한 장치의 주소가 필요합니다."; }
        catch (Exception error) when (error is ApiException or InvalidOperationException or HttpRequestException)
        {
            if (IsUsable && (target is null ? generation == Interlocked.Read(ref selectionGeneration) : target.IsCurrent))
            {
                enrollmentAttempted = launched;
                status.Text = launched ? "지문 등록 결과 확인이 필요합니다. 기존 회원은 유지되며 새 등록은 자동 실행하지 않습니다."
                    : "등록 대상 회원 확인 실패: " + error.Message;
            }
        }
        finally { enrolling = false; SetControls(); }
    }

    private async Task ResetPassword()
    {
        if (!IsUsable || loading || writing || selectedId == 0 || selectedId == session?.User.Id) return;
        if (password.Text.Length < 10) { status.Text = "초기화할 새 비밀번호를 10~128자로 입력해 주세요."; return; }
        int targetId = selectedId;
        long generation = Interlocked.Read(ref selectionGeneration);
        if (MessageBox.Show(this, "선택한 사용자의 비밀번호를 변경하고 모든 접속을 종료할까요?", "비밀번호 초기화", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        if (!IsUsable || selectedId != targetId || generation != Interlocked.Read(ref selectionGeneration)) return;
        FeatureUiScope.Operation? operation = null;
        writing = true; SetControls();
        try
        {
            operation = requests!.BeginOperation();
            await session!.PostAsync<MessageResult>($"users/{targetId}/password", new { new_password = password.Text }, operation.Cancellation);
            if (IsUsable && operation.IsCurrent && generation == Interlocked.Read(ref selectionGeneration)) status.Text = "비밀번호 초기화 완료. 새 비밀번호를 사용자에게 별도로 전달해 주세요.";
        }
        catch (OperationCanceledException) { /* The owning login session has ended. */ }
        catch (ApiException ex) { if (IsUsable && operation?.IsCurrent == true && generation == Interlocked.Read(ref selectionGeneration)) status.Text = ex.Message; }
        finally { writing = false; if (!IsDisposed) password.Clear(); SetControls(); }
    }
}
