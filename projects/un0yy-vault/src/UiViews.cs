// un0yy Vault - file list (list + grid), thumbnails, preview panel, image viewer
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Un0yyVault
{
    static class Imaging
    {
        public const long MaxImageBytes = 80L * 1024 * 1024;

        /// Decrypts an image into memory and returns a scaled copy (never written to disk).
        public static Bitmap Decode(Vault v, VaultEntry en, int maxDim)
        {
            byte[] data = v.ReadAll(en, MaxImageBytes);
            if (data == null) return null;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var img = Image.FromStream(ms, true, true))
                {
                    Orient(img);
                    return Scale(img, maxDim);
                }
            }
            finally { Array.Clear(data, 0, data.Length); }
        }

        static void Orient(Image img)
        {
            try
            {
                if (Array.IndexOf(img.PropertyIdList, 0x0112) < 0) return;
                int o = img.GetPropertyItem(0x0112).Value[0];
                RotateFlipType t;
                switch (o)
                {
                    case 2: t = RotateFlipType.RotateNoneFlipX; break;
                    case 3: t = RotateFlipType.Rotate180FlipNone; break;
                    case 4: t = RotateFlipType.Rotate180FlipX; break;
                    case 5: t = RotateFlipType.Rotate90FlipX; break;
                    case 6: t = RotateFlipType.Rotate90FlipNone; break;
                    case 7: t = RotateFlipType.Rotate270FlipX; break;
                    case 8: t = RotateFlipType.Rotate270FlipNone; break;
                    default: return;
                }
                img.RotateFlip(t);
            }
            catch { }
        }

        static Bitmap Scale(Image img, int maxDim)
        {
            float s = Math.Min(1f, Math.Min((float)maxDim / img.Width, (float)maxDim / img.Height));
            int w = Math.Max(1, (int)(img.Width * s)), h = Math.Max(1, (int)(img.Height * s));
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, 0, 0, w, h);
            }
            return bmp;
        }

        public static string DecodeText(byte[] data, int maxChars)
        {
            string s;
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) s = Encoding.Unicode.GetString(data, 2, data.Length - 2);
            else
            {
                try { s = new UTF8Encoding(false, true).GetString(data); }
                catch (DecoderFallbackException) { s = Encoding.Default.GetString(data); }
                if (s.Length > 0 && s[0] == '﻿') s = s.Substring(1);
            }
            if (s.Length > maxChars) s = s.Substring(0, maxChars) + "\r\n\r\n… (Vorschau gekürzt)";
            return s.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
    }

    /// Decrypts and scales image thumbnails on a background thread. Thumbnails live in memory only.
    class ThumbCache : IDisposable
    {
        readonly Vault vault;
        readonly Control owner;
        readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        readonly HashSet<string> failed = new HashSet<string>(), queued = new HashSet<string>();
        readonly Queue<VaultEntry> queue = new Queue<VaultEntry>();
        readonly object gate = new object();
        readonly AutoResetEvent signal = new AutoResetEvent(false);
        readonly Thread worker;
        volatile bool stop;
        public event Action Updated;

        public ThumbCache(Vault v, Control owner)
        {
            vault = v; this.owner = owner;
            worker = new Thread(Run);
            worker.IsBackground = true;
            worker.Priority = ThreadPriority.BelowNormal;
            worker.Start();
        }

        public Bitmap Get(VaultEntry en)
        {
            if (!Kinds.IsImage(en.Name) || en.Size > Imaging.MaxImageBytes) return null;
            lock (gate)
            {
                Bitmap b;
                if (cache.TryGetValue(en.Id, out b)) return b;
                if (failed.Contains(en.Id) || queued.Contains(en.Id)) return null;
                queued.Add(en.Id);
                queue.Enqueue(en);
            }
            signal.Set();
            return null;
        }

        void Run()
        {
            while (!stop)
            {
                VaultEntry en = null;
                lock (gate) { if (queue.Count > 0) en = queue.Dequeue(); }
                if (en == null) { signal.WaitOne(400); continue; }
                Bitmap thumb = null;
                try { thumb = Imaging.Decode(vault, en, Dpi.D(360)); } catch { }
                lock (gate)
                {
                    queued.Remove(en.Id);
                    if (stop) { if (thumb != null) thumb.Dispose(); thumb = null; }
                    else if (thumb != null) cache[en.Id] = thumb;
                    else failed.Add(en.Id);
                }
                if (thumb != null)
                {
                    try { owner.BeginInvoke((MethodInvoker)delegate { if (Updated != null) Updated(); }); } catch { }
                }
            }
        }

        public void Dispose()
        {
            stop = true;
            signal.Set();
            try { worker.Join(1500); } catch { }
            lock (gate)
            {
                foreach (var b in cache.Values) b.Dispose();
                cache.Clear();
                queue.Clear();
            }
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
        bool dragThumb, dropActive, grid;
        int dragY;
        float dragScroll;

        public int SortCol;      // 0 name, 1 size, 2 date
        public bool SortAsc = true;
        public bool VaultEmpty;
        public string EmptyTitle = "Nichts gefunden", EmptyText = "Probier einen anderen Suchbegriff oder eine andere Kategorie.";
        public Func<VaultEntry, Bitmap> ThumbFor;
        public Func<VaultEntry, bool> IsFav;
        public event EventHandler SelectionChanged, OpenRequested, AddRequested, DeleteRequested, SortChanged, RenameRequested;
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

        public bool Grid { get { return grid; } set { grid = value; scroll = target = 0; Invalidate(); } }
        public bool DropActive { get { return dropActive; } set { if (dropActive != value) { dropActive = value; Invalidate(); } } }
        public List<VaultEntry> Items { get { return items; } }
        public List<VaultEntry> Selected { get { return items.Where(x => sel.Contains(x)).ToList(); } }

        // ---- geometry
        int RowH { get { return Dpi.D(60); } }
        int HeadH { get { return grid ? Dpi.D(6) : Dpi.D(38); } }
        int ViewH { get { return Math.Max(1, Height - HeadH); } }
        int SizeX { get { return Width - Dpi.D(300); } }
        int DateX { get { return Width - Dpi.D(180); } }
        int Pad { get { return Dpi.D(10); } }
        int Gap { get { return Dpi.D(14); } }
        int TileH { get { return Dpi.D(206); } }
        int Cols { get { return Math.Max(1, (Width - Pad * 2 - Dpi.D(10) + Gap) / (Dpi.D(172) + Gap)); } }
        int TileW { get { return (Width - Pad * 2 - Dpi.D(10) - Gap * (Cols - 1)) / Cols; } }
        int Span { get { return grid ? TileH + Gap : RowH; } }
        int Rows { get { return grid ? (items.Count + Cols - 1) / Cols : items.Count; } }
        float MaxScroll { get { return Math.Max(0, Rows * Span + Dpi.D(10) - ViewH); } }

        Rectangle TileRect(int i)
        {
            int c = i % Cols, r = i / Cols;
            return new Rectangle(Pad + c * (TileW + Gap), HeadH + Dpi.D(4) + r * Span - (int)scroll, TileW, TileH);
        }

        int IndexAt(Point p)
        {
            if (p.Y < HeadH) return -1;
            if (grid)
            {
                int y = p.Y - HeadH - Dpi.D(4) + (int)scroll, x = p.X - Pad;
                if (x < 0 || y < 0) return -1;
                int c = x / (TileW + Gap), r = y / Span;
                if (c >= Cols || x % (TileW + Gap) > TileW || y % Span > TileH) return -1;
                int i = r * Cols + c;
                return i < items.Count ? i : -1;
            }
            int j = (int)((p.Y - HeadH + scroll - Dpi.D(4)) / RowH);
            return j >= 0 && j < items.Count ? j : -1;
        }

        Rectangle ThumbRect()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            float total = Rows * Span + Dpi.D(10);
            int h = Math.Max(Dpi.D(36), (int)(ViewH * ViewH / total));
            int y = HeadH + (int)((ViewH - h) * (scroll / MaxScroll));
            return new Rectangle(Width - Dpi.D(10), y, Dpi.D(6), h);
        }

        void ScrollTo(float v) { target = Math.Max(0, Math.Min(MaxScroll, v)); scrollAnim.Start(); }

        void EnsureVisible(int i)
        {
            int row = grid ? i / Cols : i;
            float top = row * Span, bottom = top + Span + Dpi.D(10);
            if (top < target) ScrollTo(top);
            else if (bottom > target + ViewH) ScrollTo(bottom - ViewH);
        }

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

        public void Select(VaultEntry en)
        {
            int i = items.IndexOf(en);
            if (i < 0) return;
            sel.Clear(); sel.Add(en); anchor = cursor = i;
            EnsureVisible(i);
            Invalidate();
            Raise(SelectionChanged);
        }

        public void ClearSelection() { sel.Clear(); Invalidate(); Raise(SelectionChanged); }

        void Raise(EventHandler h) { if (h != null) h(this, EventArgs.Empty); }

        protected override void OnResize(EventArgs e) { base.OnResize(e); target = Math.Min(target, MaxScroll); scroll = Math.Min(scroll, MaxScroll); }

        // ---- painting
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            g.Clear(BackColor);

            if (items.Count == 0) { PaintEmpty(g); if (dropActive) PaintDrop(g); return; }

            if (!grid)
            {
                var hf = Theme.Semi(8.5f);
                PaintHead(g, "NAME", 0, new Rectangle(Dpi.D(70), 0, SizeX - Dpi.D(80), HeadH), hf);
                PaintHead(g, "GRÖSSE", 1, new Rectangle(SizeX, 0, Dpi.D(110), HeadH), hf);
                PaintHead(g, "HINZUGEFÜGT", 2, new Rectangle(DateX, 0, Dpi.D(160), HeadH), hf);
                using (var p = new Pen(Theme.Border)) g.DrawLine(p, Dpi.D(12), HeadH - 1, Width - Dpi.D(12), HeadH - 1);
            }

            g.SetClip(new Rectangle(0, HeadH, Width, ViewH));
            int firstRow = Math.Max(0, (int)(scroll / Span) - 1);
            int lastRow = (int)((scroll + ViewH) / Span) + 1;
            if (grid)
            {
                for (int i = firstRow * Cols; i < Math.Min(items.Count, (lastRow + 1) * Cols); i++) PaintTile(g, i);
            }
            else
            {
                for (int i = firstRow; i <= Math.Min(items.Count - 1, lastRow); i++) PaintRow(g, i);
            }
            var thumb = ThumbRect();
            if (thumb != Rectangle.Empty) G.Fill(g, thumb, Dpi.F(3), dragThumb ? Theme.Muted : Theme.Surface3);
            g.ResetClip();
            if (dropActive) PaintDrop(g);
        }

        void PaintHead(Graphics g, string text, int col, Rectangle r, Font f)
        {
            G.Text(g, text, f, r, SortCol == col ? Theme.Text : Theme.Faint, G.Left);
            if (SortCol == col)
            {
                int tw = G.TextW(text, f);
                G.Glyph(g, SortAsc ? Ico.Up : Ico.Down, 7f, new Rectangle(r.X + tw + Dpi.D(2), 0, Dpi.D(16), HeadH), Theme.AccentSoft);
            }
        }

        static void Split(string full, out string name, out string folder)
        {
            name = full; folder = "";
            int slash = full.LastIndexOf('/');
            if (slash >= 0) { folder = full.Substring(0, slash); name = full.Substring(slash + 1); }
        }

        void PaintRow(Graphics g, int i)
        {
            var en = items[i];
            int y = HeadH + Dpi.D(4) + i * RowH - (int)scroll;
            var row = new RectangleF(Dpi.D(8), y + Dpi.D(2), Width - Dpi.D(26), RowH - Dpi.D(4));
            bool s = sel.Contains(en);
            if (s)
            {
                G.Gradient(g, row, Dpi.F(12), Theme.A(Theme.Accent, 64), Theme.A(Theme.Accent, 22), 0f);
                G.Stroke(g, row, Dpi.F(12), Theme.A(Theme.AccentSoft, 70), 1f);
            }
            else if (i == hover) G.Fill(g, row, Dpi.F(12), Theme.Surface2);

            int k = Kinds.Of(en.Name);
            Color kc = Kinds.Color(k);
            var badge = new RectangleF(row.X + Dpi.D(12), y + (RowH - Dpi.D(40)) / 2f, Dpi.D(40), Dpi.D(40));
            Bitmap th = ThumbFor != null ? ThumbFor(en) : null;
            if (th != null) G.ImageCover(g, th, badge, Dpi.F(11));
            else
            {
                G.Fill(g, badge, Dpi.F(11), Theme.A(kc, 38));
                G.Glyph(g, Kinds.Glyphs[k], 13f, Rectangle.Round(badge), kc);
            }

            string name, folder;
            Split(en.Name, out name, out folder);
            string sub = Kinds.Label(en.Name) + (folder.Length > 0 ? "   ·   " + folder.Replace("/", " / ") : "");
            int nx = (int)row.X + Dpi.D(64);
            int nw = SizeX - nx - Dpi.D(36);
            var nf = Theme.Semi(10.5f);
            G.Text(g, name, nf, new Rectangle(nx, y + Dpi.D(9), nw, Dpi.D(22)), Theme.Text, G.Left);
            if (IsFav != null && IsFav(en))
            {
                int tw = Math.Min(nw, G.TextW(name, nf));
                G.Glyph(g, Ico.StarFill, 8.5f, new Rectangle(nx + tw + Dpi.D(2), y + Dpi.D(9), Dpi.D(18), Dpi.D(22)), Theme.Gold);
            }
            G.Text(g, sub, Theme.UI(9f), new Rectangle(nx, y + Dpi.D(31), nw, Dpi.D(18)), s ? Theme.AccentSoft : Theme.Faint, G.Left);
            G.Text(g, Fmt.Size(en.Size), Theme.UI(9.5f), new Rectangle(SizeX, y, Dpi.D(110), RowH), Theme.Muted, G.Left);
            G.Text(g, Fmt.Date(en.Added), Theme.UI(9.5f), new Rectangle(DateX, y, Dpi.D(160), RowH), Theme.Muted, G.Left);
        }

        void PaintTile(Graphics g, int i)
        {
            var en = items[i];
            var r = TileRect(i);
            bool s = sel.Contains(en);
            var rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            if (s)
            {
                G.Gradient(g, rf, Dpi.F(16), Theme.A(Theme.Accent, 70), Theme.A(Theme.Accent, 26), 90f);
                G.Stroke(g, rf, Dpi.F(16), Theme.A(Theme.AccentSoft, 110), 1.4f);
            }
            else G.Fill(g, rf, Dpi.F(16), i == hover ? Theme.Surface2 : Theme.A(Theme.Surface2, 120));

            int k = Kinds.Of(en.Name);
            Color kc = Kinds.Color(k);
            var area = new RectangleF(r.X + Dpi.D(8), r.Y + Dpi.D(8), r.Width - Dpi.D(16), Dpi.D(132));
            Bitmap th = ThumbFor != null ? ThumbFor(en) : null;
            if (th != null) G.ImageCover(g, th, area, Dpi.F(11));
            else
            {
                G.Gradient(g, area, Dpi.F(11), Theme.A(kc, 60), Theme.A(kc, 14), 120f);
                G.Glyph(g, Kinds.Glyphs[k], 26f, Rectangle.Round(area), kc);
                string ext = Kinds.ExtOf(en.Name).ToUpperInvariant();
                if (ext.Length > 0)
                {
                    var ef = Theme.Semi(7.5f);
                    var pill = new RectangleF(area.X + Dpi.D(8), area.Bottom - Dpi.D(26), G.TextW(ext, ef) + Dpi.D(14), Dpi.D(18));
                    G.Fill(g, pill, Dpi.F(9), Theme.A(Color.Black, 90));
                    G.Text(g, ext, ef, Rectangle.Round(pill), Color.White, G.Center);
                }
            }
            if (IsFav != null && IsFav(en))
            {
                var c = new RectangleF(area.Right - Dpi.D(30), area.Y + Dpi.D(6), Dpi.D(24), Dpi.D(24));
                using (var b = new SolidBrush(Theme.A(Color.Black, 130))) g.FillEllipse(b, c);
                G.Glyph(g, Ico.StarFill, 8.5f, Rectangle.Round(c), Theme.Gold);
            }
            string name, folder;
            Split(en.Name, out name, out folder);
            G.Text(g, name, Theme.Semi(9.5f), new Rectangle(r.X + Dpi.D(12), (int)area.Bottom + Dpi.D(10), r.Width - Dpi.D(24), Dpi.D(20)), Theme.Text, G.Left);
            G.Text(g, Fmt.Size(en.Size) + "  ·  " + Fmt.Date(en.Added), Theme.UI(8.5f),
                   new Rectangle(r.X + Dpi.D(12), (int)area.Bottom + Dpi.D(31), r.Width - Dpi.D(24), Dpi.D(18)), s ? Theme.AccentSoft : Theme.Faint, G.Left);
        }

        Rectangle EmptyZone()
        {
            int w = Math.Min(Width - Dpi.D(40), Dpi.D(540)), h = Math.Min(Height - Dpi.D(40), Dpi.D(310));
            return new Rectangle((Width - w) / 2, (Height - h) / 2 - Dpi.D(10), w, h);
        }

        void PaintEmpty(Graphics g)
        {
            var z = EmptyZone();
            if (VaultEmpty)
            {
                G.Fill(g, z, Dpi.F(22), Theme.A(Theme.Accent, hover == -2 ? 24 : 12));
                using (var p = G.Round(z, Dpi.F(22)))
                using (var pen = new Pen(Theme.A(Theme.AccentSoft, hover == -2 ? 160 : 90), Dpi.F(1.6f)))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, p);
                }
                var c = new RectangleF(z.X + (z.Width - Dpi.D(74)) / 2f, z.Y + Dpi.D(46), Dpi.D(74), Dpi.D(74));
                G.Glow(g, RectangleF.Inflate(c, Dpi.D(34), Dpi.D(34)), Theme.Accent, 70);
                G.Gradient(g, c, Dpi.F(22), Theme.Accent, Theme.Accent2, 45f);
                G.Glyph(g, Ico.Upload, 20f, Rectangle.Round(c), Color.White);
                G.Text(g, "Dein Tresor ist noch leer", Theme.Bold(14f), new Rectangle(z.X, (int)c.Bottom + Dpi.D(20), z.Width, Dpi.D(30)), Theme.Text, G.Center);
                G.Text(g, "Zieh Dateien oder ganze Ordner hierher, drück Strg+V – oder klick, um Dateien auszuwählen.",
                       Theme.UI(10f), new Rectangle(z.X + Dpi.D(30), (int)c.Bottom + Dpi.D(54), z.Width - Dpi.D(60), Dpi.D(44)), Theme.Muted, G.Wrap);
            }
            else
            {
                var c = new RectangleF((Width - Dpi.D(64)) / 2f, Height / 2f - Dpi.D(70), Dpi.D(64), Dpi.D(64));
                G.Fill(g, c, Dpi.F(20), Theme.Surface2);
                G.Glyph(g, Ico.Search, 18f, Rectangle.Round(c), Theme.Muted);
                G.Text(g, EmptyTitle, Theme.Bold(13f), new Rectangle(0, (int)c.Bottom + Dpi.D(16), Width, Dpi.D(28)), Theme.Text, G.Center);
                G.Text(g, EmptyText, Theme.UI(10f), new Rectangle(Dpi.D(20), (int)c.Bottom + Dpi.D(46), Width - Dpi.D(40), Dpi.D(44)), Theme.Muted, G.Wrap);
            }
        }

        void PaintDrop(Graphics g)
        {
            var r = new RectangleF(Dpi.D(6), Dpi.D(6), Width - Dpi.D(12), Height - Dpi.D(12));
            G.Fill(g, r, Dpi.F(18), Theme.A(Theme.Bg, 205));
            G.Fill(g, r, Dpi.F(18), Theme.A(Theme.Accent, 40));
            using (var p = G.Round(r, Dpi.F(18)))
            using (var pen = new Pen(Theme.AccentSoft, Dpi.F(2f)))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawPath(pen, p);
            }
            var c = new RectangleF((Width - Dpi.D(74)) / 2f, Height / 2f - Dpi.D(70), Dpi.D(74), Dpi.D(74));
            G.Glow(g, RectangleF.Inflate(c, Dpi.D(34), Dpi.D(34)), Theme.Accent, 90);
            G.Gradient(g, c, Dpi.F(22), Theme.Accent, Theme.Accent2, 45f);
            G.Glyph(g, Ico.Lock, 20f, Rectangle.Round(c), Color.White);
            G.Text(g, "Loslassen zum Verschlüsseln", Theme.Bold(14f), new Rectangle(0, (int)c.Bottom + Dpi.D(18), Width, Dpi.D(30)), Theme.Text, G.Center);
        }

        // ---- input
        protected override bool IsInputKey(Keys k)
        {
            Keys key = k & Keys.KeyCode;
            return key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right ||
                   key == Keys.Home || key == Keys.End || base.IsInputKey(k);
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
            if (!grid && e.Y < HeadH)
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
                if (e.Button == MouseButtons.Left && !ctrl && sel.Count > 0) { sel.Clear(); Raise(SelectionChanged); }
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
            else if (!(sel.Count == 1 && sel.Contains(items[i])))
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
            Cursor = h == -2 || (!grid && e.Y < HeadH && items.Count > 0) ? Cursors.Hand : Cursors.Default;
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
            ScrollTo(target - e.Delta / 120f * Span * (grid ? 0.8f : 1.5f));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (items.Count == 0) return;
            int step = grid ? Cols : 1;
            int c = -1;
            if (e.KeyCode == Keys.Down) c = Math.Min(items.Count - 1, cursor + step);
            else if (e.KeyCode == Keys.Up) c = Math.Max(0, cursor - step);
            else if (grid && e.KeyCode == Keys.Right) c = Math.Min(items.Count - 1, cursor + 1);
            else if (grid && e.KeyCode == Keys.Left) c = Math.Max(0, cursor - 1);
            else if (e.KeyCode == Keys.Home) c = 0;
            else if (e.KeyCode == Keys.End) c = items.Count - 1;
            if (c >= 0)
            {
                if (cursor < 0) c = 0;
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
            else if (e.KeyCode == Keys.F2) { Raise(RenameRequested); e.Handled = true; }
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

    // ------------------------------------------------------------------ preview panel

    class PreviewPanel : Card
    {
        readonly Vault vault;
        VaultEntry entry;
        Bitmap image;
        bool loading;
        int token;
        readonly TextBox text;
        readonly FlatButton bOpen, bExport, bRename, bFav, bDel;
        public event Action<string> ActionRequested;

        public PreviewPanel(Vault v)
        {
            vault = v;
            text = new TextBox();
            text.Multiline = true;
            text.ReadOnly = true;
            text.ScrollBars = ScrollBars.None;   // scrolls with the mouse wheel; the light system scrollbar would break the dark look
            text.BorderStyle = BorderStyle.None;
            text.BackColor = Theme.Surface2;
            text.ForeColor = Theme.Text;
            text.Font = Theme.Mono(9f);
            text.WordWrap = true;
            text.Visible = false;
            Controls.Add(text);

            bOpen = Btn("Öffnen", Ico.Open, BtnKind.Primary, "open");
            bExport = Btn("Export", Ico.Export, BtnKind.Secondary, "export");
            bRename = Btn("Umbenennen", Ico.Rename, BtnKind.Secondary, "rename");
            bFav = Btn("Favorit", Ico.Star, BtnKind.Secondary, "fav");
            bDel = Btn("Löschen", Ico.Delete, BtnKind.DangerGhost, "delete");
        }

        FlatButton Btn(string t, string glyph, BtnKind k, string action)
        {
            var b = new FlatButton(t, glyph, k);
            b.Height = Dpi.D(38);
            b.Font = Theme.Semi(9.5f);
            b.Click += delegate { if (ActionRequested != null) ActionRequested(action); };
            Controls.Add(b);
            return b;
        }

        Rectangle Area { get { return new Rectangle(Dpi.D(16), Dpi.D(16), Width - Dpi.D(32), Dpi.D(210)); } }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (bDel == null) return;
            var a = Area;
            text.SetBounds(a.X + Dpi.D(10), a.Y + Dpi.D(10), a.Width - Dpi.D(14), a.Height - Dpi.D(20));
            int w = (Width - Dpi.D(42)) / 2, x1 = Dpi.D(16), x2 = x1 + w + Dpi.D(10);
            int y = Height - Dpi.D(16) - Dpi.D(38);
            bDel.SetBounds(x1, y, Width - Dpi.D(32), Dpi.D(38));
            y -= Dpi.D(46);
            bRename.SetBounds(x1, y, w, Dpi.D(38)); bFav.SetBounds(x2, y, w, Dpi.D(38));
            y -= Dpi.D(46);
            bOpen.SetBounds(x1, y, w, Dpi.D(38)); bExport.SetBounds(x2, y, w, Dpi.D(38));
        }

        public VaultEntry Entry { get { return entry; } }

        public void Refresh(VaultEntry en)
        {
            bool same = en == entry && en != null;
            entry = en;
            bOpen.Text = en != null && Kinds.IsImage(en.Name) ? "Ansehen" : "Öffnen";
            bOpen.Glyph = en != null && Kinds.IsImage(en.Name) ? Ico.Eye : Ico.Open;
            bool fav = en != null && vault.IsFavorite(en);
            bFav.Glyph = fav ? Ico.StarFill : Ico.Star;
            bFav.Text = fav ? "Favorit ✓" : "Favorit";
            foreach (Control c in Controls) c.Invalidate();
            if (!same) LoadImage();
            Invalidate();
        }

        async void LoadImage()
        {
            int my = ++token;
            if (image != null) { image.Dispose(); image = null; }
            text.Visible = false;
            text.Text = "";
            var en = entry;
            if (en == null) return;
            if (Kinds.IsImage(en.Name) && en.Size <= Imaging.MaxImageBytes)
            {
                loading = true; Invalidate();
                Bitmap b = null;
                try { b = await Task.Run(() => Imaging.Decode(vault, en, Dpi.D(640))); } catch { }
                if (my != token || IsDisposed) { if (b != null) b.Dispose(); return; }
                image = b; loading = false; Invalidate();
            }
            else if (Kinds.IsText(en.Name) && en.Size <= 2 * 1024 * 1024)
            {
                loading = true; Invalidate();
                string s = null;
                try { s = await Task.Run(() => { byte[] d = vault.ReadAll(en, 2 * 1024 * 1024); try { return Imaging.DecodeText(d, 60000); } finally { Array.Clear(d, 0, d.Length); } }); } catch { }
                if (my != token || IsDisposed) return;
                loading = false;
                if (s != null) { text.Text = s; text.Visible = true; text.SelectionStart = 0; }
                Invalidate();
            }
            else { loading = false; Invalidate(); }
        }

        public void Clear() { token++; if (image != null) { image.Dispose(); image = null; } entry = null; text.Text = ""; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            G.HQ(g);
            if (entry == null) return;
            var a = Area;
            G.Fill(g, a, Dpi.F(14), Theme.Surface2);
            int k = Kinds.Of(entry.Name);
            Color kc = Kinds.Color(k);
            if (image != null)
            {
                var state = g.Save();
                using (var p = G.Round(a, Dpi.F(14))) g.SetClip(p, CombineMode.Intersect);
                G.ImageFit(g, image, RectangleF.Inflate(a, -Dpi.D(6), -Dpi.D(6)));
                g.Restore(state);
            }
            else if (!text.Visible)
            {
                G.Gradient(g, a, Dpi.F(14), Theme.A(kc, 56), Theme.A(kc, 10), 120f);
                if (loading) G.Text(g, "Wird entschlüsselt…", Theme.UI(9.5f), a, Theme.Muted, G.Center);
                else G.Glyph(g, Kinds.Glyphs[k], 34f, a, kc);
            }

            string name = entry.Name, folder = "";
            int slash = name.LastIndexOf('/');
            if (slash >= 0) { folder = name.Substring(0, slash).Replace("/", " / "); name = name.Substring(slash + 1); }
            int x = Dpi.D(18), w = Width - Dpi.D(36), y = a.Bottom + Dpi.D(16);
            var nf = Theme.Bold(12.5f);
            int nh = Math.Min(Dpi.D(48), G.TextH(name, nf, w));
            G.Text(g, name, nf, new Rectangle(x, y, w, nh), Theme.Text, G.WrapLeft | TextFormatFlags.EndEllipsis);
            y += nh + Dpi.D(2);
            G.Text(g, Kinds.Label(entry.Name), Theme.UI(9f), new Rectangle(x, y, w, Dpi.D(20)), kc, G.Left);
            y += Dpi.D(30);

            string[,] rows =
            {
                { "Größe", Fmt.Size(entry.Size) },
                { "Hinzugefügt", Fmt.LongDate(entry.Added) },
                { "Ordner", folder.Length > 0 ? folder : "–" },
                { "Schutz", "AES-256 verschlüsselt" },
            };
            int limit = bOpen.Top - Dpi.D(10);
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                if (y + Dpi.D(26) > limit) break;
                G.Text(g, rows[i, 0], Theme.UI(9f), new Rectangle(x, y, Dpi.D(100), Dpi.D(24)), Theme.Faint, G.Left);
                G.Text(g, rows[i, 1], Theme.Semi(9f), new Rectangle(x + Dpi.D(96), y, w - Dpi.D(96), Dpi.D(24)),
                       i == 3 ? Theme.Success : Theme.Text, G.Right);
                y += Dpi.D(26);
                if (i < rows.GetLength(0) - 1)
                    using (var p = new Pen(Theme.A(Theme.Border, 160))) g.DrawLine(p, x, y - Dpi.D(2), x + w, y - Dpi.D(2));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { token++; if (image != null) image.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------------------ image viewer

    class ImageViewerForm : ModernForm
    {
        readonly Vault vault;
        readonly List<VaultEntry> images;
        int index, token;
        Bitmap img;
        bool loading, failed, dragging;
        float zoom = 1f;
        PointF pan;
        Point dragStart;
        PointF panStart;
        readonly IconButton bPrev, bNext, bZoomIn, bZoomOut, bFit, cClose, cMax;

        public ImageViewerForm(Vault v, List<VaultEntry> list, int start)
        {
            vault = v; images = list; index = Math.Max(0, start);
            Text = "un0yy Vault – Bildbetrachter";
            Resizable = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(6, 6, 10);
            CaptionHeight = Dpi.D(58);
            MinimumSize = new Size(Dpi.D(640), Dpi.D(420));
            var wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size((int)(wa.Width * 0.86), (int)(wa.Height * 0.88));

            cClose = CaptionButton(Ico.Close, true);
            cClose.Click += delegate { Close(); };
            cMax = CaptionButton(Ico.Max, false);
            cMax.Click += delegate { ToggleMax(); };
            bZoomOut = Tool(Ico.ZoomOut, delegate { ZoomBy(1 / 1.25f, Center()); });
            bZoomIn = Tool(Ico.ZoomIn, delegate { ZoomBy(1.25f, Center()); });
            bFit = Tool(Ico.Fit, delegate { zoom = 1; pan = PointF.Empty; Invalidate(); });
            bPrev = Nav(Ico.Prev, delegate { Go(-1); });
            bNext = Nav(Ico.Next, delegate { Go(1); });
            Resize += delegate { LayoutButtons(); Invalidate(); };
            LayoutButtons();
            LoadImage();
        }

        IconButton Tool(string glyph, EventHandler click)
        {
            var b = new IconButton(glyph);
            b.GlyphSize = 11f;
            b.Size = new Size(Dpi.D(38), Dpi.D(38));
            b.Click += click;
            Controls.Add(b);
            return b;
        }

        IconButton Nav(string glyph, EventHandler click)
        {
            var b = new IconButton(glyph);
            b.GlyphSize = 16f;
            b.Size = new Size(Dpi.D(52), Dpi.D(52));
            b.Click += click;
            Controls.Add(b);
            return b;
        }

        void LayoutButtons()
        {
            int W = ClientSize.Width;
            cClose.Location = new Point(W - cClose.Width, 0);
            cMax.Location = new Point(W - cClose.Width * 2, 0);
            cMax.Glyph = WindowState == FormWindowState.Maximized ? Ico.Restore : Ico.Max;
            int x = W - cClose.Width * 2 - Dpi.D(18) - Dpi.D(38);
            bFit.Location = new Point(x, Dpi.D(10)); x -= Dpi.D(44);
            bZoomIn.Location = new Point(x, Dpi.D(10)); x -= Dpi.D(44);
            bZoomOut.Location = new Point(x, Dpi.D(10));
            int cy = CaptionHeight + (ClientSize.Height - CaptionHeight) / 2 - Dpi.D(26);
            bPrev.Location = new Point(Dpi.D(18), cy);
            bNext.Location = new Point(W - Dpi.D(70), cy);
            bPrev.Visible = bNext.Visible = images.Count > 1;
        }

        RectangleF ImageArea { get { return new RectangleF(0, CaptionHeight, ClientSize.Width, ClientSize.Height - CaptionHeight); } }
        PointF Center() { var a = ImageArea; return new PointF(a.X + a.Width / 2, a.Y + a.Height / 2); }

        float BaseScale()
        {
            if (img == null) return 1;
            var a = ImageArea;
            return Math.Min(1f, Math.Min((a.Width - Dpi.D(40)) / img.Width, (a.Height - Dpi.D(40)) / img.Height));
        }

        async void LoadImage()
        {
            int my = ++token;
            if (img != null) { img.Dispose(); img = null; }
            loading = true; failed = false; zoom = 1; pan = PointF.Empty;
            Invalidate();
            var en = images[index];
            Bitmap b = null;
            try { b = await Task.Run(() => Imaging.Decode(vault, en, 7000)); } catch { }
            if (my != token || IsDisposed) { if (b != null) b.Dispose(); return; }
            img = b; loading = false; failed = b == null;
            Invalidate();
        }

        void Go(int d)
        {
            if (images.Count < 2) return;
            index = (index + d + images.Count) % images.Count;
            LoadImage();
        }

        void ZoomBy(float f, PointF at)
        {
            if (img == null) return;
            float nz = Math.Max(0.25f, Math.Min(24f, zoom * f));
            float ratio = nz / zoom;
            var c = Center();
            pan = new PointF(at.X - c.X - (at.X - c.X - pan.X) * ratio, at.Y - c.Y - (at.Y - c.Y - pan.Y) * ratio);
            zoom = nz;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var a = ImageArea;
            if (img != null)
            {
                g.InterpolationMode = dragging || zoom > 2.5f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                float s = BaseScale() * zoom;
                float w = img.Width * s, h = img.Height * s;
                var c = Center();
                g.SetClip(a);
                g.DrawImage(img, c.X - w / 2 + pan.X, c.Y - h / 2 + pan.Y, w, h);
                g.ResetClip();
            }
            G.HQ(g);
            using (var b = new LinearGradientBrush(new RectangleF(0, 0, Width, CaptionHeight + 1), Theme.Panel, Theme.A(Theme.Panel, 220), 90f))
                g.FillRectangle(b, 0, 0, Width, CaptionHeight);
            var en = images[index];
            Art.DrawLock(g, new RectangleF(Dpi.D(18), Dpi.D(15), Dpi.D(28), Dpi.D(28)));
            string name = en.Name.Substring(en.Name.LastIndexOf('/') + 1);
            G.Text(g, name, Theme.Semi(11f), new Rectangle(Dpi.D(58), Dpi.D(8), Width / 2, Dpi.D(24)), Theme.Text, G.Left);
            string info = (index + 1) + " von " + images.Count + "   ·   " + Fmt.Size(en.Size) +
                          (img != null ? "   ·   " + (int)Math.Round(BaseScale() * zoom * 100) + " %" : "") +
                          "   ·   nur im Arbeitsspeicher entschlüsselt";
            G.Text(g, info, Theme.UI(8.5f), new Rectangle(Dpi.D(58), Dpi.D(30), Width / 2 + Dpi.D(100), Dpi.D(20)), Theme.Muted, G.Left);
            if (loading) G.Text(g, "Wird entschlüsselt…", Theme.UI(11f), Rectangle.Round(a), Theme.Muted, G.Center);
            else if (failed) G.Text(g, "Dieses Bild kann nicht angezeigt werden.", Theme.UI(11f), Rectangle.Round(a), Theme.Muted, G.Center);
            PaintFrame(g);
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); ZoomBy(e.Delta > 0 ? 1.2f : 1 / 1.2f, e.Location); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y > CaptionHeight) { dragging = true; dragStart = e.Location; panStart = pan; Cursor = Cursors.SizeAll; }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) { pan = new PointF(panStart.X + e.X - dragStart.X, panStart.Y + e.Y - dragStart.Y); Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (dragging) { dragging = false; Cursor = Cursors.Default; Invalidate(); } }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (img == null || e.Y < CaptionHeight) return;
            if (Math.Abs(zoom - 1) > 0.01f) { zoom = 1; pan = PointF.Empty; Invalidate(); }
            else ZoomBy(Math.Max(2f, 1f / BaseScale()) / zoom, e.Location);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Space) Go(1);
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Back) Go(-1);
            else if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus) ZoomBy(1.25f, Center());
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus) ZoomBy(1 / 1.25f, Center());
            else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) { zoom = 1; pan = PointF.Empty; Invalidate(); }
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Left || k == Keys.Right) { OnKeyDown(new KeyEventArgs(keyData)); return true; }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            token++;
            if (img != null) { img.Dispose(); img = null; }
            base.OnFormClosed(e);
        }
    }
}
