using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public sealed class PasswordChangeForm : Form
{
    public PasswordChangeForm()
    {
        Text = "비밀번호 변경"; ClientSize = new Size(470, 390); MinimumSize = new Size(470, 430);
        Font = new Font("맑은 고딕", 9F); StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 9 };
        for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i % 2 == 0 ? 26 : 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var current = new TextBox { UseSystemPasswordChar = true, MaxLength = 128, Dock = DockStyle.Fill };
        var next = new TextBox { UseSystemPasswordChar = true, MaxLength = 128, Dock = DockStyle.Fill };
        var repeat = new TextBox { UseSystemPasswordChar = true, MaxLength = 128, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = "현재 비밀번호", AutoSize = true }, 0, 0); layout.Controls.Add(current, 0, 1);
        layout.Controls.Add(new Label { Text = "새 비밀번호 (10~128자)", AutoSize = true }, 0, 2); layout.Controls.Add(next, 0, 3);
        layout.Controls.Add(new Label { Text = "새 비밀번호 확인", AutoSize = true }, 0, 4); layout.Controls.Add(repeat, 0, 5);
        layout.Controls.Add(new Label { Text = "변경하면 웹과 다른 PC를 포함한 모든 접속이 종료됩니다.", AutoSize = true, MaximumSize = new Size(400, 0) }, 0, 6);
        var status = new Label { AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = Color.Firebrick };
        layout.Controls.Add(status, 0, 7);
        var save = new Button { Text = "비밀번호 변경", Dock = DockStyle.Fill };
        layout.Controls.Add(save, 0, 8); Controls.Add(layout); AcceptButton = save;
        save.Click += async (_, _) =>
        {
            if (next.Text.Length < 10 || next.Text != repeat.Text) { status.Text = "새 비밀번호 길이와 확인 입력을 확인해 주세요."; return; }
            save.Enabled = false;
            try
            {
                await AppServices.Api.PostAsync<MessageResult>("auth/password", new { current_password = current.Text, new_password = next.Text });
                AppServices.Api.ClearIdentity();
                if (IsDisposed) return;
                MessageBox.Show(this, "비밀번호를 변경했습니다. 다시 로그인해 주세요.", "변경 완료");
                DialogResult = DialogResult.OK; Close();
            }
            catch (OperationCanceledException) { /* The owning login session has ended. */ }
            catch (ApiException ex) { if (!IsDisposed) status.Text = ex.Message; }
            finally { current.Clear(); next.Clear(); repeat.Clear(); if (!IsDisposed) save.Enabled = true; }
        };
    }
}
