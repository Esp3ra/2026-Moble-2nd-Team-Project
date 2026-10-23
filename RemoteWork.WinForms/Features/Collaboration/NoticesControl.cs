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
        private int? attachmentNoticeId;
        private readonly Button attachmentDownload = new() { Text = "첨부 다운로드", AutoSize = true, Height = 28, Margin = new Padding(0), Enabled = false };
        private ResourceAttachmentClient? attachmentClient;
        private bool lifetimeEnded;

        public NoticesControl() : this(PreviewRole.Employee) { }
        public NoticesControl(PreviewRole role) : this(role, new HttpNoticeService(AppServices.Api)) { }

        public NoticesControl(PreviewRole role, INoticeService noticeService)
        {
            this.noticeService = noticeService;
            previewRole = role;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeDeadlineView();
            InitializeNoticeView(role);
            cmbOptions2.SelectedIndexChanged += FilterNotices;
            gridNotices.ColumnHeaderMouseClick += SortNotices;
            InitializeDeleteMenu();
            var fileMenu = new ContextMenuStrip(components!);
            var download = new ToolStripMenuItem("첨부파일 다운로드…");
            fileMenu.Items.Add(download);
            fileMenu.Opening += (_, e) => e.Cancel = attachmentNoticeId is null || busy;
            download.Click += (_, _) =>
            {
                if (attachmentNoticeId is not int id) return;
                try
                {
                    attachmentClient ??= new ResourceAttachmentClient(AppServices.Api.CaptureSession());
                    using var dialog = new AttachmentDownloadForm(attachmentClient, "notice", id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    dialog.ShowDialog(FindForm());
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); }
            };
            txtContent.ContextMenuStrip = fileMenu;
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutContent7.Controls.Remove(lblText4); lblText4.AutoSize = false; lblText4.Dock = DockStyle.Fill; lblText4.AutoEllipsis = true;
            header.Controls.Add(lblText4, 0, 0); header.Controls.Add(attachmentDownload, 1, 0); layoutContent7.Controls.Add(header, 0, 0);
            attachmentDownload.Click += (_, _) => download.PerformClick();
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
            BindDeadlineAlertsFromOwner();
            if (!PreviewActions.IsDesignMode(this)) _ = RefreshDeadlineAlertsAsync();
            if (!PreviewActions.IsDesignMode(this)) await ReloadNoticesAsync();
        }

        // Also available for explicit refresh and integration checks; no static/sample store.
        public async Task ReloadNoticesAsync()
        {
            if (busy || IsDisposed) return;
            SetBusy(true);
            try { await LoadNoticesAsync((gridNotices.CurrentRow?.Tag as Notice)?.Id); }
            catch (ApiException ex) { ShowLoadError(ex.Message); }
            catch (OperationCanceledException) { if (!IsDisposed) ShowLoadError("접속이 변경되었습니다. 다시 로그인한 후 조회해 주세요."); }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        private async Task LoadNoticesAsync(int? selectId = null)
        {
            var loaded = await noticeService.GetNoticesAsync(lifetime.Token);
            if (IsDisposed) return;
            notices.Clear();
            notices.AddRange(loaded);
            unknownDeletes.Clear();
            RefreshNotices(notices.FirstOrDefault(n => n.Id == selectId));
        }

        private void SetBusy(bool value)
        {
            busy = value;
            attachmentDownload.Enabled = !value && attachmentNoticeId is not null;
            UseWaitCursor = value;
            btnQuery.Enabled = btnNewNoticePreview.Enabled = cmbOptions2.Enabled = gridNotices.Enabled = !value;
            lblText2.Text = value ? "공지사항 목록 · 처리 중…" : "공지사항 목록";
        }

        private void ShowLoadError(string message)
        {
            if (IsDisposed) return;
            // Never show stale cached content as if a server query succeeded.
            notices.Clear();
            RefreshNotices();
            lblText4.Text = "공지사항 조회 실패";
            txtContent.Text = message + "\r\n조회 버튼을 눌러 다시 시도해 주세요.";
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

            var menu = new ContextMenuStrip(components!);
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

            deleteItem.Click += async (_, _) =>
            {
                if (rightClickedNotice is not null)
                    await DeleteNotice(rightClickedNotice);
            };
        }

        private readonly HashSet<int> unknownDeletes = [];

        private async Task DeleteNotice(Notice notice)
        {
            if (previewRole != PreviewRole.Administrator || busy || IsDisposed) return;
            if (unknownDeletes.Contains(notice.Id))
            {
                lblText4.Text = "삭제 결과 확인 필요";
                txtContent.Text = "원래 공지의 삭제 결과가 불명확합니다. 조회 버튼으로 목록을 새로 확인한 뒤 진행해 주세요.";
                return;
            }
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
                if (!IsDisposed)
                {
                    bool unknown = ex.StatusCode is < 400 or >= 500;
                    if (unknown) unknownDeletes.Add(notice.Id);
                    lblText4.Text = unknown ? "삭제 결과 확인 필요" : "공지사항 삭제 실패";
                    txtContent.Text = ex.Message + (unknown ? "\r\n자동 재전송하지 않습니다. 조회 버튼으로 원래 공지의 저장 상태를 확인해 주세요." : "");
                }
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) { unknownDeletes.Add(notice.Id); lblText4.Text = "삭제 결과 확인 필요"; txtContent.Text = "접속이 변경되었습니다. 다시 로그인한 후 목록을 확인해 주세요."; }
            }
            finally { if (!IsDisposed) SetBusy(false); }
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
                    if (!IsDisposed) { attachmentNoticeId = detail.Id; lblText4.Text = detail.Title; txtContent.Text = detail.GetDisplayText() + "\r\n\r\n첨부파일: 본문 우클릭 → 첨부파일 다운로드"; }
                }
            }
            catch (ApiException ex) { ShowLoadError(ex.Message); }
            catch (OperationCanceledException) { if (!IsDisposed) ShowLoadError("접속이 변경되었습니다. 다시 로그인한 후 조회해 주세요."); }
            finally { if (!IsDisposed) SetBusy(false); }
        }

        private void ClearNoticeDetails()
        {
            attachmentNoticeId = null;
            attachmentDownload.Enabled = false;
            lblText4.Text = "공지사항 상세";
            txtContent.Text = gridNotices.Rows.Count == 0
                ? "조회할 공지사항이 없습니다."
                : "목록에서 공지사항을 선택한 뒤 조회 버튼을 눌러 주세요.";
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
