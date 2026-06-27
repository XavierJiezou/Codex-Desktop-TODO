using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CodexDesktopTodo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TodoForm());
        }
    }

    internal sealed class TodoItem
    {
        public string Text;
        public bool Done;
    }

    internal sealed class TodoForm : Form
    {
        private readonly string dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexDesktopTODO");
        private readonly List<TodoItem> todos = new List<TodoItem>();
        private readonly FlowLayoutPanel listPanel = new FlowLayoutPanel();
        private readonly TextBox input = new TextBox();
        private readonly Label countLabel = new Label();
        private readonly Label completedLabel = new Label();
        private readonly Label saveLabel = new Label();
        private readonly HeaderIconButton lockButton = new HeaderIconButton(HeaderIcon.Lock);
        private readonly HeaderIconButton pinButton = new HeaderIconButton(HeaderIcon.Pin);
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ToolTip toolTip = new ToolTip();
        private Panel headerPanel;
        private Panel headerTools;
        private Panel composerPanel;
        private bool locked;
        private bool allowExit;
        private bool loading = true;

        public TodoForm()
        {
            Text = "Codex Desktop TODO";
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(280, 360);
            Size = new Size(360, 480);
            BackColor = Color.FromArgb(246, 249, 241);
            TopMost = true;
            DoubleBuffered = true;

            LoadState();
            BuildUi();
            RenderTodos();
            UpdateChrome();
            Shown += delegate { input.Focus(); };
            loading = false;

            Move += delegate { SaveStateSoon(); };
            Resize += delegate { SaveStateSoon(); };
            FormClosing += OnFormClosing;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen border = new Pen(Color.FromArgb(186, 202, 176)))
            {
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }
        }

        private void BuildUi()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.BackColor = BackColor;
            Controls.Add(layout);

            Panel header = new Panel();
            headerPanel = header;
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(239, 246, 232);
            header.MouseDown += BeginDrag;
            layout.Controls.Add(header, 0, 0);

            Label dot = new Label();
            dot.Text = "●";
            dot.ForeColor = Color.FromArgb(102, 151, 87);
            dot.AutoSize = true;
            dot.Location = new Point(11, 13);
            dot.MouseDown += BeginDrag;
            header.Controls.Add(dot);

            Label badge = new Label();
            badge.Text = "TODO";
            badge.ForeColor = Color.White;
            badge.BackColor = Color.FromArgb(31, 38, 28);
            badge.Font = new Font(Font, FontStyle.Bold);
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.Location = new Point(31, 8);
            badge.Size = new Size(58, 25);
            badge.MouseDown += BeginDrag;
            header.Controls.Add(badge);

            Label title = new Label();
            title.Text = "今日清单";
            title.ForeColor = Color.FromArgb(37, 45, 34);
            title.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(98, 10);
            title.MouseDown += BeginDrag;
            header.Controls.Add(title);

            countLabel.AutoSize = true;
            countLabel.ForeColor = Color.FromArgb(88, 103, 78);
            countLabel.Location = new Point(174, 13);
            countLabel.MouseDown += BeginDrag;
            header.Controls.Add(countLabel);

            headerTools = new Panel();
            headerTools.Size = new Size(136, 30);
            headerTools.BackColor = header.BackColor;
            headerTools.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(headerTools);

            HeaderIconButton closeButton = HeaderButton(HeaderIcon.Close, "退出");
            closeButton.Location = new Point(108, 1);
            closeButton.Click += delegate { allowExit = true; Close(); };
            headerTools.Controls.Add(closeButton);

            HeaderIconButton hideButton = HeaderButton(HeaderIcon.Tray, "隐藏到托盘");
            hideButton.Location = new Point(72, 1);
            hideButton.Click += delegate { Hide(); };
            headerTools.Controls.Add(hideButton);

            pinButton.Location = new Point(36, 1);
            StyleHeaderButton(pinButton, "置顶");
            pinButton.Click += delegate
            {
                TopMost = !TopMost;
                UpdateChrome();
                SaveStateSoon();
            };
            headerTools.Controls.Add(pinButton);

            lockButton.Location = new Point(0, 1);
            StyleHeaderButton(lockButton, "锁定位置");
            lockButton.Click += delegate
            {
                locked = !locked;
                UpdateChrome();
                SaveStateSoon();
            };
            headerTools.Controls.Add(lockButton);
            headerTools.BringToFront();
            header.Resize += delegate { LayoutHeader(); };
            LayoutHeader();

            Panel composer = new Panel();
            composerPanel = composer;
            composer.Dock = DockStyle.Fill;
            composer.Padding = new Padding(12, 8, 12, 8);
            composer.BackColor = BackColor;
            composer.Paint += PaintComposer;
            layout.Controls.Add(composer, 0, 1);

            Button addButton = new Button();
            addButton.Text = "+";
            addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            addButton.Location = new Point(Width - 51, 11);
            addButton.Size = new Size(32, 32);
            addButton.FlatStyle = FlatStyle.Flat;
            addButton.FlatAppearance.BorderSize = 0;
            addButton.BackColor = Color.FromArgb(57, 106, 64);
            addButton.ForeColor = Color.White;
            addButton.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold);
            addButton.Click += delegate { AddTodo(); };
            composer.Controls.Add(addButton);

            input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            input.Location = new Point(22, 17);
            input.Size = new Size(Width - 91, 22);
            input.BorderStyle = BorderStyle.None;
            input.BackColor = Color.White;
            input.ForeColor = Color.FromArgb(42, 48, 39);
            input.Font = new Font(Font.FontFamily, 11F, FontStyle.Regular);
            input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddTodo();
                }
            };
            composer.Controls.Add(input);
            composer.Resize += delegate
            {
                addButton.Left = composer.ClientSize.Width - 49;
                input.Width = Math.Max(80, composer.ClientSize.Width - 91);
                composer.Invalidate();
            };

            listPanel.Dock = DockStyle.Fill;
            listPanel.FlowDirection = FlowDirection.TopDown;
            listPanel.WrapContents = false;
            listPanel.AutoScroll = true;
            listPanel.Padding = new Padding(12, 2, 12, 4);
            listPanel.BackColor = Color.FromArgb(246, 249, 241);
            listPanel.Resize += delegate { RenderTodos(); };
            layout.Controls.Add(listPanel, 0, 2);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Fill;
            footer.BackColor = Color.FromArgb(246, 249, 241);
            layout.Controls.Add(footer, 0, 3);

            completedLabel.AutoSize = true;
            completedLabel.ForeColor = Color.FromArgb(92, 105, 84);
            completedLabel.Location = new Point(14, 8);
            footer.Controls.Add(completedLabel);

            saveLabel.AutoSize = true;
            saveLabel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            saveLabel.ForeColor = Color.FromArgb(92, 105, 84);
            saveLabel.Location = new Point(Width - 62, 8);
            footer.Controls.Add(saveLabel);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示 TODO", null, delegate { Show(); Activate(); });
            menu.Items.Add("隐藏到托盘", null, delegate { Hide(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { allowExit = true; Close(); });
            tray.Icon = SystemIcons.Application;
            tray.Text = "Codex Desktop TODO";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Show(); Activate(); };
        }

        private HeaderIconButton HeaderButton(HeaderIcon icon, string tip)
        {
            HeaderIconButton button = new HeaderIconButton(icon);
            StyleHeaderButton(button, tip);
            return button;
        }

        private void StyleHeaderButton(HeaderIconButton button, string tip)
        {
            button.Size = new Size(28, 28);
            button.BackColor = headerPanel == null ? Color.FromArgb(239, 246, 232) : headerPanel.BackColor;
            button.ForeColor = Color.FromArgb(43, 52, 39);
            toolTip.SetToolTip(button, tip);
        }

        private void LayoutHeader()
        {
            if (headerPanel == null || headerTools == null)
            {
                return;
            }

            headerTools.Left = Math.Max(0, headerPanel.ClientSize.Width - headerTools.Width - 8);
            headerTools.Top = 6;
            countLabel.Visible = countLabel.Right <= headerTools.Left - 8;
        }

        private void PaintComposer(object sender, PaintEventArgs e)
        {
            Rectangle box = new Rectangle(14, 10, composerPanel.ClientSize.Width - 66, 34);
            using (GraphicsPath path = RoundedRect(box, 6))
            using (SolidBrush brush = new SolidBrush(Color.White))
            using (Pen pen = new Pen(Color.FromArgb(216, 225, 207)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (locked || e.Button != MouseButtons.Left)
            {
                return;
            }

            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0xA1, new IntPtr(0x2), IntPtr.Zero);
        }

        private void AddTodo()
        {
            string text = input.Text.Trim();
            if (text.Length == 0)
            {
                return;
            }

            todos.Insert(0, new TodoItem { Text = text, Done = false });
            input.Clear();
            RenderTodos();
            SaveStateSoon();
        }

        private void RenderTodos()
        {
            listPanel.SuspendLayout();
            listPanel.Controls.Clear();

            foreach (TodoItem todo in todos)
            {
                listPanel.Controls.Add(CreateTodoRow(todo));
            }

            listPanel.ResumeLayout();
            UpdateChrome();
        }

        private Control CreateTodoRow(TodoItem todo)
        {
            TodoRowPanel row = new TodoRowPanel(todo, Font);
            row.Width = Math.Max(240, listPanel.ClientSize.Width - 28);
            row.Height = 42;
            row.Margin = new Padding(0, 0, 0, 7);
            row.BackColor = Color.Transparent;
            row.ToggleRequested += delegate
            {
                todo.Done = !todo.Done;
                SaveStateSoon();
                RenderTodos();
            };
            row.DeleteRequested += delegate
            {
                todos.Remove(todo);
                RenderTodos();
                SaveStateSoon();
            };
            row.EditRequested += delegate { EditTodo(todo); };

            return row;
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void EditTodo(TodoItem todo)
        {
            using (EditDialog dialog = new EditDialog(todo.Text))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string text = dialog.Value.Trim();
                    if (text.Length > 0)
                    {
                        todo.Text = text;
                        RenderTodos();
                        SaveStateSoon();
                    }
                }
            }
        }

        private void UpdateChrome()
        {
            countLabel.Text = todos.Count + " 项";
            int completed = 0;
            foreach (TodoItem todo in todos)
            {
                if (todo.Done)
                {
                    completed++;
                }
            }

            completedLabel.Text = completed + " 已完成";
            saveLabel.Text = "已保存";
            lockButton.Active = locked;
            pinButton.Active = TopMost;
            LayoutHeader();
        }

        private void SaveStateSoon()
        {
            if (loading)
            {
                return;
            }

            saveLabel.Text = "保存中";
            SaveState();
            saveLabel.Text = "已保存";
        }

        private void LoadState()
        {
            Directory.CreateDirectory(dataDir);
            string settings = Path.Combine(dataDir, "settings.txt");
            string tasks = Path.Combine(dataDir, "todos.txt");

            if (File.Exists(settings))
            {
                foreach (string line in File.ReadAllLines(settings, Encoding.UTF8))
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    int number;
                    if (parts[0] == "x" && int.TryParse(parts[1], out number)) Left = number;
                    if (parts[0] == "y" && int.TryParse(parts[1], out number)) Top = number;
                    if (parts[0] == "w" && int.TryParse(parts[1], out number)) Width = Math.Max(280, number);
                    if (parts[0] == "h" && int.TryParse(parts[1], out number)) Height = Math.Max(360, number);
                    if (parts[0] == "topmost") TopMost = parts[1] == "1";
                    if (parts[0] == "locked") locked = parts[1] == "1";
                }
            }
            else
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                Left = work.Right - Width - 32;
                Top = work.Top + 80;
            }

            EnsureVisible();

            if (File.Exists(tasks))
            {
                foreach (string line in File.ReadAllLines(tasks, Encoding.UTF8))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    try
                    {
                        string text = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                        if (text.Trim().Length > 0)
                        {
                            todos.Add(new TodoItem { Done = parts[0] == "1", Text = text });
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void EnsureVisible()
        {
            Rectangle work = Screen.FromRectangle(Bounds).WorkingArea;
            Width = Math.Min(Math.Max(Width, 280), work.Width);
            Height = Math.Min(Math.Max(Height, 360), work.Height);
            Left = Math.Max(work.Left, Math.Min(Left, work.Right - Width));
            Top = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height));
        }

        private void SaveState()
        {
            Directory.CreateDirectory(dataDir);

            List<string> settings = new List<string>();
            settings.Add("x=" + Left);
            settings.Add("y=" + Top);
            settings.Add("w=" + Width);
            settings.Add("h=" + Height);
            settings.Add("topmost=" + (TopMost ? "1" : "0"));
            settings.Add("locked=" + (locked ? "1" : "0"));
            File.WriteAllLines(Path.Combine(dataDir, "settings.txt"), settings.ToArray(), Encoding.UTF8);

            List<string> lines = new List<string>();
            foreach (TodoItem todo in todos)
            {
                string text = Convert.ToBase64String(Encoding.UTF8.GetBytes(todo.Text));
                lines.Add((todo.Done ? "1" : "0") + "|" + text);
            }

            File.WriteAllLines(Path.Combine(dataDir, "todos.txt"), lines.ToArray(), Encoding.UTF8);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            SaveState();
            if (!allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            tray.Visible = false;
            tray.Dispose();
        }
    }

    internal enum HeaderIcon
    {
        Lock,
        Pin,
        Tray,
        Close
    }

    internal sealed class HeaderIconButton : Control
    {
        private bool active;
        private bool hovered;
        private bool pressed;

        public HeaderIconButton(HeaderIcon icon)
        {
            Icon = icon;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public HeaderIcon Icon { get; private set; }

        public bool Active
        {
            get { return active; }
            set
            {
                active = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressed)
            {
                pressed = false;
                Invalidate();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(2, 2, Width - 5, Height - 5);
            Color foreground = Active ? Color.White : Color.FromArgb(43, 52, 39);
            Color background = Parent == null ? Color.FromArgb(239, 246, 232) : Parent.BackColor;

            e.Graphics.Clear(background);

            if (Active || hovered || pressed)
            {
                Color fill = Active
                    ? Color.FromArgb(31, 38, 28)
                    : (pressed ? Color.FromArgb(213, 225, 204) : Color.FromArgb(226, 237, 218));
                using (GraphicsPath path = RoundedRect(rect, 5))
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }

            using (Pen pen = new Pen(foreground, 1.6F))
            using (SolidBrush brush = new SolidBrush(foreground))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                e.Graphics.TranslateTransform((Width - 24) / 2F, (Height - 24) / 2F);

                if (Icon == HeaderIcon.Lock)
                {
                    e.Graphics.DrawRectangle(pen, 7, 11, 10, 7);
                    e.Graphics.DrawArc(pen, 8, 5, 8, 10, 190, 160);
                    e.Graphics.FillEllipse(brush, 11, 14, 2, 2);
                }
                else if (Icon == HeaderIcon.Pin)
                {
                    Point[] pin = new Point[]
                    {
                        new Point(10, 5),
                        new Point(16, 11),
                        new Point(13, 14),
                        new Point(16, 17),
                        new Point(15, 18),
                        new Point(12, 15),
                        new Point(7, 20),
                        new Point(6, 19),
                        new Point(11, 14),
                        new Point(8, 11)
                    };
                    e.Graphics.DrawLines(pen, pin);
                }
                else if (Icon == HeaderIcon.Tray)
                {
                    e.Graphics.DrawRectangle(pen, 6, 6, 12, 9);
                    e.Graphics.DrawLine(pen, 10, 18, 14, 18);
                    e.Graphics.DrawLine(pen, 12, 15, 12, 18);
                }
                else if (Icon == HeaderIcon.Close)
                {
                    e.Graphics.DrawLine(pen, 8, 8, 16, 16);
                    e.Graphics.DrawLine(pen, 16, 8, 8, 16);
                }

                e.Graphics.ResetTransform();
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class TodoRowPanel : Panel
    {
        private const int CheckHitWidth = 46;
        private const int DeleteHitWidth = 50;
        private readonly TodoItem todo;
        private readonly Font textFont;
        private readonly Font doneFont;
        private bool hoverCheckbox;
        private bool hoverDelete;
        private bool pressedCheckbox;
        private bool pressedDelete;

        public TodoRowPanel(TodoItem todo, Font baseFont)
        {
            this.todo = todo;
            textFont = new Font(baseFont.FontFamily, 10F, FontStyle.Bold);
            doneFont = new Font(baseFont.FontFamily, 10F, FontStyle.Bold | FontStyle.Strikeout);
            DoubleBuffered = true;
            Cursor = Cursors.Default;
        }

        public event EventHandler ToggleRequested;
        public event EventHandler DeleteRequested;
        public event EventHandler EditRequested;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = TodoFormRoundedRect(rect, 7))
            using (SolidBrush brush = new SolidBrush(Color.White))
            using (Pen pen = new Pen(Color.FromArgb(218, 226, 210)))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            DrawCheckbox(e.Graphics);
            DrawText(e.Graphics);
            DrawDelete(e.Graphics);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool nextHoverCheckbox = IsInCheckboxZone(e.Location);
            bool nextHoverDelete = IsInDeleteZone(e.Location);

            if (nextHoverCheckbox != hoverCheckbox || nextHoverDelete != hoverDelete)
            {
                hoverCheckbox = nextHoverCheckbox;
                hoverDelete = nextHoverDelete;
                Cursor = hoverCheckbox || hoverDelete ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverCheckbox = false;
            hoverDelete = false;
            pressedCheckbox = false;
            pressedDelete = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                pressedCheckbox = IsInCheckboxZone(e.Location);
                pressedDelete = IsInDeleteZone(e.Location);
                if (pressedCheckbox || pressedDelete)
                {
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            bool toggle = pressedCheckbox && IsInCheckboxZone(e.Location);
            bool delete = pressedDelete && IsInDeleteZone(e.Location);
            pressedCheckbox = false;
            pressedDelete = false;
            Invalidate();

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (toggle && ToggleRequested != null)
            {
                ToggleRequested(this, EventArgs.Empty);
            }
            else if (delete && DeleteRequested != null)
            {
                DeleteRequested(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (e.X >= CheckHitWidth && e.X < Width - DeleteHitWidth && EditRequested != null)
            {
                EditRequested(this, EventArgs.Empty);
            }
        }

        private void DrawCheckbox(Graphics graphics)
        {
            Rectangle hot = new Rectangle(6, 6, 32, Height - 12);
            if (hoverCheckbox || pressedCheckbox)
            {
                Color hotFill = pressedCheckbox ? Color.FromArgb(226, 237, 218) : Color.FromArgb(241, 247, 236);
                using (GraphicsPath hotPath = TodoFormRoundedRect(hot, 6))
                using (SolidBrush hotBrush = new SolidBrush(hotFill))
                {
                    graphics.FillPath(hotBrush, hotPath);
                }
            }

            Rectangle box = new Rectangle(13, 11, 20, 20);
            Color border = todo.Done ? Color.FromArgb(57, 106, 64) : Color.FromArgb(153, 173, 144);
            Color fill = todo.Done ? Color.FromArgb(57, 106, 64) : Color.White;

            using (GraphicsPath path = TodoFormRoundedRect(box, 5))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, todo.Done ? 1.4F : 1.15F))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }

            if (todo.Done)
            {
                using (Pen checkPen = new Pen(Color.White, 2.1F))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    graphics.DrawLines(checkPen, new Point[]
                    {
                        new Point(box.Left + 5, box.Top + 11),
                        new Point(box.Left + 9, box.Top + 14),
                        new Point(box.Left + 15, box.Top + 6)
                    });
                }
            }
        }

        private void DrawText(Graphics graphics)
        {
            Rectangle textRect = new Rectangle(CheckHitWidth, 10, Math.Max(20, Width - CheckHitWidth - DeleteHitWidth - 4), 22);
            Color textColor = todo.Done ? Color.FromArgb(120, 132, 112) : Color.FromArgb(28, 34, 25);
            TextRenderer.DrawText(
                graphics,
                todo.Text,
                todo.Done ? doneFont : textFont,
                textRect,
                textColor,
                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            );
        }

        private void DrawDelete(Graphics graphics)
        {
            Rectangle hot = new Rectangle(Width - DeleteHitWidth + 7, 6, DeleteHitWidth - 14, Height - 12);
            Color foreground = hoverDelete || pressedDelete ? Color.FromArgb(130, 58, 47) : Color.FromArgb(110, 123, 101);

            if (hoverDelete || pressedDelete)
            {
                Color hotFill = pressedDelete ? Color.FromArgb(244, 222, 217) : Color.FromArgb(251, 238, 235);
                using (GraphicsPath hotPath = TodoFormRoundedRect(hot, 6))
                using (SolidBrush hotBrush = new SolidBrush(hotFill))
                {
                    graphics.FillPath(hotBrush, hotPath);
                }
            }

            int centerX = Width - 25;
            int centerY = Height / 2;
            using (Pen pen = new Pen(foreground, 1.9F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(pen, centerX - 5, centerY - 5, centerX + 5, centerY + 5);
                graphics.DrawLine(pen, centerX + 5, centerY - 5, centerX - 5, centerY + 5);
            }
        }

        private bool IsInCheckboxZone(Point point)
        {
            return point.X >= 0 && point.X < CheckHitWidth && point.Y >= 0 && point.Y < Height;
        }

        private bool IsInDeleteZone(Point point)
        {
            return point.X >= Width - DeleteHitWidth && point.X < Width && point.Y >= 0 && point.Y < Height;
        }

        private static GraphicsPath TodoFormRoundedRect(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class EditDialog : Form
    {
        private readonly TextBox textBox = new TextBox();
        public string Value { get { return textBox.Text; } }

        public EditDialog(string value)
        {
            Text = "编辑 TODO";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 92);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9F);

            textBox.Text = value;
            textBox.Location = new Point(12, 14);
            textBox.Size = new Size(336, 24);
            Controls.Add(textBox);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(190, 52);
            ok.Size = new Size(75, 26);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(273, 52);
            cancel.Size = new Size(75, 26);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
