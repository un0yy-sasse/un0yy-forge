// un0yy Vault v1.0.1 - password protected, AES-256 encrypted file vault.
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
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

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

}
