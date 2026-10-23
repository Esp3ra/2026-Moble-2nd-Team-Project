using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static void LeaveListLayout(string? renderDirectory)
    {
        foreach (bool administrator in new[] { false, true })
        foreach (var size in new[] { new Size(1100, 600), new Size(900, 460), new Size(1320, 760) })
        {
            using var api = Login(administrator);
            var flow = new Workflow();
            using var page = new AttendanceControl(administrator ? PreviewRole.Administrator : PreviewRole.Employee,
                api.CaptureSession(), flow) { Dock = DockStyle.Fill };
            using var host = new Form { ClientSize = size, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new(-20000, -20000), Text = "연차 목록 · 합성 데이터 화면 검사" };
            host.Controls.Add(page);
            host.Show();
            flow.ListCompletion.SetResult([Request() with { Status = "rejected", DecisionReason = "일정 조정이 필요합니다." }]);
            Complete(Call(page, "LoadLeaveAsync"));
            var grid = Field<DataGridView>(page, "gridLeaveRequests");
            foreach (bool leaveOnly in new[] { false, true, false })
            {
                page.LeaveOnly = leaveOnly;
                page.PerformLayout();
                host.PerformLayout();
                Application.DoEvents();
                if (size.Height >= 600 && !leaveOnly)
                {
                    page.AutoScrollPosition = Point.Empty;
                    Application.DoEvents();
                    var initialRow = grid.GetRowDisplayRectangle(0, true);
                    var initialVisible = Rectangle.Intersect(grid.RectangleToScreen(initialRow), page.RectangleToScreen(page.ClientRectangle));
                    Check(initialRow.Height == grid.Rows[0].Height && initialVisible.Height == initialRow.Height,
                        "Request visible without scrolling at supported window height: " + size);
                }
                page.ScrollControlIntoView(grid);
                Application.DoEvents();
                string context = $"{size.Width}x{size.Height}, admin={administrator}, leaveOnly={leaveOnly}";
                Check(grid.Rows.Count == 1, "Stored request retained through resize/mode changes: " + context);
                var row = grid.GetRowDisplayRectangle(0, true);
                Check(row.Height == grid.Rows[0].Height && row.Height > 0,
                    $"A complete data row fits below the header (grid={grid.Height}, row={row.Height}): " + context);
                var visible = Rectangle.Intersect(grid.RectangleToScreen(row), page.RectangleToScreen(page.ClientRectangle));
                Check(visible.Height == row.Height && visible.Width > 0, "Data row reachable in the scroll viewport: " + context);
                grid.Rows[0].Selected = true;
                Application.DoEvents();
                Check(Field<Button>(page, "btnLeaveDetails").Enabled, "Visible row can open application details: " + context);
                Check(grid.Rows[0].Cells["columnLeaveDecisionReason"].Value?.ToString() == "일정 조정이 필요합니다.",
                    "Rejection reason retained in the actual data grid: " + context);
            }
            if (!administrator && renderDirectory is not null && size.Height is 600 or 460)
            {
                Directory.CreateDirectory(renderDirectory);
                using var image = new Bitmap(host.Width, host.Height);
                host.DrawToBitmap(image, new Rectangle(Point.Empty, host.Size));
                image.Save(Path.Combine(renderDirectory, $"leave-list-{size.Width}x{size.Height}.png"));
            }
        }
    }
}
