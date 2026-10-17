using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class NoticesControl : UserControl
    {
        // Shared for the lifetime of the application, including menu navigation.
        private static readonly List<Notice> notices = new()
        {
            new Notice
            {
                Title = "보안 점검 안내", Target = "전 직원", IsPinned = true,
                CreatedAt = new DateTime(2026, 10, 13),
                Content = "원격 근무 환경의 보안 점검이 예정되어 있습니다.\r\n담당 업무와 일정을 확인해 주세요."
            },
            new Notice
            {
                Title = "주간 업무 공유", Target = "기획팀",
                CreatedAt = new DateTime(2026, 10, 12),
                Content = "이번 주 업무 일정과 진행 상황을 공유해 주세요."
            },
            new Notice
            {
                Title = "개발 일정 안내", Target = "개발팀",
                CreatedAt = new DateTime(2026, 10, 11),
                Content = "개발 일정과 담당 업무를 확인해 주세요."
            }
        };

        public NoticesControl() : this(PreviewRole.Employee) { }

        public NoticesControl(PreviewRole role)
        {
            previewRole = role;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
            cmbOptions2.SelectedIndexChanged += FilterNotices;
            gridNotices.ColumnHeaderMouseClick += SortNotices;
            InitializeDeleteMenu();
            ClearNoticeDetails();
        }

        private void InitializeSampleData(PreviewRole role)
        {
            bool administrator = role == PreviewRole.Administrator;
            cmbOptions2.SelectedIndex = 0;
            btnNewNoticePreview.Visible = administrator;
            RefreshNotices();
        }

        private readonly PreviewRole previewRole;
        private int sortColumnIndex = -1;
        private bool sortAscending = true;

        private void SortNotices(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.ColumnIndex < 0) return;

            var selectedNotice = gridNotices.CurrentRow?.Tag as Notice;
            sortAscending = sortColumnIndex != e.ColumnIndex || !sortAscending;
            sortColumnIndex = e.ColumnIndex;
            RefreshNotices(selectedNotice);
        }

        private IOrderedEnumerable<Notice> OrderNotices(IEnumerable<Notice> source)
        {
            // Pinned notices remain first, regardless of the selected column.
            var ordered = source.OrderByDescending(n => n.IsPinned);
            if (sortColumnIndex == -1 || sortColumnIndex == 3)
                return sortColumnIndex == 3 && sortAscending
                    ? ordered.ThenBy(n => n.CreatedAt)
                    : ordered.ThenByDescending(n => n.CreatedAt);

            Func<Notice, string> key = sortColumnIndex switch
            {
                0 => n => n.Target,
                1 => n => n.Title,
                _ => n => n.Author
            };
            var comparer = StringComparer.Create(
                System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), false);
            return (sortAscending
                ? ordered.ThenBy(key, comparer)
                : ordered.ThenByDescending(key, comparer))
                .ThenByDescending(n => n.CreatedAt);
        }

        private void InitializeDeleteMenu()
        {
            if (previewRole != PreviewRole.Administrator) return;

            var menu = new ContextMenuStrip(components);
            var deleteItem = new ToolStripMenuItem("삭제");
            Notice? rightClickedNotice = null;
            menu.Items.Add(deleteItem);

            gridNotices.CellMouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;

                var row = gridNotices.Rows[e.RowIndex];
                if (row.Tag is not Notice notice) return;

                gridNotices.ClearSelection();
                gridNotices.CurrentCell = row.Cells[e.ColumnIndex];
                row.Selected = true;
                rightClickedNotice = notice;
                menu.Show(Cursor.Position);
            };

            deleteItem.Click += (_, _) =>
            {
                if (rightClickedNotice is not null)
                    DeleteNotice(rightClickedNotice);
            };
        }

        private void DeleteNotice(Notice notice)
        {
            if (previewRole != PreviewRole.Administrator) return;

            int index = notices.IndexOf(notice);
            if (index < 0) return;

            notices.RemoveAt(index);
            Notice? next = notices.Count == 0 ? null : notices[Math.Min(index, notices.Count - 1)];
            RefreshNotices(next);
        }

        private void RefreshNotices(Notice? select = null)
        {
            gridNotices.Rows.Clear();
            string? target = cmbOptions2.SelectedItem?.ToString() switch
            {
                "전 직원" => "전 직원",
                "기획팀" => "기획팀",
                "개발팀" => "개발팀",
                "디자인팀" => "디자인팀",
                _ => null
            };
            var filteredNotices = notices.Where(notice =>
                target is null || notice.Target == target);

            foreach (var notice in OrderNotices(filteredNotices))
            {
                int index = gridNotices.Rows.Add(notice.Target, notice.Title,
                    notice.Author, notice.CreatedAt.ToString("yyyy.MM.dd"));
                gridNotices.Rows[index].Tag = notice;
            }
            if (gridNotices.Rows.Count > 0)
            {
                var selectedRow = gridNotices.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(row => ReferenceEquals(row.Tag, select))
                    ?? gridNotices.Rows[0];
                gridNotices.CurrentCell = selectedRow.Cells[0];
            }
            foreach (DataGridViewColumn column in gridNotices.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
            ClearNoticeDetails();
        }

        private void FilterNotices(object? sender, EventArgs e)
        {
            RefreshNotices(gridNotices.CurrentRow?.Tag as Notice);
        }

        private void QueryNotice_Click(object? sender, EventArgs e)
        {
            ShowSelectedNotice();
        }

        private void ClearNoticeDetails()
        {
            lblText4.Text = "공지사항 상세";
            txtContent.Text = gridNotices.Rows.Count == 0
                ? "조회할 공지사항이 없습니다."
                : "목록에서 공지사항을 선택한 뒤 조회 버튼을 눌러 주세요.";
        }

        private void ShowSelectedNotice()
        {
            var notice = gridNotices.CurrentRow?.Tag as Notice;
            lblText4.Text = notice?.Title ?? "공지사항 상세";
            txtContent.Text = notice?.GetDisplayText() ?? "목록에서 공지사항을 선택한 뒤 조회 버튼을 눌러 주세요.";
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnNewNoticePreview_Click(object? sender, EventArgs e)
        {
            using var form = new NoticeEditForm();
            if (form.ShowDialog(FindForm()) != DialogResult.OK || form.CreatedNotice is null)
                return;

            notices.Add(form.CreatedNotice);
            RefreshNotices(form.CreatedNotice);
        }

    }
}
