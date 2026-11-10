// un0yy Vault v1.0.0 - password protected, AES-256 encrypted file vault.
// Build: see build.ps1 (uses the C# compiler that ships with Windows / .NET Framework 4.8)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("un0yy Vault")]
[assembly: AssemblyProduct("un0yy Vault")]
[assembly: AssemblyDescription("Password protected, encrypted file vault")]
[assembly: AssemblyCompany("un0yy")]
[assembly: AssemblyCopyright("un0yy")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Un0yyVault
{
    // ------------------------------------------------------------------ crypto / storage

    class VaultEntry
    {
        public string Id;
        public string Name;
        public long Size;
        public DateTime Added; // UTC
    }

    class Vault
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("UN0YYV1");
        const int Iterations = 310000;

        public readonly string Dir;
        readonly string keyPath, indexPath, filesDir;
        byte[] ek, mk; // AES key, HMAC key (only in memory while unlocked)
        public List<VaultEntry> Entries = new List<VaultEntry>();

        public Vault(string dir)
        {
            Dir = dir;
            keyPath = Path.Combine(dir, "vault.key");
            indexPath = Path.Combine(dir, "index.dat");
            filesDir = Path.Combine(dir, "files");
        }

        public bool Exists { get { return File.Exists(keyPath); } }

        // ---- helpers
        static byte[] Rand(int n)
        {
            byte[] b = new byte[n];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(b);
            return b;
        }

        static byte[] Derive(string pw, byte[] salt, int it)
        {
            using (var k = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(pw), salt, it, HashAlgorithmName.SHA256))
                return k.GetBytes(64);
        }

        static byte[] Part(byte[] a, int off, int len)
        {
            byte[] r = new byte[len];
            Buffer.BlockCopy(a, off, r, 0, len);
            return r;
        }

        static byte[] Cat(params byte[][] parts)
        {
            int n = 0;
            foreach (var p in parts) n += p.Length;
            byte[] r = new byte[n];
            int o = 0;
            foreach (var p in parts) { Buffer.BlockCopy(p, 0, r, o, p.Length); o += p.Length; }
            return r;
        }

        static bool Eq(byte[] a, int ao, byte[] b, int bo, int n)
        {
            int d = 0;
            for (int i = 0; i < n; i++) d |= a[ao + i] ^ b[bo + i];
            return d == 0;
        }

        static void ReadExact(Stream s, byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = s.Read(buf, got, count - got);
                if (n <= 0) throw new EndOfStreamException();
                got += n;
            }
        }

        static void WriteAtomic(string path, byte[] data)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); }
                catch (IOException) { File.Copy(tmp, path, true); File.Delete(tmp); }
                catch (PlatformNotSupportedException) { File.Copy(tmp, path, true); File.Delete(tmp); }
            }
            else File.Move(tmp, path);
        }

        // Encrypt-then-MAC: IV(16) | AES-256-CBC ciphertext | HMAC-SHA256(IV|ciphertext)
        static byte[] Seal(byte[] k64, byte[] plain)
        {
            byte[] e = Part(k64, 0, 32), m = Part(k64, 32, 32);
            using (var aes = Aes.Create())
            {
                aes.Key = e; aes.GenerateIV(); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                byte[] ct;
                using (var enc = aes.CreateEncryptor()) ct = enc.TransformFinalBlock(plain, 0, plain.Length);
                byte[] body = Cat(aes.IV, ct);
                byte[] tag;
                using (var h = new HMACSHA256(m)) tag = h.ComputeHash(body);
                return Cat(body, tag);
            }
        }

        static byte[] Unseal(byte[] k64, byte[] data)
        {
            if (data.Length < 16 + 16 + 32) throw new CryptographicException("too short");
            byte[] e = Part(k64, 0, 32), m = Part(k64, 32, 32);
            int bl = data.Length - 32;
            byte[] tag;
            using (var h = new HMACSHA256(m)) tag = h.ComputeHash(data, 0, bl);
            if (!Eq(tag, 0, data, bl, 32)) throw new CryptographicException("auth");
            using (var aes = Aes.Create())
            {
                aes.Key = e; aes.IV = Part(data, 0, 16); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var dec = aes.CreateDecryptor()) return dec.TransformFinalBlock(data, 16, bl - 16);
            }
        }

        byte[] MasterKey { get { return Cat(ek, mk); } }

        void SetKeys(byte[] master)
        {
            ek = Part(master, 0, 32);
            mk = Part(master, 32, 32);
            Array.Clear(master, 0, master.Length);
        }

        void WriteKeyFile(string pw, byte[] master)
        {
            byte[] salt = Rand(16);
            byte[] pk = Derive(pw, salt, Iterations);
            try { WriteAtomic(keyPath, Cat(Magic, salt, BitConverter.GetBytes(Iterations), Seal(pk, master))); }
            finally { Array.Clear(pk, 0, pk.Length); }
        }

        byte[] TryOpenMaster(string pw)
        {
            byte[] data = File.ReadAllBytes(keyPath);
            if (data.Length < Magic.Length + 16 + 4 + 64 || !Eq(data, 0, Magic, 0, Magic.Length))
                throw new InvalidDataException("Die Datei vault.key ist beschädigt.");
            int o = Magic.Length;
            byte[] salt = Part(data, o, 16); o += 16;
            int it = BitConverter.ToInt32(data, o); o += 4;
            if (it < 1000 || it > 50000000) throw new InvalidDataException("Die Datei vault.key ist beschädigt.");
            byte[] pk = Derive(pw, salt, it);
            try { return Unseal(pk, Part(data, o, data.Length - o)); }
            catch (CryptographicException) { return null; }
            finally { Array.Clear(pk, 0, pk.Length); }
        }

        // ---- public API
        public void Create(string pw)
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(filesDir);
            byte[] master = Rand(64);
            WriteKeyFile(pw, master);
            SetKeys(master);
            Entries = new List<VaultEntry>();
            SaveIndex();
        }

        public bool Unlock(string pw)
        {
            byte[] m = TryOpenMaster(pw);
            if (m == null) return false;
            SetKeys(m);
            Directory.CreateDirectory(filesDir);
            LoadIndex();
            return true;
        }

        public bool ChangePassword(string oldPw, string newPw)
        {
            byte[] m = TryOpenMaster(oldPw);
            if (m == null) return false;
            try { WriteKeyFile(newPw, m); }
            finally { Array.Clear(m, 0, m.Length); }
            return true;
        }

        public void Lock()
        {
            if (ek != null) Array.Clear(ek, 0, ek.Length);
            if (mk != null) Array.Clear(mk, 0, mk.Length);
            ek = null; mk = null;
            Entries = new List<VaultEntry>();
        }

        void LoadIndex()
        {
            Entries = new List<VaultEntry>();
            if (!File.Exists(indexPath)) return;
            byte[] plain;
            try { plain = Unseal(MasterKey, File.ReadAllBytes(indexPath)); }
            catch (CryptographicException) { throw new InvalidDataException("Die Dateiliste (index.dat) ist beschädigt."); }
            foreach (string line in Encoding.UTF8.GetString(plain).Split('\n'))
            {
                string[] p = line.Split('\t');
                if (p.Length != 4) continue;
                var en = new VaultEntry();
                en.Id = p[0];
                en.Name = Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));
                en.Size = long.Parse(p[2]);
                en.Added = new DateTime(long.Parse(p[3]), DateTimeKind.Utc);
                Entries.Add(en);
            }
        }

        void SaveIndex()
        {
            var sb = new StringBuilder("UN0YYIDX1\n");
            foreach (var e in Entries)
                sb.Append(e.Id).Append('\t')
                  .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Name))).Append('\t')
                  .Append(e.Size.ToString()).Append('\t')
                  .Append(e.Added.Ticks.ToString()).Append('\n');
            WriteAtomic(indexPath, Seal(MasterKey, Encoding.UTF8.GetBytes(sb.ToString())));
        }

        string BlobPath(string id) { return Path.Combine(filesDir, id + ".bin"); }

        public VaultEntry AddFile(string src, string name)
        {
            var en = new VaultEntry();
            en.Id = Guid.NewGuid().ToString("N");
            en.Name = name;
            en.Size = new FileInfo(src).Length;
            en.Added = DateTime.UtcNow;
            string dst = BlobPath(en.Id);
            try { EncryptFile(src, dst); }
            catch { try { File.Delete(dst); } catch { } throw; }
            Entries.Add(en);
            SaveIndex();
            return en;
        }

        public void Remove(VaultEntry en)
        {
            Entries.Remove(en);
            SaveIndex();
            try { File.Delete(BlobPath(en.Id)); } catch { }
        }

        public void Export(VaultEntry en, string dst)
        {
            try { DecryptFile(BlobPath(en.Id), dst); }
            catch { try { File.Delete(dst); } catch { } throw; }
        }

        void EncryptFile(string src, string dst)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = ek; aes.GenerateIV(); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using (var outFs = new FileStream(dst, FileMode.CreateNew, FileAccess.ReadWrite))
                {
                    outFs.Write(aes.IV, 0, 16);
                    using (var inFs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var enc = aes.CreateEncryptor())
                    using (var cs = new CryptoStream(outFs, enc, CryptoStreamMode.Write, true))
                    {
                        inFs.CopyTo(cs, 1 << 16);
                        cs.FlushFinalBlock();
                    }
                    outFs.Flush();
                    outFs.Position = 0;
                    byte[] tag;
                    using (var h = new HMACSHA256(mk)) tag = h.ComputeHash(outFs);
                    outFs.Seek(0, SeekOrigin.End);
                    outFs.Write(tag, 0, 32);
                }
            }
        }

        void DecryptFile(string src, string dst)
        {
            using (var inFs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long len = inFs.Length;
                if (len < 16 + 16 + 32) throw new InvalidDataException("Die verschlüsselte Datei ist beschädigt.");
                long bodyLen = len - 32;
                byte[] buf = new byte[1 << 16];
                byte[] tag;
                using (var h = new HMACSHA256(mk))
                {
                    long rem = bodyLen;
                    while (rem > 0)
                    {
                        int n = inFs.Read(buf, 0, (int)Math.Min(buf.Length, rem));
                        if (n <= 0) throw new EndOfStreamException();
                        h.TransformBlock(buf, 0, n, null, 0);
                        rem -= n;
                    }
                    h.TransformFinalBlock(new byte[0], 0, 0);
                    tag = h.Hash;
                }
                byte[] stored = new byte[32];
                ReadExact(inFs, stored, 32);
                if (!Eq(tag, 0, stored, 0, 32))
                    throw new InvalidDataException("Die verschlüsselte Datei wurde verändert oder ist beschädigt.");

                inFs.Position = 0;
                byte[] iv = new byte[16];
                ReadExact(inFs, iv, 16);
                using (var aes = Aes.Create())
                {
                    aes.Key = ek; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                    using (var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write))
                    using (var dec = aes.CreateDecryptor())
                    using (var cs = new CryptoStream(outFs, dec, CryptoStreamMode.Write))
                    {
                        long rem = bodyLen - 16;
                        while (rem > 0)
                        {
                            int n = inFs.Read(buf, 0, (int)Math.Min(buf.Length, rem));
                            if (n <= 0) throw new EndOfStreamException();
                            cs.Write(buf, 0, n);
                            rem -= n;
                        }
                        cs.FlushFinalBlock();
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ look & feel

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(16, 16, 22);
        public static readonly Color Panel = Color.FromArgb(26, 26, 36);
        public static readonly Color Row2 = Color.FromArgb(30, 30, 42);
        public static readonly Color Field = Color.FromArgb(40, 40, 56);
        public static readonly Color Text = Color.FromArgb(236, 236, 246);
        public static readonly Color Muted = Color.FromArgb(150, 150, 172);
        public static readonly Color Accent = Color.FromArgb(124, 77, 255);
        public static readonly Color Accent2 = Color.FromArgb(40, 180, 255);
        public static readonly Color Select = Color.FromArgb(66, 46, 140);
        public static readonly Color Danger = Color.FromArgb(226, 68, 92);
        public static readonly Color Warn = Color.FromArgb(255, 190, 80);

        public static Font F(float size, FontStyle st) { return new Font("Segoe UI", size, st); }
    }

    static class Ui
    {
        public static Button Btn(string text, Color back, int width)
        {
            var b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.BorderColor = back;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.25f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(back, 0.05f);
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.Font = Theme.F(10f, FontStyle.Bold);
            b.Size = new Size(width, 38);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(0, 0, 10, 0);
            b.UseVisualStyleBackColor = false;
            return b;
        }

        public static TextBox Field(bool password)
        {
            var t = new TextBox();
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Theme.Field;
            t.ForeColor = Theme.Text;
            t.Font = Theme.F(12f, FontStyle.Regular);
            t.UseSystemPasswordChar = password;
            return t;
        }

        public static Label Lbl(string text, float size, FontStyle st, Color c)
        {
            var l = new Label();
            l.Text = text;
            l.Font = Theme.F(size, st);
            l.ForeColor = c;
            l.BackColor = Color.Transparent;
            l.AutoSize = true;
            return l;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);

        public static void Placeholder(TextBox t, string text)
        {
            EventHandler apply = delegate { SendMessage(t.Handle, 0x1501, (IntPtr)1, text); };
            if (t.IsHandleCreated) apply(t, EventArgs.Empty); else t.HandleCreated += apply;
        }
    }

    static class Art
    {
        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawLock(Graphics g, RectangleF r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float w = r.Width, h = r.Height;
            using (var path = RoundRect(r, w * 0.24f))
            using (var br = new LinearGradientBrush(r, Theme.Accent, Theme.Accent2, 45f))
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
            using (var path = RoundRect(body, w * 0.07f))
            using (var wb = new SolidBrush(Color.White))
                g.FillPath(wb, path);
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

        // writes a multi-size .ico (PNG entries) - used by build.ps1
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
            {
                using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Transparent);
                        DrawLock(g, new RectangleF(0, 0, s, s));
                    }
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

    class LogoBox : Control
    {
        public LogoBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Art.DrawLock(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1));
        }
    }

    class DarkForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        public DarkForm()
        {
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(10f, FontStyle.Regular);
            Icon = Art.AppIcon;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int v = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref v, 4);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------ login / create

    class LoginForm : DarkForm
    {
        readonly Vault vault;
        readonly bool create;
        TextBox pw, pw2;
        Label err;
        Button go;
        int fails;
        DateTime lockedUntil = DateTime.MinValue;
        Timer cooldown;

        public LoginForm(Vault v)
        {
            vault = v;
            create = !v.Exists;
            Text = "un0yy Vault";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            int W = 420;
            ClientSize = new Size(W, create ? 640 : 480);

            int x = 40, w = W - 80, y = 32;
            var logo = new LogoBox();
            logo.SetBounds((W - 84) / 2, y, 84, 84);
            Controls.Add(logo);
            y += 100;

            Controls.Add(Center("un0yy Vault", 20f, FontStyle.Bold, Theme.Text, y, 44)); y += 46;
            Controls.Add(Center(create ? "Erstelle deinen privaten Tresor" : "Gib dein Master-Passwort ein",
                                10.5f, FontStyle.Regular, Theme.Muted, y, 26)); y += 44;

            Controls.Add(At(Ui.Lbl(create ? "Neues Master-Passwort" : "Passwort", 9.5f, FontStyle.Bold, Theme.Muted), x, y)); y += 24;
            pw = Ui.Field(true); pw.SetBounds(x, y, w, 30); Controls.Add(pw); y += 46;

            if (create)
            {
                Controls.Add(At(Ui.Lbl("Passwort wiederholen", 9.5f, FontStyle.Bold, Theme.Muted), x, y)); y += 24;
                pw2 = Ui.Field(true); pw2.SetBounds(x, y, w, 30); Controls.Add(pw2); y += 46;
            }

            var show = new CheckBox();
            show.Text = "Passwort anzeigen";
            show.ForeColor = Theme.Muted;
            show.FlatStyle = FlatStyle.Flat;
            show.AutoSize = true;
            show.Location = new Point(x, y);
            show.CheckedChanged += delegate
            {
                pw.UseSystemPasswordChar = !show.Checked;
                if (pw2 != null) pw2.UseSystemPasswordChar = !show.Checked;
            };
            Controls.Add(show);
            y += 38;

            go = Ui.Btn(create ? "Tresor erstellen" : "Entsperren", Theme.Accent, w);
            go.SetBounds(x, y, w, 44);
            go.Font = Theme.F(11f, FontStyle.Bold);
            go.Click += OnGo;
            Controls.Add(go);
            AcceptButton = go;
            y += 54;

            err = new Label();
            err.ForeColor = Theme.Danger;
            err.TextAlign = ContentAlignment.MiddleCenter;
            err.SetBounds(x, y, w, 40);
            Controls.Add(err);
            y += 44;

            if (create)
            {
                var warn = new Label();
                warn.Text = "Achtung: Merk dir dein Passwort gut! Wenn du es vergisst, kann NIEMAND deine Dateien " +
                            "wiederherstellen - auch du nicht. Mindestens 8 Zeichen.";
                warn.ForeColor = Theme.Warn;
                warn.Font = Theme.F(9f, FontStyle.Regular);
                warn.TextAlign = ContentAlignment.TopCenter;
                warn.SetBounds(x, y, w, 60);
                Controls.Add(warn);
            }

            cooldown = new Timer();
            cooldown.Interval = 250;
            cooldown.Tick += delegate
            {
                double rem = (lockedUntil - DateTime.Now).TotalSeconds;
                if (rem <= 0)
                {
                    cooldown.Stop();
                    go.Enabled = true;
                    err.Text = "Falsches Passwort. Versuch es nochmal.";
                    pw.Focus();
                }
                else err.Text = "Zu viele Fehlversuche - warte " + Math.Ceiling(rem) + " s.";
            };

            Shown += delegate { pw.Focus(); };
        }

        static Control At(Control c, int x, int y) { c.Location = new Point(x, y); return c; }

        Label Center(string text, float size, FontStyle st, Color c, int y, int h)
        {
            var l = Ui.Lbl(text, size, st, c);
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.SetBounds(0, y, ClientSize.Width, h);
            return l;
        }

        void OnGo(object sender, EventArgs e)
        {
            if (DateTime.Now < lockedUntil) return;
            err.Text = "";
            if (create)
            {
                if (pw.Text.Length < 8) { err.Text = "Das Passwort muss mindestens 8 Zeichen lang sein."; return; }
                if (pw.Text != pw2.Text) { err.Text = "Die Passwörter stimmen nicht überein."; return; }
                Cursor = Cursors.WaitCursor;
                try { vault.Create(pw.Text); }
                catch (Exception ex) { Cursor = Cursors.Default; err.Text = "Fehler: " + ex.Message; return; }
                Cursor = Cursors.Default;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            go.Enabled = false;
            Cursor = Cursors.WaitCursor;
            Application.DoEvents();
            bool ok = false;
            string problem = null;
            try { ok = vault.Unlock(pw.Text); }
            catch (Exception ex) { problem = ex.Message; }
            Cursor = Cursors.Default;
            go.Enabled = true;

            if (ok) { DialogResult = DialogResult.OK; Close(); return; }
            if (problem != null) { err.Text = "Tresor konnte nicht geöffnet werden: " + problem; return; }

            fails++;
            pw.SelectAll();
            pw.Focus();
            int wait = fails >= 3 ? Math.Min(60, 1 << (fails - 2)) : 0;
            if (wait > 0)
            {
                lockedUntil = DateTime.Now.AddSeconds(wait);
                go.Enabled = false;
                err.Text = "Zu viele Fehlversuche - warte " + wait + " s.";
                cooldown.Start();
            }
            else err.Text = "Falsches Passwort.";
        }
    }

    // ------------------------------------------------------------------ change password

    class ChangePasswordForm : DarkForm
    {
        readonly Vault vault;
        TextBox oldPw, newPw, newPw2;
        Label err;

        public ChangePasswordForm(Vault v)
        {
            vault = v;
            Text = "Passwort ändern";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 370);
            int x = 30, w = 340, y = 24;
            oldPw = Add("Aktuelles Passwort", x, w, ref y);
            newPw = Add("Neues Passwort (mind. 8 Zeichen)", x, w, ref y);
            newPw2 = Add("Neues Passwort wiederholen", x, w, ref y);
            err = new Label();
            err.ForeColor = Theme.Danger;
            err.TextAlign = ContentAlignment.MiddleCenter;
            err.SetBounds(x, y, w, 36);
            Controls.Add(err);
            y += 42;
            var ok = Ui.Btn("Speichern", Theme.Accent, 165);
            ok.Location = new Point(x, y);
            ok.Click += OnOk;
            var cancel = Ui.Btn("Abbrechen", Theme.Field, 165);
            cancel.Location = new Point(x + w - 165, y);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        TextBox Add(string label, int x, int w, ref int y)
        {
            var l = Ui.Lbl(label, 9.5f, FontStyle.Bold, Theme.Muted);
            l.Location = new Point(x, y);
            Controls.Add(l);
            y += 24;
            var t = Ui.Field(true);
            t.SetBounds(x, y, w, 30);
            Controls.Add(t);
            y += 46;
            return t;
        }

        void OnOk(object sender, EventArgs e)
        {
            if (newPw.Text.Length < 8) { err.Text = "Das neue Passwort braucht mindestens 8 Zeichen."; return; }
            if (newPw.Text != newPw2.Text) { err.Text = "Die neuen Passwörter stimmen nicht überein."; return; }
            Cursor = Cursors.WaitCursor;
            bool ok;
            try { ok = vault.ChangePassword(oldPw.Text, newPw.Text); }
            catch (Exception ex) { Cursor = Cursors.Default; err.Text = "Fehler: " + ex.Message; return; }
            Cursor = Cursors.Default;
            if (!ok) { err.Text = "Das aktuelle Passwort ist falsch."; oldPw.SelectAll(); oldPw.Focus(); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ------------------------------------------------------------------ main window

    class MainForm : DarkForm, IMessageFilter
    {
        const int AutoLockMinutes = 10;
        readonly Vault vault;
        public bool LockRequested;
        ListView list;
        Label status, headerSub, empty;
        TextBox search;
        Button bAdd, bOpen, bSave, bDel, bPw, bLock;
        string tempDir;
        DateTime lastActivity = DateTime.Now;
        Timer idle;

        public MainForm(Vault v)
        {
            vault = v;
            Text = "un0yy Vault";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 640);
            MinimumSize = new Size(800, 500);
            AllowDrop = true;

            // footer
            var footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 30;
            footer.BackColor = Theme.Panel;
            status = Ui.Lbl("", 9f, FontStyle.Regular, Theme.Muted);
            status.AutoSize = false;
            status.Dock = DockStyle.Fill;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.Padding = new Padding(14, 0, 0, 0);
            status.AutoEllipsis = true;
            footer.Controls.Add(status);

            // list
            list = new ListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HideSelection = false;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.Panel;
            list.ForeColor = Theme.Text;
            list.Font = Theme.F(10f, FontStyle.Regular);
            list.AllowDrop = true;
            list.OwnerDraw = true;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            var rowHeight = new ImageList();
            rowHeight.ImageSize = new Size(1, 32);
            list.SmallImageList = rowHeight;
            list.Columns.Add("Name", 470);
            list.Columns.Add("Typ", 90);
            list.Columns.Add("Größe", 100);
            list.Columns.Add("Hinzugefügt", 160);
            list.DrawColumnHeader += DrawHeader;
            list.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { e.DrawDefault = false; };
            list.DrawSubItem += DrawCell;
            list.SelectedIndexChanged += delegate { UpdateButtons(); list.Invalidate(); };
            list.DoubleClick += delegate { OpenSelected(); };
            list.KeyDown += OnListKey;
            list.DragEnter += OnDragEnter;
            list.DragDrop += OnDragDrop;

            empty = Ui.Lbl("Dein Tresor ist leer.\n\nZieh Dateien oder Ordner hierher\noder klick auf \"+  Hinzufügen\".",
                           12f, FontStyle.Regular, Theme.Muted);
            empty.AutoSize = false;
            empty.Dock = DockStyle.Fill;
            empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.BackColor = Theme.Panel;
            empty.AllowDrop = true;
            empty.DragEnter += OnDragEnter;
            empty.DragDrop += OnDragDrop;
            empty.Click += delegate { AddViaDialog(); };
            empty.Cursor = Cursors.Hand;
            list.Controls.Add(empty);

            // toolbar
            var bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 64;
            bar.Padding = new Padding(16, 13, 16, 0);
            bar.BackColor = Theme.Bg;
            bar.WrapContents = false;
            bAdd = Ui.Btn("+  Hinzufügen", Theme.Accent, 150);
            bOpen = Ui.Btn("Öffnen", Theme.Field, 100);
            bSave = Ui.Btn("Speichern unter...", Theme.Field, 160);
            bDel = Ui.Btn("Löschen", Theme.Field, 100);
            bPw = Ui.Btn("Passwort ändern", Theme.Field, 160);
            bAdd.Click += delegate { AddViaDialog(); };
            bOpen.Click += delegate { OpenSelected(); };
            bSave.Click += delegate { ExportSelected(); };
            bDel.Click += delegate { DeleteSelected(); };
            bPw.Click += delegate
            {
                using (var f = new ChangePasswordForm(vault))
                    if (f.ShowDialog(this) == DialogResult.OK)
                        MessageBox.Show(this, "Dein Passwort wurde geändert.", "un0yy Vault", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            bar.Controls.AddRange(new Control[] { bAdd, bOpen, bSave, bDel, bPw });

            // header
            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 78;
            header.BackColor = Theme.Panel;
            header.Width = ClientSize.Width;
            var logo = new LogoBox();
            logo.SetBounds(20, 16, 46, 46);
            var title = Ui.Lbl("un0yy Vault", 15f, FontStyle.Bold, Theme.Text);
            title.Location = new Point(76, 13);
            headerSub = Ui.Lbl("", 9.5f, FontStyle.Regular, Theme.Muted);
            headerSub.Location = new Point(78, 45);
            bLock = Ui.Btn("Sperren", Theme.Danger, 110);
            bLock.Location = new Point(header.Width - 130, 20);
            bLock.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bLock.Click += delegate { DoLock(); };
            search = Ui.Field(false);
            search.Font = Theme.F(11f, FontStyle.Regular);
            search.SetBounds(header.Width - 130 - 250, 24, 236, 28);
            search.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            search.TextChanged += delegate { RefreshList(); };
            Ui.Placeholder(search, "Suchen...");
            header.Controls.AddRange(new Control[] { logo, title, headerSub, search, bLock });

            Controls.Add(list);
            Controls.Add(footer);
            Controls.Add(bar);
            Controls.Add(header);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            idle = new Timer();
            idle.Interval = 15000;
            idle.Tick += delegate
            {
                if ((DateTime.Now - lastActivity).TotalMinutes >= AutoLockMinutes && CanFocus) DoLock();
            };
            idle.Start();
            Application.AddMessageFilter(this);

            SetStatus(null);
            RefreshList();
            Resize += delegate { FitColumns(); };
            Shown += delegate { FitColumns(); };
        }

        // ---- drawing
        void DrawHeader(object s, DrawListViewColumnHeaderEventArgs e)
        {
            using (var b = new SolidBrush(Theme.Field)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.F(9.5f, FontStyle.Bold), r, Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        void DrawCell(object s, DrawListViewSubItemEventArgs e)
        {
            bool sel = e.Item.Selected;
            Color back = sel ? Theme.Select : (e.ItemIndex % 2 == 0 ? Theme.Panel : Theme.Row2);
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, r,
                e.ColumnIndex == 0 || sel ? Theme.Text : Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        void FitColumns()
        {
            int rest = list.ClientSize.Width - list.Columns[1].Width - list.Columns[2].Width - list.Columns[3].Width;
            if (rest > 200) list.Columns[0].Width = rest;
        }

        // ---- helpers
        static string Fmt(long b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double d = b;
            int i = 0;
            while (d >= 1024 && i < u.Length - 1) { d /= 1024; i++; }
            return (i == 0 ? b.ToString() : d.ToString("0.#")) + " " + u[i];
        }

        static string TypeOf(string name)
        {
            string ext = Path.GetExtension(name.Replace('/', '\\'));
            return string.IsNullOrEmpty(ext) ? "Datei" : ext.TrimStart('.').ToUpperInvariant();
        }

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

        List<VaultEntry> Selected()
        {
            var r = new List<VaultEntry>();
            foreach (ListViewItem it in list.SelectedItems) r.Add((VaultEntry)it.Tag);
            return r;
        }

        void SetStatus(string text)
        {
            status.Text = text ?? ("AES-256 verschlüsselt   ·   Automatische Sperre nach " + AutoLockMinutes +
                                   " Min.   ·   Speicherort: " + vault.Dir);
        }

        void UpdateButtons()
        {
            int n = list.SelectedItems.Count;
            bOpen.Enabled = n > 0;
            bSave.Enabled = n > 0;
            bDel.Enabled = n > 0;
        }

        void RefreshList()
        {
            string q = search.Text.Trim().ToLowerInvariant();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var en in vault.Entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (q.Length > 0 && !en.Name.ToLowerInvariant().Contains(q)) continue;
                var it = new ListViewItem(en.Name);
                it.SubItems.Add(TypeOf(en.Name));
                it.SubItems.Add(Fmt(en.Size));
                it.SubItems.Add(en.Added.ToLocalTime().ToString("dd.MM.yyyy  HH:mm"));
                it.Tag = en;
                list.Items.Add(it);
            }
            list.EndUpdate();
            empty.Visible = vault.Entries.Count == 0;
            long total = vault.Entries.Sum(x => x.Size);
            headerSub.Text = "Entsperrt   ·   " + vault.Entries.Count + (vault.Entries.Count == 1 ? " Datei" : " Dateien") +
                             "   ·   " + Fmt(total);
            UpdateButtons();
        }

        // ---- add
        void AddViaDialog()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Dateien zum Tresor hinzufügen";
                d.Multiselect = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                var items = d.FileNames.Select(f => new KeyValuePair<string, string>(f, Path.GetFileName(f))).ToList();
                AddItems(items, new List<string>());
            }
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
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
                            items.Add(new KeyValuePair<string, string>(f,
                                baseName + "/" + f.Substring(root.Length + 1).Replace('\\', '/')));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Ordner konnte nicht gelesen werden:\n" + ex.Message, "un0yy Vault",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                SetStatus("Verschlüssele " + (i + 1) + " / " + items.Count + ":  " + items[i].Value);
                Application.DoEvents();
                try
                {
                    vault.AddFile(items[i].Key, UniqueName(items[i].Value));
                    done.Add(items[i].Key);
                }
                catch (Exception ex) { errors.Add(items[i].Value + ": " + ex.Message); }
            }
            Cursor = Cursors.Default;
            SetStatus(null);
            RefreshList();

            if (errors.Count > 0)
                MessageBox.Show(this, "Diese Dateien konnten nicht hinzugefügt werden:\n\n" +
                    string.Join("\n", errors.Take(15)), "un0yy Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (done.Count == 0) return;

            var answer = MessageBox.Show(this,
                done.Count + (done.Count == 1 ? " Datei ist" : " Dateien sind") + " jetzt verschlüsselt im Tresor.\n\n" +
                "Sollen die Originale vom PC gelöscht werden, damit niemand sie ohne Passwort sehen kann?\n\n" +
                "Ja = Originale löschen      Nein = Originale behalten",
                "un0yy Vault", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            int failed = 0;
            foreach (string f in done)
            {
                try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { failed++; }
            }
            foreach (string d in folders)
            {
                try { if (Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length == 0) Directory.Delete(d, true); }
                catch { }
            }
            if (failed > 0)
                MessageBox.Show(this, failed + " Original(e) konnten nicht gelöscht werden (evtl. noch geöffnet).",
                    "un0yy Vault", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ---- open / export / delete
        void OpenSelected()
        {
            var sel = Selected();
            if (sel.Count == 0) return;
            if (sel.Count > 10) { MessageBox.Show(this, "Bitte höchstens 10 Dateien auf einmal öffnen.", "un0yy Vault"); return; }
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
                SetStatus("Geöffnet als temporäre Kopie - wird beim Sperren gelöscht. Änderungen daran werden NICHT im Tresor gespeichert.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Datei konnte nicht geöffnet werden:\n" + ex.Message, "un0yy Vault",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Cursor = Cursors.Default; }
        }

        void ExportSelected()
        {
            var sel = Selected();
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
                SetStatus("Gespeichert: " + sel.Count + (sel.Count == 1 ? " Datei" : " Dateien") +
                          " entschlüsselt exportiert (die Kopie ist NICHT geschützt).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Speichern fehlgeschlagen:\n" + ex.Message, "un0yy Vault",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Cursor = Cursors.Default; }
        }

        void DeleteSelected()
        {
            var sel = Selected();
            if (sel.Count == 0) return;
            string what = sel.Count == 1 ? "\"" + sel[0].Name + "\"" : sel.Count + " Dateien";
            if (MessageBox.Show(this, what + " endgültig aus dem Tresor löschen?\n\nDas kann nicht rückgängig gemacht werden.",
                    "Löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            foreach (var en in sel)
            {
                try { vault.Remove(en); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Löschen fehlgeschlagen:\n" + ex.Message, "un0yy Vault");
                    break;
                }
            }
            RefreshList();
        }

        void OnListKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { OpenSelected(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem it in list.Items) it.Selected = true;
                e.Handled = true;
            }
        }

        // ---- lock / cleanup
        void DoLock()
        {
            LockRequested = true;
            Close();
        }

        public bool PreFilterMessage(ref Message m)
        {
            // keyboard / mouse activity resets the auto-lock timer
            if (m.Msg == 0x100 || m.Msg == 0x104 || m.Msg == 0x200 || m.Msg == 0x201 || m.Msg == 0x204 || m.Msg == 0x20A)
                lastActivity = DateTime.Now;
            return false;
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

    // ------------------------------------------------------------------ entry point

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--make-icon") { Art.WriteIco(args[1]); return 0; }
            if (args.Length == 2 && args[0] == "--selftest") return SelfTest(args[1]);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
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

        // automated check of the crypto layer, used by build.ps1
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
                var en = v.AddFile(src, "ordner/hallo.bin");
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
                if (raw.Length < content.Length) throw new Exception("blob too small");
                raw[raw.Length / 2] ^= 0x01;
                File.WriteAllBytes(blob, raw);
                bool tamperCaught = false;
                try { v2.Export(v2.Entries[0], outp); } catch (InvalidDataException) { tamperCaught = true; }
                if (!tamperCaught) throw new Exception("tampering not detected");
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

                string keyText = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(data, "index.dat")));
                if (keyText.Contains("hallo")) throw new Exception("file name visible in index");
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
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
