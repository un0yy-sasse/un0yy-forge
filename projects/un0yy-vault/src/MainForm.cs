// un0yy Vault - main window
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Un0yyVault
{
    class SidebarCard : Card
    {
        public string Files = "", SizeText = "";
        public int SecondLabelY;
        public bool ShowStorage = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            G.Text(g, "BIBLIOTHEK", Theme.Semi(8f), new Rectangle(Dpi.D(22), Dpi.D(12), Width, Dpi.D(24)), Theme.Faint, G.Left);
            G.Text(g, "KATEGORIEN", Theme.Semi(8f), new Rectangle(Dpi.D(22), SecondLabelY, Width, Dpi.D(24)), Theme.Faint, G.Left);
            if (!ShowStorage) return;
            var box = new RectangleF(Dpi.D(12), Height - Dpi.D(204), Width - Dpi.D(24), Dpi.D(94));
            G.Gradient(g, box, Dpi.F(14), Theme.A(Theme.Success, 30), Theme.A(Theme.Accent2, 14), 30f);
            G.Stroke(g, box, Dpi.F(14), Theme.A(Theme.Success, 60), 1f);
            var c = new RectangleF(box.X + Dpi.D(14), box.Y + Dpi.D(14), Dpi.D(34), Dpi.D(34));
            G.Fill(g, c, Dpi.F(11), Theme.A(Theme.Success, 50));
            G.Glyph(g, Ico.Shield, 11f, Rectangle.Round(c), Theme.Success);
            G.Text(g, "Geschützt", Theme.Semi(10.5f), new Rectangle((int)c.Right + Dpi.D(10), (int)c.Y - Dpi.D(2), Width, Dpi.D(20)), Theme.Text, G.Left);
            G.Text(g, "AES-256 verschlüsselt", Theme.UI(8.5f), new Rectangle((int)c.Right + Dpi.D(10), (int)c.Y + Dpi.D(17), Width, Dpi.D(18)), Theme.Muted, G.Left);
            G.Text(g, Files + "   ·   " + SizeText, Theme.UI(9f), new Rectangle((int)box.X + Dpi.D(14), (int)box.Y + Dpi.D(58), (int)box.Width - Dpi.D(20), Dpi.D(24)), Theme.Text, G.Left);
        }
    }

    class ContentCard : Card
    {
        public string Title = "", Subtitle = "", Hint = "", Selection = "";
        public int TitleWidth;
        public bool HideHint;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            int tw = Math.Max(Dpi.D(120), TitleWidth > 0 ? TitleWidth : Width - Dpi.D(560));
            G.Text(g, Title, Theme.Bold(18f), new Rectangle(Dpi.D(28), Dpi.D(20), tw, Dpi.D(38)), Theme.Text, G.Left);
            G.Text(g, Subtitle, Theme.UI(9.5f), new Rectangle(Dpi.D(29), Dpi.D(56), tw, Dpi.D(22)), Theme.Muted, G.Left);
            var row = new Rectangle(Dpi.D(28), Dpi.D(92), Width - Dpi.D(56), Dpi.D(40));
            if (Selection.Length > 0)
            {
                var pill = new RectangleF(row.X, row.Y + Dpi.D(4), G.TextW(Selection, Theme.Semi(9.5f)) + Dpi.D(40), Dpi.D(32));
                G.Fill(g, pill, Dpi.F(16), Theme.A(Theme.Accent, 60));
                G.Glyph(g, Ico.Check, 9f, new Rectangle((int)pill.X + Dpi.D(8), (int)pill.Y, Dpi.D(20), (int)pill.Height), Theme.AccentSoft);
                G.Text(g, Selection, Theme.Semi(9.5f), new Rectangle((int)pill.X + Dpi.D(28), (int)pill.Y, (int)pill.Width, (int)pill.Height), Theme.Text, G.Left);
            }
            else if (Hint.Length > 0 && !HideHint)
            {
                G.Glyph(g, Ico.Info, 9.5f, new Rectangle(row.X, row.Y, Dpi.D(18), row.Height), Theme.Faint);
                G.Text(g, Hint, Theme.UI(9f), new Rectangle(row.X + Dpi.D(26), row.Y, row.Width, row.Height), Theme.Faint, G.Left);
            }
        }

        public int SelectionPillRight() { return Dpi.D(28) + G.TextW(Selection, Theme.Semi(9.5f)) + Dpi.D(40); }
    }

    class MainForm : ModernForm, IMessageFilter
    {
        const int HotkeyId = 0xB00B;
        static readonly int[] NavCats = { 0, 7, 8, 1, 2, 3, 4, 5, 6 };

        readonly Vault vault;
        readonly AppSettings settings;
        public bool LockRequested;
        readonly SidebarCard side;
        readonly ContentCard content;
        readonly PreviewPanel preview;
        readonly NavItem[] nav = new NavItem[9];
        readonly FileList list;
        readonly InputBox search;
        readonly FlatButton bAdd, bOpen, bExport, bDel, bSettings, bLock, bSort;
        readonly IconButton cClose, cMax, cMin, vList, vGrid;
        readonly Toast toast;
        readonly ProgressOverlay progress;
        readonly ContextMenuStrip menu, sortMenu;
        readonly ThumbCache thumbs;
        int category;
        bool busy, compactHeader;
        string tempDir;
        DateTime lastActivity = DateTime.Now;
        readonly Timer idle, syncTimer;
        FileSystemWatcher watcher;
        readonly Dictionary<string, Watched> watched = new Dictionary<string, Watched>(StringComparer.OrdinalIgnoreCase);

        class Watched { public VaultEntry Entry; public string Path; public DateTime Stamp; public long Length; }

        public MainForm(Vault v, AppSettings s)
        {
            vault = v;
            settings = s;
            Text = "un0yy Vault";
            Resizable = true;
            FadeIn = true;
            Opacity = 0;
            StartPosition = FormStartPosition.CenterScreen;
            var wa = Screen.PrimaryScreen.WorkingArea;
            int w0 = s.Width > 0 ? s.Width : Dpi.D(1280), h0 = s.Height > 0 ? s.Height : Dpi.D(780);
            ClientSize = new Size(Math.Min(w0, wa.Width - Dpi.D(20)), Math.Min(h0, wa.Height - Dpi.D(20)));
            MinimumSize = new Size(Math.Min(Dpi.D(1000), wa.Width), Math.Min(Dpi.D(600), wa.Height));
            if (s.Maximized) WindowState = FormWindowState.Maximized;
            CaptionHeight = Dpi.D(50);
            AllowDrop = true;

            cClose = CaptionButton(Ico.Close, true);
            cMax = CaptionButton(Ico.Max, false);
            cMin = CaptionButton(Ico.Min, false);
            cClose.Click += delegate { Close(); };
            cMax.Click += delegate { ToggleMax(); };
            cMin.Click += delegate { WindowState = FormWindowState.Minimized; };

            thumbs = new ThumbCache(vault, this);
            thumbs.Updated += delegate { list.Invalidate(); };

            // ---- sidebar
            side = new SidebarCard();
            Controls.Add(side);
            string[] navNames = { "Alle Dateien", "Favoriten", "Zuletzt hinzugefügt" };
            string[] navGlyphs = { Ico.Folder, Ico.StarFill, Ico.Clock };
            for (int i = 0; i < nav.Length; i++)
            {
                int cat = NavCats[i];
                NavItem n;
                if (i == 0) n = new NavItem(navNames[0], navGlyphs[0], () => Theme.AccentSoft);
                else if (i == 1) n = new NavItem(navNames[1], navGlyphs[1], () => Theme.Gold);
                else if (i == 2) n = new NavItem(navNames[2], navGlyphs[2], () => Theme.Accent2);
                else n = new NavItem(Kinds.Names[cat], Kinds.Glyphs[cat], () => Kinds.Color(cat));
                n.Click += delegate { SelectCategory(cat); };
                nav[i] = n;
                side.Controls.Add(n);
            }
            nav[0].Selected = true;
            bSettings = new FlatButton("Einstellungen", Ico.Settings, BtnKind.Ghost);
            bLock = new FlatButton("Tresor sperren", Ico.Lock, BtnKind.DangerGhost);
            bSettings.Click += delegate { ShowSettings(); };
            bLock.Click += delegate { DoLock(); };
            side.Controls.Add(bSettings);
            side.Controls.Add(bLock);

            // ---- content
            content = new ContentCard();
            content.Hint = "Doppelklick öffnet   ·   F2 umbenennen   ·   Strg+V einfügen   ·   Rechtsklick für mehr";
            Controls.Add(content);
            search = new InputBox(Ico.Search, "Suchen…  (Strg+F)", false);
            search.TextChanged += delegate { RefreshList(); };
            bAdd = new FlatButton("Hinzufügen", Ico.Add, BtnKind.Primary);
            bAdd.Click += delegate { AddViaDialog(); };
            bSort = new FlatButton("Name", Ico.Sort, BtnKind.Ghost);
            bSort.Height = Dpi.D(40);
            bSort.Click += delegate { ShowSortMenu(); };
            vList = new IconButton(Ico.List); vList.Size = new Size(Dpi.D(40), Dpi.D(40)); vList.GlyphSize = 11f;
            vGrid = new IconButton(Ico.Grid); vGrid.Size = new Size(Dpi.D(40), Dpi.D(40)); vGrid.GlyphSize = 11f;
            vList.Click += delegate { SetGrid(false); };
            vGrid.Click += delegate { SetGrid(true); };
            bOpen = new FlatButton("Öffnen", Ico.Open, BtnKind.Secondary);
            bExport = new FlatButton("Exportieren", Ico.Export, BtnKind.Secondary);
            bDel = new FlatButton("Löschen", Ico.Delete, BtnKind.DangerGhost);
            foreach (var b in new[] { bOpen, bExport, bDel }) b.Height = Dpi.D(36);
            bOpen.Click += delegate { OpenSelected(); };
            bExport.Click += delegate { ExportSelected(); };
            bDel.Click += delegate { DeleteSelected(); };

            list = new FileList();
            list.AllowDrop = true;
            list.ThumbFor = en => thumbs.Get(en);
            list.IsFav = en => vault.IsFavorite(en);
            list.SortCol = s.SortCol; list.SortAsc = s.SortAsc;
            list.Grid = s.Grid;
            list.SelectionChanged += delegate { UpdateSelectionUi(); };
            list.OpenRequested += delegate { OpenSelected(); };
            list.DeleteRequested += delegate { DeleteSelected(); };
            list.RenameRequested += delegate { RenameSelected(); };
            list.AddRequested += delegate { AddViaDialog(); };
            list.SortChanged += delegate { SaveSort(); RefreshList(); };
            list.ContextRequested += delegate(object o, MouseEventArgs e) { ShowContextMenu(e.Location); };

            toast = new Toast();
            progress = new ProgressOverlay();
            content.Controls.AddRange(new Control[] { search, bSort, vList, vGrid, bAdd, bOpen, bExport, bDel, list, progress, toast });

            preview = new PreviewPanel(vault);
            preview.Visible = false;
            preview.ActionRequested += OnPreviewAction;
            Controls.Add(preview);

            menu = Menus.Create();
            sortMenu = Menus.Create();

            foreach (Control c in new Control[] { this, content, list })
            {
                c.AllowDrop = true;
                c.DragEnter += OnDragEnter;
                c.DragOver += OnDragEnter;
                c.DragLeave += delegate { list.DropActive = false; };
                c.DragDrop += OnDragDrop;
            }

            idle = new Timer();
            idle.Interval = 10000;
            idle.Tick += delegate { CheckIdle(); };
            idle.Start();
            syncTimer = new Timer();
            syncTimer.Interval = 1500;
            syncTimer.Tick += delegate { syncTimer.Stop(); SyncEdits(false); };
            Application.AddMessageFilter(this);

            vList.Active = !list.Grid; vGrid.Active = list.Grid;
            UpdateSortButton();
            LayoutAll();
            RefreshList();
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    if (settings.LockOnMinimize && !busy) BeginInvoke((MethodInvoker)DoLock);
                    return;
                }
                LayoutAll();
            };
            Shown += delegate
            {
                list.Focus();
                if (vault.Entries.Count == 0) toast.Show("Willkommen! Zieh deine ersten Dateien ins Fenster.", ToastKind.Info);
            };
        }

        // ================================================================ layout

        void LayoutAll()
        {
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W < 10 || H < 10) return;
            int m = Dpi.D(10), top = Dpi.D(50);
            cClose.Location = new Point(W - cClose.Width, 0);
            cMax.Location = new Point(W - cClose.Width * 2, 0);
            cMin.Location = new Point(W - cClose.Width * 3, 0);
            cMax.Glyph = WindowState == FormWindowState.Maximized ? Ico.Restore : Ico.Max;
            cMax.Invalidate();

            int sw = Dpi.D(250);
            side.SetBounds(m, top, sw, H - top - m);
            int nx = Dpi.D(10), nw = sw - Dpi.D(20), y = Dpi.D(38);
            for (int i = 0; i < nav.Length; i++)
            {
                if (i == 3) { side.SecondLabelY = y + Dpi.D(8); y += Dpi.D(34); }
                nav[i].SetBounds(nx, y, nw, Dpi.D(38));
                y += Dpi.D(40);
            }
            bSettings.SetBounds(Dpi.D(12), side.Height - Dpi.D(102), sw - Dpi.D(24), Dpi.D(40));
            bLock.SetBounds(Dpi.D(12), side.Height - Dpi.D(56), sw - Dpi.D(24), Dpi.D(42));
            side.ShowStorage = side.Height - Dpi.D(212) > y + Dpi.D(4);

            bool showPreview = preview.Visible;
            int pw = showPreview ? Dpi.D(330) : 0;
            int cx = m + sw + m;
            int cw = W - cx - m - (showPreview ? pw + m : 0);
            content.SetBounds(cx, top, cw, H - top - m);
            if (showPreview) preview.SetBounds(cx + cw + m, top, pw, H - top - m);

            int ch = content.Height;
            int ax = cw - Dpi.D(24) - bAdd.PreferredW();
            bAdd.SetBounds(ax, Dpi.D(24), bAdd.PreferredW(), Dpi.D(44));
            vGrid.Location = new Point(ax - Dpi.D(12) - vGrid.Width, Dpi.D(26));
            vList.Location = new Point(vGrid.Left - Dpi.D(4) - vList.Width, Dpi.D(26));
            bSort.Width = bSort.PreferredW();
            bSort.Location = new Point(vList.Left - Dpi.D(8) - bSort.Width, Dpi.D(26));
            // keep at least ~240px for the title; otherwise the search box moves into the second row
            int room = bSort.Left - Dpi.D(10) - Dpi.D(28) - Dpi.D(240);
            compactHeader = room < Dpi.D(180);
            if (!compactHeader)
            {
                int sWidth = Math.Min(Dpi.D(300), room);
                search.SetBounds(bSort.Left - Dpi.D(10) - sWidth, Dpi.D(23), sWidth, Dpi.D(46));
                content.TitleWidth = search.Left - Dpi.D(40);
            }
            else
            {
                int sWidth = Math.Min(Dpi.D(280), cw - Dpi.D(56) - Dpi.D(170));
                search.SetBounds(cw - Dpi.D(24) - sWidth, Dpi.D(88), sWidth, Dpi.D(44));
                content.TitleWidth = bSort.Left - Dpi.D(40);
            }
            list.SetBounds(Dpi.D(14), Dpi.D(140), cw - Dpi.D(28), ch - Dpi.D(154));
            content.HideHint = compactHeader;
            PlaceActions();
            toast.Reposition();
            progress.Center();
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
            var tf = Theme.Semi(11f);
            G.Text(g, "un0yy Vault", tf, new Rectangle(Dpi.D(56), Dpi.D(6), Dpi.D(200), Dpi.D(40)), Theme.Text, G.Left);
            int tx = Dpi.D(56) + G.TextW("un0yy Vault", tf) + Dpi.D(6);
            var vf = Theme.Semi(7.5f);
            var vp = new RectangleF(tx, Dpi.D(16), G.TextW("2.0", vf) + Dpi.D(12), Dpi.D(18));
            G.Gradient(g, vp, Dpi.F(9), Theme.Accent, Theme.Accent2, 0f);
            G.Text(g, "2.0", vf, Rectangle.Round(vp), Color.White, G.Center);
            tx = (int)vp.Right + Dpi.D(12);

            string st = "Entsperrt";
            var pill = new RectangleF(tx, Dpi.D(14), G.TextW(st, Theme.Semi(8.5f)) + Dpi.D(30), Dpi.D(24));
            G.Fill(g, pill, Dpi.F(12), Theme.A(Theme.Success, 34));
            using (var b = new SolidBrush(Theme.Success)) g.FillEllipse(b, pill.X + Dpi.D(10), pill.Y + pill.Height / 2 - Dpi.D(3), Dpi.D(6), Dpi.D(6));
            G.Text(g, st, Theme.Semi(8.5f), new Rectangle((int)pill.X + Dpi.D(21), (int)pill.Y, (int)pill.Width, (int)pill.Height), Theme.Success, G.Left);
            string info = (settings.AutoLock > 0 ? "Auto-Sperre nach " + settings.AutoLock + " Min." : "Auto-Sperre aus") +
                          "   ·   Notfall-Sperre: Strg+Umschalt+L";
            G.Text(g, info, Theme.UI(8.5f), new Rectangle((int)pill.Right + Dpi.D(14), Dpi.D(6), Math.Max(10, cMin.Left - (int)pill.Right - Dpi.D(24)), Dpi.D(40)), Theme.Faint, G.Left);
            PaintFrame(g);
        }

        // ================================================================ data -> ui

        void SelectCategory(int cat)
        {
            category = cat;
            for (int j = 0; j < nav.Length; j++) nav[j].Selected = NavCats[j] == cat;
            list.ClearSelection();
            RefreshList();
        }

        IEnumerable<VaultEntry> Filter(IEnumerable<VaultEntry> src, int cat)
        {
            if (cat == 7) return src.Where(x => vault.IsFavorite(x));
            if (cat == 8) { var since = DateTime.UtcNow.AddDays(-7); return src.Where(x => x.Added >= since); }
            if (cat > 0) return src.Where(x => Kinds.Of(x.Name) == cat);
            return src;
        }

        static string CatName(int cat) { return cat == 7 ? "Favoriten" : cat == 8 ? "Zuletzt hinzugefügt" : Kinds.Names[cat]; }

        void RefreshList()
        {
            string q = search.Text.Trim().ToLowerInvariant();
            var src = Filter(vault.Entries, category);
            if (q.Length > 0) src = src.Where(x => x.Name.ToLowerInvariant().Contains(q));
            IOrderedEnumerable<VaultEntry> sorted;
            if (list.SortCol == 1) sorted = list.SortAsc ? src.OrderBy(x => x.Size) : src.OrderByDescending(x => x.Size);
            else if (list.SortCol == 2) sorted = list.SortAsc ? src.OrderBy(x => x.Added) : src.OrderByDescending(x => x.Added);
            else sorted = list.SortAsc ? src.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase) : src.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var shown = sorted.ToList();
            list.VaultEmpty = vault.Entries.Count == 0;
            if (q.Length > 0) { list.EmptyTitle = "Nichts gefunden"; list.EmptyText = "Keine Datei passt zu „" + search.Text.Trim() + "“."; }
            else if (category == 7) { list.EmptyTitle = "Noch keine Favoriten"; list.EmptyText = "Markiere wichtige Dateien mit dem Stern – sie erscheinen dann hier."; }
            else if (category == 8) { list.EmptyTitle = "Nichts Neues"; list.EmptyText = "In den letzten 7 Tagen wurden keine Dateien hinzugefügt."; }
            else { list.EmptyTitle = "Hier ist noch nichts"; list.EmptyText = "In dieser Kategorie liegen noch keine Dateien."; }
            list.SetItems(shown);

            for (int i = 0; i < nav.Length; i++)
            {
                int n = Filter(vault.Entries, NavCats[i]).Count();
                nav[i].Count = n > 0 ? n.ToString() : "";
                nav[i].Invalidate();
            }

            content.Title = CatName(category);
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
            var sel = list.Selected;
            int n = sel.Count;
            content.Selection = n > 0 ? n + " ausgewählt" : "";
            content.HideHint = compactHeader;
            bOpen.Visible = bExport.Visible = bDel.Visible = n > 1;   // single file: the preview panel has these buttons
            PlaceActions();
            content.Invalidate(new Rectangle(0, Dpi.D(88), content.Width, Dpi.D(48)));

            bool show = n == 1;
            if (show) preview.Refresh(sel[0]); else preview.Clear();
            if (show != preview.Visible) { preview.Visible = show; LayoutAll(); }
        }

        void SetGrid(bool g)
        {
            list.Grid = g;
            vList.Active = !g; vGrid.Active = g;
            settings.Grid = g; settings.Save();
        }

        void SaveSort() { settings.SortCol = list.SortCol; settings.SortAsc = list.SortAsc; settings.Save(); UpdateSortButton(); }

        void UpdateSortButton()
        {
            string[] names = { "Name", "Größe", "Datum" };
            bSort.Text = names[list.SortCol] + (list.SortAsc ? "  ↑" : "  ↓");
            bSort.Width = bSort.PreferredW();
        }

        void ShowSortMenu()
        {
            sortMenu.Items.Clear();
            string[] names = { "Name", "Größe", "Hinzugefügt" };
            for (int i = 0; i < 3; i++)
            {
                int col = i;
                var it = Menus.Item(names[i], delegate { list.SortCol = col; SaveSort(); RefreshList(); LayoutAll(); }, null);
                it.Checked = list.SortCol == i;
                sortMenu.Items.Add(it);
            }
            sortMenu.Items.Add(new ToolStripSeparator());
            var asc = Menus.Item("Aufsteigend", delegate { list.SortAsc = true; SaveSort(); RefreshList(); LayoutAll(); }, null);
            var desc = Menus.Item("Absteigend", delegate { list.SortAsc = false; SaveSort(); RefreshList(); LayoutAll(); }, null);
            asc.Checked = list.SortAsc; desc.Checked = !list.SortAsc;
            sortMenu.Items.Add(asc); sortMenu.Items.Add(desc);
            sortMenu.Show(bSort, new Point(0, bSort.Height + Dpi.D(4)));
        }

        void ShowContextMenu(Point at)
        {
            var sel = list.Selected;
            if (sel.Count == 0) return;
            menu.Items.Clear();
            bool one = sel.Count == 1;
            bool img = one && Kinds.IsImage(sel[0].Name);
            if (img) menu.Items.Add(Menus.Item("Ansehen", delegate { OpenSelected(); }, null));
            menu.Items.Add(Menus.Item(img ? "Mit Standard-App öffnen" : "Öffnen & bearbeiten", delegate { foreach (var en in list.Selected.Take(10)) OpenExternal(en); }, null));
            menu.Items.Add(Menus.Item("Entschlüsselt speichern unter…", delegate { ExportSelected(); }, null));
            if (one) menu.Items.Add(Menus.Item("Umbenennen\tF2", delegate { RenameSelected(); }, null));
            bool allFav = sel.All(x => vault.IsFavorite(x));
            menu.Items.Add(Menus.Item(allFav ? "Aus Favoriten entfernen" : "Zu Favoriten hinzufügen", delegate { ToggleFavorite(sel, !allFav); }, null));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item("Aus dem Tresor löschen\tEntf", delegate { DeleteSelected(); }, "danger"));
            menu.Show(list, at);
        }

        void OnPreviewAction(string a)
        {
            var en = preview.Entry;
            if (en == null) return;
            if (a == "open") OpenSelected();
            else if (a == "export") ExportSelected();
            else if (a == "rename") RenameSelected();
            else if (a == "fav") ToggleFavorite(new List<VaultEntry> { en }, !vault.IsFavorite(en));
            else if (a == "delete") DeleteSelected();
        }

        // ================================================================ helpers

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

        void SetBusy(bool b)
        {
            busy = b;
            side.Enabled = !b;
            foreach (Control c in new Control[] { bAdd, bSort, vList, vGrid, search, bOpen, bExport, bDel, list, preview }) c.Enabled = !b;
        }

        // ================================================================ add

        void AddViaDialog()
        {
            if (busy) return;
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
            bool files = !busy && e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effect = files ? DragDropEffects.Copy : DragDropEffects.None;
            list.DropActive = files;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            list.DropActive = false;
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || busy) return;
            Activate();
            AddPaths(paths);
        }

        void AddPaths(IEnumerable<string> paths)
        {
            var items = new List<KeyValuePair<string, string>>();
            var folders = new List<string>();
            foreach (string p in paths)
            {
                if (File.Exists(p)) items.Add(new KeyValuePair<string, string>(p, Path.GetFileName(p)));
                else if (Directory.Exists(p))
                {
                    string root = p.TrimEnd('\\', '/');
                    if (string.Equals(Path.GetFullPath(root), Path.GetFullPath(vault.Dir), StringComparison.OrdinalIgnoreCase)) continue;
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
            AddItems(items, folders);
        }

        async void AddItems(List<KeyValuePair<string, string>> items, List<string> folders)
        {
            if (items.Count == 0 || busy) return;
            SetBusy(true);
            toast.HideNow();
            progress.Begin(items.Count == 1 ? "Verschlüssele 1 Datei" : "Verschlüssele " + items.Count + " Dateien");
            var sizes = items.Select(it => { try { return new FileInfo(it.Key).Length; } catch { return 0L; } }).ToList();
            long total = Math.Max(1, sizes.Sum());
            long doneBytes = 0;
            var done = new List<string>();
            var errors = new List<string>();
            VaultEntry last = null;

            for (int i = 0; i < items.Count; i++)
            {
                string src = items[i].Key, name = UniqueName(items[i].Value);
                long baseBytes = doneBytes;
                string label = (i + 1) + " / " + items.Count + "   ·   " + items[i].Value;
                progress.Set((float)baseBytes / total, label);
                var reporter = new Progress<long>(b => progress.Set((float)(baseBytes + b) / total, label));
                IProgress<long> rep = reporter;
                try
                {
                    var en = await Task.Run(() => vault.EncryptNew(src, name, b => rep.Report(b)));
                    try { vault.Commit(en); done.Add(src); last = en; }
                    catch (Exception ex) { vault.Discard(en); errors.Add(items[i].Value + ": " + ex.Message); }
                }
                catch (Exception ex) { errors.Add(items[i].Value + ": " + ex.Message); }
                doneBytes = baseBytes + sizes[i];
            }
            progress.Set(1f, "Fertig");
            await Task.Delay(250);
            progress.End();
            SetBusy(false);
            RefreshList();
            if (last != null && done.Count == 1) list.Select(last);
            list.Focus();

            if (errors.Count > 0)
                ModernDialog.Tell(this, "Nicht alles hat geklappt",
                    "Diese Dateien konnten nicht hinzugefügt werden:\n\n" + string.Join("\n", errors.Take(12)), DlgKind.Warn);
            if (done.Count == 0) return;
            toast.Show(Fmt.Files(done.Count) + " sicher verschlüsselt", ToastKind.Success);

            int answer = ModernDialog.Ask(this, "Originale löschen?",
                Fmt.Files(done.Count) + (done.Count == 1 ? " ist" : " sind") + " jetzt verschlüsselt im Tresor.\n\n" +
                "Die Originale liegen aber noch ungeschützt auf deinem PC. Sollen sie gelöscht werden, damit niemand sie ohne Passwort sehen kann?" +
                (settings.SecureDelete ? "\n\nSie werden vorher mit Nullen überschrieben." : ""),
                DlgKind.Question, new[] { "Behalten", "Originale löschen" }, 1);
            if (answer != 1) return;

            int failed = 0;
            foreach (string f in done)
            {
                try
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    if (settings.SecureDelete) Wipe(f);
                    File.Delete(f);
                }
                catch { failed++; }
            }
            foreach (string d in folders)
            {
                try { if (Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length == 0) Directory.Delete(d, true); } catch { }
            }
            if (failed > 0) toast.Show(failed + " Original(e) konnten nicht gelöscht werden (evtl. noch geöffnet).", ToastKind.Warn);
            else toast.Show("Originale gelöscht – jetzt nur noch im Tresor.", ToastKind.Success);
        }

        static void Wipe(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                long len = fs.Length;
                byte[] zero = new byte[1 << 20];
                long rem = len;
                while (rem > 0)
                {
                    int n = (int)Math.Min(zero.Length, rem);
                    fs.Write(zero, 0, n);
                    rem -= n;
                }
                fs.Flush(true);
            }
        }

        // ================================================================ open / edit sync

        void OpenSelected()
        {
            if (busy) return;
            var sel = list.Selected;
            if (sel.Count == 0) return;
            if (sel.Count == 1 && Kinds.IsImage(sel[0].Name) && sel[0].Size <= Imaging.MaxImageBytes)
            {
                var imgs = list.Items.Where(x => Kinds.IsImage(x.Name) && x.Size <= Imaging.MaxImageBytes).ToList();
                using (var v = new ImageViewerForm(vault, imgs, imgs.IndexOf(sel[0]))) v.ShowDialog(this);
                return;
            }
            if (sel.Count > 10) { toast.Show("Bitte höchstens 10 Dateien auf einmal öffnen.", ToastKind.Warn); return; }
            foreach (var en in sel) OpenExternal(en);
        }

        void OpenExternal(VaultEntry en)
        {
            try
            {
                if (tempDir == null)
                {
                    tempDir = Path.Combine(Path.GetTempPath(), "un0yy-vault-" + Guid.NewGuid().ToString("N").Substring(0, 12));
                    Directory.CreateDirectory(tempDir);
                    watcher = new FileSystemWatcher(tempDir);
                    watcher.IncludeSubdirectories = true;
                    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                    watcher.SynchronizingObject = this;
                    FileSystemEventHandler h = delegate { if (!busy) { syncTimer.Stop(); syncTimer.Start(); } };
                    watcher.Changed += h;
                    watcher.Created += h;
                    watcher.Renamed += delegate { syncTimer.Stop(); syncTimer.Start(); };
                    watcher.EnableRaisingEvents = true;
                }
                Cursor = Cursors.WaitCursor;
                string dir = Path.Combine(tempDir, Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(dir);
                string dst = Path.Combine(dir, LeafName(en.Name));
                vault.Export(en, dst);
                var fi = new FileInfo(dst);
                watched[dst] = new Watched { Entry = en, Path = dst, Stamp = fi.LastWriteTimeUtc, Length = fi.Length };
                var psi = new ProcessStartInfo(dst);
                psi.UseShellExecute = true;
                Process.Start(psi);
                toast.Show("Geöffnet – Änderungen werden beim Speichern automatisch verschlüsselt.", ToastKind.Info);
            }
            catch (Exception ex)
            {
                ModernDialog.Tell(this, "Öffnen fehlgeschlagen", ex.Message, DlgKind.Warn);
            }
            finally { Cursor = Cursors.Default; }
        }

        /// Re-encrypts opened temp copies that were changed. Returns the number of files that could not be saved.
        int SyncEdits(bool final)
        {
            int saved = 0, pending = 0;
            foreach (var w in watched.Values.ToList())
            {
                if (!vault.Entries.Contains(w.Entry)) continue;
                FileInfo fi;
                try { fi = new FileInfo(w.Path); if (!fi.Exists) continue; } catch { continue; }
                if (fi.LastWriteTimeUtc == w.Stamp && fi.Length == w.Length) continue;
                try
                {
                    vault.ReplaceContent(w.Entry, w.Path);
                    fi.Refresh();
                    w.Stamp = fi.LastWriteTimeUtc; w.Length = fi.Length;
                    saved++;
                }
                catch (IOException) { pending++; }
                catch (Exception) { pending++; }
            }
            if (saved > 0)
            {
                RefreshList();
                if (!final) toast.Show(saved == 1 ? "Änderungen sicher im Tresor gespeichert." : saved + " Dateien aktualisiert und verschlüsselt.", ToastKind.Success);
            }
            if (pending > 0 && !final) syncTimer.Start();
            return pending;
        }

        // ================================================================ export / rename / favourites / delete

        void ExportSelected()
        {
            if (busy) return;
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
            catch (Exception ex) { ModernDialog.Tell(this, "Speichern fehlgeschlagen", ex.Message, DlgKind.Warn); }
            finally { Cursor = Cursors.Default; }
        }

        void RenameSelected()
        {
            if (busy) return;
            var sel = list.Selected;
            if (sel.Count != 1) return;
            var en = sel[0];
            string folder = en.Name.Contains("/") ? en.Name.Substring(0, en.Name.LastIndexOf('/') + 1) : "";
            string res = PromptDialog.Ask(this, "Umbenennen", "Neuer Name („/“ erstellt einen Ordner)", en.Name, delegate(string v)
            {
                if (v.Length == 0) return "Der Name darf nicht leer sein.";
                if (v.Split('/').Any(p => p.Trim().Length == 0)) return "Ungültiger Name.";
                if (v != en.Name && vault.Entries.Any(x => x != en && string.Equals(x.Name, v, StringComparison.OrdinalIgnoreCase)))
                    return "Eine Datei mit diesem Namen gibt es schon.";
                return null;
            });
            if (res == null || res == en.Name) return;
            try { vault.Rename(en, res); }
            catch (Exception ex) { ModernDialog.Tell(this, "Umbenennen fehlgeschlagen", ex.Message, DlgKind.Warn); return; }
            RefreshList();
            list.Select(en);
            toast.Show("Umbenannt in „" + LeafName(res) + "“", ToastKind.Success);
        }

        void ToggleFavorite(List<VaultEntry> entries, bool fav)
        {
            try { foreach (var en in entries) vault.SetFavorite(en, fav); }
            catch (Exception ex) { ModernDialog.Tell(this, "Speichern fehlgeschlagen", ex.Message, DlgKind.Warn); return; }
            RefreshList();
            toast.Show(fav ? (entries.Count == 1 ? "Zu Favoriten hinzugefügt ★" : Fmt.Files(entries.Count) + " zu Favoriten hinzugefügt")
                           : "Aus Favoriten entfernt", ToastKind.Success);
        }

        void DeleteSelected()
        {
            if (busy) return;
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

        // ================================================================ settings / backup

        void ShowSettings()
        {
            if (busy) return;
            using (var f = new SettingsForm(settings, vault))
            {
                f.Changed += delegate { RepaintAll(this); };
                f.ChangePasswordRequested += delegate
                {
                    using (var c = new ChangePasswordForm(vault))
                        if (c.ShowDialog(f) == DialogResult.OK) toast.Show("Dein Passwort wurde geändert.", ToastKind.Success);
                };
                f.BackupRequested += delegate { Backup(f); };
                f.OpenFolderRequested += delegate
                {
                    try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + vault.Dir + "\"") { UseShellExecute = true }); } catch { }
                };
                f.ShowDialog(this);
            }
            RepaintAll(this);
        }

        async void Backup(IWin32Window owner)
        {
            string dest;
            using (var d = new SaveFileDialog())
            {
                d.Title = "Verschlüsseltes Backup speichern";
                d.Filter = "ZIP-Archiv (*.zip)|*.zip";
                d.FileName = "un0yy-vault-backup-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip";
                if (d.ShowDialog(owner) != DialogResult.OK) return;
                dest = d.FileName;
            }
            string full = Path.GetFullPath(dest), dir = Path.GetFullPath(vault.Dir).TrimEnd('\\') + "\\";
            if (full.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
            {
                ModernDialog.Tell(owner, "Anderen Ort wählen", "Das Backup kann nicht im Tresor-Ordner selbst gespeichert werden.", DlgKind.Warn);
                return;
            }
            Cursor = Cursors.WaitCursor;
            string source = vault.Dir;
            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(dest)) File.Delete(dest);
                    ZipFile.CreateFromDirectory(source, dest, CompressionLevel.Fastest, true);
                });
                ModernDialog.Tell(owner, "Backup erstellt",
                    "Gespeichert unter:\n" + dest + "\n\nDer Inhalt bleibt verschlüsselt. Zum Wiederherstellen das ZIP entpacken und un0yy-vault.exe neben den Ordner „un0yy-vault-data“ legen.",
                    DlgKind.Success);
            }
            catch (Exception ex) { ModernDialog.Tell(owner, "Backup fehlgeschlagen", ex.Message, DlgKind.Warn); }
            finally { Cursor = Cursors.Default; }
        }

        // ================================================================ lock / keys / cleanup

        void CheckIdle()
        {
            if (busy || settings.AutoLock <= 0) return;
            if ((DateTime.Now - lastActivity).TotalMinutes < settings.AutoLock) return;
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
                if (f != this && f is ModernForm && f.Modal) f.Close();
            if (CanFocus) DoLock();
        }

        void DoLock()
        {
            if (busy) { toast.Show("Bitte warten, bis die Verschlüsselung fertig ist.", ToastKind.Warn); return; }
            LockRequested = true;
            Close();
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x100 || m.Msg == 0x104 || m.Msg == 0x200 || m.Msg == 0x201 || m.Msg == 0x204 || m.Msg == 0x20A)
                lastActivity = DateTime.Now;
            return false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { Native.RegisterHotKey(Handle, HotkeyId, 0x0002 | 0x0004 | 0x4000, (int)Keys.L); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HotkeyId)
            {
                foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
                    if (f != this && f is ModernForm && f.Modal) f.Close();
                BeginInvoke((MethodInvoker)DoLock);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (busy) return;
            if (e.Control && e.KeyCode == Keys.F) { search.FocusBox(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.L) { DoLock(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.O) { AddViaDialog(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.V && !search.Box.Focused)
            {
                try
                {
                    if (Clipboard.ContainsFileDropList())
                    {
                        var files = Clipboard.GetFileDropList().Cast<string>().ToList();
                        AddPaths(files);
                        e.Handled = true;
                    }
                }
                catch { }
            }
            else if (e.KeyCode == Keys.Escape && search.Box.Focused && search.Text.Length > 0) { search.Text = ""; e.Handled = true; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy) { e.Cancel = true; toast.Show("Bitte warten, bis die Verschlüsselung fertig ist.", ToastKind.Warn); return; }
            if (watched.Count > 0)
            {
                syncTimer.Stop();
                int pending = SyncEdits(true);
                if (pending > 0)
                {
                    int a = ModernDialog.Ask(this, "Datei noch geöffnet",
                        "Mindestens eine geöffnete Datei konnte nicht gespeichert werden, weil sie noch in einem anderen Programm benutzt wird.\n\nSchließe das Programm und versuch es nochmal – oder sperre trotzdem (die letzten Änderungen gehen dann verloren).",
                        DlgKind.Warn, new[] { "Trotzdem sperren", "Zurück" }, 1);
                    if (a != 0) { e.Cancel = true; LockRequested = false; return; }
                }
            }
            base.OnFormClosing(e);
            if (e.Cancel) return;
            settings.Maximized = WindowState == FormWindowState.Maximized;
            if (WindowState == FormWindowState.Normal) { settings.Width = ClientSize.Width; settings.Height = ClientSize.Height; }
            settings.Save();
            try { Native.UnregisterHotKey(Handle, HotkeyId); } catch { }
            Application.RemoveMessageFilter(this);
            idle.Stop();
            syncTimer.Stop();
            if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); }
            preview.Clear();
            thumbs.Dispose();
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
}
