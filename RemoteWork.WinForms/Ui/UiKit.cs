namespace RemoteWork.WinForms.Ui;

/// <summary>Layout helpers only. No authentication, network, storage or business operations.</summary>
public static class UiKit
{
    public static readonly Color Background = Color.FromArgb(242, 245, 250);
    public static readonly Color Ink = Color.FromArgb(30, 42, 61);
    public static readonly Color Muted = Color.FromArgb(103, 116, 135);
    public static readonly Color Accent = Color.FromArgb(52, 101, 224);
    public static readonly Color Navy = Color.FromArgb(23, 38, 63);

    public static Label Label(string text, int size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = true, ForeColor = Ink,
        Font = new Font("맑은 고딕", size, bold ? FontStyle.Bold : FontStyle.Regular),
        Margin = new Padding(0, 4, 8, 4)
    };

    // Only preview/navigation callbacks are supplied by screens. A missing callback is inert.
    public static Button Button(string text, Action? click = null, bool primary = false)
    {
        var button = new Button
        {
            Text = text, AutoSize = true, MinimumSize = new Size(88, 34),
            Padding = new Padding(12, 4, 12, 4), FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Color.White,
            ForeColor = primary ? Color.White : Ink,
            Font = new Font("맑은 고딕", 9), Margin = new Padding(0, 0, 8, 6),
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(213, 220, 231);
        button.Click += (_, _) =>
        {
            if (click is not null) click();
            else MessageBox.Show(button.FindForm(), "준비 중인 기능입니다.\n현재는 화면 기본 틀만 제공하며, 데이터를 처리하거나 저장·전송하지 않습니다.",
                "기능 준비 중", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        return button;
    }

    public static TextBox Input(string text = "", bool multiline = false) => new()
    {
        Text = text, Multiline = multiline, Dock = DockStyle.Fill,
        Height = multiline ? 96 : 30, MinimumSize = new Size(0, multiline ? 90 : 28),
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
        Font = new Font("맑은 고딕", 10), BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(0, 4, 0, 6)
    };

    public static ComboBox Combo(params string[] values)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill,
            Font = new Font("맑은 고딕", 10), Margin = new Padding(0, 4, 0, 6) };
        box.Items.AddRange(values);
        if (values.Length > 0) box.SelectedIndex = 0;
        return box;
    }

    public static TextBox ReadOnlyText(string text)
    {
        var multiline = text.Contains('\n');
        return new TextBox
        {
            Text = text, ReadOnly = true, Multiline = multiline, Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Ink,
            Font = new Font("맑은 고딕", 10), ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            MinimumSize = new Size(0, multiline ? 56 : 28), TabStop = false
        };
    }

    public static DataGridView Grid(string[] columns, params string[][] rows)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, AllowUserToOrderColumns = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(235, 239, 246), EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 36, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            MinimumSize = new Size(0, 140), Font = new Font("맑은 고딕", 9)
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(243, 246, 251), ForeColor = Muted,
            Font = new Font("맑은 고딕", 9, FontStyle.Bold), Padding = new Padding(6)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            ForeColor = Ink, BackColor = Color.White,
            SelectionBackColor = Color.FromArgb(231, 239, 255), SelectionForeColor = Ink,
            Padding = new Padding(6), NullValue = ""
        };
        grid.RowTemplate.Height = 38;
        for (var index = 0; index < columns.Length; index++)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = $"Column{index}", HeaderText = columns[index], MinimumWidth = ColumnWidth(columns[index]),
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        foreach (var row in rows) grid.Rows.Add(row.Cast<object>().ToArray());
        return grid;
    }

    private static int ColumnWidth(string title)
    {
        if (title.Contains("제목") || title.Contains("내용") || title.Contains("파일") || title.Contains("기간")) return 168;
        if (title.Contains("시각") || title.Contains("일시")) return 144;
        if (title.Contains("일자") || title.Contains("일") || title.Contains("번호")) return 106;
        return 82;
    }

    public static Panel Card(string title, Control content)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White,
            Padding = new Padding(16), Margin = new Padding(0, 0, 10, 10),
            MinimumSize = new Size(0, Math.Max(120, content.MinimumSize.Height + 68)), BorderStyle = BorderStyle.FixedSingle };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Label(title, 10, true), 0, 0);
        content.Dock = DockStyle.Fill;
        layout.Controls.Add(content, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    public static FlowLayoutPanel Toolbar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
            Margin = new Padding(0, 0, 0, 8), MinimumSize = new Size(0, 40) };
        foreach (var control in controls) { control.Dock = DockStyle.None; bar.Controls.Add(control); }
        return bar;
    }

    public static TableLayoutPanel Split(Control left, Control right, int leftPercent = 60)
    {
        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, leftPercent));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 - leftPercent));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Dock = right.Dock = DockStyle.Fill;
        split.Controls.Add(left, 0, 0); split.Controls.Add(right, 1, 0);
        return split;
    }

    public static Panel Fields(params (string Label, Control Input)[] fields)
    {
        var scroll = new PreviewScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, RowCount = fields.Length, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < fields.Length; row++)
        {
            var (text, input) = fields[row];
            var height = input is TextBox { Multiline: true } ? 112 : input is DataGridView ? 180 : 44;
            if (input is TextBox { ReadOnly: true, Multiline: true } readOnly)
            {
                height = Math.Clamp(readOnly.Text.Split('\n').Length * 22 + 12, 56, 112);
                readOnly.MinimumSize = new Size(0, height - 10);
            }
            if (input is DateTimePicker date)
            {
                date.Format = DateTimePickerFormat.Custom; date.CustomFormat = " yyyy-MM-dd";
                date.Font = new Font("맑은 고딕", 9);
            }
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            var label = Label(text, 9, true); label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft; label.AutoSize = false;
            input.Dock = DockStyle.Fill;
            layout.Controls.Add(label, 0, row); layout.Controls.Add(input, 1, row);
        }
        scroll.Controls.Add(layout);
        return scroll;
    }

    private static Panel ScrollContent(Control content, int minimumHeight)
    {
        var scroll = new PreviewScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        content.Dock = DockStyle.Top;
        content.Margin = Padding.Empty; content.MinimumSize = new Size(0, minimumHeight);
        content.Height = minimumHeight;
        scroll.Controls.Add(content);
        void ResizeContent()
        {
            content.Height = Math.Max(minimumHeight, scroll.ClientSize.Height);
        }
        scroll.ClientSizeChanged += (_, _) => ResizeContent();
        scroll.SizeChanged += (_, _) => ResizeContent();
        ResizeContent();
        return scroll;
    }

    private sealed class PreviewScrollPanel : Panel
    {
        // Keep a manually selected scroll position when a preview input retains focus.
        protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;
    }

    public static TableLayoutPanel Stack(params Control[] controls)
    {
        var stack = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
            ColumnCount = 1, RowCount = controls.Length, Margin = Padding.Empty };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var filling = controls.Count(c => c is not FlowLayoutPanel && c is not System.Windows.Forms.Label);
        for (var row = 0; row < controls.Length; row++)
        {
            var control = controls[row];
            var compact = control is FlowLayoutPanel || control is System.Windows.Forms.Label;
            stack.RowStyles.Add(compact ? new RowStyle(SizeType.AutoSize) : new RowStyle(SizeType.Percent, 100f / Math.Max(1, filling)));
            control.Dock = DockStyle.Fill; stack.Controls.Add(control, 0, row);
        }
        return stack;
    }

    public static void Page(UserControl page, string title, string description, Control content)
    {
        page.SuspendLayout();
        page.AutoScaleMode = AutoScaleMode.Font;
        page.Font = new Font("맑은 고딕", 9);
        page.BackColor = Background; page.Padding = new Padding(24, 18, 14, 14);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(Heading(title, description), 0, 0);
        layout.Controls.Add(PreviewBanner(), 0, 1);
        layout.Controls.Add(ScrollContent(content, 560), 0, 2);
        page.Controls.Add(layout); page.ResumeLayout(true);
    }

    public static void Dialog(Form form, string title, string description, Control content, params Control[] actions)
    {
        form.SuspendLayout();
        form.Text = $"{title} · 화면 미리보기"; form.Font = new Font("맑은 고딕", 9);
        form.AutoScaleMode = AutoScaleMode.Font; form.BackColor = Background;
        form.ClientSize = new Size(780, 630); form.MinimumSize = new Size(690, 560);
        form.StartPosition = FormStartPosition.CenterParent; form.ShowInTaskbar = false;
        form.Padding = new Padding(22); form.MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.Controls.Add(Heading(title, description), 0, 0); layout.Controls.Add(PreviewBanner(), 0, 1);
        layout.Controls.Add(ScrollContent(content, 440), 0, 2);
        var close = Button("닫기", form.Close);
        close.DialogResult = DialogResult.Cancel; form.CancelButton = close;
        var footer = Toolbar(actions.Concat(new Control[] { close }).ToArray());
        footer.FlowDirection = FlowDirection.RightToLeft; footer.Padding = new Padding(0, 10, 0, 0);
        layout.Controls.Add(footer, 0, 3);
        form.Controls.Add(layout); form.ResumeLayout(true);
        form.Shown += (_, _) => ResetScroll(form);
    }

    private static void ResetScroll(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is ScrollableControl scroll) scroll.AutoScrollPosition = Point.Empty;
            ResetScroll(child);
        }
    }

    private static Control Heading(string title, string description)
    {
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(Label(title, 20, true), 0, 0);
        var subtitle = Label(description, 9); subtitle.ForeColor = Muted;
        heading.Controls.Add(subtitle, 0, 1); return heading;
    }

    private static Control PreviewBanner()
    {
        var banner = Label("샘플 화면  ·  서버와 장치 미연결  ·  모든 업무 처리 기능 준비 중", 9);
        banner.AutoSize = false; banner.Dock = DockStyle.Fill;
        banner.TextAlign = ContentAlignment.MiddleLeft; banner.BackColor = Color.FromArgb(230, 238, 255);
        banner.ForeColor = Accent; banner.Padding = new Padding(10, 0, 0, 0);
        banner.Margin = new Padding(0, 0, 10, 10); return banner;
    }

    public static void OpenPreview(Control owner, Form form)
    {
        using (form) form.ShowDialog(owner.FindForm());
    }
}
