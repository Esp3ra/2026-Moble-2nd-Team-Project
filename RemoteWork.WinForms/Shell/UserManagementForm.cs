using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public sealed class UserManagementForm : Form
{
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
    private bool loading;

    public UserManagementForm()
    {
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
        var create = new Button { Text = "새 계정 입력", AutoSize = true };
        actions.Controls.AddRange(new Control[] { active, create, save, reset });
        var actionRow = editor.RowCount++; editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); editor.Controls.Add(actions, 0, actionRow);
        layout.Controls.Add(editor, 1, 0); layout.SetRowSpan(editor, 2);
        var paging = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var reload = new Button { Text = "새로고침", AutoSize = true };
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
    }

    private void ClearEditor()
    {
        selectedId = 0; login.Enabled = true; login.Clear(); name.Clear(); number.Clear(); department.Clear(); password.Clear();
        role.SelectedIndex = 0; active.Checked = true; active.Enabled = false; save.Text = "계정 생성"; reset.Enabled = false;
    }

    private void Fill(UserInfo user)
    {
        selectedId = user.Id; login.Text = user.LoginId; login.Enabled = false; name.Text = user.DisplayName;
        number.Text = user.EmployeeNo; department.Text = user.Department; role.SelectedIndex = user.Role == "admin" ? 1 : 0;
        active.Checked = user.IsActive; active.Enabled = true; password.Clear(); save.Text = "사용자 정보 저장";
        reset.Enabled = user.Id != AppServices.Api.CurrentUser?.Id;
    }

    private async Task LoadUsers()
    {
        if (loading) return;
        loading = true; save.Enabled = false;
        previous.Enabled = next.Enabled = false;
        try
        {
            var result = await AppServices.Api.GetAsync<PageResult<UserInfo>>($"users?page={page}&limit=50");
            if (IsDisposed) return;
            grid.DataSource = result.Items;
            pageLabel.Text = $"{page}쪽 · 총 {result.Total}명";
            previous.Enabled = page > 1; next.Enabled = page * 50 < result.Total;
            if (result.Items.Count > 0) Fill(result.Items[0]); else ClearEditor();
            status.Text = "계정을 비활성화하거나 권한을 변경하면 해당 사용자의 기존 접속이 종료됩니다.";
        }
        catch (ApiException ex) { if (!IsDisposed) status.Text = ex.Message; }
        finally { loading = false; if (!IsDisposed) save.Enabled = true; }
    }

    private async Task Save()
    {
        save.Enabled = false;
        try
        {
            if (selectedId == 0)
                await AppServices.Api.PostAsync<UserResult>("users", new { login_id = login.Text.Trim(), display_name = name.Text.Trim(),
                    employee_no = number.Text.Trim(), department = department.Text.Trim(), role = role.SelectedIndex == 1 ? "admin" : "employee", password = password.Text });
            else
                await AppServices.Api.PatchAsync<UserResult>($"users/{selectedId}", new { display_name = name.Text.Trim(), employee_no = number.Text.Trim(),
                    department = department.Text.Trim(), role = role.SelectedIndex == 1 ? "admin" : "employee", is_active = active.Checked });
            password.Clear(); await LoadUsers();
        }
        catch (ApiException ex) { if (!IsDisposed) status.Text = ex.Message; }
        finally { if (!IsDisposed) save.Enabled = true; }
    }

    private async Task ResetPassword()
    {
        if (selectedId == 0 || selectedId == AppServices.Api.CurrentUser?.Id) return;
        if (password.Text.Length < 10) { status.Text = "초기화할 새 비밀번호를 10~128자로 입력해 주세요."; return; }
        if (MessageBox.Show(this, "선택한 사용자의 비밀번호를 변경하고 모든 접속을 종료할까요?", "비밀번호 초기화", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        reset.Enabled = false;
        try
        {
            await AppServices.Api.PostAsync<MessageResult>($"users/{selectedId}/password", new { new_password = password.Text });
            if (!IsDisposed) status.Text = "비밀번호 초기화 완료. 새 비밀번호를 사용자에게 별도로 전달해 주세요.";
        }
        catch (ApiException ex) { if (!IsDisposed) status.Text = ex.Message; }
        finally { password.Clear(); if (!IsDisposed) reset.Enabled = true; }
    }
}
