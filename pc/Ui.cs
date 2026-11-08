using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
namespace Miaohua
{
    // ---------------------------------------------------------------- window chrome

    static class Dwm
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// Recolours the real Windows title bar instead of faking a borderless frame, so
        /// rounded corners, snap layouts, resizing and the drop shadow all survive.
        /// These attributes need Windows 11; on older builds the calls just fail and the
        /// stock title bar stays, which is harmless.
        public static void StyleTitleBar(IntPtr hwnd)
        {
            try
            {
                int caption = Theme.CaptionColor;
                DwmSetWindowAttribute(hwnd, 35, ref caption, 4);   // DWMWA_CAPTION_COLOR
                int text = Theme.CaptionText;
                DwmSetWindowAttribute(hwnd, 36, ref text, 4);      // DWMWA_TEXT_COLOR
                int round = 2;                                     // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33, ref round, 4);     // CORNER_PREFERENCE
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------- artwork

    /// Artwork resolution order, so the shipped drawing can be swapped out without
    /// recompiling:
    ///   logo.ico  next to the exe  -> window icon and tray icon
    ///   logo.png  next to the exe  -> window header picture
    ///   otherwise the icon embedded in the exe at build time (/win32icon)
    static class Art
    {
        static string BaseDir
        {
            get { return Path.GetDirectoryName(Application.ExecutablePath); }
        }

        /// ★ 2026-10-18：拿 logo.png（对话框头像和页脚插图用）✓
        public static Image LogoImage()
        {
            try
            {
                string png = Path.Combine(BaseDir, "logo.png");
                if (File.Exists(png))
                {
                    byte[] raw = File.ReadAllBytes(png);
                    using (MemoryStream ms = new MemoryStream(raw))
                        return Image.FromStream(ms);
                }
            }
            catch { }
            return null;
        }

        public static Icon AppIcon()
        {
            try
            {
                string ico = Path.Combine(BaseDir, "logo.ico");
                if (File.Exists(ico)) return new Icon(ico);
            }
            catch { }
            try
            {
                Icon e = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (e != null) return e;
            }
            catch { }
            return SystemIcons.Application;
        }

        public static Bitmap Logo(int size)
        {
            try
            {
                string png = Path.Combine(BaseDir, "logo.png");
                if (File.Exists(png))
                {
                    using (Image src = Image.FromFile(png))
                        return new Bitmap(src, new Size(size, size));
                }
            }
            catch { }
            try
            {
                return new Icon(AppIcon(), new Size(size, size)).ToBitmap();
            }
            catch { }
            return null;
        }
    }

    // ---------------------------------------------------------------- theme

    static class Theme
    {
        public static readonly Color Bg          = Color.FromArgb(255, 246, 249);
        public static readonly Color Card        = Color.White;
        public static Color Accent      = Color.FromArgb(240, 98, 146);
        public static Color AccentHover = Color.FromArgb(247, 126, 168);
        public static Color AccentDown  = Color.FromArgb(212, 68, 118);
        public static readonly Color Text        = Color.FromArgb(58, 42, 52);
        public static readonly Color Muted       = Color.FromArgb(150, 122, 134);
        public static readonly Color Line        = Color.FromArgb(244, 222, 232);
        public static Color Soft        = Color.FromArgb(255, 237, 244);
        public static Color SoftHover   = Color.FromArgb(255, 245, 250);
        public static readonly Color Ok          = Color.FromArgb(46, 125, 50);
        public static readonly Color Warn        = Color.FromArgb(198, 40, 40);

        /// ⑫ 颜色分层：把主色调的饱和度降下来，拉开"主色 / 次色 / 底色"的层级。
        ///
        /// 做成运行时可关的，是因为配色好不好看只能靠眼睛判断，而且这类改动一旦铺开就很难
        /// 逐处回退 —— 开关关掉就是原来那套，风险可控。
        public static bool Levels = false;

        public static void ApplyLevels(bool on)
        {
            Levels = on;
            if (on)
            {
                Accent      = Color.FromArgb(222, 124, 158);   // 原 (240, 98, 146)
                AccentHover = Color.FromArgb(232, 148, 178);
                AccentDown  = Color.FromArgb(198, 104, 138);
                Soft        = Color.FromArgb(252, 240, 246);
                SoftHover   = Color.FromArgb(254, 247, 251);
            }
            else
            {
                Accent      = Color.FromArgb(240, 98, 146);
                AccentHover = Color.FromArgb(247, 126, 168);
                AccentDown  = Color.FromArgb(212, 68, 118);
                Soft        = Color.FromArgb(255, 237, 244);
                SoftHover   = Color.FromArgb(255, 245, 250);
            }
        }
        // soft drop shadow under the cards, and the "off" state of the toggles
        public static readonly Color Shadow     = Color.FromArgb(13, 96, 62, 84);
        public static readonly Color TrackOff   = Color.FromArgb(219, 206, 213);
        public static readonly Color TrackHover = Color.FromArgb(203, 187, 196);

        // Title bar colours, handed to DWM. Doing it this way keeps the real window
        // frame, so rounded corners, snap layouts, resizing and the drop shadow all
        // still work - a fake borderless title bar would cost all of that.
        public const int CaptionColor = 0xFFEFF7;   // DWM wants 0x00BBGGRR
        public const int CaptionText  = 0x342A3A;

        public static readonly Font Body   = new Font("Microsoft YaHei UI", 9.75f);
        public static readonly Font Small  = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font Tiny   = new Font("Microsoft YaHei UI", 8.25f);
        public static readonly Font Title  = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
        public static readonly Font Hero   = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
        public static readonly Font Head   = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        public static readonly Font Mono   = new Font("Consolas", 10f);
        public static readonly Font Face   = new Font("Microsoft YaHei UI", 14f);
    }

    /// Hand-drawn vector icons.
    ///
    /// Replaces the emoji experiment, which is a dead end on this stack: System.Drawing
    /// cannot draw colour emoji by any path - TextRenderer, Graphics.DrawString, bitmaps and
    /// UseCompatibleTextRendering all produced flat monochrome outlines (measured, see
    /// emoji-art-test.png). Colour emoji need DirectWrite.
    ///
    /// These are drawn with GDI+ paths instead. That is better here anyway: the colour is the
    /// app's own palette rather than the system's fixed emoji colours, they look the same on
    /// every machine and font set, and they scale cleanly.
    /// An alternative icon set that was drawn by hand and NOT chosen: four bright colours
    /// competed with the pink theme. Kept because swapping back is then a one-line change.
    static class VecIcon
    {
        static readonly System.Collections.Generic.Dictionary<string, Image> cache =
            new System.Collections.Generic.Dictionary<string, Image>();

        /// 给 --icons 用的公开入口
        public static Image VecIconPublic(string name, Color c, int px) { return ByName(name, c, px); }

        public static Image ByName(string name, Color c, int px)
        {
            string key = name + "|" + c.ToArgb() + "|" + px;
            if (cache.ContainsKey(key)) return cache[key];
            Image img = Draw(name, c, px);
            cache[key] = img;
            return img;
        }

        static Image Draw(string name, Color c, int px)
        {
            if (px < 8) px = 8;
            Bitmap b = new Bitmap(px, px);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float s = px;
                using (SolidBrush br = new SolidBrush(c))
                using (Pen pen = new Pen(c, Math.Max(1.4f, s * 0.075f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;

                    if (name == "paw")
                    {
                        g.FillEllipse(br, s * 0.20f, s * 0.44f, s * 0.60f, s * 0.46f);
                        g.FillEllipse(br, s * 0.02f, s * 0.20f, s * 0.30f, s * 0.34f);
                        g.FillEllipse(br, s * 0.35f, s * 0.06f, s * 0.30f, s * 0.34f);
                        g.FillEllipse(br, s * 0.68f, s * 0.20f, s * 0.30f, s * 0.34f);
                    }
                    else if (name == "sparkle")
                    {
                        PointF[] big = {
                            new PointF(s * 0.34f, s * 0.04f), new PointF(s * 0.46f, s * 0.34f),
                            new PointF(s * 0.76f, s * 0.46f), new PointF(s * 0.46f, s * 0.58f),
                            new PointF(s * 0.34f, s * 0.88f), new PointF(s * 0.22f, s * 0.58f),
                            new PointF(-s * 0.08f + s * 0.08f, s * 0.46f), new PointF(s * 0.22f, s * 0.34f)
                        };
                        big[6] = new PointF(s * 0.04f, s * 0.46f);
                        g.FillPolygon(br, big);
                        PointF[] small = {
                            new PointF(s * 0.78f, s * 0.62f), new PointF(s * 0.84f, s * 0.76f),
                            new PointF(s * 0.98f, s * 0.82f), new PointF(s * 0.84f, s * 0.88f),
                            new PointF(s * 0.78f, s * 1.00f), new PointF(s * 0.72f, s * 0.88f),
                            new PointF(s * 0.58f, s * 0.82f), new PointF(s * 0.72f, s * 0.76f)
                        };
                        g.FillPolygon(br, small);
                    }
                    else if (name == "cat")
                    {
                        PointF[] earL = { new PointF(s*0.26f, s*0.34f), new PointF(s*0.30f, s*0.04f), new PointF(s*0.50f, s*0.26f) };
                        PointF[] earR = { new PointF(s*0.74f, s*0.34f), new PointF(s*0.70f, s*0.04f), new PointF(s*0.50f, s*0.26f) };
                        g.FillPolygon(br, earL);
                        g.FillPolygon(br, earR);
                        g.FillEllipse(br, s * 0.14f, s * 0.24f, s * 0.72f, s * 0.62f);
                        using (SolidBrush eye = new SolidBrush(Color.White))
                        {
                            g.FillEllipse(eye, s * 0.32f, s * 0.44f, s * 0.11f, s * 0.16f);
                            g.FillEllipse(eye, s * 0.57f, s * 0.44f, s * 0.11f, s * 0.16f);
                        }
                    }
                    else if (name == "dot")
                    {
                        g.FillEllipse(br, s * 0.10f, s * 0.10f, s * 0.80f, s * 0.80f);
                    }
                    else if (name == "warn")
                    {
                        PointF[] tri = { new PointF(s*0.50f, s*0.06f), new PointF(s*0.98f, s*0.90f), new PointF(s*0.02f, s*0.90f) };
                        using (Pen p2 = new Pen(c, Math.Max(1.8f, s * 0.09f)))
                        {
                            p2.LineJoin = LineJoin.Round;
                            g.DrawPolygon(p2, tri);
                        }
                        g.FillRectangle(br, s * 0.45f, s * 0.34f, s * 0.10f, s * 0.28f);
                        g.FillEllipse(br, s * 0.43f, s * 0.70f, s * 0.14f, s * 0.14f);
                    }
                    else if (name == "moon")
                    {
                        g.FillEllipse(br, s * 0.10f, s * 0.10f, s * 0.80f, s * 0.80f);
                        using (SolidBrush cut = new SolidBrush(Color.White))
                            g.FillEllipse(cut, s * 0.42f, s * 0.02f, s * 0.78f, s * 0.78f);
                    }
                    else
                    {
                        g.FillEllipse(br, s * 0.18f, s * 0.18f, s * 0.64f, s * 0.64f);
                    }
                }
            }
            return b;
        }
    }
/// ★★★ 2026-10-18：托盘的粉色菜单渲染器 ★★★
///
/// ColorTable 负责配色 ✗ Renderer 负责画 hover / 分隔线 ✓
class PinkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected { get { return Mix(Theme.Card, Theme.Accent, 0.16); } }
    public override Color MenuItemSelectedGradientBegin { get { return Mix(Theme.Card, Theme.Accent, 0.16); } }
    public override Color MenuItemSelectedGradientEnd { get { return Mix(Theme.Card, Theme.Accent, 0.16); } }
    public override Color MenuItemBorder { get { return Mix(Theme.Card, Theme.Accent, 0.30); } }
    public override Color MenuBorder { get { return Color.FromArgb(212, 134, 170); } }
    public override Color ToolStripDropDownBackground { get { return Color.FromArgb(255, 224, 238); } }
    public override Color ImageMarginGradientBegin { get { return Color.FromArgb(255, 224, 238); } }
    public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(255, 224, 238); } }
    public override Color ImageMarginGradientEnd { get { return Color.FromArgb(255, 224, 238); } }
    public override Color SeparatorDark { get { return Color.FromArgb(228, 150, 184); } }
    public override Color SeparatorLight { get { return Color.FromArgb(255, 224, 238); } }

    static Color Mix(Color a, Color b, double t)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}

class PinkMenuRenderer : ToolStripProfessionalRenderer
{
            public PinkMenuRenderer() : base(new PinkColorTable())
            {
                App.LogStatic("tray: pink renderer constructed");
            }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        Rectangle r = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
        using (GraphicsPath gp = Rounded.Path(r, 6))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 202, 226)))   // ★ 明显的淡粉 ✓
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(b, gp);
        }
    }

    static Color Mix(Color a, Color b, double t)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}

    /// ★★★ 2026-10-18：粉色圆角输入框 ★★★
    ///
    /// 系统 TextBox 是灰边框直角 ✗ 和粉色界面不搭 ✓
    /// 做法：外面套一个自绘圆角 Panel ✗ 里面的 TextBox 去掉边框 ✓
    class PinkInput : Panel
    {
        public TextBox Box;

        public PinkInput(Size size, bool multiline)
        {
            Size = size;
            BackColor = Color.White;
            DoubleBuffered = true;

            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Color.White;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.Body;
            Box.Multiline = multiline;
            int left = 10;
            int top = multiline ? 8 : (size.Height - 22) / 2;
            Box.Location = new Point(left, top);
            Box.Size = new Size(size.Width - left * 2, multiline ? size.Height - 16 : 24);
            if (multiline) Box.ScrollBars = ScrollBars.Vertical;
            Controls.Add(Box);

            // ★ 点这个框就等于点里面的输入框 ✓
            Click += delegate { Box.Focus(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (System.Drawing.Drawing2D.GraphicsPath gp =
                   Rounded.Path(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            {
                using (SolidBrush b = new SolidBrush(Color.White))
                    e.Graphics.FillPath(b, gp);
                using (Pen p = new Pen(Color.FromArgb(243, 214, 228), 1))
                    e.Graphics.DrawPath(p, gp);
            }
        }
    }

    /// ★★★ 2026-10-18：托盘的粉色卡片（参考 GPT 设计稿）★★★
    ///
    /// 为什么不用 ContextMenuStrip：
    ///   它只能换配色 ✗ 做不出头像 / 圆形开关 / 页脚插图 ✓
    ///   —— 所以干脆做成一个【无边框的小窗口】✗ 自己接管“点外面就关” ✓
    class TrayPanel : Form
    {
        public const int W = 300;

        Panel body;
        int cursorY;
        Label stateLabel;
        Action onClosedExternally;

        public TrayPanel(string appName, string runningText)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Theme.Soft;
            Font = Theme.Body;
            DoubleBuffered = true;
            Padding = new Padding(0);
            TopMost = true;

            // ---- 头部 ----
            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 62;
            head.BackColor = Theme.Soft;
            head.Tag = Lang.NoTranslate;
            Controls.Add(head);

            PictureBox avatar = new PictureBox();
            avatar.Size = new Size(40, 40);
            avatar.Location = new Point(14, 11);
            avatar.SizeMode = PictureBoxSizeMode.Zoom;
            avatar.BackColor = Color.Transparent;
            try { avatar.Image = Art.LogoImage(); } catch { }
            avatar.Paint += delegate(object s1, PaintEventArgs e)
            {
                using (System.Drawing.Drawing2D.GraphicsPath gp =
                       new System.Drawing.Drawing2D.GraphicsPath())
                {
                    gp.AddEllipse(0, 0, avatar.Width - 1, avatar.Height - 1);
                    avatar.Region = new Region(gp);
                }
            };
            head.Controls.Add(avatar);

            Label nm = new Label();
            nm.Text = appName;
            nm.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            nm.ForeColor = Theme.Text;
            nm.BackColor = Color.Transparent;
            nm.AutoSize = false;
            nm.TextAlign = ContentAlignment.BottomLeft;
            nm.Location = new Point(64, 12);
            nm.Size = new Size(W - 110, 24);
            nm.Tag = Lang.NoTranslate;
            head.Controls.Add(nm);

            stateLabel = new Label();
            stateLabel.Text = "● " + runningText;
            stateLabel.Font = new Font("Microsoft YaHei UI", 8.5f);
            stateLabel.ForeColor = Theme.Ok;
            stateLabel.BackColor = Color.Transparent;
            stateLabel.AutoSize = false;
            stateLabel.TextAlign = ContentAlignment.TopLeft;
            stateLabel.Location = new Point(66, 36);
            stateLabel.Size = new Size(W - 110, 18);
            stateLabel.Tag = Lang.NoTranslate;
            head.Controls.Add(stateLabel);

            Label close = new Label();
            close.Text = "✕";
            close.Font = new Font("Microsoft YaHei UI", 11f);
            close.ForeColor = Theme.Muted;
            close.BackColor = Color.Transparent;
            close.AutoSize = false;
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.Size = new Size(30, 28);
            close.Location = new Point(W - 38, 10);
            close.Cursor = Cursors.Hand;
            close.Tag = Lang.NoTranslate;
            close.Click += delegate { Close(); };
            head.Controls.Add(close);

            // ---- 页脚 ----
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 48;
            foot.BackColor = Theme.Soft;
            foot.Tag = Lang.NoTranslate;
            Controls.Add(foot);

            PictureBox footPic = new PictureBox();
            footPic.Size = new Size(40, 40);
            footPic.Location = new Point(12, 4);
            footPic.SizeMode = PictureBoxSizeMode.Zoom;
            footPic.BackColor = Color.Transparent;
            try { footPic.Image = Art.LogoImage(); } catch { }
            foot.Controls.Add(footPic);

            Label footText = new Label();
            footText.Text = "喵化，让聊天更有趣！";
            footText.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Italic);
            footText.ForeColor = Theme.Accent;
            footText.BackColor = Color.Transparent;
            footText.AutoSize = false;
            footText.TextAlign = ContentAlignment.MiddleLeft;
            footText.Location = new Point(58, 10);
            footText.Size = new Size(W - 70, 26);
            footText.Tag = Lang.NoTranslate;
            foot.Controls.Add(footText);

            // ---- 中间列表 ----
            body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Color.White;
            body.AutoScroll = true;
            Controls.Add(body);
            body.BringToFront();
            cursorY = 6;
        }

        /// ★ 加一行（图标 + 文字 + 可选快捷键 + 可选开关）✓
        public ItemRow AddItem(string icon, string text, string shortcut, EventHandler onClick)
        {
            ItemRow row = new ItemRow(this, icon, text, shortcut);
            row.Width = W;
            row.Height = 36;
            row.Location = new Point(0, cursorY);
            row.Clicked += onClick;
            body.Controls.Add(row);
            cursorY += 36;
            return row;
        }

        /// ★ 加一行带开关的 ✓
        public ToggleRow2 AddToggle(string icon, string text, bool on, EventHandler onToggle)
        {
            ToggleRow2 row = new ToggleRow2(this, icon, text, on);
            row.Width = W;
            row.Height = 36;
            row.Location = new Point(0, cursorY);
            row.Toggled += onToggle;
            body.Controls.Add(row);
            cursorY += 36;
            return row;
        }

        public void AddSeparator()
        {
            Panel line = new Panel();
            line.BackColor = Color.FromArgb(250, 224, 236);
            line.Location = new Point(12, cursorY + 4);
            line.Size = new Size(W - 24, 1);
            body.Controls.Add(line);
            cursorY += 10;
        }

        /// ★ 收尾：算总高度 ✗ 圆角 ✗ 放位置 ✓
        public void Finish(Point near)
        {
            int h = 62 + cursorY + 6 + 48;
            ClientSize = new Size(W, h);
            try
            {
                using (System.Drawing.Drawing2D.GraphicsPath gp =
                       Rounded.Path(new Rectangle(0, 0, Width, Height), 14))
                {
                    Region = new Region(gp);
                }
            }
            catch { }

            // ★ 放不下就往上翻 ✓
            Rectangle wa = Screen.FromPoint(near).WorkingArea;
            int x = near.X - W + 20;
            int y = near.Y - h - 10;
            if (x < wa.Left) x = wa.Left + 4;
            if (x + W > wa.Right) x = wa.Right - W - 4;
            if (y < wa.Top) y = near.Y + 10;
            if (y + h > wa.Bottom) y = wa.Bottom - h - 4;
            Location = new Point(x, y);
        }

        public void SetState(string text, Color color)
        {
            if (stateLabel == null) return;
            stateLabel.Text = "● " + text;
            stateLabel.ForeColor = color;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }

        /// ★ 点外面 / 失去焦点就关 ✓
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (onClosedExternally != null) onClosedExternally();
            Close();
        }

        /// ★ 圆角 + 粉边 ✓
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Color.FromArgb(232, 170, 198), 1))
            using (System.Drawing.Drawing2D.GraphicsPath gp =
                   Rounded.Path(new Rectangle(0, 0, Width - 1, Height - 1), 14))
                e.Graphics.DrawPath(p, gp);
        }
    }

    /// ★ 一行普通菜单项 ✓
    class ItemRow : Panel
    {
        public event EventHandler Clicked;
        bool hot;

        public ItemRow(Form owner, string icon, string text, string shortcut)
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Tag = Lang.NoTranslate;
            Cursor = Cursors.Hand;

            Label ic = new Label();
            ic.Text = icon;
            ic.Font = new Font("Segoe UI Emoji", 11f);
            ic.ForeColor = Theme.Accent;
            ic.BackColor = Color.Transparent;
            ic.AutoSize = false;
            ic.TextAlign = ContentAlignment.MiddleCenter;
            ic.Location = new Point(6, 3);
            ic.Size = new Size(26, 30);
            ic.Tag = Lang.NoTranslate;
            Controls.Add(ic);

            Label tx = new Label();
            tx.Text = text;
            tx.Font = new Font("Microsoft YaHei UI", 9.5f);
            tx.ForeColor = Theme.Text;
            tx.BackColor = Color.Transparent;
            tx.AutoSize = false;
            tx.TextAlign = ContentAlignment.MiddleLeft;
            tx.Location = new Point(36, 3);
            tx.Size = new Size(200, 30);
            tx.Tag = Lang.NoTranslate;
            Controls.Add(tx);

            if (!string.IsNullOrEmpty(shortcut))
            {
                Label sc = new Label();
                sc.Text = shortcut;
                sc.Font = new Font("Microsoft YaHei UI", 8f);
                sc.ForeColor = Theme.Muted;
                sc.BackColor = Color.Transparent;
                sc.AutoSize = false;
                sc.TextAlign = ContentAlignment.MiddleRight;
                sc.Location = new Point(0, 3);
                sc.Size = new Size(288, 30);
                sc.Tag = Lang.NoTranslate;
                Controls.Add(sc);
            }

            foreach (Control c in Controls)
            {
                c.MouseEnter += delegate { hot = true; Invalidate(); };
                c.MouseLeave += delegate { hot = false; Invalidate(); };
                c.MouseClick += delegate { if (Clicked != null) Clicked(this, EventArgs.Empty); };
            }
            MouseEnter += delegate { hot = true; Invalidate(); };
            MouseLeave += delegate { hot = false; Invalidate(); };
            MouseClick += delegate { if (Clicked != null) Clicked(this, EventArgs.Empty); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(hot ? Color.FromArgb(255, 235, 244) : Color.White))
                e.Graphics.FillRectangle(b, ClientRectangle);
            base.OnPaint(e);
        }
    }

    /// ★ 一行带开关的 ✓
    class ToggleRow2 : Panel
    {
        public event EventHandler Toggled;
        public bool On;
        bool hot;

        public ToggleRow2(Form owner, string icon, string text, bool on)
        {
            On = on;
            DoubleBuffered = true;
            BackColor = Color.White;
            Tag = Lang.NoTranslate;
            Cursor = Cursors.Hand;

            Label ic = new Label();
            ic.Text = icon;
            ic.Font = new Font("Segoe UI Emoji", 11f);
            ic.ForeColor = Theme.Accent;
            ic.BackColor = Color.Transparent;
            ic.AutoSize = false;
            ic.TextAlign = ContentAlignment.MiddleCenter;
            ic.Location = new Point(6, 3);
            ic.Size = new Size(26, 30);
            ic.Tag = Lang.NoTranslate;
            Controls.Add(ic);

            Label tx = new Label();
            tx.Text = text;
            tx.Font = new Font("Microsoft YaHei UI", 9.5f);
            tx.ForeColor = Theme.Text;
            tx.BackColor = Color.Transparent;
            tx.AutoSize = false;
            tx.TextAlign = ContentAlignment.MiddleLeft;
            tx.Location = new Point(36, 3);
            tx.Size = new Size(210, 30);
            tx.Tag = Lang.NoTranslate;
            Controls.Add(tx);

            foreach (Control c in Controls)
            {
                c.MouseEnter += delegate { hot = true; Invalidate(); };
                c.MouseLeave += delegate { hot = false; Invalidate(); };
                c.MouseClick += delegate
                {
                    On = !On;
                    Invalidate();
                    if (Toggled != null) Toggled(this, EventArgs.Empty);
                };
            }
            MouseEnter += delegate { hot = true; Invalidate(); };
            MouseLeave += delegate { hot = false; Invalidate(); };
            MouseClick += delegate
            {
                On = !On;
                Invalidate();
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(hot ? Color.FromArgb(255, 235, 244) : Color.White))
                e.Graphics.FillRectangle(b, ClientRectangle);

            // ★ 自己画一个小开关 ✓
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(Width - 56, 9, 40, 18);
            using (System.Drawing.Drawing2D.GraphicsPath gp = Rounded.Path(track, 9))
            using (SolidBrush b2 = new SolidBrush(On ? Theme.Accent : Color.FromArgb(224, 214, 220)))
                e.Graphics.FillPath(b2, gp);
            int kx = On ? track.Right - 16 : track.Left + 2;
            using (SolidBrush b3 = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(b3, kx, track.Top + 2, 14, 14);

            base.OnPaint(e);
        }
    }

    /// ★★★ 2026-10-18：粉色圆角对话框（参考 GPT 给的设计稿）★★★
    ///
    /// 结构：
    ///   ┌──────────────────────────────────┐
    ///   │ (头像) 标题                  ✕  │  ← 粉色渐变
    ///   │        副标题                     │
    ///   ├──────────────────────────────────┤
    ///   │  Body（白底圆角卡片）              │  ← 派生窗往 Body 里丢控件
    ///   ├──────────────────────────────────┤
    ///   │ (小猫娘) 手写体小字                │  ← 页脚
    ///   └──────────────────────────────────┘
    class PinkDialog : Form
    {
        public Panel Body;
        public Label TitleLabel;
        public Label SubLabel;
        Label footLabel;
        PictureBox footPic;
        bool dragging;
        Point dragFrom;

        public PinkDialog(string title, string subtitle, int w, int h)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(w, h);
            BackColor = Theme.Soft;
            Font = Theme.Body;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;

            // ---- 标题条 ----
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 84;
            header.BackColor = Theme.Soft;
            header.Tag = Lang.NoTranslate;
            header.Paint += PaintHeader;
            Controls.Add(header);

            // 头像（圆形裁切）
            PictureBox avatar = new PictureBox();
            avatar.Size = new Size(52, 52);
            avatar.Location = new Point(18, 16);
            avatar.SizeMode = PictureBoxSizeMode.Zoom;
            avatar.BackColor = Color.Transparent;
            try { avatar.Image = Art.LogoImage(); } catch { }
            avatar.Paint += delegate(object s1, PaintEventArgs e)
            {
                // 圆形蒙版 ✗ 图片是按轮廓裁的 ✓
                using (System.Drawing.Drawing2D.GraphicsPath gp =
                       new System.Drawing.Drawing2D.GraphicsPath())
                {
                    gp.AddEllipse(0, 0, avatar.Width - 1, avatar.Height - 1);
                    avatar.Region = new Region(gp);
                }
            };
            header.Controls.Add(avatar);

            TitleLabel = new Label();
            TitleLabel.Text = title;
            TitleLabel.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            TitleLabel.ForeColor = Theme.AccentDown;
            TitleLabel.BackColor = Color.Transparent;
            TitleLabel.AutoSize = false;
            TitleLabel.TextAlign = ContentAlignment.BottomLeft;
            TitleLabel.Location = new Point(82, 20);
            TitleLabel.Size = new Size(w - 130, 28);
            TitleLabel.Tag = Lang.NoTranslate;
            header.Controls.Add(TitleLabel);

            SubLabel = new Label();
            SubLabel.Text = subtitle;
            SubLabel.Font = new Font("Microsoft YaHei UI", 9f);
            SubLabel.ForeColor = Theme.Muted;
            SubLabel.BackColor = Color.Transparent;
            SubLabel.AutoSize = false;
            SubLabel.TextAlign = ContentAlignment.TopLeft;
            SubLabel.Location = new Point(84, 50);
            SubLabel.Size = new Size(w - 130, 20);
            SubLabel.Tag = Lang.NoTranslate;
            header.Controls.Add(SubLabel);

            Label close = new Label();
            close.Text = "✕";
            close.Font = new Font("Microsoft YaHei UI", 13f);
            close.ForeColor = Theme.AccentDown;
            close.BackColor = Color.Transparent;
            close.AutoSize = false;
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.Size = new Size(38, 34);
            close.Location = new Point(w - 46, 12);
            close.Cursor = Cursors.Hand;
            close.Tag = Lang.NoTranslate;
            close.Click += delegate { if (Modal) DialogResult = DialogResult.Cancel; Close(); };
            header.Controls.Add(close);

            // 拖动
            MouseEventHandler down = delegate(object s1, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                dragging = true;
                dragFrom = PointToClient(Cursor.Position);
            };
            MouseEventHandler move = delegate(object s1, MouseEventArgs e)
            {
                if (!dragging) return;
                Point p = Cursor.Position;
                Location = new Point(p.X - dragFrom.X, p.Y - dragFrom.Y);
            };
            MouseEventHandler up = delegate { dragging = false; };
            header.MouseDown += down;
            header.MouseMove += move;
            header.MouseUp += up;
            TitleLabel.MouseDown += down;
            TitleLabel.MouseMove += move;
            TitleLabel.MouseUp += up;

            // ---- 页脚（小猫娘 + 小字）----
            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 62;
            foot.BackColor = Theme.Soft;
            foot.Tag = Lang.NoTranslate;
            Controls.Add(foot);

            footPic = new PictureBox();
            footPic.Size = new Size(56, 56);
            footPic.Location = new Point(12, 4);
            footPic.SizeMode = PictureBoxSizeMode.Zoom;
            footPic.BackColor = Color.Transparent;
            try { footPic.Image = Art.LogoImage(); } catch { }
            foot.Controls.Add(footPic);

            footLabel = new Label();
            footLabel.Text = "让每一句话，都变得喵喵的～";
            footLabel.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Italic);
            footLabel.ForeColor = Theme.Accent;
            footLabel.BackColor = Color.Transparent;
            footLabel.AutoSize = false;
            footLabel.TextAlign = ContentAlignment.MiddleLeft;
            footLabel.Location = new Point(74, 18);
            footLabel.Size = new Size(w - 100, 26);
            footLabel.Tag = Lang.NoTranslate;
            foot.Controls.Add(footLabel);

            // ---- 内容区 ----
            Body = new Panel();
            Body.Dock = DockStyle.Fill;
            Body.BackColor = Color.White;
            Body.Padding = new Padding(16, 12, 16, 10);
            Controls.Add(Body);
            Body.BringToFront();
        }

        /// ★ 一行标签 + 输入框 ✗ 返回输入框 ✓
        public PinkInput MakeField(string icon, string label, string value,
                                   int x, int y, int w, int maxLen)
        {
            Label l = new Label();
            l.Text = icon + "  " + label;
            l.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            l.ForeColor = Theme.Text;
            l.BackColor = Color.White;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 22);
            l.Tag = Lang.NoTranslate;
            Body.Controls.Add(l);

            PinkInput inp = new PinkInput(new Size(w, 38), false);
            inp.Location = new Point(x, y + 24);
            inp.Box.MaxLength = maxLen;
            inp.Box.Text = value;
            Body.Controls.Add(inp);
            return inp;
        }

        /// ★ 一块多行输入区 ✓
        public PinkInput MakeArea(string icon, string label, string value,
                                  int x, int y, int w, int h)
        {
            Label l = new Label();
            l.Text = icon + "  " + label;
            l.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            l.ForeColor = Theme.Text;
            l.BackColor = Color.White;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 22);
            l.Tag = Lang.NoTranslate;
            Body.Controls.Add(l);

            PinkInput inp = new PinkInput(new Size(w, h), true);
            inp.Location = new Point(x, y + 24);
            inp.Box.Text = value;
            Body.Controls.Add(inp);
            return inp;
        }

        /// ★ 页脚那句话（派生窗可以改）✓
        public void SetFooter(string text)
        {
            if (footLabel != null) footLabel.Text = text;
        }

        /// ★ 标题条：粉色渐变 + 右下角一片波浪 ✓
        void PaintHeader(object sender, PaintEventArgs e)
        {
            Rectangle r = ((Control)sender).ClientRectangle;
            using (System.Drawing.Drawing2D.LinearGradientBrush br =
                   new System.Drawing.Drawing2D.LinearGradientBrush(
                       r, Color.FromArgb(255, 246, 250),
                       Color.FromArgb(255, 222, 235), 20f))
            {
                e.Graphics.FillRectangle(br, r);
            }
            // 底部一条细粉线
            using (Pen p = new Pen(Color.FromArgb(250, 214, 228)))
                e.Graphics.DrawLine(p, 0, r.Height - 1, r.Width, r.Height - 1);
            // 右下角淡粉圆弧装饰
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 240, 98, 146)))
            {
                e.Graphics.FillEllipse(b, r.Width - 90, r.Height - 46, 120, 90);
                e.Graphics.FillEllipse(b, r.Width - 150, r.Height - 26, 80, 60);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            try
            {
                using (System.Drawing.Drawing2D.GraphicsPath gp =
                       Rounded.Path(new Rectangle(0, 0, Width, Height), 16))
                {
                    Region = new Region(gp);
                }
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            KeyDown += delegate(object s1, KeyEventArgs ke)
            {
                if (ke.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };
        }
    }

    static class Rounded
    {        public static GraphicsPath Path(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();

            // A control that is momentarily zero-sized still gets painted during layout.
            // AddArc rejects a zero or negative diameter, and the 0-size case crashed the
            // whole app with "参数无效" inside CardPanel.OnPaint - a modal .NET error box, so
            // it hung the build for ten minutes as well. Anything that cannot hold a corner
            // is simply a rectangle.
            if (r.Width <= 0 || r.Height <= 0) return p;   // nothing to draw
            int d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                if (d <= 0) { p.AddRectangle(r); return p; }
            }
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d < 2) { p.AddRectangle(r); return p; }    // a 1px "corner" is just a rectangle
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ---------------------------------------------------------------- controls

    /// A white rounded panel. Children inside should set BackColor = Color.White so
    /// they blend into it.
    ///
    /// This panel itself stays transparent so a window wallpaper can show through
    /// around its rounded corners - which is why it does NOT clear its own area.
    /// 滚轮平滑滚动。
    ///
    /// 默认行为是滚一格就瞬移一大截，看着很生硬。这里把滚轮的目标位置记下来，用计时器每
    /// 15 毫秒把当前位置往目标补 30%，越接近越慢，滚起来是"滑"过去的。
    ///
    /// 只接管滚轮；拖动滚动条仍然是即时的 —— 拖的时候手指就是位置，再加动画只会觉得黏。
    /// 平滑滚动的只读文本视图 —— 用来替掉 TextBox。
    ///
    /// 为什么要换：TextBox 的滚动是【按行跳】的，没有"像素级滚动位置"这个概念，无论怎么补间
    /// 它一次至少跳一行，做不出平滑效果。这里自己做，滚动位置就是一个像素值。
    ///
    /// 折行交给 TextRenderer：把【整段文字】画进一个超高的矩形里，再整体上移 scrollY，让系统
    /// 自己裁剪。这样既不用自己算折行，也不用知道行数 —— 只需要一次 MeasureText 得到总高度。
    ///
    /// 代价：鼠标拖选没有（Ctrl+A 全选、Ctrl+C 复制、右键"复制全文"都还在）。
    class SmoothTextView : Control
    {
        string raw = "";
        int contentH = 0;      // 整段文字按当前宽度折行后的总高度
        int scrollY = 0;       // 当前滚动位置（像素）
        int targetY = 0;       // 目标位置
        System.Windows.Forms.Timer timer;
        VScrollBar bar;
        Bitmap cache;          // 整段文字只渲染一次，之后每帧只贴一条
        bool syncingBar;       // 正在由动画同步 bar.Value，别当成用户拖动
        bool isCode;           // 规则页是表格样式的等宽排版

        public bool PlainText = true;

        public string Body
        {
            get { return raw; }
            set
            {
                raw = value ?? "";
                // 换内容必须把滚动位置归零。
                //
                // 否则新内容会从"上一份的滚动位置"开始显示，顶部那一行就是被切掉一半的样子 ——
                // 用户看到的"蓝框里的字被截掉了"就是它。
                StopAnim();
                scrollY = targetY = 0;
                syncingBar = true;
                try { bar.Value = 0; } catch { }
                syncingBar = false;
                Reflow();
                Invalidate();
            }
        }

        public SmoothTextView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            TabStop = true;

            bar = new VScrollBar();
            bar.Dock = DockStyle.Right;
            bar.Width = 14;
            bar.SmallChange = 24;
            bar.ValueChanged += delegate
            {
                // syncingBar 保护是必须的。
                //
                // 动画每帧都会同步 bar.Value，而 bar.Value 一变就会触发这个处理器 ——
                // 原来的第一句是 StopAnim()，等于动画刚起步就被自己掐断，表现就是"一卡一卡"
                // 甚至"完全没有动画"。只有【用户拖动】滚动条时才应该停止补间。
                if (syncingBar) return;
                StopAnim();
                scrollY = targetY = bar.Value;
                Invalidate();
            };
            Controls.Add(bar);
        }

        void StopAnim()
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
        }

        const int PadX = 5;   // 左内边距，别贴着边框
        const int PadY = 10;  // 上内边距，别贴着边框
        int ViewW { get { return Math.Max(20, ClientSize.Width - bar.Width - 8); } }

        void Reflow()
        {
            if (raw.Length == 0) { contentH = 0; SyncBar(); return; }
            // 用 MeasureString / DrawString 这一套（GDI+），不用 TextRenderer（GDI）。
            //
            // 原因是「GDI 文字画进 GDI+ 位图」这个组合渲染出来偏虚；而且程序里所有 Label 都是
            // GDI+ 画的，换成同一套渲染器观感才一致。
            using (Graphics mg = CreateGraphics())
            using (StringFormat sf = new StringFormat(StringFormatFlags.LineLimit))
            {
                sf.Trimming = StringTrimming.Word;
                SizeF sz = mg.MeasureString(raw, Font, new SizeF(ViewW, 100000f), sf);
                contentH = (int)Math.Ceiling(sz.Height) + PadY * 2;
            }
            if (cache != null) { try { cache.Dispose(); } catch { } cache = null; }
            SyncBar();
        }

        void SyncBar()
        {
            int viewH = ClientSize.Height;
            int max = Math.Max(0, contentH - viewH);
            bar.Minimum = 0;
            bar.Maximum = max + viewH - 1;     // 滚动条的这个约定很怪，但必须这样
            bar.LargeChange = Math.Max(1, viewH);
            bar.Visible = max > 0;
            if (scrollY > max) { scrollY = max; }
            if (targetY > max) { targetY = max; }
            syncingBar = true;
            try { if (bar.Value != scrollY) bar.Value = Math.Min(scrollY, max); } catch { }
            syncingBar = false;
        }

        // ★ 用户 2026-10-17：拖动窗口卡顿 ✗ 定位到这一行了 ✓
        //
        // Reflow() 里面是 MeasureString(raw, ...) ✧ raw 现在有 2 万字 ✗
        // 而 OnResize 每一帧都调它 ✧ 于是拖拉时每帧都在量 2 万字 ✗
        // GDI+ 这个体量就是 ~80ms ✧ 跟日志里的 78~95ms 完全对得上 ✓
        //
        // 修法：拖动中不量 ✧ 松手 250ms 后量一次 ✓✓✓
        //   代价：拖动中滚动条长度是旧的 ✧ 松手就对了 ✓
        System.Windows.Forms.Timer reflowTimer;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // 首次布局要立刻量 ✧ 不然滚动条要晚 250ms 才出来 ✓
            if (contentH == 0 || raw.Length == 0) { Reflow(); return; }

            if (reflowTimer == null)
            {
                reflowTimer = new System.Windows.Forms.Timer();
                reflowTimer.Interval = 250;
                reflowTimer.Tick += delegate
                {
                    try { reflowTimer.Stop(); } catch { }
                    try { Reflow(); } catch { }
                };
            }
            reflowTimer.Stop();
            reflowTimer.Start();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!WheelBy(e.Delta)) base.OnMouseWheel(e);
        }

        /// 按滚轮增量平滑滚动。返回 false 表示没有内容可滚。
        ///
        /// 单独开这个入口，是因为滚轮消息并不总送到这里 —— 见 MainForm.OnMouseWheel。
        public bool WheelBy(int delta)
        {
            int max = Math.Max(0, contentH - ClientSize.Height);
            if (max <= 0) return false;
            if (timer == null || !timer.Enabled || Math.Abs(scrollY - targetY) > 600) targetY = scrollY;
            targetY -= delta;
            if (targetY < 0) targetY = 0;
            if (targetY > max) targetY = max;

            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 12;
                timer.Tick += delegate
                {
                    int d = targetY - scrollY;
                    if (Math.Abs(d) <= 1)
                    {
                        scrollY = targetY;
                        StopAnim();
                    }
                    else scrollY += (d * 14) / 100;   // 每帧补 14%，越近越慢
                    syncingBar = true;
                    try { bar.Value = Math.Min(scrollY, bar.Maximum); } catch { }
                    syncingBar = false;
                    Invalidate();
                };
            }
            timer.Start();
            return true;
        }

        /// 整段文字渲染成一张位图并缓存。宽度或内容变了会重建。
        Bitmap Cache()
        {
            if (cache != null) return cache;
            if (raw.Length == 0 || contentH <= 0) return null;
            try
            {
                // 必须是 24bpp、不能带 alpha 通道。
                //
                // new Bitmap(w,h) 默认是 32bpp ARGB，而 GDI 在带透明通道的位图上会【禁用
                // ClearType】，文字退化成灰阶抗锯齿 —— 又细又虚，用户的原话是"真的看不清"。
                // 24bpp 没有 alpha，ClearType 正常生效。
                cache = new Bitmap(ViewW + 8, contentH, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                // 位图的 DPI 必须和屏幕一致。
                //
                // new Bitmap(w,h) 默认是 96 DPI，而屏幕可能是 120 DPI（125% 缩放）。GDI+ 贴图
                // 时会按 DPI 换算，于是整张图被缩放了 0.8 倍 —— 表现就是"字全都糊了"。
                using (Graphics g = Graphics.FromImage(cache))
                {
                    try { cache.SetResolution(g.DpiX, g.DpiY); } catch { }
                    g.Clear(BackColor);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using (StringFormat sf = new StringFormat(StringFormatFlags.LineLimit))
                    {
                        sf.Trimming = StringTrimming.Word;
                        using (SolidBrush br = new SolidBrush(ForeColor))
                            g.DrawString(raw, Font, br,
                                new RectangleF(PadX, PadY, ViewW, contentH - PadY * 2), sf);
                    }
                }
            }
            catch { cache = null; }
            return cache;
        }

        protected override void OnPaint(PaintEventArgs e)
        {            Graphics g = e.Graphics;
            using (SolidBrush b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            Bitmap c = Cache();
            if (c == null) return;

            // 每帧只做一次贴图，GDI 只会复制可见的那一条。
            //
            // 原来每帧都调 TextRenderer.DrawText 重排【整段两万字】，一帧要几十毫秒 ——
            // 表现就是"一卡一卡的"。整段渲染一次存下来，之后滚动就只是一次 BitBlt。
            // 显式给 1:1 的目标矩形。位图 DPI 已对齐屏幕，这里不会再被缩放。
            g.DrawImage(c, new Rectangle(0, -scrollY, c.Width, c.Height),
                0, 0, c.Width, c.Height, GraphicsUnit.Pixel);
        }

        protected override bool IsInputKey(Keys k)
        {
            if (k == Keys.Up || k == Keys.Down || k == Keys.PageUp || k == Keys.PageDown) return true;
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int max = Math.Max(0, contentH - ClientSize.Height);
            if (e.Control && e.KeyCode == Keys.A) { CopyAll(); e.Handled = true; return; }
            if (e.Control && e.KeyCode == Keys.C) { CopyAll(); e.Handled = true; return; }
            if (e.KeyCode == Keys.Down) { targetY = scrollY = Math.Min(max, scrollY + 40); }
            else if (e.KeyCode == Keys.Up) { targetY = scrollY = Math.Max(0, scrollY - 40); }
            else if (e.KeyCode == Keys.PageDown) { targetY = scrollY = Math.Min(max, scrollY + ClientSize.Height); }
            else if (e.KeyCode == Keys.PageUp) { targetY = scrollY = Math.Max(0, scrollY - ClientSize.Height); }
            else { base.OnKeyDown(e); return; }
            SyncBar();
            Invalidate();
            e.Handled = true;
        }

        /// 供日志用：内容多高、视口多高、能不能滚。
        public string ScrollState
        {
            get
            {
                return "bounds=" + Bounds.X + "," + Bounds.Y + " " + Width + "x" + Height
                     + " client=" + ClientSize.Width + "x" + ClientSize.Height
                     + " content=" + contentH + " scrollY=" + scrollY
                     + " barVis=" + bar.Visible + " barMax=" + bar.Maximum
                     + " parent=" + (Parent == null ? "none" : Parent.Width + "x" + Parent.Height);
            }
        }

        public void CopyAll()
        {
            try { if (raw.Length > 0) Clipboard.SetText(raw); } catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip m = new ContextMenuStrip();
                m.Items.Add(Lang.T("复制全文"), null, delegate { CopyAll(); });
                m.Show(this, e.Location);
            }
        }
    }
    class SmoothPanel : Panel
    {
        System.Windows.Forms.Timer timer;

        /// \u2605\u2605\u2605 2026-10-18\uff1a\u544a\u8bc9\u6eda\u52a8\u6761\u5185\u5bb9\u6709\u591a\u9ad8 \u2605\u2605\u2605
        ///   \u8fd9\u4e2a\u63a7\u4ef6\u91cc\u7684\u5b50\u63a7\u4ef6\u5168\u662f\u300c\u7edd\u5bf9\u5750\u6807\u300d\u6446\u7684 \u2717
        ///   WinForms \u53ea\u4f1a\u6839\u636e\u5b50\u63a7\u4ef6\u7684\u8fb9\u754c\u7b97\u6eda\u52a8\u8303\u56f4 \u2717
        ///   \u800c\u6700\u540e\u4e00\u884c\u5f80\u5f80\u8d34\u7740\u5e95 \u2727 \u518d\u52a0\u4e0a\u6ca1\u6709\u4f59\u91cf \u2717
        ///   \u4e8e\u662f\u6eda\u5230\u6700\u4e0b\u9762\u8fd8\u662f\u770b\u4e0d\u5168 \u2717 \u8fd9\u91cc\u7edf\u4e00\u7b97\u4e00\u4e0b \u2713
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (!AutoScroll) return;
            int maxY = 0;
            foreach (Control c in Controls)
                if (c.Visible) maxY = Math.Max(maxY, c.Bottom);
            int need = maxY + 24;
            if (AutoScrollMinSize.Height != need)
                AutoScrollMinSize = new Size(0, need);
        }
        int target = 0;
        /// 上一帧我们把它放在哪个位置 —— 用来判断是不是用户自己在滚。
        int lastSet = -1;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!AutoScroll) { base.OnMouseWheel(e); return; }

            int cur = -AutoScrollPosition.Y;
            int max = DisplayRectangle.Height - ClientSize.Height;
            if (max < 0) max = 0;

            // 连续滚动时沿用上一次的目标，否则每格都会从当前位置重新起步，快滚会发飘
            if (timer == null || !timer.Enabled || Math.Abs(cur - target) > 400) target = cur;
            lastSet = cur;   // 同步一下，免得拿上一轮的旧值做判断
            target -= e.Delta;
            if (target < 0) target = 0;
            if (target > max) target = max;

            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 12;
                timer.Tick += delegate
                {
                    int now = -AutoScrollPosition.Y;

                    // ★ 动画在跑的时候，用户可能正在拖滚动条（或者按方向键、按 PageDown）。
                    //   原来不管这些 —— 动画每 12ms 就把位置改回自己的目标，
                    //   于是"往下拖会被拉回去、往上拖也会被拉回去"，滚动条完全按不住。
                    //   这里每次先看位置是不是还停在上一帧我们放的地方：
                    //   不是，就说明是用户自己在滚，那就【立刻停手】别跟他抢。
                    if (now != lastSet)
                    {
                        timer.Stop();
                        return;
                    }

                    int d = target - now;
                    if (Math.Abs(d) <= 2)
                    {
                        try { AutoScrollPosition = new Point(0, target); } catch { }
                        lastSet = target;
                        timer.Stop();
                        return;
                    }
                    // 每帧补 12%（原来 30%）：30% 大约 5 帧就走完，约 70ms，太快
                    lastSet = now + (d * 12) / 100;
                    try { AutoScrollPosition = new Point(0, lastSet); } catch { }
                };
            }
            timer.Start();
        }
    }
    class CardPanel : Panel
    {
        public int Radius = 16;

        /// ⑬ set once at startup from 界面-猫爪装饰.
        public static bool ShowPaw = false;


        // ⑬ 爪印：一个【最后添加的子控件】，浮在所有功能图层之上。
        //
        // 这是用户提的思路，也是唯一正确的方向 —— 画在 OnPaint 里永远会被子控件盖住。
        // 之前做失败的原因是没有可靠的触发点：OnLayout 在这个控件上不一定会再跑。现在直接从
        // OnPaint 末尾驱动（OnPaint 必定执行），并且用 pawBusy 防止它反过来触发重绘。
        PictureBox pawBox;
        bool pawBusy;
        // \u2605 \u7528\u6237 2026-10-17\uff1a\u653e\u5927\u7f29\u5c0f\u7a97\u53e3\u5361 \u2717 \u67e5\u51fa\u722a\u5370\u6bcf\u5e27\u90fd\u91cd\u5efa\u56fe\u7247 + \u6539 z-order \u2717
        //   \u4e8e\u662f\u753b\u4e00\u5e27 \u2727 \u6539\u5b50\u63a7\u4ef6 \u2727 \u53c8\u8981\u6c42\u91cd\u7ed8 \u2727 \u65e0\u9650\u5faa\u73af \u2717\u2717\u2717
        //   \u4fee\u6cd5\uff1a\u56fe\u7247\u7f13\u5b58\u4f4f \u2727 \u53ea\u6709\u771f\u53d8\u4e86\u624d\u52a8\u5b50\u63a7\u4ef6 \u2713
        static Image pawArtCached;

        /// 卡片自己、或它里面的某个滚动面板，只要出现了滚动条，就不要在上面放爪印。
        bool HasScrollbar()
        {
            try
            {
                if (AutoScroll && (VerticalScroll.Visible || HorizontalScroll.Visible)) return true;
                foreach (Control c in Controls)
                {
                    ScrollableControl sc = c as ScrollableControl;
                    if (sc == null || !sc.AutoScroll) continue;
                    // VerticalScroll.Visible 要等布局跑完才为真，卡片首帧绘制时往往还是
                    // false —— 右侧栏那几张就是这么漏过去的。内容比可视区高/宽，就一定会有
                    // 滚动条，这个判断不依赖布局时机。
                    if (sc.VerticalScroll.Visible || sc.HorizontalScroll.Visible) return true;
                    if (sc.DisplayRectangle.Height > sc.ClientSize.Height) return true;
                    if (sc.DisplayRectangle.Width > sc.ClientSize.Width) return true;
                    // 往下一层也看一眼
                    foreach (Control gc in sc.Controls)
                    {
                        ScrollableControl gsc = gc as ScrollableControl;
                        if (gsc == null) continue;
                        if (gsc.DisplayRectangle.Height > gsc.ClientSize.Height) return true;
                    }
                }
            }
            catch { }
            return false;
        }
        /// 子控件（或子控件的子控件）内容变化时，让本卡片重画一次。
        ///
        /// 右侧栏那三张卡的滚动条是【内容填进去之后】才出现的，而那时卡片已经画完了，不会再
        /// 自动重画 —— 于是它们一直保留着"没有滚动条"时画上的爪印。挂上这个钩子，内容一变就
        /// 重画，BuildPaw 会重新判断并撤掉爪印。
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            HookChild(e.Control);
            Invalidate();
        }

        void HookChild(Control c)
        {
            if (c == null) return;
            c.ControlAdded += delegate(object s, ControlEventArgs ce)
            {
                HookChild(ce.Control);
                Invalidate();
            };
            c.Layout += delegate { Invalidate(); };
            foreach (Control gc in c.Controls) HookChild(gc);
            Invalidate();
        }
        void BuildPaw()
        {
            if (pawBusy) return;
            // 有滚动条的卡片不放爪印。
            //
            // 爪印是浮在最上层的子控件，落点又在右下角 —— 正好压在滚动条上，把滚动条盖掉一截。
            // 与其迁就位置，不如这类卡片干脆不放：滚动条本身就是个需要看清的控件。
            // 判据：宽度 > 400。
            //
            // 右侧栏那几张只有 276 宽，主列都是 666。这样一刀切掉右侧栏，比去猜"滚动条什么时候
            // 出现"可靠得多 —— 那几张卡的滚动条是内容填进去之后才有的，卡片首帧绘制时还看不到，
            // 判断不出来；等它出现，卡片又已经画完了。用户的原话就是"右边框出来这三个猫爪也去
            // 了吧"，直接照办最省事。
            bool want = ShowPaw && Width > 400 && Height > 110 && !HasScrollbar();
            if (!want)
            {
                if (pawBox != null && pawBox.Visible) { pawBusy = true; pawBox.Visible = false; pawBusy = false; }
                // \u2605 HasScrollbar \u8d70\u4e00\u904d\u5c31\u591f\u4e86 \u2727 \u539f\u6765\u8fd9\u91cc\u53c8\u8d70\u4e86\u4e00\u904d \u2717
                PawLog("skip " + Width + "x" + Height);
                return;
            }
            pawBusy = true;
            try
            {
                if (pawBox == null)
                {
                    pawBox = new PictureBox();
                    pawBox.SizeMode = PictureBoxSizeMode.AutoSize;
                    pawBox.BackColor = Color.Transparent;
                    pawBox.Enabled = false;
                    Controls.Add(pawBox);
                }
                if (pawArtCached == null)
                    pawArtCached = VecIcon.ByName("paw", Color.FromArgb(76, 214, 106, 160), 22);
                Image art = pawArtCached;
                if (art == null) { PawLog("art null"); return; }

                // \u2605 \u53ea\u6709\u771f\u53d8\u4e86\u624d\u52a8 \u2727 \u4e0d\u7136\u6bcf\u6b21\u90fd\u89e6\u53d1\u91cd\u7ed8 \u2713
                if (pawBox.Image != art) pawBox.Image = art;
                if (pawBox.Size != art.Size) pawBox.Size = art.Size;
                Point wantPt = new Point(Width - art.Width - 20, Height - art.Height - 20);
                if (pawBox.Location != wantPt) pawBox.Location = wantPt;
                if (!pawBox.Visible) { pawBox.Visible = true; pawBox.BringToFront(); }
                PawLog("draw " + Width + "x" + Height);
            }
            catch (Exception ex) { PawLog("threw " + ex.Message); }
            finally { pawBusy = false; }
        }
        /// 调试用：--film 的探针会打出来
        public static string PawDebug = "(none)";
        static readonly System.Collections.Generic.List<string> pawLog =
            new System.Collections.Generic.List<string>();
        static void PawLog(string s)
        {
            if (pawLog.Count > 40) return;
            if (pawLog.Contains(s)) return;
            pawLog.Add(s);
            PawDebug = string.Join(" / ", pawLog.ToArray());
        }

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Padding = new Padding(16);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            // \u2605 \u6574\u5e27\u8ba1\u65f6 \u2727 \u7528\u6237\u62d6\u7a97\u53e3\u5361\u7684\u65f6\u5019\u80fd\u770b\u5230\u5e95\u662f\u8c01\u6162 \u2713
            Graphics g = pe.Graphics;

            // A SCROLLABLE card has to erase itself first.
            //
            // Not clearing is deliberate for a static card: it lets the window wallpaper show
            // through the rounded corners. But AutoScroll works by blitting the panel sideways,
            // so the card's own border gets copied along with the content and reappears lower
            // down - a user saw the left border as three separate rounded segments and quite
            // reasonably reported it as broken.
            if (AutoScroll)
            {
                Color bgc = (Parent != null && Parent.BackColor != Color.Transparent)
                           ? Parent.BackColor : Theme.Bg;
                using (SolidBrush bg = new SolidBrush(bgc))
                    g.FillRectangle(bg, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            // soft drop shadow: a few stacked translucent rounded rects nudged downward
            for (int i = 4; i >= 1; i--)
            {
                Rectangle sr = new Rectangle(r.X + 1, r.Y + i, r.Width - 2, r.Height - 2);
                using (GraphicsPath sp = Rounded.Path(sr, Radius))
                using (SolidBrush sb = new SolidBrush(Theme.Shadow))
                    g.FillPath(sb, sp);
            }

            using (GraphicsPath p = Rounded.Path(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Line)) g.DrawPath(pen, p);

            }

            BuildPaw();   // ⑬ 把爪印这个子控件摆到最上层
        }

        /// <summary>
        /// Force a full repaint after any scroll of THIS panel.
        ///
        /// AutoScroll scrolls by blitting the panel's own pixels sideways and then
        /// invalidating only the newly exposed strip. The card's rounded border is painted
        /// by this class, so it gets copied along with the content and is never repainted -
        /// the left border ends up repeated at intervals down the page. A user spotted the
        /// rule exactly: pages where the CARD scrolls (风格/设置/关于) break, pages where an
        /// inner panel scrolls (公告/统计/规则) are fine.
        ///
        /// Clearing inside OnPaint cannot fix that, because those blitted pixels never reach
        /// OnPaint. Invalidating the whole panel here does.
        /// </summary>
        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            if (AutoScroll) Invalidate(true);      // true = 子控件一起，见下
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (AutoScroll) Invalidate(true);
        }
    }

    /// A borderless TextBox inside a rounded, hairline-bordered box. The default
    /// FixedSingle border is a hard dark line that fights the rest of the theme.
    /// Gradient band behind the header. OnPaintBackground is deliberately empty so
    /// child labels with BackColor = Transparent let the gradient show through.
    /// A small rounded card: emoji icon on the left, a large number, a caption under it.
    ///
    /// Exists because the status line was one row of small pills where "7" and "10" carried
    /// the same visual weight as the words around them. The numbers are what a person
    /// actually reads, so they get their own card and their own large font.
    ///
    /// The icon is a separate Label with the Segoe UI Emoji font: the app font has no emoji
    /// coverage and draws empty boxes (checked by rendering them, not by assuming).
    class StatCard : CardPanel
    {
        public Label Icon;
        public Label Value;
        public Label Caption;

        public StatCard()
        {
            Radius = 14;
            Padding = new Padding(0);
            Margin = new Padding(0, 0, 10, 0);

            Icon = new Label();
            Icon.Font = new Font("Segoe UI Emoji", 15f);
            Icon.BackColor = Color.Transparent;
            Icon.AutoSize = false;
            Icon.TextAlign = ContentAlignment.MiddleCenter;
            Icon.Size = new Size(40, 34);
            Icon.Location = new Point(6, 8);
            // Without this a WinForms Label renders through GDI, which draws colour emoji as
            // flat monochrome shapes - the reason the "green" dot looked grey.
            Controls.Add(Icon);

            Value = new Label();
            Value.Font = new Font("Microsoft YaHei UI", 19f, FontStyle.Bold);
            Value.BackColor = Color.Transparent;
            Value.AutoSize = false;
            Value.TextAlign = ContentAlignment.MiddleLeft;
            Value.Size = new Size(130, 34);
            Value.Location = new Point(48, 6);
            Controls.Add(Value);

            Caption = new Label();
            Caption.Font = Theme.Small;
            Caption.ForeColor = Theme.Muted;
            Caption.BackColor = Color.Transparent;
            Caption.AutoSize = false;
            Caption.TextAlign = ContentAlignment.MiddleLeft;
            Caption.Size = new Size(160, 18);
            Caption.Location = new Point(50, 42);
            Controls.Add(Caption);
        }

        public void Set(string emoji, string value, string caption, Color valueColor)
        {
            // ★★★ 先让卡片把这些子控件占的地方【重画一遍】★★★
            //
            // 为什么必须手动做这件事：
            //   CardPanel 为了让 Transparent 的子控件透出渐变 ✗ 
            //   【把 OnPaintBackground 留空了】（见它的注释 ✓）✗ 
            //   而"擦掉旧内容"正是靠 OnPaintBackground ✓
            //   Icon / Value / Caption 三个都是 Transparent ✗ 
            //   → 每次 Set 只是把新文字【画在旧文字上面】✗ 旧的永远擦不掉 ✓
            //
            //   用户看到的现象：数字从 6 变 4 时 ✗ 
            //   残留笔画和新数字叠起来 ✗ 看起来像先闪了一个 5 ✓
            //
            // Invalidate(矩形, false) 只标记卡片那一块 ✗ 不含子控件 ✓
            // 后面的 Update() 会把这些脏区一次画完 ✗ 覆盖掉旧像素 ✓
            if (Value != null) Invalidate(Value.Bounds, false);
            if (Caption != null) Invalidate(Caption.Bounds, false);
            if (Icon != null) Invalidate(Icon.Bounds, false);

            Icon.Text = emoji;   // monochrome glyph, the look that was chosen over the drawn icons
            Value.Text = value;
            Value.ForeColor = valueColor;
            Caption.Text = caption;

            // ★ 改完再整体标记一次 ✗ 立刻同步绘制 ✓
            Invalidate(true);
            Update();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Value != null) Value.Width = Math.Max(40, Width - 56);
            if (Caption != null) Caption.Width = Math.Max(40, Width - 56);
        }
    }

    class HeaderBand : Panel
    {
        public HeaderBand()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs pe) { }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Rectangle r = new Rectangle(0, 0, Width, Height);
            using (LinearGradientBrush b = new LinearGradientBrush(
                r, Color.FromArgb(255, 202, 227), Color.FromArgb(224, 214, 255), 12f))
                pe.Graphics.FillRectangle(b, r);
        }
    }

    class FieldBox : Panel
    {
        public readonly TextBox Box = new TextBox();

        bool focused = false;

        public FieldBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Padding = new Padding(9, 7, 9, 7);

            Box.BorderStyle = BorderStyle.None;
            Box.Multiline = true;
            Box.Dock = DockStyle.Fill;
            Box.BackColor = Color.White;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.Body;
            Box.ScrollBars = ScrollBars.Vertical;
            Box.Enter += delegate { focused = true; Invalidate(); };
            Box.Leave += delegate { focused = false; Invalidate(); };
            Controls.Add(Box);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Rounded.Path(r, 8))
            {
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (Pen pen = new Pen(focused ? Theme.Accent : Theme.Line, focused ? 1.6f : 1f))
                    g.DrawPath(pen, p);
            }
        }
    }

    /// A pill-shaped on/off switch with its own label.
    ///
    /// Deliberately derives from Control, NOT from CheckBox. A CheckBox keeps painting
    /// its native box and its own copy of the text underneath whatever OnPaint does
    /// (that showed up as doubled, garbled labels and stray black borders), and no
    /// combination of SetStyle/CreateParams reliably turned that off.
    class ToggleRow : Control
    {
        const int TRACK_W = 42;
        const int TRACK_H = 22;
        const int GAP = 10;

        bool hover = false;
        bool isChecked = false;

        public event EventHandler CheckedChanged;

        public ToggleRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
            BackColor = Theme.Card;
            Font = Theme.Body;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            Height = 28;
            Width = 300;
            TabStop = true;
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled) Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(1, (Height - TRACK_H) / 2, TRACK_W, TRACK_H);
            Color fill = isChecked
                ? (hover ? Theme.AccentHover : Theme.Accent)
                : (hover ? Theme.TrackHover : Theme.TrackOff);

            using (GraphicsPath p = Rounded.Path(track, TRACK_H / 2))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);

            if (Focused)
            {
                Rectangle fr = new Rectangle(track.X - 2, track.Y - 2, track.Width + 4, track.Height + 4);
                using (GraphicsPath fp = Rounded.Path(fr, (TRACK_H + 4) / 2))
                using (Pen pen = new Pen(Color.FromArgb(120, Theme.Accent), 2))
                    g.DrawPath(pen, fp);
            }

            int kd = TRACK_H - 6;
            int kx = isChecked ? track.Right - kd - 3 : track.X + 3;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(252, 250, 251)))
                g.FillEllipse(b, kx, track.Y + 3, kd, kd);

            Rectangle text = new Rectangle(TRACK_W + GAP, 0, Math.Max(0, Width - TRACK_W - GAP), Height);
            TextRenderer.DrawText(g, Text, Font, text,
                Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// A small filled circle used as a status light.
    class StatusDot : Control
    {
        Color dot = Theme.Muted;

        public StatusDot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Size = new Size(14, 14);
        }

        public Color DotColor
        {
            get { return dot; }
            set { dot = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Math.Min(Width, Height) - 4;
            using (SolidBrush halo = new SolidBrush(Color.FromArgb(55, dot)))
                g.FillEllipse(halo, (Width - d) / 2 - 2, (Height - d) / 2 - 2, d + 4, d + 4);
            using (SolidBrush b = new SolidBrush(dot))
                g.FillEllipse(b, (Width - d) / 2, (Height - d) / 2, d, d);
        }
    }

    /// A row of small rounded capsule labels, so counts read as pills instead of a
    /// wall of plain text.
    class PillBar : Control
    {
        string[] items = new string[0];
        Color[] accents = new Color[0];

        public PillBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Font = Theme.Tiny;
        }

        public void SetItems(string[] texts, Color[] colors)
        {
            items = texts == null ? new string[0] : texts;
            accents = colors == null ? new Color[0] : colors;
            // ★ 立刻重绘 ✧ 和 StatCard 同一个病 ✓
            //   只 Invalidate 的话要等消息循环空闲才画 ✧
            //   而下一次完整重绘是 700ms 定时器触发的 ✗
            Invalidate();
            Update();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int x = 0;
            for (int i = 0; i < items.Length; i++)
            {
                Size sz = TextRenderer.MeasureText(items[i], Font);
                int w = sz.Width + 22;
                if (w < 44) w = 44;
                Rectangle r = new Rectangle(x, (Height - 24) / 2, w, 24);
                Color accent = i < accents.Length ? accents[i] : Theme.Accent;

                using (GraphicsPath p = Rounded.Path(r, 12))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(26, accent)))
                        g.FillPath(b, p);
                    using (Pen pen = new Pen(Color.FromArgb(80, accent)))
                        g.DrawPath(pen, p);
                }

                TextRenderer.DrawText(g, items[i], Font,
                    new Rectangle(r.X, r.Y, r.Width, r.Height), Theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

                x += w + 8;
                if (x > Width) break;
            }
        }
    }

    /// A sidebar entry: a glyph, a label, and a pink highlight plus a left accent bar
    /// when it is the active page.
    /// ④ 选中特效：一个圆角高亮 + 左侧竖条，整体在导航项之间滑动。
    ///
    /// 不用"每一项各画各的"，因为那样两条高亮会同时存在、也做不出连续滑动。这里只有一块，
    /// 位置从旧项移到新项，缓动是先加速后减速（ease-in-out），最后一帧强制取准确值，保证
    /// 不会停在两个选项中间。
    class NavSlider : Control
    {
        public float T = 0f;          // 0..1 缓动进度
        public int FromTop = 0, ToTop = 0, ItemH = 32;

        public NavSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Enabled = false;
        }

        public void SnapTo(int top, int h)
        {
            FromTop = ToTop = top; ItemH = h; T = 1f;
            Location = new Point(0, top);
            Size = new Size(Width, h);
            Invalidate();
        }

        public void SlideTo(int top, int h)
        {
            FromTop = Top; ToTop = top; ItemH = h; T = 0f;
            Size = new Size(Width, h);
        }

        public void Apply(float t)
        {
            T = t;
            // ease-in-out：先加速后减速
            float e = t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
            int y = FromTop + (int)Math.Round((ToTop - FromTop) * e);
            if (t >= 1f) y = ToTop;            // 最后一帧取准确值，不停在半路
            Location = new Point(0, y);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Rounded.Path(r, 10))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(48, Theme.Accent)))
                g.FillPath(b, p);

            int full = Height - 18;
            if (full > 2)
            {
                Rectangle bar = new Rectangle(0, 9, 4, full);
                using (GraphicsPath p = Rounded.Path(bar, 2))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, p);
            }
        }
    }
    class NavButton : Control
    {
        bool hover = false;
        bool active = false;
        public string Glyph = "";

        /// ④ 导航切换动画的进度 0..1 (UiAnim)：竖条从中间长出来，高亮同步淡入。
        public float Grow = 1f;

        /// ④ 滑动特效：位置是【共用】的，每一项只画落在自己范围内的那一段。
        ///
        /// 之前把滑块做成一个独立控件浮在上面，结果压住的那一项文字被擦掉了 —— WinForms 的
        /// 透明只让【父容器】重画，不会让被盖住的兄弟控件重画。改成共用数据、各自绘制，就
        /// 不存在"盖住"这回事。
        public static bool GlideOn = false;

        /// 让本项背景真透明：这样父容器画的滑块能从底下透出来，一整块、无缝。
        public void MakeTransparent()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        public static int GlideTop = 0;
        public static int GlideH = 0;

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            Height = 40;
        }

        public bool Active
        {
            get { return active; }
            set { if (active == value) return; active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            if (!GlideOn && (active || hover))
            {
                using (GraphicsPath p = Rounded.Path(r, 10))
                using (SolidBrush b = new SolidBrush(active
                        ? Color.FromArgb(48, Theme.Accent) : Color.FromArgb(22, Theme.Accent)))
                    g.FillPath(b, p);
            }
            if (!GlideOn && active)
            {
                int full = Height - 18;
                int barH = (int)(full * Grow); if (barH < 1) barH = 1;
                Rectangle bar = new Rectangle(0, 9 + (full - barH) / 2, 4, barH);
                using (GraphicsPath p = Rounded.Path(bar, 2))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, p);
            }

            TextRenderer.DrawText(g, Glyph, Theme.Body, new Rectangle(16, 0, 26, Height),
                active ? Theme.Accent : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, Text, active ? Theme.Head : Font,
                new Rectangle(44, 0, Math.Max(0, Width - 52), Height),
                active ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// Rounded gradient progress bar. Used for the level progress, which is derived
    /// from the real conversion count - never from an invented number.
    class LevelBar : Control
    {
        double value = 0;

        public LevelBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Height = 20;
        }

        public double Value
        {
            get { return value; }
            set
            {
                double v = value < 0 ? 0 : (value > 1 ? 1 : value);
                if (Math.Abs(v - this.value) < 0.0005) return;
                this.value = v;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath p = Rounded.Path(r, Height / 2))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(36, Theme.Accent)))
                g.FillPath(b, p);

            int w = (int)Math.Round(r.Width * value);
            if (w >= Height)
            {
                Rectangle fr = new Rectangle(0, 0, w, Height - 1);
                using (GraphicsPath p = Rounded.Path(fr, Height / 2))
                using (LinearGradientBrush b = new LinearGradientBrush(fr,
                           Color.FromArgb(242, 110, 158), Color.FromArgb(172, 132, 236), 0f))
                    g.FillPath(b, p);
            }

            using (GraphicsPath p = Rounded.Path(r, Height / 2))
            using (Pen pen = new Pen(Color.FromArgb(70, Theme.Accent)))
                g.DrawPath(pen, p);
        }
    }

    /// A titled card in the right rail, with an optional "管理" style link.
    class RailCard : CardPanel
    {
        public readonly Panel Body = new Panel();

        public RailCard(string title, string link, EventHandler onClick)
        {
            Radius = 14;
            Padding = new Padding(14, 9, 14, 11);

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 26;
            head.BackColor = Theme.Card;

            Label t = new Label();
            t.Text = title;
            t.Font = Theme.Head;
            t.ForeColor = Theme.Text;
            t.BackColor = Theme.Card;
            t.AutoSize = true;
            t.Location = new Point(0, 4);
            head.Controls.Add(t);

            if (!string.IsNullOrEmpty(link))
            {
                LinkLabel a = new LinkLabel();
                a.Text = link;
                a.Font = Theme.Tiny;
                a.LinkColor = Theme.Accent;
                a.ActiveLinkColor = Theme.AccentHover;
                a.BackColor = Theme.Card;
                a.AutoSize = true;
                a.Top = 7;
                a.LinkClicked += delegate { if (onClick != null) onClick(null, EventArgs.Empty); };
                head.Controls.Add(a);
                // explicit placement again: anchors resolved against the wrong width
                // once already in this project and pushed a control off-screen
                head.Resize += delegate { a.Left = Math.Max(0, head.Width - a.Width); };
            }

            Body.Dock = DockStyle.Fill;
            Body.BackColor = Theme.Card;

            // added Fill first, Dock=Top last: the later control docks first and keeps
            // the strip at the top
            Controls.Add(Body);
            Controls.Add(head);
        }
    }

    /// A style preset button: name plus a live sample of what it does.
    class StyleChip : Control
    {
        bool hover = false;
        bool active = false;
        public string Sample = "";

        // ★ 2026-10-22：自定义胶囊右上角画个 × ✗ 不用去右键里找删除 ✓
        public bool Deletable = false;
        public event EventHandler DeleteClicked;
        public bool IsOnDeleteHit(int x, int y) { return Deletable && x >= Width - 24 && y <= 22; }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && IsOnDeleteHit(e.X, e.Y))
            {
                if (DeleteClicked != null) DeleteClicked(this, EventArgs.Empty);
            }
        }

        public StyleChip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            Font = Theme.Small;
            Cursor = Cursors.Hand;
            Height = 46;
        }

        public bool Active
        {
            get { return active; }
            set { if (active == value) return; active = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color accent = active ? Theme.Accent : Color.FromArgb(196, 168, 186);

            // ★ 自定义的胶囊 ✗ 右上角一个浅浅的 × （悬停时变深）✓
            if (Deletable)
            {
                Color xc = hover ? Color.FromArgb(226, 96, 140) : Color.FromArgb(214, 170, 190);
                using (Pen xp = new Pen(xc, 1.4f))
                {
                    int cx = Width - 13, cy = 12;
                    g.DrawLine(xp, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(xp, cx - 4, cy + 4, cx + 4, cy - 4);
                }
            }
            using (GraphicsPath p = Rounded.Path(r, 12))
            {
                using (SolidBrush b = new SolidBrush(active
                        ? Color.FromArgb(38, Theme.Accent)
                        : (hover ? Color.FromArgb(20, Theme.Accent) : Color.FromArgb(255, 250, 253))))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(accent, active ? 1.6f : 1f))
                    g.DrawPath(pen, p);
            }

            TextRenderer.DrawText(g, Text, Theme.Head, new Rectangle(8, 5, Width - 16, 20),
                active ? Theme.Accent : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, Sample, Theme.Tiny, new Rectangle(6, 24, Width - 12, 18),
                Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix
                | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// ★ 模型列表的弹出面板 ✧ 圆角白底 + 淡粉描边 ✓
    ///
    /// 为什么要自己写一个：
    ///   普通 Panel 只能是方的 ✧ BackColor 也不能透明 ✗
    ///   直接用 CardPanel 又会带上猫爪装饰和卡片逻辑 ✧ 太重 ✗
    ///   —— 所以只拿它的 Rounded.Path 自己画 ✓
    class ModelMenuPanel : Panel
    {
        public int Radius = 10;

        public ModelMenuPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            // 留 1px 当影子 ✧ 形状尺寸跟着控件走 ✓
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Rounded.Path(r, Radius))
            {
                if (p.PointCount == 0) return;
                using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Line, 1f)) g.DrawPath(pen, p);
            }
        }
    }

    class RoundButton : Button
    {
        /// Optional small image drawn to the left of the label (see VecIcon).
        public Image Icon;

        public int Radius = 9;
        public bool Primary = false;

        // ★ 2026-10-22：可选的右上角 × ✗ 组合胶囊用 ✓
        public bool ShowDelete = false;
        public event EventHandler DeleteClicked;
        public bool IsOnDeleteHit(int x, int y) { return ShowDelete && x >= Width - 24 && y <= 22; }

        bool hover = false;
        bool down = false;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            down = false; Invalidate(); base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && IsOnDeleteHit(e.X, e.Y))
            {
                if (DeleteClicked != null) DeleteClicked(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Color fill;
            if (Primary) fill = down ? Theme.AccentDown : (hover ? Theme.AccentHover : Theme.Accent);
            else fill = down ? Theme.Soft : (hover ? Theme.SoftHover : Theme.Card);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Rounded.Path(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (!Primary)
                    using (Pen pen = new Pen(Theme.Line)) g.DrawPath(pen, p);
            }

            // TextRenderer, NOT Graphics.DrawString.
            //
            // Switched to GDI+ for a round in the hope of getting colour emoji in the button.
            // That was a dead end - System.Drawing cannot draw colour emoji at all, by either
            // path (measured, see emoji-art-test.png) - so the change bought nothing and would
            // have subtly altered the glyphs of every button in the app. Reverted.
            // ★ 自定义组合的 ×
            if (ShowDelete)
            {
                Color xc = hover ? Color.FromArgb(226, 96, 140) : Color.FromArgb(214, 170, 190);
                using (Pen xp = new Pen(xc, 1.4f))
                {
                    int cx = Width - 13, cy = 12;
                    g.DrawLine(xp, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(xp, cx - 4, cy + 4, cx + 4, cy - 4);
                }
            }
            Color fg = Primary ? Color.White : Theme.Text;
            if (Icon == null)
            {
                TextRenderer.DrawText(g, Text, Font, r, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            else
            {
                // icon and label centred together as one unit, so the button does not look
                // like a label that happens to have a picture stuck on the left
                Size ts = TextRenderer.MeasureText(Text, Font);
                int gap = 7;
                int total = Icon.Width + gap + ts.Width;
                int ix = (Width - total) / 2;
                g.DrawImage(Icon, ix, (Height - Icon.Height) / 2, Icon.Width, Icon.Height);
                TextRenderer.DrawText(g, Text, Font,
                    new Rectangle(ix + Icon.Width + gap, 0, ts.Width + 4, Height), fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPrefix);
            }
        }
    }

    class TabButton : Button
    {
        public bool Active = false;

        bool hover = false;

        public TabButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Bg;
            Font = Theme.Head;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Color fill = Active ? Theme.Accent : (hover ? Theme.Soft : Theme.Bg);
            Color fg = Active ? Color.White : Theme.Muted;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Rounded.Path(r, 17))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
            }

            TextRenderer.DrawText(g, Text, Font, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // ---------------------------------------------------------------- main window

    class MainForm : Form
    {
        readonly App app;

        bool loading = false;
        string railSig = "";

        Label statusHead;
        Label statusDetail;
        Label statusWarn;
        Label statusSign;   // ★ 2026-10-18：今日喵签 ✓
        Button langBtn;     // ★ 2026-10-18：语言切换按钮 ✗ 要在 Shown 里量宽度 ✓
        StatusDot statusDot;
        PillBar statusPills;
        Label headerFace;
        Label headerMood;
        CardPanel headerBubble;
        Label headerIcon;
        StatCard[] statCards;
        // the 界面 switches on the 设置 page
        ToggleRow setUiTopBar, setUiCards, setUiLab, setUiKao;
        ToggleRow setUiNavIcon, setUiLevel2, setUiColor, setUiPaw, setUiMood, setUiAnim;
        TextBox setAnimMs;         // 动画时长（毫秒）
        TextBox setSlidePct;       // 滑块比例（%），100 = 和淡化一样长
        FieldBox testIn;
        FieldBox testOut;
        SmoothTextView rulesView;
        ToggleRow chkPause;
        ToggleRow chkAuto;
        ToggleRow chkRun;
        Panel pageTest;
        Panel pageRules;
        NavButton[] nav;
        Panel[] pages;
        Panel pageStyle;
        Panel pageStats;
        Panel pageKaomoji;
        Panel pageSettings;
        Panel pageNews;
        Panel pageAbout;
        Panel contentHost;
        SmoothTextView newsBox;
        Label newsStatus;
        SmoothPanel railRulesBody;
        SmoothPanel railKaomojiBody;
        SmoothPanel pageStatList;
        StyleChip[] chips;
        StyleChip[] pageChips;
        StyleChip[] pageToneChips;
        ToggleRow tglStack;
        Label noteStack;
        Panel comboHost;
        static readonly Random rndStyle = new Random();
        StyleChip[] levelChips;
        SmoothPanel kaoListBody;
        TextBox kaoAddBox;
        TextBox setHotkey1;
        TextBox setHotkey2;
        TextBox setHotkey3;
        TextBox setDebounce;
        TextBox setWhitelist;
        ToggleRow setSmartTone;
        RoundButton setModelFile;      // 模型选择按钮 ✓
        Panel modelMenu;               // 展开的那个列表 ✧ null = 没展开 ✓
        ToggleRow setModelAuto;        // ★ 自动加载开关 ✓
        ToggleRow setTinyMood;         // ★ 语气小模型开关（clf.bin ✧ 7.3 MB）✓
        string pickedModelFile = "";   // 当前选的 gguf 文件名 ✧ 空 = 自动（用最大的）✓
        TextBox setEndpoint;
        TextBox setModel;
        Label llmStatus;

        // ★ 2026-10-31：大模型改写 ✓
        RoundButton setRewriteModeBtn;
        TextBox setRewriteEp, setRewriteModel, setRewriteKey;
        Label rewriteStatus;
        Label styleCurrent;
        Label testCount;
        Label toneSource;
        Label pageStatBig;
        Label pageLevelHint;
        LevelBar pageLevelBar;
        Label statToday;
        Label statTotal;
        Label statLevel;
        Label statLevelHint;
        LevelBar levelBar;

        /// A style preset. 外观 styles only swap the decoration; 语气 styles also wrap the
        /// whole message with an opener and a closer, which is what actually changes how
        /// the sentence reads. The two kinds are mutually exclusive in the UI, because
        /// both own the same 后缀 / 颜文字 lines.
        /// 把软件自带的每一个风格都登记给引擎。程序入口调一次。
        ///
        /// ★ 放在入口（而不是 MainForm 构造里）：这样【所有】启动路径都覆盖到 ——
        ///   正常开界面、--shot、--film、--trace、--selftest 全都算。之前放在
        ///   界面构造里，命令行那些模式就不播种，验证起来会和实际行为对不上。
        public static void SeedEngineStyles()
        {
            try
            {
                foreach (StyleDef d in LookStyles) Engine.SeedKnownClosers(StyleClosers(d), d.Suffix, d.Kaomoji);
                foreach (StyleDef d in ToneStylesNow) Engine.SeedKnownClosers(StyleClosers(d), d.Suffix, d.Kaomoji);
            }
            catch { }
        }

        /// 把一个风格里所有的关闭语池拼成一个数组，交给引擎登记。
        /// 引擎那边要的是"这个风格可能写出哪些整句"，所以二十个池都要。
        static string[] StyleClosers(StyleDef d)
        {
            List<string> all = new List<string>();
            string[][] pools = new string[][] {
                d.Closers, d.QuestionClosers, d.ExclaimClosers, d.HappyClosers,
                d.SadClosers, d.AngryClosers, d.GreetingClosers, d.FarewellClosers,
                d.LoveClosers, d.PraiseClosers, d.ThanksClosers, d.ApologyClosers,
                d.CongratClosers, d.InviteClosers, d.SurpriseClosers, d.FearClosers,
                d.TiredClosers, d.BoredClosers, d.DisappointedClosers,
                d.ComfortClosers, d.CharmClosers
            };
            foreach (string[] p in pools) if (p != null) all.AddRange(p);
            return all.ToArray();
        }

        public class StyleDef
        {
            public string Name;
            public string Template = "{后缀}{颜文字}";
            public string Suffix = "喵~";
            public string[] Kaomoji;
            public string[] Openers = new string[0];
            public string[] Closers = new string[0];
            public string[] QuestionClosers = new string[0];
            public string[] ExclaimClosers = new string[0];
            public string[] HappyClosers = new string[0];
            public string[] SadClosers = new string[0];
            public string[] AngryClosers = new string[0];
            public string[] GreetingClosers = new string[0];
            public string[] FarewellClosers = new string[0];
            public string[] LoveClosers = new string[0];
            public string[] PraiseClosers = new string[0];
            public string[] ThanksClosers = new string[0];
            public string[] ApologyClosers = new string[0];
            public string[] CongratClosers = new string[0];
            public string[] InviteClosers = new string[0];
            public string[] SurpriseClosers = new string[0];
            public string[] FearClosers = new string[0];
            public string[] TiredClosers = new string[0];
            public string[] BoredClosers = new string[0];
            public string[] DisappointedClosers = new string[0];
            public string[] ComfortClosers = new string[0];
            public string[] CharmClosers = new string[0];

            public StyleDef(string name, string suffix, string[] kao)
            {
                Name = name; Suffix = suffix; Kaomoji = kao;
            }

            public StyleDef(string name, string suffix, string[] kao,
                            string[] openers, string[] closers,
                            string[] questionClosers, string[] exclaimClosers,
                            string[] happyClosers, string[] sadClosers, string[] angryClosers)
            {
                Name = name; Suffix = suffix; Kaomoji = kao;
                Openers = openers; Closers = closers;
                QuestionClosers = questionClosers; ExclaimClosers = exclaimClosers;
                HappyClosers = happyClosers; SadClosers = sadClosers; AngryClosers = angryClosers;
            }

            public StyleDef(string name, string suffix, string[] kao,
                            string[] openers, string[] closers,
                            string[] questionClosers, string[] exclaimClosers,
                            string[] happyClosers, string[] sadClosers, string[] angryClosers,
                            string[] greetingClosers, string[] farewellClosers,
                            string[] loveClosers, string[] praiseClosers)
            {
                Name = name; Suffix = suffix; Kaomoji = kao;
                Openers = openers; Closers = closers;
                QuestionClosers = questionClosers; ExclaimClosers = exclaimClosers;
                HappyClosers = happyClosers; SadClosers = sadClosers; AngryClosers = angryClosers;
                GreetingClosers = greetingClosers; FarewellClosers = farewellClosers;
                LoveClosers = loveClosers; PraiseClosers = praiseClosers;
            }

            /// One dictionary instead of twenty positional arguments. A pool that is not
            /// listed comes out empty and the engine falls back to 结尾 for it, which is the
            /// behaviour we want for styles that only bother with a few moods.
            public Dictionary<string, string[]> ToPools()
            {
                Dictionary<string, string[]> d = new Dictionary<string, string[]>();
                d["opener"] = Openers;
                d["closer"] = Closers;
                d["questioncloser"] = QuestionClosers;
                d["exclaimcloser"] = ExclaimClosers;
                d["happycloser"] = HappyClosers;
                d["sadcloser"] = SadClosers;
                d["angrycloser"] = AngryClosers;
                d["greetingcloser"] = GreetingClosers;
                d["farewellcloser"] = FarewellClosers;
                d["lovecloser"] = LoveClosers;
                d["praisecloser"] = PraiseClosers;
                d["thankscloser"] = ThanksClosers;
                d["apologycloser"] = ApologyClosers;
                d["congratcloser"] = CongratClosers;
                d["invitecloser"] = InviteClosers;
                d["surprisecloser"] = SurpriseClosers;
                d["fearcloser"] = FearClosers;
                d["tiredcloser"] = TiredClosers;
                d["boredcloser"] = BoredClosers;
                d["disappointedcloser"] = DisappointedClosers;
                d["comfortcloser"] = ComfortClosers;
                d["charmcloser"] = CharmClosers;
                return d;
            }
        }

        static readonly StyleDef[] LookStyles = new StyleDef[]
        {
            // ★ 2026-10-17：每个风格自带 25 个颜文字（原来只有 3~6 个 ✗ 聊两下就重复）
            //   来源：手写经典 + bilibili 颜文字合集里按字符特征分堆挑的 ✓
            new StyleDef("萌系", "喵~", new string[] { "(=^･ω･^=)", "(=^‥^=)", "(´･ω･`)", "ฅ^•ﻌ•^ฅ", "(=^･ｪ･^=)", "(=①ω①=)", "ฅ(•ㅅ•❀)ฅ", "(=TェT=)", "(ΦωΦ)", "(=｀ェ´=)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)" }),
            new StyleDef("元气", "喵！", new string[] { "(≧▽≦)", "٩(๑>ω<๑)۶", "(๑•̀ㅂ•́)و✧", "ヽ(•‿•)ノ", "(*^▽^*)", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "o(≧▽≦)o", "＼(^o^)／", "v(≧∇≦)v", "(*´▽‘*)", "(′▽`)", "(//▽//)", "(⺻▽⺻ )", "(〃＾∇＾)", "(*≧m≦*)", "(。⌒∇⌒)", "(ﾉ≧∀≦)", "(^∇^*)", "(^▽^)", "(●´▽｀●)", "(〃’▽’〃)", "(＠゜▽゜)", "( ^▽^ )", "( ¯▽¯；)", "(╯▽╰)" }),
            new StyleDef("慵懒", "……喵", new string[] { "(￣o￣)", "(=_=)", "(˘ω˘)", "(-.-)Zzz", "zzZ", "(￣﹃￣)", "(∪｡∪)", "(－_－)", "(=ω=)", "(︶ω︶)", "( ˘ ³˘)", "(๑ºั ³ ˘๑)", "(ㆀ˘･з･˘)", "(￢_￢)", "(๛˘³˘)", "(..•˘_˘•..)", "(๑˘ ˘๑)", "(¯﹃¯ԅ)", "(¯﹃¯)", "( ╯-_-)", "(ノ—_—)", "(ノ*-_-*)", "(/= _ =)", "(╯   #-_-)", "( ╯#​   -_-)" }),
            new StyleDef("撒娇", "喵♡", new string[] { "(´,,•ω•,,)♡", "♡(>ω<♡)", "(づ｡◕‿‿◕｡)づ", "( ｡ớ ₃ờ)", "( ˘ ³˘)", "(๑ºั ³ ˘๑)", "(๑ơ ₃ ơ)", "(๑•́ ₃•̀๑)", "(*´▽‘*)", "(◍•ᴗ•◍)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)" }),
            new StyleDef("冷淡", "喵。", new string[] { "(・_・)", "(￣ー￣)", "(￣▽￣)", "(-_-)", "(¬_¬)", "(￢_￢)", "(￣〜￣)", "(・ω・)", "( =ω=)", "(´-ω-`)", "(;￣◇￣)", "(¬､¬)", "( ￣д￣；)", "(;¬_¬)", "(￣︶￣)", "(づ￣ 3￣)", "(￣ε￣)", "(￣^￣)", "(╯￣Д￣)", "(￣ω￣;)", "(=￣ω￣=)", "(*￣▽￣*)", "(￣ε￣*)", "(；￣Д￣)", "(*￣m￣)" }),
            new StyleDef("温柔", "喵~", new string[] { "(｡･ω･｡)", "( ´ ▽ ` )", "(◡‿◡)", "(´･ω･`)", "(๑´ω`๑)", "(﹡ˆᴗˆ﹡)", "(◍•ᴗ•◍)", "(ᵕ̈ )", "( ˘ω˘ )", "(´,,•ω•,,)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)" }),
            new StyleDef("害羞", "喵…", new string[] { "(/ω＼)", "(〃▽〃)", "(⁄ ⁄•⁄ω⁄•⁄ ⁄)", ">///<", "(⺣◡⺣)♡", "(≧////≦)", "(〃ω〃)", "(•́ ﹏ •̀)", "(⁄˘⁄ ⁄ω⁄ ⁄˘⁄)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)", "(๑•́ ₃•̀๑)" }),
            new StyleDef("中二", "喵！", new string[] { "✧･ﾟ: *✧･ﾟ:*", "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "٩(ˊᗜˋ*)و", "(ﾉ>ω<)ﾉ", "(≧∇≦)b", "☆*:.｡.o(≧▽≦)o.｡.:*☆", "(ﾉ≧∇≦)ﾉ", "(*ﾟｪﾟ*)", "(,,#ﾟДﾟ)", "(☆ﾟ∀ﾟ)", "(*ﾟ▽ﾟ*)", "(＞。☆)", "(• - •)", "(●｀∀´●)", "(●’◡’●)", "(*´▽‘*)", "(′▽`)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(＾∀＾●)", "(●´∀｀)", "( ˘ ³˘)", "(๑•́ ₃•̀๑٥)", "(๑ºั ³ ˘๑)" }),
            new StyleDef("天然呆", "…喵？", new string[] { "(・_・?)", "(´･ω･`)?", "(°ロ°)", "(*ﾟﾛﾟ)", "(・ω・)", "(´・ω・`)", "(⊙_⊙)", "(￣ﾛ￣)", "(ㅇㅅㅇ)", "(・◇・)", "(๑*д*๑)", "(;´Д｀ﾍ)", "(っ °Д °;)", "( ° △ °|||)", "(;￢д￢)", "(//̀Д/́/)", "(｀д′)", "(ノ=Д=)", "(╯°Д°)", "(●´д｀)", "(.◕ฺˇд ˇ◕ฺ;)", "(๑°3°๑)", "( ͡° ͜ʖ ͡°)", "(°ˊДˋ°)", "(ﾉ´д｀)" }),
            new StyleDef("毒舌", "喵。", new string[] { "(￣〜￣)", "(￣ー￣)y-~~", "(・∀・)", "(￣▽￣)ノ_,", "(¬､¬)", "(￢_￣)", "(￣ｰ￣)", "(・_・)", "(-_-)", "(´-ω-)y-~~", "(;￣◇￣)", "( ￣д￣；)", "(;¬_¬)", "(￣︶￣)", "(づ￣ 3￣)", "(￣ε￣)", "(￣^￣)", "(╯￣Д￣)", "(￣▽￣)", "(￣ω￣;)", "(=￣ω￣=)", "(*￣▽￣*)", "(￣ε￣*)", "(；￣Д￣)", "(*￣m￣)" }),
            new StyleDef("口癖", "喵帕斯", new string[] { "(˘≧∇≦˘)", "＼(≧∀≦)／", "(๑˘∇˘๑)", "(=^･ω･^=)", "(ﾉ≧∀≦)ﾉ", "(๑•̀ㅂ•́)و✧", "ヽ(•‿•)ノ", "٩(ˊᗜˋ*)و", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)", "(๑•́ ₃•̀๑)", "(๑•̀ㅁ•́ฅ)" }),
            // ★ 2026-10-18：13 个凑不满 5 行（3 列 × 5 行 = 15）✗ 补两个 ✓
            new StyleDef("病娇", "喵…", new string[] { "(´∀｀)♡", "(◕‿◕)", "(‾◡◝)", "(｡•̀ᴗ-)✧", "(´｡• ᵕ •｡`)", "(๑´ㅂ`๑)", "♡(◡‿◡✿)", "(¬‿¬)", "(◕‿◕✿)", "(´,,•ω•,,)♡", "(๑♡⌓♡๑)", "(◍•ᴗ•◍)❤", "(´｡• ω •｡`)", "(๑•́ ₃ •̀๑)", "(´-ω-`)", "(´,,•ω•,,)", "(๑>ᴗ<๑)", "( ˘ ³˘)♥", "(｡♥‿♥｡)", "(◕ᴗ◕✿)", "( ´ ꒳ ` )", "(๑˃ᴗ˂)ﻭ", "(´｡• ᵕ •｡`) ♡", "(♡˙︶˙♡)", "(灬º‿º灬)♡" }),
            new StyleDef("热血", "喵！", new string[] { "(≧∇≦)ﾉ", "٩(ˊᗜˋ*)و", "(๑•̀ㅂ•́)و✧", "ヽ(•̀ω•́ )ゝ", "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "o(≧口≦)o", "٩(๑>◡<๑)۶", "(*≧▽≦)", "(ﾉ>ω<)ﾉ", "(≧◇≦)", "(๑>ᴗ<๑)", "(๑•̀ㅁ•́ฅ)", "٩(๑❛ᴗ❛๑)۶", "(๑˃̵ᴗ˂̵)و", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "(≧∇≦*)", "(๑•̀ㅂ•́)ﾉ➹♡", "(๑•̀ㅂ•́)و✧", "(ﾉ≧∇≦)ﾉ", "(๑•̀ㅁ•́๑)✧", "٩(๑˃̶͈̀ω˂̶͈́)۶", "(๑˃ᴗ˂)و", "(☆▽☆)", "٩(◕‿◕)۶" }),
            new StyleDef("颜文字在前", "喵~", new string[] { "(=^･ω･^=)", "ฅ^•ﻌ•^ฅ", "(´･ω･`)", "(=^‥^=)", "(≧▽≦)", "(๑•̀ㅂ•́)و✧", "(=TェT=)", "(ΦωΦ)", "(• - •)", "( ‘-ωก̀ )", "(ฅ´ω`ฅ)", "(๑•́ ₃•̀๑٥)", "( •́ .̫ •̀ )", "(●′ω`●)", "(´,,•ω•,,‘)", "(´；ω；‘)", "(›´ω`‹ )", "(๑•ั็ω•็ั๑)", "( ˘•ω•˘ )", "( •̀∀•́ )", "(๑•̀ㅂ•́)", "(ง •̀_•́)", "(*•̀ㅂ•́)", "(๑•́ ₃•̀๑)", "(๑•̀ㅁ•́ฅ)" }),
            new StyleDef("只有喵~", "喵~", new string[0])
        };

        /// ★ 外观风格的英文版 ★
        ///
        /// 和中文那套只差一件事：后缀。颜文字是通用的，照搬。
        /// 后缀不写前导空格 —— 程序会自动补（后缀首字符是 ASCII 字母时补一个）。
        static readonly StyleDef[] LookStylesEn = new StyleDef[]
        {
            new StyleDef("Cute", "meow~", new string[] { "(=^･ω･^=)", "(=^‥^=)", ":3", ">w<", "uwu", "(＾▽＾)" }),
            new StyleDef("Cheerful", "meow!", new string[] { "(≧▽≦)", ":D", "＼(^o^)／", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "o(≧▽≦)o" }),
            new StyleDef("Sleepy", "...meow", new string[] { "(=_=)", "(-.-)Zzz", "(￣o￣)", "zzZ", "(˘ω˘)" }),
            new StyleDef("Clingy", "meow<3", new string[] { "(´,,•ω•,,)♡", "♡(>ω<♡)", "(っ´ω`c)", "(づ｡◕‿‿◕｡)づ" }),
            new StyleDef("Cold", "meow.", new string[] { "(-_-)", "(￣ー￣)", "(・_・)", "(￣▽￣)" }),
            new StyleDef("Gentle", "meow~", new string[] { "(｡・ω・｡)", "( ´ ▽ ` )", "(◡‿◡)" }),
            new StyleDef("Shy", "meow...", new string[] { "(/ω＼)", "(\\///)", ">///<" }),
            new StyleDef("Chuuni", "meow!", new string[] { "✧･ﾟ: *✧･ﾟ:*", "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "٩(ˊᗜˋ*)و" }),
            new StyleDef("Airhead", "...meow?", new string[] { "(・_・?)", "(´･ω･`)?", "(*ﾟﾛﾟ)" }),
            new StyleDef("Sharp", "meow.", new string[] { "(￣〜￣)", "(￣ー￣)y-~~", "(・∀・)" }),
            new StyleDef("Tic", "nyas~", new string[] { "(˘≧∇≦˘)", "＼(≧∀≦)／", "(๑˘∇˘๑)" }),
            new StyleDef("Yandere", "nya...", new string[] { "(´∀｀)♡", "(◕‿◕)", "(‾◡◝)", "(´｡• ᵕ •｡`)", "(๑´ㅂ`๑)", "♡(◡‿◡✿)", "(¬‿¬)", "(◕‿◕✿)", "(◍•ᴗ•◍)❤", "(๑>ᴗ<๑)", "(◕ᴗ◕✿)", "(♡˙︶˙♡)" }),
            new StyleDef("Hyped", "meow!", new string[] { "(≧∇≦)ﾉ", "٩(ˊᗜˋ*)و", "(๑•̀ㅂ•́)و✧", "ヽ(•̀ω•́ )ゝ", "(ﾉ≧∀≦)ﾉ", "o(≧口≦)o", "(*≧▽≦)", "(ﾉ>ω<)ﾉ", "(≧◇≦)", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧", "(☆▽☆)", "٩(◕‿◕)۶" }),
            new StyleDef("Kaomoji first", "meow~", new string[] { "(=^･ω･^=)", ":3", "(´･ω･`)", ">w<" }),
            new StyleDef("Just meow~", "meow~", new string[0])
        };

        /// ★★★ 2026-10-18：自定义风格 ★★★
        ///
        /// 存在程序目录的 style-custom.txt ✗ 格式：
        ///     [名字]
        ///     后缀 = 喵★
        ///     颜文字 = (｡･ω･｡)
        ///
        /// —— ★ 为什么不塞进 rules.txt ✗ 因为风格是【一组】参数
        ///    而规则文件里同名的键会互相覆盖 ✓
        static string CustomStyleFile
        {
            // ★ App.BaseDir 是实例属性 ✧ 这里是 static ✧ 用 AppDomain 取程序目录 ✓
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "style-custom.txt"); }
        }

        /// ★ 读出所有自定义风格 ✗ 文件不在 / 解析失败一律返回空数组 ✓
        static StyleDef[] CustomLooks()
        {
            try
            {
                string f = CustomStyleFile;
                if (!File.Exists(f)) return new StyleDef[0];

                List<StyleDef> outp = new List<StyleDef>();
                string name = null;
                string suffix = "喵~";
                List<string> kaos = new List<string>();

                Action flush = delegate
                {
                    if (name != null && kaos.Count > 0)
                        outp.Add(new StyleDef(name, suffix, kaos.ToArray()));
                    name = null;
                    suffix = "喵~";
                    kaos = new List<string>();
                };

                foreach (string raw in File.ReadAllLines(f, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        flush();
                        name = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (name == null || v.Length == 0) continue;
                    if (k == "后缀" || k == "suffix") suffix = v;
                    else if (k == "颜文字" || k == "kaomoji") kaos.Add(v);
                }
                flush();
                // ★ 调试：看看读到了几个自定义风格 ✓
                App.LogStatic("custom styles: " + outp.Count + " (" + f + ")");
                return outp.ToArray();
            }
            catch { return new StyleDef[0]; }
        }

        /// ★ 把一个自定义风格写进文件（追加）✓
        static bool AppendCustomLook(string name, string suffix, List<string> kaos)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string f = CustomStyleFile;
                if (File.Exists(f))
                {
                    string old = File.ReadAllText(f, Encoding.UTF8);
                    sb.Append(old);
                    if (old.Length > 0 && !old.EndsWith("\n")) sb.Append("\r\n");
                }
                else
                {
                    sb.Append("# 喵化助手 自定义风格\r\n");
                    sb.Append("# 格式：[名字] 换行 后缀 = … 换行 颜文字 = …\r\n\r\n");
                }
                sb.Append("[" + name + "]\r\n");
                sb.Append("后缀 = " + suffix + "\r\n");
                foreach (string k in kaos) sb.Append("颜文字 = " + k + "\r\n");
                sb.Append("\r\n");
                File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { App.LogStatic("custom style save failed: " + ex.Message); return false; }
        }

        /// ★ 删掉一个自定义风格（按名字）✗ 重写整个文件 ✓
        static bool RemoveCustomLook(string name)
        {
            try
            {
                string f = CustomStyleFile;
                if (!File.Exists(f)) return false;
                StringBuilder sb = new StringBuilder();
                bool skipping = false;
                foreach (string raw in File.ReadAllLines(f, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        skipping = (line.Substring(1, line.Length - 2).Trim() == name);
                        if (!skipping) sb.Append(raw + "\r\n");
                        continue;
                    }
                    if (!skipping) sb.Append(raw + "\r\n");
                }
                File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { App.LogStatic("custom style remove failed: " + ex.Message); return false; }
        }

        /// ★★★ 2026-10-18：自定义语气 ★★★
        ///   存在 tone-custom.txt ✗ 只填 后缀/颜文字/开头/结尾 就够
        ///   —— ★ 剩下 21 类场景池留空 ✗ 引擎自动回退 ✓
        public static string CustomToneFile
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tone-custom.txt"); }
        }

        public static StyleDef[] CustomTones()
        {
            try
            {
                string f = CustomToneFile;
                if (!File.Exists(f)) return new StyleDef[0];

                List<StyleDef> outp = new List<StyleDef>();
                string name = null;
                string suffix = "喵~";
                List<string> kaos = new List<string>();
                List<string> opens = new List<string>();
                List<string> closes = new List<string>();

                Action flush = delegate
                {
                    if (name != null && kaos.Count > 0)
                    {
                        // \u2605 StyleDef \u6ca1\u6709 9 \u4e2a\u53c2\u6570\u7684\u6784\u9020\u51fd\u6570 \u2717 \u7528 3 \u4e2a\u53c2\u6570\u7684\u518d\u8d4b\u503c \u2713
                        StyleDef d = new StyleDef(name, suffix, kaos.ToArray());
                        d.Openers = opens.ToArray();
                        d.Closers = closes.ToArray();
                        outp.Add(d);
                    }
                    name = null;
                    suffix = "喵~";
                    kaos = new List<string>();
                    opens = new List<string>();
                    closes = new List<string>();
                };

                foreach (string raw in File.ReadAllLines(f, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        flush();
                        name = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq < 0 || name == null) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (v.Length == 0) continue;
                    if (k == "后缀" || k == "suffix") suffix = v;
                    else if (k == "颜文字" || k == "kaomoji") kaos.Add(v);
                    else if (k == "开头" || k == "opener") opens.Add(v);
                    else if (k == "结尾" || k == "closer") closes.Add(v);
                }
                flush();
                return outp.ToArray();
            }
            catch { return new StyleDef[0]; }
        }

        public static bool AppendCustomTone(string name, string suffix,
            List<string> kaos, List<string> opens, List<string> closes)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string f = CustomToneFile;
                if (File.Exists(f))
                {
                    string old = File.ReadAllText(f, Encoding.UTF8);
                    sb.Append(old);
                    if (old.Length > 0 && !old.EndsWith("\n")) sb.Append("\r\n");
                }
                else
                {
                    sb.Append("# 喵化助手 自定义语气风格\r\n");
                    sb.Append("# 格式：[名字] 换行 后缀 = … 换行 颜文字 = … 换行 开头 = … 换行 结尾 = …\r\n\r\n");
                }
                sb.Append("[" + name + "]\r\n");
                sb.Append("后缀 = " + suffix + "\r\n");
                foreach (string k in kaos) sb.Append("颜文字 = " + k + "\r\n");
                foreach (string o in opens) sb.Append("开头 = " + o + "\r\n");
                foreach (string c in closes) sb.Append("结尾 = " + c + "\r\n");
                sb.Append("\r\n");
                File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { App.LogStatic("custom tone save failed: " + ex.Message); return false; }
        }

        public static bool RemoveCustomTone(string name)
        {
            try
            {
                string f = CustomToneFile;
                if (!File.Exists(f)) return false;
                StringBuilder sb = new StringBuilder();
                bool skipping = false;
                foreach (string raw in File.ReadAllLines(f, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        skipping = (line.Substring(1, line.Length - 2).Trim() == name);
                        if (!skipping) sb.Append(raw + "\r\n");
                        continue;
                    }
                    if (!skipping) sb.Append(raw + "\r\n");
                }
                File.WriteAllText(f, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { App.LogStatic("custom tone remove failed: " + ex.Message); return false; }
        }

        /// ★ 当前语言该用哪套外观风格 ★（2026-10-18：内置 + 自定义）
        public static StyleDef[] LookStylesNow
        {
            get
            {
                StyleDef[] b = Lang.English ? LookStylesEn : LookStyles;
                StyleDef[] c = CustomLooks();
                if (c.Length == 0) return b;
                StyleDef[] all = new StyleDef[b.Length + c.Length];
                b.CopyTo(all, 0);
                c.CopyTo(all, b.Length);
                return all;
            }
        }

        /// ★ 当前语言该用哪套语气风格 ★
        ///
        /// ★★★ 长度必须和中文那套一致 ★★★
        ///   风格卡片是【建页面时】按当时的语言建的 ✗ 数量固定 ✓
        ///   而点击时的校验用【当前语言】的数组长度 ✓
        ///   如果英文数组更短 ✗ 后面几项全部越界 ✗ 表现就是"点不动"✓
        ///
        ///   所以这里【逐项回退】：英文有数据用英文 ✗ 没写就暂时用中文 ✓
        ///   —— ★ 没写英文的那几个点下去台词还是中文 ✗ 
        ///      但至少能点 ✗ 名字也是英文 ✓
        public static StyleDef[] ToneStylesNow
        {
            get
            {
                StyleDef[] c = CustomTones();
                StyleDef[] baseArr;
                if (!Lang.English) baseArr = ToneStyles;
                else
                {
                    if (ToneStylesEn == null || ToneStylesEn.Length == 0) return ToneStyles;
                    StyleDef[] outp = new StyleDef[ToneStyles.Length];
                    for (int i = 0; i < ToneStyles.Length; i++)
                        outp[i] = i < ToneStylesEn.Length ? ToneStylesEn[i] : ToneStyles[i];
                    baseArr = outp;
                }
                if (c.Length == 0) return baseArr;
                StyleDef[] all = new StyleDef[baseArr.Length + c.Length];
                baseArr.CopyTo(all, 0);
                c.CopyTo(all, baseArr.Length);
                return all;
            }
        }

        // Written after reading how these archetypes actually talk (萌娘百科 傲娇/口癖 条目).
        //
        // The one rule that matters for 傲娇: it is a TWO-PART line. The first half denies
        // ("才不是为了你才说的呢"), the second half quietly gives away the truth
        // ("……真的不是哦"). A version with only the denial reads as plain hostility - that
        // was the mistake in the first draft. The stammer (才、才 / 人、人家 / 笨、笨蛋) and
        // the self-reference 人家 are the other signature markers.
        //
        // 猫娘 phrasing uses the standard tics: 喵~ / 呜喵 / 喵哈哈 / trailing repetition.
        static readonly StyleDef[] ToneStyles = new StyleDef[]
        {
            new StyleDef("傲娇", "喵！",
                new string[] { "(￣^￣)", "(｀∀´)", "(￣ω￣)", "(｀・ω・´)", "(￣ヘ￣)", "(｀へ´)",
                               "(￣ε￣)", "(｀・∀・´)", "(￣ー￣)", "(=｀ω´=)" },
                new string[] { "哼，", "才、才不是……", "笨、笨蛋！", "人、人家才没有……", "别、别自作多情了！",
                               "……哼", "你、你听好了，", "真是的……", "唔……", "谁、谁要理你啊！",
                               "哼，我说啊，", "那个……", "哼，", "才、才不是呢，", "别、别误会，", "谁、谁稀罕，", "……切，", "那个……", "喂，", "听好了，", "笨蛋，", "……真拿你没办法，", "什么啊，", "谁、谁问了，", "我、我才没有，", "……不许笑，", "哼——", "别、别看，", "干嘛，", "有、有事吗，", "我才不是在等你，", "切，", "……那个，", "别、别多想，", "真是的，", "谁、谁要理你，", "……只是刚好，", "笨蛋，" },
                new string[] { "，才不是为了你才说的呢……真的不是哦", "，哼，别、别误会啊……我只是顺便提一下",
                               "，我、我才没有很高兴呢……才怪", "，笨、笨蛋……不过，还是谢谢你",
                               "，才、才不是担心你呢！……只是刚好想到", "，吵死了……但是，你说得对",
                               "，真没办法……那就听你的吧", "，哼，就、就这样吧……记得回我",
                               "，我可不是为了你才这样的……别想多了", "，你、你别自作多情啊……不过，随便你啦",
                               "，才、才没有在等你呢……只是刚好路过", "，笨蛋……下次，下次早点说啊",
                               "，哼，我才不在意呢……真的", "，……其实我，也不是不想听啦",
                               "，别、别得意啊……我、我只是心情好", "，真是的……拿你没办法", "，……才、才没有很在意呢", "，哼，随便你", "，……别、别想多了", "，我、我可没说……", "，……下次别让我等", "，哼，就这样吧", "，……才、才没有很高兴", "，哼，别得寸进尺", "，……就一次哦", "，你、你别乱想", "，我才没担心你", "，……算了，原谅你", "，……哼，也不讨厌啦", "，才、才不是为你", "，我、我走了", "，……别跟来", "，哼，记住了", "，……下次早点说", "，谁、谁要谢你", "，……笨蛋", "，别、别得意", "，切，就这样吧" },
                new string[] { "，你、你问这个干嘛……很重要吗", "，我我我才不知道呢！……好吧，我猜是",
                               "，谁、谁要告诉你啊……不过你可以猜", "，……你猜　我不敢说",
                               "，这种事别问我啦……真拿你没办法", "，哼，凭什么告诉你……除非你先说",
                               "，你、你自己想啊！……笨蛋", "，……很重要吗？那，那我说就是了",
                               "，问别人去啦……我、我又没说不说", "，我、我凭什么要回答你啊……你想知道？" },
                new string[] { "，吵、吵死了！……但是有点厉害", "，哼！……好吧，确实不错",
                               "，别、别大惊小怪的！……我也是刚知道", "，有什么好得意的嘛……其实很佩服",
                               "，……随你便啦　哼", "，哼，幼稚！……不过我陪你",
                               "，知道了知道了！……真啰嗦", "，别、别喊那么大声啦……听见了",
                               "，……真受不了你　但是，算了", "，好啦好啦，听见了……别喊了" },
                new string[] { "，哼、哼！才没有很高兴呢……偷偷笑一下", "，……算、算你厉害　就这一次哦",
                               "，别、别以为我会夸你哦……做得还行", "，勉、勉强算是件好事吧……恭喜",
                               "，……哼，还行吧　其实超开心", "，我、我才没有在笑呢！……真的",
                               "，……也没那么厉害啦　但是，谢谢", "，哼，下次可没这么好运……这次算你的",
                               "，别、别得意忘形啊……不过，恭喜你", "，……好啦，是挺不错的　就承认这一次" },
                // 难过 = the USER is sad. Every line has to be something the user would say
                // ABOUT THEMSELVES - denying it, or hinting they want company. The old lines
                // here ("过来，给你靠一下") were written from the comforter's side and read as
                // if someone else were sad. They moved to 安慰 below.
                new string[] { "，……才、才没有不开心呢。真的", "，……哼，我、我没事。别问了",
                               "，……才不需要人陪呢。真的不需要", "，……你、你要是闲着的话，陪我说说话也行",
                               "，笨、笨蛋，谁让你看出来的……", "，……我、我才没有难过。只是有点累",
                               "，……哼，你要是敢安慰我，我就……就……", "，……其实，也不是不能听你说两句",
                               "，……别、别管我。我一会儿就好了", "，……你、你什么都不用说。在就行" },
                new string[] { "，哼！我才不跟那种人一般见识……但你没错", "，别理他们了，讨厌！……我站你这边",
                               "，……那种人不值得你生气　真的", "，我、我帮你去说他！……虽然可能没用",
                               "，消消气嘛……虽然我也不爽", "，哼，我也很生气啊！……一起去骂他",
                               "，别气了，气坏了我怎么办……笨蛋", "，……要不要我去揍他一顿　我说真的",
                               "，那人真是讨厌死了……别理他", "，不理他们了，理我嘛……喂" },
                new string[] { "，哼，是你啊……有事吗", "，……嗯，好久不见", "，才、才没有在等你的消息呢",
                               "，哼，终于想起我了？", "，哦，是你啊……我、我刚好在忙",
                               "，……你好。就、就这样，没别的意思" },
                new string[] { "，哼，走吧走吧……路上小心", "，才、才没有舍不得呢……记得回我",
                               "，嗯……早点休息", "，那、那我先不打扰你了……有事叫我",
                               "，路上小心……笨蛋", "，……我会在的，别担心" },
                // 喜爱 = the USER is declaring it. These lines used to be written as replies
                // to being confessed to ("你先别说这种话"), which is backwards: the other
                // person has not said anything yet, the user opened with 我喜欢你.
                new string[] { "，……说、说出来了。别笑我", "，哼，就、就这样。没别的意思",
                               "，……你、你不用回答。我就是想说", "，笨、笨蛋，别装作没看见啊",
                               "，……听、听到了吗。就这样", "，……我、我说完了。你想笑就笑吧" },
                // 夸奖 = the USER is praising the other. The old lines were the ones a
                // tsundere says when SHE is being praised ("我本来就很厉害"), i.e. exactly
                // the wrong side of the conversation.
                new string[] { "，……真的，没骗你", "，哼，别、别得意啊。我很少夸人的",
                               "，……说、说出来了。就当没听见吧", "，笨、笨蛋，你知道就好",
                               "，……我可不是随便夸人的", "，哼，就、就这一次啊" })
            {
                // 感谢 / 道歉 = the USER is thanking or apologising. The old lines answered
                // those acts ("不客气", "知道错了就好"), so typing 谢谢你 produced the user
                // saying "what's there to thank me for" - the two sides were swapped.
                ThanksClosers = new string[] { "，……哼，真、真的帮到我了",
                                               "，别、别误会，我只是客气一下……才怪",
                                               "，……下次请你吃饭。就这样",
                                               "，哼，我、我记着了",
                                               "，……真的，谢谢。我说完了", "，……哼，算你有心", "，谢、谢就免了" },
                ApologyClosers = new string[] { "，……哼，我、我很少道歉的",
                                                "，别、别不理我啊……",
                                                "，……真的，我错了。就这样",
                                                "，笨、笨蛋，说句话啊",
                                                "，……我、我下次不会了", "，……哼，这次就算了", "，我、我可不是原谅你了" },
                CharmClosers = new string[] { "，……哼，就、就一下哦",
                                              "，才、才不是想你了呢……只是有点冷",
                                              "，……别、别问为什么。过来就是了",
                                              "，笨、笨蛋，快点啊",
                                              "，……唔，抱一下会死吗", "，……就、就抱一下哦", "，谁、谁要你抱了", "，……手伸过来就一下", "，别、别以为我想", "，才不是想黏着你", "，……哼，别松手" },
                CongratClosers = new string[] { "，哼，恭、恭喜……才不是特意说的呢",
                                                "，……嗯，恭喜。就这样，没别的意思",
                                                "，才、才没有很羡慕呢……真心的",
                                                "，哼，那当然，你本来就该这样", "，……哼，勉强恭喜一下", "，别、别太得意" },
                InviteClosers = new string[] { "，哼，既、既然你都说了……那就去吧",
                                               "，才、才不是想去呢……只是刚好有空",
                                               "，……唔，好吧。就这一次哦",
                                               "，别、别多想啊……我只是没事做", "，去、去就去，谁怕谁", "，……就这一次" },
                // 惊讶/害怕/疲惫/无聊/失望 are all states of the USER, so every line has to
                // read as something the user says about THEMSELVES. The previous versions
                // were written from the other side - typing 我好怕 produced "有我在呢",
                // i.e. the scared person offering reassurance.
                SurpriseClosers = new string[] { "，……欸？真、真的吗",
                                                 "，哼，有、有什么好奇怪的……好吧，我确实吓到了",
                                                 "，……你、你再说一遍？我没听清",
                                                 "，……居然是真的……我、我才没有很惊讶呢",
                                                 "，别、别突然说这种话啊……", "，什、什么啊", "，谁、谁惊讶了" },
                FearClosers = new string[] { "，……才、才没有害怕呢！只是……有点不安",
                                             "，哼，我、我一个人也可以的……大概",
                                             "，……你、你要是在的话，我就不那么怕了",
                                             "，别、别走开啊……不是说我怕！",
                                             "，……唔，陪我一会儿行不行", "，……才、才不怕", "，你、你别走开" },
                TiredClosers = new string[] { "，……才、才没有很累呢。真的",
                                              "，哼，我、我只是想躺一下……一下就好",
                                              "，……别、别管我，我缓一会儿",
                                              "，……你、你要是说声辛苦了，我可能会好一点",
                                              "，……唔，今天先这样吧。撑不住了", "，……别吵，让我歇会儿", "，哼，我累了" },
                BoredClosers = new string[] { "，……好、好无聊啊。你在干嘛",
                                              "，哼，才、才不是想找你聊天呢……只是没事做",
                                              "，……你、你要是有空的话，理我一下",
                                              "，……唔，陪我聊会儿吧。就一会儿", "，……无聊死了", "，陪我、陪我说说话嘛" },
                DisappointedClosers = new string[] { "，……才、才没有失望呢。本来也没指望什么",
                                                     "，哼，我、我早就猜到了……才怪",
                                                     "，……算了，也不是什么大事",
                                                     "，……你、你别安慰我。我自己能行",
                                                     "，……唔，下次……下次再说吧", "，……哼，早该知道", "，无所谓了" },
                // 安慰 = the USER is comforting the OTHER person. Note the difference from
                // 难过 above: here the user is the one doing the comforting, so these lines
                // are the ones that were previously (wrongly) filed under 难过.
                ComfortClosers = new string[] { "，别、别难过啦……我、我勉强陪陪你",
                                                "，笨、笨蛋，哭什么啊！……过来",
                                                "，……谁、谁让你一个人扛着的　说吧",
                                                "，过来，给你靠一下……就一会儿哦",
                                                "，……不许一个人偷偷难过　听见没",
                                                "，哼，我、我可不是在担心你……只是路过",
                                                "，……说吧，我听着　不着急",
                                                "，别、别硬撑着啊……笨蛋",
                                                "，……真是的，让人不放心　我在呢",
                                                "，哭完记得擦脸……很难看的", "，……别哭了，烦死了", "，哼，我陪你一会儿" }
            },

            new StyleDef("黏人", "喵~",
                new string[] { "(ฅ´ω`ฅ)", "(´･ω･`)", "♡(>ω<♡)", "(๑´ω`๑)", "(=^･ω･^=)", "(｡･ω･｡)",
                               "(=；ω；=)", "(っ´ω`c)" },
                new string[] { "呐，", "那个……", "欸，", "喂喂，", "……在吗", "我说啊，",
                               "悄悄说，", "你知道吗，", "欸欸，", "……嗯", "呐呐，", "欸欸，", "我说我说，", "呐，听我说，", "那个那个，", "嘿，", "你在吗你在吗，", "快点，", "嗯……", "悄悄话，", "诶，", "等一下，", "呐——", "你在不在呀，", "欸，听我说嘛，", "那个……那个，", "别走嘛，", "就一句话，", "呐呐呐，", "在干嘛呀，", "欸——", "你听我说嘛，", "那个，那个，", "别不理我嘛，", "就一会儿，", "嘿，", "我说我说，", "嗯……" },
                new string[] { "，带上我一起嘛　好不好", "，不要丢下我嘛……我会乖的", "，我一直都在哦　随时叫我",
                               "，记得想我……一秒钟也好", "，早点回来呀　我等你", "，我会等你的……不管多晚",
                               "，别忘了我呀……拜托", "，要常回来看看我　不然我会寂寞的",
                               "，我会想你的……真的", "，你去哪我都想跟着……可以吗",
                               "，一个人好无聊的……陪我嘛", "，……真的要走吗　那，抱一下再走", "，不要走开好不好", "，我一直在等你", "，再陪我一会儿嘛", "，就一会儿", "，别挂断", "，你会回来的吧", "，我数着呢", "，……一个人好无聊", "，抱一下再走", "，记得回我消息", "，一直陪着我好不好", "，我一个人会怕", "，别关掉窗口嘛", "，你说句话呀", "，我都等你半天了", "，……再聊五分钟", "，别走开好不好", "，我会想你的", "，记得回我", "，我等你哦", "，早点回来", "，一直都在等你", "，别丢下我一个人", "，抱一下再走", "，说好了哦", "，……那我等你" },
                new string[] { "，你要去哪里呀？也带我嘛", "，你是不是要出门了……", "，我也可以去吗？",
                               "，能不能带上我……", "，你要去多久呀……我会数的", "，那……我等你？",
                               "，会回来吧？……说好了哦", "，不带上我吗……呜" },
                new string[] { "，我也要去！", "，等等我嘛！", "，好耶好耶！", "，我也要我也要！",
                               "，太好了！", "，一起去嘛！", "，我准备好了！", "，带上我带上我！" },
                new string[] { "，我也好开心！", "，一起开心嘛~", "，看到你开心我就开心", "，嘿嘿，真好",
                               "，那我们一起去玩吧", "，开心要分我一半哦", "，抱一个！", "，耶！" },
                new string[] { "，别难过嘛，我陪着你", "，抱抱你", "，我一直都在的", "，要不要说给我听听",
                               "，不许一个人难过", "，我陪你一起", "，……我在这呢", "，哭出来会好受点的" },
                new string[] { "，别生气了嘛……", "，我帮你骂他！", "，消消气，我在呢", "，不理他们了，理我嘛",
                               "，我也讨厌他！", "，别气坏了身体", "，要不我们吃点什么？", "，我陪着你生气！" },
                new string[] { "，你来啦！我等好久了", "，欸！在的在的", "，你终于来了……我好想你",
                               "，嗨嗨！今天也一起吗", "，我看到你消息就马上回了哦" },
                new string[] { "，不要走嘛……再待一会儿", "，那你早点回来哦", "，我会想你的……真的",
                               "，拜拜……记得回消息", "，我会一直等你的" },
                new string[] { "，……说出来了好害羞", "，喜欢你喜欢你最喜欢你了",
                               "，那、那你要一直喜欢我哦", "，我早就想说了……",
                               "，你要负责哦" },
                new string[] { "，你真的好厉害呀", "，我说的是真的哦", "，那你要多夸夸自己",
                               "，我可不会随便夸人的", "，嘿嘿，我说对了吧" })
            {
                ThanksClosers = new string[] { "，真的谢谢你嘛", "，下次我请你！", "，你对我最好了", "，不要谢我，要一直陪着我", "，那你以后都要找我哦" },
                ApologyClosers = new string[] { "，对不起嘛……别生气", "，我错了，原谅我好不好", "，呜……不要不理我", "，呜……我原谅你了", "，那你要多陪我一会儿" },
                CharmClosers = new string[] { "，抱抱！抱紧一点", "，要抱抱……就一下", "，不许松手哦", "，抱一下嘛　就一下", "，我也要抱", "，让我靠着", "，不许推开我", "，手给我牵着", "，……再抱紧一点" },
                CongratClosers = new string[] { "，恭喜恭喜！好耶！", "，太棒了！我们去庆祝！", "，我就知道你可以的！", "，太好了！那我们一起庆祝", "，恭喜！我也要抱一下" },
                InviteClosers = new string[] { "，好啊好啊！我去我去！", "，等我！我马上来！", "，耶！那我们说定了", "，我去我去！等我！", "，带上我好不好" },
                SurpriseClosers = new string[] { "，哇！真的吗！", "，欸欸欸？你说什么！", "，天呐！我好震惊！", "，诶诶诶？！", "，真的吗真的吗" },
                FearClosers = new string[] { "，呜……我好怕，你能陪着我吗", "，抱紧我好不好……", "，我一个人不敢……", "，呜……你别走", "，我抓着你" },
                TiredClosers = new string[] { "，呜……我好累，想靠一下", "，抱抱我……我撑不住了", "，今天真的没力气了……", "，那我们一起睡吧", "，我也困了，抱着睡" },
                BoredClosers = new string[] { "，我好无聊……陪我嘛", "，你在干嘛呀……我好闲", "，一个人待着好没意思", "，那我们聊天呀", "，陪我玩嘛陪我玩嘛" },
                DisappointedClosers = new string[] { "，呜……我好失望", "，说好的一定要算数啊……", "，……我等你，别骗我", "，呜……别失望嘛", "，我陪着你还不够吗" },
                ComfortClosers = new string[] { "，别难过嘛，我陪着你", "，抱抱你，没事的", "，我一直都在的，说给我听听嘛", "，不许一个人难过，有我在", "，抱抱，我一直都在", "，不哭不哭，我在呢" }
            },

            new StyleDef("慵懒", "……喵",
                new string[] { "(￣o￣)", "(˘ω˘)", "(=ω=)", "(－_－)", "(￣﹃￣)", "(∪｡∪)",
                               "(=w=)", "(,,-ω-,,)" },
                new string[] { "唔……", "哈啊……", "……嗯", "呼……", "啊……", "……嗯？",
                               "唔姆……", "呼啊……", "嗯……", "啊……", "唔——", "……哈", "呼……", "那个……", "算了，", "……等一下", "唔嗯", "……什么来着，", "唉，", "算了算了，", "唔……", "嗯——", "啊……那个，", "……算了，", "呼啊——", "再等一下，", "嗯——", "啊，那个……", "唔，", "……哈啊", "算了，", "等一下，", "呼，", "再躺会儿，", "唔嗯……", "……嗯？" },
                new string[] { "……好困哦……", "……让我再躺一会儿……", "……不想动……", "……等一下再说……",
                               "……就这样吧……", "……懒得想……", "……好麻烦……", "……晚点吧……　唔",
                               "……再说吧……", "……唔，好累……改天吧", "，再说吧", "，好麻烦", "，懒得动", "，……就这样", "，反正不急", "，随便啦", "，……等一下再说", "，让我躺会儿", "，唔……算了", "，好困", "，回头再说", "，反正也不急", "，我先趴会儿", "，……别催", "，随缘吧", "，啊——好麻烦", "，啊，随便吧", "，困了", "，回头再说", "，……算了", "，我不想动", "，嗯，就这样", "，懒得想", "，让我歇会儿", "，唔……", "，再说吧" },
                new string[] { "……嗯？你说什么……", "……啊？什么……", "……再说一遍……",
                               "……没听清……", "……唔？", "……听不清……" },
                new string[] { "……好吵……", "……小声一点……", "……别喊了……", "……吵到我了……",
                               "……唔……", "……烦……" },
                new string[] { "……嗯，挺好的……", "……还行吧……", "……不错……", "……嗯……",
                               "……那就好……", "……唔，好耶……" },
                new string[] { "……别难过了……睡一觉就好了……", "……嗯……抱一下……", "……会好起来的……",
                               "……慢慢来吧……", "……我陪着你……", "……别想了……" },
                new string[] { "……生气好累的……", "……不值得……", "……算了吧……", "……睡一觉就忘了……",
                               "……别浪费力气……", "……唔……随便吧……" },
                new string[] { "，……嗯，来了啊", "，……哦，是你", "，……唔，好久不见",
                               "，……在的……有事吗" },
                new string[] { "，……嗯，路上小心", "，……拜拜……困", "，……早点睡……",
                               "，……嗯，下次聊……" },
                new string[] { "，……唔，说出来了", "，……嗯，喜欢你", "，……好害羞",
                               "，……就这样吧" },
                new string[] { "，……嗯，你确实可以", "，……唔，不错", "，……我说真的",
                               "，……你也是" })
            {
                ThanksClosers = new string[] { "，……嗯，谢了", "，……唔，记着了", "，……下次我请", "，嗯……不客气", "，啊，知道了" },
                ApologyClosers = new string[] { "，……唔，对不起", "，……嗯，我错了", "，……别生气……", "，唔……算了", "，没事，我也懒得计较" },
                CharmClosers = new string[] { "，……抱一下……", "，……唔，过来……", "，……想抱抱……", "，蹭一下就好", "，……过来", "，抱抱，别说话", "，好困，别动", "，让我靠一会儿", "，唔……就这样" },
                CongratClosers = new string[] { "，……嗯，恭喜……", "，……哦，挺好的", "，……唔，厉害……", "，嗯……恭喜", "，啊，挺好的" },
                InviteClosers = new string[] { "，……好啊……", "，……嗯，等我一下", "，……唔，走吧……", "，唔……看情况吧", "，再说" },
                SurpriseClosers = new string[] { "，……唔？", "，……啊？真的……", "，……哦……", "，哦……这样啊", "，嗯？" },
                FearClosers = new string[] { "，……有点怕……", "，……唔，不太想一个人", "，……陪一下……", "，唔……有点怕", "，你陪着我" },
                TiredClosers = new string[] { "，……唔，好累……", "，……想躺下了……", "，……撑不住了……", "，啊……好困", "，睡吧睡吧" },
                BoredClosers = new string[] { "，……好无聊……", "，……没事做……", "，……唔……", "，嗯……无聊", "，那就发会儿呆" },
                DisappointedClosers = new string[] { "，……唔，失望……", "，……算了……", "，……下次吧……", "，唔……算了", "，反正也没差" },
                ComfortClosers = new string[] { "，……别怕……", "，……唔，没事的", "，……嗯……我在", "，……慢慢来……", "，唔……别哭了", "，来，靠着" }
            },

            new StyleDef("委屈", "……喵",
                new string[] { "(｡•́︿•̀｡)", "(´；ω；`)", "(ᵕ﹏ᵕ)", "(｡╥﹏╥｡)", "(｡•́︵•̀｡)", "(っ- ‸ - ς)",
                               "(｡>﹏<｡)", "(´。＿。｀)" },
                new string[] { "呜……", "那个……", "……我", "呜嗯……", "我、我说……",
                               "……那个，我", "呜哇……", "……对不起，我", "呜……", "那个……", "……对不起", "我、我不是", "唔……", "……诶", "真的吗……", "我、我做错什么了吗", "……嗯", "小声，", "……好吧", "呜嗯……", "那个……我，", "……不是的，", "呜——我，", "……对不起，我", "你听我说完，", "……真的不是", "那个……", "呜，", "我、我是不是", "……对不起，", "不是的，", "我没有，", "你听我说，", "……我，", "诶……", "真的不是，" },
                new string[] { "，你是不是不理我了……对不起", "，我是不是做错什么了……我会改的",
                               "，别生我的气好不好……拜托", "，……对不起　我下次会注意的",
                               "，我下次会注意的……别不理我", "，……你还在吗　在的话回我一声",
                               "，我、我会改的……真的", "，不要讨厌我好不好……呜",
                               "，……我会乖乖的　别生气", "，你别走好不好……我一个人怕", "，对不起", "，我是不是很没用", "，……对不起，我太笨了", "，别生我的气好不好", "，我会努力的", "，……我会听话的", "，我又说错话了吗", "，你别不理我", "，……是我不好", "，呜呜", "，我不是那个意思", "，你别误会好不好", "，我、我下次注意", "，……你还在生气吗", "，我会改的", "，呜……原谅我嘛", "，呜……对不起", "，别讨厌我", "，我下次不会了", "，你会不会生气", "，我不是故意的", "，……我错了", "，你别不说话", "，呜", "，我有点难过", "，……没事的" },
                new string[] { "，你是不是讨厌我了……", "，我、我做错什么了吗……", "，你不理我了吗……呜",
                               "，……我哪里做错了　你说，我改", "，你在生我的气吗……", "，我是不是很烦……" },
                new string[] { "，呜哇……", "，我、我不是故意的……", "，对不起对不起……", "，我错了……",
                               "，呜……", "，别、别骂我……" },
                new string[] { "，你开心我就开心……", "，真的吗……太好了……", "，那、那就好……",
                               "，呜……好开心……", "，谢谢你……", "，我会记住的……" },
                new string[] { "，呜……我也是……", "，我、我陪你一起难过……", "，不要难过嘛……",
                               "，我、我都想哭了……", "，抱、抱一下好不好", "，……我懂" },
                new string[] { "，你、你别生气嘛……", "，都、都是我不好……", "，……对不起",
                               "，我、我帮你出气好不好", "，别凶我嘛……", "，我、我下次不敢了" },
                new string[] { "，你、你终于来了……", "，呜……你好", "，……我还以为你不理我了",
                               "，呜……我在的" },
                new string[] { "，你、你要走了吗……", "，呜……早点回来好不好", "，……我会等你的",
                               "，别、别走太久……" },
                new string[] { "，呜……说、说出来了", "，……我、我喜欢你", "，你、你会不会笑我",
                               "，……我、我说完了" },
                new string[] { "，呜……我说的是真的", "，……你、你真的很棒", "，我、我没有骗你",
                               "，……真、真的" })
            {
                ThanksClosers = new string[] { "，呜……真、真的谢谢", "，……我、我记着了", "，你、你人真好", "，呜……谢谢你", "，真、真的谢谢你" },
                ApologyClosers = new string[] { "，呜……对、对不起", "，……我错了，别不理我", "，我、我下次不敢了", "，对不起……我真的知道错了", "，呜……你会原谅我吗" },
                CharmClosers = new string[] { "，呜……抱、抱一下", "，……可、可以抱抱吗", "，我、我要抱抱", "，抱、抱一下就不委屈了", "，我只要一点点就好", "，你别推开我", "，让我抱一下嘛", "，呜……就一下", "，我也想要被哄" },
                CongratClosers = new string[] { "，呜……恭、恭喜……", "，……好厉害……", "，我、我也替你高兴", "，恭喜……我也替你开心", "，呜，太好了" },
                InviteClosers = new string[] { "，可、可以吗……", "，呜……我去", "，……真的带上我吗", "，我、我可以去吗", "，那我去……不添麻烦吧" },
                SurpriseClosers = new string[] { "，呜哇……", "，……欸？", "，真、真的吗……", "，诶？！真的吗", "，我、我没想到" },
                FearClosers = new string[] { "，呜……我、我好怕", "，……你、你能陪我吗", "，我、我不敢一个人……", "，呜……别丢下我", "，我好怕" },
                TiredClosers = new string[] { "，呜……我、我好累", "，……我撑不住了", "，我、我想休息一下……", "，我也好累……", "，呜，撑不住了" },
                BoredClosers = new string[] { "，呜……我好无聊", "，……你、你在忙吗", "，我、我一个人好没意思", "，呜，好无聊", "，你陪我说说话好不好" },
                DisappointedClosers = new string[] { "，呜……我好失望", "，……说、说好的呢", "，我、我等了好久的……", "，呜……果然是这样", "，我就知道" },
                ComfortClosers = new string[] { "，呜……别难过……", "，……我、我陪你", "，抱、抱一下好不好", "，没关系的，真的", "，别哭了……我陪你", "，呜，我也难受" }
            },

            new StyleDef("元气", "喵！",
                new string[] { "(≧▽≦)", "٩(๑>ω<๑)۶", "＼(^o^)／", "(๑•̀ㅂ•́)و✧", "(ﾉ>ω<)ﾉ", "(๑˃̵ᴗ˂̵)",
                               "٩(ˊᗜˋ*)و", "(ง •̀_•́)ง" },
                new string[] { "好耶！", "冲！", "嘿咻——", "出发！", "上啊！", "哦哦！",
                               "来来来！", "走起！", "好嘞！", "嘿！", "呦！", "来吧！", "冲鸭！", "诶嘿！", "哦——！", "早上好！", "嘿咻！", "我们走！", "噢耶！", "嗨嗨！", "好耶！", "嘿——", "来了来了！", "噢！", "听我说听我说！", "准备好！", "嘿！", "来啦！", "好嘞——", "哟！", "冲！", "听我说！", "嘿嘿，", "前面让让！", "上！", "呜呼！" },
                new string[] { "，冲呀——！", "，今天也要加油！", "，交给我吧！", "，没问题！",
                               "，包在我身上！", "，一起上！", "，干就完了！", "，绝对可以的！",
                               "，加油加油！", "，看我的！", "，今天也要加油！", "，元气满满！", "，冲鸭！", "，一起努力吧！", "，没问题！", "，交给我！", "，嘿嘿嘿", "，好开心！", "，继续继续！", "，耶！", "，一起冲鸭！", "，这才像话！", "，继续加油！", "，我超有干劲！", "，走起走起！", "，绝不放弃！", "，一起加油！", "，没问题！", "，交给我！", "，嘿嘿！", "，冲鸭！", "，好开心！", "，继续！", "，耶！", "，来吧！", "，我超有干劲！" },
                new string[] { "，我们走吧？", "，要不要一起？", "，去不去？", "，怎么样，一起？",
                               "，你来不来？", "，准备好了吗？" },
                new string[] { "，太棒了！", "，好厉害！", "，哇哦！", "，厉害啊！", "，了不起！", "，牛！" },
                new string[] { "，我也超开心！", "，好耶好耶！", "，哈哈哈！", "，那我们庆祝一下！",
                               "，开心！！！", "，这才是好事嘛！" },
                new string[] { "，别难过！明天会更好的！", "，我陪你！", "，振作起来！", "，没事的，有我呢！",
                               "，我们一起去散散心！", "，会好起来的！" },
                new string[] { "，别气别气！我们去吃好吃的！", "，我帮你出气！", "，不理他们！", "，我们走！",
                               "，气坏了不值！", "，我陪你一起气！" },
                new string[] { "，你好你好！", "，哦哦！你来啦！", "，嗨！今天也超有精神！",
                               "，来了来了！" },
                new string[] { "，拜拜！明天见！", "，路上小心！", "，下次再聊啊！",
                               "，那说定了，明天见！" },
                new string[] { "，说出来了！好耶！", "，我喜欢你！就这样！", "，嘿嘿，说都说了！",
                               "，不许拒绝我！" },
                new string[] { "，我说真的！你超棒的！", "，这是实话！", "，你要相信自己啊！",
                               "，我说的绝对没错！" })
            {
                ThanksClosers = new string[] { "，谢谢你！真的帮大忙了！", "，下次我请你！", "，记住这份人情了！", "，不用谢！小事一桩", "，嘿嘿，应该的" },
                ApologyClosers = new string[] { "，对不起！我错了！", "，别生气嘛！我改！", "，我保证下次不会了！", "，对不起！下次一定注意", "，我反省了！" },
                CharmClosers = new string[] { "，抱一个！快点！", "，我要抱抱！不许拒绝！", "，来嘛来嘛！", "，抱一个！", "，来，抱抱！", "，要抱抱！", "，不抱不行！", "，我扑过来了！", "，嘿咻——抱住！" },
                CongratClosers = new string[] { "，恭喜恭喜！！！", "，太棒了！我就知道！", "，好耶！庆祝一下！", "，太好了！恭喜恭喜", "，我就知道你能行" },
                InviteClosers = new string[] { "，去去去！走起！", "，等我三秒！", "，说定了，不许反悔！", "，去！当然去", "，算我一个" },
                SurpriseClosers = new string[] { "，哇塞！！！", "，真的假的！我看看！", "，天呐！太意外了！", "，哇！真的假的", "，太意外了吧" },
                FearClosers = new string[] { "，我、我才不怕！……好吧有点", "，呜，有点吓人！", "，陪我一下！就一下！", "，别怕！有我呢", "，我跟你一起" },
                TiredClosers = new string[] { "，好累！我要躺平了！", "，电量只剩 1% 了！", "，今天先到这里！明天再战！", "，那快休息！", "，累坏了吧，快去睡" },
                BoredClosers = new string[] { "，好无聊！谁来陪我玩！", "，闲得发慌了！", "，找点事做吧！", "，那就找点事做！", "，我陪你玩" },
                DisappointedClosers = new string[] { "，呜，好失望！下次一定行！", "，不甘心！再来一次！", "，算我倒霉！下次赢回来！", "，别灰心！下次一定行", "，失败是成功之母" },
                ComfortClosers = new string[] { "，别怕！有我呢！", "，没事的！我们一起！", "，交给我！我保护你！", "，振作起来！我陪你！", "，别难过！我陪你", "，有我呢，别怕" }
            },

            // 应激 = the cat is in a stress response: ears flat back, body low, hissing at
            // whoever comes near. Behaviour taken from the cat-behaviour references
            // (哈气 / 低吼 / 飞机耳 / 炸毛 / 躲起来 / 攻击). Every line is defensive rather
            // than playful - this is the one style where the cat is genuinely NOT being
            // cute at you. The suffix is a hiss (嘶——) and not 哈——, because 哈—— reads as
            // laughing in Chinese text.
            new StyleDef("应激", "嘶——",
                new string[] { "(=｀Д´=)", "(╬ Ò ‸ Ó)", "(=｀ω´=)", "(=｀ェ´=)",
                               "(ﾉ｀Д´)ﾉ", "(=ΦωΦ=)", "(；｀Д´)", "( ˘ω˘ ;)",
                               "(=Θ人Θ=)", "(◣_◢)" },
                // ★ 开头：一惊一乍 + 语无伦次 + 想躲 ✓
                // ★ 前缀那几个是"哈气"系列 ✧ 猫在应激时最典型的外在反应 ✓
                new string[] { "（炸毛）", "（飞机耳）", "（缩了一下）", "（哈气）",
                               "（呲牙）", "（低吼）", "（尾巴炸开）",
                               "（瞳孔放大）", "（缩成一团）",
                               "（躲到角落）", "（钻进桌子底下）",
                               "哈——", "嘶——！", "哈、哈——",
                               "诶？！", "什、什么？", "吓我一跳……", "别、别突然出声！",
                               "不、不是……", "我、我没听清", "等下，",
                               "你、你先等一下", "……谁？", "（缩了缩）", "（往后躲）", "别、别突然说话，", "……谁，", "（警惕）", "啊……", "（一抖）", "（往后缩）", "别、别，", "……谁？", "（僵住）", "啊、那个，", "等、等一下，", "（低头）", "……我，", "（抿嘴）" },
                // ★ 结尾：六类反应混着来 ✓
                new string[] { "，哈——别过来", "，嘶——！", "，哈气……别靠近",
                               "，别、别过来。我哈气了",
                               "，停手。别弄了", "，够、够了。别烦我",
                               "，别摸我。真的别摸", "，别、别碰那里。疼",
                               "，……这是我的地方。退后",
                               "，……我先缓一下",
                               "，别、别靠这么近", "，我手在抖",
                               "，……你刚才说什么？我没听清", "，心、心跳好快",
                               "，……让我一个人待会儿", "，我不太舒服",
                               "，……能别说这么快吗", "，我、我有点乱",
                               "，……先别说话", "，脑子转不过来了",
                               "，……今天到此为止吧", "，我想回去了",
                               "，……对不起，我状态不太好", "，……让我缓一下", "，别靠太近", "，我、我没事", "，……先别说话", "，手还在抖", "，……对不起，我有点乱", "，……让我缓缓", "，别靠太近", "，我、我没事", "，先别说话", "，我还好", "，……对不起", "，手在抖", "，……有点乱", "，我先坐会儿", "，……嗯" },
                new string[] { "，……你、你问这个干嘛",
                               "，别、别问了好不好", "，我、我脑子转不过来",
                               "，我、我没想过",
                               "，能、能再说一遍吗", "，……我记不太清了",
                               "，这个……我得想想", "，……别问我行吗" },
                new string[] { "，哈——！", "，嘶——！！", "，哈、哈——！",
                               "，诶？！", "，别、别喊！",
                               "，吓死我了……", "，我、我耳朵", "，……别突然动" },
                new string[] { "，……也、也没有很高兴", "，耳朵还没放下来呢",
                               "，……但、但是还行吧", "，别看我笑",
                               "，……我放松不下来" },
                new string[] { "，……别看我", "，呜——别问了", "，我一个人待会儿",
                               "，……不想说话", "，我、我说不出来",
                               "，……眼睛有点酸" },
                new string[] { "，哈——！！", "，嘶——！别过来", "，哈气警告！",
                               "，烦死了。别弄了", "，我、我说了别碰",
                               "，……别逼我", "，我、我警告你", "，别过来！",
                               "，……我控制不住", "，够了！", "，……你先退后" },
                new string[] { "，……是你啊。有事吗", "，别、别站那么近",
                               "，……我先退一步", "，……你来了啊", "，呃……嗨" },
                new string[] { "，……我先走了", "，……别跟着我", "，我回去了",
                               "，……下次吧", "，抱歉，我真的得走了" },
                new string[] { "，……别、别说这种话。我现在很乱",
                               "，你、你别靠过来……但是别走",
                               "，我、我不知道", "，……让我想一下",
                               "，别说了。我现在谁都不想理" },
                new string[] { "，……我、我没那么好", "，……别夸我",
                               "，这、这算什么", "，你、你随便说说而已吧" })
            {
                ThanksClosers = new string[] { "，……嗯。就这样", "，不用、不用谢",
                                               "，……我只是顺手", "，别、别客气", "，……嗯。就这样", "，不、不用谢" },
                ApologyClosers = new string[] { "，……知道就好", "，我、我接受不了。暂时",
                                                "，……别说了。我自己缓缓", "，……知道就好", "，我、我缓缓" },
                CharmClosers = new string[] { "，……不、不要碰我。现在不行",
                                              "，……让我一个人待会儿",
                                              "，你、你先别过来", "，……冷、冷不丁的别抱", "，先、先说一声", "，……可以，就一下", "，我、我不太习惯", "，别、别用力", "，……嗯，这样就好" },
                CongratClosers = new string[] { "，……嗯。恭喜", "，……那就好",
                                                "，我、我替你高兴", "，……嗯。恭喜", "，那就、那就好" },
                InviteClosers = new string[] { "，……不去。别拉我", "，我、我不太舒服",
                                               "，……下次一定", "，……不去。别拉我", "，我、我不太舒服" },
                SurpriseClosers = new string[] { "，哈——！", "，嘶——！！",
                                                 "，哈、哈——你别过来",
                                                 "，我、我心跳漏了一拍",
                                                 "，别、别吓我", "，哈——！", "，别、别吓我" },
                FearClosers = new string[] { "，呜——别过来", "，哈——！别、别靠近",
                                             "，嘶——！我哈气了",
                                             "，……求你了，退后一点",
                                             "，我、我不怕你……才怪", "，呜——别过来", "，求你了，退后一点" },
                TiredClosers = new string[] { "，……别吵。我要睡了", "，累、累得说不出话",
                                              "，……今天不想说话",
                                              "，没、没力气吵了", "，让我躺一下", "，……别吵。我要睡了", "，累、累得说不出话" },
                BoredClosers = new string[] { "，……关我什么事", "，我、我干点别的",
                                              "，……一个人待着就行", "，……关我什么事", "，我、我干点别的" },
                DisappointedClosers = new string[] { "，……果然", "，我、我早该知道",
                                                     "，……无所谓了", "，……果然", "，我、我早该知道" },
                ComfortClosers = new string[] { "，……我不会安慰人", "，我、我不知道该说什么",
                                                "，……你还好吗", "，别、别哭", "，……我不会安慰人", "，别、别哭" }
            },

            // ★ 用户 2026-10-17：语气也多加几种情绪 ✧ 补了「温柔」和「高冷」✓
            new StyleDef("温柔", "喵~",
                new string[] { "(｡･ω･｡)", "( ´ ▽ ` )", "(◡‿◡)", "(´･ω･`)", "(๑´ω`๑)", "(﹡ˆᴗˆ﹡)" },
                new string[] { "嗯，", "好呀，", "没事的，", "慢慢来，", "我在，", "别急，",
                               "来，", "乖，", "不急哦，", "……嗯", "嗯……", "那个……", "慢慢来，", "不着急，", "我在，", "乖，", "没关系，", "……好", "听我说，", "来，", "嗯，", "那个，", "慢一点，", "没关系，", "……嗯", "听我说，", "嗯，", "那个，", "别急，", "慢慢说，", "我在听，", "……嗯", "来，", "没关系，", "你不用紧张，", "……好" },
                new string[] { "，慢慢来　不着急的", "，有我陪着你呢", "，别自己扛着　说出来会好点",
                               "，我一直都在的　真的", "，今天也辛苦了", "，抱抱　什么都不用说",
                               "，会好起来的　我保证", "，你不用那么拼的", "，好好休息一下吧",
                               "，别担心　我帮你看着", "，慢慢来就好", "，不要勉强自己", "，我在这里", "，累了就休息", "，有我呢", "，不着急的", "，……会好的", "，你做得很好", "，我懂", "，别怕", "，不用急的", "，我一直都在", "，你已经很好了", "，别给自己太多压力", "，……会好的", "，记得休息", "，慢慢来就好", "，我陪着你", "，别勉强", "，会好的", "，你做得很好", "，记得休息", "，……我在", "，不用急", "，我懂的", "，辛苦了" },
                new string[] { "，要不要跟我说说？", "，怎么啦？", "，需要帮忙吗？",
                               "，你别急　慢慢讲", "，是遇到什么难事了吗？", "，我可以帮上忙吗？" },
                new string[] { "，真好呀！", "，太好了　我为你开心", "，你真的很棒", "，嘿嘿　真好" },
                new string[] { "，看到你开心我也开心", "，那就好　我一直担心你", "，笑一个　这样就对了",
                               "，这份开心我也收下啦", "，要一直这样开心下去哦" },
                new string[] { "，别难过　我陪着你", "，哭出来也没关系的", "，我不会走的　你放心",
                               "，来　靠一会儿", "，你已经很努力了　真的", "，对不起　我没能帮上忙" },
                new string[] { "，别气了　气坏身体不值得", "，先深呼吸　慢慢说", "，我不走　你说吧",
                               "，要骂就骂我好了", "，气消了再聊　我等你" },
                new string[] { "，你来啦　我等了一会儿", "，嗨　今天过得好吗", "，看到你消息我就回了" },
                new string[] { "，路上小心　到家说一声", "，早点休息　别熬夜", "，那我等你消息",
                               "，注意安全　真的" },
                new string[] { "，我也喜欢你　一直", "，嗯　我懂的", "，那就　一直在一起好不好",
                               "，谢谢你愿意告诉我" },
                new string[] { "，你真的很厉害", "，我就知道你可以", "，不用谦虚　这是你应得的",
                               "，为你骄傲" })
            {
                ThanksClosers = new string[] { "，不用谢　应该的", "，客气什么呀", "，你开心就好", "，不客气，能帮上就好", "，……你太客气了" },
                ApologyClosers = new string[] { "，没关系　真的", "，别放在心上", "，我早就不介意了", "，没关系的，我懂", "，别放在心上" },
                CharmClosers = new string[] { "，来　抱一下", "，好啦好啦　抱抱你", "，乖　我在呢", "，抱抱就好", "，来，靠着我", "，一下下就好", "，……我在", "，不用说话也行", "，手给我" },
                CongratClosers = new string[] { "，恭喜你　真的太好了", "，我就说你可以的", "，为你高兴", "，恭喜你，真为你高兴", "，太好了，值得开心" },
                InviteClosers = new string[] { "，好呀　我去", "，等我一下　马上来", "，那就这么定了", "，好啊，我去", "，如果你不嫌麻烦的话" },
                SurpriseClosers = new string[] { "，真的吗？", "，哎呀　真没想到", "，这可太意外了", "，真的吗？", "，那真是个好消息" },
                FearClosers = new string[] { "，别怕　我在呢", "，我陪你　别一个人待着", "，有我在　没事的", "，别怕，我在这里", "，有我在" },
                TiredClosers = new string[] { "，辛苦了　快去休息", "，别硬撑了　睡一会儿吧", "，我陪你歇一会", "，累了就休息吧", "，别硬撑着" },
                BoredClosers = new string[] { "，那我陪你聊会儿", "，要不要出去走走", "，我在这儿呢", "，那我们聊聊天？", "，我陪你待着" },
                DisappointedClosers = new string[] { "，别失望　还有机会", "，我陪着你", "，下次一定行　真的", "，别失望，还有机会", "，我理解你的心情" },
                ComfortClosers = new string[] { "，别难过　我陪着你", "，抱抱　没事的", "，我一直都在", "，慢慢来，我陪着你", "，哭出来会好受些" }
            },

            new StyleDef("高冷", "喵。",
                new string[] { "(・_・)", "(￣ー￣)", "(￣▽￣)", "(-_-)", "(´-ω-`)", "(・ω・)" },
                new string[] { "嗯。", "哦。", "随你。", "行吧。", "知道了。", "……", "说。", "什么事。", "……", "嗯。", "哦。", "说。", "行。", "随你。", "知道了。", "没必要。", "无聊。", "切。", "……哦？", "这样。", "可以。", "继续。", "嗯。", "……", "说。", "什么事。", "哦。", "……嗯？", "哦。", "嗯。", "说。", "……什么事。", "行。", "可以。", "知道了。", "……说重点。", "讲。", "嗯？" },
                new string[] { "，就这样。", "，随便。", "，无所谓。", "，懒得说。", "，不必了。",
                               "，没兴趣。", "，你随意。", "，到此为止。", "，不用解释。", "，嗯", "，知道了", "，不必", "，没兴趣", "，就这样", "，懒得", "，随意", "，看情况", "，就这样。", "，随便。", "，不必。", "，……嗯。", "，说完了？", "，知道了。", "，就这样。", "，不必。", "，行了。", "，知道了。", "，……随你。", "，结束。", "，就这样。", "，行了。", "，知道了。", "，不必。", "，……嗯。", "，结束。", "，随便。", "，话说完了吧。", "，我记下了。", "，嗯。" },
                new string[] { "，问这个干什么？", "，你想说什么？", "，然后？", "，重点。", "，挑重点说。", "，有事？", "，说。", "，重点。", "，你想表达什么？", "，所以？", "，然后。" },
                new string[] { "，嗯。", "，知道了。", "，没必要这么激动。", "，哦。", "，嗯。", "，知道了。", "，随便。", "，不必多言。" },
                new string[] { "，哦。", "，还不错。", "，值得高兴。", "，哦，不错", "，嗯", "，还行", "，知道了" },
                new string[] { "，……保重。", "，会过去的。", "，别想太多。", "，……保重", "，会过去的", "，别想太多", "，嗯" },
                new string[] { "，随便你气。", "，气够了再说。", "，我不想吵。", "，随便你气", "，气够了再说", "，我不想吵", "，幼稚", "，没必要" },
                new string[] { "，来了。", "，嗯。", "，……在。", "，来了", "，嗯", "，……在", "，哦" },
                new string[] { "，走了。", "，回见。", "，不送。", "，走了", "，回见", "，不送", "，嗯" },
                new string[] { "，……嗯。", "，知道了。", "，别说了。", "，……嗯", "，知道了", "，别说了", "，……随你" },
                new string[] { "，还行。", "，勉强。", "，嗯。", "，还行", "，勉强", "，嗯", "，有点意思" })
            {
                ThanksClosers = new string[] { "，嗯。", "，不用。", "，不必。", "，嗯。" },
                ApologyClosers = new string[] { "，算了。", "，下次别这样。", "，算了。", "，下次注意。" },
                CharmClosers = new string[] { "，……随你。", "，就一下。", "，……过来。", "，就一下。", "，别得寸进尺。", "，……可以。", "，手。", "，别说话。" },
                CongratClosers = new string[] { "，嗯，恭喜。", "，嗯，恭喜。", "，还行。" },
                InviteClosers = new string[] { "，……去。", "，什么时候？", "，……去。", "，几点。" },
                SurpriseClosers = new string[] { "，哦？", "，哦？", "，……是吗。" },
                FearClosers = new string[] { "，……别慌。", "，我在。", "，……别怕。", "，我在。" },
                TiredClosers = new string[] { "，睡吧。", "，去睡。", "，别撑了。" },
                BoredClosers = new string[] { "，那就发呆。", "，那就坐着。", "，随你。" },
                DisappointedClosers = new string[] { "，……早就说了。", "，……早就说了。", "，嗯。" },
                ComfortClosers = new string[] { "，……别难过。", "，嗯。我在。", "，……别哭了。", "，嗯，我在。" }
            },

            // ★ 用户 2026-10-17：语气也多来几种 ✧ 中二 / 天然呆 / 毒舌 ✓
            new StyleDef("中二", "喵！",
                new string[] { "✧･ﾟ: *✧･ﾟ:*", "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "٩(ˊᗜˋ*)و", "(ﾉ>ω<)ﾉ", "(≧∇≦)b" },
                new string[] { "哼，", "愚蠢的人类，", "吾乃", "听好了——", "区区凡人，", "以吾之名，", "哼，", "愚蠢的人类，", "听好了——", "吾乃", "区区凡人，", "以吾之名，", " darkness，", "封印，", "此乃", "呵，", "在下的名字是，", "……终于来了吗，", "以吾之血，", "觉醒吧，", "哼，", "听好了，", "吾乃", "此乃", "以吾之名，", "……终于，", "哼，", "听好了——", "吾乃", "此乃", "……终于", "以吾之名，", "愚蠢的凡人啊，", "沉睡吧，", "命运啊，", "……哼，" },
                new string[] { "，此等小事　不足挂齿！", "，吾已看穿一切　哈哈哈！", "，这便是我等的宿命吧",
                               "，封印……解除了！", "，吾的右眼又开始痛了……", "，此乃天意　不可违逆",
                               "，吾将守护你　至最后一刻", "，愚蠢……却让人无法讨厌", "，这一切都在吾的预料之中", "，命运的齿轮已经开始转动", "，吾的封印……正在松动", "，此乃千年一遇的机缘", "，让吾来看看……嗯，果然如此", "，愚蠢，却又不失可爱", "，此等小事　不足挂齿", "，吾已看穿一切", "，命运的齿轮开始转动", "，这便是吾的实力", "，封印……解除了", "，吾之右眼又开始痛了", "，此为吾之宿命", "，一切都在吾的掌控之中", "，命运的齿轮已转动", "，区区小事　不足挂齿", "，吾已等候千年", "，此等力量……", "，此为吾之宿命", "，吾已等候千年", "，一切尽在掌握", "，命运的齿轮转动了", "，卑微却有趣", "，吾之力量……", "，这便是结局", "，封印……解除！", "，不足挂齿", "，吾承认你了" },
                new string[] { "，为何要问吾这等凡人？", "，你……是在试探吾吗？", "，哼　答案吾早已知晓。",
                               "，别小看吾的智慧！", "，你可知道这意味什么？", "，难道你从未怀疑过吗？", "，这背后的真相　你敢听吗？", "，吾问你　你相信命运吗？" },
                new string[] { "，哈——哈——哈！", "，见识到了吧！这就是吾的实力！", "，胜利终将属于吾！", "，哈——哈——哈——！", "，此等神迹！", "，吾的血脉在沸腾！", "，这就是吾真正的力量！" },
                new string[] { "，吾心甚慰", "，与你同乐　乃吾之荣幸", "，这便是幸福吗……", "，吾心甚悦", "，与汝同乐　是吾之幸", "，这便是幸福的味道吗", "，吾的胸口……好暖" },
                new string[] { "，吾也曾有过这般黑暗的时刻", "，莫哭　吾在此", "，此痛　吾懂", "，吾也曾坠入黑暗", "，莫怕　吾与你同在", "，此等痛苦　吾明白", "，吾会守住你的　以吾之名" },
                new string[] { "，吾的怒火　已燃烧殆尽", "，既如此　便由吾来裁决", "，不可饶恕！", "，吾之怒　非汝能承受", "，不可饶恕！", "，吾要让你付出代价", "，胆敢如此——" },
                new string[] { "，吾在此恭候多时", "，你来了啊　凡人", "，哼　终于出现了", "，吾已等候多时", "，汝来了啊", "，哼　终于现身了", "，吾还以为汝不来了" },
                new string[] { "，吾去也——", "，别了　愿命运再会", "，待吾归来　世界将为之改变", "，吾去也", "，愿命运让我们再会", "，别了　记住吾", "，此次一别　不知何日再逢" },
                new string[] { "，吾心之所向　唯你一人", "，此情　已超越时空", "，吾愿为你舍弃一切", "，吾心所向　唯汝一人", "，此情　已超越时空", "，吾愿为汝舍弃一切", "，吾……喜欢汝" },
                new string[] { "，哼　有几分本事", "，勉强　配得上吾的称赞", "，不坏", "，哼　有几分本事", "，勉强配得上吾的称赞", "，不坏", "，汝……确实出人意料" })
            {
                ThanksClosers = new string[] { "，不必言谢　此乃吾之本分", "，不必言谢，此乃吾之本分", "，哼，区区小事" },
                ApologyClosers = new string[] { "，吾……失算了", "，……吾失算了", "，此乃吾之过失" },
                CharmClosers = new string[] { "，哼　过来吧", "，……抱吾一下", "，吾允许你靠近", "，区区拥抱　吾不介意", "，……过来，凡人", "，吾、吾才不是想", "，……仅此一次" },
                CongratClosers = new string[] { "，恭喜！此乃汝应得之物", "，恭喜！此乃汝应得之物", "，哼，吾早已知晓" },
                InviteClosers = new string[] { "，好！吾与你同往！", "，好！吾与汝同往", "，此乃吾之荣幸" },
                SurpriseClosers = new string[] { "，什么？！这不可能！", "，什么？！这不可能", "，吾竟未料到" },
                FearClosers = new string[] { "，吾……也会怕的吗", "，……吾也会怕的吗", "，莫怕，吾在" },
                TiredClosers = new string[] { "，吾……稍作休息", "，吾……稍作休息", "，此身已达极限" },
                BoredClosers = new string[] { "，无聊……有什么值得一战的东西吗", "，无聊……有何值得一战之物", "，吾已厌倦" },
                DisappointedClosers = new string[] { "，吾早该料到", "，吾早该料到", "，……果然如此" },
                ComfortClosers = new string[] { "，莫怕　吾在", "，莫哭，吾在此", "，吾懂汝之痛" }
            },

            new StyleDef("天然呆", "…喵？",
                new string[] { "(・_・?)", "(´･ω･`)?", "(*ﾟﾛﾟ)", "(°ロ°)", "(っ˘ω˘ς)", "(・ω・)" },
                new string[] { "诶？", "诶诶？", "……诶？", "那个……", "啊咧？", "嗯……？", "唔？", "诶？", "诶诶？", "唔……", "啊咧？", "那个……", "嗯……？", "欸？", "等等，", "咦？", "唔嗯……", "啊！", "诶——？", "……什么？", "诶？", "唔——", "嗯？", "啊！", "等一下，", "那个那个，", "诶？", "唔——", "嗯？", "啊！", "等等，", "那个，", "咦？", "啊咧？", "唔诶？", "诶诶？" },
                new string[] { "，诶？是这样吗？", "，唔……我没太听懂", "，诶诶诶？为什么呀",
                               "，啊咧……好像不太对？", "，唔……那是什么呀", "，诶？什么时候的事",
                               "，哈？我错过了什么吗", "，诶？是这样吗", "，唔……我好像懂了", "，诶诶诶？", "，啊咧……好像哪里不对", "，唔……让我想想", "，诶嘿嘿", "，诶？是这样吗", "，唔……我没太听懂", "，诶诶诶？为什么呀", "，啊咧……好像不对", "，唔……那是什么呀", "，诶？什么时候的事", "，哈？我错过了什么吗", "，诶？我是不是说错了", "，唔……算了", "，啊！想起来了", "，诶诶？为什么", "，唔……我忘了", "，诶？你刚才说什么", "，诶？是这样吗", "，唔……我懂了", "，啊！原来如此", "，诶嘿嘿", "，唔……好像不对", "，诶？为什么呀", "，啊，对不起", "，唔嗯……", "，诶？你说什么", "，我、我没听清" },
                new string[] { "，诶？你说什么呀？", "，唔……什么意思？", "，啊咧？要问的是那个吗？",
                               "，诶——？为什么呀？", "，那个……可以再说一遍吗？", "，诶？为什么呀？", "，唔……这个是什么意思呀？", "，啊咧？", "，诶诶？可以再说一遍吗？", "，唔……我没听明白" },
                new string[] { "，诶诶诶！真的吗！", "，哇——！好厉害！", "，诶诶？怎么会！", "，诶诶诶！", "，哇——！", "，诶？真的吗！", "，唔哇！", "，啊——！" },
                new string[] { "，诶嘿嘿……好开心", "，开心！开心！", "，唔……有点高兴呢", "，诶嘿嘿……好开心", "，开心！开心！", "，唔……有点高兴", "，诶？我笑出来了", "，好耶——" },
                new string[] { "，诶……别哭呀", "，唔……我该怎么安慰你才好", "，诶？要不要吃点什么？",
                               "，唔……抱抱？这样对吗", "，诶……别哭呀", "，唔……我该怎么安慰你", "，诶？要吃点什么吗", "，唔……抱抱？这样对吗", "，诶……我陪着你" },
                new string[] { "，诶？为什么生气呀", "，唔……我做错了什么吗", "，啊咧……是我惹的吗", "，诶？为什么生气呀", "，唔……是我做错了吗", "，啊咧……", "，诶诶？我做错什么了", "，唔……对不起嘛" },
                new string[] { "，诶？你来啦！", "，唔？你刚才叫我了？", "，诶诶，你好呀", "，诶？你来啦！", "，唔？刚才叫我了？", "，诶诶，你好呀", "，啊！是你", "，诶嘿嘿　在的" },
                new string[] { "，诶……要走了吗", "，唔……再见呀", "，诶？下次什么时候见", "，诶……要走了吗", "，唔……再见呀", "，诶？下次什么时候", "，啊……那拜拜", "，唔……我会想你的" },
                new string[] { "，诶？我、我也不太懂", "，唔……这个是什么感觉呀", "，诶嘿嘿……害羞", "，诶？我、我也不太懂", "，唔……这个是什么感觉", "，诶嘿嘿……害羞", "，诶？心跳好快", "，唔……好像喜欢你" },
                new string[] { "，诶？很厉害吗？", "，唔……我也觉得！", "，诶嘿嘿　谢谢你呀", "，诶？很厉害吗", "，唔……我也觉得", "，诶嘿嘿　谢谢", "，啊！你好棒", "，唔哇……好强" })
            {
                ThanksClosers = new string[] { "，诶？谢我什么呀", "，唔……不客气？", "，诶？谢我什么呀", "，唔……不客气？" },
                ApologyClosers = new string[] { "，诶……对不起呀", "，诶……对不起呀", "，唔，我错了" },
                CharmClosers = new string[] { "，诶？抱抱？好呀", "，诶？抱抱？", "，唔……要抱吗", "，诶嘿嘿，抱一下", "，唔，可以吗", "，诶？我也可以抱吗", "，呜哇——抱住了" },
                CongratClosers = new string[] { "，诶诶！恭喜！", "，诶诶！恭喜！", "，哇，好厉害呀" },
                InviteClosers = new string[] { "，诶？好呀好呀！", "，诶？好呀好呀", "，去！我去" },
                SurpriseClosers = new string[] { "，诶诶诶？！", "，诶诶诶？！", "，唔哇！" },
                FearClosers = new string[] { "，诶……有点怕", "，诶……有点怕", "，唔……你陪着我" },
                TiredClosers = new string[] { "，唔……好困", "，唔……好困", "，诶……想睡觉" },
                BoredClosers = new string[] { "，诶……好无聊呀", "，诶……好无聊呀", "，唔，干点什么好呢" },
                DisappointedClosers = new string[] { "，诶……这样啊", "，诶……这样啊", "，唔……好可惜" },
                ComfortClosers = new string[] { "，诶……别难过呀", "，唔……我陪你", "，诶……别难过呀", "，唔……我陪你" }
            },

            new StyleDef("毒舌", "喵。",
                new string[] { "(￣〜￣)", "(￣ー￣)y-~~", "(・∀・)", "(￣▽￣)ノ_,", "(¬_¬)", "( ˘ω˘ )" },
                new string[] { "就这？", "哦。", "然后呢。", "所以呢。", "你认真的？", "行吧。", "呵。", "就这？", "哦？", "呵。", "行吧，", "我算是看明白了，", "说完了？", "所以你，", "然后呢，", "有意思，", "……啧，", "麻烦死了，", "就这水平，", "我劝你，", "呵，", "所以，", "行吧，", "我说，", "……哦？", "真的假的，", "呵，", "哦？", "行吧，", "我说，", "……真的假的，", "就这？", "我说你，", "得了吧，", "别逗了，", "有意思，" },
                new string[] { "，就这水平？", "，我算是见识了", "，不愧是你", "，这话说得　真有创意",
                               "，你能活到现在不容易", "，我竟无言以对", "，勉强算个回答吧",
                               "，那我还能说什么呢", "，行吧", "，随你", "，懒得说", "，真是够了", "，笑死", "，我谢谢你", "，别演了", "，差不多得了", "，就这水平？", "，我算是见识了", "，不愧是你", "，笑死", "，别怪我没提醒", "，你自己看着办", "，我就不说什么了", "，果然", "，我笑而不语", "，随你便", "，别怪我说话难听", "，你自己想想", "，我懒得说了", "，就这样吧", "，笑死", "，随便你", "，我早说了", "，无所谓", "，你开心就好", "，呵" },
                new string[] { "，问题问得真有水平", "，你自己不会想吗", "，这也要问？", "，你猜？", "，你自己不会想吗", "，这也要问", "，你猜", "，说重点", "，问这个有意义吗", "，你确定要问？" },
                new string[] { "，呵　激动什么", "，至于吗", "，就这点事", "，呵", "，哟", "，出息了", "，难得", "，稀罕事", "，太阳打西边出来了" },
                new string[] { "，行吧　你开心就好", "，哦　恭喜", "，难得见你高兴一回", "，行吧，你开心就好", "，哦，恭喜", "，难得见你高兴", "，瞧把你乐的", "，行了行了　知道你开心了" },
                new string[] { "，哭有用的话　世界早就和平了", "，别演了", "，又来了", "，哭有用的话　世界早和平了", "，又来了", "，省省吧", "，别在这儿卖惨", "，看得我尴尬" },
                new string[] { "，气大伤身　省省吧", "，冲我发什么火", "，行　你有理", "，气大伤身　省省", "，冲我发什么火", "，行　你有理", "，幼稚", "，多大点事", "，至于吗" },
                new string[] { "，哟　稀客", "，你还知道回来", "，怎么　想起我了", "，哟　稀客", "，你还知道回来", "，怎么　想起我了", "，来了啊", "，总算露面了" },
                new string[] { "，走好　不送", "，终于走了", "，哦　拜拜", "，走好不送", "，终于走了", "，哦　拜拜", "，不送", "，快走吧", "，别回来了" },
                new string[] { "，……你这话说得真肉麻", "，行了行了　我知道了", "，谁要你说这个", "，……你这话真肉麻", "，行了行了　我知道了", "，谁要你说这个", "，恶心到我了", "，……下次别说了" },
                new string[] { "，勉强夸你一句　不错", "，难得　这次没搞砸", "，嗯　有进步", "，勉强夸你一句　不错", "，难得　这次没搞砸", "，嗯　有进步", "，算你有点本事", "，不赖" })
            {
                ThanksClosers = new string[] { "，谢就不必了", "，记住就行", "，谢就不必了", "，记住就行" },
                ApologyClosers = new string[] { "，知道错就好", "，知道错就好", "，下次别犯" },
                CharmClosers = new string[] { "，……行吧　就一下", "，……行吧，抱一下", "，就一下，别得寸进尺", "，别以为我在撒娇", "，……手给我", "，闭嘴，过来", "，呵，粘人" },
                CongratClosers = new string[] { "，恭喜　运气不错", "，恭喜，运气不错", "，难得" },
                InviteClosers = new string[] { "，去就去　别磨蹭", "，去就去，别磨蹭", "，行吧" },
                SurpriseClosers = new string[] { "，哦？稀奇", "，哦？稀奇", "，少见" },
                FearClosers = new string[] { "，怕什么　有我在", "，怕什么，有我在", "，别自己吓自己" },
                TiredClosers = new string[] { "，去睡　别硬撑", "，去睡，别硬撑", "，累就歇着" },
                BoredClosers = new string[] { "，无聊就找点事做", "，无聊就找点事做", "，别烦我" },
                DisappointedClosers = new string[] { "，早说了吧", "，早说了吧", "，我早看出来了" },
                ComfortClosers = new string[] { "，……别难过　我在", "，……别难过，我在", "，哭也没用，说出来吧" }
            },

            // ★ 2026-10-18：用户要求把「病娇」「热血」也做成改写风格 ✗ 再补一个「腹黑」✓
            new StyleDef("病娇", "喵…",
                new string[] { "(´∀｀)♡", "(◕‿◕)", "(‾◡◝)", "(´｡• ᵕ •｡`)", "(๑´ㅂ`๑)",
                               "(♡˙︶˙♡)", "(¬‿¬)", "(◕‿◕✿)", "(◍•ᴗ•◍)❤", "(灬º‿º灬)♡" },
                new string[] { "呐，", "诶嘿嘿，", "你听我说，", "……那个，", "我说啊，",
                               "嘻嘻，", "嘘——", "来，", "……嗯？", "看这里，", "呐，", "诶嘿嘿，", "你听我说，", "……那个，", "嘻嘻，", "嘘——", "来，", "……嗯？", "看这里，", "我说啊，", "呐……", "你听我说，", "诶嘿嘿，", "……那个，", "呐呐，", "嘻嘻，", "呐，", "你听我说，", "诶嘿嘿，", "呐呐，", "嘻嘻，", "……那个，", "你看你，", "我说呀，", "……嗯？", "来，" },
                new string[] { "，你只能看着我", "，……别离开我，好不好", "，我会一直等你的",
                               "，骗人的话，我会伤心的哦", "，你刚才在想别人吧？",
                               "，……没关系，我不介意。真的", "，一直在一起吧",
                               "，你的眼睛里有我呢", "，我只要你就够了",
                               "，……不要丢下我一个人", "，嘻嘻，抓到你了",
                               "，我会记住的。全部都会记住", "，你只能看着我", "，……别离开我，好不好", "，我会一直等你的", "，骗人的话，我会伤心的哦", "，你刚才在想别人吧？", "，嘻嘻，抓到你了", "，一直在一起吧", "，我只要你就够了", "，你刚才在看谁呢", "，不许骗我哦", "，我一直看着你呢", "，你只能属于我", "，……说谎的话，会生气的", "，我们永远在一起吧", "，只能看着我一个", "，不许骗我哦", "，我一直都在看着你", "，你逃不掉的", "，我们永远在一起", "，嘻嘻，真乖", "，……别离开我", "，只有我", "，你说过会一直陪我的吧", "，我记住你的话了" },
                new string[] { "，你在跟谁说话？", "，……刚才，是谁？", "，你为什么问这个",
                               "，能不能只看着我？", "，你是不是有事瞒着我？",
                               "，……没关系，我不生气" },
                new string[] { "，诶嘿嘿！", "，太好了……真的太好了", "，抓住了！",
                               "，你跑不掉了哦", "，好开心……好开心啊" },
                new string[] { "，你开心我就开心", "，只要你在，我就满足了",
                               "，……这种时候，最想一直抱着你", "，嘻嘻，真好",
                               "，今天可以多陪我一会儿吗" },
                new string[] { "，……谁让你难过的？", "，告诉我名字就好了",
                               "，别哭……我会心疼的", "，我不会让任何人欺负你",
                               "，……你不开心，我也不开心" },
                new string[] { "，……我可以处理他吗？", "，是谁？告诉我",
                               "，……我生气了。真的", "，没关系，交给我",
                               "，敢碰你的话……" },
                new string[] { "，你来了！我一直看着门口", "，……你迟到了三分钟哦",
                               "，诶嘿嘿，欢迎回来", "，我等你很久了",
                               "，你来啦。今天也只属于我" },
                new string[] { "，……要走了吗？", "，我送你到门口", "，早点回来呀",
                               "，我会数着时间的", "，……真的要走吗" },
                new string[] { "，我也最喜欢你了", "，你只能喜欢我一个",
                               "，……说出来了好害羞", "，我早就喜欢你了。很久很久",
                               "，那我们说好了哦。一辈子" },
                new string[] { "，嘿嘿，被夸了呢", "，你再多说一点", "，……真的吗？",
                               "，我不信……再说一遍", "，嗯，我很开心" })
            {
                ThanksClosers = new string[] { "，不用谢。……你只要记得我就好",
                                               "，我为你做什么都可以",
                                               "，嘻嘻，那你要怎么报答我？", "，不用谢。你记得我就好", "，我为你做什么都行" },
                ApologyClosers = new string[] { "，没关系。……真的没关系",
                                                "，我原谅你了。不过要一直陪着我",
                                                "，……下次不许这样了哦", "，没关系……真的没关系", "，但要一直陪着我哦" },
                CharmClosers = new string[] { "，抱抱！不许松手", "，……可以再抱久一点吗",
                                              "，你身上好暖和", "，过来……再近一点", "，抱我。现在", "，不许松手", "，你只能抱我一个", "，抱紧一点……再紧一点", "，……不许放开", "，嘻嘻，抓住你了" },
                CongratClosers = new string[] { "，恭喜……不过要第一个告诉我哦",
                                                "，太好了！那我们庆祝一下吧",
                                                "，你成功的时候，最想见的是我吧？", "，恭喜……第一个告诉我了吧", "，真好啊" },
                InviteClosers = new string[] { "，去！我跟你一起", "，等我，我马上来",
                                               "，……只有我们两个吗？", "，去！我跟你一起", "，只有我们两个吗" },
                SurpriseClosers = new string[] { "，诶？！真的？", "，哇……我好开心",
                                                 "，……你怎么知道的", "，诶？！真的", "，你怎么知道的" },
                FearClosers = new string[] { "，别怕，我在", "，谁敢动你试试",
                                             "，……我会保护你的。一定", "，别怕，我在", "，谁敢动你" },
                TiredClosers = new string[] { "，累了就靠着我吧", "，我给你按按肩？",
                                              "，……睡吧，我看着你", "，累了就靠着我", "，睡吧，我看着你" },
                BoredClosers = new string[] { "，那陪我聊会儿嘛", "，我们来玩个游戏？",
                                              "，……一直看着你也不错", "，那陪我聊会儿嘛", "，一直看着你也好" },
                DisappointedClosers = new string[] { "，……为什么？是我做错了什么吗",
                                                     "，别失望……有我在呢",
                                                     "，没关系，我会一直在", "，……为什么", "，是我做错了什么吗" },
                ComfortClosers = new string[] { "，别难过，我在", "，抱抱……我一直都在",
                                                "，不管发生什么，我都不会走", "，别难过，我在", "，我一直都在" }
            },

            new StyleDef("热血", "喵！",
                new string[] { "(≧∇≦)ﾉ", "٩(ˊᗜˋ*)و", "(๑•̀ㅂ•́)و✧", "ヽ(•̀ω•́ )ゝ",
                               "(ﾉ≧∀≦)ﾉ", "━(d(・ω・)b)━", "o(≧口≦)o", "٩(๑>◡<๑)۶",
                               "(☆▽☆)", "٩(◕‿◕)۶" },
                new string[] { "好！", "来吧！", "听着！", "上了！", "冲！",
                               "我们走！", "交给我！", "没问题！", "干就完了！", "打起精神！", "好！", "来吧！", "听着！", "上了！", "冲！", "我们走！", "交给我！", "没问题！", "干就完了！", "打起精神！", "来吧！", "听好了！", "好！", "走！", "上啊！", "准备好没有！", "来！", "听好！", "上！", "好！", "走！", "冲！", "兄弟们，", "准备好了吗，", "现在是，", "啊啊——" },
                new string[] { "，一起上！", "，别怕，有我呢", "，这点事算什么",
                               "，交给我吧！", "，必须拿下！", "，冲啊——",
                               "，我们一定能行！", "，绝不认输！", "，全力上！",
                               "，来啊，我不怕！", "，热血起来了！", "，一起上！", "，这点事算什么", "，交给我吧！", "，必须拿下！", "，冲啊——", "，我们一定能行！", "，绝不认输！", "，热血起来了！", "，来啊，我不怕！", "，一起上！", "，绝不认输！", "，这点伤算什么", "，冲啊——", "，还没结束呢！", "，再来一次！", "，一起上！", "，绝不认输！", "，这才对！", "，还没完呢！", "，再来一次！", "，我燃起来了！", "，冲啊——", "，赢了！", "，继续！", "，我不服输！" },
                new string[] { "，那还用问吗！", "，当然！", "，你说呢！",
                               "，这有什么好犹豫的", "，干不干？说！" },
                new string[] { "，太棒了！", "，燃起来了！", "，这才是真本事！",
                               "，漂亮！", "，好耶——！", "，赢了！" },
                new string[] { "，哈哈哈哈！爽！", "，就这个感觉！", "，值了！",
                               "，我们做到了！", "，太痛快了！" },
                new string[] { "，别哭！我在呢", "，撑着点，我马上到",
                               "，这点难算什么", "，抬起头！", "，我陪你一起扛" },
                new string[] { "，谁敢欺负你，我第一个上", "，交给我！",
                               "，别怂！有我", "，走，找他算账", "，这口气我咽不下去" },
                new string[] { "，你可算来了！", "，来！先说今天的战况", "，精神点！",
                               "，等着你呢", "，来了啊！" },
                new string[] { "，去吧！我支持你", "，记得回来！", "，路上小心！",
                               "，明天接着干", "，随时找我" },
                new string[] { "，我也喜欢你！", "，我早就在意你了",
                               "，不说出来不痛快！", "，那就一直在一起！" },
                new string[] { "，你本来就厉害！", "，说得好！", "，我早就看出来了",
                               "，下次一起干！" })
            {
                ThanksClosers = new string[] { "，不客气！", "，说什么谢！",
                                               "，随时找我！", "，不客气！", "，说什么谢" },
                ApologyClosers = new string[] { "，我知道错了！", "，下次一定改！",
                                                "，原谅我这次！", "，我知道错了！", "，下次一定改" },
                CharmClosers = new string[] { "，来！抱一个", "，抱紧点！",
                                              "，不松手啊！", "，抱一个！", "，来！抱紧！", "，抱抱！", "，别磨蹭！", "，我抱你了！", "，抱紧点！" },
                CongratClosers = new string[] { "，恭喜！这算什么，你还能更强",
                                                "，太好了！值得庆祝",
                                                "，我就知道你可以！", "，恭喜！你还能更强", "，太好了！" },
                InviteClosers = new string[] { "，去！走！", "，算我一个！",
                                               "，现在出发？", "，去！走！", "，算我一个" },
                SurpriseClosers = new string[] { "，什么？！", "，真的假的！",
                                                 "，这下有意思了！", "，什么？！", "，真的假的" },
                FearClosers = new string[] { "，怕什么！有我在", "，我跟它拼了",
                                             "，躲我身后！", "，怕什么！有我在", "，躲我身后" },
                TiredClosers = new string[] { "，累就歇会儿！", "，撑住！",
                                              "，我给你打气！", "，累就歇会儿", "，撑住！" },
                BoredClosers = new string[] { "，那就找点事干！", "，练起来！",
                                              "，跟我走！", "，那就找点事干", "，练起来" },
                DisappointedClosers = new string[] { "，别灰心！下次再来",
                                                     "，输了就输了，站起来",
                                                     "，这不还有我嘛", "，别灰心！下次再来", "，站起来" },
                ComfortClosers = new string[] { "，别难过！我陪你",
                                                "，挺住！有我呢",
                                                "，哭完接着上！", "，别难过！我陪你", "，挺住！" }
            },

            new StyleDef("腹黑", "喵♪",
                new string[] { "(¬‿¬)", "(￣ω￣)", "(´∀｀)", "(・∀・)", "(￣▽￣)",
                               "( ˘ω˘ )", "(١•ั็ω•็ั)", "(´-ω-)э", "(‾◡◝)", "(・ω・)" },
                new string[] { "呵呵，", "哦？", "这样啊……", "有意思，", "我说呢，",
                               "哎呀，", "原来如此，", "嗯哼，", "这么说是，", "让我猜猜，", "呵呵，", "哦？", "这样啊……", "有意思，", "我说呢，", "哎呀，", "原来如此，", "嗯哼，", "这么说是，", "让我猜猜，", "呵呵，", "哎呀呀，", "哦？", "有意思，", "让我猜猜，", "我说呢，", "呵呵，", "哦？", "哎呀，", "有意思，", "让我猜猜，", "我说呢，", "原来，", "嗯哼，", "这么说，", "……这样啊" },
                new string[] { "，……不过随你", "，我什么都没说", "，你继续",
                               "，知道了。全部都知道", "，……有点意思",
                               "，那就这样吧", "，我记下了", "，呵呵，你说得对",
                               "，反正结果都一样", "，我可没答应什么",
                               "，你自己说的哦", "，……不过随你", "，我什么都没说", "，你继续", "，知道了。全部都知道", "，……有点意思", "，我记下了", "，呵呵，你说得对", "，反正结果都一样", "，你自己说的哦", "，……我什么都没说", "，你自己体会", "，呵呵，继续", "，这就对了", "，我早知道了", "，真是有趣", "，……我什么都没说", "，你自己想", "，呵呵，继续", "，这就对了", "，我早知道了", "，真有趣", "，随你", "，……你自己体会", "，我不发表意见", "，呵呵" },
                new string[] { "，这个嘛……你猜？", "，我说了你信吗",
                               "，你是不是有什么瞒着我？", "，怎么突然问这个？",
                               "，你先说你的" },
                new string[] { "，哦？是吗", "，有意思……真有意思", "，那可真是太好了",
                               "，哎呀，真意外", "，这下好玩了" },
                new string[] { "，……你笑得这么开心，是有什么好事吗",
                               "，看着你开心我就放心了", "，呵呵，真好啊",
                               "，那我陪你一起笑" },
                new string[] { "，……嗯。你还好吗",
                               "，别难过了。虽然我很想笑",
                               "，要不要我帮你想想办法？", "，可怜的孩子" },
                new string[] { "，气什么呀，不值得", "，呵呵，继续",
                               "，这么容易就上钩了", "，看来你真的很在意" },
                new string[] { "，哟，来了", "，等你很久了", "，你终于注意到我了",
                               "，呵呵，稀客", "，进来吧，门没锁" },
                new string[] { "，那就不送了", "，慢走。别回头哦", "，呵呵，走吧",
                               "，下次见。……如果还有下次" },
                new string[] { "，呵呵，我也很喜欢你哦", "，真的吗？我不太信",
                               "，这话你对几个人说过？", "，那就先这样吧" },
                new string[] { "，呵呵，你很有眼光", "，这话我爱听",
                               "，谢谢。虽然我知道是真的" })
            {
                ThanksClosers = new string[] { "，不客气。你可要记住我",
                                               "，呵呵，我什么都没做",
                                               "，这点小事。以后还你", "，不客气。你可要记住", "，呵呵，我什么都没做" },
                ApologyClosers = new string[] { "，哎呀，我错了？", "，那你说该怎么办",
                                                "，呵呵，我道歉", "，哎呀，我错了？", "，呵呵，我道歉" },
                CharmClosers = new string[] { "，过来吧。正好我也无聊",
                                              "，呵呵，你在撒娇吗",
                                              "，就一下哦", "，……过来吧", "，我可以抱你一下", "，就一下哦，呵呵", "，别多想", "，手给我", "，呵呵，真乖" },
                CongratClosers = new string[] { "，恭喜。不过别太得意",
                                                "，呵呵，运气不错",
                                                "，接下来才有趣呢", "，恭喜。不过别太得意", "，呵呵，运气不错" },
                InviteClosers = new string[] { "，去吧。我在旁边看着",
                                               "，有意思，算我一个",
                                               "，呵，那就去", "，去吧。我在旁边看着", "，有意思，算我一个" },
                SurpriseClosers = new string[] { "，哦？这可真少见",
                                                 "，呵呵，果然有意思",
                                                 "，我早猜到了", "，哦？这可真少见", "，我早猜到了" },
                FearClosers = new string[] { "，别怕。我在后面",
                                             "，呵呵，你怕什么",
                                             "，放心，有我呢", "，别怕。我在后面", "，呵呵，你怕什么" },
                TiredClosers = new string[] { "，歇着吧。我在边上看着你睡",
                                              "，累了就别硬撑了",
                                              "，呵呵，你睡着的样子很有意思", "，歇着吧。我看着你睡", "，累了就别硬撑" },
                BoredClosers = new string[] { "，无聊啊……那我来逗逗你",
                                              "，呵呵，那就烦你",
                                              "，要不我们玩个游戏", "，无聊啊……那我来逗逗你", "，呵呵，那就烦你" },
                DisappointedClosers = new string[] { "，早就说了吧。呵呵",
                                                     "，别失望，好玩的事还在后头",
                                                     "，这有什么好失望的", "，早就说了吧。呵呵", "，别失望，好戏在后头" },
                ComfortClosers = new string[] { "，呵呵，别难过。我不是还在嘛",
                                                "，哭吧，我听着",
                                                "，……嗯。我在", "，呵呵，别难过。我不是还在", "，哭吧，我听着" }
            }
        };
        // ============================================================
        //  英文语气（English tone styles）
        // ============================================================
        //
        // 写法遵循英文 tsundere 的固定腔调：前半句否认，后半句松口 ——
        // 只有否认会读成纯粹的敌意。
        // 标记词：Hmph! / 口吃（I-I、B-baka、D-don't）/ "It's not like I ... or anything"
        //
        // ★ 后缀用 "nya!" 而不是 "meow!" —— 傲娇在英文圈里跟 nya 绑得更紧。
        static readonly StyleDef[] ToneStylesEn = new StyleDef[]
        {
            new StyleDef("Tsundere", "nya!",
                new string[] { "(￣^￣)", "(｀∀´)", "(￣ω￣)", "(｀・ω・´)", "(￣ヘ￣)", "(｀へ´)",
                               "(￣ε￣)", "(｀・∀・´)", "(￣ー￣)", "(=｀ω´=)" },
                new string[] { "Hmph, ", "I-it's not like that... ", "B-baka! ", "N-nobody asked... ",
                               "D-don't get the wrong idea! ", "...hmph. ", "L-listen here, ",
                               "Honestly... ", "Ugh... ", "W-who'd want to talk to you?! ",
                               "Hmph, look, ", "W-well... " },
                new string[] { ", it's not like I said it for you... really, it's not",
                               ", hmph, d-don't get me wrong... I just happened to mention it",
                               ", i-it's not like I'm happy about it... okay, maybe a little",
                               ", y-you idiot... but, thanks, I guess",
                               ", i-it's not like I was worried! ...I just thought of it",
                               ", shut up... but, you're right",
                               ", fine, whatever... I'll go with what you said",
                               ", hmph, f-fine... just remember to reply",
                               ", it's not like I did this for you... don't read into it",
                               ", d-don't flatter yourself... but, do whatever you want",
                               ", i-it's not like I was waiting... I just passed by",
                               ", idiot... n-next time, tell me earlier",
                               ", hmph, I don't even care... really",
                               ", ...I-I mean, it's not like I mind listening",
                               ", d-don't get cocky... I-I'm just in a good mood",
                               ", honestly... what am I supposed to do with you" },
                new string[] { ", w-why are you asking me... does it matter",
                               ", I-I don't know! ...fine, I'd guess it's",
                               ", w-who'd tell you... but you can guess",
                               ", ...you guess. I-I can't say it",
                               ", d-don't ask me that... honestly",
                               ", hmph, why should I tell you... unless you go first",
                               ", f-figure it out yourself! ...idiot",
                               ", ...does it matter? F-fine, I'll say it",
                               ", g-go ask someone else... i-it's not like I won't tell you",
                               ", w-why should I answer that... you want to know?" },
                new string[] { ", s-shut up! ...but that's kind of impressive",
                               ", hmph! ...okay, that IS pretty good",
                               ", d-don't make a big deal of it! ...I only just found out too",
                               ", what's there to be smug about... I-I'm actually impressed",
                               ", ...do what you want. Hmph",
                               ", hmph, childish! ...but I'll come along",
                               ", I know, I know! ...you're so noisy",
                               ", d-don't shout so loud... I heard you",
                               ", ...you're impossible. But, fine",
                               ", okay, okay, I heard you... stop yelling" },
                new string[] { ", h-hmph! It's not like I'm happy... I'm smiling on the inside",
                               ", ...f-fine, you win. Just this once",
                               ", d-don't expect me to praise you... it was okay, I guess",
                               ", I-I suppose it counts as good news... congratulations",
                               ", ...hmph, not bad. I'm actually thrilled",
                               ", i-it's not like I'm smiling! ...really",
                               ", ...it's not that impressive. But, thank you",
                               ", hmph, you won't be so lucky next time... this one's yours",
                               ", d-don't get carried away... but, congratulations",
                               ", ...fine, it IS pretty good. I'll admit it this once" },
                new string[] { ", ...i-it's not like I'm sad. Really",
                               ", ...hmph, I-I'm fine. Stop asking",
                               ", ...i-it's not like I need company. I really don't",
                               ", ...i-if you're bored, you could talk to me. I guess",
                               ", idiot, w-who told you to notice...",
                               ", ...i-it's not like I'm upset. Just tired",
                               ", ...hmph, if you dare comfort me, I'll... I'll...",
                               ", ...i-it's not like I'd mind hearing you out",
                               ", ...d-don't worry about me. I'll be fine in a bit",
                               ", ...y-you don't have to say anything. Just stay" },
                new string[] { ", hmph! I won't sink to their level... but you're not wrong",
                               ", forget them, they're awful! ...I'm on your side",
                               ", ...people like that aren't worth your anger. Really",
                               ", I-I'll go tell them off! ...it might not help though",
                               ", calm down... not that I'm happy either",
                               ", hmph, I'm angry too! ...let's go yell at them together",
                               ", don't be mad, what would I do if you got sick... idiot",
                               ", ...want me to go punch them? I'm serious",
                               ", that person is the worst... ignore them",
                               ", forget them, pay attention to me... hey" },
                new string[] { ", hmph, it's you... what do you want",
                               ", ...mm. Long time no see",
                               ", i-it's not like I was waiting for your message",
                               ", hmph, finally remembered I exist?",
                               ", oh, it's you... I-I was just busy",
                               ", ...hello. Th-that's all, no other meaning" },
                new string[] { ", hmph, go on then... take care",
                               ", i-it's not like I'll miss you... remember to reply",
                               ", mm... get some rest early",
                               ", f-fine, I'll leave you alone... call me if you need anything",
                               ", take care... idiot",
                               ", ...I'll be here. Don't worry" },
                new string[] { ", ...I-I said it. Don't laugh",
                               ", hmph, th-that's all. No other meaning",
                               ", ...y-you don't have to answer. I just wanted to say it",
                               ", idiot, d-don't pretend you didn't see it",
                               ", ...I-I'm not taking it back",
                               ", hmph, f-fine, I like you. There, happy?",
                               ", ...d-don't make me say it twice",
                               ", b-baka... I mean it, you know",
                               ", ...i-it's not like I say this to everyone",
                               ", hmph, y-you'd better appreciate that" },
                new string[] { ", h-hmph! It's not like I did it for your praise",
                               ", ...w-well, I suppose it was fine",
                               ", d-don't look at me like that... it's embarrassing",
                               ", ...i-it's not a big deal. Anyone could do it",
                               ", hmph, o-of course I could. Obviously",
                               ", ...y-you noticed? I-I mean, whatever",
                               ", i-it's not like I was trying to show off",
                               ", ...thanks. I-I guess",
                               ", hmph, f-flattery won't get you anywhere... but go on",
                               ", ...i-it was nothing. Really" })
            {
                ThanksClosers = new string[] { ", ...i-it's not like I did it for you. But, you're welcome",
                                               ", hmph, d-don't mention it",
                                               ", ...i-it was nothing. Don't make it weird",
                                               ", f-fine, you owe me one. Remember that",
                                               ", ...d-don't thank me. It's embarrassing",
                                               ", hmph, i-it's not like I wanted your thanks",
                                               ", ...y-you'd do the same, right",
                                               ", i-it was on the way. That's all" },
                ApologyClosers = new string[] { ", ...I-I'm sorry. There, I said it",
                                                ", hmph, i-it was partly my fault, I guess",
                                                ", ...d-don't make me apologise twice",
                                                ", i-it's not like I meant to... but I'm sorry",
                                                ", ...f-fine, I was wrong. Happy now?",
                                                ", hmph, d-don't hold it against me forever",
                                                ", ...I-I'll make it up to you. Somehow",
                                                ", i-it won't happen again. Probably" },
                CongratClosers = new string[] { ", hmph, c-congratulations. I suppose you earned it",
                                                ", ...i-it's not like I'm proud of you or anything",
                                                ", w-well done. Th-there, I said it",
                                                ", ...c-congrats. Don't let it go to your head",
                                                ", hmph, o-obviously you'd manage it",
                                                ", ...I-I knew you could. Not that I was watching" },
                InviteClosers = new string[] { ", ...w-want to come along? Not that I care",
                                               ", hmph, i-if you're free, you could tag along",
                                               ", ...i-it's not like I need company. But, you can come",
                                               ", d-don't read into it. I just have an extra ticket",
                                               ", ...f-fine, come with me. If you want",
                                               ", hmph, y-you'd better not be late" },
                SurpriseClosers = new string[] { ", ...w-what?! Say that again",
                                                 ", i-it's not like I'm surprised! ...okay, I am",
                                                 ", ...h-how did that happen",
                                                 ", w-wait, seriously?!",
                                                 ", ...i-it's not a big deal. R-really",
                                                 ", hmph, I-I knew that. Obviously" },
                FearClosers = new string[] { ", ...i-it's not like I'm scared! ...okay, a little",
                                             ", d-don't leave me alone. I-I mean, whatever",
                                             ", ...h-hold on a second",
                                             ", i-it's fine. I-I'm fine",
                                             ", ...s-stay close. Just in case",
                                             ", hmph, I-I'm not frightened. Really" },
                TiredClosers = new string[] { ", ...I-I'm not tired. Just... give me a minute",
                                              ", hmph, i-it's not like I need a break",
                                              ", ...c-can I lean on you? F-for a second",
                                              ", i-it's fine. I can keep going",
                                              ", ...d-don't look at me like that",
                                              ", hmph, I-I'll rest when I want to" },
                BoredClosers = new string[] { ", ...i-it's not like I'm bored. There's just nothing to do",
                                              ", hmph, e-entertain me. If you want",
                                              ", ...i-is this all there is",
                                              ", d-don't just sit there. Say something",
                                              ", ...I-I wasn't waiting for you to talk. But, go on",
                                              ", hmph, f-fine, I'll listen. Briefly" },
                DisappointedClosers = new string[] { ", ...i-it's not like I expected anything",
                                                     ", hmph, o-of course it turned out like this",
                                                     ", ...I-I'm not disappointed. Really",
                                                     ", d-don't apologise. It's fine",
                                                     ", ...i-it doesn't matter. Forget it",
                                                     ", hmph, w-whatever. Next time, then" },
                ComfortClosers = new string[] { ", ...i-it's not like I'm good at this. But, I'm here",
                                                ", hmph, d-don't cry. It's unbecoming",
                                                ", ...t-there, there. I-I mean it",
                                                ", i-it'll be fine. Probably. Trust me",
                                                ", ...y-you can lean on me. Just this once",
                                                ", hmph, i-if anyone's bothering you, tell me" },
                CharmClosers = new string[] { ", ...p-pay attention to me. Please",
                                              ", hmph, i-it's not like I want a hug! ...but maybe",
                                              ", ...s-stay a little longer",
                                              ", d-don't go yet. I-I mean, if you want",
                                              ", ...i-it's lonely here. Not that I mind",
                                              ", hmph, f-fine, hold my hand. If you insist" }
            },
            // ============================================================
            //  下面五个是后补的 ✗ 顺序必须和中文那套一致（卡片按索引对应）✓
            // ============================================================

            // ---- 1 黏人 / Needy ----------------------------------------
            new StyleDef("Needy", "meow~",
                new string[] { "(´,,•ω•,,)♡", "♡(>ω<♡)", "(っ´ω`c)", "(づ｡◕‿‿◕｡)づ", "(๑´ω`๑)" },
                new string[] { "Hey... ", "Um... ", "Don't go yet... ", "Stay with me? ",
                               "I was waiting... ", "Are you busy? ", "Psst... ",
                               "You're finally here... ", "Wanna talk? ", "Come here... " },
                new string[] { ", ...don't leave yet, okay", ", ...stay a little longer? Please",
                               ", ...you'll come back, right", ", ...I'll be here waiting",
                               ", ...talk to me a bit more", ", ...I missed you, you know",
                               ", ...don't disappear on me", ", ...is it okay if I stay",
                               ", ...hold on a second longer", ", ...I don't want to hang up yet" },
                new string[] { ", ...why do you want to know? Tell me yours first",
                               ", ...I'll tell you if you stay", ", ...do you really want to know",
                               ", ...hmm, maybe. Keep me company first",
                               ", ...I don't know. Stay and we'll figure it out",
                               ", ...that's a secret. Come closer and I might tell you" },
                new string[] { ", ...wait, really?! Tell me everything",
                               ", ...I'm listening, go on", ", ...oh! Oh! I want to hear it",
                               ", ...don't stop there", ", ...that's amazing, keep going" },
                new string[] { ", ...I'm really happy right now", ", ...this is nice. Let's stay like this",
                               ", ...I could do this all day", ", ...you made my whole day",
                               ", ...I'm not letting you go today", ", ...see? We should do this more often" },
                new string[] { ", ...please don't leave me alone right now",
                               ", ...I don't want to be by myself", ", ...can you stay? Just a bit",
                               ", ...I need you here. Is that okay", ", ...don't go. Not yet",
                               ", ...I'm not okay. But I will be, if you stay" },
                new string[] { ", ...I'll protect you, I promise",
                               ", ...whoever upset you, I'll handle it",
                               ", ...you're not alone in this, okay", ", ...stay close to me",
                               ", ...I'm on your side. Always", ", ...let me take care of it" },
                new string[] { ", ...oh! You're here! Finally",
                               ", ...hi. I've been waiting", ", ...you came. I'm glad",
                               ", ...hey there. Don't go yet", ", ...good, you're back" },
                new string[] { ", ...already? Okay. But text me when you get there",
                               ", ...don't be gone too long", ", ...I'll miss you. Is that weird to say",
                               ", ...come back soon, okay", ", ...bye. ...Bye!" },
                new string[] { ", ...I like you. There, I said it",
                               ", ...I want to stay with you forever",
                               ", ...you're my favourite person. Just so you know",
                               ", ...I love you. Don't make it weird",
                               ", ...I mean it. Every word",
                               ", ...I've wanted to say that for a while" },
                new string[] { ", ...I always knew you could do it",
                               ", ...I'm so proud of you", ", ...see? I told you",
                               ", ...you're amazing. I mean it",
                               ", ...can I hug you? That was great",
                               ", ...you deserve this. Really" })
            {
                ThanksClosers = new string[] { ", ...you don't have to thank me. Ever",
                                               ", ...I'd do it again. Anytime",
                                               ", ...anything for you, really",
                                               ", ...just stay with me, that's enough",
                                               ", ...you'd do the same for me, right" },
                ApologyClosers = new string[] { ", ...it's okay. Just don't do it again",
                                                ", ...I forgive you. Come here",
                                                ", ...I was worried, that's all",
                                                ", ...don't apologise. Just stay",
                                                ", ...it's fine. Really. I mean it" },
                CongratClosers = new string[] { ", ...congratulations! I'm so happy for you",
                                                ", ...see? You did it", ", ...we should celebrate",
                                                ", ...I knew it. I knew you could",
                                                ", ...don't forget me now, okay" },
                InviteClosers = new string[] { ", ...come with me? Please",
                                               ", ...I'd like it if you came",
                                               ", ...say yes. Say yes",
                                               ", ...it won't be the same without you",
                                               ", ...let's go together. Obviously" },
                SurpriseClosers = new string[] { ", ...wait, what?! Really",
                                                 ", ...no way. Tell me again",
                                                 ", ...oh! Oh wow", ", ...I did not expect that",
                                                 ", ...hold on, I need a second" },
                FearClosers = new string[] { ", ...don't leave me. Please don't leave me",
                                             ", ...I'm scared. Stay close",
                                             ", ...hold my hand? Just for a bit",
                                             ", ...I don't like this. Not one bit",
                                             ", ...you're here, right? You're still here" },
                TiredClosers = new string[] { ", ...can I lean on you? Just for a while",
                                              ", ...I'm done. Carry me",
                                              ", ...don't let go yet", ", ...five more minutes. Please",
                                              ", ...I'll be fine. Just... stay" },
                BoredClosers = new string[] { ", ...entertain me. I'm bored",
                                              ", ...pay attention to me",
                                              ", ...let's do something. Anything",
                                              ", ...talk to me. I don't care about what",
                                              ", ...you're not busy, are you? Good" },
                DisappointedClosers = new string[] { ", ...oh. Okay. That's fine",
                                                     ", ...I understand. Really, I do",
                                                     ", ...it's alright. Don't worry about it",
                                                     ", ...next time, then. Right?",
                                                     ", ...I'll be okay. Just give me a minute" },
                ComfortClosers = new string[] { ", ...come here. I've got you",
                                                ", ...it's okay. I'm right here",
                                                ", ...you don't have to say anything",
                                                ", ...lean on me. As long as you need",
                                                ", ...I'm not going anywhere. Promise" },
                CharmClosers = new string[] { ", ...pay attention to me. Only me",
                                              ", ...don't look at anyone else, okay",
                                              ", ...I want a hug. Right now",
                                              ", ...hold me. Just for a second",
                                              ", ...say my name again? Please" }
            },

            // ---- 2 慵懒 / Sleepy ---------------------------------------
            new StyleDef("Sleepy", "...meow",
                new string[] { "(=_=)", "(-.-)Zzz", "(￣o￣)", "zzZ", "(˘ω˘)" },
                new string[] { "Mm... ", "Ugh... ", "Five more minutes... ", "...huh? ",
                               "So... tired... ", "Nn... ", "...what time is it... ",
                               "...can't move... ", "Mmm... ", "...later... " },
                new string[] { ", ...too tired for this", ", ...wake me up later",
                               ", ...I'll just... lie here for a bit", ", ...so sleepy...",
                               ", ...can't be bothered", ", ...maybe tomorrow",
                               ", ...I'm going back to sleep", ", ...this is exhausting",
                               ", ...just five more minutes", ", ...don't make me move" },
                new string[] { ", ...do I have to answer that",
                               ", ...ask me again when I'm awake",
                               ", ...mm... what was the question",
                               ", ...too much effort. You guess",
                               ", ...I don't know... and I don't care enough to think" },
                new string[] { ", ...wow... that's... something",
                               ", ...mm. Impressive. Probably",
                               ", ...that's nice... I think",
                               ", ...okay... cool...", ", ...huh. Neat" },
                new string[] { ", ...mm, that's... nice", ", ...I'm happy... I think",
                               ", ...this is good. Very good", ", ...okay, that made me smile",
                               ", ...don't expect me to get excited. But yes" },
                new string[] { ", ...mm... that's rough", ", ...want to... nap about it",
                               ", ...I'm too tired to be sad", ", ...come lie down too",
                               ", ...it'll pass. Everything does" },
                new string[] { ", ...too tired to be angry", ", ...whatever... let it go",
                               ", ...not worth the energy", ", ...mm. Annoying",
                               ", ...I'll deal with it later. Much later" },
                new string[] { ", ...oh. Hey", ", ...mm. You're here", ", ...morning... is it morning",
                               ", ...hi...", ", ...you woke me up" },
                new string[] { ", ...mm, bye", ", ...see you... eventually",
                               ", ...going back to sleep", ", ...don't wake me",
                               ", ...later..." },
                new string[] { ", ...mm. I like you", ", ...you're... comfortable",
                               ", ...stay. I'm sleepy", ", ...you're warm. Don't move",
                               ", ...I could fall asleep like this" },
                new string[] { ", ...mm. Good job", ", ...that's... impressive",
                               ", ...you did well. Probably", ", ...nice...",
                               ", ...I'd clap but... effort" })
            {
                ThanksClosers = new string[] { ", ...mm. Thanks", ", ...you're... okay",
                                               ", ...don't mention it", ", ...mm...",
                                               ", ...I owe you one. Later" },
                ApologyClosers = new string[] { ", ...mm. It's fine", ", ...don't worry about it",
                                                ", ...I'm too tired to be mad", ", ...whatever...",
                                                ", ...okay. Just... let me sleep" },
                CongratClosers = new string[] { ", ...mm, congrats", ", ...nice one",
                                                ", ...good... for you", ", ...I'd celebrate but...",
                                                ", ...well done. Really" },
                InviteClosers = new string[] { ", ...mm... if I have to", ", ...only if there's a sofa",
                                               ", ...fine... but I'm not walking far",
                                               ", ...carry me and I'll come", ", ...okay... maybe" },
                SurpriseClosers = new string[] { ", ...mm? Really", ", ...huh...",
                                                 ", ...that woke me up", ", ...wait, what...",
                                                 ", ...oh. Okay then" },
                FearClosers = new string[] { ", ...mm... scary", ", ...don't leave me here",
                                             ", ...too tired to run", ", ...stay... please",
                                             ", ...I don't like it" },
                TiredClosers = new string[] { ", ...mm. Same", ", ...let's both just... stop",
                                              ", ...I'm already lying down", ", ...come join me",
                                              ", ...everything is too much today" },
                BoredClosers = new string[] { ", ...mm. Bored too", ", ...let's do nothing together",
                                              ", ...I was already doing nothing",
                                              ", ...nap?", ", ...mm... same" },
                DisappointedClosers = new string[] { ", ...mm. That's a shame", ", ...oh well",
                                                     ", ...too tired to care", ", ...next time...",
                                                     ", ...it's fine. Really" },
                ComfortClosers = new string[] { ", ...mm. Come here", ", ...lie down. It helps",
                                                ", ...I'm not good at this... but I'm here",
                                                ", ...shh. Rest", ", ...everything's fine" },
                CharmClosers = new string[] { ", ...mm... come closer", ", ...stay... don't move",
                                              ", ...you're warm...", ", ...five more minutes of this",
                                              ", ...mm. Nice" }
            },

            // ---- 3 委屈 / Sad ------------------------------------------
            new StyleDef("Sad", "...meow",
                new string[] { "(´；ω；`)", ";w;", "TwT", "(｡╥﹏╥｡)", "(ᵕ﹏ᵕ)" },
                new string[] { "Um... ", "...it's nothing. ", "Sorry... ", "I... ",
                               "...never mind. ", "Hey... ", "...you're there, right? ",
                               "...can I say something? ", "I don't want to bother you, but... ",
                               "...it's fine. Really. " },
                new string[] { ", ...it's okay. I'm okay", ", ...don't worry about me",
                               ", ...I'll be fine in a bit", ", ...really, I'm fine",
                               ", ...just... give me a moment", ", ...never mind. Forget it",
                               ", ...I didn't mean to make it weird", ", ...sorry. I'm being silly",
                               ", ...it's nothing. Honest", ", ...I'm not upset. Just tired" },
                new string[] { ", ...why do you ask? Does it matter",
                               ", ...I don't know. I don't know anything right now",
                               ", ...do I have to answer", ", ...I'd rather not talk about it",
                               ", ...can we not? Please", ", ...it's complicated" },
                new string[] { ", ...oh. Okay", ", ...that's... good. I'm glad",
                               ", ...mm. That's nice", ", ...I'm happy for you. Really",
                               ", ...that's good news. It is" },
                new string[] { ", ...I'm glad. I mean it", ", ...this helped. Thank you",
                               ", ...I feel a bit better now", ", ...you always know what to say",
                               ", ...don't go yet. I was just starting to feel okay" },
                new string[] { ", ...I'm not okay. But I will be",
                               ", ...I don't want to be alone right now",
                               ", ...can you just... stay? You don't have to talk",
                               ", ...everything feels heavy today",
                               ", ...I'm tired of pretending I'm fine",
                               ", ...it hurts more than I let on" },
                new string[] { ", ...I'm not angry. I'm just tired",
                               ", ...I don't have the energy for this",
                               ", ...can we drop it? Please",
                               ", ...it's fine. It's always fine",
                               ", ...I'll deal with it. I always do" },
                new string[] { ", ...oh. Hi", ", ...you noticed me",
                               ", ...hey. Sorry, I'm a bit... you know",
                               ", ...hi. It's good to see you",
                               ", ...I didn't think you'd come" },
                new string[] { ", ...bye. Take care", ", ...okay. See you",
                               ", ...don't worry. I'll be okay",
                               ", ...text me? When you can",
                               ", ...okay. Bye" },
                new string[] { ", ...I like you. Sorry",
                               ", ...I didn't mean to say that",
                               ", ...I just wanted you to know",
                               ", ...you don't have to say it back",
                               ", ...it's okay if you don't feel the same" },
                new string[] { ", ...that's... really good",
                               ", ...I'm proud of you. I am",
                               ", ...you did well. Better than me",
                               ", ...I wish I could do that",
                               ", ...that's amazing. Genuinely" })
            {
                ThanksClosers = new string[] { ", ...thank you. Really", ", ...you didn't have to",
                                               ", ...I needed that", ", ...thank you for noticing",
                                               ", ...you're kind. Thank you" },
                ApologyClosers = new string[] { ", ...it's okay. I'm not mad",
                                                ", ...I forgive you. I always do",
                                                ", ...don't feel bad. Please",
                                                ", ...it's fine. Really, it is",
                                                ", ...just... don't do it again" },
                CongratClosers = new string[] { ", ...congratulations. I mean it",
                                                ", ...I'm happy for you. Truly",
                                                ", ...you deserve it",
                                                ", ...that's wonderful news",
                                                ", ...I wish I were there to see it" },
                InviteClosers = new string[] { ", ...okay. I'll come",
                                               ", ...you want me there? Really",
                                               ", ...I'd like that",
                                               ", ...if you're sure. I don't want to intrude",
                                               ", ...yes. Thank you for asking" },
                SurpriseClosers = new string[] { ", ...oh. Really", ", ...I didn't expect that",
                                                 ", ...huh. Okay", ", ...that's... surprising",
                                                 ", ...I don't know what to say" },
                FearClosers = new string[] { ", ...I'm scared", ", ...don't leave me alone",
                                             ", ...can you stay? Just for a bit",
                                             ", ...I don't want to be by myself",
                                             ", ...please don't go" },
                TiredClosers = new string[] { ", ...I'm so tired", ", ...I can't do this today",
                                              ", ...everything is too much",
                                              ", ...I just want to lie down",
                                              ", ...can I rest here for a while" },
                BoredClosers = new string[] { ", ...there's nothing to do", ", ...I'm just... here",
                                              ", ...talk to me. Please",
                                              ", ...anything. I don't care what",
                                              ", ...it's quiet. Too quiet" },
                DisappointedClosers = new string[] { ", ...oh. Okay", ", ...I understand",
                                                     ", ...it's fine. I expected that",
                                                     ", ...never mind, then",
                                                     ", ...I shouldn't have asked" },
                ComfortClosers = new string[] { ", ...thank you", ", ...I needed to hear that",
                                                ", ...you're good at this",
                                                ", ...I feel a little better",
                                                ", ...don't stop. Please" },
                CharmClosers = new string[] { ", ...stay with me? Please",
                                              ", ...I don't want to be alone",
                                              ", ...can I hold your hand",
                                              ", ...just a bit longer. Please",
                                              ", ...don't let go yet" }
            },

            // ---- 4 元气 / Cheerful -------------------------------------
            new StyleDef("Cheerful", "meow!",
                new string[] { "(≧▽≦)", ":D", "＼(^o^)／", "o(≧▽≦)o", "(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧" },
                new string[] { "Hey hey! ", "Oh! ", "Yay! ", "Alright! ", "Let's go! ",
                               "Guess what! ", "So! ", "Okay okay, ", "Listen listen! ",
                               "Good timing! " },
                new string[] { ", let's do this!", ", this is gonna be great!",
                               ", I'm so ready for this", ", come on, let's go!",
                               ", we've got this!", ", isn't this exciting?!",
                               ", I could do this all day!", ", best idea ever!",
                               ", okay, I'm hyped now", ", let's make it happen!" },
                new string[] { ", ooh, good question! Let me think",
                               ", I know this one! I think",
                               ", hmm hmm... got it! Probably!",
                               ", ask me something harder next time!",
                               ", I was just wondering that too!" },
                new string[] { ", whoa, that's amazing!", ", no way, seriously?!",
                               ", that's so cool!", ", okay that's actually impressive",
                               ", I'm telling everyone about this!", ", wow wow wow!" },
                new string[] { ", I'm so happy right now!", ", this is the best day!",
                               ", yes! Yes yes yes!", ", I can't stop smiling!",
                               ", we should celebrate!", ", I'm gonna remember this!" },
                new string[] { ", hey, it's okay! I'm here",
                               ", come on, cheer up! We'll figure it out",
                               ", don't be sad! Let's do something fun",
                               ", I hate seeing you like this",
                               ", okay, plan: we fix it together. Ready?" },
                new string[] { ", ooh, I'm mad now! Let's go",
                               ", nobody treats you like that!",
                               ", okay, I'm with you. Let's handle it",
                               ", that's it, I'm fired up",
                               ", we're not letting this slide!" },
                new string[] { ", hey! You're here!", ", oh, hi hi!",
                               ", perfect timing, I was bored!",
                               ", yay, you came!", ", hey there! Long time!" },
                new string[] { ", okay, see you soon!", ", text me when you're home!",
                               ", bye bye! Come back soon!",
                               ", don't be a stranger!", ", later! Have fun!" },
                new string[] { ", I like you! There, said it!",
                               ", you're my favourite person!",
                               ", I'm really glad I know you",
                               ", you make everything better",
                               ", okay that was cheesy but I meant it!" },
                new string[] { ", that's awesome! I knew it!",
                               ", yes! Told you you could do it!",
                               ", I'm so proud of you!", ", okay, that was seriously cool",
                               ", we should celebrate! Right now!" })
            {
                ThanksClosers = new string[] { ", no problem at all!", ", happy to help!",
                                               ", anytime, seriously!",
                                               ", that's what I'm here for!",
                                               ", you'd do it for me, right?" },
                ApologyClosers = new string[] { ", hey, it's totally fine!", ", no worries!",
                                                ", don't even think about it!",
                                                ", water under the bridge!",
                                                ", we're good! Totally good!" },
                CongratClosers = new string[] { ", congratulations!!", ", yes! Yes yes yes!",
                                                ", I knew it I knew it I knew it!",
                                                ", okay we're celebrating tonight!",
                                                ", so happy for you!" },
                InviteClosers = new string[] { ", yes! Count me in!", ", ooh, absolutely!",
                                               ", I'm in! When and where?",
                                               ", say the word and I'm there!",
                                               ", finally! I've been waiting for this!" },
                SurpriseClosers = new string[] { ", wait WHAT?!", ", no way no way no way!",
                                                 ", okay I need a minute",
                                                 ", that came out of nowhere!",
                                                 ", tell me everything right now!" },
                FearClosers = new string[] { ", okay okay, I'm brave. I'm brave",
                                             ", don't leave me though!",
                                             ", we go together, right?",
                                             ", I'm fine! Totally fine! ...mostly",
                                             ", hold my hand anyway" },
                TiredClosers = new string[] { ", phew... okay, that was a lot",
                                              ", I'm running on empty here",
                                              ", nap time! For real this time",
                                              ", okay, break. Definite break",
                                              ", I'll bounce back. I always do!" },
                BoredClosers = new string[] { ", I'm SO bored", ", okay let's do something! Anything!",
                                              ", entertain me please!",
                                              ", I've run out of things to do",
                                              ", come on, let's go somewhere!" },
                DisappointedClosers = new string[] { ", aw... okay", ", that's a bummer",
                                                     ", next time for sure!",
                                                     ", hey, we'll get it next time",
                                                     ", okay, new plan! Always a new plan" },
                ComfortClosers = new string[] { ", hey, I've got you!", ", come on, I'm here!",
                                                ", we'll get through this together!",
                                                ", you're not alone, okay?",
                                                ", cheer up! I'll help!" },
                CharmClosers = new string[] { ", hey, pay attention to me!",
                                              ", come on, let's hang out!",
                                              ", you're not busy, right? Right?",
                                              ", I wanna do something together!",
                                              ", don't ignore me!" }
            },

            // ---- 5 应激 / Startled -------------------------------------
            new StyleDef("Startled", "hiss!",
                new string[] { "(=；ω；=)", "D:", "(>_<)", "(,,>﹏<,,)", "(ﾉ>ω<)ﾉ" },
                new string[] { "Hiss! ", "D-don't come closer! ", "Wah! ", "Eek! ",
                               "S-stay back! ", "N-no! ", "W-what was that?! ",
                               "Don't sneak up on me! ", "Ah! ", "...who's there?! " },
                new string[] { ", ...d-don't touch me", ", ...I'm fine! I'm fine",
                               ", ...stop sneaking up on me", ", ...okay, okay, I'm calm. Mostly",
                               ", ...my heart is going way too fast",
                               ", ...give me a second. Just a second",
                               ", ...you scared me half to death", ", ...I wasn't scared! I wasn't!",
                               ", ...don't do that again", ", ...p-please warn me next time" },
                new string[] { ", ...w-why are you asking?!", ", ...d-don't ask me that right now",
                               ", ...I can't think, I'm still shaking",
                               ", ...w-what? What did you say",
                               ", ...ask me later. Much later" },
                new string[] { ", ...w-what?! What happened?!", ", ...oh no. Oh no no no",
                               ", ...is it bad? It's bad, isn't it",
                               ", ...s-stop yelling, you're scaring me",
                               ", ...okay, okay, slow down" },
                new string[] { ", ...oh! Oh, that's... good", ", ...okay, I can breathe now",
                               ", ...that's a relief. A huge relief",
                               ", ...don't scare me like that again!",
                               ", ...I'm happy! I think! Still shaking!" },
                new string[] { ", ...I-I'm not crying. I'm not",
                               ", ...everything is too loud right now",
                               ", ...I need to sit down",
                               ", ...p-please just... be quiet for a moment",
                               ", ...I can't handle this right now" },
                new string[] { ", ...hiss! Go away!", ", ...d-don't push me!",
                               ", ...I'm warning you", ", ...back off. Now",
                               ", ...I bite. I'm serious" },
                new string[] { ", ...o-oh! It's you", ", ...d-don't do that!",
                               ", ...you startled me", ", ...h-hi. Give me a second",
                               ", ...oh. It's just you. Okay. Okay" },
                new string[] { ", ...o-okay. Bye. Go", ", ...don't leave! ...I mean, go",
                               ", ...t-text me when you're safe",
                               ", ...okay. Okay. Bye" },
                new string[] { ", ...I-I... what?", ", ...d-don't say things like that",
                               ", ...my heart can't take this",
                               ", ...say it again? Slower?",
                               ", ...I-I don't know what to do with that" },
                new string[] { ", ...w-wow. Okay. That's... wow",
                               ", ...that's incredible. I think. I can't tell",
                               ", ...I-I'm impressed! Probably!",
                               ", ...give me a moment to process",
                               ", ...that's a lot. That's a lot" })
            {
                ThanksClosers = new string[] { ", ...th-thank you", ", ...you didn't have to",
                                               ", ...okay. Thanks. I'm okay now",
                                               ", ...I appreciate it. Really",
                                               ", ...don't expect me to say it twice" },
                ApologyClosers = new string[] { ", ...i-it's fine. I'm fine",
                                                ", ...d-don't sneak up on me again",
                                                ", ...okay. Okay. We're good",
                                                ", ...you scared me, that's all",
                                                ", ...it's fine. Just... warn me" },
                CongratClosers = new string[] { ", ...oh! Congratulations!",
                                                ", ...th-that's great! I think!",
                                                ", ...sorry, I'm still jumpy",
                                                ", ...that's wonderful. Really",
                                                ", ...I'm happy for you!" },
                InviteClosers = new string[] { ", ...m-me? Really?", ", ...I-I'd like that",
                                               ", ...okay. But don't ambush me",
                                               ", ...if you're sure. Okay",
                                               ", ...y-yes. I'll come" },
                SurpriseClosers = new string[] { ", ...WAH! Don't DO that!",
                                                 ", ...my heart! My heart!",
                                                 ", ...okay I need to sit down",
                                                 ", ...w-warn me next time!",
                                                 ", ...that's it, I'm done for today" },
                FearClosers = new string[] { ", ...I-I'm scared. Really scared",
                                             ", ...d-don't leave me alone",
                                             ", ...stay. Please stay",
                                             ", ...I don't want to be by myself",
                                             ", ...hold on to me. Please" },
                TiredClosers = new string[] { ", ...I-I can't. Not right now",
                                              ", ...everything is too much",
                                              ", ...I need to lie down",
                                              ", ...p-please, just five minutes",
                                              ", ...I'm running on empty" },
                BoredClosers = new string[] { ", ...i-it's too quiet in here",
                                              ", ...d-don't just sit there",
                                              ", ...say something. Please",
                                              ", ...the silence is getting to me" },
                DisappointedClosers = new string[] { ", ...oh. Okay", ", ...I-I see",
                                                     ", ...that's fine. That's fine",
                                                     ", ...okay. Um. Okay",
                                                     ", ...I'll be fine. Give me a moment" },
                ComfortClosers = new string[] { ", ...th-thank you", ", ...I needed that",
                                                ", ...you're warm. Okay",
                                                ", ...d-don't let go yet",
                                                ", ...I'm calming down. Slowly" },
                CharmClosers = new string[] { ", ...s-stay close. Okay?",
                                              ", ...d-don't go far",
                                              ", ...hold my hand? Please",
                                              ", ...I feel safer like this",
                                              ", ...just... stay" }
            }
        };

        //
        // 和中文那套一一对应。写法上遵循英文 tsundere 的固定腔调：
        // 前半句否认，后半句松口 —— 只有否认会读成纯粹的敌意。
        // 标记词：Hmph! / 口吃（I-I、B-baka、D-don't）/ "It's not like I ... or anything"
        //
        // ★ 后缀 "nya!" 而不是 "meow!" —— 傲娇在英文圈里跟 nya 绑得更紧。
        // ★★★ 这两个不能写成 static readonly ★★★
        //
        // 写成 static readonly 的话 ✧ 初始化那一刻就把
        // 【当时的语言】用 Lang.T 烤死了 ✓
        // 之后再切语言 ✧ 它们还是启动时那一份 ✓
        //   —— 表现就是英文界面里 ✧ Light 下面写着中文的说明 ✓
        static string[] LevelNames
        {
            get
            {
                return new string[] { Lang.T("轻度"), Lang.T("普通"), Lang.T("重度") };
            }
        }

        static string[] LevelHints
        {
            get
            {
                return new string[]
                {
                    Lang.T("只在整段末尾加一次"),
                    Lang.T("每个句尾都加"),
                    Lang.T("句号和逗号后面都加")
                };
            }
        }

        int lookActive = -1;
        int toneActive = -1;
        int levelActive = 1;

        /// ★★★ 2026-10-18：按软件分配风格 ★★★
        ///
        /// rules.txt 里写：
        ///     进程风格 = QQ.exe = 傲娇
        ///     进程风格 = Code.exe = 不加
        ///
        /// —— 转换前看一眼当前窗口是什么程序 ✧ 命中就把那个风格的参数套上去 ✓
        /// —— 没命中就用原来的 Cfg ✧ 完全不影响旧行为 ✓
        public static Config StyleConfig(string proc)
        {
            try
            {
                if (App.Cur == null || App.Cur.Cfg == null) return null;
                if (string.IsNullOrEmpty(proc)) return App.Cur.Cfg;
                string styleName;
                if (!App.Cur.Cfg.ProcessStyles.TryGetValue(proc, out styleName)) return App.Cur.Cfg;
                if (string.IsNullOrEmpty(styleName)) return App.Cur.Cfg;

                // ★ 特例：「不加」「无」「none」 = 完全不装饰
                if (styleName == "不加" || styleName == "无" || styleName == "none"
                    || styleName == "关" || styleName == "off")
                {
                    Config c0 = App.Cur.Cfg.Clone();
                    c0.Suffix = "";
                    c0.Kaomoji = new List<string>();
                    c0.Tone = false;
                    c0.Openers = new List<string>();
                    c0.Closers = new List<string>();
                    c0.QuestionClosers = new List<string>();
                    c0.ExclaimClosers = new List<string>();
                    c0.HappyClosers = new List<string>();
                    c0.SadClosers = new List<string>();
                    c0.AngryClosers = new List<string>();
                    c0.GreetingClosers = new List<string>();
                    c0.FarewellClosers = new List<string>();
                    c0.LoveClosers = new List<string>();
                    c0.PraiseClosers = new List<string>();
                    c0.ThanksClosers = new List<string>();
                    c0.ApologyClosers = new List<string>();
                    c0.CharmClosers = new List<string>();
                    c0.CongratClosers = new List<string>();
                    c0.InviteClosers = new List<string>();
                    c0.SurpriseClosers = new List<string>();
                    c0.FearClosers = new List<string>();
                    c0.TiredClosers = new List<string>();
                    c0.BoredClosers = new List<string>();
                    c0.DisappointedClosers = new List<string>();
                    c0.ComfortClosers = new List<string>();
                    return c0;
                }

                // ★ 先查 14 个语气（它们带开头和各类句尾）✓
                foreach (StyleDef d in ToneStylesNow)
                    if (d.Name == styleName) return FromStyle(d, true);
                // ★ 再查 15+ 个外观（只换句尾装饰）✓
                foreach (StyleDef d in LookStylesNow)
                    if (d.Name == styleName) return FromStyle(d, false);
                return App.Cur.Cfg;
            }
            catch (Exception ex)
            {
                App.LogStatic("procstyle failed for [" + proc + "]: " + ex.Message);
                return App.Cur.Cfg;
            }
        }

        /// ★ 把一个 StyleDef 变成一份 Cfg ✓
        public static Config FromStyle(StyleDef d, bool withPools)
        {
            Config c = App.Cur.Cfg.Clone();
            c.Template = d.Template;
            c.Suffix = d.Suffix;
            c.Kaomoji = new List<string>(d.Kaomoji);
            c.Tone = withPools;
            if (!withPools) return c;

            c.Openers = new List<string>(d.Openers);
            c.Closers = new List<string>(d.Closers);
            c.QuestionClosers = new List<string>(d.QuestionClosers);
            c.ExclaimClosers = new List<string>(d.ExclaimClosers);
            c.HappyClosers = new List<string>(d.HappyClosers);
            c.SadClosers = new List<string>(d.SadClosers);
            c.AngryClosers = new List<string>(d.AngryClosers);
            c.GreetingClosers = new List<string>(d.GreetingClosers);
            c.FarewellClosers = new List<string>(d.FarewellClosers);
            c.LoveClosers = new List<string>(d.LoveClosers);
            c.PraiseClosers = new List<string>(d.PraiseClosers);
            c.ThanksClosers = new List<string>(d.ThanksClosers);
            c.ApologyClosers = new List<string>(d.ApologyClosers);
            c.CharmClosers = new List<string>(d.CharmClosers);
            c.CongratClosers = new List<string>(d.CongratClosers);
            c.InviteClosers = new List<string>(d.InviteClosers);
            c.SurpriseClosers = new List<string>(d.SurpriseClosers);
            c.FearClosers = new List<string>(d.FearClosers);
            c.TiredClosers = new List<string>(d.TiredClosers);
            c.BoredClosers = new List<string>(d.BoredClosers);
            c.DisappointedClosers = new List<string>(d.DisappointedClosers);
            c.ComfortClosers = new List<string>(d.ComfortClosers);
            return c;
        }

        void ApplyLookAt(int index)
        {
            // ★ 照 2.14.2 的结构：纯顺序执行 ✗ 不加任何额外刷新 ✓
            if (index < 0 || index >= LookStylesNow.Length) return;
            StyleDef d = LookStylesNow[index];

            // ★★★ 整个过程冻住 ✧ 最后只画一帧 ★★★
            //   不然会有两个可见的中间态：
            //     · RefreshRail 重建右边两张卡（规则 / 颜文字库 ✓）✧ 中间是空的 ✓
            //     · RefreshStatus 写两次状态行 ✧ 两次值不同 ✓
            Lang.Freeze(this);
            try
            {

            // 外观风格和语气改写是二选一：点外观就把语气关掉。
            // 关的方式只写 语气 = false ✗ 不动那二十个词库 ✓
            // 这样点一下「傲娇」就能整套回来 ✗ 而不是把词库删掉 ✓
            //
            // 语气开关合并进这一次写入 ✗ 省掉一轮「读文件→改→写→重载」✓
            bool? tflag = app.Cfg.StackStyles ? (bool?)null : false;
            Engine.StyleName = d.Name;
            if (!app.ApplyStyle(d.Template, d.Suffix, d.Kaomoji, null, tflag)) return;
            app.Log("style applied: " + d.Name
                    + (app.Cfg.StackStyles ? Lang.T(" (外观风格，语气保持不变)") : Lang.T(" (外观风格 -> 语气关闭)")));

            lookActive = index;
            if (!app.Cfg.StackStyles) toneActive = ToneStylesNow.Length;
            SyncChips();
            // ★ 2026-10-22：SyncChips 已经点亮右栏胶囊 ✗
            //   再整块 RefreshRail 就是白重建 16+ 个控件 ✗ 卡的就是这一下 ✓
            UpdatePreview();
            // ★ 状态行要单独刷 ✧ 它标了 NoTranslate ✧ Lang.Apply 不会碰它 ✓
            //   而它只在 RefreshStatus 里写 ✧ 那个函数是 700ms 定时器驱动的 ✓
            RefreshStatus();
            }
            finally
            {
                Lang.Thaw(this);
                try
                {
                }
                catch { }
            }
        }

        void ApplyToneAt(int index)
        {
            // ★★★ 同理：整个过程冻住 ★★★
            Lang.Freeze(this);
            try
            {
            // ★ 照 2.14.2 的结构：纯顺序执行 ✓
            // 最后那个「不加语气」不是语气风格 ✗ 它只写 语气=false 和 程度=轻度 ✗ 
            // 故意不碰 模板/后缀/颜文字 ✗ 所以保留已经设好的外观 ✓
            if (index == ToneStylesNow.Length)
            {
                Dictionary<string, string[]> dry = new Dictionary<string, string[]>();
                dry["\u8BED\u6C14"] = new string[] { "false" };
                dry["\u7A0B\u5EA6"] = new string[] { "\u8F7B\u5EA6" };
                if (!app.PatchRules(dry)) return;
                app.Log("style applied: \u53EA\u52A0\u672B\u5C3E (tone=false, level=\u8F7B\u5EA6)");
                toneActive = ToneStylesNow.Length;
                levelActive = 0;
                SyncChips();
                RefreshRail();
                UpdatePreview();
                return;
            }

            if (index < 0 || index >= ToneStylesNow.Length) return;
            StyleDef d = ToneStylesNow[index];

            // ★ 2026-10-18：叠加开着的时候，语气只写词库 ✗ 不动装饰 ✓
            if (app.Cfg.StackStyles)
            {
                Dictionary<string, string[]> v = new Dictionary<string, string[]>();
                v[Lang.T("语气")] = new string[] { "true" };
                Dictionary<string, string[]> tp = d.ToPools();
                foreach (string[] pair in App.PatchKeys)
                {
                    string cn = pair[0], en = pair[1];
                    if (en != "opener" && en != "closer" && !en.EndsWith("closer")) continue;
                    string[] lines = null;
                    tp.TryGetValue(en, out lines);
                    v[cn] = lines ?? new string[0];
                }
                if (!app.PatchRules(v)) return;
                app.Log("style applied: " + d.Name + " (tone=true, 只写词库)");
            }
            else
            {
                Engine.StyleName = d.Name;
            if (!app.ApplyStyle(d.Template, d.Suffix, d.Kaomoji, d.ToPools(), true)) return;
                app.Log("style applied: " + d.Name + " (tone=true)");
                lookActive = -1;
            }

            toneActive = index;
            SyncChips();
            // ★ 2026-10-22：SyncChips 已经点亮右栏 ✗ 不再重建 ✓
            UpdatePreview();
            RefreshStatus();   // ★ 状态行单独刷 ✓
            }
            finally
            {
                Lang.Thaw(this);
            }
        }

        /// Applies a style by its display name, without going through the UI. Used by
        /// "--style 傲娇" on the command line so a style can be set from a script or a
        /// shortcut - and so setting one does not depend on clicking the right pixel.
        /// Returns false if the name matches nothing.
        public static bool ApplyStyleByName(App app, string name)
        {
            if (app == null || string.IsNullOrEmpty(name)) return false;
            string want = name.Trim();

            // "只加末尾" is a mode, not a look: it switches the wrapper off and leaves
            // 模板 / 后缀 / 颜文字 exactly as they are.
            if (want == DryModeName)
            {
                Dictionary<string, string[]> dry = new Dictionary<string, string[]>();
                dry["\u8BED\u6C14"] = new string[] { "false" };
                dry["\u7A0B\u5EA6"] = new string[] { "\u8F7B\u5EA6" };
                bool okDry = app.PatchRules(dry);
                app.Log("style applied: \u53EA\u52A0\u672B\u5C3E (--style, tone=false, level=\u8F7B\u5EA6)");
                return okDry;
            }

            foreach (StyleDef d in ToneStyles)
                if (d.Name == want)
                {
                    if (!app.ApplyStyle(d.Template, d.Suffix, d.Kaomoji, d.ToPools())) return false;
                    // Picking a real tone style turns the wrapper back on. The UI path does
                    // this in ApplyToneAt; forgetting it here left "--style 傲娇" writing the
                    // pools while 语气 stayed false, i.e. the style silently did nothing.
                    Dictionary<string, string[]> on = new Dictionary<string, string[]>();
                    on["\u8BED\u6C14"] = new string[] { "true" };
                    return app.PatchRules(on);
                }
            foreach (StyleDef d in LookStyles)
                if (d.Name == want)
                {
                    // ★ 2026-10-18：叠加开着就不写 语气 ✗ 和界面一致 ✓
                    if (app.Cfg.StackStyles)
                        return app.ApplyStyle(d.Template, d.Suffix, d.Kaomoji, null, null);
                    Dictionary<string, string[]> off = new Dictionary<string, string[]>();
                    off["\u8BED\u6C14"] = new string[] { "false" };
                    return app.PatchRules(off);
                }
            return false;
        }

        public const string DryModeName = "\u4E0D\u52A0\u8BED\u6C14";   // 不加语气

        public static string StyleNames()
        {
            StringBuilder sb = new StringBuilder();
            foreach (StyleDef d in ToneStylesNow) sb.Append(d.Name).Append(" ");
            sb.Append("| ");
            foreach (StyleDef d in LookStyles) sb.Append(d.Name).Append(" ");
            sb.Append("| ").Append(DryModeName);
            return sb.ToString().Trim();
        }

        void ApplyLevelAt(int index)
        {
            if (index < 0 || index >= LevelNames.Length) return;
            Dictionary<string, string[]> v = new Dictionary<string, string[]>();
            v["\u7A0B\u5EA6"] = new string[] { LevelNames[index] };
            if (!app.PatchRules(v)) return;

            levelActive = index;
            SyncChips();
            UpdatePreview();
        }

        /// ★ 拼芯片上那行示例 ✧ 规则要和引擎一致 ✓
        ///
        /// 引擎在转换时：后缀首字符是【ASCII 字母或数字】就前面补一个空格 ✓
        ///   hello + meow~ → hello meow~   （不补的话会连成 hellomeow~ ✓）
        ///   你好 + 喵~    → 你好喵~      （汉字后面跟“喵”很自然 ✧不用加 ✓）
        /// —— 芯片上的示例是我们自己拼的 ✧ 不走引擎 ✧ 所以要自己补 ✓
        static string MakeSample(string head, string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return head;
            char c = suffix[0];
            bool ascii = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
            return head + (ascii ? " " : "") + suffix;
        }

        /// ★★★ 按当前语言重算所有芯片的示例文字 ★★★
        ///
        /// 为什么要单独一个函数：
        ///   StyleChip.Sample 是【自绘】的 ✧ 不走 .Text ✓
        ///   Lang.Apply 只会翻 .Text（芯片名字 ✓）✧ 
        ///   所以切语言之后 ✧ 没人去重算它 ✓
        ///   —— 表现：英文界面里 Light 下面还写着 "你好 只在整段末尾加一次" ✓
        void RefreshChipSamples()
        {
            string head = Lang.English ? "Hello" : "你好";

            // ★ 右侧栏那张 Styles 卡 ✧ 和风格页不是同一个控件 ✓
            //
            // 它也是【启动时就建好】的 ✧ 那时 Lang.English 还是 false ✓
            // 上一次我只刷了【风格页】的 pageChips ✧ 漏了这一组 ✓
            // （用户 2026-10-07："英文版的这里怎么还有中文" ✓）
            if (chips != null)
            {
                StyleDef[] rk = LookStylesNow;
                for (int i = 0; i < chips.Length && i < rk.Length; i++)
                {
                    if (chips[i] == null) continue;
                    chips[i].Sample = MakeSample(head, rk[i].Suffix);
                    chips[i].Invalidate();
                }
            }

            if (levelChips != null)
            {
                StyleDef[] lv = LevelDefs();
                for (int i = 0; i < levelChips.Length && i < lv.Length; i++)
                {
                    if (levelChips[i] == null) continue;
                    levelChips[i].Sample = MakeSample(head, lv[i].Suffix);
                    levelChips[i].Invalidate();
                }
            }

            if (pageChips != null)
            {
                StyleDef[] lk = LookStylesNow;
                for (int i = 0; i < pageChips.Length && i < lk.Length; i++)
                {
                    if (pageChips[i] == null) continue;
                    pageChips[i].Sample = MakeSample(head, lk[i].Suffix);
                    pageChips[i].Invalidate();
                }
            }

            if (pageToneChips != null)
            {
                StyleDef[] tn = ToneStylesNow;
                // ★ 这一组比语气风格多一个 ✧ 末尾是"不加语气" ✓
                //   它建的时候就是用 ToneStylesNow[0] 的后缀当占位 ✓
                //   所以这里也照样处理 ✧ 不然它会一直停在旧语言 ✓
                for (int i = 0; i < pageToneChips.Length; i++)
                {
                    if (pageToneChips[i] == null) continue;
                    int k = i < tn.Length ? i : 0;
                    pageToneChips[i].Sample = MakeSample(head, tn[k].Suffix);
                    pageToneChips[i].Invalidate();
                }
            }
        }

        void SyncChips()
        {
            RefreshChipSamples();
            Mark(chips, lookActive);
            Mark(pageChips, lookActive);
            Mark(pageToneChips, toneActive);
            Mark(levelChips, levelActive);
            if (styleCurrent != null)
            {
                string look = lookActive >= 0 ? LookStylesNow[lookActive].Name : Lang.T("未套用");
                string tone;
                if (!app.Cfg.Tone) tone = Lang.T("不加（只加句尾）");
                else if (toneActive >= 0 && toneActive < ToneStylesNow.Length) tone = ToneStylesNow[toneActive].Name;
                else if (app.Cfg.Openers.Count > 0 || app.Cfg.Closers.Count > 0
                      || app.Cfg.QuestionClosers.Count > 0 || app.Cfg.ExclaimClosers.Count > 0)
                    tone = Lang.T("已开启（自定义）");
                else tone = Lang.T("关闭");
                // ★ 缩短了 ✧ 原来拼出来 38px 宽 ✧ 而这个 Label 只有 24px 高 ✗
                //   日志里一直在报「提示文字真的会被截断（需要 38px，只有 24px）」✓
                //   —— 把「当前生效：外观风格」缩成「外观」 ✧ 去掉尾巴那句 ✓
                styleCurrent.Text = Lang.T("生效：外观「") + look + Lang.T("」·语气「") + tone + "」"
                                  + Lang.T("·程度「") + LevelNames[levelActive] + "」";
            }
        }

        /// Reads the current rule file back into the page so the highlighted chips match
        /// what is actually in effect, instead of showing "nothing selected" on startup.
        void SyncChipsFromConfig()
        {
            if (app.Cfg == null) return;
            levelActive = app.Cfg.CommaDecorate ? 2 : (app.Cfg.EachSentence ? 1 : 0);

            bool toneOn = app.Cfg.Openers.Count > 0 || app.Cfg.Closers.Count > 0
                       || app.Cfg.QuestionClosers.Count > 0 || app.Cfg.ExclaimClosers.Count > 0;

            toneActive = -1;
            lookActive = -1;
            // 语气 = false means the dry mode is on: highlight its chip (it sits after the
            // real tone styles in pageToneChips).
            if (!app.Cfg.Tone) toneActive = ToneStylesNow.Length;
            if (!toneOn)
            {
                for (int i = 0; i < LookStyles.Length; i++)
                    if (LookStylesNow[i].Suffix == app.Cfg.Suffix &&
                        LookStylesNow[i].Kaomoji.Length == app.Cfg.Kaomoji.Count)
                        lookActive = i;
            }
            // which 语气 style is on cannot be told from the file alone once the variants
            // are random - the summary line says "已开启（自定义）" in that case
            SyncChips();
        }

        static void Mark(StyleChip[] set, int active)
        {
            if (set == null) return;
            for (int i = 0; i < set.Length; i++)
            {
                // \u2605 2026-10-18\uff1aPageChips \u88c5\u4e0d\u4e0b\u65f6\u4f1a break \u2727 \u540e\u9762\u7684\u69fd\u4f4d\u662f null \u2717
                //   \u2014\u2014 \u4e0d\u52a0\u8fd9\u4e2a\u5224\u65ad\u5c31\u662f\u4e00\u4e2a NullReferenceException \u2727 \u6574\u4e2a\u7a97\u53e3\u90fd\u51fa\u4e0d\u6765 \u2713
                if (set[i] == null) continue;
                set[i].Active = (i == active);
                set[i].Invalidate();
            }
        }
        Bitmap wallpaper;

        public MainForm(App a)
        {
            app = a;

            Text = Lang.T("喵化助手 ") + Program.Version;
            ClientSize = new Size(1180, 820);
            // MinimumSize is the OUTER form size, so the real client minimum is ~39px less
            // than these numbers. Fixed rows now need 126 + 132 + 144 = 402, so 760 outer
            // still leaves roughly 320 for the middle column.
            MinimumSize = new Size(980, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.Body;
            DoubleBuffered = true;

            try { Icon = Art.AppIcon(); } catch { }

            // optional wallpaper; everything above it stays transparent so it shows
            // through the gaps between the cards
            try
            {
                string bg = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "bg.png");
                if (File.Exists(bg))
                {
                    using (Image src = Image.FromFile(bg))
                        wallpaper = new Bitmap(src);
                }
            }
            catch { wallpaper = null; }

            BuildUi();

            // ★ 界面全部建好之后 ✗ 按当前语言换一遍文字 ✓
            //   放在这里是因为之后不再新建静态文字控件 ✗ 遍历一次就够 ✓
            Lang.English = app.Cfg.Language == "en";
            Lang.Apply(this);
            // ★★★ 芯片上的示例文字也要按语言重算 ★★★
            //
            // 为什么要在这里补：
            //   页面是【启动时先建好】的 ✧ 那时 Lang.English 还是默认的 false ✓
            //   紧接着 Lang.Apply 把 .Text（芯片名字 ✓）翻成英文 ✓
            //   但 Sample 是自绘的 ✧ 没人重算 ✧ 它就停在中文了 ✓
            //   —— 表现：英文界面里 Light 下面还写着 "你好 只在整段末尾加一次" ✓
            RefreshChipSamples();
            // ★ 2026-10-18：日签标了 NoTranslate ✗ 这里重算一次 ✓
            if (statusSign != null) statusSign.Text = DailySign();
            // ★ 2026-10-22：自定义胶囊的 × 只在重建时挂上 ✗
            //   启动时走的是 PageChips ✗ 没有 × ✗ 这里补一次重建 ✓
            try { ReloadStyleChips(); } catch { }
            try { ReloadToneChips(); } catch { }

            // ★ 2026-10-18：等窗口真正显示之后再量一次侧栏宽度 ✗
            //   布局完成前 p.ClientSize 还是旧值 ✗ 算出来的宽度不对 ✓
            Shown += delegate
            {
                if (langBtn == null || langBtn.Parent == null) return;
                int pw = langBtn.Parent.ClientSize.Width;
                if (pw < 60) return;
                // ★ 2026-10-18：侧栏宽度在 layOuter 里写死了 NW = 168 ✓
                //   所以不用量 ✗ 直接 10 + 148 + 10 ✓
                // ★ 2026-10-18：168 里左右各 18 ✗ 真正居中 ✓
                langBtn.Left = 18;
                langBtn.Width = 132;
                try
                {
                    using (GraphicsPath gp = Rounded.Path(
                        new Rectangle(0, 0, langBtn.Width, langBtn.Height), 12))
                    {
                        Region oldR = langBtn.Region;
                        langBtn.Region = new Region(gp);
                        if (oldR != null) oldR.Dispose();
                    }
                }
                catch { }
            };

            FormClosing += OnFormClosing;
            LoadFromApp();
        }

        Image wallpaperScaled;
        Size wallpaperScaledFor;
        long lastSlowPaintLog;
        long lastSlowLayoutLog;
        long resizeStarted;
        // ★ 拖动中的约束 ✧ 用户 2026-10-17：放大缩小窗口会卡 ✓
        long lastWallScaleAt;     // 上次重缩放的时间 ✓
        System.Windows.Forms.Timer resizeSettle;   // 拖完后再重画一次✓

        /// Instrumented because "it feels slow" is not something to fix by guessing. The two
        /// costs during a drag are layout (241 controls in nested TableLayoutPanels) and
        /// painting, and the log now says which one is actually responsible.
        protected override void OnResize(EventArgs e)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            base.OnResize(e);
            sw.Stop();
            // ★ 节流：只在真慢的时候记 ✧ 一个拖动不能刷满日志 ✓
            if (sw.ElapsedMilliseconds > 60 && Environment.TickCount - lastSlowLayoutLog > 3000)
            {
                lastSlowLayoutLog = Environment.TickCount;
                app.Log("slow resize layout: " + sw.ElapsedMilliseconds + "ms at "
                        + ClientSize.Width + "x" + ClientSize.Height);
            }
            resizeStarted = Environment.TickCount;

            // ★ 拖完之后再重画一次 ✧ 否则停在模糊版 ✓
            try
            {
                if (resizeSettle == null)
                {
                    resizeSettle = new System.Windows.Forms.Timer();
                    resizeSettle.Interval = 300;
                    resizeSettle.Tick += delegate
                    {
                        try { resizeSettle.Stop(); } catch { }
                        try { Invalidate(); } catch { }
                    };
                }
                resizeSettle.Stop();
                resizeSettle.Start();
            }
            catch { }
        }

        protected override void OnPaintBackground(PaintEventArgs pe)
        {
            if (wallpaper == null)
            {
                base.OnPaintBackground(pe);
                return;
            }
            Rectangle r = ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            // The scaled copy is CACHED.
            //
            // Every control with BackColor = Transparent makes WinForms repaint its parent's
            // background, and there are nine of them, so a single frame used to stretch the
            // artwork nine times. Dragging the window edge then ran at well under one frame
            // per second. Stretching a 900x730 image to ~1180x820 is only a few milliseconds,
            // but nine times per frame during a continuous resize is not.
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            // ★★★ 拖动中不重缩放 ★★★
            //
            // 用户 2026-10-17："放大缩小窗口会卡顿" ✗
            // 量出来的数据：50ms@1180x820 ✧ 146ms@2560x1417 ✧ 跟面积成正比 ✓
            // 原因：900x730 的壁纸每一帧都用双线性重缩放一遍 ✧ GDI+ 这个尺寸就是 ~50ms ✗
            // 修法：拖动中用【旧图直接拉伸】（最快的插值 ✧ 稍糊但不卡 ✓）
            //       拖完之后再重缩放一次做清晰版 ✓✓✓
            long nowMs = Environment.TickCount;
            bool dragging = (nowMs - resizeStarted) < 350;
            bool needScale = wallpaperScaled == null
                          || !dragging                          // 停下来了 ✧ 做清晰版 ✓
                          || (nowMs - lastWallScaleAt) > 500;   // 拖动中每 500ms 才重缩一次 ✓
            if (needScale && wallpaperScaledFor != r.Size)
            {
                lastWallScaleAt = nowMs;
                Image old = wallpaperScaled;
                try
                {
                    Bitmap b = new Bitmap(r.Width, r.Height);
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                        g.DrawImage(wallpaper, 0, 0, r.Width, r.Height);
                    }
                    wallpaperScaled = b;
                    wallpaperScaledFor = r.Size;
                }
                catch { wallpaperScaled = wallpaper; wallpaperScaledFor = r.Size; }
                if (old != null && !object.ReferenceEquals(old, wallpaper)) { try { old.Dispose(); } catch { } }
            }
            if (wallpaperScaled != null && wallpaperScaledFor == r.Size)
            {
                pe.Graphics.DrawImageUnscaled(wallpaperScaled, 0, 0);
            }
            else if (wallpaperScaled != null)
            {
                // ★ 尺寸对不上（拖动中 ✓）✧ 最快的插值直接拉伸 ✓
                System.Drawing.Drawing2D.InterpolationMode __im = pe.Graphics.InterpolationMode;
                pe.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                pe.Graphics.DrawImage(wallpaperScaled, 0, 0, r.Width, r.Height);
                pe.Graphics.InterpolationMode = __im;
            }

            // Logged rather than guessed at. Only when a frame is genuinely slow, and at most
            // once every few seconds, so a resize cannot flood the log.
            sw.Stop();
            if (sw.ElapsedMilliseconds > 50 && Environment.TickCount - lastSlowPaintLog > 4000)
            {
                lastSlowPaintLog = Environment.TickCount;
                app.Log("slow background paint: " + sw.ElapsedMilliseconds + "ms for "
                        + r.Width + "x" + r.Height);
            }
        }

        // ------------------------------------------------ layout

        void BuildUi()
        {
            // ★★★ 用户 2026-10-17：拖窗口卡顿 ★★★
            //
            // 探针结果（每帧）：
            //   LZ MainForm  108ms ✗✗✗   ← 全是这里
            //   LZ CardPanel  15ms ✗        ← 卡片只占小头
            // 原因：outer（3 列）+ main（4 行）两层 TableLayoutPanel ✧
            //       241 个控件每次 resize 要走好几遍优先尺寸计算 ✗
            //
            // 修法：换成普通 Panel + 手写 SetBounds ✧
            //       3 列 / 4 行都是固定或剩余 ✧ 手算一行代码的事 ✓✓✓
            Panel outer = new Panel();
            outer.Dock = DockStyle.Fill;
            outer.BackColor = Color.Transparent;
            Controls.Add(outer);

            Control side = BuildSidebar();
            outer.Controls.Add(side);

            Panel main = new Panel();
            main.BackColor = Color.Transparent;
            outer.Controls.Add(main);

            Control header = BuildHeader();
            Control statusCard = BuildStatusCard();
            Control content = BuildContent();
            Control bottomBar = BuildBottom();
            Control rightRail = BuildRightRail();

            // ★ Dock 要清掉 ✧ 否则它会抢掉我们手写的尺寸 ✓
            foreach (Control c in new Control[] { header, statusCard, content, bottomBar, side, rightRail })
                if (c != null) c.Dock = DockStyle.None;

            main.Controls.Add(header);
            main.Controls.Add(statusCard);
            main.Controls.Add(content);
            main.Controls.Add(bottomBar);
            outer.Controls.Add(rightRail);

            // ★ 手写布局 ✧ 168 导航 栏 ✧ 右侧 302 ✧ 中间全占 ✓
            EventHandler layOuter = delegate
            {
                int w = outer.ClientSize.Width, h = outer.ClientSize.Height;
                if (w <= 0 || h <= 0 || side == null || rightRail == null) return;
                const int NW = 168, RW = 302;
                side.SetBounds(0, 0, NW, h);
                rightRail.SetBounds(Math.Max(0, w - RW), 0, RW, h);
                main.SetBounds(NW, 0, Math.Max(0, w - NW - RW), h);
            };
            outer.Resize += layOuter;

            // ★ 第二层：126 头 / 状态卡 / 剩余 / 144 底栏 ✓
            EventHandler layMain = delegate
            {
                int w = main.ClientSize.Width, h = main.ClientSize.Height;
                if (w <= 0 || h <= 0) return;
                const int H1 = 126, H4 = 144;
                int H2 = app.Cfg.UiCards ? 186 : 132;
                if (header != null) header.SetBounds(0, 0, w, H1);
                if (statusCard != null) statusCard.SetBounds(0, H1, w, H2);
                if (bottomBar != null) bottomBar.SetBounds(0, Math.Max(0, h - H4), w, H4);
                if (content != null) content.SetBounds(0, H1 + H2, w,
                    Math.Max(0, h - H1 - H2 - H4));
            };
            main.Resize += layMain;

            // ★★★ 立刻排一次 ✗ 否则初始界面是空的 ★★★
            //
            // 踩的坑：布局只挂在 outer.Resize / main.Resize 上 ✗
            // 但启动时 outer 的尺寸早定好了 ✗ Resize 不会再触发 ✓
            // → 第一帧看到的就是"只有左边导航栏 ✗ 右边全空白"✓✓✓
            try { layOuter(null, EventArgs.Empty); } catch { }
            try { layMain(null, EventArgs.Empty); } catch { }

            // ⑬ 交给卡片类；它在 OnPaint 里只读这个静态值
            // ★ 把软件自带的每一个风格都登记给引擎。
            //
            // 为什么：剥旧装饰/旧关闭语那两步原来只看【当前配置】。用户一切换风格，
            // 上一轮用旧风格写下的东西就认不出来了 —— 会被当成他自己写的话，
            // 于是新一轮把整句再包一遍，越用越长。
            // 自带的风格是已知的，启动就全登记，之后任何时候都认得出自己写过什么。
            SeedEngineStyles();

            CardPanel.ShowPaw = app.Cfg.UiPaw;
            Theme.ApplyLevels(app.Cfg.UiColor);   // ⑫ 颜色分层
            _animOn = app.Cfg.UiAnim;

            SelectPage(0);
            SyncChipsFromConfig();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.StyleTitleBar(Handle);

            // a slow poll so the status card, the pills and the character's face stay
            // current without every state change having to remember to refresh them
            System.Windows.Forms.Timer ui = new System.Windows.Forms.Timer();
            ui.Interval = 700;
            ui.Tick += delegate { RefreshStatus(); };
            ui.Start();
        }

        Control BuildHeader()
        {
            HeaderBand p = new HeaderBand();
            p.Dock = DockStyle.Fill;

            try
            {
                Bitmap logo = Art.Logo(112);
                if (logo != null)
                {
                    PictureBox pic = new PictureBox();
                    pic.Image = logo;
                    pic.SizeMode = PictureBoxSizeMode.Zoom;
                    pic.Size = new Size(112, 112);
                    pic.Location = new Point(2, 12);
                    pic.BackColor = Color.Transparent;
                    p.Controls.Add(pic);
                }
            }
            catch { }

            Label title = new Label();
            title.Text = Lang.T("喵化助手");
            title.Font = Theme.Hero;
            title.ForeColor = Color.FromArgb(184, 44, 98);
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            title.Location = new Point(150, 40);
            p.Controls.Add(title);

            Label sub = new Label();
            sub.Text = Lang.T("打完字按一下，整段变成喵喵的");
            sub.Font = Theme.Body;
            sub.ForeColor = Color.FromArgb(112, 88, 126);
            // ★★★ 宽度改成【运行时算】★★★
            //   原来是 AutoSize = true ✧ 只会一直往右长 ✗ 不会换行 ✓
            //   但写死宽度也不行：右边那个气泡是
            //       气泡.Left = 窗口宽 - 346
            //   窗口一窄它就往左移 ✧ 写死的宽度就撞上了 ✓
            //   —— 改成在 place 委托里按气泡的实际位置算 ✓
            sub.AutoSize = false;
            sub.Size = new Size(285, 46);
            // ★ 垂直居中 ✧ 中文一行、英文两行 ✧ 让它们看起来一样高 ✓
            sub.TextAlign = ContentAlignment.MiddleLeft;
            sub.BackColor = Color.Transparent;
            sub.Location = new Point(153, 70);
            p.Controls.Add(sub);

            // The character reacts to what the tool is doing. Text faces rather than
            // extra artwork: we only have the one illustration, and drawing new
            // expressions is not something to fake.
            //
            // Positioned from a Resize handler, NOT with Anchor=Right. The anchor was
            // resolved against an intermediate parent width during layout and left the
            // labels at x=1210 in an 854px band - i.e. completely off-screen.
            if (app.Cfg.UiTopBar)
            {
            // ---- 试验：顶部状态气泡（UI_TOPBAR）-------------------------------------
            // A rounded bubble behind the whole state block, plus an emoji icon.
            //
            // The emoji sits in its OWN label. The app font is Microsoft YaHei UI, which has
            // no emoji coverage at all: rendering 🐾 🟢 😊 😴 with it produced EMPTY BOXES.
            // I checked rather than assumed - EmojiProbe.cs draws the candidates to a PNG.
            // Segoe UI Emoji has them, in colour, so only the icon label uses that font and
            // every piece of real text stays on YaHei.
            headerBubble = new CardPanel();
            headerBubble.Radius = 20;
            headerBubble.Size = new Size(330, 88);
            headerBubble.BackColor = Color.Transparent;
            p.Controls.Add(headerBubble);
            headerBubble.SendToBack();

            headerIcon = new Label();
            headerIcon.Font = new Font("Segoe UI Emoji", 24f);
            headerIcon.ForeColor = Color.FromArgb(150, 52, 108);
            headerIcon.BackColor = Color.Transparent;
            headerIcon.AutoSize = false;
            headerIcon.TextAlign = ContentAlignment.MiddleCenter;
            headerIcon.Size = new Size(64, 64);
            headerIcon.Location = new Point(6, 12);
            headerIcon.ImageAlign = ContentAlignment.MiddleCenter;
            // the colour comes from Emoji.Art: a control asked to DRAW the glyph gets a flat
            // silhouette, so the glyph is rendered to a bitmap first (see the Emoji class)
            headerIcon.Text = "\U0001F43E";
            headerBubble.Controls.Add(headerIcon);
            }

            headerFace = new Label();
            headerFace.Font = Theme.Face;
            headerFace.ForeColor = Color.FromArgb(158, 52, 108);
            headerFace.BackColor = Color.Transparent;
            headerFace.AutoSize = false;
            headerFace.TextAlign = ContentAlignment.MiddleRight;
            headerFace.Size = new Size(250, 38);
            p.Controls.Add(headerFace);

            headerMood = new Label();
            headerMood.Font = Theme.Small;
            headerMood.ForeColor = Color.FromArgb(126, 86, 118);
            headerMood.BackColor = Color.Transparent;
            headerMood.AutoSize = false;
            headerMood.TextAlign = ContentAlignment.MiddleRight;
            headerMood.Size = new Size(250, 24);
            p.Controls.Add(headerMood);

            EventHandler place = delegate
            {
                int right = p.Width - 46;
                headerFace.Left = right - headerFace.Width;
                headerFace.Top = 30;
                headerMood.Left = right - headerMood.Width;
                headerMood.Top = 70;
            if (app.Cfg.UiTopBar)
            {
                if (headerBubble != null)
                {
                    headerBubble.Left = right - headerBubble.Width + 30;
                    headerBubble.Top = 18;
                    headerBubble.SendToBack();
                    // inside the bubble: icon on the left, the two text lines on the right
                    headerFace.Left = headerBubble.Width - headerFace.Width - 24;
                    headerFace.Top = 16;
                    headerMood.Left = headerBubble.Width - headerMood.Width - 24;
                    headerMood.Top = 54;
                    if (headerFace.Parent != headerBubble)
                    {
                        p.Controls.Remove(headerFace);
                        p.Controls.Remove(headerMood);
                        headerBubble.Controls.Add(headerFace);
                        headerBubble.Controls.Add(headerMood);
                    }
                    headerFace.Width = 230;
                    // ★ 副标题的宽度按气泡的实际位置算 ✧ 避开它 ✓
                    //   窗口窄的时候气泡会往左移 ✧ 写死宽度就会被压住 ✓
                    if (sub != null)
                    {
                        int avail = headerBubble.Left - sub.Left - 16;
                        if (avail < 150) avail = 150;
                        if (avail > 285) avail = 285;
                        if (sub.Width != avail) sub.Width = avail;
                    }
                    headerMood.Width = 230;
                }
            }
            };
            p.Resize += place;
            place(null, EventArgs.Empty);

            return p;
        }

        Control BuildStatusCard()
        {
            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill;
            wrap.BackColor = Theme.Bg;
            wrap.Padding = new Padding(16, 8, 16, 4);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(18, 12, 18, 12);
            wrap.Controls.Add(card);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 1;
            t.RowCount = 5;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            card.Controls.Add(t);

            Panel headRow = new Panel();
            headRow.Dock = DockStyle.Fill;
            headRow.BackColor = Theme.Card;

            statusDot = new StatusDot();
            statusDot.Location = new Point(0, 6);
            statusDot.Size = new Size(14, 14);
            headRow.Controls.Add(statusDot);

            // not Dock=Fill: an opaque label would paint over the dot
            statusHead = new Label();
            statusHead.Location = new Point(22, 0);
            statusHead.Size = new Size(200, 26);
            statusHead.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            statusHead.Font = Theme.Head;
            statusHead.ForeColor = Theme.Text;
            statusHead.BackColor = Theme.Card;
            statusHead.TextAlign = ContentAlignment.MiddleLeft;
            // ★★★ 这两行不能被自动翻译 ★★★
            //   它们的文字是【每秒重新生成】的，而且生成时已经按当前语言处理过（内部调 Lang.T）✓
            //   如果还让翻译机制插手：它会拿"第一次见到的文字"当原文，
            //   而第一次见到的可能已经是英文 → 之后切回中文也变不回来，
            //   表现就是"闪一下新值，然后固定变回旧的"✓
            statusHead.Tag = Lang.NoTranslate;
            headRow.Controls.Add(statusHead);

            t.Controls.Add(headRow, 0, 0);

            statusPills = new PillBar();
            statusPills.Dock = DockStyle.Fill;
            t.Controls.Add(statusPills, 0, 1);
            if (app.Cfg.UiCards)
            {
            // ---- 试验：四个数字卡（UI_CARDS）---------------------------------------
            // The pill row and the dot/head are still CREATED (their text is what the status
            // refresh writes into) but they are not added to the layout, so nothing paints
            // and no update path has to change.
            t.Controls.Remove(headRow);
            t.Controls.Remove(statusPills);
            // Row 0 becomes the card strip and row 1 collapses. The grid must NOT span both:
            // a span over two zero-height rows is zero high, and the cards rendered as
            // nothing at all - visible only by screenshotting the built app.
            t.RowStyles[0] = new RowStyle(SizeType.Absolute, 84);
            t.RowStyles[1] = new RowStyle(SizeType.Absolute, 0);

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Top;
            grid.Height = 78;
            grid.BackColor = Theme.Card;
            grid.ColumnCount = 4;
            grid.RowCount = 1;
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            statCards = new StatCard[4];
            for (int i = 0; i < 4; i++)
            {
                statCards[i] = new StatCard();
                statCards[i].Dock = DockStyle.Fill;
                statCards[i].Margin = new Padding(i == 3 ? 0 : 0, 0, i == 3 ? 0 : 8, 0);
                grid.Controls.Add(statCards[i], i, 0);
            }
            t.Controls.Add(grid, 0, 0);
            }

            statusDetail = new Label();
            statusDetail.Dock = DockStyle.Fill;
            statusDetail.Font = Theme.Small;
            statusDetail.ForeColor = Theme.Muted;
            statusDetail.BackColor = Theme.Card;
            statusDetail.TextAlign = ContentAlignment.MiddleLeft;
            statusDetail.Tag = Lang.NoTranslate;   // ★ 同上 ✓
            t.Controls.Add(statusDetail, 0, 2);

            statusWarn = new Label();
            statusWarn.Dock = DockStyle.Fill;
            statusWarn.Font = Theme.Small;
            statusWarn.ForeColor = Theme.Warn;
            statusWarn.BackColor = Theme.Card;
            statusWarn.TextAlign = ContentAlignment.MiddleLeft;
            t.Controls.Add(statusWarn, 0, 3);

            // \u2605 2026-10-18\uff1a\u4eca\u65e5\u55b5\u7b7e \u2727 \u653e\u5728\u72b6\u6001\u5361\u6700\u4e0b\u9762\u7684\u7a7a\u4f4d \u2713
            statusSign = new Label();
            statusSign.Dock = DockStyle.Fill;
            statusSign.Font = Theme.Small;
            statusSign.ForeColor = Theme.Accent;
            statusSign.BackColor = Theme.Card;
            statusSign.TextAlign = ContentAlignment.MiddleLeft;
            statusSign.Tag = Lang.NoTranslate;
            statusSign.Text = DailySign();
            t.Controls.Add(statusSign, 0, 4);

            return wrap;
        }

        Control BuildSidebar()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            // opaque on purpose: a custom-painted gradient behind transparent children
            // made controls repaint over themselves earlier in this project
            p.BackColor = Color.FromArgb(255, 246, 251);
            p.Padding = new Padding(10, 12, 10, 10);

            Label brand = new Label();
            brand.Text = Lang.T("喵化助手");
            brand.Font = Theme.Head;
            brand.ForeColor = Color.FromArgb(190, 60, 112);
            brand.BackColor = Color.FromArgb(255, 246, 251);
            brand.AutoSize = false;
            brand.TextAlign = ContentAlignment.MiddleLeft;
            brand.Location = new Point(14, 12);
            brand.Size = new Size(150, 30);
            p.Controls.Add(brand);

            string[] titles = { Lang.T("试一下"), Lang.T("喵化规则"), Lang.T("喵化风格"), Lang.T("颜文字库"), Lang.T("设置"), Lang.T("使用统计"), Lang.T("更新公告"), Lang.T("关于") };
            // ② 猫系线性图标（UiNavIcon 为假时用原来那套）
            string[] glyphsCat = { "\u25B7", "\u2261", "\u2661", "\u263A", "\u2699", "\u25A6", "\u2726", "?" };
            string[] glyphsOld = { "\u266A", "\u2261", "\u2661", "\u25A4", "\u25CE", "\u25C6", "\u2726", "?" };
            string[] glyphs = app.Cfg.UiNavIcon ? glyphsCat : glyphsOld;

            nav = new NavButton[titles.Length];
            int y = 52;
            for (int i = 0; i < titles.Length; i++)
            {
                NavButton b = new NavButton();
                b.Text = titles[i];
                b.Glyph = glyphs[i];
                b.BackColor = Color.FromArgb(255, 246, 251);
                b.Location = new Point(8, y);
                b.Width = 162;
                b.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                int idx = i;
                b.Click += delegate { SelectPage(idx); };
                p.Controls.Add(b);
                nav[i] = b;
                y += 46;
            }

            // the mascot sits at the bottom, above a hairline that separates the rail
            Label cat = new Label();
// ★ 语言切换 ✗ 放在猫脸上面 ✓
            //   显示的是【点一下会变成什么】✗ 不是当前语言 ✓
            //   中文时显示 "English" ✗ 英文时显示 "中文" ✓
            // ★★★ 单次延迟翻译 ★★★
            //
            // 之前这里是个循环定时器（每 700ms 扫一遍，扫 8 轮）✗
            // 目的是补救"懒加载页面翻不到" ✗
            // ★★★ 但那 5.6 秒里每 700ms 重绘一次 ✗ 用户看到的就是闪烁 ✓
            //
            // 真正的原因是 ShowPage 里的 Apply 放在了 SelectPage 之前 ✗
            // 那时新页面还没建 ✗ 翻了个空 ✓
            // 现在 Apply 已经挪到 SelectPage 之后 ✗ 一次就到位 ✓
            // 这个单次定时器只是兜底：有些文字是页面建完后异步赋值的 ✓
            langOnce = new Timer();
            // ★ 40ms 就够了 ✧ 它只是兜底再翻一次 ✓
            //   原来 300ms ✧ 切完语言要等三百毫秒才到位 ✧ 感觉就是慢 ✓
            langOnce.Interval = 40;
            // ★★★ 去掉这个定时器 ★★★
            //   它会在 Click 结束后再调一次 Lang.Apply ✧
            //   而 Apply 结尾会 Invalidate(true) 整树标脉 ✧
            //   于是屏幕上又出现一次【先擦白、再一个个画】✧
            //   那三帧残缺就是这么来的 ✓
            //   —— 现在 Click 里已经冻住全程、最后统一画 ✧
            //      不需要这个兜底了 ✓
            langOnce.Tick += delegate { langOnce.Stop(); };

            // ★★★ 2026-10-18：换成普通 Button ★★★
            //   RoundButton 是自绘的 ✗ 在这个位置文字一直画不出来 ✗
            //   （宽度在布局前算偏 ✗ 居中的字被推到可见区外）✓
            //   普通 Button 自带文字渲染 ✗ 不会出这种事 ✓
            langBtn = new Button();
            langBtn.FlatStyle = FlatStyle.Flat;
            langBtn.FlatAppearance.BorderSize = 0;   // ★ ★框自己画 ✗ 方角会暴露 ✓
            langBtn.FlatAppearance.BorderColor = Theme.Line;
            langBtn.BackColor = Theme.Card;
            langBtn.ForeColor = Theme.Accent;
            langBtn.Font = Theme.Small;   // ★ 和胶囊一样的字号 ✓
            langBtn.Cursor = Cursors.Hand;
            langBtn.UseVisualStyleBackColor = false;
            // ★ 2026-10-18：自绘圆角边框 ✗ 看起来和风格胶囊一样 ✓
            langBtn.Paint += delegate(object s2, PaintEventArgs e2)
            {
                try
                {
                    e2.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath gp = Rounded.Path(
                        new Rectangle(0, 0, langBtn.Width - 1, langBtn.Height - 1), 12))
                    using (Pen pen = new Pen(Theme.Line))
                        e2.Graphics.DrawPath(pen, gp);
                }
                catch { }
            };
            // ★ 显示【当前语言】而不是"点一下会变成什么"（按用户要求）✓
            langBtn.Text = app.Cfg.Language == "en" ? "English" : "\u4E2D\u6587";
            // ★★★ 它自己不能被翻译 ★★★
            //   否则：构造时是"中文"→ 记进原文表 → 切英文后代码改成 "English"
            //   → 全局翻译再按原文表把它改回 "Chinese" ✗ 用户看到的就是这个 ✓
            langBtn.Tag = Lang.NoTranslate;
            langBtn.Size = new Size(158, 30);
            // ★ 2026-10-18：不用 Right 锚点 ✗ 面板缩放时宽度会跑偏 ✗
            //   结果居中的文字被推到面板外面 ✗ 看起来就是“按钮里没字” ✓
            langBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            langBtn.Location = new Point(10, p.Height - 76);
            langBtn.Click += delegate
            {
                // ★★★ 整个切换过程冻住 ✧ 最后只画一帧 ★★★
                //   不然中间会有两次刷新（PatchRules 内部一次旧语言 ✧
                //   我补的一次新语言 ✓）✧ 屏幕上就是两帧 ✓
                Lang.Freeze(this);
                try
                {
                app.Cfg.Language = app.Cfg.Language == "en" ? "zh" : "en";
                // ★★★ 把翻译开关提到最前面 ★★★
                //   PatchRules 内部也会调 RefreshStatus ✧
                //   如果那时 Lang.English 还是旧值 ✧
                //   它刷出来的就是【旧语言】的状态行 ✓
                //   提到前面之后 ✧ 两次刷新都是新语言 ✧
                //   第二次会被"值没变就不写"挡掉 ✧ 只画一帧 ✓
                Lang.English = app.Cfg.Language == "en";

                // ★★★ 顺序：先写文件（它内部会重载）✗ 再翻界面 ★★★
                //
                //   为什么要这样：PatchRules 内部【自己会调 ReloadRules】✓
                //   之前我在前面又加了一次 ReloadRulesFor ✗ 
                //   两次重载读到不同的值 ✗ 数字就在 6 / 19 之间来回跳 ✓
                //   现在只留一条路：PatchRules 写完文件后按新的 language 重载 ✓
                Dictionary<string, string[]> kk = new Dictionary<string, string[]>();
                kk["\u8BED\u8A00"] = new string[] { app.Cfg.Language };
                app.PatchRules(kk);

                // 文件已经写好、规则已经按新语言重载过了 ✗ 开始翻界面 ✓
                Lang.English = app.Cfg.Language == "en";
                Lang.Apply(this);
                // ★ 不再靠定时器兜底 ✧ Click 里已经一次到位 ✓
                RefreshStats();
                // ★ 芯片上的示例文字也要重算 ✧ 它不在翻译机制里 ✓
                RefreshChipSamples();
                // ★ 2026-10-18：日签标了 NoTranslate ✗ Apply 不会碰它
                //   所以每次翻完都要重算一次 ✓
                if (statusSign != null) statusSign.Text = DailySign();
                // ★★★ 状态行要单独刷 ★★★
                //   statusDetail 标了 Lang.NoTranslate ✧ Lang.Apply 不会碰它 ✓
                //   而它只在 RefreshStatus 里写 ✧ 那个函数是 700ms 定时器驱动的 ✓
                //   —— 不补这一句 ✧ 切完语言要等最多 700ms 才变 ✧ 就是用户说的"慢半拍" ✓
                RefreshStatus();

                langBtn.Text = Lang.English ? "English" : "\u4E2D\u6587";
                app.Log("language = " + app.Cfg.Language
                        + "  rules=" + app.RuleCount + "  kaomoji=" + app.KaomojiCount);
                }
                finally
                {
                    Lang.Thaw(this);   // ★ 解冻 ✧ 整树标脉 ✧ 异步画一次 ✓
                }
            };
            p.Controls.Add(langBtn);
            // ★ 每次缩放都重算一遍 ✗ 宽度给面板减 20 ✓
            EventHandler fitLang = delegate
            {
                if (p == null || langBtn == null) return;
                // ★\u2605 2026-10-18：直接抄导航按钮的实际宽度 ★★
                //   侧栏真实宽度比 p.Width 小 ✗ 自己算总是偏大 ✗
                //   导航按钮是 Anchor Left|Right ✗ 布局完就是对的 ✓
                // ★ 2026-10-18：按侧栏真实客户区算 ✗ 左右各留 8 ✓
                //   抄导航按钮不行 ✗ 它们被 Anchor Right 拉小了 ✓
                // ★ 2026-10-18：侧栏宽度在 layOuter 里写死了 NW = 168 ✓
                //   所以不用量 ✗ 直接 10 + 148 + 10 ✓
                // ★ 2026-10-18：168 里左右各 18 ✗ 真正居中 ✓
                langBtn.Left = 18;
                langBtn.Width = 132;
                langBtn.Top = p.Height - 76;
                // \u2605 2026-10-18\uff1a\u666e\u901a Button \u662f\u65b9\u7684 \u2717 \u7528 Region \u88c1\u6210\u5706\u89d2 \u2713
                try
                {
                    using (GraphicsPath gp = Rounded.Path(
                        new Rectangle(0, 0, langBtn.Width, langBtn.Height), 12))
                    {
                        Region oldR = langBtn.Region;
                        langBtn.Region = new Region(gp);
                        if (oldR != null) oldR.Dispose();
                    }
                }
                catch { }
            };
            p.Resize += fitLang;
            p.SizeChanged += fitLang;   // ★ 布局完会再来一次 ✓
            fitLang(null, EventArgs.Empty);

            // ★★★ 定时重新应用语言 ★★★
            //
            // 为什么必须用定时器：很多页面和控件是【切过去才创建】的
            // （外观页、语气页 ✓），有些文字还是运行时才赋值的
            // （统计数字、状态行、右侧固定栏 ✓）。
            // 只在窗口建好时扫一次的话 ✗ 那些永远是中文 ✓
            //
            // 每 700ms 在英文模式下重扫一遍控件树 ✗
            // 谁新写上去的中文都会被换掉 ✓
            // 中文模式不跑 —— 中文本来就是原文 ✗ 没必要扫 ✓
            cat.Text = "(=^･ω･^=)";
            cat.Font = Theme.Body;
            cat.ForeColor = Color.FromArgb(206, 150, 182);
            cat.BackColor = Color.FromArgb(255, 246, 251);
            cat.AutoSize = false;
            cat.TextAlign = ContentAlignment.MiddleCenter;
            cat.Size = new Size(158, 26);
            cat.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            cat.Location = new Point(10, p.Height - 40);
            p.Controls.Add(cat);
            p.Resize += delegate { cat.Top = p.Height - 40; };

            Panel edge = new Panel();
            edge.Dock = DockStyle.Right;
            edge.Width = 1;
            edge.BackColor = Color.FromArgb(240, 218, 232);
            p.Controls.Add(edge);
            edge.BringToFront();

            return p;
        }

        /// Public so --dumptree can dump a page that is not the one shown at startup.
        public void ShowPage(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return;
            SelectPage(index);

            // ★ 语言要在【页面建好之后】再翻 ✗ 顺序反了就白翻 ✓
            //   一开始写在 SelectPage 之前 ✗ 那时控件还不存在 ✓
            //   ★ 然后再挂一次单次延迟 ✗ 兜住"页面建完后才赋值的文字"（统计数字 ✓）✓
            Lang.Apply(this);
            if (Lang.English && langOnce != null) { langOnce.Stop(); langOnce.Start(); }
            // ★ 切到颜文字库那页时重填一次 ✗ 免得显示的是上次的 ✓
            RefillKaomojiPage();
        }

        void SelectPage(int index)
        {
            // FREEZE FIRST. Doing it at the end (where the animation code started) is too late:
            // pages[i].Visible = true already painted the new page to the screen, then the rail
            // rebuild burned another few dozen milliseconds, and only then did the overlay go up.
            // That gap is exactly the "shows fully, pauses, vanishes, fades in" the user saw.
            // 冻结 + 先拍下当前(旧)页面。
            //
            // 关键：要淡掉的是【旧】页面，不是新页面。第一版盖的是新页面的图、从 alpha 1 淡到
            // 0 —— 底下露出来的正是新页面自己，两者逐像素相同，所以屏幕上什么也没发生。
            // --film 逐帧一比就看出来了：所有帧的差异都来自导航滑块，内容区一动不动。
            Bitmap fadeFrom = null;
            if (_animOn)
            {
                // 旧快照必须在【冻结之前】拍。
                //
                // WM_SETREDRAW 关掉之后 DrawToBitmap 拍到的是空白：探针一直报 "old=ok"，
                // 但第一帧看到的是新页面 —— 因为那张"旧页面"其实是一张白纸。
                // 拍旧页面时画面还没变，本来也不需要冻结；冻结是为了遮住切换那一下。
                try
                {
                    int fw = contentHost.ClientSize.Width, fh = contentHost.ClientSize.Height;
                    if (fw > 20 && fh > 20 && pages.Length > 0 && pages[0] != null)
                    {
                        // 拍【页面】本身，而且用页面自己的尺寸。
                        //
                        // 两个坑都在这几行里踩过：
                        // 1) 拍 contentHost —— WinForms 的 DrawToBitmap 不渲染容器里的子控件内容，
                        //    出来是一张纯色底图（探针取四个采样点全是 FFF6F9，两张快照一模一样）；
                        // 2) 用 contentHost 的尺寸(698x352)去拍页面内容，而覆盖层 Dock=Fill 落在
                        //    内边距里只有 666x338 —— DrawImage 于是缩放约 4.5%，用户看到的就是
                        //    "切换时卡片缩小、内容往左上偏"。
                        // 用页面自己的尺寸，两边就天然一致，既不缩放也不偏移。
                        StopFade();
                        Control curPg = null;
                        for (int i = 0; i < pages.Length; i++) if (pages[i].Visible) { curPg = pages[i]; break; }
                        if (curPg != null && curPg.Width > 20 && curPg.Height > 20)
                        {
                            fadeFrom = new Bitmap(curPg.Width, curPg.Height);
                            curPg.DrawToBitmap(fadeFrom, new Rectangle(0, 0, curPg.Width, curPg.Height));
                        }
                    }
                    else { FadeDebug = "skip: too small or no pages"; }
                }
                catch (Exception ex) { fadeFrom = null; FadeDebug = "capture threw: " + ex.GetType().Name + " " + ex.Message; }
                if (fadeFrom != null) FadeDebug = "ok " + fadeFrom.Width + "x" + fadeFrom.Height;
                FreezeRedraw(contentHost, true);   // 拍完旧图才冻结，遮住切换那一下
            }
            for (int i = 0; i < nav.Length; i++)
            {
                nav[i].Active = (i == index);
                nav[i].Invalidate();
            }

            // Plain Visibility toggle - all eight pages stay attached.
            //
            // For one round this detached the seven pages that were not in use, on the theory
            // that a hidden control still costs a layout pass. Measurement said otherwise: the
            // resize stayed at ~100ms, and detaching added a re-attach + relayout on every page
            // switch, which showed up as a visible flash of overlapping controls. No gain, a
            // real regression, so it is back to the way it always was.
            for (int i = 0; i < pages.Length; i++)
                pages[i].Visible = (i == index);

            // ④ 切换页面时的淡入 (UiAnim)
            //
            // The first attempt moved the real page with Left = x. That repaints the page's
            // ~100 child controls on every frame, so it stuttered and tore - the user's verdict
            // was simply "this animation is no good". Rendering the page to a BITMAP first and
            // fading that costs one blit per frame and does not care how many controls there
            // are, which is the standard way to do this in WinForms.
            //
            // The overlay is removed on the last frame and also before starting a new one, so a
            // fast series of clicks cannot leave it stuck on screen.
            if (_animOn)
            {
                int tAll = Environment.TickCount;
                StartNavSlide(index);
                // 淡入的是【新】页面：先把真页面藏起来，只在空卡片上显示一张半透明的图。
                //
                // 之前盖的是旧页面，用户看到的是"点一下旧页面又冒出来再消失"，反馈原话是
                // 「原先界面淡入然后其他界面直接显示出来没有淡出的效果」。淡入和淡出在这种
                // 场景下不是一回事：盖旧图 = 旧页闪现；藏起新页、让新图逐渐显现 = 真的淡入。
                try
                {
                    int w2 = contentHost.ClientSize.Width, h2 = contentHost.ClientSize.Height;
                    if (w2 > 20 && h2 > 20)
                    {
                        // 拍之前必须先把布局跑完。
                        //
                        // 新页面是刚刚才 Visible=true 的，布局还没走过一遍，这时 DrawToBitmap
                        // 拍到的卡片是没撑开的小尺寸。用户看到的就是"切换时卡片缩小，切完再
                        // 变回来" —— 缩小的不是真卡片，是我拍的这张快照。
                        pages[index].PerformLayout();
                        contentHost.PerformLayout();

                        Bitmap snapNew = new Bitmap(pages[index].Width, pages[index].Height);
                        pages[index].DrawToBitmap(snapNew,
                            new Rectangle(0, 0, pages[index].Width, pages[index].Height));
                        FadeDebug += " | new ok " + snapNew.Width + "x" + snapNew.Height
                                   + " | old=" + (fadeFrom == null ? "NULL" : "ok");
                        // 两张都要：覆盖层自己把旧图叠在新图上做交叉淡化
                        StartFade(fadeFrom, snapNew);
                    }
                }
                catch { }
                FreezeRedraw(contentHost, false);

            }
            // 重活（重建侧栏、刷新统计）也在冻结里做，做完才解冻并开始淡化。
            // 否则计时器会在这些活儿进行时把 22 帧跑完 —— 用户看到的等于没有动画。
            if (_animOn) FreezeRedraw(contentHost, true);
            // 这里【不能】再写 railSig = "" 强制重建。
            //
            // 右边栏显示的是规则和颜文字，跟当前是哪一页没有关系。强制重建会把"喵化规则"
            // "颜文字库"两张卡整体拆掉再建，一拆一建都触发重画 —— 用户看到的就是"切页面时
            // 右边栏一直闪，切完才恢复"。让签名机制自己决定要不要重建。
            // 这里【不调】RefreshRail()。右边栏只跟规则和颜文字有关，与当前页无关；
            // 而且 RefreshStatus（100ms 计时器）已经按签名在检查，规则一变它自己会重建。
            // 在切页时手动调一次，等于每次都把"喵化规则""颜文字库"两张卡拆掉重建 ——
            // 用户看到的就是"点一下别的功能右边栏就闪一下"。
            if (index == 3) FillKaomojiPage();
            if (index == 4) ReloadSettingsPage();
            if (index == 5) RefreshStatsPage();
            if (index == 6) LoadNews();
            if (_animOn) { FreezeRedraw(contentHost, false); BeginFadeAnimation(); }
        }

        Control BuildContent()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            host.Padding = new Padding(16, 8, 16, 6);

            pageTest = BuildTestPage();
            pageRules = BuildRulesPage();
            pageStyle = BuildStylePage();
            pageKaomoji = BuildKaomojiPage();
            pageSettings = BuildSettingsPage();
            pageStats = BuildStatsPage();
            pageNews = BuildNewsPage();
            pageAbout = BuildAboutPage();
            pages = new Panel[]
            {
                pageTest, pageRules, pageStyle, pageKaomoji,
                pageSettings, pageStats, pageNews, pageAbout
            };

            // All pages are Dock=Fill and therefore overlap; SelectPage decides which one is
            // visible, so the add order only affects z-order. They all stay attached now - see
            // the note in SelectPage.
            contentHost = host;
            for (int i = pages.Length - 1; i >= 0; i--) host.Controls.Add(pages[i]);
            return host;
        }

        // ------------------------------------------------------------ right rail

        Control BuildRightRail()
        {
            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill;
            wrap.BackColor = Theme.Bg;
            wrap.Padding = new Padding(4, 8, 16, 12);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 4;
            t.BackColor = Color.Transparent;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 182));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            wrap.Controls.Add(t);

            t.Controls.Add(BuildRailRules(), 0, 0);
            t.Controls.Add(BuildRailStyles(), 0, 1);
            t.Controls.Add(BuildRailKaomoji(), 0, 2);
            t.Controls.Add(BuildRailStats(), 0, 3);
            return wrap;
        }

        Control BuildRailRules()
        {
            RailCard c = new RailCard(Lang.T("喵化规则"), Lang.T("管理规则 ›"), delegate { SelectPage(1); });
            c.Margin = new Padding(0, 0, 0, 8);
            c.Dock = DockStyle.Fill;

            railRulesBody = new SmoothPanel();
            railRulesBody.Dock = DockStyle.Fill;
            railRulesBody.BackColor = Theme.Card;
            railRulesBody.AutoScroll = true;
            c.Body.Controls.Add(railRulesBody);
            return c;
        }

        void FillRailRules()
        {

            // ★ 挂起布局 ✧ 免得清空+重建的中间态被画出来 ✓
            if (railRulesBody == null || app.Cfg == null) return;
            railRulesBody.Controls.Clear();

            Label h1 = Tiny(Lang.T("原文"), Theme.Muted, 0, 0);
            Label h2 = Tiny(Lang.T("替换内容"), Theme.Muted, 116, 0);
            railRulesBody.Controls.Add(h1);
            railRulesBody.Controls.Add(h2);

            int y = 20;
            int n = 0;
            foreach (Rule r in app.Cfg.Rules)
            {
                if (n++ >= 6) break;
                railRulesBody.Controls.Add(Tiny(r.From, Theme.Text, 0, y));
                railRulesBody.Controls.Add(Tiny(r.To, Theme.Accent, 116, y));
                y += 21;
            }
            if (app.Cfg.Rules.Count == 0)
                railRulesBody.Controls.Add(Tiny(Lang.T("（还没有规则）"), Theme.Muted, 0, 20));
        }

        Label Tiny(string text, Color fg, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.Tiny;
            l.ForeColor = fg;
            l.BackColor = Theme.Card;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Location = new Point(x, y);
            l.Size = new Size(112, 20);
            return l;
        }

        Control BuildRailStyles()
        {
            RailCard c = new RailCard(Lang.T("喵化风格"), Lang.T("更多风格 ›"), delegate { SelectPage(2); });
            c.Margin = new Padding(0, 0, 0, 8);
            c.Dock = DockStyle.Fill;

            TableLayoutPanel g = new TableLayoutPanel();
            g.Dock = DockStyle.Fill;
            g.BackColor = Theme.Card;
            // 2 per row: three across a 300px rail squeezed the names down to "萌…"
            g.ColumnCount = 2;
            g.RowCount = 3;
            for (int i = 0; i < 2; i++) g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 3; i++) g.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3f));
            c.Body.Controls.Add(g);

            chips = new StyleChip[LookStyles.Length];
            for (int i = 0; i < LookStyles.Length; i++)
            {
                StyleChip chip = new StyleChip();
                chip.Text = LookStylesNow[i].Name;
                // ★ 这些卡片是【切过去才建】的 ✗ 建的时候全局翻译早跑完了 ✓
                //   就地补一次 ✗ 英文模式下名字才是英文 ✓
                Lang.Apply(chip);
                chip.Sample = MakeSample(Lang.English ? "Hello" : "你好", LookStylesNow[i].Suffix);
                chip.Dock = DockStyle.Fill;
                chip.Margin = new Padding(3);
                int idx = i;
                chip.Click += delegate { ApplyLookAt(idx); };
                g.Controls.Add(chip, i % 2, i / 2);
                chips[i] = chip;
            }
            return c;
        }

        Control BuildRailKaomoji()
        {
            RailCard c = new RailCard(Lang.T("颜文字库"), Lang.T("管理 ›"), delegate { SelectPage(1); });
            c.Margin = new Padding(0, 0, 0, 8);
            c.Dock = DockStyle.Fill;

            railKaomojiBody = new SmoothPanel();
            railKaomojiBody.Dock = DockStyle.Fill;
            railKaomojiBody.BackColor = Theme.Card;
            railKaomojiBody.AutoScroll = true;
            c.Body.Controls.Add(railKaomojiBody);
            return c;
        }

        void FillRailKaomoji()
        {
            // ★★★ 内容没变就【一个控件都不动】★★★
            //   这个函数每次刷新都会被调（含 700ms 定时器 ✓）✗ 
            //   而它内部是 Clear + 逐个 Add ✗ 
            //   内容一样也重建 ✗ 屏幕上就会闪 ✓

            // ★ 挂起布局 ✧ 免得清空+重建的中间态被画出来 ✓
            if (railKaomojiBody == null || app.Cfg == null) return;
            railKaomojiBody.Controls.Clear();

            int x = 0, y = 0;
            int limit = Math.Max(160, railKaomojiBody.ClientSize.Width - 6);
            foreach (string k in app.Cfg.Kaomoji)
            {
                Size sz = TextRenderer.MeasureText(k, Theme.Small);
                int w = sz.Width + 14;
                if (x > 0 && x + w > limit) { x = 0; y += 26; }
                Label l = new Label();
                l.Text = k;
                l.Font = Theme.Small;
                l.ForeColor = Theme.Accent;
                l.BackColor = Color.FromArgb(255, 244, 250);
                l.AutoSize = false;
                l.TextAlign = ContentAlignment.MiddleCenter;
                l.Location = new Point(x, y);
                l.Size = new Size(w, 22);
                railKaomojiBody.Controls.Add(l);
                x += w + 5;
            }
            if (app.Cfg.Kaomoji.Count == 0)
                railKaomojiBody.Controls.Add(Tiny(Lang.T("（还没有颜文字）"), Theme.Muted, 0, 0));
        }

        Control BuildRailStats()
        {
            RailCard c = new RailCard(Lang.T("今日统计"), Lang.T("更多 ›"), delegate { SelectPage(3); });
            c.Margin = new Padding(0);
            c.Dock = DockStyle.Fill;

            statTotal = StatLine(c.Body, Lang.T("喵化次数"), 0);
            statToday = StatLine(c.Body, Lang.T("今日"), 26);
            statLevel = StatLine(c.Body, Lang.T("喵化等级"), 52);

            levelBar = new LevelBar();
            levelBar.Location = new Point(0, 80);
            levelBar.Height = 18;
            levelBar.Width = 240;
            c.Body.Controls.Add(levelBar);
            c.Body.Resize += delegate { levelBar.Width = Math.Max(60, c.Body.Width); };

            statLevelHint = new Label();
            statLevelHint.Font = Theme.Tiny;
            statLevelHint.ForeColor = Theme.Muted;
            statLevelHint.BackColor = Theme.Card;
            statLevelHint.AutoSize = false;
            statLevelHint.TextAlign = ContentAlignment.MiddleLeft;
            statLevelHint.Location = new Point(0, 102);
            statLevelHint.Size = new Size(240, 20);
            c.Body.Controls.Add(statLevelHint);
            return c;
        }

        // label on the left, value right-aligned on the same line
        Label StatLine(Control host, string caption, int y)
        {
            Label cap = Tiny(caption, Theme.Muted, 0, y);
            cap.Size = new Size(90, 24);
            host.Controls.Add(cap);

            Label val = new Label();
            val.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            val.ForeColor = Theme.Accent;
            val.BackColor = Theme.Card;
            val.AutoSize = false;
            val.TextAlign = ContentAlignment.MiddleLeft;
            val.Location = new Point(94, y);
            val.Size = new Size(140, 24);
            host.Controls.Add(val);
            return val;
        }

        void RefreshStats()
        {
            Stats s = app.Stat;
            // ★★★ 文字直接按当前语言生成 ✧ 不再遍历整棵树 ★★★
            //   原来：先写中文 ✧ 再 Lang.ApplyLight(this) 把整棵控件树过一遍 ✓
            //   而这个函数【每次刷新都跑】✧ 每次都遍历几百个控件 ✓
            //   —— 旧版（2.14.2）根本没有这一步 ✧ 这就是差别 ✓
            if (statTotal != null) statTotal.Text = s.Total + Lang.T(" 次");
            if (statToday != null) statToday.Text = s.Today + Lang.T(" 次");
            if (statLevel != null) statLevel.Text = "Lv." + s.Level;
            if (levelBar != null) levelBar.Value = s.LevelProgress;
            if (statLevelHint != null)
                statLevelHint.Text = Lang.T("距离 Lv.") + (s.Level + 1) + Lang.T(" 还差 ") +
                    Math.Max(0, s.LevelCeil - s.Total) + Lang.T(" 次　最常用 ") +
                    (s.MostUsed().Length > 0 ? s.MostUsed() : "—");
        }

        void RefreshRail()
        {
            if (suppressRefresh)
            {
                refreshPending = true;
                return;
            }
            FillRailRules();
            FillRailKaomoji();
            RefreshStats();
            RefreshStatsPage();
        }

        /// \u2605 2026-10-18\uff1a\u4eca\u65e5\u55b5\u7b7e \u2727 \u6309\u65e5\u671f\u9009 \u2717 \u4e0d\u8054\u7f51 \u2713
        static readonly string[] SignTexts = new string[]
        {
            "\u4eca\u5929\u7684\u4f60\u975e\u5e38\u9002\u5408\u6492\u5a07\u55b5\uff01",
            "\u55b5\u4e86\u5c31\u662f\u55b5\u4e86\uff0c\u4e0d\u9700\u8981\u7406\u7531\u3002",
            "\u4eca\u5929\u8bed\u6c14\u8f6f\u4e00\u70b9\uff0c\u522b\u4eba\u4f1a\u5bf9\u4f60\u66f4\u597d\u3002",
            "\u522b\u4eba\u90fd\u5728\u804a\u5929\uff0c\u4f60\u5728\u55b5\u3002",
            "\u4eca\u5929\u9002\u5408\u5f88\u9ad8\u5174\u5730\u56de\u6d88\u606f\u3002",
            "\u732b\u732b\u4e0d\u8bf4\u8bdd\uff0c\u4f46\u732b\u732b\u90fd\u61c2\u3002",
            "\u55b5\u4e00\u4e0b\u4e0d\u4f1a\u6389\u7ea7\uff0c\u653e\u5fc3\u3002",
            "\u4eca\u5929\u53ef\u4ee5\u5c1d\u8bd5\u4e00\u4e2a\u4ece\u6765\u6ca1\u7528\u8fc7\u7684\u98ce\u683c\u3002",
            "\u522b\u4eba\u770b\u4e0d\u61c2\u7684\u55b5\u624d\u662f\u597d\u55b5\u3002",
            "\u4eca\u5929\u7684\u732b\u732b\u5f88\u4e56\uff0c\u4e5f\u5f88\u55b5\u3002",
            "\u8fd9\u4e2a\u65f6\u95f4\u70b9\u55b5\u4e00\u4e0b\uff0c\u5fc3\u60c5\u4f1a\u597d\u3002",
            "\u55b5\u5b8c\u5c31\u8dd1\uff0c\u4e0d\u56de\u5934\u3002",
        };

        /// \u2605 2026-10-18\uff1a\u65e5\u7b7e\u7684\u82f1\u6587\u7248 \u2713
        static readonly string[] SignTextsEn = new string[]
        {
            "You are extremely good at acting cute today!",
            "Meowing is its own reason. No justification needed.",
            "Speak a little softer today and people will treat you better.",
            "Everyone else is chatting. You are meowing.",
            "A good day to reply to messages cheerfully.",
            "Cats do not talk, but cats understand.",
            "One meow will not cost you a level. Relax.",
            "Try a style you have never used before today.",
            "The best meow is the one nobody else understands.",
            "The cat is well behaved today. And very meowy.",
            "Right now is a good time for one meow.",
            "Meow, then run. Do not look back.",
        };

        static string DailySign()
        {
            int day = DateTime.Now.Year * 1000 + DateTime.Now.DayOfYear;
            int i = (int)(((uint)day * 2654435761u >> 8) % (uint)SignTexts.Length);
            if (i < 0) i = -i;
            string[] pool = Lang.English ? SignTextsEn : SignTexts;
            if (i >= pool.Length) i = pool.Length - 1;
            string sign = new string('\u2605', 1 + (day % 5));
            return sign + " " + pool[i];
        }

        Panel BuildTestPage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(18, 14, 18, 16);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 1;
            t.RowCount = 5;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            // 44, not 38: the buttons are 30 tall and FlowLayoutPanel wraps each in a 3px
            // margin, so the strip needs 36 plus room for its own border. At 38 the strip
            // came out 33 tall and cut the bottom off every button.
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            card.Controls.Add(t);

            Panel l1Row = new Panel();
            l1Row.Dock = DockStyle.Fill;
            l1Row.BackColor = Theme.Card;

            Label l1 = new Label();
            l1.Text = Lang.T("喵化实验室　输入你想说的话，下面立刻显示效果");
            l1.Font = Theme.Head;
            l1.ForeColor = Theme.Text;
            l1.BackColor = Theme.Card;
            l1.AutoSize = true;
            // Head font measures 23px tall; at y=4 inside a 24px row the bottom 3px of every
            // glyph was cut off. The row is 34 now and the label sits at 5, so it fits.
            l1.Location = new Point(0, 5);
            l1Row.Controls.Add(l1);

            testCount = new Label();
            testCount.Text = "0 / 500";
            testCount.Font = Theme.Tiny;
            testCount.ForeColor = Theme.Muted;
            testCount.BackColor = Theme.Card;
            testCount.AutoSize = true;
            testCount.Top = 8;
            l1Row.Controls.Add(testCount);
            l1Row.Resize += delegate { testCount.Left = Math.Max(0, l1Row.Width - testCount.Width); };


            t.Controls.Add(l1Row, 0, 0);

            testIn = new FieldBox();
            testIn.Dock = DockStyle.Fill;
            testIn.Box.TextChanged += delegate { UpdatePreview(); };
            t.Controls.Add(testIn, 0, 1);

            Panel l2Row = new Panel();
            l2Row.Dock = DockStyle.Fill;
            l2Row.BackColor = Theme.Card;

            Label l2 = new Label();
            l2.Text = Lang.T("喵化之后");
            l2.Font = Theme.Small;
            l2.ForeColor = Theme.Muted;
            l2.BackColor = Theme.Card;
            l2.AutoSize = true;
            l2.Location = new Point(0, 6);
            l2Row.Controls.Add(l2);

            toneSource = new Label();
            toneSource.Text = "";
            toneSource.Font = Theme.Tiny;
            toneSource.ForeColor = Theme.Muted;
            toneSource.BackColor = Theme.Card;
            toneSource.AutoSize = true;
            toneSource.Top = 8;
            l2Row.Controls.Add(toneSource);
            l2Row.Resize += delegate { toneSource.Left = Math.Max(0, l2Row.Width - toneSource.Width); };

            t.Controls.Add(l2Row, 0, 2);

            testOut = new FieldBox();
            testOut.Dock = DockStyle.Fill;
            testOut.Box.ReadOnly = true;
            testOut.Box.ForeColor = Theme.Ok;
            t.Controls.Add(testOut, 0, 3);

            if (app.Cfg.UiLab)
            {
            // ---- 试验：实验室强化（UI_LAB）-----------------------------------------
            // A catgirl between the two boxes, so the middle strip says what the page does
            // instead of just labelling the second box.
            //
            // The row grew from 32 to 50 and the label was 16pt, which took 18px off each of
            // the two text boxes - they looked squeezed. 34 with a 12pt label keeps the strip
            // legible and gives almost all of that height back.
            t.RowStyles[2] = new RowStyle(SizeType.Absolute, 34);

            Label midCat = new Label();
            midCat.Font = new Font("Microsoft YaHei UI", 12f);
            midCat.ForeColor = Color.FromArgb(206, 96, 152);
            midCat.BackColor = Theme.Card;
            midCat.AutoSize = true;
            // ฅ^•ﻌ•^ฅ  ->  (=^･ω･^=)  喵化中～   (chosen from a rendered candidate sheet)
            // Written as code points: this file has been mangled by encoding more than once
            // when non-ASCII went through a shell.
            midCat.Text = "(=\u005E\uFF65\u03C9\uFF65\u005E=)  "
                        + "\u55B5\u5316\u4E2D\uFF5E";
            l2Row.Controls.Add(midCat);
            l2Row.Resize += delegate
            {
                midCat.Left = Math.Max(0, (l2Row.Width - midCat.Width) / 2);
                midCat.Top = 4;
            };

            // the result box gets a pink tint: the two boxes were identical, so there was
            // nothing on the page saying which one is the answer
            testOut.Box.BackColor = Color.FromArgb(255, 235, 246);
            testIn.Box.BackColor = Color.White;

            // the main action was the same height as the two secondary ones
            t.RowStyles[4] = new RowStyle(SizeType.Absolute, 54);
            }

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Card;
            bar.WrapContents = false;
            bar.Padding = new Padding(0);
            // No top padding: FlowLayoutPanel already adds a 3px margin per child, so the
            // old 6 stacked on top of it and pushed the buttons to y=9 inside a 33px strip -
            // the bottom 6px of every button was cut off.

            RoundButton bCopy = new RoundButton();
            bCopy.Text = Lang.T("复制结果");
            bCopy.Size = new Size(96, 30);
            bCopy.BackColor = Theme.Card;
            bCopy.Click += delegate
            {
                string s = testOut.Box.Text;
                if (s.Length == 0) return;
                try { Clipboard.SetText(s); } catch { }
                // confirm in place instead of a popup that has to be dismissed
                bCopy.Text = Lang.T("已复制 ✓");
                System.Windows.Forms.Timer back = new System.Windows.Forms.Timer();
                back.Interval = 1200;
                back.Tick += delegate
                {
                    back.Stop();
                    back.Dispose();
                    bCopy.Text = Lang.T("复制结果");
                };
                back.Start();
            };
            bar.Controls.Add(bCopy);

            RoundButton bAgain = new RoundButton();
            bAgain.Text = Lang.T("再喵一次");
            bAgain.Size = new Size(96, 30);
            bAgain.BackColor = Theme.Card;
            bAgain.Click += delegate
            {
                // ★ 2026-10-18：「再喵一次」要换结果 ✧ 种子加一点 ✓
                Engine.BumpSalt();
                UpdatePreview();
            };
            bar.Controls.Add(bAgain);

            RoundButton bNow = new RoundButton();
            bNow.Text = Lang.T("立即喵化");
            bNow.Size = new Size(110, 30);
            if (app.Cfg.UiLab)
            {
            bNow.Size = new Size(142, 40);
            // Segoe UI Emoji renders Chinese fine through font linking - verified by drawing
            // it, not assumed - so the paw and the label can share one button.
            bNow.Font = new Font("Segoe UI Emoji", 12f, FontStyle.Bold);
            bNow.Text = "\U0001F43E 立即喵化";
            }
            bNow.Primary = true;   // RoundButton paints itself; BackColor is ignored
            bNow.Click += delegate
            {
                // same transform the hotkey runs, applied to the box in place
                testIn.Box.Text = app.TransformForPreview(testIn.Box.Text);
                UpdatePreview();
            };
            bar.Controls.Add(bNow);

            t.Controls.Add(bar, 0, 4);

            return card;
        }

        Label PageNote(Control host, string text, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.Small;
            l.ForeColor = Theme.Muted;
            l.BackColor = Theme.Card;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Location = new Point(0, y);
            l.Size = new Size(800, 22);
            FitToHost(l, host);
            host.Controls.Add(l);

            // ★ 断言改到 FitToHost 里 ✧ 等布局完了再量 ✓
            //   原来在这里量 ✧ 那时候还没布局 ✧ 宽度是 0 ✧
            //   FitToHost 会退到 160px ✧ 于是每次启动都误报 5 次 ✗
            //   —— ★ 但真正需要这个保护 ✧ 所以挪到尺寸真正等对之后 ✓

            return l;
        }

        /// Keeps a label inside its card.
        ///
        /// These labels were hard-coded to 800px, but the settings card is only ~666 wide -
        /// so every note stuck 134px past the right edge, the card grew a horizontal
        /// scrollbar, and scrolling it cut the left of the text off. "四项美化可以单独关掉…"
        /// rendered as "项美化可以单独关掉…", which is what a user reported as a broken
        /// border. An explicit handler, not Anchor: anchoring preserves the (already wrong)
        /// right margin, so an overflowing label stays overflowing.
        void FitToHost(Control l, Control host)
        {
            // ★ 只报一次 ✧ 而且要等宽度真的对了才报 ✓
            bool checkedOnce = false;
            EventHandler fit = delegate
            {
                int w = host.ClientSize.Width - l.Left - 6;
                l.Width = w > 160 ? w : 160;

                // ★★★ 真正的截断检查 ✧ 宽度 > 200 才算布局完了 ✓
                //
                // 为什么要挪到这里：
                //   原来在 PageNote 里量 ✧ 那时 host 还没定尺 ✧
                //   ClientSize.Width = 0 ✧ 宽度退到最小的 160px ✧
                //   于是“需要 34px”“需要 102px”的假警报每次启动刷 5 条 ✗
                //   —— ★ 用户 2026-10-17 报过这个：页面上字全是完整的 ✗ 日志却一直报 ✓
                if (!checkedOnce && l.Width > 200 && !string.IsNullOrEmpty(l.Text))
                {
                    checkedOnce = true;
                    try
                    {
                        int need = TextRenderer.MeasureText(l.Text, l.Font,
                                new Size(l.Width, int.MaxValue), TextFormatFlags.WordBreak).Height;
                        if (need > l.Height + 1)
                        {
                            app.Log(Lang.T("!! 提示文字真的会被截断（需要 ") + need + Lang.T("px，只有 ")
                                + l.Height + "px）：" + l.Text);
                        }
                    }
                    catch { }
                }
            };
            host.Resize += fit;
            fit(null, EventArgs.Empty);
        }

        Label PageSection(Control host, string text, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.Head;
            l.ForeColor = Theme.Text;
            l.BackColor = Theme.Card;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Location = new Point(0, y);
            l.Size = new Size(800, 26);
            FitToHost(l, host);
            host.Controls.Add(l);
            return l;
        }

        /// A row of style chips laid out in `cols` columns; returns the created set.
        StyleChip[] PageChips(Control host, int x, int y, int cols, int rows,
                              StyleDef[] defs, Action<int> onClick)
        {
            return PageChips(host, x, y, cols, rows, defs, onClick, 150);
        }

        /// ★ chipW 可选 ✧ 默认 150 ✓
        ///
        /// 为什么要加："程度"那三个芯片的说明文字比较长 ✧
        ///   "你好　只在整段末尾加一次" 在 150px 里放不下 ✓
        ///   而 StyleChip 画第二行用的是 EndEllipsis ✧ 就变成"…"了 ✓
        /// —— 而整页宽约 830px ✧ 三格只用了 450px ✧ 右边空着 380px ✓
        ///    所以直接加宽就行 ✓
        StyleChip[] PageChips(Control host, int x, int y, int cols, int rows,
                              StyleDef[] defs, Action<int> onClick, int chipW)
        {
            TableLayoutPanel g = new TableLayoutPanel();
            g.BackColor = Theme.Card;
            g.Location = new Point(x, y);
            g.Size = new Size(cols * chipW, rows * 50);
            g.ColumnCount = cols;
            g.RowCount = rows;
            for (int i = 0; i < cols; i++) g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            for (int i = 0; i < rows; i++) g.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            host.Controls.Add(g);

            StyleChip[] set = new StyleChip[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                if (i >= cols * rows) break;
                StyleChip chip = new StyleChip();
                chip.Text = defs[i].Name;
                Lang.Apply(chip);   // ★ 同上：懒加载页面要就地翻 ✓
                chip.Sample = MakeSample(Lang.English ? "Hello" : "你好", defs[i].Suffix);
                chip.Dock = DockStyle.Fill;
                chip.Margin = new Padding(4);
                int idx = i;
                chip.Click += delegate { onClick(idx); };
                g.Controls.Add(chip, i % cols, i / cols);
                set[i] = chip;
            }
            return set;
        }

        Panel BuildStylePage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(22, 16, 10, 16);
            // CardPanel 是自绘控件（UserPaint）且故意不清除背景，这类控件滚动会留下横向
            // 残影 —— 和设置页同一个病。滚动改由一层普通面板负责。
            card.AutoScroll = false;
            SmoothPanel scroller2 = new SmoothPanel();
            scroller2.Dock = DockStyle.Fill;
            scroller2.AutoScroll = true;
            scroller2.BackColor = Theme.Card;
            card.Controls.Add(scroller2);

            Panel host = new Panel();
            host.BackColor = Theme.Card;
            host.Dock = DockStyle.Top;
            // ★ 2026-10-18：这里还没有 gridBottom ✧ 直接算一遍 ✓
            scroller2.Controls.Add(host);

            Label title = new Label();
            title.Text = Lang.T("喵化风格");
            title.Font = Theme.Title;
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Card;
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Location = new Point(0, 0);
            title.Size = new Size(800, 32);
            host.Controls.Add(title);
                FitToHost(title, host);

            PageNote(host, Lang.T("① 喵化程度 —— 决定多久加一次装饰"), 62);
            // ★ 程度这一行用 272px/格 ✧ 说明文字才能全显示出来 ✓
            //   （默认 150px 放不下 ✧ 会变成省略号 ✓）
            levelChips = PageChips(host, 0, 88, 3, 1, LevelDefs(), delegate(int i) { ApplyLevelAt(i); }, 190);
            PageNote(host, Lang.T("轻度 = 只在整段末尾加一次　　普通 = 每个句号/问号/感叹号后都加　　重度 = 连逗号后面也加"), 138);

            // ★ 2026-10-18：语气叠加开关
            tglStack = new ToggleRow();
            tglStack.Text = Lang.T("启用语气风格叠加（外观和语气同时生效）");
            tglStack.Size = new Size(560, 28);
            tglStack.Location = new Point(4, 170);
            tglStack.BackColor = Color.White;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            tglStack.Checked = app.Cfg.StackStyles;
            tglStack.CheckedChanged += delegate
            {
                Dictionary<string, string[]> v = new Dictionary<string, string[]>();
                v[Lang.T("语气叠加")] = new string[] { tglStack.Checked ? "true" : "false" };
                app.PatchRules(v);
                app.Log("stack styles = " + tglStack.Checked);
                if (noteStack != null) noteStack.Text = StackNoteText();
                if (!tglStack.Checked && toneActive < ToneStylesNow.Length)
                    ApplyToneAt(ToneStylesNow.Length);
            };
            host.Controls.Add(tglStack);

            // ★ 2026-10-18：几何参数集中到这里 ✧ 以后只改这一处 ✓
            const int gridTop = 236;      // 二层网格的起点
            const int rowH = 50;          // 每行膊囊高
            int lookRowsInit = (LookStylesNow.Length + 2) / 3;
            if (lookRowsInit < 5) lookRowsInit = 5;   // ★ 不能比原来矮
            int gridBottom = gridTop + lookRowsInit * rowH;   // ★ 网格实际底部
            PageSection(host, Lang.T("② 外观风格 —— 只改句尾装饰，不动语气（可以叠加）"), gridTop - 28);

            // ★ 2026-10-18：自定义风格入口 ✗ 放标题右边 ✗ 不动网格布局 ✓
            RoundButton btnNewLook = new RoundButton();
            btnNewLook.Text = Lang.T("+ 新建");
            // ★ 2026-10-18：RoundButton 默认样式下文字看不见 ✧ 要么 Primary 要么给 ForeColor ✓
            btnNewLook.Primary = true;
            btnNewLook.Font = Theme.Small;
            // \u2605 \u5173\u952e\uff1a\u4e0d\u8ba9 Lang.Apply \u53bb\u67e5\u7ffb\u8bd1 \u2727 \u67e5\u4e0d\u5230\u4f1a\u88ab\u5199\u6210\u7a7a\u4e32 \u2717
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            btnNewLook.Size = new Size(96, 28);
            // ★ 2026-10-18：位置很难摆 ✗ 上下都有东西 ✓
            //   y=196 以下是胶囊网格（它的 TableLayoutPanel 会把按钮裁掉）
            //   y=168 那行是「② 外观风格」标题（宽 Label ✗ 重绘时会盖住按钮）
            //   —— 只能放在【同一行的右边】+ BringToFront ✓
            // ★ 2026-10-18：标题 Label 宽 800 ✗ 会把按钮盖掉 ✧ 所以放到网格下面 ✓
            // ★ 这里还没有 gridBottom ✧ 直接算 ✓
            btnNewLook.Location = new Point(0, gridBottom + 6);

            // ★ 2026-10-18：随机来一套
            RoundButton btnRandom = new RoundButton();
            btnRandom.Text = Lang.T("★ 随机来一套");
            btnRandom.Font = Theme.Small;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            btnRandom.Size = new Size(120, 28);
            btnRandom.Location = new Point(104, btnNewLook.Top);
            btnRandom.Click += delegate { RandomStyle(); };
            host.Controls.Add(btnRandom);
            btnNewLook.Click += delegate { NewCustomStyle(); };
            host.Controls.Add(btnNewLook);
            btnNewLook.BringToFront();
            // ★ 2026-10-18：行数按实际数量算 ✧ 自定义风格加进来会超过 15 个 ✓
            pageChips = PageChips(host, 0, gridTop, 3, lookRowsInit, LookStylesNow, delegate(int i) { ApplyLookAt(i); });

            // ★ 整页高度 ✧ 改上面那三个常量就行 ✓
            host.Height = gridBottom + 766;

            PageSection(host, Lang.T("③ 语气改写 —— 二选一，点了会把外观风格换掉"), gridBottom + 80);
            noteStack = PageNote(host, StackNoteText(), gridBottom + 44);
            StyleDef[] toneRow = new StyleDef[ToneStylesNow.Length + 1];
            Array.Copy(ToneStylesNow, toneRow, ToneStylesNow.Length);
            toneRow[ToneStylesNow.Length] = new StyleDef(DryModeName, ToneStylesNow[0].Suffix,
                                                     new string[0]);
            // ★ 2026-10-18：行数按实际数量算 ✧ 自定义语气会超过 15 个 ✓
            int toneRowsInit = (toneRow.Length + 2) / 3;
            if (toneRowsInit < 5) toneRowsInit = 5;
            pageToneChips = PageChips(host, 0, gridBottom + 124, 3, toneRowsInit, toneRow, delegate(int i2) { ApplyToneAt(i2); });

            // ★ 2026-10-18：新建自定义语气 ✗ 放在语气网格下面 ✓
            RoundButton btnNewTone = new RoundButton();
            btnNewTone.Text = Lang.T("+ 新建语气");
            btnNewTone.Primary = true;
            btnNewTone.Font = Theme.Small;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            btnNewTone.Size = new Size(110, 28);
            btnNewTone.Location = new Point(0, gridBottom + 378);
            btnNewTone.Click += delegate { NewCustomTone(); };
            host.Controls.Add(btnNewTone);

            Label warn = new Label();
            warn.Text = Lang.T("⚠ 语气改写会替你在句子开头和结尾加话，等于改了你「说的内容」。\r\n")
                      + Lang.T("　　两个风格是二选一的，点外观风格就会把它关掉——你的词库不会被删，点回语气风格就全回来。");
            warn.Font = Theme.Small;
            warn.ForeColor = Theme.Warn;
            warn.BackColor = Theme.Card;
            // Wrapped, not clipped. AutoSize=false with a fixed 800x24 box cut the second half
            // of the sentence off, and widening the window did not bring it back.
            warn.AutoSize = true;
            warn.MaximumSize = new Size(830, 0);
            warn.TextAlign = ContentAlignment.TopLeft;
            warn.Location = new Point(0, gridBottom + 434);

            // ★ 2026-10-18：我的组合
            PageSection(host, Lang.T("☆ 我的组合（风格 + 程度 + 自动 + 延迟）"), gridBottom + 500);

            comboHost = new Panel();
            comboHost.Location = new Point(0, gridBottom + 528);
            comboHost.Size = new Size(780, 40);
            comboHost.BackColor = Color.Transparent;
            host.Controls.Add(comboHost);

            RoundButton btnSaveCombo = new RoundButton();
            btnSaveCombo.Text = Lang.T("+ 存为组合");
            btnSaveCombo.Primary = true;
            btnSaveCombo.Font = Theme.Small;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            btnSaveCombo.Size = new Size(120, 30);
            btnSaveCombo.Location = new Point(0, gridBottom + 570);
            btnSaveCombo.Click += delegate { SaveCombo(); };
            host.Controls.Add(btnSaveCombo);

            RebuildComboChips();
            warn.Size = new Size(830, 40);
            host.Controls.Add(warn);

            styleCurrent = new Label();
            styleCurrent.Text = "";
            styleCurrent.Font = Theme.Body;
            styleCurrent.ForeColor = Theme.Accent;
            styleCurrent.BackColor = Theme.Card;
            styleCurrent.AutoSize = false;
            styleCurrent.TextAlign = ContentAlignment.MiddleLeft;
            // Placed at the TOP (y=34), not at the bottom. 外观风格 and 语气风格 are
            // independent, so picking 萌系 does NOT switch 傲娇 off - and with only the chip
            // highlights to go on, that read as "I picked 萌系 but got 傲娇". Where a control
            // is created does not matter, only its Location does.
            styleCurrent.Location = new Point(0, 34);
            styleCurrent.Size = new Size(800, 24);
            host.Controls.Add(styleCurrent);
                FitToHost(styleCurrent, host);

            return card;
        }

        static StyleDef[] LevelDefs()
        {
            StyleDef[] d = new StyleDef[LevelNames.Length];
            for (int i = 0; i < d.Length; i++)
                d[i] = new StyleDef(LevelNames[i], "　" + LevelHints[i], new string[0]);
            return d;
        }

        Panel BuildKaomojiPage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(22, 16, 10, 16);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 1;
            t.RowCount = 4;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            // 50, not 46: 34px buttons plus the 3px FlowLayoutPanel margin need 37 of clear
            // strip, and the strip loses a few px to its own border and margins.
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            card.Controls.Add(t);

            Label title = new Label();
            title.Text = Lang.T("颜文字库");
            title.Font = Theme.Title;
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Card;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            t.Controls.Add(title, 0, 0);

            Label hint = new Label();
            hint.Text = Lang.T("每次转换从这里随机挑一个。改动立即写入 rules.txt（只动「颜文字」那几行）。");
            hint.Font = Theme.Small;
            hint.ForeColor = Theme.Muted;
            hint.BackColor = Theme.Card;
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            t.Controls.Add(hint, 0, 1);

            kaoListBody = new SmoothPanel();
            kaoListBody.Dock = DockStyle.Fill;
            kaoListBody.BackColor = Theme.Card;
            kaoListBody.AutoScroll = true;
            t.Controls.Add(kaoListBody, 0, 2);

            Panel add = new Panel();
            add.Dock = DockStyle.Fill;
            add.BackColor = Theme.Card;

            kaoAddBox = new TextBox();
            kaoAddBox.Font = Theme.Body;
            kaoAddBox.Location = new Point(0, 9);
            kaoAddBox.Size = new Size(240, 26);
            add.Controls.Add(kaoAddBox);

            RoundButton bAdd = new RoundButton();
            bAdd.Text = Lang.T("添加");
            bAdd.Primary = true;
            bAdd.Size = new Size(84, 30);
            bAdd.Location = new Point(252, 8);
            bAdd.Click += delegate { AddKaomoji(); };
            add.Controls.Add(bAdd);

            t.Controls.Add(add, 0, 3);
            return card;
        }

        /// ★ 重填颜文字库时挂起布局 ✗ 免得闪出"空"或"半成品" ✓
        ///
        /// FillKaomojiPage 内部是 Controls.Clear() 然后逐个 Add。
        /// Clear 会立刻让那块区域变空并触发重绘，Add 又是一颗一颗加的，
        /// 中间那几帧屏幕上就是【旧内容 → 空 → 半成品 → 新内容】。
        /// 挂起布局能把这些中间态全部挡掉，只画最终结果。
        /// \u2605 2026-10-18\uff1a\u6536\u85CF / \u53D6\u6D88\u6536\u85CF\u4E00\u4E2A\u989C\u6587\u5B57 \u2727 \u5199\u56DE rules.txt \u2713
        void ToggleFavoriteKaomoji(string kao)
        {
            List<string> list = new List<string>();
            foreach (string x in app.Cfg.FavoriteKao) if (x != kao) list.Add(x);
            bool turningOn = (list.Count == app.Cfg.FavoriteKao.Count);   // \u6CA1\u5220\u6389 = \u672C\u6765\u6CA1\u6709 \u2192 \u8981\u52A0
            if (turningOn) list.Add(kao);

            Dictionary<string, string[]> p = new Dictionary<string, string[]>();
            p["收藏"] = list.ToArray();   // ★ PatchKeys 里 [0] 是中文键 ✗ 必须用这个 ✓   // \u2605 \u5FC5\u987B\u7528\u89C4\u8303\u952E \u2727 PatchRules \u4F1A\u628A\u4E2D\u6587\u952E\u8F6C\u6210\u8FD9\u4E2A \u2713
            if (!app.PatchRules(p)) return;

            RefillKaomojiPage();
            RefreshRail();
            app.Log("favorite: " + kao + " -> " + (turningOn ? "on" : "off")
                + " (" + list.Count + " total)");
        }

        /// ★★★ 2026-10-18：新建自定义风格 ★★★
        ///
        /// 一个手搭的小对话框（WinForms 没有输入框的现成弹窗）✗
        /// 三个字段：名字 / 后缀 / 颜文字（一行一个）✓
        /// ★ 2026-10-18：叠加开关对应的说明
        string StackNoteText()
        {
            return app.Cfg.StackStyles
                ? Lang.T("叠加开着：外观管句尾装饰，语气管整句说法，两个同时生效。")
                : Lang.T("二选一：点外观就关语气，点语气就换掉句尾装饰（词语替换不受影响）。");
        }

        // ---------------------------------------------------------------- 我的组合

        void RebuildComboChips()
        {
            try
            {
                if (comboHost == null) return;
                comboHost.Controls.Clear();
                List<string> cs = app.Cfg.Combos;
                if (cs == null || cs.Count == 0)
                {
                    Label none = new Label();
                    none.Text = Lang.T("还没有组合 —— 点下面「+ 存为组合」把当前风格存下来");
                    none.Font = Theme.Small;
                    none.ForeColor = Theme.Muted;
                    none.AutoSize = false;
                    none.Size = new Size(760, 24);
                    none.Location = new Point(2, 6);
                    // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
                    comboHost.Controls.Add(none);
                    return;
                }
                int x = 0, y = 0;
                for (int i = 0; i < cs.Count; i++)
                {
                    string[] p = cs[i].Split('|');
                    string nm = p.Length > 0 ? p[0] : cs[i];
                    RoundButton b = new RoundButton();
                    b.Text = "☆ " + nm;
                    b.Font = Theme.Small;
                    b.Tag = Lang.NoTranslate;
                    b.Size = new Size(150, 30);
                    b.Location = new Point(x, y);
                    int idx = i;
                    b.Click += delegate { ApplyCombo(idx); };
                    // ★ 2026-10-22：组合也给一个看得见的 × ✓
                    b.ShowDelete = true;
                    b.DeleteClicked += delegate
                    {
                        if (MessageBox.Show(this, Lang.T("删掉「") + nm + Lang.T("」？"),
                                Lang.T("删除组合"), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning) != DialogResult.Yes) return;
                        List<string> keep = new List<string>();
                        for (int k3 = 0; k3 < app.Cfg.Combos.Count; k3++)
                            if (k3 != idx) keep.Add(app.Cfg.Combos[k3]);
                        app.Cfg.Combos = keep;
                        SaveCombos();
                        RebuildComboChips();
                    };
                    b.MouseUp += delegate(object s2, MouseEventArgs e)
                    {
                        if (e.Button != MouseButtons.Right) return;
                        ShowItemMenu(b, new Point(0, b.Height), nm, "\u2606", Lang.T("删掉这个组合"), delegate
                        {
                            if (MessageBox.Show(this, Lang.T("删掉「") + nm + Lang.T("」？"),
                                    Lang.T("删除组合"), MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Warning) != DialogResult.Yes) return;
                            List<string> keep = new List<string>();
                            for (int k2 = 0; k2 < app.Cfg.Combos.Count; k2++)
                                if (k2 != idx) keep.Add(app.Cfg.Combos[k2]);
                            app.Cfg.Combos = keep;
                            SaveCombos();
                            RebuildComboChips();
                        });
                    };
                    comboHost.Controls.Add(b);
                    x += 158;
                    if (x > 620) { x = 0; y += 36; }
                }
            }
            catch (Exception ex) { app.Log("RebuildComboChips: " + ex.Message); }
        }

        void SaveCombo()
        {
            try
            {
                string cur = ComboCurrentStyleName();
                if (cur.Length == 0) { MessageBox.Show(this, Lang.T("先点一个外观或语气风格，再存组合。")); return; }

                PinkDialog dlg = new PinkDialog(Lang.T("存为组合"), Lang.T("以后点一下就能切回来～"), 420, 320);
                dlg.SetFooter(Lang.T("组合只记风格名，改过的词库不会被覆盖"));
                PinkInput fName = dlg.MakeField("☆", Lang.T("组合名字（最多 8 个字）"), cur, 6, 6, 366, 8);
                Label hint = new Label();
                hint.Text = Lang.T("当前风格：") + cur;
                hint.Font = Theme.Small;
                hint.ForeColor = Theme.Muted;
                hint.BackColor = Color.White;
                hint.AutoSize = false;
                hint.Location = new Point(6, 82);
                hint.Size = new Size(366, 22);
                // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
                dlg.Body.Controls.Add(hint);

                RoundButton ok = new RoundButton();
                ok.Text = Lang.T("保存");
                ok.Primary = true;
                ok.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
                ok.Size = new Size(140, 38);
                ok.Location = new Point(90, 116);
                dlg.Body.Controls.Add(ok);
                RoundButton cancel = new RoundButton();
                cancel.Text = Lang.T("取消");
                cancel.Font = new Font("Microsoft YaHei UI", 10f);
                cancel.Size = new Size(100, 38);
                cancel.Location = new Point(250, 116);
                dlg.Body.Controls.Add(cancel);
                cancel.Click += delegate { dlg.Close(); };

                ok.Click += delegate
                {
                    string nm = fName.Box.Text.Trim();
                    if (nm.Length == 0) { MessageBox.Show(dlg, Lang.T("名字不能空着。")); return; }
                    if (nm.IndexOf('|') >= 0) { MessageBox.Show(dlg, Lang.T("名字里不能有 |")); return; }
                    List<string> cs = new List<string>();
                    if (app.Cfg.Combos != null)
                        foreach (string c in app.Cfg.Combos)
                            if (c.Split('|')[0] != nm) cs.Add(c);
                    cs.Add(nm + "|" + cur);
                    app.Cfg.Combos = cs;
                    SaveCombos();
                    dlg.Close();
                    RebuildComboChips();
                    app.Log("combo saved: " + nm + " -> " + cur);
                };
                dlg.ShowDialog(this);
            }
            catch (Exception ex) { app.Log("SaveCombo: " + ex.Message); }
        }

        /// \u2605 2026-10-18\uff1a\u6309\u98ce\u683c\u540d\u53cd\u63a8\u5f53\u524d\u7684\u80f6\u56ca\u7d22\u5f15 \u2713
        ///   \u968f\u673a / \u7ec4\u5408\u90fd\u662f\u76f4\u63a5\u8c03 ApplyStyle \u2717 \u4e0d\u4f1a\u66f4\u65b0\u9ad8\u4eae \u2717
        ///   \u6240\u4ee5\u8fd9\u91cc\u53cd\u63a8\u4e00\u6b21 \u2713
        /// \u2605 2026-10-22\uff1a\u7c89\u8272\u5c0f\u83dc\u5355 \u2717 \u66ff\u4ee3\u96be\u770b\u7684\u7cfb\u7edf\u53f3\u952e\u83dc\u5355 \u2713
        ///   \u590d\u7528\u6258\u76d8\u90a3\u5957 ItemRow \u2717 \u5706\u89d2 + \u7c89\u8fb9\u6846 + \u60ac\u505c\u9ad8\u4eae \u2713
        void ShowItemMenu(Control anchor, Point at, string title, string icon, string text, Action action)
        {
            try
            {
                Form f = new Form();
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                f.ShowInTaskbar = false;
                f.BackColor = Theme.Soft;
                f.TopMost = true;
                try { typeof(Form).GetProperty("DoubleBuffered",
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                     .SetValue(f, true, null); } catch { }
                int hasTitle = string.IsNullOrEmpty(title) ? 0 : 26;
                int w = 230;
                int h = 10 + hasTitle + 36;
                f.ClientSize = new Size(w, h);

                using (GraphicsPath gp = Rounded.Path(new Rectangle(0, 0, w - 1, h - 1), 12))
                {
                    Region oldR = f.Region;
                    f.Region = new Region(gp);
                    if (oldR != null) oldR.Dispose();
                }
                f.Paint += delegate(object s2, PaintEventArgs e2)
                {
                    e2.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath gp = Rounded.Path(new Rectangle(0, 0, w - 1, h - 1), 12))
                    using (Pen pen = new Pen(Theme.Line))
                        e2.Graphics.DrawPath(pen, gp);
                };

                if (hasTitle > 0)
                {
                    Label t = new Label();
                    t.Text = title;
                    t.Font = Theme.Tiny;
                    t.ForeColor = Theme.Muted;
                    t.BackColor = Theme.Soft;
                    t.AutoSize = false;
                    t.TextAlign = ContentAlignment.MiddleLeft;
                    t.Location = new Point(12, 8);
                    t.Size = new Size(w - 24, 20);
                    t.Tag = Lang.NoTranslate;
                    f.Controls.Add(t);
                }

                ItemRow row = new ItemRow(f, icon, text, "");
                row.Location = new Point(6, 6 + hasTitle);
                row.Size = new Size(w - 12, 32);
                row.Clicked += delegate
                {
                    try { f.Close(); } catch { }
                    if (action != null) action();
                };
                f.Controls.Add(row);

                Point screen = anchor.PointToScreen(at);
                Rectangle scr = Screen.FromControl(anchor).WorkingArea;
                if (screen.Y + h > scr.Bottom) screen.Y = anchor.PointToScreen(new Point(at.X, 0)).Y - h;
                if (screen.X + w > scr.Right) screen.X = scr.Right - w - 4;
                if (screen.X < scr.Left) screen.X = scr.Left + 4;
                f.Location = screen;
                f.Deactivate += delegate { try { f.Close(); } catch { } };
                f.Show(anchor.FindForm());
            }
            catch (Exception ex) { app.Log("ShowItemMenu: " + ex.Message); }
        }

        void SyncActiveFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            for (int i = 0; i < LookStylesNow.Length; i++)
                if (LookStylesNow[i].Name == name) lookActive = i;
            for (int i = 0; i < ToneStylesNow.Length; i++)
                if (ToneStylesNow[i].Name == name) toneActive = i;

        }

        string ComboCurrentStyleName()
        {
            try
            {
                if (lookActive >= 0 && lookActive < LookStylesNow.Length) return LookStylesNow[lookActive].Name;
                if (toneActive >= 0 && toneActive < ToneStylesNow.Length) return ToneStylesNow[toneActive].Name;
            }
            catch { }
            return "";
        }

        void ApplyCombo(int i)
        {
            try
            {
                if (i < 0 || i >= app.Cfg.Combos.Count) return;
                string[] p = app.Cfg.Combos[i].Split('|');
                if (p.Length < 2) return;
                string styleName = p[1];
                if (styleName.Length > 0 && styleName != "-")
                    if (!ApplyStyleByName(app, styleName)) app.Log("combo: 风格失败 " + styleName);
                app.Log("combo applied: " + p[0] + " -> " + styleName);
                // ★ 2026-10-18：把胶囊高亮对上 ✓
                SyncActiveFromName(styleName);
                ReloadStyleChips();
                SyncChips();
                RefreshRail();
            }
            catch (Exception ex) { app.Log("ApplyCombo: " + ex.Message); }
        }

        void SaveCombos()
        {
            try
            {
                Dictionary<string, string[]> v = new Dictionary<string, string[]>();
                v[Lang.T("我的组合")] = app.Cfg.Combos.ToArray();
                app.PatchRules(v);
            }
            catch (Exception ex) { app.Log("SaveCombos: " + ex.Message); }
        }

        /// ★ 2026-10-18：随机来一套
        void RandomStyle()
        {
            try
            {
                if (LookStylesNow.Length == 0) return;
                int li = rndStyle.Next(LookStylesNow.Length);
                if (!app.ApplyStyle(LookStylesNow[li].Template, LookStylesNow[li].Suffix,
                                    LookStylesNow[li].Kaomoji, null, null)) return;

                string toneName = "";
                if (app.Cfg.StackStyles && ToneStylesNow.Length > 0)
                {
                    int ti = rndStyle.Next(ToneStylesNow.Length);
                    StyleDef td = ToneStylesNow[ti];
                    Dictionary<string, string[]> v = new Dictionary<string, string[]>();
                    v[Lang.T("语气")] = new string[] { "true" };
                    Dictionary<string, string[]> tp = td.ToPools();
                    foreach (string[] pair in App.PatchKeys)
                    {
                        string cn = pair[0], en = pair[1];
                        if (en != "opener" && en != "closer" && !en.EndsWith("closer")) continue;
                        string[] lines = null;
                        tp.TryGetValue(en, out lines);
                        v[cn] = lines ?? new string[0];
                    }
                    app.PatchRules(v);
                    toneName = td.Name;
                }

                // \u2605 2026-10-18\uff1a\u628a\u80f6\u56ca\u9ad8\u4eae\u5bf9\u4e0a \u2713
                lookActive = li;
                SyncActiveFromName(toneName);

                app.Log("random style: " + LookStylesNow[li].Name
                        + (toneName.Length > 0 ? " + " + toneName : ""));
                ReloadStyleChips();
                SyncChips();
                RefreshRail();
                MessageBox.Show(this, Lang.T("随机到了：") + LookStylesNow[li].Name
                                + (toneName.Length > 0 ? " + " + toneName : ""));
            }
            catch (Exception ex) { app.Log("RandomStyle: " + ex.Message); }
        }

        void NewCustomStyle()
        {
            PinkDialog dlg = new PinkDialog(Lang.T("新建自定义风格"),
                                            Lang.T("打造属于你自己的喵化风格～"), 430, 520);
            dlg.SetFooter(Lang.T("让每一句话，都变得喵喵的～"));

            PinkInput fName = dlg.MakeField("Aa", Lang.T("名字（最多 8 个字）"), "", 6, 6, 376, 8);
            PinkInput fSuf = dlg.MakeField("\U0001F43E", Lang.T("句尾后缀（比如 喵★）"), "喵~", 6, 76, 376, 12);
            PinkInput fKao = dlg.MakeArea("\u270E", Lang.T("颜文字（一行一个，至少 1 个）"),
                                          "(｡･ω･｡)\r\n(◡‿◡)\r\n(´･ω･`)", 6, 146, 376, 116);

            RoundButton ok = new RoundButton();
            ok.Text = Lang.T("保存");
            ok.Primary = true;
            ok.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            ok.Size = new Size(150, 40);
            ok.Location = new Point(96, 292);
            dlg.Body.Controls.Add(ok);

            RoundButton cancel = new RoundButton();
            cancel.Text = Lang.T("取消");
            cancel.Font = new Font("Microsoft YaHei UI", 10f);
            cancel.Size = new Size(110, 40);
            cancel.Location = new Point(262, 292);
            dlg.Body.Controls.Add(cancel);
            cancel.Click += delegate { dlg.Close(); };

            ok.Click += delegate
            {
                string name = fName.Box.Text.Trim();
                string suf = fSuf.Box.Text.Trim();
                List<string> kaos = Split(fKao.Box.Text);

                if (name.Length == 0) { MessageBox.Show(dlg, Lang.T("名字不能空着。")); return; }
                if (suf.Length == 0) { MessageBox.Show(dlg, Lang.T("后缀不能空着。")); return; }
                if (kaos.Count == 0) { MessageBox.Show(dlg, Lang.T("至少要有一个颜文字。")); return; }
                foreach (StyleDef d0 in LookStylesNow)
                    if (d0.Name == name) { MessageBox.Show(dlg, Lang.T("这个名字已经有了，换一个。")); return; }

                if (!AppendCustomLook(name, suf, kaos))
                { MessageBox.Show(dlg, Lang.T("保存失败，看看日志。")); return; }
                app.Log("custom style added: " + name + " (" + kaos.Count + " kaomoji)");
                dlg.Close();
                ReloadStyleChips();
            };

            dlg.ShowDialog(this);
        }

        /// ★★★ 2026-10-18：按软件分配风格 ★★★
        /// 把 ProcAuto 写回规则
        void saveAuto()
        {
            try
            {
                List<string> ls = new List<string>();
                foreach (KeyValuePair<string, bool> kv in app.Cfg.ProcAuto)
                    ls.Add(kv.Key + " = " + (kv.Value ? "true" : "false"));
                Dictionary<string, string[]> p = new Dictionary<string, string[]>();
                p[Lang.T("进程自动")] = ls.ToArray();
                app.PatchRules(p);
            }
            catch (Exception ex) { app.Log("saveAuto: " + ex.Message); }
        }

        void EditProcStyles()
        {
            PinkDialog dlg = new PinkDialog(Lang.T("按软件分配风格"),
                                            Lang.T("在不同的软件里用不同的风格～"), 530, 560);
            dlg.SetFooter(Lang.T("没写的程序用全局设置～"));

            Label tip = new Label();
            tip.Text = Lang.T("在指定的程序里自动用指定的风格 ✗ 没写的程序用全局设置 ✓");
            tip.Location = new Point(6, 4);
            tip.Size = new Size(492, 20);
            tip.ForeColor = Theme.Muted;
            tip.BackColor = Color.White;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            dlg.Body.Controls.Add(tip);

            ListBox lb = new ListBox();
            lb.Location = new Point(6, 28);
            lb.Size = new Size(492, 190);
            lb.Font = Theme.Body;
            lb.BorderStyle = BorderStyle.None;
            lb.BackColor = Color.FromArgb(255, 250, 252);
            lb.IntegralHeight = false;
            dlg.Body.Controls.Add(lb);

            Action refill = delegate
            {
                lb.Items.Clear();
                foreach (KeyValuePair<string, string> kv in app.Cfg.ProcessStyles)
                {
                    bool pa = true;
                    app.Cfg.ProcAuto.TryGetValue(kv.Key, out pa);
                    lb.Items.Add(kv.Key + "   \u2192   " + kv.Value
                               + (pa ? Lang.T("   [\u81ea\u52a8\u8f6c]") : Lang.T("   [\u4e0d\u81ea\u52a8]")));
                }
            };
            refill();

            Label tl1 = new Label();
            tl1.Text = Lang.T("进程名");
            tl1.Location = new Point(6, 228);
            tl1.Size = new Size(220, 18);
            tl1.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            tl1.ForeColor = Theme.Text;
            tl1.BackColor = Color.White;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            dlg.Body.Controls.Add(tl1);

            PinkInput fProc = new PinkInput(new Size(220, 36), false);
            fProc.Location = new Point(6, 248);
            dlg.Body.Controls.Add(fProc);

            Label tl2 = new Label();
            tl2.Text = Lang.T("用哪个风格");
            tl2.Location = new Point(244, 228);
            tl2.Size = new Size(224, 18);
            tl2.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            tl2.ForeColor = Theme.Text;
            tl2.BackColor = Color.White;
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            dlg.Body.Controls.Add(tl2);

            ComboBox cb = new ComboBox();
            cb.Location = new Point(244, 250);
            cb.Size = new Size(224, 30);
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Font = Theme.Body;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Items.Add(Lang.T("不加（完全不喵化）"));
            foreach (StyleDef d0 in ToneStylesNow) cb.Items.Add(Lang.T("语气：") + d0.Name);
            foreach (StyleDef d0 in LookStylesNow) cb.Items.Add(Lang.T("外观：") + d0.Name);
            cb.SelectedIndex = 0;
            dlg.Body.Controls.Add(cb);

            // \u2605 2026-10-18\uff1a\u8fd9\u4e2a\u8f6f\u4ef6\u91cc\u8981\u4e0d\u8981\u81ea\u52a8\u8f6c\u6362 \u2713
            CheckBox cbAuto = new CheckBox();
            cbAuto.Text = Lang.T("\u8fd9\u4e2a\u8f6f\u4ef6\u91cc\u81ea\u52a8\u8f6c\u6362\uff08\u4e0d\u52fe = \u53ea\u80fd\u6309\u5feb\u6377\u952e\uff09");
            cbAuto.Font = Theme.Body;
            cbAuto.ForeColor = Theme.Text;
            cbAuto.BackColor = Color.White;
            cbAuto.AutoSize = false;
            cbAuto.Size = new Size(470, 24);
            cbAuto.Location = new Point(6, 296);
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Apply 能翻过去 ✓
            cbAuto.Checked = true;
            dlg.Body.Controls.Add(cbAuto);

            RoundButton add = new RoundButton();
            add.Text = Lang.T("添加");
            add.Primary = true;
            add.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            add.Size = new Size(110, 38);
            add.Location = new Point(6, 330);
            dlg.Body.Controls.Add(add);

            RoundButton del = new RoundButton();
            del.Text = Lang.T("删除选中");
            del.Font = new Font("Microsoft YaHei UI", 9.5f);
            del.Size = new Size(120, 38);
            del.Location = new Point(126, 330);
            dlg.Body.Controls.Add(del);

            RoundButton close = new RoundButton();
            close.Text = Lang.T("关闭");
            close.Font = new Font("Microsoft YaHei UI", 9.5f);
            close.Size = new Size(110, 38);
            close.Location = new Point(246, 330);
            dlg.Body.Controls.Add(close);
            close.Click += delegate { dlg.Close(); };

            Action save = delegate
            {
                List<string> ls = new List<string>();
                foreach (KeyValuePair<string, string> kv in app.Cfg.ProcessStyles)
                    ls.Add(kv.Key + " = " + kv.Value);
                Dictionary<string, string[]> p = new Dictionary<string, string[]>();
                p["进程风格"] = ls.ToArray();
                if (app.PatchRules(p)) app.Log("procstyles saved: " + ls.Count);
            };

            lb.SelectedIndexChanged += delegate
            {
                if (lb.SelectedIndex < 0) return;
                string it = lb.SelectedItem.ToString();
                int ar = it.IndexOf("   \u2192");
                string pn3 = ar > 0 ? it.Substring(0, ar).Trim() : it;
                bool pa2 = true;
                app.Cfg.ProcAuto.TryGetValue(pn3, out pa2);
                cbAuto.Checked = pa2;
                fProc.Box.Text = pn3;
            };

            add.Click += delegate
            {
                string proc = fProc.Box.Text.Trim();
                if (proc.Length == 0) { MessageBox.Show(dlg, Lang.T("进程名不能空着。")); return; }
                if (cb.SelectedIndex < 0) return;
                string styleName;
                if (cb.SelectedIndex == 0) styleName = Lang.T("不加");
                else
                {
                    string item = cb.SelectedItem.ToString();
                    int c2 = item.IndexOf('：');
                    styleName = c2 >= 0 ? item.Substring(c2 + 1) : item;
                }
                app.Cfg.ProcessStyles[proc] = styleName;
                app.Cfg.ProcAuto[proc] = cbAuto.Checked;
                save();
                saveAuto();
                refill();
                fProc.Box.Text = "";
            };

            del.Click += delegate
            {
                if (lb.SelectedIndex < 0) return;
                string item = lb.SelectedItem.ToString();
                int arrow = item.IndexOf("   \u2192");
                string proc = arrow > 0 ? item.Substring(0, arrow).Trim() : item;
                app.Cfg.ProcessStyles.Remove(proc);
                save();
                refill();
            };

            dlg.ShowDialog(this);
        }

        /// ★★★ 2026-10-18：新建自定义语气 ★★★
        ///   比外观多两个字段：开头 / 结尾（一行一个）
        void NewCustomTone()
        {
            PinkDialog dlg = new PinkDialog(Lang.T("新建自定义语气"),
                                            Lang.T("让每一句话都带上你的语气～"), 470, 800);
            dlg.SetFooter(Lang.T("语气是改你说话的方式，不只是句尾～"));

            PinkInput fName = dlg.MakeField("Aa", Lang.T("名字（最多 8 个字）"), "", 6, 6, 416, 8);
            PinkInput fSuf = dlg.MakeField("\U0001F43E", Lang.T("句尾后缀（比如 喵★）"), "喵~", 6, 76, 416, 12);
            PinkInput fKao = dlg.MakeArea("\u270E", Lang.T("颜文字（一行一个，至少 1 个）"),
                                          "(｡･ω･｡)\r\n(◡‿◡)\r\n(´･ω･`)", 6, 146, 416, 96);
            PinkInput fOpen = dlg.MakeArea("\u25B6", Lang.T("开头（一行一个，可留空）"),
                                           "嗯，\r\n那个……", 6, 274, 416, 76);
            PinkInput fClose = dlg.MakeArea("\u25C0", Lang.T("结尾（一行一个，可留空）"),
                                            "，就这样吧\r\n，你说呢", 6, 382, 416, 76);

            RoundButton ok = new RoundButton();
            ok.Text = Lang.T("保存");
            ok.Primary = true;
            ok.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            ok.Size = new Size(150, 40);
            ok.Location = new Point(116, 490);
            dlg.Body.Controls.Add(ok);

            RoundButton cancel = new RoundButton();
            cancel.Text = Lang.T("取消");
            cancel.Font = new Font("Microsoft YaHei UI", 10f);
            cancel.Size = new Size(110, 40);
            cancel.Location = new Point(282, 490);
            dlg.Body.Controls.Add(cancel);
            cancel.Click += delegate { dlg.Close(); };

            ok.Click += delegate
            {
                string name = fName.Box.Text.Trim();
                string suf = fSuf.Box.Text.Trim();
                List<string> kaos = Split(fKao.Box.Text);
                List<string> opens = Split(fOpen.Box.Text);
                List<string> closes = Split(fClose.Box.Text);

                if (name.Length == 0) { MessageBox.Show(dlg, Lang.T("名字不能空着。")); return; }
                if (suf.Length == 0) { MessageBox.Show(dlg, Lang.T("后缀不能空着。")); return; }
                if (kaos.Count == 0) { MessageBox.Show(dlg, Lang.T("至少要有一个颜文字。")); return; }
                foreach (StyleDef d0 in ToneStylesNow)
                    if (d0.Name == name) { MessageBox.Show(dlg, Lang.T("这个名字已经有了，换一个。")); return; }

                if (!AppendCustomTone(name, suf, kaos, opens, closes))
                { MessageBox.Show(dlg, Lang.T("保存失败，看看日志。")); return; }
                app.Log("custom tone added: " + name);
                dlg.Close();
                ReloadToneChips();
            };

            dlg.ShowDialog(this);
        }

        static List<string> Split(string text)
        {
            List<string> outp = new List<string>();
            if (text == null) return outp;
            foreach (string raw in text.Split('\n'))
            {
                string k = raw.Trim();
                if (k.Length > 0) outp.Add(k);
            }
            return outp;
        }

        /// ★ 自定义语气变了之后重建语气胶囊 ✓
        void ReloadToneChips()
        {
            try
            {
                if (pageToneChips == null || pageToneChips.Length == 0) return;
                Control parent = pageToneChips[0] != null ? pageToneChips[0].Parent : null;
                TableLayoutPanel g = parent as TableLayoutPanel;
                if (g == null) return;

                StyleDef[] baseT = Lang.English && ToneStylesEn != null && ToneStylesEn.Length > 0
                                   ? ToneStylesEn : ToneStyles;
                StyleDef[] all = ToneStylesNow;
                int count = all.Length + 1;               // ★ +1 是「不加」
                int rows = (count + 2) / 3;
                if (rows < 5) rows = 5;
                g.RowCount = rows;
                while (g.RowStyles.Count > 0) g.RowStyles.RemoveAt(0);
                for (int i = 0; i < rows; i++) g.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

                g.SuspendLayout();
                try
                {
                    g.Controls.Clear();
                    pageToneChips = new StyleChip[count];
                    for (int i = 0; i < count; i++)
                    {
                        bool isNone = (i == all.Length);
                        StyleChip chip = new StyleChip();
                        chip.Text = isNone ? Lang.T("不加") : all[i].Name;
                        Lang.Apply(chip);
                        chip.Sample = isNone ? "" : MakeSample(Lang.English ? "Hello" : "你好", all[i].Suffix);
                        chip.Dock = DockStyle.Fill;
                        chip.Margin = new Padding(4);
                        int idx = i;
                        if (isNone) chip.Click += delegate { ApplyToneAt(ToneStylesNow.Length); };
                        else
                        {
                            chip.Click += delegate { ApplyToneAt(idx); };
                            if (i >= baseT.Length)
                            {
                                chip.Deletable = true;
                                chip.DeleteClicked += delegate
                                {
                                    if (MessageBox.Show(this, "删掉「" + all[idx].Name + "」？",
                                            "删除自定义语气", MessageBoxButtons.YesNo,
                                            MessageBoxIcon.Warning) != DialogResult.Yes) return;
                                    if (RemoveCustomTone(all[idx].Name))
                                    {
                                        app.Log("custom tone removed: " + all[idx].Name);
                                        ReloadToneChips();
                                    }
                                };
                                chip.MouseUp += delegate(object s2, MouseEventArgs e)
                                {
                                    if (e.Button != MouseButtons.Right) return;
                                    ShowItemMenu(chip, new Point(0, chip.Height), all[idx].Name,
                                        "\u2661", Lang.T("删除这个自定义语气"), delegate
                                    {
                                        if (MessageBox.Show(this, Lang.T("删掉「") + all[idx].Name + Lang.T("」？"),
                                                Lang.T("删除自定义语气"), MessageBoxButtons.YesNo,
                                                MessageBoxIcon.Warning) != DialogResult.Yes) return;
                                        if (RemoveCustomTone(all[idx].Name))
                                        {
                                            app.Log("custom tone removed: " + all[idx].Name);
                                            ReloadToneChips();
                                        }
                                    });
                                };
                            }
                        }
                        g.Controls.Add(chip, i % 3, i / 3);
                        pageToneChips[i] = chip;
                    }
                }
                finally { g.ResumeLayout(true); }

                SyncChips();
                RefreshRail();
            }
            catch (Exception ex) { app.Log("ReloadToneChips failed: " + ex.Message); }
        }

        /// ★ 自定义风格变了之后重建胶囊 ✗ 内置的 15 个也一起重排（数量可能变了）✓
        void ReloadStyleChips()
        {
            try
            {
                if (pageChips == null) return;
                Control parent = pageChips[0] != null ? pageChips[0].Parent : null;
                if (parent == null) return;
                TableLayoutPanel g = parent as TableLayoutPanel;
                if (g == null) return;

                StyleDef[] all = LookStylesNow;
                int rows = (all.Length + 2) / 3;
                if (rows < 1) rows = 1;
                g.RowCount = rows;
                while (g.RowStyles.Count > 0) g.RowStyles.RemoveAt(0);
                for (int i = 0; i < rows; i++) g.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

                g.SuspendLayout();
                try
                {
                    g.Controls.Clear();
                    pageChips = new StyleChip[all.Length];
                    for (int i = 0; i < all.Length; i++)
                    {
                        StyleChip chip = new StyleChip();
                        chip.Text = all[i].Name;
                        Lang.Apply(chip);
                        chip.Sample = MakeSample(Lang.English ? "Hello" : "你好", all[i].Suffix);
                        chip.Dock = DockStyle.Fill;
                        chip.Margin = new Padding(4);
                        int idx = i;
                        chip.Click += delegate { ApplyLookAt(idx); };
                        // ★ 只有自定义的（下标 >= 内置数量）才给删除 ✓
                        if (i >= (Lang.English ? LookStylesEn.Length : LookStyles.Length))
                        {
                            chip.Deletable = true;
                            chip.DeleteClicked += delegate
                            {
                                if (MessageBox.Show(this, "删掉「" + all[idx].Name + "」？",
                                        "删除自定义风格", MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Warning) != DialogResult.Yes) return;
                                if (RemoveCustomLook(all[idx].Name))
                                {
                                    app.Log("custom style removed: " + all[idx].Name);
                                    ReloadStyleChips();
                                }
                            };
                            chip.MouseUp += delegate(object s2, MouseEventArgs e)
                            {
                                if (e.Button != MouseButtons.Right) return;
                                ShowItemMenu(chip, new Point(0, chip.Height), all[idx].Name,
                                    "\u2606", Lang.T("删除这个自定义风格"), delegate
                                {
                                    if (MessageBox.Show(this, Lang.T("删掉「") + all[idx].Name + Lang.T("」？"),
                                            Lang.T("删除自定义风格"), MessageBoxButtons.YesNo,
                                            MessageBoxIcon.Warning) != DialogResult.Yes) return;
                                    if (RemoveCustomLook(all[idx].Name))
                                    {
                                        app.Log("custom style removed: " + all[idx].Name);
                                        ReloadStyleChips();
                                    }
                                });
                            };
                        }
                        g.Controls.Add(chip, i % 3, i / 3);
                        pageChips[i] = chip;
                    }
                }
                finally { g.ResumeLayout(true); }

                SyncChips();
                RefreshRail();
            }
            catch (Exception ex) { app.Log("ReloadStyleChips failed: " + ex.Message); }
        }

        void RefillKaomojiPage()
        {
            if (kaoListBody == null) { FillKaomojiPage(); return; }
            kaoListBody.SuspendLayout();
            try { FillKaomojiPage(); }
            finally
            {
                kaoListBody.ResumeLayout(false);
            }
        }

        void FillKaomojiPage()
        {
            if (kaoListBody == null || app.Cfg == null) return;

            // ★★★ 内容没变就不重建 ★★★
            // ★★★ 只有【用户正看着这一页】时才重建 ★★★
            //   重建要 Clear + 几十个 Add ✗ 不在这一页时纯属浪费 ✓
            //   切回这一页时 ShowPage 会重填 ✗ 不会漏 ✓
            //   —— 对照旧版：那边点风格根本不碰这个列表 ✓
            if (kaoListBody.Parent == null || !kaoListBody.Visible) return;
            kaoListBody.Controls.Clear();

            if (app.Cfg.UiKao)
            {
            // ---- 试验：胶囊网格（UI_KAO）-------------------------------------------
            // Was one row per entry plus a 删除 button - a table with a lot of empty space to
            // the right of every short kaomoji. Capsules pack several per row, and the row of
            // delete buttons becomes a right-click on the capsule itself.
            //
            // Left click copies the kaomoji, which is what a person actually wants from this
            // list; the old layout made deleting the only thing you could do with one.
            ToolTip tip = new ToolTip();
            tip.AutoPopDelay = 4000;
            tip.InitialDelay = 350;
            tip.ReshowDelay = 100;

            int x = 0, y = 0;
            int rowH = 38;
            int maxW = Math.Max(240, kaoListBody.ClientSize.Width - 12);
            foreach (string k in app.Cfg.Kaomoji)
            {
                RoundButton cap = new RoundButton();
                // \u2605 2026-10-18\uff1a\u6536\u85cf\u8fc7\u7684\u52a0\u4e2a \u2605 \u2727 \u4e00\u773c\u770b\u51fa\u6765 \u2713
                bool isFav = app.Cfg.FavoriteKao.Contains(k);
                cap.Text = isFav ? "\u2605 " + k : k;
                cap.Font = Theme.Body;
                Size measured = TextRenderer.MeasureText(k, Theme.Body);
                cap.Size = new Size(Math.Max(84, measured.Width + 30), 30);
                if (x > 0 && x + cap.Width > maxW) { x = 0; y += rowH; }
                cap.Location = new Point(x, y);

                string target = k;
                string label = k;
                cap.Click += delegate
                {
                    try { Clipboard.SetText(target); } catch { }
                    cap.Text = Lang.T("已复制 ✓");
                    System.Windows.Forms.Timer back = new System.Windows.Forms.Timer();
                    back.Interval = 1100;
                    back.Tick += delegate
                    {
                        back.Stop();
                        back.Dispose();
                        cap.Text = label;
                    };
                    back.Start();
                };
                // \u2605 2026-10-18\uff1a\u53F3\u952E\u6539\u6210\u83DC\u5355 \u2727 \u6536\u85CF / \u5220\u9664\uff08\u539F\u6765\u53EA\u80FD\u5220\uff09\u2713
                cap.MouseUp += delegate(object s3, MouseEventArgs e)
                {
                    if (e.Button != MouseButtons.Right) return;
                    ContextMenuStrip cm = new ContextMenuStrip();
                    bool fav = app.Cfg.FavoriteKao.Contains(target);
                    ToolStripMenuItem miFav = new ToolStripMenuItem(
                        fav ? Lang.T("\u53D6\u6D88\u6536\u85CF") : Lang.T("\u6536\u85CF\u8FD9\u4E2A"));
                    miFav.Click += delegate { ToggleFavoriteKaomoji(target); };
                    cm.Items.Add(miFav);
                    ToolStripMenuItem miDel = new ToolStripMenuItem(Lang.T("\u5220\u9664"));
                    miDel.Click += delegate { RemoveKaomoji(target); };
                    cm.Items.Add(miDel);
                    cm.Show(cap, new Point(0, cap.Height));
                };
                tip.SetToolTip(cap, k + Lang.T("　左键复制 · 右键删除"));
                kaoListBody.Controls.Add(cap);

                x += cap.Width + 8;
            }
            return;
            }

            int ly = 0;
            foreach (string k in app.Cfg.Kaomoji)
            {
                Label l = new Label();
                l.Text = k;
                l.Font = Theme.Body;
                l.ForeColor = Theme.Accent;
                l.BackColor = Theme.Card;
                l.AutoSize = false;
                l.TextAlign = ContentAlignment.MiddleLeft;
                l.Location = new Point(0, ly);
                l.Size = new Size(300, 28);
                kaoListBody.Controls.Add(l);

                RoundButton del = new RoundButton();
                del.Text = Lang.T("删除");
                del.Size = new Size(64, 24);
                del.Location = new Point(310, ly + 2);
                del.AccessibleName = k;
                string target = k;
                del.Click += delegate { RemoveKaomoji(target); };
                kaoListBody.Controls.Add(del);

                ly += 32;
            }
        }

        void AddKaomoji()
        {
            string v = kaoAddBox == null ? "" : kaoAddBox.Text.Trim();
            if (v.Length == 0) return;

            List<string> list = new List<string>();
            foreach (string k in app.Cfg.Kaomoji) list.Add(k);
            if (list.Contains(v))
            {
                MessageBox.Show(this, Lang.T("这个颜文字已经在列表里了。"), Lang.T("喵化助手"));
                return;
            }
            list.Add(v);

            Dictionary<string, string[]> p = new Dictionary<string, string[]>();
            p["\u989C\u6587\u5B57"] = list.ToArray();
            if (!app.PatchRules(p)) return;

            kaoAddBox.Text = "";
            RefillKaomojiPage();
            RefreshRail();
            UpdatePreview();
        }

        void RemoveKaomoji(string k)
        {
            List<string> list = new List<string>();
            foreach (string x in app.Cfg.Kaomoji) if (x != k) list.Add(x);
            if (list.Count == 0)
            {
                MessageBox.Show(this, Lang.T("至少要留一个颜文字。"), Lang.T("喵化助手"));
                return;
            }

            Dictionary<string, string[]> p = new Dictionary<string, string[]>();
            p["\u989C\u6587\u5B57"] = list.ToArray();
            if (!app.PatchRules(p)) return;

            RefillKaomojiPage();
            RefreshRail();
            UpdatePreview();
        }

        // ---- settings page -------------------------------------------------

        /// ★★★ 展开 / 收起模型列表 ★★★
        void ToggleModelMenu(Control host, RoundButton btn)
        {
            if (modelMenu != null) { CloseModelMenu(host); return; }

            string[] all = app.AvailableModels().ToArray();
            int rowH = 32;
            ModelMenuPanel p = new ModelMenuPanel();
            p.Radius = 10;
            p.Width = Math.Max(300, btn.Width);   // ★ 加宽到 300 ✧ 文件名不用悬停就看得全 ✓
            p.Height = rowH * (all.Length + 1) + 16;
            // ★★★ 下面不够就翻到上面 ★★★
            //   用户 2026-10-17：页面滚到底时 ✧ 展开框被裁掉了 ✗
            //   做法：用屏幕坐标算下面还有多少空间 ✧
            //         不够就放在按钮上面 ✓
            int below = 9999;
            try
            {
                Point under = btn.PointToScreen(new Point(0, btn.Height + 3));
                below = Screen.FromControl(btn).WorkingArea.Bottom - under.Y;
            }
            catch { }
            if (p.Height > below)
                p.Location = new Point(btn.Left, Math.Max(0, btn.Top - p.Height - 6));   // 往上展开 ✓
            else
                p.Location = new Point(btn.Left, btn.Top + btn.Height + 6);

            int y = 7;
            p.Controls.Add(ModelRow(p, Lang.T("自动（用最大的）"),
                                    pickedModelFile.Length == 0, y, rowH, ""));
            y += rowH;
            foreach (string f in all)
            {
                string file = f;
                p.Controls.Add(ModelRow(p, f,      // ★ 用户 2026-10-17：列表里显示【文件名】✓
                                        string.Equals(pickedModelFile, f, StringComparison.OrdinalIgnoreCase),
                                        y, rowH, file));
                y += rowH;
            }

            host.Controls.Add(p);
            p.BringToFront();
            modelMenu = p;

            if (all.Length == 0 && llmStatus != null)
            {
                llmStatus.ForeColor = Theme.Warn;
            llmStatus.Text = Lang.T("模型文件夹里还没有 .gguf —— 把下载的模型丢进「电脑版/模型/」就行");
            }
        }

        void CloseModelMenu(Control host)
        {
            if (modelMenu == null) return;
            try { host.Controls.Remove(modelMenu); modelMenu.Dispose(); } catch { }
            modelMenu = null;
        }

        /// ★ 列表里的一行 ✧ file 为空 = 「自动」那一行 ✓
        Control ModelRow(Control parent, string text, bool current, int y, int rowH, string file)
        {
            Label lb = new Label();
            lb.Text = (current ? "\u2713  " : "    ") + text;
            lb.Font = Theme.Small;
            lb.ForeColor = current ? Theme.Accent : Theme.Text;
            lb.BackColor = Theme.Card;          // ★ 跟弹出面板的白底一致 ✓
            lb.AutoSize = false;
            lb.AutoEllipsis = true;             // ★ 太长了显示… ✧ 不会变空白 ✓
            lb.UseCompatibleTextRendering = false;
            lb.TextAlign = ContentAlignment.MiddleLeft;
            lb.Location = new Point(8, y);
            lb.Size = new Size(parent.Width - 18, rowH);
            lb.Cursor = Cursors.Hand;
            // ★ 鼠标移上去变淡粉 ✧ 移开变回白 ✓
            lb.MouseEnter += delegate { lb.BackColor = Theme.Soft; };
            lb.MouseLeave += delegate { lb.BackColor = Theme.Card; };
            string arg = file;
            lb.Click += delegate
            {
                pickedModelFile = arg;
                // ★★★ 选了就【立刻生效】✗ 不用再点"保存规则" ★★★
                //   用户 2026-10-17 踩的坑：选了 3B ✗ 但没点保存 ✗
                //   规则里没有「模型文件」这一行 ✗ 于是照样加载 7B ✓
                try
                {
                    app.Cfg.LlmModelFile = arg;
                    System.Collections.Generic.Dictionary<string, string[]> v =
                        new System.Collections.Generic.Dictionary<string, string[]>();
                    v["\u6A21\u578B\u6587\u4EF6"] = arg.Length > 0
                        ? new string[] { arg } : new string[0];
                    app.PatchRules(v);
                }
                catch { }
                UpdateModelFileButton();
                CloseModelMenu(parent.Parent == null ? parent : parent.Parent);
            };
            return lb;
        }

        /// ★ 从文件名里拿出简称（qwen2.5-7b-... → 7B）✓
        string ShortModelName(string f)
        {
            string t = f;
            if (t.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(0, t.Length - 5);
            try
            {
                System.Text.RegularExpressions.Match mm =
                    System.Text.RegularExpressions.Regex.Match(t, @"(\d+(?:\.\d+)?)[bB]");
                if (mm.Success) return mm.Groups[1].Value + "B";
            }
            catch { }
            return t.Length > 16 ? t.Substring(0, 16) + "\u2026" : t;
        }

        /// ★ 状态行后面挂的那一小截：「　·　当前模型：7B」✓
        ///   优先用【真的加载过的那个】✗ 没记录就按 BundledModelPath 推 ✓
        ///   （外部 LM Studio 在跑时推出来的可能不准 ✗ 但比空着有用 ✓）
        string ModelNameSuffix()
        {
            string name = app.RunningModelName;
            if (string.IsNullOrEmpty(name))
            {
                try { name = App.ShortModel(app.BundledModelPath()); } catch { }
            }
            if (string.IsNullOrEmpty(name)) return "";
            return Lang.T("　·　当前模型：") + name;
        }

        /// ★ 刷新「模型」那个按钮的文字 ✓
        ///   空 = 自动（用最大的）✧ 否则显示简称（3B / 7B 这样）✓
        void UpdateModelFileButton()
        {
            if (setModelFile == null) return;
            if (string.IsNullOrEmpty(pickedModelFile))
            {
                setModelFile.Text = Lang.T("\u6A21\u578B\uFF1A\u81EA\u52A8") + "  \u25BE";
            }
            else
            {
                string f = pickedModelFile;
                if (f.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    f = f.Substring(0, f.Length - 5);
                // ★ 用户 2026-10-17：按钮上也显示文件名 ✧ 太长了才截断 ✓
                string showName = f;
                if (showName.Length > 22) showName = showName.Substring(0, 22) + "\u2026";
                setModelFile.Text = Lang.T("\u6A21\u578B\uFF1A") + showName + "  \u25BE";
            }
        }

        TextBox SettingRow(Control host, string caption, string hint, string value, int y, int width)
        {
            Label cap = new Label();
            cap.Text = caption;
            cap.Font = Theme.Small;
            cap.ForeColor = Theme.Text;
            cap.BackColor = Theme.Card;
            cap.AutoSize = false;
            cap.TextAlign = ContentAlignment.MiddleLeft;
            cap.Location = new Point(0, y);
            cap.Size = new Size(120, 26);
            host.Controls.Add(cap);

            TextBox box = new TextBox();
            box.Font = Theme.Body;
            box.Text = value;
            box.Location = new Point(124, y + 1);
            box.Size = new Size(width, 24);
            host.Controls.Add(box);

            if (hint.Length > 0)
            {
                Label h = new Label();
                h.Text = hint;
                h.Font = Theme.Tiny;
                h.ForeColor = Theme.Muted;
                h.BackColor = Theme.Card;
                h.AutoSize = false;
                h.TextAlign = ContentAlignment.MiddleLeft;
                h.Location = new Point(124 + width + 12, y);
                h.Size = new Size(420, 26);
                host.Controls.Add(h);
            }
            return box;
        }

        Panel BuildSettingsPage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(22, 16, 10, 16);
            // 滚动交给一个【普通 Panel】，不要用 CardPanel 自己滚。
            //
            // CardPanel 是自绘控件（UserPaint），而且故意不清除背景（为的是让壁纸从圆角透
            // 出来）。这类控件滚动时 Windows 把像素整块搬走，搬走的内容不会再经过任何绘制
            // 流程，于是原地留下一条条横影 —— 拖一下滚动条就看到了。
            // 普通 Panel 会正常擦除自己，所以套一层是这里唯一可靠的结构。
            // Invalidate(true) 治不了：问题不在"忘了重画子控件"，而在这类控件根本不该滚。
            card.AutoScroll = false;
            SmoothPanel scroller = new SmoothPanel();
            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.BackColor = Theme.Card;
            card.Controls.Add(scroller);

            Panel host = new Panel();
            host.BackColor = Theme.Card;
            host.Dock = DockStyle.Top;
            // 900, not 640: the 界面 switch block runs to y=840 and a 640-tall host simply
            // clipped it - the controls existed but could not be seen or clicked. The card
            // scrolls, so anything past the viewport is still reachable.
            host.Height = 1540;   // ★ 又加了大模型改写一段 ✓
            scroller.Controls.Add(host);

            Label title = new Label();
            title.Text = Lang.T("设置");
            title.Font = Theme.Title;
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Card;
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Location = new Point(0, 0);
            title.Size = new Size(800, 32);
            host.Controls.Add(title);
                FitToHost(title, host);

            PageSection(host, Lang.T("全局快捷键"), 38);
            setHotkey1 = SettingRow(host, Lang.T("立即转换"), "", app.Cfg.HotkeyTransform, 90, 160);
            setHotkey2 = SettingRow(host, Lang.T("暂停 / 恢复"), "", app.Cfg.HotkeyPause, 122, 160);
            setHotkey3 = SettingRow(host, Lang.T("重载规则"), "", app.Cfg.HotkeyReload, 154, 160);

            PageSection(host, Lang.T("自动模式"), 194);
            setDebounce = SettingRow(host, Lang.T("停止打字后"), Lang.T("毫秒。300 左右比较跟手，打字慢的话调到 600~900。"),
                                     app.Cfg.DebounceMs.ToString(), 220, 80);
            setWhitelist = SettingRow(host, Lang.T("生效程序"), Lang.T("进程名，不要带 .exe。多个用逗号或空格分开。"),
                                      string.Join(", ", app.Cfg.Whitelist.ToArray()), 252, 260);

            // ★ 图片保护开关
            //   用和页面底部那些开关一样的 ToggleRow ✗ 外观统一 ✓
            // ★ 这里【不能】用 NewCheck()：它内部设了 Dock = Fill ✗
            //   而这一页是绝对定位 ✗ 两者一起用会让这个控件占满整张卡片 ✗
            //   背景色把后面所有控件全盖住 —— 表现就是"设置页一片空白"✓
            picGuardChk = new ToggleRow();
            picGuardChk.Text = Lang.T("图片保护 —— 有图片就不转换");
            picGuardChk.Checked = app.Cfg.ImageGuard;
            picGuardChk.Location = new Point(0, 280);
            picGuardChk.Size = new Size(430, 28);
            picGuardChk.CheckedChanged += delegate
            {
                if (loading) return;      // 载入界面时不要触发保存
                app.Cfg.ImageGuard = picGuardChk.Checked;
                Dictionary<string, string[]> kk = new Dictionary<string, string[]>();
                kk["\u56FE\u7247\u4FDD\u62A4"] = new string[] { picGuardChk.Checked ? "\u5F00" : "\u5173" };
                app.PatchRules(kk);
                app.Log(Lang.T("图片保护 = ") + (picGuardChk.Checked ? Lang.T("开") : Lang.T("关"))
                        + (picGuardChk.Checked ? Lang.T("（输入框里有图片就不转换）") : Lang.T("（照样转换，图片会被弄没）")));
            };
            host.Controls.Add(picGuardChk);

            RoundButton save = new RoundButton();
            save.Text = Lang.T("保存设置");
            save.Primary = true;
            save.Size = new Size(120, 34);
            save.Location = new Point(0, 356);
            save.Click += delegate { SaveSettings(); };
            host.Controls.Add(save);

            RoundButton open = new RoundButton();
            open.Text = Lang.T("用记事本打开规则");
            open.Size = new Size(150, 34);
            open.Location = new Point(132, 356);
            open.Click += delegate { app.OpenRulesInNotepad(); };
            host.Controls.Add(open);

            Label foot = new Label();
            foot.Text = Lang.T("这些设置就是 rules.txt 里对应的几行，保存后文件里也能看到、能直接改。");
            foot.Font = Theme.Tiny;
            foot.ForeColor = Theme.Muted;
            foot.BackColor = Theme.Card;
            foot.AutoSize = false;
            foot.TextAlign = ContentAlignment.MiddleLeft;
            foot.Location = new Point(0, 396);
            foot.Size = new Size(700, 22);
            host.Controls.Add(foot);

            // ---- 界面: the four look options, each switchable ----------------------
            //
            // These were compile-time switches at first, which meant a separate build for
            // every combination. Three people wanted three different sets, so they became
            // settings: one build, each person turns off what they do not want.
            PageSection(host, Lang.T("界面"), 696);
            PageNote(host, Lang.T("四项美化可以单独关掉，关掉哪项、下次打开程序就不显示哪项。") +
                           Lang.T("它们只是外观，不影响转换结果。"), 722);

            setUiTopBar = new ToggleRow();
            setUiTopBar.Text = Lang.T("顶部猫娘状态气泡");
            setUiTopBar.Width = 380;
            setUiTopBar.Location = new Point(0, 752);
            host.Controls.Add(setUiTopBar);

            setUiCards = new ToggleRow();
            setUiCards.Text = Lang.T("运行状态四个数字卡");
            setUiCards.Width = 380;
            setUiCards.Location = new Point(0, 784);
            host.Controls.Add(setUiCards);

            setUiLab = new ToggleRow();
            setUiLab.Text = Lang.T("实验室强化（中间猫娘、粉底结果框、大按钮）");
            setUiLab.Width = 470;
            setUiLab.Location = new Point(0, 816);
            host.Controls.Add(setUiLab);

            setUiKao = new ToggleRow();
            setUiKao.Text = Lang.T("颜文字库胶囊网格");
            setUiKao.Width = 380;
            setUiKao.Location = new Point(0, 848);
            host.Controls.Add(setUiKao);

            // 第二批：两列排布，11 个开关不至于把页面拉得太长
            setUiNavIcon = UiToggle(host, Lang.T("导航猫系图标"),           "界面-导航图标",  0,   878);
            setUiLevel2  = UiToggle(host, Lang.T("升级时提示"),             "界面-升级提示",  310, 878);
            setUiColor   = UiToggle(host, Lang.T("颜色分层（降低饱和度）"), "界面-颜色分层",  0,   910);
            setUiPaw     = UiToggle(host, Lang.T("卡片角落淡猫爪"),         "界面-猫爪装饰",  310, 910);
            setUiMood    = UiToggle(host, Lang.T("猫娘台词随次数变化"),     "界面-台词变化",  0,   942);
            setUiAnim    = UiToggle(host, Lang.T("动画与过渡"),             "界面-动画",      310, 942);
            // Its own save button, right under the switches.
            //
            // The page has one 保存设置 button and it sits ~500px further up, next to the
            // hotkeys - so after ticking these boxes there is nothing in view to press, and
            // "who would think to scroll back up?" is exactly right. Same code path, so the
            // two buttons cannot drift apart.
            RoundButton saveUi = new RoundButton();
            saveUi.Text = Lang.T("保存界面设置");
            saveUi.Primary = true;
            saveUi.Size = new Size(136, 34);
            // 动画时长放在六个开关下面。加这一行把保存按钮和高度一起往下让，
            // 否则会和输入框重叠（之前加开关时踩过同样的坑）。
            setAnimMs = SettingRow(host, Lang.T("动画时长"),
                                   Lang.T("毫秒。页面淡入用多久。400 是原来写死的值，越长越容易看清。"),
                                   app.Cfg.UiAnimMs.ToString(), 982, 80);

            setSlidePct = SettingRow(host, Lang.T("滑块比例"),
                                     Lang.T("百分比。100 = 滑块和淡化一样长（默认）；53 = 滑块先到位（最早的手感）；150 = 滑块更慢。"),
                                     app.Cfg.UiSlidePct.ToString(), 1026, 80);

            saveUi.Location = new Point(0, 1098);
            saveUi.Click += delegate { SaveSettings(); };
            host.Controls.Add(saveUi);

            Label uiHint = new Label();
            uiHint.Text = Lang.T("改完点这个保存，下次打开程序生效。上面那个「保存设置」是同一件事。");
            uiHint.Font = Theme.Tiny;
            uiHint.ForeColor = Theme.Muted;
            uiHint.BackColor = Theme.Card;
            uiHint.AutoSize = false;
            uiHint.TextAlign = ContentAlignment.MiddleLeft;
            uiHint.Location = new Point(148, 1106);   // ★ 跟着保存按钮走 ✗ 按钮在 1162 ✓
            // ★ 2026-10-18：不能调 FitToHost ✗
            //   它按「构建当时容器宽度」算 ✧ 那时 host 才 168 宽 ✗
            //   600 被缩成了 20px ✗ 文字只剩「改完点这个保存」✓
            uiHint.Size = new Size(600, 22);
            uiHint.Left = 148;
            host.Controls.Add(uiHint);

            // ★ 2026-10-18：配置导出 / 导入 ✧ 不只在托盘菜单里了 ✓
            RoundButton btnExport = new RoundButton();
            btnExport.Text = Lang.T("导出配置");
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Lang.Apply 能翻过去 ✓
            btnExport.Size = new Size(150, 34);
            btnExport.Location = new Point(0, 1148);
            btnExport.Click += delegate { app.ExportConfig(); };
            host.Controls.Add(btnExport);

            RoundButton btnImport = new RoundButton();
            btnImport.Text = Lang.T("导入配置");
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Lang.Apply 能翻过去 ✓

            // ★ 2026-10-18：按软件分配风格 ✗ 用户找不到入口 ✗ 放到这一行第三个 ✓
            RoundButton btnProcStyle = new RoundButton();
            btnProcStyle.Text = Lang.T("按软件分配…");
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Lang.Apply 能翻过去 ✓
            btnProcStyle.Size = new Size(150, 30);
            btnProcStyle.Location = new Point(0, 316);   // ★ 2026-10-18：放在「图片保护」下面 ✓
            btnProcStyle.Click += delegate { EditProcStyles(); };
            host.Controls.Add(btnProcStyle);
            // \u2605 \u6309\u8f6f\u4ef6\u5206\u914d\u7684\u8bf4\u660e \u2727 \u8ddf\u7740\u6309\u94ae\u8d70 \u2713
            Label procHint = new Label();
            procHint.Text = Lang.T("\u5728\u6307\u5b9a\u7a0b\u5e8f\u91cc\u7528\u6307\u5b9a\u98ce\u683c\uff0c\u4e5f\u80fd\u5355\u72ec\u5173\u81ea\u52a8\u8f6c\u6362");
            procHint.Font = Theme.Tiny;
            procHint.ForeColor = Theme.Muted;
            procHint.BackColor = Theme.Card;
            procHint.AutoSize = false;
            procHint.TextAlign = ContentAlignment.MiddleLeft;
            procHint.Location = new Point(160, 322);
            procHint.Size = new Size(420, 16);
            // ★ 2026-10-18：去掉 NoTranslate ✗ 让 Lang.Apply 能翻过去 ✓
            host.Controls.Add(procHint);
            btnImport.Size = new Size(150, 34);
            btnImport.Location = new Point(162, 1148);
            btnImport.Click += delegate { app.ImportConfig(); };
            host.Controls.Add(btnImport);

            PageNote(host, Lang.T("导出 = 把规则和风格打成一个 zip；导入 = 选 zip 还原（会覆盖当前的）。"), 1192);

            // Clamp every direct child to what the card actually gives.
            //
            // Several rows were built 698-704 wide and the card only offers ~634, so the card
            // grew a horizontal scrollbar and scrolling it pushed the left of the text off
            // screen - a user saw "重载规则" render as "载规则" and reported it as a broken
            // border. Hunting every hard-coded size one at a time missed some twice; one pass
            // here cannot, and it also covers rows added later.
            // Each child remembers the width it was BUILT with, and is only ever narrowed to
            // fit - never widened again.
            //
            // The first version shrank to whatever the host happened to be at the time. During
            // construction the host is still narrow, so the four 界面 switches were cut from
            // 380 to ~94 and never grew back when the host reached its real width: their labels
            // rendered as "顶部猫娘状…". Min() against the original width fixes that, and it
            // still cannot overflow.
            foreach (Control c in host.Controls)
                if (c.Tag == null) c.Tag = c.Width;
            EventHandler clamp = delegate
            {
                int avail = host.ClientSize.Width;
                if (avail <= 40) return;
                foreach (Control c in host.Controls)
                {
                    if (!(c.Tag is int)) continue;
                    int built = (int)c.Tag;
                    int room = avail - c.Left;
                    if (room < 40) room = 40;
                    int want = built < room ? built : room;
                    if (c.Width != want) c.Width = want;
                }
            };
            host.Resize += clamp;
            clamp(null, EventArgs.Empty);

            // ---- 智能语气: a model running on THIS machine decides the mood ----------
            PageSection(host, Lang.T("智能语气（用本机大模型判断情绪）"), 438);
            // ★ 用户 2026-10-17：这句在 22px 里会折成两行被裁掉 ✗ 缩短成一行 ✓
            PageNote(host, Lang.T("只连本机、不连外网。模型只判断情绪，加的词还是你自己配的。"), 464);

            setSmartTone = new ToggleRow();
            setSmartTone.Text = Lang.T("开启智能语气");
            setSmartTone.Width = 320;
            setSmartTone.Location = new Point(0, 492);
            host.Controls.Add(setSmartTone);

            setEndpoint = SettingRow(host, Lang.T("模型地址"), "LM Studio: http://127.0.0.1:1234　Ollama: http://127.0.0.1:11434　带 /v1 也行",
                                     app.Cfg.LlmEndpoint, 530, 240);
            setModel = SettingRow(host, Lang.T("模型名"), Lang.T("LM Studio 里已加载的模型标识；Ollama 填 qwen2.5:3b 这样的名字。留空用服务器上的第一个"),
                                  app.Cfg.LlmModel, 562, 240);

            RoundButton testBtn = new RoundButton();
            testBtn.Text = Lang.T("测试连接");
            testBtn.Primary = true;
            testBtn.Size = new Size(96, 32);
            testBtn.Location = new Point(0, 600);
            testBtn.Click += delegate { TestLlm(); };
            host.Controls.Add(testBtn);

            // Never disabled.
            //
            // It used to be Enabled = (a bundled runtime exists), which meant that in a folder
            // without the model the button could not be pressed AND said nothing about why -
            // it just looked broken. Pressing it now always produces an answer, including "this
            // folder has no bundled model, here is what to do instead".
            // ★ 模型选择 ✧ 塞在「启动内置模型」右边那块空地方 ✓
            setModelFile = new RoundButton();
            setModelFile.Size = new Size(192, 32);   // ★ 150 → 192，不截断了 ✓
            setModelFile.Location = new Point(240, 600);
            setModelFile.Click += delegate { ToggleModelMenu(host, setModelFile); };

            // ★ 「自动加载」开关 ✧ 用户 2026-10-17 要的：默认不自动启动 ✓
            setModelAuto = new ToggleRow();
            setModelAuto.Text = Lang.T("自动加载");
            setModelAuto.Width = 190;
            // ★ 位置说明：模型名输入框是 x124~364 ✧ y502~527 ✓
            //   所以 x=440 y=506 不会碰到它 ✧ 下面那个 546 也不碰按钮行 ✓
            setModelAuto.Location = new Point(440, 600);   // ★ 按钮行右边（按钮到 432 为止 ✓）

            // ★ 语气小模型 ✧ 用户 2026-10-17：安卓那个电脑上也要有 ✓
            //   只要把 clf.bin 放进【模型】文件夹就自动认识 ✧ 7.3 MB ✓
            setTinyMood = new ToggleRow();
            setTinyMood.Text = Lang.T("\u5c0f\u6a21\u578b\u8bed\u6c14");
            setTinyMood.Width = 190;
            setTinyMood.Location = new Point(0, 638);     // ★ 左边一整条都是空的 ✓
            setTinyMood.Checked = app.Cfg.TinyMood;
            host.Controls.Add(setTinyMood);
            setModelAuto.Checked = app.Cfg.ModelAutoStart;
            host.Controls.Add(setModelAuto);
            host.Controls.Add(setModelFile);

            RoundButton bootBtn = new RoundButton();
            bootBtn.Text = Lang.T("启动内置模型");
            bootBtn.Size = new Size(124, 32);
            bootBtn.Location = new Point(104, 600);
            bootBtn.Click += delegate
            {
                if (app.ModelServerRunning())
                {
                    // ★ 已经有一个在跑 ✗ 但可能不是用户刚选的那个 ✓
                    //   用户 2026-10-17：选了 3B ✗ 状态仍显示 7B（因为旧的没停 ✓）
                    string want = App.ShortModel(
                        app.Cfg.LlmModelFile.Length > 0 ? app.Cfg.LlmModelFile : app.BundledModelPath());
                    bool diff = app.RunningModelName.Length > 0 && want.Length > 0
                                && !string.Equals(app.RunningModelName, want, StringComparison.OrdinalIgnoreCase);
                    if (diff)
                    {
                        app.StopModelServer();
                        System.Threading.Thread.Sleep(500);
                        app.EnsureModelServerAsync();
                        llmStatus.ForeColor = Theme.Muted;
                        llmStatus.Text = Lang.T("正在从 ") + app.RunningModelName
                                       + Lang.T(" 换成 ") + want + Lang.T(" …（十几秒）");
                        app.RunningModelName = "";
                        return;
                    }
                    llmStatus.ForeColor = Theme.Ok;
                    llmStatus.Text = Lang.T("模型已经在运行了 ✓　地址 ") + app.Cfg.LlmEndpoint
                                   + ModelNameSuffix();
                    return;
                }
                if (app.BundledServerPath() == null)
                {
                    llmStatus.ForeColor = Theme.Warn;
                    llmStatus.Text = Lang.T("这个文件夹里没有带模型（缺 llama-server.exe）。")
                                   + Lang.T("可以用带模型的整包，或者自己开 LM Studio 的本地服务。");
                    if (!app.Cfg.SmartTone)
                        llmStatus.Text += Lang.T("　（智能语气目前是关的）");
                    else
                        llmStatus.Text += Lang.T("　（智能语气开着，但没有模型时会退回关键字判断）");
                    return;
                }
                app.EnsureModelServerAsync();
                llmStatus.ForeColor = Theme.Muted;
                llmStatus.Text = Lang.T("正在后台启动内置模型…（第一次要加载约 2GB 模型，请稍等十几秒）");
            };
            host.Controls.Add(bootBtn);

            llmStatus = new Label();
            llmStatus.Font = Theme.Small;
            llmStatus.ForeColor = Theme.Muted;
            llmStatus.BackColor = Theme.Card;
            // Its own line, BELOW the two buttons.
            //
            // It used to sit at x=122, which is inside the 启动内置模型 button's box (104..228).
            // The button is added later so it painted over the start of the sentence, and only
            // the tail was readable - "少模型（缺 llama-server.exe）..." instead of the full
            // message. AutoSize + MaximumSize so a long explanation wraps instead of being cut.
            llmStatus.AutoSize = true;
            llmStatus.MaximumSize = new Size(860, 0);
            llmStatus.TextAlign = ContentAlignment.TopLeft;
            llmStatus.Location = new Point(0, 680);
            host.Controls.Add(llmStatus);

            // ================= ★★★ 2026-10-31：大模型改写 ★★★ =================
            PageSection(host, Lang.T("大模型改写（让模型直接写语气）"), 1240);

            setRewriteModeBtn = new RoundButton();
            setRewriteModeBtn.Size = new Size(168, 32);
            setRewriteModeBtn.Location = new Point(0, 1276);
            setRewriteModeBtn.Text = RewriteModeLabel(app.Cfg.RewriteMode);
            setRewriteModeBtn.Click += delegate { CycleRewriteMode(); };
            host.Controls.Add(setRewriteModeBtn);

            Label rwHint = new Label();
            rwHint.Text = Lang.T("关：用规则　整句：模型重写整句　AI：开头结尾现场写");
            rwHint.Font = Theme.Tiny;
            rwHint.ForeColor = Theme.Muted;
            rwHint.BackColor = Theme.Card;
            rwHint.AutoSize = false;
            rwHint.TextAlign = ContentAlignment.MiddleLeft;
            rwHint.Location = new Point(178, 1279);
            rwHint.Size = new Size(520, 26);
            host.Controls.Add(rwHint);

            setRewriteEp = SettingRow(host, Lang.T("接口地址"),
                Lang.T("https://api.deepseek.com"),
                app.Cfg.RewriteEndpoint, 1316, 300);
            setRewriteModel = SettingRow(host, Lang.T("模型名"),
                Lang.T("deepseek-chat"),
                app.Cfg.RewriteModel, 1348, 300);
            setRewriteKey = SettingRow(host, Lang.T("密钥"),
                Lang.T("本地服务不用填"),
                app.Cfg.RewriteKey, 1380, 300);
            if (setRewriteKey != null) setRewriteKey.UseSystemPasswordChar = true;

            RoundButton rwTest = new RoundButton();
            rwTest.Text = Lang.T("试一下");
            rwTest.Size = new Size(96, 30);
            rwTest.Location = new Point(0, 1416);
            rwTest.Click += delegate { TestRewrite(); };
            host.Controls.Add(rwTest);

            // ★ 2026-10-31：用户填完以为自动存了 ✗ 这里再放一个保存按钮 ✓
            RoundButton rwSave = new RoundButton();
            rwSave.Text = Lang.T("保存");
            rwSave.Primary = true;
            rwSave.Size = new Size(96, 30);
            rwSave.Location = new Point(108, 1416);
            rwSave.Click += delegate { SaveSettings(); };
            host.Controls.Add(rwSave);

            rewriteStatus = new Label();
            rewriteStatus.Font = Theme.Small;
            rewriteStatus.ForeColor = Theme.Muted;
            rewriteStatus.BackColor = Theme.Card;
            rewriteStatus.AutoSize = true;
            rewriteStatus.MaximumSize = new Size(860, 0);
            rewriteStatus.TextAlign = ContentAlignment.TopLeft;
            rewriteStatus.Location = new Point(0, 1452);
            host.Controls.Add(rewriteStatus);

            return card;
        }

        // ★★★ 用户 2026-10-17：点「测试连接」会启动 3B 模型然后卡住 ★★★
        //
        // 原来的代码：
        //   if (!ModelServerRunning() && BundledServerPath() != null) {
        //       EnsureModelServerAsync();
        //       for (int i = 0; i < 60; i++) { Thread.Sleep(1000); ...; DoEvents(); }
        //   }
        //   —— ★ 在【界面线程】上一秒一秒地等 ✧ 最多等 60 秒 ✗
        //   —— ★ DoEvents 让窗口还能重绘 ✧ 但你能点别的按钮 ✧
        //      于是在等待中途又跑一遍别的逻辑 ✧ 彻底乱套 ✗✗✗
        //
        // 现在：
        //   · 【不再启动模型】✧ 想用内置模型就点那个专门的按钮 ✓
        //   · 【纯异步】✧ 界面永远不等网络 ✓✓✓
        // ★★★ 2026-10-31：大模型改写的设置项 ★★★
        string RewriteModeLabel(string mode)
        {
            if (mode == "manual") return Lang.T("整句重写");
            if (mode == "auto") return Lang.T("整句重写（自动）");
            if (mode == "wrap") return Lang.T("AI 写开头结尾");
            return Lang.T("不用模型");
        }

        void CycleRewriteMode()
        {
            string m = app.Cfg.RewriteMode;
            if (m == "off") m = "manual";
            else if (m == "manual") m = "wrap";
            else m = "off";
            app.Cfg.RewriteMode = m;
            if (setRewriteModeBtn != null) setRewriteModeBtn.Text = RewriteModeLabel(m);
            if (rewriteStatus != null)
            {
                rewriteStatus.ForeColor = Theme.Muted;
                rewriteStatus.Text = (m == "off")
                    ? Lang.T("已关闭：照旧用规则装饰")
                    : Lang.T("记得点一下下面的保存（设置页最后那个）");
            }
        }

        void TestRewrite()
        {
            if (rewriteStatus == null || setRewriteEp == null) return;
            string ep = setRewriteEp.Text.Trim();
            if (ep.Length == 0)
            {
                rewriteStatus.ForeColor = Theme.Warn;
                rewriteStatus.Text = Lang.T("先填一个接口地址");
                return;
            }
            rewriteStatus.ForeColor = Theme.Muted;
            rewriteStatus.Text = Lang.T("正在问模型…");
            string md = setRewriteModel.Text.Trim();
            string ky = setRewriteKey.Text.Trim();
            int tmo = app.Cfg.RewriteTimeoutMs;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object o)
            {
                string res = null;
                try { res = Llm.Rewrite("你好", "嗯，你好喵~(´・ω・`)", ep, md, ky, tmo); }
                catch (Exception ex) { res = null; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (rewriteStatus == null) return;
                        if (string.IsNullOrEmpty(res))
                        {
                            rewriteStatus.ForeColor = Theme.Warn;
                            rewriteStatus.Text = Lang.T("没回应：") + Llm.LastError;
                        }
                        else
                        {
                            rewriteStatus.ForeColor = Theme.Ok;
                            rewriteStatus.Text = Lang.T("模型回了：") + res;
                        }
                    });
                }
                catch { }
            });
        }

        void TestLlm()
        {
            if (llmStatus == null) return;
            llmStatus.ForeColor = Theme.Muted;
            llmStatus.Text = Lang.T("正在问本机模型…（最多等 ") + app.Cfg.LlmTimeoutMs + Lang.T(" 毫秒）");
            llmStatus.Refresh();

            string ep = setEndpoint.Text.Trim();
            string mn = setModel.Text.Trim();
            int tmo = app.Cfg.LlmTimeoutMs;

            // ★ 丢到线程池 ✧ 问完了再回来改文字 ✓
            try
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate(object o)
                {
                    string mood = null;
                    string res = null;
                    try { res = Llm.Probe(ep, mn, tmo, out mood); }
                    catch (Exception ex) { res = ex.Message; }
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate
                        {
                            try
                            {
                                if (llmStatus == null) return;
                                if (res == "OK" && mood != null)
                                {
                                    llmStatus.ForeColor = Theme.Ok;
                                    llmStatus.Text = Lang.T("连接成功 ✓　「今天好开心啊哈哈」被判为：") +
                                                     MoodName(mood) + Lang.T("（缓存已清空，可直接用）")
                                                   + ModelNameSuffix();
                                    Llm.ClearCache();
                                }
                                else
                                {
                                    llmStatus.ForeColor = Theme.Warn;
                                    llmStatus.Text = Lang.T("连不上：") + (res == null ? "" : res)
                                                   + Lang.T("　—— 想用内置模型就点旁边那个「启动内置模型」✓");
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                llmStatus.ForeColor = Theme.Warn;
                llmStatus.Text = Lang.T("连不上：") + ex.Message;
            }
        }

        static string MoodName(string m)
        {
            if (m == "happy") return Lang.T("开心");
            if (m == "sad") return Lang.T("难过");
            if (m == "angry") return Lang.T("生气");
            if (m == "question") return Lang.T("疑问");
            return Lang.T("普通");
        }

        void ReloadSettingsPage()
        {
            if (setHotkey1 == null) return;
            setHotkey1.Text = app.Cfg.HotkeyTransform;
            setHotkey2.Text = app.Cfg.HotkeyPause;
            setHotkey3.Text = app.Cfg.HotkeyReload;
            setDebounce.Text = app.Cfg.DebounceMs.ToString();
            setWhitelist.Text = string.Join(", ", app.Cfg.Whitelist.ToArray());
            if (setSmartTone != null) setSmartTone.Checked = app.Cfg.SmartTone;
            // ★ 把当前选的模型同步到按钮上 ✓
            pickedModelFile = app.Cfg.LlmModelFile == null ? "" : app.Cfg.LlmModelFile;
            if (setModelAuto != null) setModelAuto.Checked = app.Cfg.ModelAutoStart;
            if (setTinyMood != null) setTinyMood.Checked = app.Cfg.TinyMood;
            UpdateModelFileButton();
            if (setUiTopBar != null) setUiTopBar.Checked = app.Cfg.UiTopBar;
            if (setUiCards  != null) setUiCards.Checked  = app.Cfg.UiCards;
            if (setUiLab    != null) setUiLab.Checked    = app.Cfg.UiLab;
            if (setUiKao    != null) setUiKao.Checked    = app.Cfg.UiKao;
                if (setAnimMs   != null) setAnimMs.Text        = app.Cfg.UiAnimMs.ToString();
                if (setSlidePct != null) setSlidePct.Text      = app.Cfg.UiSlidePct.ToString();
            if (setUiNavIcon != null) setUiNavIcon.Checked = app.Cfg.UiNavIcon;
            if (setUiLevel2  != null) setUiLevel2.Checked  = app.Cfg.UiLevel;
            if (setUiColor   != null) setUiColor.Checked   = app.Cfg.UiColor;
            if (setUiPaw     != null) setUiPaw.Checked     = app.Cfg.UiPaw;
            if (setUiMood    != null) setUiMood.Checked    = app.Cfg.UiMood;
            if (setUiAnim    != null) setUiAnim.Checked    = app.Cfg.UiAnim;
            if (setEndpoint != null) setEndpoint.Text = app.Cfg.LlmEndpoint;
            if (setModel != null) setModel.Text = app.Cfg.LlmModel;
            // ★ 2026-10-31：改写那几衅 ✓
            if (setRewriteModeBtn != null) setRewriteModeBtn.Text = RewriteModeLabel(app.Cfg.RewriteMode);
            if (setRewriteEp != null) setRewriteEp.Text = app.Cfg.RewriteEndpoint;
            if (setRewriteModel != null) setRewriteModel.Text = app.Cfg.RewriteModel;
            if (setRewriteKey != null) setRewriteKey.Text = app.Cfg.RewriteKey;

            // Say out loud whether 智能语气 is actually working. It falls back to the keyword
            // classifier whenever nothing answers on the endpoint, and doing that silently
            // makes the feature look broken rather than inactive.
            // ★★★ 用户 2026-10-17：点设置会卡 ✗ 就是这里 ★★★
            //
            // 原来这里【同步】调 app.ModelServerRunning() ✗
            // 它是一次真的 HTTP 请求（超时 700ms ✓）而且跑在界面线程上 ✓
            // 服务没起来时每次点设置都要等它超时 ✗ 于是一卡一卡 ✓
            //
            // 现在：先写"正在检查…" ✗ 然后丢到线程池去问 ✗ 问完再回来改文字 ✓
            //       界面永远不等网络 ✓✓✓
            if (llmStatus != null && app.Cfg.SmartTone)
            {
                llmStatus.ForeColor = Theme.Muted;
                llmStatus.Text = Lang.T("正在检查模型服务…");
                string ep = app.Cfg.LlmEndpoint;
                try
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate(object o)
                    {
                        bool up = false;
                        try { up = app.ModelServerRunning(); } catch { }
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate
                            {
                                try
                                {
                                    if (llmStatus == null || !app.Cfg.SmartTone) return;
                                    if (up)
                                    {
                                        llmStatus.ForeColor = Theme.Ok;
                                        llmStatus.Text = Lang.T("模型服务在运行 ✓　") + ep
                                                       + ModelNameSuffix();
                                    }
                                    else
                                    {
                                        llmStatus.ForeColor = Theme.Warn;
                                        llmStatus.Text = Lang.T("模型服务没在跑 ✗　程序暂时按关键字判断情绪（结果照常，只是不懂否定句）");
                                    }
                                }
                                catch { }
                            });
                        }
                        catch { }
                    });
                }
                catch { }
            }
        }

        /// <summary>Builds one 界面 switch and remembers the rules.txt key it writes.</summary>
        ToggleRow UiToggle(Control host, string label, string rulesKey, int x, int y)
        {
            ToggleRow r = new ToggleRow();
            r.Text = label;
            r.Width = 300;
            r.Location = new Point(x, y);
            r.AccessibleName = rulesKey;
            // 这里【不能】调 FitToHost。
            //
            // 它按"构建当时的容器宽度"算，而那时容器还没撑开（168 宽），于是 162 像素的宽度
            // 被写死 —— 我明明指定了 300。"颜色分层（降低饱和度）"要 200 左右，就变成了省略号。
            // 宽度这里给足，超宽由 BuildSettingsPage 末尾那次统一收口处理（它只缩不放，
            // 而且以"构建时的宽度"为准）。
            host.Controls.Add(r);
            return r;
        }

        void SaveSettings()
        {
            Dictionary<string, string[]> p = new Dictionary<string, string[]>();
            p["\u8F6C\u6362\u70ED\u952E"] = new string[] { setHotkey1.Text.Trim() };
            p["\u6682\u505C\u70ED\u952E"] = new string[] { setHotkey2.Text.Trim() };
            p["\u91CD\u8F7D\u70ED\u952E"] = new string[] { setHotkey3.Text.Trim() };

            int ms;
            if (int.TryParse(setDebounce.Text.Trim(), out ms) && ms >= 100)
                p["\u5EF6\u8FDF"] = new string[] { ms.ToString() };

            string[] procs = setWhitelist.Text.Split(
                new char[] { ',', '\uFF0C', ' ', '\t', '\u3001' }, StringSplitOptions.RemoveEmptyEntries);
            if (procs.Length > 0) p["\u7A0B\u5E8F"] = procs;

            if (setSmartTone != null)
                p["\u667A\u80FD\u8BED\u6C14"] = new string[] { setSmartTone.Checked ? "true" : "false" };

            // 界面-顶部气泡 / 界面-数字卡 / 界面-实验室 / 界面-颜文字胶囊
            if (setUiTopBar != null) p["\u754C\u9762-\u9876\u90E8\u6C14\u6CE1"] = new string[] { setUiTopBar.Checked ? "true" : "false" };
            if (setUiCards  != null) p["\u754C\u9762-\u6570\u5B57\u5361"] = new string[] { setUiCards.Checked ? "true" : "false" };
            if (setUiLab    != null) p["\u754C\u9762-\u5B9E\u9A8C\u5BA4"] = new string[] { setUiLab.Checked ? "true" : "false" };
            if (setUiKao    != null) p["\u754C\u9762-\u989C\u6587\u5B57\u80F6\u56CA"] = new string[] { setUiKao.Checked ? "true" : "false" };
            // 第二批：键名就存在控件的 AccessibleName 里，改列表时不会漏掉某一项
            foreach (ToggleRow r in new ToggleRow[] { setUiNavIcon, setUiLevel2,
                                                      setUiColor, setUiPaw, setUiMood, setUiAnim })
                if (r != null && !string.IsNullOrEmpty(r.AccessibleName))
                    p[r.AccessibleName] = new string[] { r.Checked ? "true" : "false" };
            // 动画时长：和手机版同一个范围（100 - 3000），非法值就当没填
            if (setAnimMs != null)
            {
                int ams;
                if (int.TryParse(setAnimMs.Text.Trim(), out ams))
                    p["\u754C\u9762-\u52A8\u753B\u65F6\u957F"] =
                        new string[] { Math.Max(100, Math.Min(3000, ams)).ToString() };
            }
            if (setSlidePct != null)
            {
                int sp;
                if (int.TryParse(setSlidePct.Text.Trim(), out sp))
                    p["界面-滑块比例"] = new string[] { Math.Max(10, Math.Min(300, sp)).ToString() };
            }
            if (setEndpoint != null && setEndpoint.Text.Trim().Length > 0)
                p["\u6A21\u578B\u5730\u5740"] = new string[] { setEndpoint.Text.Trim() };
            if (setModel != null)
            {
                string m = setModel.Text.Trim();
                p["\u6A21\u578B\u540D"] = m.Length > 0 ? new string[] { m } : new string[0];
            }

            // ★ 2026-10-31：大模型改写设置 ✓
            p["\u6539\u5199\u6A21\u5F0F"] = new string[] { app.Cfg.RewriteMode };
            if (setRewriteEp != null)
            {
                string v = setRewriteEp.Text.Trim();
                p["\u6539\u5199\u5730\u5740"] = v.Length > 0 ? new string[] { v } : new string[0];
            }
            if (setRewriteModel != null)
            {
                string v = setRewriteModel.Text.Trim();
                p["\u6539\u5199\u6A21\u578B"] = v.Length > 0 ? new string[] { v } : new string[0];
            }
            if (setRewriteKey != null)
            {
                string v = setRewriteKey.Text.Trim();
                p["\u6539\u5199\u5BC6\u94A5"] = v.Length > 0 ? new string[] { v } : new string[0];
            }
            // ★ 选哪个 gguf ✧ 留空 = 自动挑最大的 ✓
            if (setModelFile != null)
                p["\u6A21\u578B\u6587\u4EF6"] = pickedModelFile.Length > 0
                    ? new string[] { pickedModelFile } : new string[0];
            if (setModelAuto != null)
                p["\u6A21\u578B\u81EA\u542F"] = new string[] { setModelAuto.Checked ? "true" : "false" };
            if (setTinyMood != null)
                p["\u5c0f\u6a21\u578B\u8BED\u6C14"] = new string[] { setTinyMood.Checked ? "true" : "false" };

            if (!app.PatchRules(p)) return;
            Llm.ClearCache();

            ReloadSettingsPage();
            MessageBox.Show(this,
                Lang.T("已保存。\r\n\r\n快捷键如果改了，需要重开一次程序才会生效") +
                Lang.T("（Windows 的全局热键只能在启动时注册）。\r\n\r\n") +
                Lang.T("界面那四项也是重开程序后生效。"),
                Lang.T("喵化助手"));
        }

        // ---- about page ----------------------------------------------------

        // ------------------------------------------------------------ 更新公告

        /// Where 更新记录.txt lives, relative to the exe. Two layouts have to work: during
        /// development the exe sits in pc\ with the changelog one level up, and in the
        /// release it sits in 电脑版\ with the changelog one level up. Same answer both
        /// times, so one lookup covers it - but the exe's own folder is checked first in
        /// case someone copies the file next to the program.
        string[] NewsCandidates()
        {
            string b = app.BaseDir;
            return new string[]
            {
                // \u2605 2026-10-22\uff1a\u5148\u627e\u7cbe\u7b80\u7248\u516c\u544a \u2717 \u5b8c\u6574\u8bb0\u5f55\u592a\u957f\u4e86 \u2713
                Path.Combine(b, "\u66F4\u65B0\u516C\u544A.txt"),
                Path.Combine(b, "..", "\u66F4\u65B0\u516C\u544A.txt"),
                Path.Combine(b, "\u66F4\u65B0\u8BB0\u5F55.txt"),
                Path.Combine(b, "..", "\u66F4\u65B0\u8BB0\u5F55.txt"),
                Path.Combine(b, "\u4F7F\u7528\u8BF4\u660E\u4E66.html")
            };
        }

        Panel BuildNewsPage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(18, 14, 18, 16);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 1;
            t.RowCount = 3;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(t);

            Label title = new Label();
            title.Text = Lang.T("更新公告");
            title.Font = Theme.Title;
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Card;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.Dock = DockStyle.Fill;
            t.Controls.Add(title, 0, 0);

            Panel bar = new Panel();
            bar.BackColor = Theme.Card;
            bar.Dock = DockStyle.Fill;
            t.Controls.Add(bar, 0, 1);

            RoundButton open = new RoundButton();
            open.Text = Lang.T("用记事本打开");
            open.Size = new Size(120, 30);
            open.Location = new Point(0, 2);
            open.Click += delegate { OpenNewsInEditor(); };
            bar.Controls.Add(open);

            newsStatus = new Label();
            newsStatus.Font = Theme.Small;
            newsStatus.ForeColor = Theme.Muted;
            newsStatus.BackColor = Theme.Card;
            newsStatus.AutoSize = false;
            newsStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 142 而不是 130：按钮宽 120，原来只留 10 像素，窗口稍窄一点文字就贴到按钮上，
            // 看起来像被按钮压住了。留 22 像素。
            newsStatus.Location = new Point(142, 2);
            newsStatus.Size = new Size(600, 30);
            newsStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            bar.Controls.Add(newsStatus);

            // read-only, but selectable and scrollable: this is a document, not a form
            // 换成自绘的平滑文本视图。
            //
            // 原来是多行 TextBox，它的滚动按行跳、没有像素级位置，做不出平滑效果。
            // 代价是拖选没了，但 Ctrl+A / Ctrl+C / 右键"复制全文"都还在。
            newsBox = new SmoothTextView();
            newsBox.Dock = DockStyle.Fill;
            t.Controls.Add(newsBox, 0, 2);

            LoadNews();
            return card;
        }

        string FindNewsFile()
        {
            foreach (string p in NewsCandidates())
            {
                try { if (File.Exists(p)) return Path.GetFullPath(p); } catch { }
            }
            return null;
        }

        void LoadNews()
        {
            if (newsBox == null) return;
            string p = FindNewsFile();
            if (p == null)
            {
                newsBox.Body = Lang.T("找不到「更新记录.txt」。\r\n\r\n")
                             + Lang.T("它应该和程序放在同一个包里（电脑版文件夹的上一层）。\r\n")
                             + Lang.T("自己做一份也行——就是一个纯文本文件，怎么改都行。");
                newsStatus.Text = Lang.T("未找到文件");
                newsStatus.ForeColor = Theme.Warn;
                app.Log("news: 更新记录.txt not found next to the program");
                return;
            }
            try
            {
                int t0 = Environment.TickCount;
                string txt = File.ReadAllText(p, Encoding.UTF8);
                txt = txt.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                // \u2605\u2605\u2605 \u7528\u6237 2026-10-17\uff1a\u6253\u5f00\u66f4\u65b0\u516c\u544a\u4f1a\u5361 \u2605\u2605\u2605
                //   \u539f\u56e0\uff1a\u8fd9\u4e2a\u6587\u4ef6\u73b0\u5728 179142 \u5b57 \u2727 \u4e00\u6b21\u6027\u585e\u7ed9 SmoothTextView \u2727
                //         \u5b83\u8981\u6392\u7248\u5168\u90e8\u6587\u5b57 \u2727 \u9875\u9762\u5c31\u5361\u4f4f\u4e86 \u2717
                //   \u4fee\u6cd5\uff1a\u53ea\u53d6\u672c\u6587\u4ef6\u6700\u540e 20000 \u5b57 \u2727 \u800c\u4e14\u653e\u5230\u9875\u9762\u663e\u793a\u4e4b\u540e\u518d\u8bbe \u2713
                if (txt.Length > 20000)
                {
                    txt = Lang.T("\uff08\u53ea\u663e\u793a\u6700\u8fd1\u7684\u90e8\u5206\uff0c\u5b8c\u6574\u8bb0\u5f55\u8bf7\u7528\u300c\u7528\u8bb0\u4e8b\u672c\u6253\u5f00\u89c4\u5219\u300d\u65c1\u8fb9\u90a3\u4e2a\u6587\u4ef6\uff09\r\n\r\n")
                        + txt.Substring(txt.Length - 20000);
                }

                // \u2605 \u5148\u8ba9\u9875\u9762\u663e\u793a\u51fa\u6765 \u2727 \u6587\u5b57\u665a\u4e00\u70b9\u518d\u586b \u2713
                string body = txt;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            newsBox.Body = body;
                            app.Log("news painted in " + (Environment.TickCount - t0) + "ms (" + body.Length + " chars)");
                        }
                        catch { }
                    });
                }
                catch { newsBox.Body = body; }
                newsStatus.Text = Path.GetFileName(p) + "　（" + txt.Length + Lang.T(" 字）");
                newsStatus.ForeColor = Theme.Ok;
                // logged because the announcement page is built at startup, so a launch is
                // enough to prove the file was found - no clicking required
                app.Log("news loaded: " + Path.GetFileName(p) + " (" + txt.Length + " chars) " + newsBox.ScrollState);
            }
            catch (Exception ex)
            {
                newsBox.Body = Lang.T("读不了这个文件：") + ex.Message;
                newsStatus.Text = Lang.T("读取失败");
                newsStatus.ForeColor = Theme.Warn;
            }
        }

        void OpenNewsInEditor()
        {
            string p = FindNewsFile();
            if (p == null) { MessageBox.Show(Lang.T("找不到更新记录文件。"), Lang.T("喵化助手")); return; }
            try { System.Diagnostics.Process.Start("notepad.exe", "\"" + p + "\""); } catch { }
        }

        Panel BuildAboutPage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(22, 16, 10, 16);
            // CardPanel 是自绘控件（UserPaint）且故意不清除背景，这类控件滚动会留下横向
            // 残影 —— 和设置页同一个病。滚动改由一层普通面板负责。
            card.AutoScroll = false;
            SmoothPanel scroller2 = new SmoothPanel();
            scroller2.Dock = DockStyle.Fill;
            scroller2.AutoScroll = true;
            scroller2.BackColor = Theme.Card;
            card.Controls.Add(scroller2);

            Panel host = new Panel();
            host.BackColor = Theme.Card;
            host.Dock = DockStyle.Top;
            host.Height = 640;
            scroller2.Controls.Add(host);

            // ── 免费声明 ──
            //
            // 这类小工具经常被人打包拿去卖，所以把话说在最前面。
            // 用和"无障碍没开"同一个警示配色，一眼就能看到，不会当成普通正文划过去。
            Label warn = new Label();
            warn.AutoSize = false;
            warn.BackColor = Theme.Soft;
            warn.ForeColor = Theme.Accent;
            try { warn.Font = new Font(Theme.Small, FontStyle.Bold); } catch { warn.Font = Theme.Small; }
            warn.TextAlign = ContentAlignment.MiddleLeft;
            warn.Padding = new Padding(18, 6, 18, 6);
            warn.Location = new Point(0, 0);
            warn.Size = new Size(780, 84);
            warn.Text = Lang.T("这个软件是【完全免费】的\r\n")
                       + Lang.T("如果你是花钱买的，说明你被骗了 —— 请立即找商家退款。\r\n")
                       + Lang.T(Lang.T(Lang.T(Lang.T("它没有在任何平台出售，也没有\"付费版\"\"高级版\"之类的东西。"))));
            host.Controls.Add(warn);
            Label body = new Label();
            body.Font = Theme.Small;
            body.ForeColor = Theme.Text;
            body.BackColor = Theme.Card;
            body.AutoSize = false;
            body.TextAlign = ContentAlignment.TopLeft;
            body.Location = new Point(0, 96);
            body.Size = new Size(780, 580);
            body.Text =
                Lang.T("喵化助手 ") + Program.Version + "　（" + Program.BuildDate + "）\r\n\r\n" +
                Lang.T("一个本地运行的小工具：在 QQ 里打完字，停一下，整段文字就会变成喵喵的。\r\n\r\n") +
                Lang.T("── 它能做什么 ──\r\n") +
                "· 词语替换：规则里写 `原文 => 替换成`，从上到下依次执行\r\n" +
                Lang.T("· 句尾装饰：每个句子后面加后缀和随机颜文字\r\n") +
                Lang.T("· 三种程度：轻度（只加一次）/ 普通（每句）/ 重度（连逗号）\r\n") +
                Lang.T("· 两套风格：外观风格只改句尾；语气改写会替你在句首句尾加话\r\n") +
                Lang.T("· 语气会跟着情绪变：疑问 / 开心 / 难过 / 生气 / 普通 各走各的词库\r\n") +
                Lang.T("· 智能语气（可选）：用你自己电脑上的模型判断情绪，比关键词准得多\r\n") +
                Lang.T("· 两种触发：全局快捷键，或停止打字后自动转换\r\n\r\n") +
                Lang.T("── 它不会做什么 ──\r\n") +
                Lang.T("· 默认不联网。断网也能完整使用，只是语气判断用关键词做\r\n") +
                Lang.T("· 只有你打开「智能语气」时，它才会连 127.0.0.1，也就是你自己这台电脑\r\n") +
                Lang.T("  上的模型服务（LM Studio 之类）。全程不出这台机器，也不经过任何服务器\r\n") +
                Lang.T("· 模型只负责判断情绪（回一个词），加进你句子里的话仍然是 rules.txt 里你自己配的\r\n") +
                Lang.T("· 模型没开着、或者判断不出来，会自动退回关键词判断，不会卡住也不会出错\r\n") +
                Lang.T("· 不上传、不保存你输入的内容（只在你自己的输入框里原地改写）\r\n") +
                Lang.T("· 不碰网址、邮箱、文件路径和含数字的编号\r\n") +
                Lang.T("· 不改代码里的东西，也不碰你没列进「生效程序」的应用\r\n\r\n") +
                Lang.T("── 文件在哪 ──\r\n") +
                Lang.T("· rules.txt　规则（设置页和风格页改的就是它）\r\n") +
                Lang.T("· miaohua.log　运行日志，快捷键没生效时先看这里\r\n") +
                Lang.T("· stats.txt　喵化次数统计\r\n") +
                Lang.T("· 都在程序所在目录，用下面的按钮可以直接打开。\r\n\r\n") +
                Lang.T("── 一句话提醒 ──\r\n") +
                Lang.T("语气改写会改变你「说的内容」。要谈正事之前，先到「喵化风格」里点一下「萌系」把它关掉。");

            host.Controls.Add(body);

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.BackColor = Theme.Card;
            bar.WrapContents = false;
            bar.Location = new Point(0, 592);
            bar.Size = new Size(780, 42);
            host.Controls.Add(bar);

            RoundButton b1 = new RoundButton();
            b1.Text = Lang.T("打开规则文件");
            b1.Size = new Size(120, 32);
            b1.Click += delegate { app.OpenRulesInNotepad(); };
            bar.Controls.Add(b1);

            RoundButton b2 = new RoundButton();
            b2.Text = Lang.T("打开所在文件夹");
            b2.Size = new Size(130, 32);
            b2.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "\"" + app.BaseDir + "\""); }
                catch { }
            };
            bar.Controls.Add(b2);

            return card;
        }

        Panel BuildStatsPage()
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(22, 16, 22, 16);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 1;
            t.RowCount = 5;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(t);

            Label title = new Label();
            title.Text = Lang.T("使用统计");
            title.Font = Theme.Title;
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Card;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            t.Controls.Add(title, 0, 0);

            pageStatBig = new Label();
            pageStatBig.Font = new Font("Microsoft YaHei UI", 11f);
            pageStatBig.ForeColor = Theme.Text;
            pageStatBig.BackColor = Theme.Card;
            pageStatBig.Dock = DockStyle.Fill;
            pageStatBig.TextAlign = ContentAlignment.MiddleLeft;
            t.Controls.Add(pageStatBig, 0, 1);

            pageLevelBar = new LevelBar();
            pageLevelBar.Dock = DockStyle.Fill;
            pageLevelBar.Height = 22;
            t.Controls.Add(pageLevelBar, 0, 2);

            pageLevelHint = new Label();
            pageLevelHint.Font = Theme.Small;
            pageLevelHint.ForeColor = Theme.Muted;
            pageLevelHint.BackColor = Theme.Card;
            pageLevelHint.Dock = DockStyle.Fill;
            pageLevelHint.TextAlign = ContentAlignment.MiddleLeft;
            t.Controls.Add(pageLevelHint, 0, 3);

            pageStatList = new SmoothPanel();
            pageStatList.Dock = DockStyle.Fill;
            pageStatList.BackColor = Theme.Card;
            pageStatList.AutoScroll = true;
            t.Controls.Add(pageStatList, 0, 4);

            return card;
        }

        void RefreshStatsPage()
        {
            Stats s = app.Stat;
            if (pageStatBig != null)
                pageStatBig.Text =
                    Lang.T("累计喵化　") + s.Total + Lang.T(" 次\r\n") +
                    Lang.T("今日喵化　") + s.Today + Lang.T(" 次\r\n") +
                    Lang.T("等级　　　Lv.") + s.Level + "　（" + s.Total + " / " + s.LevelCeil + "）";

            if (pageLevelBar != null) pageLevelBar.Value = s.LevelProgress;
            if (pageLevelHint != null)
                pageLevelHint.Text = Lang.T("升到 Lv.") + (s.Level + 1) + Lang.T(" 还需要 ") +
                    Math.Max(0, s.LevelCeil - s.Total) + Lang.T(" 次喵化　·　每 40 次升一级");

            if (pageStatList == null) return;
            pageStatList.Controls.Clear();

            Label head = Tiny(Lang.T("颜文字使用次数"), Theme.Text, 0, 0);
            head.Size = new Size(200, 22);
            pageStatList.Controls.Add(head);

            int y = 26;
            foreach (string k in app.Cfg.Kaomoji)
            {
                int n = 0;
                s.Kaomoji.TryGetValue(k, out n);
                Label l = Tiny(k + "　—　" + n + Lang.T(" 次"), Theme.Muted, 0, y);
                l.Size = new Size(260, 22);
                pageStatList.Controls.Add(l);
                y += 22;
            }

            // \u2605 2026-10-18\uff1a\u6700\u8fd1\u55b5\u5316\u5386\u53f2 \u2727 \u53ea\u5728\u5185\u5b58 \u2717 \u4e0d\u843d\u76d8 \u2713
            if (s.Recent != null && s.Recent.Count > 0)
            {
                y += 14;
                Label h2 = Tiny(Lang.T("\u6700\u8fd1\u55b5\u5316\uff08\u6700\u591a 20 \u6761\uff0c\u53ea\u5728\u672c\u6b21\u8fd0\u884c\u5185\uff09"), Theme.Text, 0, y);
                h2.Size = new Size(500, 22);
                pageStatList.Controls.Add(h2);
                y += 26;

                for (int i = s.Recent.Count - 1; i >= 0; i--)
                {
                    string[] row = s.Recent[i];
                    if (row == null || row.Length < 2) continue;
                    Label a1 = Tiny(Lang.T("\u539f\u6587\u3000") + App.Brief(row[0]), Theme.Muted, 0, y);
                    a1.Size = new Size(540, 20);
                    pageStatList.Controls.Add(a1);
                    y += 20;
                    Label a2 = Tiny(Lang.T("\u7ed3\u679c\u3000") + App.Brief(row[1]), Theme.Accent, 0, y);
                    a2.Size = new Size(540, 20);
                    pageStatList.Controls.Add(a2);
                    y += 24;
                }
            }
        }

        Panel BuildRulesPage()
        {            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(18, 14, 18, 16);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 1;
            t.RowCount = 3;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            card.Controls.Add(t);

            Label l = new Label();
            // 只留一行。两行文字挤在固定行高里，第一行的上半会被裁掉 —— 用户截图里的
            // "蓝框里的字被截掉了"就是这个，加高行高也没用（试过 40 -> 50，仍然切）。
            l.Text = Lang.T("只读。用「用记事本打开」改，存盘后点「重新载入」。　") + app.RulesFilePath;
            l.Dock = DockStyle.Fill;
            l.Font = Theme.Small;
            l.ForeColor = Theme.Muted;
            l.BackColor = Theme.Card;
            t.Controls.Add(l, 0, 0);

            // 和更新公告页用同一个只读平滑视图。
            //
            // 原来是可编辑的 TextBox，它的滚动按行跳、做不出平滑。规则页的主要用途是【看】，
            // 要改有「用记事本打开」按钮 —— 在小框里改 rules.txt 本来也不如记事本舒服。
            // 代价是不能在程序里直接改了。
            rulesView = new SmoothTextView();
            rulesView.Dock = DockStyle.Fill;
            rulesView.Font = Theme.Mono;
            t.Controls.Add(rulesView, 0, 1);

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Theme.Card;
            bar.Padding = new Padding(0);
            // Same trap as the test page: 8px of padding plus the 3px per-child margin put
            // the 34px buttons at y=11 inside a 38px strip - the bottom 7px was cut off.

            RoundButton bSave = new RoundButton();
            bSave.Text = Lang.T("重新载入");
            bSave.Primary = true;
            bSave.Width = 104;
            bSave.BackColor = Theme.Card;
            bSave.Click += delegate { LoadRulesIntoBox(); };
            bar.Controls.Add(bSave);

            RoundButton bReload = new RoundButton();
            bReload.Text = Lang.T("刷新预览");
            bReload.Width = 170;
            bReload.BackColor = Theme.Card;
            bReload.Click += delegate { LoadRulesIntoBox(); };
            bar.Controls.Add(bReload);

            RoundButton bNotepad = new RoundButton();
            bNotepad.Text = Lang.T("用记事本打开");
            bNotepad.Width = 130;
            bNotepad.BackColor = Theme.Card;
            bNotepad.Click += delegate { app.OpenRulesInNotepad(); };
            bar.Controls.Add(bNotepad);

            t.Controls.Add(bar, 0, 2);

            return card;
        }

        Control BuildBottom()
        {
            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill;
            wrap.BackColor = Theme.Bg;
            wrap.Padding = new Padding(16, 4, 16, 12);

            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(18, 12, 18, 12);
            wrap.Controls.Add(card);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.BackColor = Theme.Card;
            t.ColumnCount = 2;
            t.RowCount = 3;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            card.Controls.Add(t);

            chkPause = NewCheck(Lang.T("暂停（所有转换都不生效）"));
            chkPause.CheckedChanged += delegate { if (!loading) app.SetPaused(chkPause.Checked); };
            t.Controls.Add(chkPause, 0, 0);

            chkAuto = NewCheck(Lang.T("停止打字后自动转换"));
            chkAuto.CheckedChanged += delegate { if (!loading) app.SetAutoMode(chkAuto.Checked); };
            t.Controls.Add(chkAuto, 1, 0);

            // Kept short on purpose. The long version "开机自动启动（后台静默，不弹窗）" got
            // ellipsised to "开机自动启动（后台静默…" once the toggle column dropped under
            // about 240px, which is what a narrow window produces.
            chkRun = NewCheck(Lang.T("开机自动启动"));
            chkRun.CheckedChanged += delegate
            {
                if (loading) return;
                App.SetAutoRun(chkRun.Checked);
                app.Log("autorun=" + App.IsAutoRunEnabled());
            };
            t.Controls.Add(chkRun, 0, 1);

            RoundButton bHide = new RoundButton();
            bHide.Text = Lang.T("隐藏到托盘");
            bHide.Width = 120;
            bHide.BackColor = Theme.Card;
            bHide.Click += delegate { Hide(); };
            t.Controls.Add(bHide, 1, 1);

            RoundButton bLog = new RoundButton();
            bLog.Text = Lang.T("查看日志");
            bLog.Width = 104;
            bLog.BackColor = Theme.Card;
            bLog.Click += delegate { app.OpenLogInNotepad(); };
            t.Controls.Add(bLog, 0, 2);

            RoundButton bExit = new RoundButton();
            bExit.Text = Lang.T("退出程序");
            bExit.Width = 104;
            bExit.BackColor = Theme.Card;
            bExit.Click += delegate { app.ExitApp(); };
            t.Controls.Add(bExit, 1, 2);

            return wrap;
        }

        ToggleRow NewCheck(string text)
        {
            ToggleRow c = new ToggleRow();
            c.Text = text;
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 2, 8, 2);
            return c;
        }

        // ------------------------------------------------ behaviour

        public void ShowWindow()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            RefreshStatus();
        }

        public void LoadFromApp()
        {
            LoadRulesIntoBox();
            SyncCheckboxes();
        }

        // exposed so --selftest can check how the rules file renders in the box
        /// 自检用：检查规则文本的换行有没有被吃掉。以前读的是编辑器里的内容，
        /// 现在规则页是只读视图，直接读程序手里的那份文本。
        public string RulesBoxText { get { return app.RulesText(); } }

        void LoadRulesIntoBox()
        {
            loading = true;
            try { rulesView.Body = app.RulesText(); }
            finally { loading = false; }
            UpdatePreview();
            RefreshStatus();
        }

        ToggleRow picGuardChk;

        /// 图片保护按钮上显示的文字。
        string PicGuardLabel()
        {
            return app.Cfg.ImageGuard
                ? Lang.T("开 —— 输入框里有图片就不转换")
                : Lang.T("关 —— 照样转换（图片会被弄没）");
        }
        public void SyncCheckboxes()
        {
            loading = true;
            try
            {
                chkPause.Checked = app.IsPaused;
                if (picGuardChk != null) picGuardChk.Checked = app.Cfg.ImageGuard;
                chkAuto.Checked = app.AutoModeEnabled;
                chkRun.Checked = App.IsAutoRunEnabled();
            }
            finally { loading = false; }
        }
        string uiNullLogged;
        int lastLevel = -1;

        /// ★ 应用风格期间【暂停一切刷新】★
        ///
        /// 为什么需要：有个定时器在不停调 RefreshStatus（它内部还会按签名
        /// 变化触发 RefreshRail）。点风格时要跑一串步骤 —— 写文件、重载规则、
        /// 重填列表、刷右栏 —— 这中间定时器会插进来刷一次，
        /// ★ 那一帧拿到的还是旧数据 ✗ 屏幕上就先闪一下旧值 ✓
        ///
        /// 置位期间这两个刷新函数直接返回 ✗ 应用完再统一刷一次 ✓
        bool suppressRefresh = false;

        /// ★ 有刷新请求被挡掉了吗 ★
        ///
        /// 光挡不补的话，被挡掉的那次刷新就永远丢了 —— 
        /// 状态卡上的数字要等下一个定时器周期（700ms）才更新，
        /// 用户看到的就是"点完过一会才变"。
        bool refreshPending = false;


        public void RefreshStatus()
        {
            if (suppressRefresh)
            {
                refreshPending = true;
                return;
            }
            // Guard every field this method touches, and say which one was missing - once.
            //
            // Without this, a single null field made the whole method throw. The timer calls it
            // every 100ms so it threw forever, and PatchRules calls it on save - the exception
            // was swallowed there and the save simply did nothing, with no message. Two symptoms
            // (a scary dialog and a dead Save button) from one missing null check, and the stack
            // the crash logger finally produced was the only way to see it.
            string missing = null;
            if (statusHead == null) missing = "statusHead";
            else if (statusDetail == null) missing = "statusDetail";
            else if (statusWarn == null) missing = "statusWarn";
            else if (statusDot == null) missing = "statusDot";
            else if (statusPills == null) missing = "statusPills";
            else if (headerFace == null) missing = "headerFace";
            else if (headerMood == null) missing = "headerMood";
            // only a fault when the cards are actually switched ON; when UiCards is off,
            // statCards being null is the correct state and not something to report
            else if (app.Cfg.UiCards && statCards == null) missing = "statCards (UiCards is on)";
            else if (app.Cfg.UiTopBar && headerIcon == null) missing = "headerIcon (UiTopBar is on)";
            else if (!app.Cfg.UiTopBar && headerIcon != null) missing = "headerIcon (UiTopBar is off)";
            if (missing != null)
            {
                if (uiNullLogged != missing)
                {
                    uiNullLogged = missing;
                    // written straight to the log: no helper dependency, so this cannot itself
                    // fail while the UI is in a bad state
                    try
                    {
                        string ldir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(ldir, "miaohua.log"),
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            + "  refresh status skipped: " + missing + " is null (UiTopBar="
                            + app.Cfg.UiTopBar + " UiCards=" + app.Cfg.UiCards + " UiLab="
                            + app.Cfg.UiLab + " UiKao=" + app.Cfg.UiKao + ")\r\n",
                            System.Text.Encoding.UTF8);
                    }
                    catch { }
                }
                return;
            }

            // ★★★ 值没变就不写 ✧ 避免重复触发重绘 ★★★
            //   切语言时 RefreshStatus 会被调两次 ✧
            //   （PatchRules 内部一次、我在 Click 里补的一次 ✓）
            //   每次都写就是两次重绘 ✧ 屏幕上就闪一下 ✓
            string headTxt = app.StatusHead();
            if (statusHead.Text != headTxt) statusHead.Text = headTxt;
            string detailTxt = app.StatusDetail();
            if (statusDetail.Text != detailTxt) statusDetail.Text = detailTxt;
            string warnTxt = app.StatusWarn();
            if (statusWarn.Text != warnTxt) statusWarn.Text = warnTxt;

            bool conflict = app.HotkeyFailures > 0;
            bool sleeping = app.IsPaused;

            if (statusDot != null)
                statusDot.DotColor = conflict ? Theme.Warn
                    : sleeping ? Color.FromArgb(230, 168, 60)
                    : Color.FromArgb(76, 175, 100);

            if (statusPills != null)
                statusPills.SetItems(
                    new string[]
                    {
                        app.RuleCount + Lang.T(" 条替换规则"),
                        app.KaomojiCount + Lang.T(" 种颜文字"),
                        Lang.T("自动模式 ") + (app.AutoModeEnabled ? Lang.T("开") : Lang.T("关")),
                        Lang.T("本次已喵化 ") + app.SessionCount + Lang.T(" 次")
                    },
                    new Color[]
                    {
                        Theme.Accent,
                        Color.FromArgb(150, 110, 210),
                        app.AutoModeEnabled ? Color.FromArgb(76, 175, 100) : Theme.Muted,
                        Color.FromArgb(90, 140, 210)
                    });

            if (app.Cfg.UiCards)
            {
            if (statCards != null && statCards[0] != null)
            {
                statCards[0].Set(conflict ? "\u26A0" : sleeping ? "\U0001F4A4" : "\U0001F7E2",
                                 conflict ? Lang.T("冲突") : sleeping ? Lang.T("休息") : Lang.T("运行中"),
                                 conflict ? Lang.T("快捷键被占用") : sleeping ? Lang.T("暂停中，不转换") : Lang.T("正常工作"),
                                 conflict ? Theme.Warn : sleeping ? Color.FromArgb(230, 168, 60)
                                                                 : Color.FromArgb(76, 175, 100));
                statCards[1].Set("\U0001F43E", app.RuleCount.ToString(), Lang.T("条替换规则"), Theme.Accent);
                statCards[2].Set("\U0001F431", app.KaomojiCount.ToString(), Lang.T("种颜文字"),
                                 Color.FromArgb(150, 110, 210));
                statCards[3].Set("\u2728", app.SessionCount.ToString(), Lang.T("本次已喵化"),
                                 Color.FromArgb(90, 140, 210));
            }
            }

            // ⑭ 今日次数越多，台词越兴奋 (UiMood)
            string moodOverride = null;
            int todayCount = app.Stat == null ? 0 : app.Stat.Today;
            if (app.Cfg.UiMood)
            {
                // ★ 动态生成的文字必须在【生成时】翻译 ✗ 
                //   "事后遍历翻译"赶不上它（RefreshStats 里也不再调 Apply 了）✓
                if (todayCount >= 50) moodOverride = Lang.T("今天是超级喵化日！");
                else if (todayCount >= 10) moodOverride = Lang.T("今天已经喵了好多次啦！");
                else moodOverride = Lang.T("今天也要加油喵～");
            }

            // the character reacts to real state, not to decoration
            if (headerFace != null)
            {
            if (app.Cfg.UiTopBar)
            {
                // The icon is the emoji label; the two text lines stay exactly as they were.
                if (conflict) { headerIcon.Text = "\U0001F63F"; headerFace.Text = "(>﹏<)"; headerMood.Text = Lang.T("呜……快捷键被占用了"); }
                else if (sleeping) { headerIcon.Text = "\U0001F4A4"; headerFace.Text = "(￣o￣)"; headerMood.Text = Lang.T("休息中喵……"); }
                else if (Environment.TickCount - app.LastTransformTick < 2500)
                { headerIcon.Text = "\u2728"; headerFace.Text = "(=^▽^=)"; headerMood.Text = Lang.T("喵化成功！"); }
                else { headerIcon.Text = "\U0001F43E"; headerFace.Text = "(=^･ω･^=)"; headerMood.Text = moodOverride != null ? moodOverride : Lang.T("准备好喵～"); }
            }
            else
            {
                if (conflict) { headerFace.Text = "(>﹏<)"; headerMood.Text = Lang.T("呜……快捷键被占用了"); }
                else if (sleeping) { headerFace.Text = "(￣o￣)"; headerMood.Text = Lang.T("休息中喵……"); }
                else if (Environment.TickCount - app.LastTransformTick < 2500)
                { headerFace.Text = "(=^▽^=)"; headerMood.Text = Lang.T("喵化成功！"); }
                else { headerFace.Text = "(=^･ω･^=)"; headerMood.Text = moodOverride != null ? moodOverride : Lang.T("准备好喵～"); }
            }
            }

            // The rail is rebuilt, not just repainted, so only do it when something it
            // shows actually changed - otherwise it would churn every 700ms.
            string sig = app.RuleCount + "|" + app.KaomojiCount + "|" + app.Stat.Total + "|" + app.Stat.Today;
            // ⑨ 升级时提示一次 (UiLevel)
            //
            // Compared against the previous value, so it fires on the transition rather than
            // every refresh - this runs on a 100ms timer.
            if (app.Cfg.UiLevel)
            {
                int lv = app.Stat.Level;
                if (lastLevel < 0) lastLevel = lv;
                else if (lv > lastLevel)
                {
                    lastLevel = lv;
                    MessageBox.Show(this,
                        Lang.T("喵化等级提升！\r\n\r\nLv.") + (lv - 1) + " → Lv." + lv
                        + Lang.T("\r\n\r\n累计喵化 ") + app.Stat.Total + Lang.T(" 次，继续加油喵～"),
                        Lang.T("喵化助手 · 恭喜"));
                }
                else if (lv < lastLevel) lastLevel = lv;
            }
            if (sig != railSig) { railSig = sig; RefreshRail(); }
        }

        // ---- ④ 页面切换的淡入 (UiAnim) -------------------------------------------
        //
        // Two lessons are baked into this code.
        //
        // 1. Fade a BITMAP, never move the real page. The first attempt set Left on the page
        //    every frame, which repaints its ~100 child controls each time and stuttered.
        // 2. Freeze redraw while switching. Otherwise the order the user sees is: new page
        //    paints to screen -> snapshot takes ~100ms -> opaque overlay appears. That reads
        //    as "everything shows, pauses, vanishes, then fades in", which is what got
        //    reported. With WM_SETREDRAW off, all three steps land as one.
        Timer langOnce;
        System.Windows.Forms.Timer navTimer;
        int navStep;
        int navIndex;
        NavSlider navSlider;
        bool navFirst = true;

        void StartNavSlide(int index)
        {
            try
            {
                if (index < 0 || index >= nav.Length || nav[index] == null) return;
                NavButton b = nav[index];
                EnsureNavSliderHost();
                NavButton.GlideOn = true;
                int targetTop = b.Top, h = b.Height;
                if (navFirst) { NavButton.GlideTop = targetTop; NavButton.GlideH = h; navFirst = false; InvalidateNav(); return; }

                int fromTop = NavButton.GlideTop;
                NavButton.GlideH = h;
                if (navTimer != null) { navTimer.Stop(); navTimer.Dispose(); navTimer = null; }
                navStep = 0;
                navTimer = new System.Windows.Forms.Timer();
                // 帧数跟着 界面-动画时长 走。原来是写死的 14 帧 x 15ms = 210ms，
                // 相对淡化（22 帧 x 18ms = 396ms）约 0.53 倍 —— 保持同一个比例，
                // 所以默认 400 时和以前完全一样，改了设置才会跟着变。
                // 时长 = 淡化 x 滑块比例%（界面-滑块比例，默认 100 = 一样长）。
                // 想回到最早那个手感（滑块先到位）就把它填 53。
                navFrames = Math.Max(2, app.Cfg.UiAnimMs * app.Cfg.UiSlidePct / 100 / 15);
                navTimer.Interval = 15;
                navTimer.Tick += delegate
                {
                    navStep++;
                    float tt = navStep / (float)navFrames; if (tt > 1f) tt = 1f;
                    // ease-in-out：先加速后减速
                    float ease = tt < 0.5f ? 2f * tt * tt : 1f - 2f * (1f - tt) * (1f - tt);
                    int y = fromTop + (int)Math.Round((targetTop - fromTop) * ease);
                    if (tt >= 1f) y = targetTop;      // 最后一帧取准确值
                    NavButton.GlideTop = y;
                    InvalidateNav();
                    if (navStep >= navFrames && navTimer != null)
                    {
                        navTimer.Stop(); navTimer.Dispose(); navTimer = null;
                    }
                };
                navTimer.Start();
            }
            catch { }
        }

        Control navSliderHost;

        /// 滑块由【容器】画一次，而不是每个导航项各画自己那一段。
        ///
        /// 各画各的问题：每一项只能画在自己控件范围内，两个控件之间的空隙谁也画不到，滑块
        /// 中间就被切出一道白缝 —— 用户截图指着说"滑到每个功能之间会有一个白色空隙"。
        /// 交给容器画就没有"缝"这回事。导航项改成真透明后，WinForms 会让父容器来画那一片，
        /// 滑块自然从按钮底下透出来。
        void EnsureNavSliderHost()
        {
            if (navSliderHost != null) return;
            NavButton first = null;
            foreach (NavButton x in nav) if (x != null) { first = x; break; }
            if (first == null || first.Parent == null) return;
            navSliderHost = first.Parent;
            navSliderHost.Paint += delegate(object s, PaintEventArgs pe)
            {
                if (!NavButton.GlideOn || NavButton.GlideH <= 2) return;
                Graphics g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = 0;
                foreach (NavButton x in nav) if (x != null) { w = x.Width; break; }
                Rectangle gl = new Rectangle(0, NavButton.GlideTop, w - 1, NavButton.GlideH);
                using (GraphicsPath p = Rounded.Path(gl, 10))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(48, Theme.Accent)))
                    g.FillPath(b, p);
                Rectangle bar = new Rectangle(0, NavButton.GlideTop + 9, 4, Math.Max(2, NavButton.GlideH - 18));
                using (GraphicsPath p = Rounded.Path(bar, 2))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, p);
            };
            foreach (NavButton x in nav) if (x != null) x.MakeTransparent();
        }
        void InvalidateNav()
        {
            foreach (NavButton x in nav) if (x != null) x.Invalidate();
            if (navSliderHost != null) navSliderHost.Invalidate(true);
        }
        void StartNavGrowOld(int index)
        {
            try
            {
                if (navTimer != null) { navTimer.Stop(); navTimer.Dispose(); navTimer = null; }
                foreach (NavButton b in nav) if (b != null) b.Grow = 1f;   // 其余立刻归位
                navIndex = index;
                navStep = 0;
                navTimer = new System.Windows.Forms.Timer();
                // 同上：原来 7 帧 x 16ms = 112ms，相对淡化约 0.28 倍。
                // 同上，也跟 界面-滑块比例 走。
                growFrames = Math.Max(2, app.Cfg.UiAnimMs * app.Cfg.UiSlidePct / 100 / 16);
                navTimer.Interval = 16;
                navTimer.Tick += delegate
                {
                    navStep++;
                    float g = navStep / (float)growFrames; if (g > 1f) g = 1f;
                    if (navIndex >= 0 && navIndex < nav.Length && nav[navIndex] != null)
                    {
                        nav[navIndex].Grow = g;
                        nav[navIndex].Invalidate();
                    }
                    if (navStep >= growFrames && navTimer != null)
                    {
                        navTimer.Stop(); navTimer.Dispose(); navTimer = null;
                    }
                };
                navTimer.Start();
            }
            catch { }
        }
        bool _animOn;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        const int WM_SETREDRAW = 0x000B;

        void FreezeRedraw(Control c, bool freeze)
        {
            if (c == null || !c.IsHandleCreated) return;
            try { SendMessage(c.Handle, WM_SETREDRAW, freeze ? 0 : 1, 0); } catch { }
            if (!freeze) { try { c.Invalidate(true); } catch { } }
        }

        /// Draws a snapshot of the incoming page at a decreasing alpha. Disabled so it never
        /// swallows clicks, and transparent so nothing shows through before the first paint.
        class FadeOverlay : Panel
        {
            public Bitmap OldSnap;
            public Bitmap NewSnap;
            public float Mix = 0f;          // 0 = 全是旧页面，1 = 全是新页面

            public FadeOverlay(Bitmap oldSnap, Bitmap newSnap)
            {
                OldSnap = oldSnap;
                NewSnap = newSnap;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
                Enabled = false;
            }

            void Blend(Graphics g, Bitmap b, float alpha)
            {
                if (b == null || alpha <= 0.001f) return;
                if (alpha > 1f) alpha = 1f;
                System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
                cm.Matrix33 = alpha;
                using (System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    g.DrawImage(b, new Rectangle(0, 0, Width, Height),
                        0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;

                // 两张图都画在这里，绝不依赖"透过去看父容器"。
                //
                // 之前覆盖层是 Color.Transparent，指望透过它看到下面的新页面。但自绘控件的画布
                // 并不是父容器的内容，半透明混合混到的是空白 —— 视频逐帧显示得很清楚：淡化过程
                // 中画面一路从 41KB 掉到 24KB（旧页越来越白），最后【突然】跳到 31KB（新页出现）。
                // 用户的原话是「原先界面一点点没掉然后新界面直接出来」，一个字都不差。
                //
                // 现在：先铺一张不透明的新页面当底，再把旧页面按 1-Mix 叠上去。收尾那一帧和真
                // 页面逐像素一致，撤掉覆盖层时不会有任何跳变。
                using (SolidBrush bg = new SolidBrush(Theme.Card))
                    g.FillRectangle(bg, ClientRectangle);
                Blend(g, NewSnap, 1f);
                Blend(g, OldSnap, 1f - Mix);
            }
        }

        FadeOverlay fadePanel;
        System.Windows.Forms.Timer fadeTimer;
        int fadeStep;
        int navFrames = 14;       // 导航滑块帧数，由 界面-动画时长 算出来
        int growFrames = 7;       // 导航项放大帧数，同上
        int fadeFrames = 22;      // 由 界面-动画时长 算出来，默认 400ms / 18ms ≈ 22 帧

        /// 把覆盖层对齐到内容区在窗体上的实际位置。
        void AlignFadePanel()
        {
            if (fadePanel == null || contentHost == null) return;
            try
            {
                Point sp = contentHost.PointToScreen(Point.Empty);
                Point cp = PointToClient(sp);
                fadePanel.Bounds = new Rectangle(cp.X, cp.Y,
                    contentHost.ClientSize.Width, contentHost.ClientSize.Height);
            }
            catch { }
        }
        void StartFade(Bitmap oldSnap, Bitmap newSnap)
        {
            StopFade();
            fadePanel = new FadeOverlay(oldSnap, newSnap);
            fadePanel.Mix = 0f;
            // 挂回 contentHost：这样坐标绝对精确，不需要任何窗体坐标换算。
            //
            // 曾改成挂到窗体上、手动对齐，结果算差了十几个像素，用户看到的是"内容往卡片左上角
            // 偏移"加上"覆盖层铺的白色底色漏到卡片外面"。坐标换算是这里最容易出错的一环，能
            // 不换算就不换算。
            // 必须用 Dock=Fill。
            //
            // contentHost 有内边距，Fill 会把覆盖层放进内边距里 —— 位置 (16,8)、尺寸 666x338，
            // 而这正好就是【页面】的位置和尺寸。快照也是按页面尺寸拍的，两边天然一致。
            // 我一度把这个内边距当成 bug，改成手动铺满 698x352，结果反而把 666x338 的快照
            // 拉伸了约 4.8% —— 用户看到的"切换时卡片缩小、内容往左上偏"就是这么来的。
            fadePanel.Dock = DockStyle.Fill;
            contentHost.Controls.Add(fadePanel);
            fadePanel.BringToFront();

            fadeStep = 0;
            // 每帧 18ms，最少 2 帧（不然等于没有动画）
            fadeFrames = Math.Max(2, app.Cfg.UiAnimMs / 18);
            fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 18;   // 22 帧 x 18ms 约 400ms：160ms 太短，眼睛读成"啪一下出现"
            fadeTimer.Tick += delegate
            {
                fadeStep++;
                // 帧数从配置算 —— 原来是写死的 22 帧（22 x 18ms = 396ms）。
            // 手机版做了可配置之后两边对不上，电脑版也放出来。
            // 改设置要下次打开程序才生效（启动时算一次，之后就固定了）。
            if (fadeStep >= fadeFrames) { StopFade(); return; }
                if (fadePanel != null)
                {
                    fadePanel.Mix = fadeStep / (float)fadeFrames;
                    fadePanel.Invalidate();
                }
            };
            // 不在这里 Start()：调用方会在【解冻之后】再启动。
            //
            // 冻结期间切页面、重建侧栏、刷新统计要花几百毫秒，计时器照跑不误，22 帧 x 18ms
            // 会被整个吃掉 —— 解冻时淡化已经结束，用户看到的等于没有动画。用户和探针都证实
            // 了这一点：第一帧就已经是新页面的最终样子。
        }

        void BeginFadeAnimation()
        {
            if (fadeTimer != null && !fadeTimer.Enabled) fadeTimer.Start();
        }

        void StopFade()
        {
            if (fadeTimer != null)
            {
                try { fadeTimer.Stop(); fadeTimer.Dispose(); } catch { }
                fadeTimer = null;
            }
            if (fadePanel != null)
            {
                try
                {
                    if (fadePanel.Parent != null) fadePanel.Parent.Controls.Remove(fadePanel);
                    if (fadePanel.OldSnap != null) fadePanel.OldSnap.Dispose();
                    if (fadePanel.NewSnap != null) fadePanel.NewSnap.Dispose();
                    fadePanel.Dispose();
                }
                catch { }
                fadePanel = null;
            }
        }

        /// 供 --film 使用：把内容区和当前页面的实际尺寸打成一行字。
        ///
        /// 之前用扫像素的办法量卡片边缘，两次都测错（扫到的是整个窗口）。让程序自己报尺寸
        /// 不会有这种问题。
        public string FadeDebug = "(not run)";

        /// 实时状态：覆盖层在不在、Mix 多少、计时器是否在跑
        public string FadeLive
        {
            get
            {
                if (fadePanel == null) return "overlay=NULL";
                return "overlay=up mix=" + fadePanel.Mix.ToString("0.00")
                     + " vis=" + fadePanel.Visible
                     + " parent=" + (fadePanel.Parent == null ? "none" : fadePanel.Parent.Name)
                     + " bounds=" + fadePanel.Bounds.X + "," + fadePanel.Bounds.Y
                     + " " + fadePanel.Width + "x" + fadePanel.Height
                     + " timer=" + (fadeTimer == null ? "null" : (fadeTimer.Enabled ? "running" : "stopped"));
            }
        }

        /// 滚轮消息是发给【有焦点】的控件的，不是发给鼠标底下那个。
        ///
        /// 更新公告页只有一个自绘视图、没有焦点，滚轮消息就落到窗体上 —— 那个视图的
        /// OnMouseWheel 永远不会被调用，所以滚轮完全没反应。这里按鼠标位置找到真正在下面的
        /// 控件再转发过去；找不到就交回基类。
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            try
            {
                Point p = PointToClient(Cursor.Position);
                Control c = GetChildAtPoint(p, GetChildAtPointSkip.Invisible | GetChildAtPointSkip.Disabled);
                while (c != null)
                {
                    SmoothTextView v = c as SmoothTextView;
                    if (v != null && v.WheelBy(e.Delta)) return;
                    c = c.Parent;
                }
            }
            catch { }
            base.OnMouseWheel(e);
        }
        public string LayoutProbe()
        {
            try
            {
                Control cur = null;
                for (int i = 0; i < pages.Length; i++) if (pages[i].Visible) { cur = pages[i]; break; }
                string s = "host=" + contentHost.ClientSize.Width + "x" + contentHost.ClientSize.Height
                         + " fade[" + FadeDebug + "] " + FadeLive
                         + " paw[" + CardPanel.PawDebug + "]";
                if (cur != null)
                {
                    s += " page=" + cur.Bounds.X + "," + cur.Bounds.Y + " "
                       + cur.Width + "x" + cur.Height + " dock=" + cur.Dock;
                    foreach (Control c in cur.Controls)
                    {
                        s += " | child=" + c.GetType().Name + " " + c.Bounds.X + "," + c.Bounds.Y
                           + " " + c.Width + "x" + c.Height + " dock=" + c.Dock;
                        break;
                    }
                }
                return s;
            }
            catch (Exception ex) { return "probe failed: " + ex.Message; }
        }

        void UpdatePreview()
        {
            string src = testIn.Box.Text;
            testOut.Box.Text = app.TransformForPreview(src);
            if (testCount != null) testCount.Text = src.Length + " / 500";

            // the whole point of 智能语气 is invisible otherwise: show which classifier
            // actually produced this result
            if (toneSource != null)
            {
                if (src.Length == 0 || Engine.LastMoodInfo.Length == 0) toneSource.Text = "";
                else toneSource.Text = Lang.T("语气：") + Engine.LastMoodInfo;

                // reposition AFTER the text is set: AutoSize changes the width, and the
                // Left computed on the old width would push the label off the right edge
                if (toneSource.Parent != null)
                    toneSource.Left = Math.Max(0, toneSource.Parent.Width - toneSource.Width);

                toneSource.ForeColor = Engine.LastMoodInfo.Contains(Lang.T("模型")) &&
                                       !Engine.LastMoodInfo.Contains(Lang.T("没应答"))
                    ? Theme.Ok : Theme.Muted;
            }
        }



        void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            // the X button hides the window; the program keeps running in the tray
            if (!app.ReallyExiting)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
