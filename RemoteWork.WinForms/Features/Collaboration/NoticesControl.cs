using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class NoticesControl : UserControl
    {
        private readonly List<Notice> notices = new();
        private readonly INoticeService noticeService;
        private readonly CancellationTokenSource lifetime = new();
        private bool busy;
        private bool lifetimeEnded;

        public NoticesControl() : this(PreviewRole.Employee) { }
        public NoticesControl(PreviewRole role) : this(role, new HttpNoticeService(AppServices.Api)) { }

        public NoticesControl(PreviewRole role, INoticeService noticeService)
        {
            this.noticeService = noticeService;
            previewRole = role;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeNoticeView(role);
            cmbOptions2.SelectedIndexChanged += FilterNotices;
            gridNotices.ColumnHeaderMouseClick += SortNotices;
            InitializeDeleteMenu();
            ClearNoticeDetails();
            Disposed += (_, _) =>
            {
                if (lifetimeEnded) return;
                lifetimeEnded = true;
                lifetime.Cancel();
                lifetime.Dispose();
            };
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!PreviewActions.IsDesignMode(this)) await ReloadNoticesAsync();
        }

        // Also available for explicit refresh and integration checks; no static/sample store.
        public async Task ReloadNoticesAsync()
        {
            if (busy || IsDisposed) return;
            SetBusy(true);
            try { await LoadNoticesAsync((gridNotices.CurrentRow?.Tag as Notice)?.Id); }
            catch (ApiException ex) { ShowLoadError(ex.Message); }
            catch (OperationCanceledException) { if (!IsDisposed) ShowLoadError("������ ����Ǿ����ϴ�. �ٽ� �α����� �� ��ȸ�� �ּ���."); }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        private async Task LoadNoticesAsync(int? selectId = null)
        {
            var loaded = await noticeService.GetNoticesAsync(lifetime.Token);
            if (IsDisposed) return;
            notices.Clear();
            notices.AddRange(loaded);
            RefreshNotices(notices.FirstOrDefault(n => n.Id == selectId));
        }

        private void SetBusy(bool value)
        {
            busy = value;
            UseWaitCursor = value;
            btnQuery.Enabled = btnNewNoticePreview.Enabled = cmbOptions2.Enabled = gridNotices.Enabled = !value;
            lblText2.Text = value ? "�������� ��� �� ó�� �ߡ�" : "�������� ���";
        }

        private void ShowLoadError(string message)
        {
            if (IsDisposed) return;
            // Never show stale cached content as if a server query succeeded.
            notices.Clear();
            RefreshNotices();
            lblText4.Text = "�������� ��ȸ ����";
            txtContent.Text = message + "\r\n��ȸ ��ư�� ���� �ٽ� �õ��� �ּ���.";
        }

        private void InitializeNoticeView(PreviewRole role)
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
            var deleteItem = new ToolStripMenuItem("����");
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

            deleteItem.Click += async (_, _) =>
            {
                if (rightClickedNotice is not null)
                    await DeleteNotice(rightClickedNotice);
            };
        }

        private async Task DeleteNotice(Notice notice)
        {
            if (previewRole != PreviewRole.Administrator || busy || IsDisposed) return;
            SetBusy(true);
            try
            {
                await noticeService.DeleteNoticeAsync(notice.Id, lifetime.Token);
                if (IsDisposed) return;
                notices.RemoveAll(n => n.Id == notice.Id);
                RefreshNotices();
            }
            catch (ApiException ex)
            {
                if (!IsDisposed) { lblText4.Text = "�������� ���� ����"; txtContent.Text = ex.Message; }
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) { lblText4.Text = "���� ��� Ȯ�� �ʿ�"; txtContent.Text = "������ ����Ǿ����ϴ�. �ٽ� �α����� �� ����� Ȯ���� �ּ���."; }
            }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        private void RefreshNotices(Notice? select = null)
        {
            gridNotices.Rows.Clear();
            string? target = cmbOptions2.SelectedItem?.ToString() switch
            {
                "�� ����" => "�� ����",
                "��ȹ��" => "��ȹ��",
                "������" => "������",
                "��������" => "��������",
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
                    .FirstOrDefault(row => select is not null && (row.Tag as Notice)?.Id == select.Id)
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

        private async void QueryNotice_Click(object? sender, EventArgs e)
        {
            if (busy || IsDisposed) return;
            int? selectedId = (gridNotices.CurrentRow?.Tag as Notice)?.Id;
            SetBusy(true);
            try
            {
                // Refresh from DB as well: notices written on another PC appear here.
                await LoadNoticesAsync(selectedId);
                if (IsDisposed) return;
                if (gridNotices.CurrentRow?.Tag is Notice selected)
                {
                    var detail = await noticeService.GetNoticeAsync(selected.Id, lifetime.Token);
                    if (!IsDisposed) { lblText4.Text = detail.Title; txtContent.Text = detail.GetDisplayText(); }
                }
            }
            catch (ApiException ex) { ShowLoadError(ex.Message); }
            catch (OperationCanceledException) { if (!IsDisposed) ShowLoadError("������ ����Ǿ����ϴ�. �ٽ� �α����� �� ��ȸ�� �ּ���."); }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        private void ClearNoticeDetails()
        {
            lblText4.Text = "�������� ��";
            txtContent.Text = gridNotices.Rows.Count == 0
                ? "��ȸ�� ���������� �����ϴ�."
                : "��Ͽ��� ���������� ������ �� ��ȸ ��ư�� ���� �ּ���.";
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnNewNoticePreview_Click(object? sender, EventArgs e)
        {
            if (previewRole != PreviewRole.Administrator || busy || IsDisposed) return;
            using var form = new NoticeEditForm(noticeService);
            if (form.ShowDialog(FindForm()) != DialogResult.OK || form.CreatedNotice is null)
                return;

            if (IsDisposed) return;
            // CreatedNotice is the committed server response (including ID/author/date).
            notices.RemoveAll(n => n.Id == form.CreatedNotice.Id);
            notices.Add(form.CreatedNotice);
            RefreshNotices(form.CreatedNotice);
        }

    }
}
