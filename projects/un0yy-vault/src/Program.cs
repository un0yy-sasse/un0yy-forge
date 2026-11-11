// un0yy Vault - entry point, settings, self test
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Un0yyVault
{
    /// Non-secret UI preferences, stored as plain text next to the vault (settings.ini).
    class AppSettings
    {
        public int Accent, AutoLock = 10, SortCol, Width, Height;
        public bool LockOnMinimize, SecureDelete = true, Grid, SortAsc = true, Maximized;
        string path;

        public static AppSettings Load(string dir)
        {
            var s = new AppSettings();
            s.path = Path.Combine(dir, "settings.ini");
            try
            {
                if (!File.Exists(s.path)) return s;
                foreach (string line in File.ReadAllLines(s.path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int n; int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                    bool b = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                    switch (k)
                    {
                        case "accent": s.Accent = n; break;
                        case "autolock": s.AutoLock = n; break;
                        case "sort": s.SortCol = Math.Max(0, Math.Min(2, n)); break;
                        case "width": s.Width = n; break;
                        case "height": s.Height = n; break;
                        case "lockOnMinimize": s.LockOnMinimize = b; break;
                        case "secureDelete": s.SecureDelete = b; break;
                        case "grid": s.Grid = b; break;
                        case "sortAsc": s.SortAsc = b; break;
                        case "maximized": s.Maximized = b; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(path))) return; // vault not created yet
                var sb = new StringBuilder("# un0yy Vault settings (no secrets in here)\r\n");
                sb.Append("accent=").Append(Accent).Append("\r\n");
                sb.Append("autolock=").Append(AutoLock).Append("\r\n");
                sb.Append("lockOnMinimize=").Append(LockOnMinimize ? 1 : 0).Append("\r\n");
                sb.Append("secureDelete=").Append(SecureDelete ? 1 : 0).Append("\r\n");
                sb.Append("grid=").Append(Grid ? 1 : 0).Append("\r\n");
                sb.Append("sort=").Append(SortCol).Append("\r\n");
                sb.Append("sortAsc=").Append(SortAsc ? 1 : 0).Append("\r\n");
                sb.Append("width=").Append(Width).Append("\r\n");
                sb.Append("height=").Append(Height).Append("\r\n");
                sb.Append("maximized=").Append(Maximized ? 1 : 0).Append("\r\n");
                File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }
    }

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
            var settings = AppSettings.Load(dir);
            Theme.SetAccent(settings.Accent);
            var vault = new Vault(dir);
            while (true)
            {
                using (var login = new LoginForm(vault))
                    if (login.ShowDialog() != DialogResult.OK) return 0;
                bool again;
                using (var main = new MainForm(vault, settings))
                {
                    main.ShowDialog();
                    again = main.LockRequested;
                }
                if (vault.IsUnlocked) vault.Lock();
                if (!again) return 0;
            }
        }

        static void Check(bool ok, string what) { if (!ok) throw new Exception(what); }

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
                Check(v.KeyIterations == Vault.Iterations, "kdf iterations");
                byte[] content = new byte[777777];
                new Random(42).NextBytes(content);
                string src = Path.Combine(dir, "hallo.bin");
                File.WriteAllBytes(src, content);
                long reported = 0;
                var en = v.EncryptNew(src, "ordner/hallo.bin", b => reported = b);
                v.Commit(en);
                Check(reported == content.Length, "progress reporting");
                var en2 = v.AddFile(src, "leer-test.bin");
                v.Remove(en2);
                v.SetFavorite(en, true);
                v.Lock();
                log.AppendLine("create + add + progress + remove + favourite: ok");

                var v2 = new Vault(data);
                Check(!v2.Unlock("falsches passwort"), "wrong password accepted");
                log.AppendLine("wrong password rejected: ok");
                Check(v2.Unlock("richtiges passwort 1"), "right password rejected");
                Check(v2.Entries.Count == 1 && v2.Entries[0].Name == "ordner/hallo.bin" && v2.Entries[0].Size == content.Length, "index mismatch");
                Check(v2.IsFavorite(v2.Entries[0]), "favourite lost");
                log.AppendLine("unlock + index + favourites: ok");

                string outp = Path.Combine(dir, "out.bin");
                v2.Export(v2.Entries[0], outp);
                Check(File.ReadAllBytes(outp).SequenceEqual(content), "decrypted content differs");
                Check(v2.ReadAll(v2.Entries[0], long.MaxValue).SequenceEqual(content), "in-memory decrypt differs");
                Check(v2.ReadAll(v2.Entries[0], 10) == null, "size limit ignored");
                log.AppendLine("decrypt to file + memory: ok");

                v2.Rename(v2.Entries[0], "neu/umbenannt.bin");
                byte[] changed = Encoding.UTF8.GetBytes("geändert!");
                string edit = Path.Combine(dir, "edit.bin");
                File.WriteAllBytes(edit, changed);
                v2.ReplaceContent(v2.Entries[0], edit);
                Check(v2.IsFavorite(v2.Entries[0]), "favourite lost after edit");
                Check(Directory.GetFiles(Path.Combine(data, "files")).Length == 1, "old blob not removed");
                v2.Lock();
                var v3 = new Vault(data);
                Check(v3.Unlock("richtiges passwort 1"), "unlock after edit");
                Check(v3.Entries[0].Name == "neu/umbenannt.bin", "rename not saved");
                Check(v3.ReadAll(v3.Entries[0], 1000).SequenceEqual(changed), "edit not saved");
                log.AppendLine("rename + edit sync: ok");

                string blob = Directory.GetFiles(Path.Combine(data, "files"))[0];
                byte[] raw = File.ReadAllBytes(blob);
                raw[raw.Length / 2] ^= 0x01;
                File.WriteAllBytes(blob, raw);
                bool caught = false;
                try { v3.Export(v3.Entries[0], outp); } catch (InvalidDataException) { caught = true; }
                Check(caught, "tampering not detected");
                Check(!File.Exists(outp), "partial output left behind");
                log.AppendLine("tamper detection: ok");

                Check(!v3.ChangePassword("falsch", "neues passwort 22"), "change with wrong old pw");
                Check(v3.ChangePassword("richtiges passwort 1", "neues passwort 22"), "change failed");
                v3.Lock();
                var v4 = new Vault(data);
                Check(!v4.Unlock("richtiges passwort 1"), "old password still works");
                Check(v4.Unlock("neues passwort 22"), "new password rejected");
                Check(v4.Entries.Count == 1, "entries lost after pw change");
                log.AppendLine("change password: ok");

                foreach (string f in new[] { "index.dat", "meta.dat" })
                    Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(data, f))).Contains("umbenannt"), "plaintext in " + f);
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
