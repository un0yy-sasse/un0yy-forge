// un0yy Vault - user interface (custom drawn, borderless, DPI aware)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Un0yyVault
{
    // ================================================================== basics

    static class Dpi
    {
        public static readonly float S = Init();
        static float Init()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1f, g.DpiX / 96f); }
            catch { return 1f; }
        }
        public static int D(float v) { return (int)Math.Round(v * S); }
        public static float F(float v) { return v * S; }
    }

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(11, 11, 16);
        public static readonly Color Panel = Color.FromArgb(19, 19, 27);
        public static readonly Color Surface = Color.FromArgb(23, 23, 33);
        public static readonly Color Surface2 = Color.FromArgb(33, 33, 47);
        public static readonly Color Surface3 = Color.FromArgb(45, 45, 63);
        public static readonly Color Border = Color.FromArgb(48, 48, 68);
        public static readonly Color Text = Color.FromArgb(242, 242, 250);
        public static readonly Color Muted = Color.FromArgb(146, 146, 172);
        public static readonly Color Faint = Color.FromArgb(96, 96, 122);
        public static readonly Color Accent = Color.FromArgb(124, 77, 255);
        public static readonly Color Accent2 = Color.FromArgb(40, 180, 255);
        public static readonly Color AccentSoft = Color.FromArgb(178, 156, 255);
        public static readonly Color Danger = Color.FromArgb(244, 80, 110);
        public static readonly Color Success = Color.FromArgb(52, 211, 153);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static string iconFamily;

        static Font Get(string family, float size, FontStyle st)
        {
            string key = family + "|" + size + "|" + (int)st;
            Font f;
            if (!fonts.TryGetValue(key, out f)) { f = new Font(family, size, st); fonts[key] = f; }
            return f;
        }

        public static Font UI(float size) { return Get("Segoe UI", size, FontStyle.Regular); }
        public static Font Bold(float size) { return Get("Segoe UI", size, FontStyle.Bold); }
        public static Font Semi(float size) { return Get("Segoe UI Semibold", size, FontStyle.Regular); }
        public static Font Icon(float size)
        {
            if (iconFamily == null)
            {
                iconFamily = "Segoe MDL2 Assets";
                try
                {
                    using (var c = new InstalledFontCollection())
                        if (c.Families.Any(x => x.Name == "Segoe Fluent Icons")) iconFamily = "Segoe Fluent Icons";
                }
                catch { }
            }
            return Get(iconFamily, size, FontStyle.Regular);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color A(Color c, int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c); }
    }

    static class Ico
    {
        public const string Add = "", Search = "", Lock = "", Unlock = "", Key = "";
        public const string Open = "", Export = "", Delete = "", Shield = "", Eye = "";
        public const string EyeOff = "", Close = "", Min = "", Max = "", Restore = "";
        public const string Upload = "", Info = "", Warning = "", Error = "", Check = "";
        public const string Up = "", Down = "", Folder = "", Photo = "", Doc = "";
        public const string Video = "", Music = "", Package = "", Page = "", Clock = "";
    }

    static class G
    {
        public static void HQ(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2f);
            if (rad <= 0.5f) { p.AddRectangle(r); return p; }
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static void Gradient(Graphics g, RectangleF r, float rad, Color a, Color b, float angle)
        {
            if (r.Width < 1 || r.Height < 1) return;
            using (var p = Round(r, rad)) using (var br = new LinearGradientBrush(r, a, b, angle)) g.FillPath(br, p);
        }

        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl)
        {
            TextRenderer.DrawText(g, s, f, r, c, fl | TextFormatFlags.NoPrefix);
        }

        public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags Right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        public const TextFormatFlags Wrap = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak;
        public const TextFormatFlags WrapLeft = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak;

        public static void Glyph(Graphics g, string glyph, float size, Rectangle r, Color c)
        {
            TextRenderer.DrawText(g, glyph, Theme.Icon(size), r, c, Center | TextFormatFlags.NoPadding);
        }

        public static void Glow(Graphics g, RectangleF r, Color c, int alpha)
        {
            if (r.Width < 2 || r.Height < 2) return;
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(r);
                using (var b = new PathGradientBrush(p))
                {
                    b.CenterColor = Theme.A(c, alpha);
                    b.SurroundColors = new[] { Theme.A(c, 0) };
                    g.FillPath(b, p);
                }
            }
        }

        public static int TextW(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
        }
    }

    static class Art
    {
        public static void DrawLock(Graphics g, RectangleF r)
        {
            G.HQ(g);
            float w = r.Width, h = r.Height;
            using (var path = G.Round(r, w * 0.26f))
            using (var br = new LinearGradientBrush(r, Theme.Accent, Theme.Accent2, 45f))
                g.FillPath(br, path);
            // soft highlight
            using (var path = G.Round(new RectangleF(r.X, r.Y, w, h * 0.55f), w * 0.26f))
            using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y, w, h * 0.55f + 1), Color.FromArgb(45, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                g.FillPath(br, path);

            float sw = Math.Max(1.5f, w * 0.085f);
            float sx = r.X + w * 0.33f, sy = r.Y + h * 0.18f, ss = w * 0.34f;
            float bodyTop = r.Y + h * 0.46f;
            using (var pen = new Pen(Color.White, sw))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawArc(pen, sx, sy, ss, ss, 180, 180);
                g.DrawLine(pen, sx, sy + ss / 2, sx, bodyTop);
                g.DrawLine(pen, sx + ss, sy + ss / 2, sx + ss, bodyTop);
            }
            var body = new RectangleF(r.X + w * 0.22f, bodyTop - h * 0.02f, w * 0.56f, h * 0.38f);
            G.Fill(g, body, w * 0.08f, Color.White);
            float cx = r.X + w * 0.5f, cy = body.Y + body.Height * 0.42f, kr = w * 0.055f;
            using (var kb = new SolidBrush(Theme.Accent))
            {
                g.FillEllipse(kb, cx - kr, cy - kr, kr * 2, kr * 2);
                g.FillRectangle(kb, cx - w * 0.022f, cy, w * 0.044f, body.Height * 0.33f);
            }
        }

        static Icon cached;
        public static Icon AppIcon
        {
            get
            {
                if (cached != null) return cached;
                try { cached = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
                if (cached == null)
                {
                    var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp)) DrawLock(g, new RectangleF(0, 0, 32, 32));
                    cached = Icon.FromHandle(bmp.GetHicon());
                }
                return cached;
            }
        }

        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
            {
                using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); DrawLock(g, new RectangleF(0, 0, s, s)); }
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
                }
            }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }

    static class Kinds
    {
        public static readonly string[] Names = { "Alle Dateien", "Bilder", "Dokumente", "Videos", "Musik", "Archive", "Sonstige" };
        public static readonly string[] Glyphs = { Ico.Folder, Ico.Photo, Ico.Doc, Ico.Video, Ico.Music, Ico.Package, Ico.Page };
        public static readonly Color[] Colors =
        {
            Theme.AccentSoft, Color.FromArgb(244, 114, 182), Color.FromArgb(96, 165, 250), Color.FromArgb(251, 146, 60),
            Color.FromArgb(74, 222, 128), Color.FromArgb(250, 204, 21), Color.FromArgb(167, 139, 250)
        };
        static readonly string[][] Ext =
        {
            new string[0],
            new[] { "jpg", "jpeg", "png", "gif", "bmp", "webp", "heic", "heif", "svg", "ico", "tif", "tiff", "raw", "psd" },
            new[] { "pdf", "doc", "docx", "txt", "rtf", "odt", "xls", "xlsx", "csv", "ppt", "pptx", "md", "json", "xml", "html", "htm", "odp", "ods" },
            new[] { "mp4", "mov", "avi", "mkv", "wmv", "webm", "m4v", "flv", "3gp" },
            new[] { "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus" },
            new[] { "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso" }
        };

        public static string ExtOf(string name)
        {
            string e = Path.GetExtension(name.Replace('/', '\\'));
            return string.IsNullOrEmpty(e) ? "" : e.TrimStart('.').ToLowerInvariant();
        }

        public static int Of(string name)
        {
            string e = ExtOf(name);
            for (int i = 1; i < Ext.Length; i++) if (Ext[i].Contains(e)) return i;
            return 6;
        }
    }

    static class Fmt
    {
        public static string Size(long b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double d = b;
            int i = 0;
            while (d >= 1024 && i < u.Length - 1) { d /= 1024; i++; }
            return (i == 0 ? b.ToString() : d.ToString("0.#")) + " " + u[i];
        }

        public static string Date(DateTime utc)
        {
            DateTime t = utc.ToLocalTime();
            if (t.Date == DateTime.Today) return "Heute, " + t.ToString("HH:mm");
            if (t.Date == DateTime.Today.AddDays(-1)) return "Gestern, " + t.ToString("HH:mm");
            return t.ToString("dd.MM.yyyy");
        }

        public static string Files(int n) { return n + (n == 1 ? " Datei" : " Dateien"); }
    }

    static class Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        public static void Cue(TextBox t, string text)
        {
            EventHandler apply = delegate { SendMessage(t.Handle, 0x1501, (IntPtr)1, text); };
            if (t.IsHandleCreated) apply(t, EventArgs.Empty); else t.HandleCreated += apply;
        }
    }

    // ================================================================== controls

    class AnimControl : Control
    {
        protected float hoverT;
        protected bool over, down;
        readonly Timer anim;

        public AnimControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            anim = new Timer();
            anim.Interval = 15;
            anim.Tick += delegate
            {
                float t = over && Enabled ? 1f : 0f;
                hoverT += (t - hoverT) * 0.28f;
                if (Math.Abs(t - hoverT) < 0.02f) { hoverT = t; anim.Stop(); }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); over = true; anim.Start(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); over = false; down = false; anim.Start(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); anim.Start(); Invalidate(); }
        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
    }

    enum BtnKind { Primary, Secondary, Ghost, Danger, DangerGhost }

    class FlatButton : AnimControl
    {
        public BtnKind Kind;
        public string Glyph;

        public FlatButton(string text, string glyph, BtnKind kind)
        {
            Text = text; Glyph = glyph; Kind = kind;
            Font = Theme.Semi(10f);
            Cursor = Cursors.Hand;
            Size = new Size(PreferredW(), Dpi.D(40));
        }

        public int PreferredW()
        {
            int w = G.TextW(Text, Font) + Dpi.D(36);
            if (!string.IsNullOrEmpty(Glyph)) w += Dpi.D(26);
            return w;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Dpi.F(10);
            float h = hoverT;
            bool en = Enabled;
            Color fg = Theme.Text;
            switch (Kind)
            {
                case BtnKind.Primary:
                    if (!en) { G.Fill(g, r, rad, Theme.Surface2); fg = Theme.Faint; break; }
                    Color a = Theme.Mix(Theme.Accent, Color.White, 0.14f * h), b = Theme.Mix(Theme.Accent2, Color.White, 0.14f * h);
                    if (down) { a = Theme.Mix(a, Color.Black, 0.15f); b = Theme.Mix(b, Color.Black, 0.15f); }
                    G.Gradient(g, r, rad, a, b, 0f);
                    fg = Color.White;
                    break;
                case BtnKind.Secondary:
                    G.Fill(g, r, rad, en ? Theme.Mix(Theme.Surface2, Theme.Surface3, h + (down ? 0.4f : 0f)) : Theme.Surface2);
                    G.Stroke(g, r, rad, Theme.Mix(Theme.Border, Theme.Faint, h * 0.6f), 1f);
                    fg = en ? Theme.Text : Theme.Faint;
                    break;
                case BtnKind.Ghost:
                    if (h > 0.01f) G.Fill(g, r, rad, Theme.A(Theme.Surface3, (int)(255 * h)));
                    fg = en ? Theme.Mix(Theme.Muted, Theme.Text, h) : Theme.Faint;
                    break;
                case BtnKind.Danger:
                    G.Fill(g, r, rad, en ? Theme.Mix(Theme.Danger, Color.White, 0.12f * h) : Theme.Surface2);
                    fg = en ? Color.White : Theme.Faint;
                    break;
                case BtnKind.DangerGhost:
                    G.Fill(g, r, rad, en ? Theme.A(Theme.Danger, (int)(26 + 46 * h)) : Theme.Surface2);
                    fg = en ? Theme.Mix(Theme.Danger, Color.White, 0.35f * h) : Theme.Faint;
                    break;
            }
            int tw = G.TextW(Text, Font);
            int gw = string.IsNullOrEmpty(Glyph) ? 0 : Dpi.D(26);
            int x = (Width - tw - gw) / 2;
            if (gw > 0) G.Glyph(g, Glyph, 11f, new Rectangle(x, 0, Dpi.D(18), Height), fg);
            G.Text(g, Text, Font, new Rectangle(x + gw, 0, tw + 4, Height), fg, G.Left);
        }
    }

    class IconButton : AnimControl
    {
        public string Glyph;
        public bool CloseStyle;
        public float GlyphSize = 10f;

        public IconButton(string glyph) { Glyph = glyph; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            Color fg;
            if (CloseStyle)
            {
                if (hoverT > 0.01f) using (var b = new SolidBrush(Theme.A(Theme.Danger, (int)(255 * hoverT)))) g.FillRectangle(b, ClientRectangle);
                fg = Theme.Mix(Theme.Muted, Color.White, hoverT);
            }
            else
            {
                if (hoverT > 0.01f) G.Fill(g, new RectangleF(0, 0, Width - 1, Height - 1), Dpi.F(8), Theme.A(Theme.Surface3, (int)(255 * hoverT)));
                fg = Theme.Mix(Theme.Muted, Theme.Text, hoverT);
            }
            G.Glyph(g, Glyph, GlyphSize, ClientRectangle, fg);
        }
    }

    class NavItem : AnimControl
    {
        public string Glyph, Count;
        public Color Tint;
        bool selected;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        public NavItem(string text, string glyph, Color tint)
        {
            Text = text; Glyph = glyph; Tint = tint;
            Cursor = Cursors.Hand;
            Height = Dpi.D(40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            if (selected)
            {
                G.Gradient(g, r, Dpi.F(10), Theme.A(Theme.Accent, 70), Theme.A(Theme.Accent, 18), 0f);
                G.Fill(g, new RectangleF(0, Height * 0.26f, Dpi.F(3), Height * 0.48f), Dpi.F(2), Theme.AccentSoft);
            }
            else if (hoverT > 0.01f) G.Fill(g, r, Dpi.F(10), Theme.A(Theme.Surface2, (int)(255 * hoverT)));

            Color fg = selected ? Theme.Text : Theme.Mix(Theme.Muted, Theme.Text, hoverT);
            G.Glyph(g, Glyph, 11f, new Rectangle(Dpi.D(12), 0, Dpi.D(22), Height), selected ? Tint : Theme.Mix(Theme.Faint, Tint, hoverT * 0.8f));
            G.Text(g, Text, selected ? Theme.Semi(10f) : Theme.UI(10f), new Rectangle(Dpi.D(44), 0, Width - Dpi.D(90), Height), fg, G.Left);
            if (!string.IsNullOrEmpty(Count))
            {
                var f = Theme.Semi(8.5f);
                int cw = Math.Max(Dpi.D(24), G.TextW(Count, f) + Dpi.D(12));
                var pill = new RectangleF(Width - cw - Dpi.D(10), (Height - Dpi.D(20)) / 2f, cw, Dpi.D(20));
                G.Fill(g, pill, Dpi.F(10), selected ? Theme.A(Theme.Accent, 110) : Theme.Surface2);
                G.Text(g, Count, f, Rectangle.Round(pill), selected ? Color.White : Theme.Muted, G.Center);
            }
        }
    }

    class InputBox : Control
    {
        public readonly TextBox Box;
        readonly string glyph;
        readonly IconButton eye;
        bool focused;
        public event EventHandler EnterPressed;
        public new event EventHandler TextChanged;

        public InputBox(string glyph, string placeholder, bool password)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            this.glyph = glyph;
            Height = Dpi.D(46);
            Cursor = Cursors.IBeam;

            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Surface2;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.UI(11f);
            Box.UseSystemPasswordChar = password;
            Box.GotFocus += delegate { focused = true; Invalidate(); };
            Box.LostFocus += delegate { focused = false; Invalidate(); };
            Box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (EnterPressed != null) EnterPressed(this, EventArgs.Empty); }
            };
            Box.TextChanged += delegate { if (TextChanged != null) TextChanged(this, EventArgs.Empty); };
            Controls.Add(Box);
            Native.Cue(Box, placeholder);

            if (password)
            {
                eye = new IconButton(Ico.Eye);
                eye.GlyphSize = 11f;
                eye.Click += delegate
                {
                    Box.UseSystemPasswordChar = !Box.UseSystemPasswordChar;
                    eye.Glyph = Box.UseSystemPasswordChar ? Ico.Eye : Ico.EyeOff;
                    eye.Invalidate();
                    Box.Focus();
                };
                Controls.Add(eye);
            }
        }

        public override string Text { get { return Box.Text; } set { Box.Text = value; } }
        public void FocusBox() { Box.Focus(); Box.SelectAll(); }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Box.Focus(); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Box == null) return;
            int left = glyph != null ? Dpi.D(44) : Dpi.D(16);
            int right = eye != null ? Dpi.D(46) : Dpi.D(16);
            Box.SetBounds(left, (Height - Box.Height) / 2, Math.Max(10, Width - left - right), Box.Height);
            if (eye != null) eye.SetBounds(Width - Dpi.D(40), (Height - Dpi.D(32)) / 2, Dpi.D(32), Dpi.D(32));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
            if (focused) G.Fill(g, new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), Dpi.F(12), Theme.A(Theme.Accent, 50));
            G.Fill(g, r, Dpi.F(11), Theme.Surface2);
            G.Stroke(g, r, Dpi.F(11), focused ? Theme.Accent : Theme.Border, focused ? 1.6f : 1f);
            if (glyph != null)
                G.Glyph(g, glyph, 11f, new Rectangle(Dpi.D(12), 0, Dpi.D(24), Height), focused ? Theme.AccentSoft : Theme.Faint);
        }
    }

    class Card : Panel
    {
        public Color Fill = Theme.Panel;
        public float Radius = 16;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            g.Clear(BackColor);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            G.Fill(g, r, Dpi.F(Radius), Fill);
            G.Stroke(g, r, Dpi.F(Radius), Theme.A(Color.White, 10), 1f);
        }
    }

    enum ToastKind { Info, Success, Warn, Error, Busy }

    class Toast : Control
    {
        ToastKind kind;
        readonly Timer hide;

        public Toast()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            Font = Theme.UI(10f);
            hide = new Timer();
            hide.Tick += delegate { hide.Stop(); Visible = false; };
        }

        public void Show(string text, ToastKind k)
        {
            Text = text; kind = k;
            int w = Math.Min(Dpi.D(620), G.TextW(text, Font) + Dpi.D(76));
            Size = new Size(w, Dpi.D(48));
            if (Parent != null) Location = new Point((Parent.Width - w) / 2, Parent.Height - Height - Dpi.D(22));
            Visible = true;
            BringToFront();
            Invalidate();
            hide.Stop();
            if (k != ToastKind.Busy) { hide.Interval = k == ToastKind.Error || k == ToastKind.Warn ? 5500 : 3500; hide.Start(); }
            Update();
        }

        public void HideNow() { hide.Stop(); Visible = false; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            G.Fill(g, r, Dpi.F(14), Theme.Surface3);
            G.Stroke(g, r, Dpi.F(14), Theme.A(Color.White, 22), 1f);
            Color c = kind == ToastKind.Success ? Theme.Success : kind == ToastKind.Warn ? Theme.Warn :
                      kind == ToastKind.Error ? Theme.Danger : Theme.AccentSoft;
            string gl = kind == ToastKind.Success ? Ico.Check : kind == ToastKind.Warn ? Ico.Warning :
                        kind == ToastKind.Error ? Ico.Error : kind == ToastKind.Busy ? Ico.Lock : Ico.Info;
            var circle = new RectangleF(Dpi.D(10), (Height - Dpi.D(28)) / 2f, Dpi.D(28), Dpi.D(28));
            using (var b = new SolidBrush(Theme.A(c, 45))) g.FillEllipse(b, circle);
            G.Glyph(g, gl, 9.5f, Rectangle.Round(circle), c);
            G.Text(g, Text, Font, new Rectangle(Dpi.D(48), 0, Width - Dpi.D(60), Height), Theme.Text, G.Left);
        }
    }

    // ------------------------------------------------------------------ file list

    class FileList : Control
    {
        List<VaultEntry> items = new List<VaultEntry>();
        readonly HashSet<VaultEntry> sel = new HashSet<VaultEntry>();
        int anchor = -1, hover = -1, cursor = -1;
        float scroll, target;
        readonly Timer scrollAnim;
        bool dragThumb;
        int dragY;
        float dragScroll;
        bool dropActive;

        public int SortCol;      // 0 name, 1 size, 2 date
        public bool SortAsc = true;
        public bool VaultEmpty;
        public event EventHandler SelectionChanged, OpenRequested, AddRequested, DeleteRequested, SortChanged;
        public event MouseEventHandler ContextRequested;

        public FileList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Panel;
            TabStop = true;
            scrollAnim = new Timer();
            scrollAnim.Interval = 15;
            scrollAnim.Tick += delegate
            {
                scroll += (target - scroll) * 0.3f;
                if (Math.Abs(target - scroll) < 0.6f) { scroll = target; scrollAnim.Stop(); }
                Invalidate();
            };
        }

        int RowH { get { return Dpi.D(60); } }
        int HeadH { get { return Dpi.D(38); } }
        int ViewH { get { return Math.Max(1, Height - HeadH); } }
        float MaxScroll { get { return Math.Max(0, items.Count * RowH + Dpi.D(8) - ViewH); } }
        int SizeX { get { return Width - Dpi.D(300); } }
        int DateX { get { return Width - Dpi.D(180); } }

        public bool DropActive { get { return dropActive; } set { if (dropActive != value) { dropActive = value; Invalidate(); } } }

        public List<VaultEntry> Selected { get { return items.Where(x => sel.Contains(x)).ToList(); } }

        public void SetItems(List<VaultEntry> list)
        {
            items = list;
            var keep = new HashSet<VaultEntry>(list);
            sel.RemoveWhere(x => !keep.Contains(x));
            hover = -1;
            if (cursor >= items.Count) cursor = items.Count - 1;
            target = Math.Min(target, MaxScroll); scroll = Math.Min(scroll, MaxScroll);
            Invalidate();
            Raise(SelectionChanged);
        }

        public void ClearSelection() { sel.Clear(); Invalidate(); Raise(SelectionChanged); }

        void Raise(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        int IndexAt(Point p)
        {
            if (p.Y < HeadH) return -1;
            int i = (int)((p.Y - HeadH + scroll - Dpi.D(4)) / RowH);
            return i >= 0 && i < items.Count ? i : -1;
        }

        Rectangle ThumbRect()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            float total = items.Count * RowH + Dpi.D(8);
            int h = Math.Max(Dpi.D(36), (int)(ViewH * ViewH / total));
            int y = HeadH + (int)((ViewH - h) * (scroll / MaxScroll));
            return new Rectangle(Width - Dpi.D(10), y, Dpi.D(6), h);
        }

        void ScrollTo(float v) { target = Math.Max(0, Math.Min(MaxScroll, v)); scrollAnim.Start(); }

        void EnsureVisible(int i)
        {
            float top = i * RowH, bottom = top + RowH + Dpi.D(8);
            if (top < target) ScrollTo(top);
            else if (bottom > target + ViewH) ScrollTo(bottom - ViewH);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); target = Math.Min(target, MaxScroll); scroll = Math.Min(scroll, MaxScroll); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            g.Clear(BackColor);

            if (items.Count == 0) { PaintEmpty(g); if (dropActive) PaintDrop(g); return; }

            // header
            var hf = Theme.Semi(8.5f);
            PaintHead(g, "NAME", 0, new Rectangle(Dpi.D(70), 0, SizeX - Dpi.D(80), HeadH), hf, TextFormatFlags.Left);
            PaintHead(g, "GRÖSSE", 1, new Rectangle(SizeX, 0, Dpi.D(110), HeadH), hf, TextFormatFlags.Left);
            PaintHead(g, "HINZUGEFÜGT", 2, new Rectangle(DateX, 0, Dpi.D(160), HeadH), hf, TextFormatFlags.Left);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, Dpi.D(12), HeadH - 1, Width - Dpi.D(12), HeadH - 1);

            g.SetClip(new Rectangle(0, HeadH, Width, ViewH));
            int first = Math.Max(0, (int)(scroll / RowH) - 1);
            int last = Math.Min(items.Count - 1, (int)((scroll + ViewH) / RowH) + 1);
            for (int i = first; i <= last; i++) PaintRow(g, i);

            var thumb = ThumbRect();
            if (thumb != Rectangle.Empty) G.Fill(g, thumb, Dpi.F(3), dragThumb ? Theme.Muted : Theme.Surface3);
            g.ResetClip();
            if (dropActive) PaintDrop(g);
        }

        void PaintHead(Graphics g, string text, int col, Rectangle r, Font f, TextFormatFlags align)
        {
            Color c = SortCol == col ? Theme.Text : Theme.Faint;
            G.Text(g, text, f, r, c, G.Left);
            if (SortCol == col)
            {
                int tw = G.TextW(text, f);
                G.Glyph(g, SortAsc ? Ico.Up : Ico.Down, 7f, new Rectangle(r.X + tw + Dpi.D(2), 0, Dpi.D(16), HeadH), Theme.AccentSoft);
            }
        }

        void PaintRow(Graphics g, int i)
        {
            var en = items[i];
            int y = HeadH + Dpi.D(4) + i * RowH - (int)scroll;
            var row = new RectangleF(Dpi.D(8), y + Dpi.D(2), Width - Dpi.D(26), RowH - Dpi.D(4));
            bool s = sel.Contains(en);
            if (s)
            {
                G.Gradient(g, row, Dpi.F(12), Theme.A(Theme.Accent, 64), Theme.A(Theme.Accent, 26), 0f);
                G.Stroke(g, row, Dpi.F(12), Theme.A(Theme.AccentSoft, 70), 1f);
            }
            else if (i == hover) G.Fill(g, row, Dpi.F(12), Theme.Surface2);

            int k = Kinds.Of(en.Name);
            Color kc = Kinds.Colors[k];
            var badge = new RectangleF(row.X + Dpi.D(12), y + (RowH - Dpi.D(40)) / 2f, Dpi.D(40), Dpi.D(40));
            G.Fill(g, badge, Dpi.F(11), Theme.A(kc, 38));
            G.Glyph(g, Kinds.Glyphs[k], 13f, Rectangle.Round(badge), kc);

            string name = en.Name, folder = "";
            int slash = name.LastIndexOf('/');
            if (slash >= 0) { folder = name.Substring(0, slash); name = name.Substring(slash + 1); }
            string ext = Kinds.ExtOf(en.Name);
            string sub = (ext.Length > 0 ? ext.ToUpperInvariant() + "-Datei" : "Datei") + (folder.Length > 0 ? "   ·   " + folder.Replace("/", " / ") : "");

            int nx = (int)row.X + Dpi.D(64);
            int nw = SizeX - nx - Dpi.D(16);
            G.Text(g, name, Theme.Semi(10.5f), new Rectangle(nx, y + Dpi.D(9), nw, Dpi.D(22)), Theme.Text, G.Left);
            G.Text(g, sub, Theme.UI(9f), new Rectangle(nx, y + Dpi.D(31), nw, Dpi.D(18)), s ? Theme.AccentSoft : Theme.Faint, G.Left);
            G.Text(g, Fmt.Size(en.Size), Theme.UI(9.5f), new Rectangle(SizeX, y, Dpi.D(110), RowH), Theme.Muted, G.Left);
            G.Text(g, Fmt.Date(en.Added), Theme.UI(9.5f), new Rectangle(DateX, y, Dpi.D(160), RowH), Theme.Muted, G.Left);
        }

        Rectangle EmptyZone()
        {
            int w = Math.Min(Width - Dpi.D(40), Dpi.D(520)), h = Math.Min(Height - Dpi.D(40), Dpi.D(300));
            return new Rectangle((Width - w) / 2, (Height - h) / 2 - Dpi.D(10), w, h);
        }

        void PaintEmpty(Graphics g)
        {
            var z = EmptyZone();
            if (VaultEmpty)
            {
                G.Fill(g, z, Dpi.F(20), Theme.A(Theme.Accent, hover == -2 ? 22 : 12));
                using (var p = G.Round(z, Dpi.F(20)))
                using (var pen = new Pen(Theme.A(Theme.AccentSoft, hover == -2 ? 150 : 90), Dpi.F(1.6f)))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, p);
                }
                var c = new RectangleF(z.X + (z.Width - Dpi.D(72)) / 2f, z.Y + Dpi.D(48), Dpi.D(72), Dpi.D(72));
                G.Glow(g, RectangleF.Inflate(c, Dpi.D(30), Dpi.D(30)), Theme.Accent, 70);
                G.Gradient(g, c, Dpi.F(22), Theme.Accent, Theme.Accent2, 45f);
                G.Glyph(g, Ico.Upload, 20f, Rectangle.Round(c), Color.White);
                G.Text(g, "Dein Tresor ist noch leer", Theme.Bold(14f), new Rectangle(z.X, (int)c.Bottom + Dpi.D(20), z.Width, Dpi.D(30)), Theme.Text, G.Center);
                G.Text(g, "Zieh Dateien oder ganze Ordner hierher – oder klick, um Dateien auszuwählen.",
                       Theme.UI(10f), new Rectangle(z.X + Dpi.D(30), (int)c.Bottom + Dpi.D(54), z.Width - Dpi.D(60), Dpi.D(44)), Theme.Muted, G.Wrap);
            }
            else
            {
                var c = new RectangleF((Width - Dpi.D(64)) / 2f, Height / 2f - Dpi.D(70), Dpi.D(64), Dpi.D(64));
                G.Fill(g, c, Dpi.F(20), Theme.Surface2);
                G.Glyph(g, Ico.Search, 18f, Rectangle.Round(c), Theme.Muted);
                G.Text(g, "Nichts gefunden", Theme.Bold(13f), new Rectangle(0, (int)c.Bottom + Dpi.D(16), Width, Dpi.D(28)), Theme.Text, G.Center);
                G.Text(g, "Probier einen anderen Suchbegriff oder eine andere Kategorie.", Theme.UI(10f),
                       new Rectangle(0, (int)c.Bottom + Dpi.D(46), Width, Dpi.D(24)), Theme.Muted, G.Center);
            }
        }

        void PaintDrop(Graphics g)
        {
            var r = new RectangleF(Dpi.D(6), Dpi.D(6), Width - Dpi.D(12), Height - Dpi.D(12));
            G.Fill(g, r, Dpi.F(18), Theme.A(Theme.Bg, 200));
            G.Fill(g, r, Dpi.F(18), Theme.A(Theme.Accent, 40));
            using (var p = G.Round(r, Dpi.F(18)))
            using (var pen = new Pen(Theme.AccentSoft, Dpi.F(2f)))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawPath(pen, p);
            }
            var c = new RectangleF((Width - Dpi.D(72)) / 2f, Height / 2f - Dpi.D(70), Dpi.D(72), Dpi.D(72));
            G.Gradient(g, c, Dpi.F(22), Theme.Accent, Theme.Accent2, 45f);
            G.Glyph(g, Ico.Lock, 20f, Rectangle.Round(c), Color.White);
            G.Text(g, "Loslassen zum Verschlüsseln", Theme.Bold(14f), new Rectangle(0, (int)c.Bottom + Dpi.D(18), Width, Dpi.D(30)), Theme.Text, G.Center);
        }

        // ---- input
        protected override bool IsInputKey(Keys k)
        {
            Keys key = k & Keys.KeyCode;
            return key == Keys.Up || key == Keys.Down || key == Keys.Home || key == Keys.End || base.IsInputKey(k);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (items.Count == 0)
            {
                if (e.Button == MouseButtons.Left && VaultEmpty && EmptyZone().Contains(e.Location)) Raise(AddRequested);
                return;
            }
            var thumb = ThumbRect();
            if (e.Button == MouseButtons.Left && thumb != Rectangle.Empty && e.X >= thumb.X - Dpi.D(6))
            {
                if (thumb.Contains(e.X + Dpi.D(2), e.Y)) { dragThumb = true; dragY = e.Y; dragScroll = scroll; }
                else ScrollTo(target + (e.Y < thumb.Y ? -ViewH : ViewH));
                Invalidate();
                return;
            }
            if (e.Y < HeadH)
            {
                if (e.Button != MouseButtons.Left) return;
                int col = e.X >= DateX ? 2 : e.X >= SizeX ? 1 : 0;
                if (col == SortCol) SortAsc = !SortAsc; else { SortCol = col; SortAsc = col == 0; }
                Raise(SortChanged);
                return;
            }
            int i = IndexAt(e.Location);
            bool ctrl = (ModifierKeys & Keys.Control) != 0, shift = (ModifierKeys & Keys.Shift) != 0;
            if (i < 0)
            {
                if (e.Button == MouseButtons.Left && !ctrl) { sel.Clear(); Raise(SelectionChanged); }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (!sel.Contains(items[i])) { sel.Clear(); sel.Add(items[i]); anchor = cursor = i; Raise(SelectionChanged); }
            }
            else if (shift && anchor >= 0)
            {
                if (!ctrl) sel.Clear();
                for (int j = Math.Min(anchor, i); j <= Math.Max(anchor, i); j++) sel.Add(items[j]);
                cursor = i;
                Raise(SelectionChanged);
            }
            else if (ctrl)
            {
                if (!sel.Remove(items[i])) sel.Add(items[i]);
                anchor = cursor = i;
                Raise(SelectionChanged);
            }
            else
            {
                sel.Clear(); sel.Add(items[i]); anchor = cursor = i;
                Raise(SelectionChanged);
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragThumb) { dragThumb = false; Invalidate(); }
            if (e.Button == MouseButtons.Right && ContextRequested != null && IndexAt(e.Location) >= 0) ContextRequested(this, e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragThumb)
            {
                var t = ThumbRect();
                float range = ViewH - t.Height;
                if (range > 0) { scroll = target = Math.Max(0, Math.Min(MaxScroll, dragScroll + (e.Y - dragY) * MaxScroll / range)); Invalidate(); }
                return;
            }
            int h = items.Count == 0 ? (VaultEmpty && EmptyZone().Contains(e.Location) ? -2 : -1) : IndexAt(e.Location);
            Cursor = h == -2 ? Cursors.Hand : (e.Y < HeadH && items.Count > 0 ? Cursors.Hand : Cursors.Default);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover != -1) { hover = -1; Invalidate(); } }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && IndexAt(e.Location) >= 0) Raise(OpenRequested);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollTo(target - e.Delta / 120f * RowH * 1.5f);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (items.Count == 0) return;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || e.KeyCode == Keys.Home || e.KeyCode == Keys.End)
            {
                int c = cursor < 0 ? 0 : cursor;
                if (e.KeyCode == Keys.Down) c = Math.Min(items.Count - 1, cursor + 1);
                else if (e.KeyCode == Keys.Up) c = Math.Max(0, cursor - 1);
                else if (e.KeyCode == Keys.Home) c = 0;
                else c = items.Count - 1;
                if (e.Shift && anchor >= 0)
                {
                    sel.Clear();
                    for (int j = Math.Min(anchor, c); j <= Math.Max(anchor, c); j++) sel.Add(items[j]);
                }
                else { sel.Clear(); sel.Add(items[c]); anchor = c; }
                cursor = c;
                EnsureVisible(c);
                Invalidate();
                Raise(SelectionChanged);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter) { Raise(OpenRequested); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { Raise(DeleteRequested); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (var x in items) sel.Add(x);
                Invalidate();
                Raise(SelectionChanged);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape) ClearSelection();
        }

        protected override void Dispose(bool disposing) { if (disposing) scrollAnim.Dispose(); base.Dispose(disposing); }
    }

    // ------------------------------------------------------------------ menu look

    class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface2; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface2; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface2; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface2; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Surface3; } }
        public override Color MenuItemSelected { get { return Theme.Surface3; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Surface3; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Surface3; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Tag as string == "danger" ? Theme.Danger : (e.Item.Enabled ? Theme.Text : Theme.Faint);
            base.OnRenderItemText(e);
        }
    }

    // ================================================================== windows

    class ModernForm : Form
    {
        public int CaptionHeight;
        public bool Resizable;

        public ModernForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.UI(10f);
            Icon = Art.AppIcon;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            CaptionHeight = Dpi.D(44);
            KeyPreview = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;                 // CS_DROPSHADOW
                cp.Style |= 0x00020000 | 0x00080000;      // WS_MINIMIZEBOX | WS_SYSMENU (taskbar minimize)
                if (Resizable) cp.Style |= 0x00010000;    // WS_MAXIMIZEBOX
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; Native.DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { } // Win11 rounded corners
            try { int v = 1; Native.DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
            UpdateMaxBounds();
        }

        protected void UpdateMaxBounds()
        {
            try
            {
                var s = Screen.FromHandle(Handle);
                MaximizedBounds = new Rectangle(s.WorkingArea.X - s.Bounds.X, s.WorkingArea.Y - s.Bounds.Y, s.WorkingArea.Width, s.WorkingArea.Height);
            }
            catch { }
        }

        protected override void OnMove(EventArgs e) { base.OnMove(e); if (IsHandleCreated) UpdateMaxBounds(); }

        public void ToggleMax()
        {
            UpdateMaxBounds();
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84 && (int)m.Result == 1) // WM_NCHITTEST -> HTCLIENT
            {
                long lp = m.LParam.ToInt64();
                var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int b = Dpi.D(7);
                if (Resizable && WindowState == FormWindowState.Normal)
                {
                    bool l = p.X < b, r = p.X >= Width - b, t = p.Y < b, bo = p.Y >= Height - b;
                    if (t && l) { m.Result = (IntPtr)13; return; }
                    if (t && r) { m.Result = (IntPtr)14; return; }
                    if (bo && l) { m.Result = (IntPtr)16; return; }
                    if (bo && r) { m.Result = (IntPtr)17; return; }
                    if (l) { m.Result = (IntPtr)10; return; }
                    if (r) { m.Result = (IntPtr)11; return; }
                    if (t) { m.Result = (IntPtr)12; return; }
                    if (bo) { m.Result = (IntPtr)15; return; }
                }
                if (p.Y < CaptionHeight) m.Result = (IntPtr)2; // HTCAPTION: drag + snap + double click
            }
        }

        protected IconButton CaptionButton(string glyph, bool close)
        {
            var b = new IconButton(glyph);
            b.CloseStyle = close;
            b.GlyphSize = 8f;
            b.Size = new Size(Dpi.D(46), Dpi.D(34));
            Controls.Add(b);
            return b;
        }

        protected void PaintFrame(Graphics g)
        {
            if (WindowState == FormWindowState.Maximized) return;
            using (var p = new Pen(Theme.A(Color.White, 16))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    enum DlgKind { Info, Question, Warn, Danger, Success }

    class ModernDialog : ModernForm
    {
        readonly string title, message;
        readonly DlgKind kind;
        readonly Rectangle msgRect;
        int result = -1;

        ModernDialog(string title, string message, DlgKind kind, string[] buttons, int primary)
        {
            this.title = title; this.message = message; this.kind = kind;
            Text = title;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            int W = Dpi.D(460), pad = Dpi.D(26), tx = Dpi.D(88);
            var ms = TextRenderer.MeasureText(message, Theme.UI(10f), new Size(W - tx - pad, 2000), G.WrapLeft | TextFormatFlags.NoPrefix);
            msgRect = new Rectangle(tx, Dpi.D(58), W - tx - pad, ms.Height + Dpi.D(4));
            int by = Math.Max(Dpi.D(96), msgRect.Bottom + Dpi.D(26));
            ClientSize = new Size(W, by + Dpi.D(40) + pad);
            CaptionHeight = ClientSize.Height;

            int x = W - pad;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                BtnKind bk = i == primary ? (kind == DlgKind.Danger ? BtnKind.Danger : BtnKind.Primary) : BtnKind.Secondary;
                var b = new FlatButton(buttons[i], null, bk);
                b.Width = Math.Max(Dpi.D(104), b.PreferredW());
                x -= b.Width;
                b.Location = new Point(x, by);
                x -= Dpi.D(10);
                int idx = i;
                b.Click += delegate { result = idx; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(b);
                if (i == primary) AcceptButtonIndex = idx;
            }
        }

        int AcceptButtonIndex = -1;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { result = -1; Close(); }
            else if (e.KeyCode == Keys.Enter && AcceptButtonIndex >= 0) { result = AcceptButtonIndex; DialogResult = DialogResult.OK; Close(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            Color c = kind == DlgKind.Danger ? Theme.Danger : kind == DlgKind.Warn ? Theme.Warn :
                      kind == DlgKind.Success ? Theme.Success : Theme.AccentSoft;
            string gl = kind == DlgKind.Danger ? Ico.Delete : kind == DlgKind.Warn ? Ico.Warning :
                        kind == DlgKind.Success ? Ico.Check : kind == DlgKind.Question ? Ico.Lock : Ico.Info;
            var circle = new RectangleF(Dpi.D(26), Dpi.D(26), Dpi.D(46), Dpi.D(46));
            G.Glow(g, RectangleF.Inflate(circle, Dpi.D(18), Dpi.D(18)), c, 45);
            G.Fill(g, circle, Dpi.F(14), Theme.A(c, 40));
            G.Glyph(g, gl, 14f, Rectangle.Round(circle), c);
            G.Text(g, title, Theme.Bold(13f), new Rectangle(Dpi.D(88), Dpi.D(24), Width - Dpi.D(110), Dpi.D(30)), Theme.Text, G.Left);
            G.Text(g, message, Theme.UI(10f), msgRect, Theme.Muted, G.WrapLeft);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        public static int Ask(IWin32Window owner, string title, string message, DlgKind kind, string[] buttons, int primary)
        {
            using (var d = new ModernDialog(title, message, kind, buttons, primary))
            {
                if (owner == null) d.StartPosition = FormStartPosition.CenterScreen;
                d.ShowDialog(owner);
                return d.result;
            }
        }

        public static void Tell(IWin32Window owner, string title, string message, DlgKind kind)
        {
            Ask(owner, title, message, kind, new[] { "OK" }, 0);
        }
    }

    // ------------------------------------------------------------------ login

    class LoginForm : ModernForm
    {
        readonly Vault vault;
        readonly bool create;
        readonly InputBox pw, pw2;
        readonly FlatButton go;
        readonly Rectangle errRect;
        readonly int strengthY;
        string error = "";
        int fails, strength;
        DateTime lockedUntil = DateTime.MinValue;
        readonly Timer cooldown, shake;
        int shakeFrame, shakeBase;

        public LoginForm(Vault v)
        {
            vault = v;
            create = !v.Exists;
            Text = "un0yy Vault";
            StartPosition = FormStartPosition.CenterScreen;
            int W = Dpi.D(440);
            ClientSize = new Size(W, Dpi.D(create ? 640 : 520));
            CaptionHeight = ClientSize.Height;

            var close = CaptionButton(Ico.Close, true);
            close.Location = new Point(W - close.Width, 0);
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            var min = CaptionButton(Ico.Min, false);
            min.Location = new Point(W - close.Width * 2, 0);
            min.Click += delegate { WindowState = FormWindowState.Minimized; };

            int x = Dpi.D(44), w = W - Dpi.D(88), y = Dpi.D(262);
            pw = new InputBox(Ico.Key, create ? "Neues Master-Passwort" : "Master-Passwort", true);
            pw.SetBounds(x, y, w, Dpi.D(50));
            pw.EnterPressed += delegate { if (pw2 != null) pw2.FocusBox(); else Submit(); };
            Controls.Add(pw);
            y += Dpi.D(62);

            if (create)
            {
                pw2 = new InputBox(Ico.Check, "Passwort wiederholen", true);
                pw2.SetBounds(x, y, w, Dpi.D(50));
                pw2.EnterPressed += delegate { Submit(); };
                Controls.Add(pw2);
                y += Dpi.D(62);
                strengthY = y;
                pw.TextChanged += delegate { strength = Score(pw.Text); Invalidate(); };
                y += Dpi.D(34);
            }

            go = new FlatButton(create ? "Tresor erstellen" : "Entsperren", create ? Ico.Shield : Ico.Unlock, BtnKind.Primary);
            go.Font = Theme.Semi(11f);
            go.SetBounds(x, y, w, Dpi.D(52));
            go.Click += delegate { Submit(); };
            Controls.Add(go);
            y += Dpi.D(62);
            errRect = new Rectangle(x, y, w, Dpi.D(40));

            cooldown = new Timer();
            cooldown.Interval = 200;
            cooldown.Tick += delegate
            {
                double rem = (lockedUntil - DateTime.Now).TotalSeconds;
                if (rem <= 0) { cooldown.Stop(); go.Enabled = true; SetError("Falsches Passwort – versuch es nochmal."); pw.FocusBox(); }
                else SetError("Zu viele Fehlversuche – warte " + Math.Ceiling(rem) + " Sekunden.");
            };

            shake = new Timer();
            shake.Interval = 16;
            shake.Tick += delegate
            {
                shakeFrame++;
                if (shakeFrame > 14) { shake.Stop(); Left = shakeBase; return; }
                Left = shakeBase + (int)(Math.Sin(shakeFrame * 1.3) * Dpi.D(9) * (1 - shakeFrame / 15.0));
            };

            Shown += delegate { pw.FocusBox(); };
        }

        static int Score(string p)
        {
            if (p.Length == 0) return 0;
            int classes = (p.Any(char.IsLower) ? 1 : 0) + (p.Any(char.IsUpper) ? 1 : 0) + (p.Any(char.IsDigit) ? 1 : 0) +
                          (p.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);
            int s = 1;
            if (p.Length >= 8 && classes >= 2) s = 2;
            if (p.Length >= 12 && classes >= 3) s = 3;
            if (p.Length >= 16 && classes >= 3 || p.Length >= 12 && classes == 4) s = 4;
            if (p.Length < 8) s = 1;
            return s;
        }

        void SetError(string e) { error = e; Invalidate(errRect); }

        void Shake()
        {
            if (shake.Enabled) return;
            shakeBase = Left; shakeFrame = 0; shake.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            int W = ClientSize.Width;
            G.Glow(g, new RectangleF(-W * 0.3f, -Dpi.D(260), W * 1.6f, Dpi.D(560)), Theme.Accent, 60);
            G.Glow(g, new RectangleF(W * 0.35f, Height - Dpi.D(220), W, Dpi.D(380)), Theme.Accent2, 26);

            var logo = new RectangleF((W - Dpi.D(84)) / 2f, Dpi.D(62), Dpi.D(84), Dpi.D(84));
            G.Glow(g, RectangleF.Inflate(logo, Dpi.D(46), Dpi.D(46)), Theme.Accent, 95);
            Art.DrawLock(g, logo);

            G.Text(g, "un0yy Vault", Theme.Bold(22f), new Rectangle(0, Dpi.D(160), W, Dpi.D(44)), Theme.Text, G.Center);
            G.Text(g, create ? "Erstelle deinen privaten, verschlüsselten Tresor." : "Willkommen zurück – entsperre deinen Tresor.",
                   Theme.UI(10.5f), new Rectangle(0, Dpi.D(206), W, Dpi.D(26)), Theme.Muted, G.Center);

            int x = Dpi.D(44), w = W - Dpi.D(88);
            if (create)
            {
                int seg = (w - Dpi.D(18)) / 4;
                Color[] cols = { Theme.Danger, Theme.Warn, Theme.Accent2, Theme.Success };
                string[] labels = { "Zu schwach", "Okay", "Gut", "Sehr stark" };
                for (int i = 0; i < 4; i++)
                    G.Fill(g, new RectangleF(x + i * (seg + Dpi.D(6)), strengthY, seg, Dpi.D(5)), Dpi.F(3),
                           i < strength ? cols[strength - 1] : Theme.Surface2);
                string lbl = strength == 0 ? "Mind. 8 Zeichen · Zahlen & Sonderzeichen machen es stärker"
                                           : "Passwortstärke: " + labels[strength - 1];
                G.Text(g, lbl, Theme.UI(8.5f), new Rectangle(x, strengthY + Dpi.D(9), w, Dpi.D(20)),
                       strength == 0 ? Theme.Faint : cols[strength - 1], G.Left);
            }

            if (error.Length > 0)
            {
                G.Glyph(g, Ico.Error, 10f, new Rectangle(errRect.X, errRect.Y, Dpi.D(22), errRect.Height), Theme.Danger);
                G.Text(g, error, Theme.UI(9.5f), new Rectangle(errRect.X + Dpi.D(28), errRect.Y, errRect.Width - Dpi.D(28), errRect.Height), Theme.Danger,
                       TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }

            if (create)
            {
                var box = new RectangleF(x, errRect.Bottom + Dpi.D(6), w, Dpi.D(62));
                G.Fill(g, box, Dpi.F(12), Theme.A(Theme.Warn, 22));
                G.Stroke(g, box, Dpi.F(12), Theme.A(Theme.Warn, 70), 1f);
                G.Glyph(g, Ico.Warning, 12f, new Rectangle(x + Dpi.D(10), (int)box.Y, Dpi.D(28), (int)box.Height), Theme.Warn);
                G.Text(g, "Merk dir dein Passwort gut! Wenn du es vergisst, kann NIEMAND deine Dateien wiederherstellen – auch du nicht.",
                       Theme.UI(9f), new Rectangle(x + Dpi.D(44), (int)box.Y + Dpi.D(10), w - Dpi.D(56), (int)box.Height - Dpi.D(12)),
                       Theme.Mix(Theme.Warn, Color.White, 0.35f), G.WrapLeft);
            }

            int fy = Height - Dpi.D(40);
            string foot = "AES-256   ·   Nur auf deinem PC   ·   Ohne Internet";
            int fw = G.TextW(foot, Theme.UI(8.5f)) + Dpi.D(22);
            int fx = (W - fw) / 2;
            G.Glyph(g, Ico.Shield, 9f, new Rectangle(fx, fy, Dpi.D(16), Dpi.D(20)), Theme.Success);
            G.Text(g, foot, Theme.UI(8.5f), new Rectangle(fx + Dpi.D(22), fy, fw, Dpi.D(20)), Theme.Faint, G.Left);
            PaintFrame(g);
        }

        void Submit()
        {
            if (DateTime.Now < lockedUntil || !go.Enabled) return;
            SetError("");
            if (create)
            {
                if (pw.Text.Length < 8) { SetError("Das Passwort muss mindestens 8 Zeichen lang sein."); Shake(); return; }
                if (pw.Text != pw2.Text) { SetError("Die Passwörter stimmen nicht überein."); Shake(); pw2.FocusBox(); return; }
                go.Enabled = false; go.Text = "Tresor wird erstellt…"; Cursor = Cursors.WaitCursor; Update();
                try { vault.Create(pw.Text); }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default; go.Enabled = true; go.Text = "Tresor erstellen";
                    SetError("Fehler: " + ex.Message); return;
                }
                Cursor = Cursors.Default;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            go.Enabled = false; go.Text = "Wird entsperrt…"; Cursor = Cursors.WaitCursor; Update();
            bool ok = false;
            string problem = null;
            try { ok = vault.Unlock(pw.Text); }
            catch (Exception ex) { problem = ex.Message; }
            Cursor = Cursors.Default; go.Enabled = true; go.Text = "Entsperren";

            if (ok) { DialogResult = DialogResult.OK; Close(); return; }
            if (problem != null) { SetError("Tresor konnte nicht geöffnet werden: " + problem); return; }

            fails++;
            Shake();
            pw.FocusBox();
            int wait = fails >= 3 ? Math.Min(60, 1 << (fails - 2)) : 0;
            if (wait > 0)
            {
                lockedUntil = DateTime.Now.AddSeconds(wait);
                go.Enabled = false;
                SetError("Zu viele Fehlversuche – warte " + wait + " Sekunden.");
                cooldown.Start();
            }
            else SetError("Falsches Passwort.");
        }
    }

    // ------------------------------------------------------------------ change password

    class ChangePasswordForm : ModernForm
    {
        readonly Vault vault;
        readonly InputBox oldPw, newPw, newPw2;
        readonly Rectangle errRect;
        string error = "";

        public ChangePasswordForm(Vault v)
        {
            vault = v;
            Text = "Passwort ändern";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            int W = Dpi.D(440), x = Dpi.D(30), w = W - Dpi.D(60);
            ClientSize = new Size(W, Dpi.D(430));
            CaptionHeight = ClientSize.Height;

            var close = CaptionButton(Ico.Close, true);
            close.Location = new Point(W - close.Width, 0);
            close.Click += delegate { Close(); };

            int y = Dpi.D(112);
            oldPw = new InputBox(Ico.Lock, "Aktuelles Passwort", true); oldPw.SetBounds(x, y, w, Dpi.D(48)); y += Dpi.D(60);
            newPw = new InputBox(Ico.Key, "Neues Passwort (mind. 8 Zeichen)", true); newPw.SetBounds(x, y, w, Dpi.D(48)); y += Dpi.D(60);
            newPw2 = new InputBox(Ico.Check, "Neues Passwort wiederholen", true); newPw2.SetBounds(x, y, w, Dpi.D(48)); y += Dpi.D(56);
            oldPw.EnterPressed += delegate { newPw.FocusBox(); };
            newPw.EnterPressed += delegate { newPw2.FocusBox(); };
            newPw2.EnterPressed += delegate { Save(); };
            Controls.Add(oldPw); Controls.Add(newPw); Controls.Add(newPw2);
            errRect = new Rectangle(x, y, w, Dpi.D(30));
            y += Dpi.D(38);

            var cancel = new FlatButton("Abbrechen", null, BtnKind.Secondary);
            var save = new FlatButton("Speichern", Ico.Check, BtnKind.Primary);
            int bw = (w - Dpi.D(12)) / 2;
            cancel.SetBounds(x, y, bw, Dpi.D(44));
            save.SetBounds(x + bw + Dpi.D(12), y, bw, Dpi.D(44));
            cancel.Click += delegate { Close(); };
            save.Click += delegate { Save(); };
            Controls.Add(cancel); Controls.Add(save);
            Shown += delegate { oldPw.FocusBox(); };
        }

        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) Close(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var circle = new RectangleF(Dpi.D(30), Dpi.D(32), Dpi.D(46), Dpi.D(46));
            G.Glow(g, RectangleF.Inflate(circle, Dpi.D(20), Dpi.D(20)), Theme.Accent, 60);
            G.Gradient(g, circle, Dpi.F(14), Theme.Accent, Theme.Accent2, 45f);
            G.Glyph(g, Ico.Key, 14f, Rectangle.Round(circle), Color.White);
            G.Text(g, "Passwort ändern", Theme.Bold(14f), new Rectangle(Dpi.D(90), Dpi.D(30), Width - Dpi.D(120), Dpi.D(28)), Theme.Text, G.Left);
            G.Text(g, "Deine Dateien bleiben dabei unverändert.", Theme.UI(9.5f), new Rectangle(Dpi.D(90), Dpi.D(58), Width - Dpi.D(120), Dpi.D(22)), Theme.Muted, G.Left);
            if (error.Length > 0)
            {
                G.Glyph(g, Ico.Error, 10f, new Rectangle(errRect.X, errRect.Y, Dpi.D(22), errRect.Height), Theme.Danger);
                G.Text(g, error, Theme.UI(9.5f), new Rectangle(errRect.X + Dpi.D(28), errRect.Y, errRect.Width - Dpi.D(28), errRect.Height), Theme.Danger, G.Left);
            }
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        void Save()
        {
            error = "";
            if (newPw.Text.Length < 8) error = "Das neue Passwort braucht mindestens 8 Zeichen.";
            else if (newPw.Text != newPw2.Text) error = "Die neuen Passwörter stimmen nicht überein.";
            if (error.Length > 0) { Invalidate(); return; }
            Cursor = Cursors.WaitCursor;
            bool ok;
            try { ok = vault.ChangePassword(oldPw.Text, newPw.Text); }
            catch (Exception ex) { Cursor = Cursors.Default; error = "Fehler: " + ex.Message; Invalidate(); return; }
            Cursor = Cursors.Default;
            if (!ok) { error = "Das aktuelle Passwort ist falsch."; Invalidate(); oldPw.FocusBox(); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ------------------------------------------------------------------ main window

    class SidebarCard : Card
    {
        public string Files = "", SizeText = "";

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            G.Text(g, "BIBLIOTHEK", Theme.Semi(8f), new Rectangle(Dpi.D(22), Dpi.D(14), Width, Dpi.D(24)), Theme.Faint, G.Left);

            var box = new RectangleF(Dpi.D(12), Height - Dpi.D(212), Width - Dpi.D(24), Dpi.D(100));
            G.Gradient(g, box, Dpi.F(14), Theme.A(Theme.Success, 30), Theme.A(Theme.Accent2, 14), 30f);
            G.Stroke(g, box, Dpi.F(14), Theme.A(Theme.Success, 60), 1f);
            var c = new RectangleF(box.X + Dpi.D(14), box.Y + Dpi.D(16), Dpi.D(34), Dpi.D(34));
            G.Fill(g, c, Dpi.F(11), Theme.A(Theme.Success, 50));
            G.Glyph(g, Ico.Shield, 11f, Rectangle.Round(c), Theme.Success);
            G.Text(g, "Geschützt", Theme.Semi(10.5f), new Rectangle((int)c.Right + Dpi.D(10), (int)c.Y - Dpi.D(2), Width, Dpi.D(20)), Theme.Text, G.Left);
            G.Text(g, "AES-256 verschlüsselt", Theme.UI(8.5f), new Rectangle((int)c.Right + Dpi.D(10), (int)c.Y + Dpi.D(17), Width, Dpi.D(18)), Theme.Muted, G.Left);
            G.Text(g, Files + "   ·   " + SizeText, Theme.UI(9f), new Rectangle((int)box.X + Dpi.D(14), (int)box.Y + Dpi.D(62), (int)box.Width - Dpi.D(20), Dpi.D(24)), Theme.Text, G.Left);
        }
    }

    class ContentCard : Card
    {
        public string Title = "", Subtitle = "", Hint = "", Selection = "";

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            G.Text(g, Title, Theme.Bold(18f), new Rectangle(Dpi.D(28), Dpi.D(20), Width / 2, Dpi.D(38)), Theme.Text, G.Left);
            G.Text(g, Subtitle, Theme.UI(9.5f), new Rectangle(Dpi.D(29), Dpi.D(56), Width / 2, Dpi.D(22)), Theme.Muted, G.Left);
            var row = new Rectangle(Dpi.D(28), Dpi.D(92), Width - Dpi.D(56), Dpi.D(40));
            if (Selection.Length > 0)
            {
                var pill = new RectangleF(row.X, row.Y + Dpi.D(4), G.TextW(Selection, Theme.Semi(9.5f)) + Dpi.D(40), Dpi.D(32));
                G.Fill(g, pill, Dpi.F(16), Theme.A(Theme.Accent, 60));
                G.Glyph(g, Ico.Check, 9f, new Rectangle((int)pill.X + Dpi.D(8), (int)pill.Y, Dpi.D(20), (int)pill.Height), Theme.AccentSoft);
                G.Text(g, Selection, Theme.Semi(9.5f), new Rectangle((int)pill.X + Dpi.D(28), (int)pill.Y, (int)pill.Width, (int)pill.Height), Theme.Text, G.Left);
            }
            else if (Hint.Length > 0)
            {
                G.Glyph(g, Ico.Info, 9.5f, new Rectangle(row.X, row.Y, Dpi.D(18), row.Height), Theme.Faint);
                G.Text(g, Hint, Theme.UI(9f), new Rectangle(row.X + Dpi.D(26), row.Y, row.Width, row.Height), Theme.Faint, G.Left);
            }
        }

        public int SelectionPillRight() { return Dpi.D(28) + G.TextW(Selection, Theme.Semi(9.5f)) + Dpi.D(40); }
    }

    class MainForm : ModernForm, IMessageFilter
    {
        const int AutoLockMinutes = 10;
        readonly Vault vault;
        public bool LockRequested;
        readonly SidebarCard side;
        readonly ContentCard content;
        readonly NavItem[] nav = new NavItem[7];
        readonly FileList list;
        readonly InputBox search;
        readonly FlatButton bAdd, bOpen, bExport, bDel, bPw, bLock;
        readonly IconButton cClose, cMax, cMin;
        readonly Toast toast;
        readonly ContextMenuStrip menu;
        int category;
        string tempDir;
        DateTime lastActivity = DateTime.Now;
        readonly Timer idle;

        public MainForm(Vault v)
        {
            vault = v;
            Text = "un0yy Vault";
            Resizable = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Dpi.D(1120), Dpi.D(700));
            MinimumSize = new Size(Dpi.D(900), Dpi.D(580));
            CaptionHeight = Dpi.D(50);
            AllowDrop = true;

            cClose = CaptionButton(Ico.Close, true);
            cMax = CaptionButton(Ico.Max, false);
            cMin = CaptionButton(Ico.Min, false);
            cClose.Click += delegate { Close(); };
            cMax.Click += delegate { ToggleMax(); };
            cMin.Click += delegate { WindowState = FormWindowState.Minimized; };

            // sidebar
            side = new SidebarCard();
            Controls.Add(side);
            for (int i = 0; i < nav.Length; i++)
            {
                var n = new NavItem(Kinds.Names[i], Kinds.Glyphs[i], Kinds.Colors[i]);
                int idx = i;
                n.Click += delegate { SelectCategory(idx); };
                nav[i] = n;
                side.Controls.Add(n);
            }
            nav[0].Selected = true;
            bPw = new FlatButton("Passwort ändern", Ico.Key, BtnKind.Ghost);
            bLock = new FlatButton("Tresor sperren", Ico.Lock, BtnKind.DangerGhost);
            bPw.Click += delegate
            {
                using (var f = new ChangePasswordForm(vault))
                    if (f.ShowDialog(this) == DialogResult.OK) toast.Show("Dein Passwort wurde geändert.", ToastKind.Success);
            };
            bLock.Click += delegate { DoLock(); };
            side.Controls.Add(bPw);
            side.Controls.Add(bLock);

            // content
            content = new ContentCard();
            content.Hint = "Doppelklick öffnet eine Datei   ·   Rechtsklick für mehr   ·   Strg+A wählt alles aus";
            Controls.Add(content);
            search = new InputBox(Ico.Search, "Dateien suchen…", false);
            search.TextChanged += delegate { RefreshList(); };
            bAdd = new FlatButton("Hinzufügen", Ico.Add, BtnKind.Primary);
            bAdd.Click += delegate { AddViaDialog(); };
            bOpen = new FlatButton("Öffnen", Ico.Open, BtnKind.Secondary);
            bExport = new FlatButton("Exportieren", Ico.Export, BtnKind.Secondary);
            bDel = new FlatButton("Löschen", Ico.Delete, BtnKind.DangerGhost);
            foreach (var b in new[] { bOpen, bExport, bDel }) b.Height = Dpi.D(36);
            bOpen.Click += delegate { OpenSelected(); };
            bExport.Click += delegate { ExportSelected(); };
            bDel.Click += delegate { DeleteSelected(); };

            list = new FileList();
            list.AllowDrop = true;
            list.SelectionChanged += delegate { UpdateSelectionUi(); };
            list.OpenRequested += delegate { OpenSelected(); };
            list.DeleteRequested += delegate { DeleteSelected(); };
            list.AddRequested += delegate { AddViaDialog(); };
            list.SortChanged += delegate { RefreshList(); };
            list.ContextRequested += delegate(object s, MouseEventArgs e) { menu.Show(list, e.Location); };

            toast = new Toast();
            content.Controls.AddRange(new Control[] { search, bAdd, bOpen, bExport, bDel, list, toast });

            menu = new ContextMenuStrip();
            menu.Renderer = new DarkRenderer();
            menu.ShowImageMargin = false;
            menu.Font = Theme.UI(10f);
            menu.Items.Add(MenuItem("Öffnen", delegate { OpenSelected(); }, null));
            menu.Items.Add(MenuItem("Entschlüsselt speichern unter…", delegate { ExportSelected(); }, null));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem("Aus dem Tresor löschen", delegate { DeleteSelected(); }, "danger"));

            foreach (Control c in new Control[] { this, content, list })
            {
                c.AllowDrop = true;
                c.DragEnter += OnDragEnter;
                c.DragOver += OnDragEnter;
                c.DragLeave += delegate { list.DropActive = false; };
                c.DragDrop += OnDragDrop;
            }

            idle = new Timer();
            idle.Interval = 15000;
            idle.Tick += delegate
            {
                if ((DateTime.Now - lastActivity).TotalMinutes >= AutoLockMinutes && CanFocus) DoLock();
            };
            idle.Start();
            Application.AddMessageFilter(this);

            LayoutAll();
            RefreshList();
            Resize += delegate { LayoutAll(); };
            Shown += delegate { list.Focus(); };
        }

        static ToolStripMenuItem MenuItem(string text, EventHandler click, string tag)
        {
            var it = new ToolStripMenuItem(text);
            it.Click += click;
            it.Tag = tag;
            it.Padding = new Padding(Dpi.D(6), Dpi.D(5), Dpi.D(6), Dpi.D(5));
            return it;
        }

        void LayoutAll()
        {
            int W = ClientSize.Width, H = ClientSize.Height;
            int m = Dpi.D(10), top = Dpi.D(50);
            cClose.Location = new Point(W - cClose.Width, 0);
            cMax.Location = new Point(W - cClose.Width * 2, 0);
            cMin.Location = new Point(W - cClose.Width * 3, 0);
            cMax.Glyph = WindowState == FormWindowState.Maximized ? Ico.Restore : Ico.Max;
            cMax.Invalidate();

            int sw = Dpi.D(250);
            side.SetBounds(m, top, sw, H - top - m);
            int nx = Dpi.D(10), nw = sw - Dpi.D(20);
            for (int i = 0; i < nav.Length; i++) nav[i].SetBounds(nx, Dpi.D(42) + i * Dpi.D(44), nw, Dpi.D(40));
            bPw.SetBounds(Dpi.D(12), side.Height - Dpi.D(102), sw - Dpi.D(24), Dpi.D(40));
            bLock.SetBounds(Dpi.D(12), side.Height - Dpi.D(56), sw - Dpi.D(24), Dpi.D(42));

            int cx = m + sw + m;
            content.SetBounds(cx, top, W - cx - m, H - top - m);
            int cw = content.Width, ch = content.Height;
            bAdd.SetBounds(cw - Dpi.D(28) - bAdd.PreferredW(), Dpi.D(24), bAdd.PreferredW(), Dpi.D(44));
            int sWidth = Math.Min(Dpi.D(300), Math.Max(Dpi.D(160), cw - Dpi.D(560)));
            search.SetBounds(bAdd.Left - Dpi.D(12) - sWidth, Dpi.D(23), sWidth, Dpi.D(46));
            list.SetBounds(Dpi.D(14), Dpi.D(140), cw - Dpi.D(28), ch - Dpi.D(154));
            PlaceActions();
            if (toast.Visible) toast.Location = new Point((cw - toast.Width) / 2, ch - toast.Height - Dpi.D(22));
            Invalidate();
        }

        void PlaceActions()
        {
            int x = content.SelectionPillRight() + Dpi.D(12), y = Dpi.D(94);
            foreach (var b in new[] { bOpen, bExport, bDel })
            {
                b.Width = b.PreferredW();
                b.Location = new Point(x, y);
                x += b.Width + Dpi.D(8);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            G.Glow(g, new RectangleF(-Dpi.D(200), -Dpi.D(240), Dpi.D(700), Dpi.D(420)), Theme.Accent, 34);
            Art.DrawLock(g, new RectangleF(Dpi.D(20), Dpi.D(13), Dpi.D(26), Dpi.D(26)));
            G.Text(g, "un0yy Vault", Theme.Semi(11f), new Rectangle(Dpi.D(56), Dpi.D(6), Dpi.D(200), Dpi.D(40)), Theme.Text, G.Left);
            int tx = Dpi.D(56) + G.TextW("un0yy Vault", Theme.Semi(11f)) + Dpi.D(12);
            string st = "Entsperrt";
            var pill = new RectangleF(tx, Dpi.D(14), G.TextW(st, Theme.Semi(8.5f)) + Dpi.D(30), Dpi.D(24));
            G.Fill(g, pill, Dpi.F(12), Theme.A(Theme.Success, 34));
            using (var b = new SolidBrush(Theme.Success)) g.FillEllipse(b, pill.X + Dpi.D(10), pill.Y + pill.Height / 2 - Dpi.D(3), Dpi.D(6), Dpi.D(6));
            G.Text(g, st, Theme.Semi(8.5f), new Rectangle((int)pill.X + Dpi.D(21), (int)pill.Y, (int)pill.Width, (int)pill.Height), Theme.Success, G.Left);
            G.Text(g, "Automatische Sperre nach " + AutoLockMinutes + " Min. Inaktivität", Theme.UI(8.5f),
                   new Rectangle((int)pill.Right + Dpi.D(14), Dpi.D(6), Dpi.D(320), Dpi.D(40)), Theme.Faint, G.Left);
            PaintFrame(g);
        }

        // ---- data -> ui
        void SelectCategory(int i)
        {
            category = i;
            for (int j = 0; j < nav.Length; j++) nav[j].Selected = j == i;
            list.ClearSelection();
            RefreshList();
        }

        void RefreshList()
        {
            string q = search.Text.Trim().ToLowerInvariant();
            IEnumerable<VaultEntry> src = vault.Entries;
            if (category > 0) src = src.Where(x => Kinds.Of(x.Name) == category);
            if (q.Length > 0) src = src.Where(x => x.Name.ToLowerInvariant().Contains(q));
            IOrderedEnumerable<VaultEntry> sorted;
            if (list.SortCol == 1) sorted = list.SortAsc ? src.OrderBy(x => x.Size) : src.OrderByDescending(x => x.Size);
            else if (list.SortCol == 2) sorted = list.SortAsc ? src.OrderBy(x => x.Added) : src.OrderByDescending(x => x.Added);
            else sorted = list.SortAsc ? src.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase) : src.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var shown = sorted.ToList();
            list.VaultEmpty = vault.Entries.Count == 0;
            list.SetItems(shown);

            var counts = new int[7];
            foreach (var en in vault.Entries) { counts[0]++; counts[Kinds.Of(en.Name)]++; }
            for (int i = 0; i < nav.Length; i++) { nav[i].Count = counts[i] > 0 ? counts[i].ToString() : ""; nav[i].Invalidate(); }

            content.Title = Kinds.Names[category];
            content.Subtitle = Fmt.Files(shown.Count) + "   ·   " + Fmt.Size(shown.Sum(x => x.Size)) +
                               (q.Length > 0 ? "   ·   Suche: „" + search.Text.Trim() + "“" : "");
            side.Files = Fmt.Files(vault.Entries.Count);
            side.SizeText = Fmt.Size(vault.Entries.Sum(x => x.Size));
            side.Invalidate();
            content.Invalidate();
            UpdateSelectionUi();
        }

        void UpdateSelectionUi()
        {
            int n = list.Selected.Count;
            content.Selection = n > 0 ? n + " ausgewählt" : "";
            bOpen.Visible = bExport.Visible = bDel.Visible = n > 0;
            PlaceActions();
            content.Invalidate(new Rectangle(0, Dpi.D(88), content.Width, Dpi.D(48)));
        }

        // ---- helpers
        static string SafePart(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            s = s.Trim();
            if (s == "" || s == "." || s == "..") s = "datei";
            return s;
        }

        static string LeafName(string name)
        {
            string[] parts = name.Split('/');
            return SafePart(parts[parts.Length - 1]);
        }

        static string UniquePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path), stem = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
            for (int i = 2; ; i++)
            {
                string p = Path.Combine(dir, stem + " (" + i + ")" + ext);
                if (!File.Exists(p)) return p;
            }
        }

        string UniqueName(string name)
        {
            var names = new HashSet<string>(vault.Entries.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(name)) return name;
            int slash = name.LastIndexOf('/');
            string folder = slash >= 0 ? name.Substring(0, slash + 1) : "";
            string leaf = slash >= 0 ? name.Substring(slash + 1) : name;
            string stem = Path.GetFileNameWithoutExtension(leaf), ext = Path.GetExtension(leaf);
            for (int i = 2; ; i++)
            {
                string n = folder + stem + " (" + i + ")" + ext;
                if (!names.Contains(n)) return n;
            }
        }

        // ---- add
        void AddViaDialog()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Dateien zum Tresor hinzufügen";
                d.Multiselect = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                AddItems(d.FileNames.Select(f => new KeyValuePair<string, string>(f, Path.GetFileName(f))).ToList(), new List<string>());
            }
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effect = files ? DragDropEffects.Copy : DragDropEffects.None;
            list.DropActive = files;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            list.DropActive = false;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null) return;
            var items = new List<KeyValuePair<string, string>>();
            var folders = new List<string>();
            foreach (string p in paths)
            {
                if (File.Exists(p)) items.Add(new KeyValuePair<string, string>(p, Path.GetFileName(p)));
                else if (Directory.Exists(p))
                {
                    string root = p.TrimEnd('\\', '/');
                    folders.Add(root);
                    string baseName = Path.GetFileName(root);
                    try
                    {
                        foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                            items.Add(new KeyValuePair<string, string>(f, baseName + "/" + f.Substring(root.Length + 1).Replace('\\', '/')));
                    }
                    catch (Exception ex)
                    {
                        ModernDialog.Tell(this, "Ordner nicht lesbar", "Der Ordner konnte nicht gelesen werden:\n" + ex.Message, DlgKind.Warn);
                    }
                }
            }
            Activate();
            AddItems(items, folders);
        }

        void AddItems(List<KeyValuePair<string, string>> items, List<string> folders)
        {
            if (items.Count == 0) return;
            var done = new List<string>();
            var errors = new List<string>();
            Cursor = Cursors.WaitCursor;
            for (int i = 0; i < items.Count; i++)
            {
                toast.Show("Verschlüssele " + (i + 1) + " von " + items.Count + "  –  " + items[i].Value, ToastKind.Busy);
                Application.DoEvents();
                try { vault.AddFile(items[i].Key, UniqueName(items[i].Value)); done.Add(items[i].Key); }
                catch (Exception ex) { errors.Add(items[i].Value + ": " + ex.Message); }
            }
            Cursor = Cursors.Default;
            RefreshList();

            if (errors.Count > 0)
                ModernDialog.Tell(this, "Nicht alles hat geklappt",
                    "Diese Dateien konnten nicht hinzugefügt werden:\n\n" + string.Join("\n", errors.Take(12)), DlgKind.Warn);
            if (done.Count == 0) { toast.HideNow(); return; }
            toast.Show(Fmt.Files(done.Count) + " sicher verschlüsselt", ToastKind.Success);

            int answer = ModernDialog.Ask(this, "Originale löschen?",
                Fmt.Files(done.Count) + (done.Count == 1 ? " ist" : " sind") + " jetzt verschlüsselt im Tresor.\n\n" +
                "Die Originale liegen aber noch ungeschützt auf deinem PC. Sollen sie gelöscht werden, damit niemand sie ohne Passwort sehen kann?",
                DlgKind.Question, new[] { "Behalten", "Originale löschen" }, 1);
            if (answer != 1) return;

            int failed = 0;
            foreach (string f in done)
            {
                try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { failed++; }
            }
            foreach (string d in folders)
            {
                try { if (Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length == 0) Directory.Delete(d, true); } catch { }
            }
            if (failed > 0) toast.Show(failed + " Original(e) konnten nicht gelöscht werden (evtl. noch geöffnet).", ToastKind.Warn);
            else toast.Show("Originale gelöscht – nur noch im Tresor.", ToastKind.Success);
        }

        // ---- open / export / delete
        void OpenSelected()
        {
            var sel = list.Selected;
            if (sel.Count == 0) return;
            if (sel.Count > 10) { toast.Show("Bitte höchstens 10 Dateien auf einmal öffnen.", ToastKind.Warn); return; }
            try
            {
                if (tempDir == null)
                {
                    tempDir = Path.Combine(Path.GetTempPath(), "un0yy-vault-" + Guid.NewGuid().ToString("N").Substring(0, 12));
                    Directory.CreateDirectory(tempDir);
                }
                Cursor = Cursors.WaitCursor;
                foreach (var en in sel)
                {
                    string dir = Path.Combine(tempDir, Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(dir);
                    string dst = Path.Combine(dir, LeafName(en.Name));
                    vault.Export(en, dst);
                    var psi = new ProcessStartInfo(dst);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                toast.Show("Geöffnet als temporäre Kopie – wird beim Sperren gelöscht.", ToastKind.Info);
            }
            catch (Exception ex)
            {
                ModernDialog.Tell(this, "Öffnen fehlgeschlagen", ex.Message, DlgKind.Warn);
            }
            finally { Cursor = Cursors.Default; }
        }

        void ExportSelected()
        {
            var sel = list.Selected;
            if (sel.Count == 0) return;
            try
            {
                if (sel.Count == 1)
                {
                    using (var d = new SaveFileDialog())
                    {
                        d.Title = "Entschlüsselte Kopie speichern";
                        d.FileName = LeafName(sel[0].Name);
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        Cursor = Cursors.WaitCursor;
                        vault.Export(sel[0], d.FileName);
                    }
                }
                else
                {
                    using (var d = new FolderBrowserDialog())
                    {
                        d.Description = "Ordner wählen, in den die " + sel.Count + " Dateien entschlüsselt gespeichert werden";
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        Cursor = Cursors.WaitCursor;
                        foreach (var en in sel)
                        {
                            string[] parts = en.Name.Split('/').Select(SafePart).ToArray();
                            string dst = Path.Combine(new[] { d.SelectedPath }.Concat(parts).ToArray());
                            Directory.CreateDirectory(Path.GetDirectoryName(dst));
                            vault.Export(en, UniquePath(dst));
                        }
                    }
                }
                toast.Show(Fmt.Files(sel.Count) + " entschlüsselt gespeichert (diese Kopie ist nicht geschützt).", ToastKind.Success);
            }
            catch (Exception ex)
            {
                ModernDialog.Tell(this, "Speichern fehlgeschlagen", ex.Message, DlgKind.Warn);
            }
            finally { Cursor = Cursors.Default; }
        }

        void DeleteSelected()
        {
            var sel = list.Selected;
            if (sel.Count == 0) return;
            string what = sel.Count == 1 ? "„" + LeafName(sel[0].Name) + "“" : sel.Count + " Dateien";
            int a = ModernDialog.Ask(this, "Endgültig löschen?",
                what + " wird endgültig aus dem Tresor gelöscht. Das kann nicht rückgängig gemacht werden.",
                DlgKind.Danger, new[] { "Abbrechen", "Löschen" }, 1);
            if (a != 1) return;
            foreach (var en in sel)
            {
                try { vault.Remove(en); }
                catch (Exception ex) { ModernDialog.Tell(this, "Löschen fehlgeschlagen", ex.Message, DlgKind.Warn); break; }
            }
            RefreshList();
            toast.Show(Fmt.Files(sel.Count) + " gelöscht.", ToastKind.Success);
        }

        // ---- lock / cleanup
        void DoLock() { LockRequested = true; Close(); }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x100 || m.Msg == 0x104 || m.Msg == 0x200 || m.Msg == 0x201 || m.Msg == 0x204 || m.Msg == 0x20A)
                lastActivity = DateTime.Now;
            return false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.F) { search.FocusBox(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L) { DoLock(); e.Handled = true; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            Application.RemoveMessageFilter(this);
            idle.Stop();
            vault.Lock();
            if (tempDir != null) { try { Directory.Delete(tempDir, true); } catch { } }
        }

        public static void CleanupOldTemp()
        {
            try
            {
                foreach (string d in Directory.GetDirectories(Path.GetTempPath(), "un0yy-vault-*"))
                {
                    try { Directory.Delete(d, true); } catch { }
                }
            }
            catch { }
        }
    }

    // ================================================================== entry point

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--make-icon") { Art.WriteIco(args[1]); return 0; }
            if (args.Length == 2 && args[0] == "--selftest") return SelfTest(args[1]);

            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { Crash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Crash(e.ExceptionObject as Exception); };
            try { return Run(); }
            catch (Exception ex) { Crash(ex); return 1; }
        }

        static void Crash(Exception ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "un0yy-vault-error.log"),
                    DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            try
            {
                MessageBox.Show("Unerwarteter Fehler:\n\n" + (ex == null ? "?" : ex.Message) +
                                "\n\nDetails stehen in un0yy-vault-error.log neben der exe.", "un0yy Vault",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        static int Run()
        {
            MainForm.CleanupOldTemp();

            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "un0yy-vault-data");
            var vault = new Vault(dir);
            while (true)
            {
                using (var login = new LoginForm(vault))
                    if (login.ShowDialog() != DialogResult.OK) return 0;
                bool again;
                using (var main = new MainForm(vault))
                {
                    main.ShowDialog();
                    again = main.LockRequested;
                }
                if (!again) return 0;
            }
        }

        static int SelfTest(string reportPath)
        {
            string dir = Path.Combine(Path.GetTempPath(), "un0yy-selftest-" + Guid.NewGuid().ToString("N"));
            var log = new StringBuilder();
            try
            {
                Directory.CreateDirectory(dir);
                string data = Path.Combine(dir, "data");
                var v = new Vault(data);
                v.Create("richtiges passwort 1");
                byte[] content = new byte[777777];
                new Random(42).NextBytes(content);
                string src = Path.Combine(dir, "hallo.bin");
                File.WriteAllBytes(src, content);
                v.AddFile(src, "ordner/hallo.bin");
                var en2 = v.AddFile(src, "leer-test.bin");
                v.Remove(en2);
                v.Lock();
                log.AppendLine("create + add + remove: ok");

                var v2 = new Vault(data);
                if (v2.Unlock("falsches passwort")) throw new Exception("wrong password accepted");
                log.AppendLine("wrong password rejected: ok");
                if (!v2.Unlock("richtiges passwort 1")) throw new Exception("right password rejected");
                if (v2.Entries.Count != 1 || v2.Entries[0].Name != "ordner/hallo.bin" || v2.Entries[0].Size != content.Length)
                    throw new Exception("index mismatch");
                log.AppendLine("unlock + index: ok");

                string outp = Path.Combine(dir, "out.bin");
                v2.Export(v2.Entries[0], outp);
                if (!File.ReadAllBytes(outp).SequenceEqual(content)) throw new Exception("decrypted content differs");
                log.AppendLine("decrypt roundtrip: ok");

                string blob = Directory.GetFiles(Path.Combine(data, "files"))[0];
                byte[] raw = File.ReadAllBytes(blob);
                raw[raw.Length / 2] ^= 0x01;
                File.WriteAllBytes(blob, raw);
                bool caught = false;
                try { v2.Export(v2.Entries[0], outp); } catch (InvalidDataException) { caught = true; }
                if (!caught) throw new Exception("tampering not detected");
                if (File.Exists(outp)) throw new Exception("partial output left behind");
                log.AppendLine("tamper detection: ok");

                if (v2.ChangePassword("falsch", "neues passwort 22")) throw new Exception("change with wrong old pw");
                if (!v2.ChangePassword("richtiges passwort 1", "neues passwort 22")) throw new Exception("change failed");
                v2.Lock();
                var v3 = new Vault(data);
                if (v3.Unlock("richtiges passwort 1")) throw new Exception("old password still works");
                if (!v3.Unlock("neues passwort 22")) throw new Exception("new password rejected");
                if (v3.Entries.Count != 1) throw new Exception("entries lost after pw change");
                log.AppendLine("change password: ok");

                string idx = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(data, "index.dat")));
                if (idx.Contains("hallo")) throw new Exception("file name visible in index");
                log.AppendLine("file names hidden: ok");

                log.AppendLine("SELFTEST OK");
                File.WriteAllText(reportPath, log.ToString());
                return 0;
            }
            catch (Exception ex)
            {
                log.AppendLine("SELFTEST FAIL: " + ex);
                File.WriteAllText(reportPath, log.ToString());
                return 1;
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
