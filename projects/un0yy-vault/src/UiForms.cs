// un0yy Vault - windows: base window, dialogs, login, settings
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Un0yyVault
{
    class ModernForm : Form
    {
        public int CaptionHeight;
        public bool Resizable;
        protected bool FadeIn;
        Timer fade;

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
            try { int v = 2; Native.DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
            try { int v = 1; Native.DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
            UpdateMaxBounds();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!FadeIn) return;
            fade = new Timer();
            fade.Interval = 15;
            fade.Tick += delegate
            {
                double o = Math.Min(1.0, Opacity + 0.09);
                Opacity = o;
                if (o >= 1.0) { fade.Stop(); fade.Dispose(); fade = null; }
            };
            fade.Start();
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
                if (p.Y < CaptionHeight) m.Result = (IntPtr)2;
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

        public static void RepaintAll(Control c)
        {
            c.Invalidate();
            foreach (Control ch in c.Controls) RepaintAll(ch);
        }
    }

    enum DlgKind { Info, Question, Warn, Danger, Success }

    class ModernDialog : ModernForm
    {
        readonly string title, message;
        readonly DlgKind kind;
        readonly Rectangle msgRect;
        int result = -1, acceptIndex = -1;

        ModernDialog(string title, string message, DlgKind kind, string[] buttons, int primary)
        {
            this.title = title; this.message = message; this.kind = kind;
            Text = title;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            int W = Dpi.D(470), pad = Dpi.D(26), tx = Dpi.D(88);
            int mh = G.TextH(message, Theme.UI(10f), W - tx - pad);
            msgRect = new Rectangle(tx, Dpi.D(58), W - tx - pad, mh + Dpi.D(4));
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
                if (i == primary) acceptIndex = idx;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { result = -1; Close(); }
            else if (e.KeyCode == Keys.Enter && acceptIndex >= 0) { result = acceptIndex; DialogResult = DialogResult.OK; Close(); }
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

    class PromptDialog : ModernForm
    {
        readonly string title, hint;
        readonly InputBox box;
        string error = "";
        readonly Func<string, string> validate;
        public string Value;

        PromptDialog(string title, string hint, string value, Func<string, string> validate)
        {
            this.title = title; this.hint = hint; this.validate = validate;
            Text = title;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            int W = Dpi.D(470), x = Dpi.D(28), w = W - Dpi.D(56);
            ClientSize = new Size(W, Dpi.D(250));
            CaptionHeight = ClientSize.Height;
            box = new InputBox(Ico.Rename, "", false);
            box.SetBounds(x, Dpi.D(96), w, Dpi.D(48));
            box.Text = value;
            box.EnterPressed += delegate { Ok(); };
            Controls.Add(box);
            var cancel = new FlatButton("Abbrechen", null, BtnKind.Secondary);
            var ok = new FlatButton("Speichern", Ico.Check, BtnKind.Primary);
            ok.Width = cancel.Width = Dpi.D(130);
            ok.Location = new Point(W - x - ok.Width, Dpi.D(184));
            cancel.Location = new Point(ok.Left - Dpi.D(10) - cancel.Width, Dpi.D(184));
            ok.Click += delegate { Ok(); };
            cancel.Click += delegate { Close(); };
            Controls.Add(ok); Controls.Add(cancel);
            Shown += delegate
            {
                box.Box.Focus();
                string t = box.Text;
                int dot = t.LastIndexOf('.');
                int slash = t.LastIndexOf('/');
                int start = slash + 1;
                box.Box.Select(start, (dot > start ? dot : t.Length) - start);
            };
        }

        void Ok()
        {
            string v = box.Text.Trim();
            string err = validate(v);
            if (err != null) { error = err; Invalidate(); return; }
            Value = v;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) Close(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            var circle = new RectangleF(Dpi.D(28), Dpi.D(26), Dpi.D(44), Dpi.D(44));
            G.Gradient(g, circle, Dpi.F(14), Theme.Accent, Theme.Accent2, 45f);
            G.Glyph(g, Ico.Rename, 13f, Rectangle.Round(circle), Color.White);
            G.Text(g, title, Theme.Bold(13f), new Rectangle(Dpi.D(86), Dpi.D(24), Width - Dpi.D(110), Dpi.D(26)), Theme.Text, G.Left);
            G.Text(g, hint, Theme.UI(9f), new Rectangle(Dpi.D(86), Dpi.D(50), Width - Dpi.D(110), Dpi.D(22)), Theme.Muted, G.Left);
            if (error.Length > 0)
                G.Text(g, error, Theme.UI(9f), new Rectangle(Dpi.D(30), Dpi.D(150), Width - Dpi.D(60), Dpi.D(26)), Theme.Danger, G.Left);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        public static string Ask(IWin32Window owner, string title, string hint, string value, Func<string, string> validate)
        {
            using (var d = new PromptDialog(title, hint, value, validate))
                return d.ShowDialog(owner) == DialogResult.OK ? d.Value : null;
        }
    }

    // ------------------------------------------------------------------ login

    class LoginForm : ModernForm
    {
        readonly Vault vault;
        readonly bool create;
        readonly InputBox pw, pw2;
        readonly FlatButton go;
        readonly Rectangle errRect, capsRect;
        readonly int strengthY;
        string error = "";
        int fails, strength;
        bool caps, busy;
        DateTime lockedUntil = DateTime.MinValue;
        readonly Timer cooldown, shake, capsTimer;
        int shakeFrame, shakeBase;

        public LoginForm(Vault v)
        {
            vault = v;
            create = !v.Exists;
            Text = "un0yy Vault";
            StartPosition = FormStartPosition.CenterScreen;
            FadeIn = true;
            Opacity = 0;
            int W = Dpi.D(440);
            ClientSize = new Size(W, Dpi.D(create ? 660 : 540));
            CaptionHeight = ClientSize.Height;

            var close = CaptionButton(Ico.Close, true);
            close.Location = new Point(W - close.Width, 0);
            close.Click += delegate { if (!busy) { DialogResult = DialogResult.Cancel; Close(); } };
            var min = CaptionButton(Ico.Min, false);
            min.Location = new Point(W - close.Width * 2, 0);
            min.Click += delegate { WindowState = FormWindowState.Minimized; };

            int x = Dpi.D(44), w = W - Dpi.D(88), y = Dpi.D(262);
            pw = new InputBox(Ico.Key, create ? "Neues Master-Passwort" : "Master-Passwort", true);
            pw.SetBounds(x, y, w, Dpi.D(50));
            pw.EnterPressed += delegate { if (pw2 != null) pw2.FocusBox(); else Submit(); };
            Controls.Add(pw);
            y += Dpi.D(54);
            capsRect = new Rectangle(x, y, w, Dpi.D(20));
            y += Dpi.D(10);

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
            else y += Dpi.D(4);

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

            capsTimer = new Timer();
            capsTimer.Interval = 250;
            capsTimer.Tick += delegate
            {
                bool c = IsKeyLocked(Keys.CapsLock);
                if (c != caps) { caps = c; Invalidate(capsRect); }
            };
            capsTimer.Start();

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
            G.Glow(g, new RectangleF(-W * 0.3f, -Dpi.D(260), W * 1.6f, Dpi.D(560)), Theme.Accent, 64);
            G.Glow(g, new RectangleF(W * 0.35f, Height - Dpi.D(220), W, Dpi.D(380)), Theme.Accent2, 28);

            var logo = new RectangleF((W - Dpi.D(84)) / 2f, Dpi.D(62), Dpi.D(84), Dpi.D(84));
            G.Glow(g, RectangleF.Inflate(logo, Dpi.D(46), Dpi.D(46)), Theme.Accent, 95);
            Art.DrawLock(g, logo);

            G.Text(g, "un0yy Vault", Theme.Bold(22f), new Rectangle(0, Dpi.D(160), W, Dpi.D(44)), Theme.Text, G.Center);
            G.Text(g, create ? "Erstelle deinen privaten, verschlüsselten Tresor." : "Willkommen zurück – entsperre deinen Tresor.",
                   Theme.UI(10.5f), new Rectangle(0, Dpi.D(206), W, Dpi.D(26)), Theme.Muted, G.Center);

            int x = Dpi.D(44), w = W - Dpi.D(88);
            if (caps)
            {
                G.Glyph(g, Ico.Caps, 9f, new Rectangle(capsRect.X + Dpi.D(4), capsRect.Y, Dpi.D(16), capsRect.Height), Theme.Warn);
                G.Text(g, "Feststelltaste ist aktiv", Theme.UI(8.5f), new Rectangle(capsRect.X + Dpi.D(24), capsRect.Y, capsRect.Width, capsRect.Height), Theme.Warn, G.Left);
            }
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
            string foot = "v2.0   ·   AES-256   ·   Nur auf deinem PC   ·   Ohne Internet";
            int fw = G.TextW(foot, Theme.UI(8.5f)) + Dpi.D(22);
            int fx = (W - fw) / 2;
            G.Glyph(g, Ico.Shield, 9f, new Rectangle(fx, fy, Dpi.D(16), Dpi.D(20)), Theme.Success);
            G.Text(g, foot, Theme.UI(8.5f), new Rectangle(fx + Dpi.D(22), fy, fw, Dpi.D(20)), Theme.Faint, G.Left);
            PaintFrame(g);
        }

        async void Submit()
        {
            if (busy || DateTime.Now < lockedUntil || !go.Enabled) return;
            SetError("");
            string p1 = pw.Text;
            if (create)
            {
                if (p1.Length < 8) { SetError("Das Passwort muss mindestens 8 Zeichen lang sein."); Shake(); return; }
                if (p1 != pw2.Text) { SetError("Die Passwörter stimmen nicht überein."); Shake(); pw2.FocusBox(); return; }
            }
            busy = true;
            go.Enabled = false;
            go.Text = create ? "Tresor wird erstellt…" : "Wird entsperrt…";
            Cursor = Cursors.WaitCursor;
            bool ok = false;
            string problem = null;
            try
            {
                if (create) { await Task.Run(() => vault.Create(p1)); ok = true; }
                else
                {
                    ok = await Task.Run(() => vault.Unlock(p1));
                    if (ok && vault.NeedsKdfUpgrade)
                    {
                        go.Text = "Sicherheit wird verbessert…";
                        try { await Task.Run(() => vault.ChangePassword(p1, p1)); } catch { }
                    }
                }
            }
            catch (Exception ex) { problem = ex.Message; }
            Cursor = Cursors.Default;
            busy = false;
            go.Enabled = true;
            go.Text = create ? "Tresor erstellen" : "Entsperren";

            if (ok) { DialogResult = DialogResult.OK; Close(); return; }
            if (problem != null) { SetError((create ? "Fehler: " : "Tresor konnte nicht geöffnet werden: ") + problem); return; }

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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy) { e.Cancel = true; return; }
            base.OnFormClosing(e);
            capsTimer.Stop();
        }
    }

    // ------------------------------------------------------------------ change password

    class ChangePasswordForm : ModernForm
    {
        readonly Vault vault;
        readonly InputBox oldPw, newPw, newPw2;
        readonly FlatButton save;
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
            save = new FlatButton("Speichern", Ico.Check, BtnKind.Primary);
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

        async void Save()
        {
            if (!save.Enabled) return;
            error = "";
            if (newPw.Text.Length < 8) error = "Das neue Passwort braucht mindestens 8 Zeichen.";
            else if (newPw.Text != newPw2.Text) error = "Die neuen Passwörter stimmen nicht überein.";
            if (error.Length > 0) { Invalidate(); return; }
            string o = oldPw.Text, n = newPw.Text;
            save.Enabled = false; save.Text = "Speichere…";
            Cursor = Cursors.WaitCursor;
            bool ok = false;
            try { ok = await Task.Run(() => vault.ChangePassword(o, n)); }
            catch (Exception ex) { error = "Fehler: " + ex.Message; }
            Cursor = Cursors.Default;
            save.Enabled = true; save.Text = "Speichern";
            if (error.Length > 0) { Invalidate(); return; }
            if (!ok) { error = "Das aktuelle Passwort ist falsch."; Invalidate(); oldPw.FocusBox(); return; }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ------------------------------------------------------------------ settings

    class SettingsForm : ModernForm
    {
        readonly AppSettings settings;
        readonly Vault vault;
        readonly List<Tuple<Rectangle, string, string>> rows = new List<Tuple<Rectangle, string, string>>();
        readonly List<Tuple<Point, string>> sections = new List<Tuple<Point, string>>();
        readonly List<Swatch> swatches = new List<Swatch>();
        readonly Rectangle hotkeyRect, infoRect;
        public event Action Changed, ChangePasswordRequested, BackupRequested, OpenFolderRequested;
        static readonly int[] LockValues = { 1, 5, 10, 30, 0 };

        public SettingsForm(AppSettings s, Vault v)
        {
            settings = s; vault = v;
            Text = "Einstellungen";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Panel;
            int W = Dpi.D(880), pad = Dpi.D(32), gap = Dpi.D(40);
            int colW = (W - pad * 2 - gap) / 2;
            int c1 = pad, c2 = pad + colW + gap;

            var close = CaptionButton(Ico.Close, true);
            close.Location = new Point(W - close.Width, 0);
            close.Click += delegate { Close(); };

            // ---- left column: security
            int y = Dpi.D(104);
            sections.Add(Tuple.Create(new Point(c1, y), "SICHERHEIT"));
            y += Dpi.D(30);
            rows.Add(Tuple.Create(new Rectangle(c1, y, colW, Dpi.D(22)), "Automatisch sperren nach", ""));
            y += Dpi.D(28);
            var seg = new Segmented(new[] { "1 Min", "5 Min", "10 Min", "30 Min", "Nie" });
            seg.SetBounds(c1, y, colW, Dpi.D(36));
            seg.SelectedIndex = Math.Max(0, Array.IndexOf(LockValues, s.AutoLock));
            seg.SelectedChanged += delegate { settings.AutoLock = LockValues[seg.SelectedIndex]; Fire(); };
            Controls.Add(seg);
            y += Dpi.D(36) + Dpi.D(18);

            y = ToggleRow(c1, colW, y, "Beim Minimieren sperren", "Sperrt sofort, wenn du das Fenster minimierst.",
                          s.LockOnMinimize, delegate(bool b) { settings.LockOnMinimize = b; });
            y = ToggleRow(c1, colW, y, "Originale sicher überschreiben", "Überschreibt Dateien vor dem Löschen mit Nullen.",
                          s.SecureDelete, delegate(bool b) { settings.SecureDelete = b; });
            y = ButtonRow(c1, colW, y, "Master-Passwort", "Jederzeit änderbar – Dateien bleiben erhalten.",
                          new FlatButton("Ändern", Ico.Key, BtnKind.Secondary), delegate { if (ChangePasswordRequested != null) ChangePasswordRequested(); });
            hotkeyRect = new Rectangle(c1, y + Dpi.D(4), colW, Dpi.D(58));
            int leftBottom = hotkeyRect.Bottom;

            // ---- right column: appearance + vault
            y = Dpi.D(104);
            sections.Add(Tuple.Create(new Point(c2, y), "AUSSEHEN"));
            y += Dpi.D(30);
            rows.Add(Tuple.Create(new Rectangle(c2, y, colW, Dpi.D(22)), "Akzentfarbe", ""));
            y += Dpi.D(28);
            for (int i = 0; i < Theme.Palettes.GetLength(0); i++)
            {
                var sw = new Swatch(i);
                sw.Location = new Point(c2 + i * Dpi.D(52) - Dpi.D(4), y);
                sw.Selected = i == Theme.AccentIndex;
                int idx = i;
                sw.Click += delegate
                {
                    settings.Accent = idx;
                    Theme.SetAccent(idx);
                    foreach (var x in swatches) x.Selected = x.Index == idx;
                    RepaintAll(this);
                    Fire();
                };
                swatches.Add(sw);
                Controls.Add(sw);
            }
            y += Dpi.D(44) + Dpi.D(26);

            sections.Add(Tuple.Create(new Point(c2, y), "TRESOR"));
            y += Dpi.D(30);
            y = ButtonRow(c2, colW, y, "Backup erstellen", "Verschlüsselte ZIP-Kopie deines Tresors.",
                          new FlatButton("Backup", Ico.Save, BtnKind.Secondary), delegate { if (BackupRequested != null) BackupRequested(); });
            y = ButtonRow(c2, colW, y, "Speicherort", v.Dir,
                          new FlatButton("Öffnen", Ico.FolderOpen, BtnKind.Ghost), delegate { if (OpenFolderRequested != null) OpenFolderRequested(); });
            infoRect = new Rectangle(c2, y + Dpi.D(4), colW, Dpi.D(58));
            int bottom = Math.Max(leftBottom, infoRect.Bottom) + Dpi.D(26);

            var done = new FlatButton("Fertig", Ico.Check, BtnKind.Primary);
            done.Width = Dpi.D(140);
            done.Location = new Point(W - pad - done.Width, bottom);
            done.Click += delegate { Close(); };
            Controls.Add(done);
            ClientSize = new Size(W, bottom + Dpi.D(40) + Dpi.D(26));
            CaptionHeight = Dpi.D(90);
        }

        void Fire() { settings.Save(); if (Changed != null) Changed(); }

        int ToggleRow(int x, int w, int y, string label, string sub, bool value, Action<bool> set)
        {
            rows.Add(Tuple.Create(new Rectangle(x, y, w - Dpi.D(70), Dpi.D(44)), label, sub));
            var t = new ToggleSwitch();
            t.SetSilently(value);
            t.Location = new Point(x + w - t.Width, y + Dpi.D(9));
            t.CheckedChanged += delegate { set(t.Checked); Fire(); };
            Controls.Add(t);
            return y + Dpi.D(58);
        }

        int ButtonRow(int x, int w, int y, string label, string sub, FlatButton b, EventHandler click)
        {
            b.Height = Dpi.D(38);
            b.Width = b.PreferredW();
            rows.Add(Tuple.Create(new Rectangle(x, y, w - b.Width - Dpi.D(16), Dpi.D(44)), label, sub));
            b.Location = new Point(x + w - b.Width, y + Dpi.D(3));
            b.Click += click;
            Controls.Add(b);
            return y + Dpi.D(58);
        }

        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Escape) Close(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            G.HQ(g);
            G.Glow(g, new RectangleF(-Dpi.D(160), -Dpi.D(220), Dpi.D(620), Dpi.D(400)), Theme.Accent, 40);
            var circle = new RectangleF(Dpi.D(32), Dpi.D(28), Dpi.D(48), Dpi.D(48));
            G.Gradient(g, circle, Dpi.F(15), Theme.Accent, Theme.Accent2, 45f);
            G.Glyph(g, Ico.Settings, 15f, Rectangle.Round(circle), Color.White);
            G.Text(g, "Einstellungen", Theme.Bold(16f), new Rectangle(Dpi.D(94), Dpi.D(24), Dpi.D(400), Dpi.D(32)), Theme.Text, G.Left);
            G.Text(g, "un0yy Vault 2.0  ·  Änderungen werden sofort gespeichert", Theme.UI(9.5f),
                   new Rectangle(Dpi.D(94), Dpi.D(56), Dpi.D(500), Dpi.D(22)), Theme.Muted, G.Left);

            foreach (var s in sections)
            {
                G.Text(g, s.Item2, Theme.Semi(8.5f), new Rectangle(s.Item1.X, s.Item1.Y, Dpi.D(300), Dpi.D(20)), Theme.AccentSoft, G.Left);
                using (var p = new Pen(Theme.Border))
                {
                    int lx = s.Item1.X + G.TextW(s.Item2, Theme.Semi(8.5f)) + Dpi.D(8);
                    g.DrawLine(p, lx, s.Item1.Y + Dpi.D(10), s.Item1.X + (Width - Dpi.D(64) - Dpi.D(40)) / 2, s.Item1.Y + Dpi.D(10));
                }
            }
            foreach (var r in rows)
            {
                if (r.Item3.Length == 0)
                    G.Text(g, r.Item2, Theme.Semi(10.5f), r.Item1, Theme.Text, G.Left);
                else
                {
                    G.Text(g, r.Item2, Theme.Semi(10.5f), new Rectangle(r.Item1.X, r.Item1.Y, r.Item1.Width, Dpi.D(22)), Theme.Text, G.Left);
                    G.Text(g, r.Item3, Theme.UI(8.5f), new Rectangle(r.Item1.X, r.Item1.Y + Dpi.D(22), r.Item1.Width, Dpi.D(20)), Theme.Muted,
                           G.Left | TextFormatFlags.PathEllipsis);
                }
            }

            InfoBox(g, hotkeyRect, Ico.Keyboard, "Notfall-Sperre:  Strg + Umschalt + L",
                    "Sperrt sofort – egal welches Programm aktiv ist.", Theme.AccentSoft);
            InfoBox(g, infoRect, Ico.Shield, "AES-256  ·  HMAC-SHA256  ·  PBKDF2",
                    vault.KeyIterations.ToString("N0") + " Runden pro Passwortprüfung  ·  " + Fmt.Files(vault.Entries.Count), Theme.Success);
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        static void InfoBox(Graphics g, Rectangle r, string glyph, string title, string text, Color c)
        {
            G.Fill(g, r, Dpi.F(12), Theme.A(c, 18));
            G.Stroke(g, r, Dpi.F(12), Theme.A(c, 60), 1f);
            G.Glyph(g, glyph, 12f, new Rectangle(r.X + Dpi.D(10), r.Y, Dpi.D(30), r.Height), c);
            G.Text(g, title, Theme.Semi(9.5f), new Rectangle(r.X + Dpi.D(48), r.Y + Dpi.D(9), r.Width - Dpi.D(56), Dpi.D(20)), Theme.Text, G.Left);
            G.Text(g, text, Theme.UI(8.5f), new Rectangle(r.X + Dpi.D(48), r.Y + Dpi.D(30), r.Width - Dpi.D(56), Dpi.D(20)), Theme.Muted, G.Left);
        }
    }
}
