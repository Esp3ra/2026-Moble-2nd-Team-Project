using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public sealed class UserManagementForm : Form
{
    private readonly ApiClient.Session? session;
    private readonly FeatureUiScope? requests;
    private CancellationTokenRegistration sessionCancellation;
    private CreatedUserEnrollmentTarget? enrollmentTarget;
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

    /// <summary>Future enrollment service borrows this target. No scan starts automatically.</summary>
    public CreatedUserEnrollmentTarget? EnrollmentTarget => enrollmentTarget?.IsCurrent == true ? enrollmentTarget : null;
    public event EventHandler? EnrollmentTargetChanged;

    public UserManagementForm() : this(AppServices.Api) { }

    public UserManagementForm(ApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        try { session = api.CaptureSession(); }
        catch (InvalidOperationException) { /* A disconnected view never starts requests. */ }
        if (session is not null)
            requests = new FeatureUiScope(session, action => BeginInvoke(action), () => IsUsable);
        Text = "사용자 관리"; ClientSize = new Size(1060, 640); MinimumSize = new Size(960, 650);
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
        actions.Controls.AddRange(new Control[] { active, create, save, reset });
        var actionRow = editor.RowCount++; editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); editor.Controls.Add(actions, 0, actionRow);
        layout.Controls.Add(editor, 1, 0); layout.SetRowSpan(editor, 2);
        var paging = new FlowLayoutPanel { Dock = DockStyle.Fill };
        paging.Controls.AddRange(new Control[] { previous, pageLabel, next, reload }); layout.Controls.Add(paging, 0, 1);
        layout.Controls.Add(status, 0, 2); layout.SetColumnSpan(status, 2); Controls.Add(layout);
        grid.SelectionChanged += (_, _) => { if (!loading && grid.CurrentRow?.DataBoundItem is UserInfo user) Fill(user); };
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
        bool enabled = IsUsable && !loading && !writing;
        grid.Enabled = create.Enabled = reload.Enabled = save.Enabled = enabled;
        foreach (var control in new Control[] { name, number, department, role, password }) control.Enabled = enabled;
        login.Enabled = enabled && selectedId == 0;
        active.Enabled = enabled && selectedId != 0;
        reset.Enabled = enabled && selectedId != 0 && selectedId != session?.User.Id;
        previous.Enabled = enabled && page > 1;
        next.Enabled = enabled && page * 50 < total;
    }

    private void ClearEditor()
    {
        InvalidateTarget();
        selectedId = 0; login.Enabled = true; login.Clear(); name.Clear(); number.Clear(); department.Clear(); password.Clear();
        role.SelectedIndex = 0; active.Checked = true; active.Enabled = false; save.Text = "계정 생성"; reset.Enabled = false;
        SetControls();
    }

    private void Fill(UserInfo user, bool invalidate = true)
    {
        if (invalidate) InvalidateTarget();
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
                ? "계정 생성 완료. 지문 등록은 별도 연결 단계이며, 생성된 회원 ID를 유지합니다."
                : "사용자 정보 저장 완료.";
            status.Text = message;
            await LoadUsers(result.User, message);
        }
        catch (OperationCanceledException) { /* The owning login session has ended. */ }
        catch (ApiException ex)
        { if (IsUsable && operation?.IsCurrent == true && generation == Interlocked.Read(ref selectionGeneration)) status.Text = ex.Message + (targetId == 0 && ex.StatusCode == 0 ? " 생성 여부를 목록에서 확인한 뒤 다시 시도해 주세요." : ""); }
        finally { writing = false; SetControls(); }
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
