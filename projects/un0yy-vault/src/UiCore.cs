// un0yy Vault - design system: theme, drawing helpers, basic controls
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Un0yyVault
{
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
        public static readonly Color Bg = Color.FromArgb(10, 10, 15);
        public static readonly Color Panel = Color.FromArgb(18, 18, 26);
        public static readonly Color Surface2 = Color.FromArgb(31, 31, 45);
        public static readonly Color Surface3 = Color.FromArgb(44, 44, 62);
        public static readonly Color Border = Color.FromArgb(46, 46, 66);
        public static readonly Color Text = Color.FromArgb(243, 243, 250);
        public static readonly Color Muted = Color.FromArgb(148, 148, 174);
        public static readonly Color Faint = Color.FromArgb(98, 98, 124);
        public static readonly Color Danger = Color.FromArgb(244, 80, 110);
        public static readonly Color Success = Color.FromArgb(52, 211, 153);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Gold = Color.FromArgb(250, 204, 21);

        public static Color Accent, Accent2, AccentSoft;
        public static int AccentIndex;
        public static readonly string[] AccentNames = { "Violett", "Ozean", "Smaragd", "Sunset", "Rosé" };
        public static readonly Color[,] Palettes =
        {
            { Color.FromArgb(124, 77, 255), Color.FromArgb(40, 180, 255) },
            { Color.FromArgb(37, 99, 235), Color.FromArgb(34, 211, 238) },
            { Color.FromArgb(16, 185, 129), Color.FromArgb(163, 230, 53) },
            { Color.FromArgb(249, 115, 22), Color.FromArgb(244, 63, 94) },
            { Color.FromArgb(219, 39, 119), Color.FromArgb(168, 85, 247) },
        };

        static Theme() { SetAccent(0); }

        public static void SetAccent(int i)
        {
            if (i < 0 || i >= Palettes.GetLength(0)) i = 0;
            AccentIndex = i;
            Accent = Palettes[i, 0];
            Accent2 = Palettes[i, 1];
            AccentSoft = Mix(Accent, Color.White, 0.42f);
        }

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
        public static Font Mono(float size) { return Get("Consolas", size, FontStyle.Regular); }
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
        public const string Star = "", StarFill = "", Settings = "", Grid = "", List = "";
        public const string Rename = "", Save = "", FolderOpen = "", ZoomIn = "", ZoomOut = "";
        public const string Prev = "", Next = "", Edit = "", Sort = "", Fit = "";
        public const string Sync = "", Palette = "", Bolt = "", Keyboard = "", Caps = "";
    }

    static class G
    {
        public static void HQ(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
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
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static void Gradient(Graphics g, RectangleF r, float rad, Color a, Color b, float angle)
        {
            if (r.Width < 1 || r.Height < 1) return;
            using (var p = Round(r, rad)) using (var br = new LinearGradientBrush(r, a, b, angle)) g.FillPath(br, p);
        }

        /// Draws an image "cover"-fitted into a rounded rectangle.
        public static void ImageCover(Graphics g, Image img, RectangleF r, float rad)
        {
            if (img == null || r.Width < 1 || r.Height < 1) return;
            float s = Math.Max(r.Width / img.Width, r.Height / img.Height);
            float w = img.Width * s, h = img.Height * s;
            var dst = new RectangleF(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
            var state = g.Save();
            using (var p = Round(r, rad)) g.SetClip(p, CombineMode.Intersect);
            g.DrawImage(img, dst);
            g.Restore(state);
        }

        /// Draws an image "contain"-fitted (whole image visible) into a rectangle.
        public static RectangleF ImageFit(Graphics g, Image img, RectangleF r)
        {
            if (img == null || r.Width < 1 || r.Height < 1) return RectangleF.Empty;
            float s = Math.Min(1f * r.Width / img.Width, 1f * r.Height / img.Height);
            float w = img.Width * s, h = img.Height * s;
            var dst = new RectangleF(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
            g.DrawImage(img, dst);
            return dst;
        }

        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl)
        {
            TextRenderer.DrawText(g, s, f, r, c, fl | TextFormatFlags.NoPrefix);
        }

        public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags Right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        public const TextFormatFlags CenterEllipsis = Center | TextFormatFlags.EndEllipsis;
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

        public static int TextH(string s, Font f, int width)
        {
            return TextRenderer.MeasureText(s, f, new Size(width, int.MaxValue), WrapLeft | TextFormatFlags.NoPrefix).Height;
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
            var hi = new RectangleF(r.X, r.Y, w, h * 0.55f);
            using (var path = G.Round(hi, w * 0.26f))
            using (var br = new LinearGradientBrush(new RectangleF(hi.X, hi.Y, hi.Width, hi.Height + 1), Color.FromArgb(45, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
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
        static readonly System.Drawing.Color[] colors =
        {
            System.Drawing.Color.Empty, System.Drawing.Color.FromArgb(244, 114, 182), System.Drawing.Color.FromArgb(96, 165, 250), System.Drawing.Color.FromArgb(251, 146, 60),
            System.Drawing.Color.FromArgb(74, 222, 128), System.Drawing.Color.FromArgb(250, 204, 21), System.Drawing.Color.FromArgb(167, 139, 250)
        };
        static readonly string[][] Ext =
        {
            new string[0],
            new[] { "jpg", "jpeg", "png", "gif", "bmp", "webp", "heic", "heif", "svg", "ico", "tif", "tiff", "raw", "psd", "jfif" },
            new[] { "pdf", "doc", "docx", "txt", "rtf", "odt", "xls", "xlsx", "csv", "ppt", "pptx", "md", "json", "xml", "html", "htm", "odp", "ods", "log" },
            new[] { "mp4", "mov", "avi", "mkv", "wmv", "webm", "m4v", "flv", "3gp" },
            new[] { "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma", "opus" },
            new[] { "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso" }
        };
        static readonly string[] TextExt =
        {
            "txt", "md", "json", "xml", "html", "htm", "csv", "log", "ini", "cfg", "conf", "yml", "yaml", "cs", "py", "js", "ts",
            "css", "java", "c", "cpp", "h", "bat", "cmd", "ps1", "sh", "sql", "lua", "rs", "go", "php", "rb", "toml", "env"
        };
        static readonly string[] ImageExt = { "jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "ico", "jfif" };

        public static System.Drawing.Color Color(int k) { return k == 0 ? Theme.AccentSoft : colors[k]; }

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

        public static bool IsText(string name) { return TextExt.Contains(ExtOf(name)); }
        public static bool IsImage(string name) { return ImageExt.Contains(ExtOf(name)); }

        public static string Label(string name)
        {
            string e = ExtOf(name);
            return e.Length > 0 ? e.ToUpperInvariant() + "-Datei" : "Datei";
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

        public static string LongDate(DateTime utc) { return utc.ToLocalTime().ToString("dd.MM.yyyy  ·  HH:mm 'Uhr'"); }

        public static string Files(int n) { return n.ToString("N0") + (n == 1 ? " Datei" : " Dateien"); }
    }

    static class Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr h, int id);

        public static void Cue(TextBox t, string text)
        {
            EventHandler apply = delegate { SendMessage(t.Handle, 0x1501, (IntPtr)1, text); };
            if (t.IsHandleCreated) apply(t, EventArgs.Empty); else t.HandleCreated += apply;
        }
    }

    // ================================================================== basic controls

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
                bool done = Math.Abs(t - hoverT) < 0.02f;
                if (done) hoverT = t;
                if (!Animate() && done) anim.Stop();
                Invalidate();
            };
        }

        /// Override to run extra animations; return true while still animating.
        protected virtual bool Animate() { return false; }
        protected void Kick() { anim.Start(); }

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
            int tw = string.IsNullOrEmpty(Text) ? 0 : G.TextW(Text, Font);
            int gw = string.IsNullOrEmpty(Glyph) ? 0 : (tw > 0 ? Dpi.D(26) : Dpi.D(18));
            int x = (Width - tw - gw) / 2;
            if (gw > 0) G.Glyph(g, Glyph, 11f, new Rectangle(x, 0, Dpi.D(18), Height), fg);
            if (tw > 0) G.Text(g, Text, Font, new Rectangle(x + gw, 0, tw + 4, Height), fg, G.Left);
        }
    }

    class IconButton : AnimControl
    {
        public string Glyph;
        public bool CloseStyle;
        public float GlyphSize = 10f;
        bool active;
        public bool Active { get { return active; } set { active = value; Invalidate(); } }

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
                var r = new RectangleF(0, 0, Width - 1, Height - 1);
                if (active) G.Fill(g, r, Dpi.F(9), Theme.A(Theme.Accent, 90));
                else if (hoverT > 0.01f) G.Fill(g, r, Dpi.F(9), Theme.A(Theme.Surface3, (int)(255 * hoverT)));
                fg = active ? Color.White : (Enabled ? Theme.Mix(Theme.Muted, Theme.Text, hoverT) : Theme.Faint);
            }
            G.Glyph(g, Glyph, GlyphSize, ClientRectangle, fg);
        }
    }

    class NavItem : AnimControl
    {
        public string Glyph, Count;
        public Func<Color> Tint;
        bool selected;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        public NavItem(string text, string glyph, Func<Color> tint)
        {
            Text = text; Glyph = glyph; Tint = tint;
            Cursor = Cursors.Hand;
            Height = Dpi.D(38);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            if (selected)
            {
                G.Gradient(g, r, Dpi.F(10), Theme.A(Theme.Accent, 72), Theme.A(Theme.Accent, 16), 0f);
                G.Fill(g, new RectangleF(0, Height * 0.26f, Dpi.F(3), Height * 0.48f), Dpi.F(2), Theme.AccentSoft);
            }
            else if (hoverT > 0.01f) G.Fill(g, r, Dpi.F(10), Theme.A(Theme.Surface2, (int)(255 * hoverT)));

            Color tint = Tint();
            Color fg = selected ? Theme.Text : Theme.Mix(Theme.Muted, Theme.Text, hoverT);
            G.Glyph(g, Glyph, 11f, new Rectangle(Dpi.D(12), 0, Dpi.D(22), Height), selected ? tint : Theme.Mix(Theme.Faint, tint, hoverT * 0.8f));
            G.Text(g, Text, selected ? Theme.Semi(10f) : Theme.UI(10f), new Rectangle(Dpi.D(44), 0, Width - Dpi.D(90), Height), fg, G.Left);
            if (!string.IsNullOrEmpty(Count))
            {
                var f = Theme.Semi(8.5f);
                int cw = Math.Max(Dpi.D(24), G.TextW(Count, f) + Dpi.D(12));
                var pill = new RectangleF(Width - cw - Dpi.D(10), (Height - Dpi.D(20)) / 2f, cw, Dpi.D(20));
                G.Fill(g, pill, Dpi.F(10), selected ? Theme.A(Theme.Accent, 120) : Theme.Surface2);
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
        public new event KeyEventHandler KeyUp;

        public InputBox(string glyph, string placeholder, bool password)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            this.glyph = glyph;
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
            Box.KeyUp += delegate(object s, KeyEventArgs e) { if (KeyUp != null) KeyUp(this, e); };
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
            Height = Dpi.D(46);
        }

        public override string Text { get { return Box == null ? "" : Box.Text; } set { if (Box != null) Box.Text = value; } }
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

    class ToggleSwitch : AnimControl
    {
        bool on;
        float knob;
        public event EventHandler CheckedChanged;
        public bool Checked
        {
            get { return on; }
            set { if (on == value) return; on = value; Kick(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }

        public ToggleSwitch() { Size = new Size(Dpi.D(46), Dpi.D(26)); Cursor = Cursors.Hand; }

        public void SetSilently(bool v) { on = v; knob = v ? 1f : 0f; Invalidate(); }

        protected override bool Animate()
        {
            float t = on ? 1f : 0f;
            knob += (t - knob) * 0.3f;
            if (Math.Abs(t - knob) < 0.02f) { knob = t; return false; }
            return true;
        }

        protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var track = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color off = Theme.Mix(Theme.Surface3, Theme.Faint, hoverT * 0.4f);
            if (knob > 0.01f) G.Gradient(g, track, Height / 2f, Theme.A(Theme.Accent, (int)(255 * knob)), Theme.A(Theme.Accent2, (int)(255 * knob)), 0f);
            if (knob < 0.99f) G.Fill(g, track, Height / 2f, Theme.A(off, (int)(255 * (1 - knob))));
            float d = Height - Dpi.F(8);
            float x = Dpi.F(4) + (Width - Dpi.F(8) - d) * knob;
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, Dpi.F(4), d, d);
        }
    }

    class Segmented : Control
    {
        readonly string[] options;
        int selected, hover = -1;
        public event EventHandler SelectedChanged;
        public int SelectedIndex { get { return selected; } set { selected = value; Invalidate(); } }

        public Segmented(string[] options)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            this.options = options;
            Font = Theme.Semi(9f);
            Height = Dpi.D(36);
            Cursor = Cursors.Hand;
        }

        int At(int x) { int w = Width / options.Length; return Math.Max(0, Math.Min(options.Length - 1, x / Math.Max(1, w))); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = At(e.X); if (h != hover) { hover = h; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = At(e.X);
            if (i != selected) { selected = i; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            G.Fill(g, r, Dpi.F(10), Theme.Surface2);
            float w = (Width - Dpi.F(8)) / (float)options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var seg = new RectangleF(Dpi.F(4) + i * w, Dpi.F(4), w, Height - Dpi.F(8));
                if (i == selected) G.Gradient(g, seg, Dpi.F(8), Theme.Accent, Theme.Mix(Theme.Accent, Theme.Accent2, 0.6f), 0f);
                else if (i == hover) G.Fill(g, seg, Dpi.F(8), Theme.Surface3);
                G.Text(g, options[i], Font, Rectangle.Round(seg), i == selected ? Color.White : Theme.Muted, G.Center);
            }
        }
    }

    class Swatch : AnimControl
    {
        public readonly int Index;
        bool selected;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        public Swatch(int index) { Index = index; Size = new Size(Dpi.D(44), Dpi.D(44)); Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            float pad = Dpi.F(selected ? 6 : 7) - hoverT * Dpi.F(1.5f);
            var r = new RectangleF(pad, pad, Width - pad * 2 - 1, Height - pad * 2 - 1);
            using (var br = new LinearGradientBrush(r, Theme.Palettes[Index, 0], Theme.Palettes[Index, 1], 45f)) g.FillEllipse(br, r);
            if (selected)
            {
                using (var p = new Pen(Color.White, Dpi.F(2))) g.DrawEllipse(p, Dpi.F(2), Dpi.F(2), Width - Dpi.F(4) - 1, Height - Dpi.F(4) - 1);
                G.Glyph(g, Ico.Check, 10f, ClientRectangle, Color.White);
            }
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
            int w = Math.Min(Dpi.D(640), G.TextW(text, Font) + Dpi.D(76));
            Size = new Size(w, Dpi.D(48));
            Reposition();
            Visible = true;
            BringToFront();
            Invalidate();
            hide.Stop();
            if (k != ToastKind.Busy) { hide.Interval = k == ToastKind.Error || k == ToastKind.Warn ? 5500 : 3500; hide.Start(); }
        }

        public void Reposition()
        {
            if (Parent != null) Location = new Point((Parent.Width - Width) / 2, Parent.Height - Height - Dpi.D(22));
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
                        kind == ToastKind.Error ? Ico.Error : kind == ToastKind.Busy ? Ico.Sync : Ico.Info;
            var circle = new RectangleF(Dpi.D(10), (Height - Dpi.D(28)) / 2f, Dpi.D(28), Dpi.D(28));
            using (var b = new SolidBrush(Theme.A(c, 45))) g.FillEllipse(b, circle);
            G.Glyph(g, gl, 9.5f, Rectangle.Round(circle), c);
            G.Text(g, Text, Font, new Rectangle(Dpi.D(48), 0, Width - Dpi.D(60), Height), Theme.Text, G.Left);
        }
    }

    /// Centered card with a progress bar, shown while files are encrypted.
    class ProgressOverlay : Control
    {
        public string Title = "", Detail = "";
        float value, shown;
        readonly Timer anim;

        public ProgressOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            Size = new Size(Dpi.D(460), Dpi.D(150));
            anim = new Timer();
            anim.Interval = 15;
            anim.Tick += delegate { shown += (value - shown) * 0.25f; Invalidate(); };
        }

        public void Begin(string title) { Title = title; Detail = ""; value = shown = 0; Visible = true; BringToFront(); anim.Start(); Center(); }
        public void Set(float v, string detail) { value = Math.Max(0, Math.Min(1, v)); Detail = detail; }
        public void End() { anim.Stop(); Visible = false; }
        public void Center() { if (Parent != null) Location = new Point((Parent.Width - Width) / 2, (Parent.Height - Height) / 2); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            G.Fill(g, r, Dpi.F(18), Theme.Surface3);
            G.Stroke(g, r, Dpi.F(18), Theme.A(Color.White, 24), 1f);
            var c = new RectangleF(Dpi.D(22), Dpi.D(22), Dpi.D(44), Dpi.D(44));
            G.Gradient(g, c, Dpi.F(14), Theme.Accent, Theme.Accent2, 45f);
            G.Glyph(g, Ico.Lock, 14f, Rectangle.Round(c), Color.White);
            G.Text(g, Title, Theme.Bold(12f), new Rectangle(Dpi.D(80), Dpi.D(20), Width - Dpi.D(150), Dpi.D(26)), Theme.Text, G.Left);
            G.Text(g, Detail, Theme.UI(9f), new Rectangle(Dpi.D(80), Dpi.D(46), Width - Dpi.D(100), Dpi.D(22)), Theme.Muted, G.Left);
            string pct = ((int)Math.Round(shown * 100)) + " %";
            G.Text(g, pct, Theme.Bold(12f), new Rectangle(Width - Dpi.D(90), Dpi.D(20), Dpi.D(68), Dpi.D(26)), Theme.AccentSoft, G.Right);
            var bar = new RectangleF(Dpi.D(22), Dpi.D(96), Width - Dpi.D(44), Dpi.D(10));
            G.Fill(g, bar, Dpi.F(5), Theme.Surface2);
            if (shown > 0.005f) G.Gradient(g, new RectangleF(bar.X, bar.Y, Math.Max(bar.Height, bar.Width * shown), bar.Height), Dpi.F(5), Theme.Accent, Theme.Accent2, 0f);
            G.Text(g, "Bitte warten – deine Dateien werden mit AES-256 verschlüsselt.", Theme.UI(8.5f),
                   new Rectangle(Dpi.D(22), Dpi.D(114), Width - Dpi.D(44), Dpi.D(22)), Theme.Faint, G.Left);
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
    }

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
        public override Color CheckBackground { get { return Theme.A(Theme.Accent, 90); } }
        public override Color CheckSelectedBackground { get { return Theme.A(Theme.Accent, 120); } }
        public override Color CheckPressedBackground { get { return Theme.A(Theme.Accent, 120); } }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Tag as string == "danger" ? Theme.Danger : (e.Item.Enabled ? Theme.Text : Theme.Faint);
            base.OnRenderItemText(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            G.Glyph(g, Ico.Check, 9f, e.ImageRectangle, Theme.AccentSoft);
        }
    }

    static class Menus
    {
        public static ContextMenuStrip Create()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkRenderer();
            m.Font = Theme.UI(10f);
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler click, string tag)
        {
            var it = new ToolStripMenuItem(text);
            if (click != null) it.Click += click;
            it.Tag = tag;
            it.Padding = new Padding(Dpi.D(4), Dpi.D(5), Dpi.D(6), Dpi.D(5));
            return it;
        }
    }
}
