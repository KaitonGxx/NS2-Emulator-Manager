// A build's own data (its portable `user` folder): whether it has one, copying an old %APPDATA% folder into it
// ("Make portable"), and sharing parts of it between builds ("Shared data").
// Sharing works with directory junctions: <build>\user\keys -> <install folder>\Shared data\keys, and so on. Settings
// (user\config) and shader caches are never shared: they belong to each build.
// Rules: nothing is deleted. Data that would be replaced goes to "Shared data\Set aside\". The %APPDATA% folder is
// only read. Nothing happens while a build that uses the data is running.
// C# 5 (the .NET Framework csc).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

public static class UserData
{
    // ---- what can be shared
    public class Item
    {
        public string Key, Label;
        public string[] Paths;        // relative to the user folder
        public bool Default;          // ticked by default in the Shared data dialog
    }
    public static readonly Item[] Items =
    {
        new Item { Key = "keys",     Label = "Keys",                Paths = new[] { "keys" },                                   Default = true },
        new Item { Key = "firmware", Label = "Firmware",            Paths = new[] { @"nand\system\Contents" },                  Default = true },
        new Item { Key = "saves",    Label = "Saves and profiles",  Paths = new[] { @"nand\user", @"nand\system\save" },        Default = true },
        new Item { Key = "mods",     Label = "Mods",                Paths = new[] { "load" },                                   Default = false },
    };
    public static Item Find(string key) { return Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase)); }

    public const string SHARED = "Shared data";
    public const string SET_ASIDE = "Set aside";
    public static string SharedRoot { get { return Path.Combine(Core.Home, SHARED); } }
    static string UserDir(EmuDef e) { return Path.Combine(e.Dir, "user"); }
    static string Full(string p) { return Path.GetFullPath(p).TrimEnd('\\'); }

    // ---- portable or not
    public static bool IsPortable(EmuDef e) { return !string.IsNullOrEmpty(e.Dir) && Directory.Exists(UserDir(e)); }

    // The %APPDATA% folder a non-portable build uses. Eden-family builds name it after themselves (eglantine.exe ->
    // %APPDATA%\eglantine). Some forks kept Eden's name (Lichii uses %APPDATA%\eden): only when the build's own folder
    // doesn't exist, the older names are tried. Returns null when there is nothing to copy.
    public static string AppDataSource(EmuDef e)
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(e.Exe))
        {
            string own = Path.Combine(roaming, Path.GetFileNameWithoutExtension(e.Exe));
            if (Directory.Exists(own)) return HasFiles(own) ? own : null;
        }
        foreach (string n in new[] { "eden", "yuzu", "sudachi", "citron", "suyu", "torzu" })
        {
            string d = Path.Combine(roaming, n);
            if (Directory.Exists(d) && (Directory.Exists(Path.Combine(d, "nand")) || Directory.Exists(Path.Combine(d, "keys"))) && HasFiles(d)) return d;
        }
        return null;
    }
    // True when the AppData folder is named after another emulator (the user is told whose folder it is).
    public static bool SourceIsShared(EmuDef e, string from)
    {
        return from != null && !string.Equals(Path.GetFileName(Full(from)), Path.GetFileNameWithoutExtension(e.Exe ?? ""), StringComparison.OrdinalIgnoreCase);
    }

    // Gives a build its own user folder. With from != null, that folder (e.g. %APPDATA%\eglantine) is copied in first,
    // so the build keeps its keys, firmware, saves and settings. The source is only read.
    public static string MakePortable(EmuDef e, string from, Action<string> log)
    {
        if (string.IsNullOrEmpty(e.Dir) || string.IsNullOrEmpty(e.Exe) || !File.Exists(Path.Combine(e.Dir, e.Exe))) throw new Exception(e.Name + " isn't installed.");
        string bad = Core.FolderProblem(e, e.Dir);
        if (bad != null) throw new Exception(bad);
        if (IsPortable(e)) return e.Name + " already keeps its own data.";
        BlockIfRunning(e, null);
        string user = UserDir(e);
        if (from == null)
        {
            Directory.CreateDirectory(user);
            log("Made " + user + " (empty).");
            return e.Name + " now keeps its own data.";
        }
        string src = Full(from);
        if (!Directory.Exists(src)) throw new Exception(src + " doesn't exist.");
        // nothing may be writing to the source while it is copied: other builds using it, or that emulator itself
        foreach (EmuDef x in Core.Cfg.Emulators)
            if (x != e && !IsPortable(x) && Core.Running(x).Count > 0)
            {
                string xs = AppDataSource(x);
                if (xs != null && string.Equals(Full(xs), src, StringComparison.OrdinalIgnoreCase)) throw new Exception("Close " + x.Name + " first.");
            }
        foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName(Path.GetFileName(src)))
        { p.Dispose(); throw new Exception("Close " + Path.GetFileName(src) + " first."); }
        string link = FindReparse(src);
        if (link != null) { log(link + " is a link (" + JunctionTarget(link) + "); not copying."); throw new Exception("Some of its data is in a linked folder, so it can't be copied. Nothing was changed."); }
        string tmp = Path.Combine(e.Dir, ".user.copying");
        if (Directory.Exists(tmp)) DeleteTree(tmp);   // a copy that was cut short earlier: only ever our own copy
        log("Copying " + src + " into " + user + " (the original is left as it is)...");
        try
        {
            long[] n = CopyTree(src, tmp);
            long[] check = Count(tmp);
            if (check[0] != n[0] || check[1] != n[1]) throw new Exception("The copy doesn't match (" + check[0] + " of " + n[0] + " files).");
            Directory.Move(tmp, user);
            log("Copied " + n[0] + " files (" + Core.Mb(n[1]) + ").");
        }
        catch { try { if (Directory.Exists(tmp)) DeleteTree(tmp); } catch { } throw; }
        return e.Name + " now keeps its own data.";
    }

    // ---- shared data
    public enum State { Off, On, Other }   // Other: linked somewhere else by hand, or only partly shared

    public static State StateOf(EmuDef e, Item it)
    {
        if (!IsPortable(e)) return State.Off;
        int on = 0, other = 0;
        foreach (string rel in it.Paths)
        {
            string b = Path.Combine(UserDir(e), rel);
            string t = JunctionTarget(b);
            if (t == null) continue;
            if (string.Equals(Full(t), Full(Path.Combine(SharedRoot, rel)), StringComparison.OrdinalIgnoreCase)) on++; else other++;
        }
        if (on == it.Paths.Length) return State.On;
        return on == 0 && other == 0 ? State.Off : State.Other;
    }

    public static bool Shares(EmuDef e, string key) { Item it = Find(key); return it != null && StateOf(e, it) == State.On; }
    public static List<Item> SharedItems(EmuDef e) { return Items.Where(i => StateOf(e, i) == State.On).ToList(); }

    // Where e's copy of an item path really is, with every link on the way resolved (a link on user, user\nand or the
    // folder itself). Unknown link kinds: its own path.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandle(IntPtr h, StringBuilder path, uint len, uint flags);
    static string RealPath(EmuDef e, string rel)
    {
        string b = Path.Combine(UserDir(e), rel);
        if (Directory.Exists(b))
        {
            IntPtr h = CreateFile(b, 0, 7, IntPtr.Zero, OPEN_EXISTING, BACKUP_SEMANTICS, IntPtr.Zero);   // follows links
            if (h != INVALID)
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    uint n = GetFinalPathNameByHandle(h, sb, 1024, 0);
                    if (n > 0 && n < 1024)
                    {
                        string p = sb.ToString();
                        if (p.StartsWith(@"\\?\UNC\")) p = @"\\" + p.Substring(8); else if (p.StartsWith(@"\\?\")) p = p.Substring(4);
                        return Full(p);
                    }
                }
                finally { CloseHandle(h); }
        }
        try { return Full(b); } catch { return b; }
    }
    // Some path of the item is a link that doesn't point into the shared folder (made by hand): left alone.
    public static bool LinkedElsewhere(EmuDef e, Item it)
    {
        if (!IsPortable(e)) return false;
        foreach (string rel in it.Paths)
        {
            string t = JunctionTarget(Path.Combine(UserDir(e), rel));
            if (t != null && !string.Equals(t == "(another kind of link)" ? t : Full(t), Full(Path.Combine(SharedRoot, rel)), StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // e and x use the same folder for some path of the item (shared now, partly shared, an old shared folder or a
    // link made by hand).
    static bool SameData(EmuDef e, EmuDef x, Item it)
    {
        if (!IsPortable(e) || !IsPortable(x)) return false;
        return it.Paths.Any(rel => string.Equals(RealPath(e, rel), RealPath(x, rel), StringComparison.OrdinalIgnoreCase));
    }

    // Thrown when the build and the shared folder both hold different data for an item: the caller asks which to keep.
    public class Conflict : Exception { public Item It; public Conflict(Item it) : base("Both have " + it.Label.ToLower() + ".") { It = it; } }

    // Can links be made from e's user folder to the shared folder (NTFS on both)? Tried with a test link, before
    // anything is moved.
    static void CheckLinks(EmuDef e)
    {
        string probe = Path.Combine(UserDir(e), ".linktest");
        try
        {
            if (JunctionTarget(probe) != null) RemoveDirectory(probe);
            else if (Directory.Exists(probe)) DeleteEmptyTree(probe);
            CreateJunction(probe, SharedRoot);
        }
        catch (Exception ex) { throw new Exception("Sharing doesn't work on the drive " + e.Name + " is on. (" + ex.Message + ")"); }
        finally { try { if (JunctionTarget(probe) != null) RemoveDirectory(probe); } catch { } }
    }

    // Links one item of e's user folder to the shared folder. prefer: null (stop with Conflict when both sides hold
    // different data), "shared" (keep the shared copy; the build's goes to Set aside) or "build" (keep the build's; the
    // shared copy goes to Set aside). When only the build has data, it is moved into the shared folder. Everything is
    // checked first; if a step fails, what that step moved is put back.
    public static void Share(EmuDef e, Item it, string prefer, Action<string> log) { Share(e, it, prefer, false, log); }
    static void Share(EmuDef e, Item it, string prefer, bool othersMayRun, Action<string> log)
    {
        CheckFolders(e);
        BlockIfRunning(e, othersMayRun ? null : it);
        string user = UserDir(e);
        // decide everything first, so nothing changes when one part is in conflict or can't be done
        foreach (string rel in it.Paths)
        {
            string b = Path.Combine(user, rel), s = Path.Combine(SharedRoot, rel);
            string t = JunctionTarget(b);
            if (t != null && !string.Equals(Full(t), Full(s), StringComparison.OrdinalIgnoreCase))
                throw new Exception(b + " already points somewhere else (" + t + "); not changing it.");
            if (t != null) continue;
            if (prefer == null && HasFiles(b) && HasFiles(s) && !SameTree(b, s)) throw new Conflict(it);
            string lb = FindReparse(b), ls = FindReparse(s);
            if (lb != null || ls != null) throw new Exception((lb ?? ls) + " is a link; not changing it.");
        }
        CheckLinks(e);
        string asideB = null, asideS = null;   // one Set aside folder per side for this item, keeping each path's place
        foreach (string rel in it.Paths)
        {
            string b = Path.Combine(user, rel), s = Path.Combine(SharedRoot, rel);
            if (JunctionTarget(b) != null) continue;   // already shared
            bool bHas = HasFiles(b), sHas = HasFiles(s);
            string movedB = null, movedS = null; bool bToS = false, bWasEmpty = false, sWasEmpty = false;
            try
            {
                if (bHas && sHas)
                {
                    if (SameTree(b, s) || prefer != "build") { if (asideB == null) asideB = NewAside(e.Name + " - " + it.Label); movedB = SetAside(b, asideB, rel, log); }
                    else
                    {
                        if (asideS == null) asideS = NewAside("Shared - " + it.Label);
                        movedS = SetAside(s, asideS, rel, log);
                        MoveTree(b, s, log); bToS = true;
                    }
                }
                else if (bHas)
                {
                    if (Directory.Exists(s)) { DeleteEmptyTree(s); sWasEmpty = true; }
                    MoveTree(b, s, log); bToS = true;
                }
                else if (Directory.Exists(b)) { DeleteEmptyTree(b); bWasEmpty = true; }
                Directory.CreateDirectory(s);
                Directory.CreateDirectory(Path.GetDirectoryName(b));
                CreateJunction(b, s);
            }
            catch
            {   // put back what this step moved, newest first
                try
                {
                    if (JunctionTarget(b) != null) RemoveDirectory(b);
                    if (bToS && Directory.Exists(s)) MoveTree(s, b, log);
                    if (movedS != null && Directory.Exists(movedS)) { if (Directory.Exists(s) && !HasFiles(s)) DeleteEmptyTree(s); MoveTree(movedS, s, log); }
                    if (movedB != null && Directory.Exists(movedB)) MoveTree(movedB, b, log);
                    if (bWasEmpty) Directory.CreateDirectory(b);
                    if (sWasEmpty || !Directory.Exists(s)) Directory.CreateDirectory(s);   // other builds' links point here
                }
                catch (Exception ex2) { log("Could not put everything back for " + rel + ": " + ex2.Message); }
                throw;
            }
            log(e.Name + ": " + rel + " -> " + s);
        }
    }

    // Stops sharing one item: the build gets its own copy of the shared data (the shared folder keeps it for the other
    // builds). The copy is made and checked while the link is still there; only then is the link swapped for it.
    public static void Unshare(EmuDef e, Item it, Action<string> log)
    {
        CheckFolders(e);
        BlockIfRunning(e, it);
        long need = 0;
        foreach (string rel in it.Paths)
        {
            string b = Path.Combine(UserDir(e), rel), s = Path.Combine(SharedRoot, rel);
            string t = JunctionTarget(b);
            if (t == null) continue;
            if (!string.Equals(Full(t), Full(s), StringComparison.OrdinalIgnoreCase)) throw new Exception(b + " points somewhere else (" + t + "); not changing it.");
            if (Directory.Exists(s)) { if (FindReparse(s) != null) throw new Exception(FindReparse(s) + " is a link; not copying it."); need += Count(s)[1]; }
        }
        try
        {
            DriveInfo di = new DriveInfo(Path.GetPathRoot(Full(UserDir(e))));
            if (di.AvailableFreeSpace < need + (64L << 20)) throw new Exception("Not enough free space on " + di.Name.TrimEnd('\\') + ".");
        }
        catch (ArgumentException) { }
        foreach (string rel in it.Paths)
        {
            string b = Path.Combine(UserDir(e), rel), s = Path.Combine(SharedRoot, rel);
            if (JunctionTarget(b) == null) continue;
            string tmp = b + ".copying";
            if (Directory.Exists(tmp)) DeleteTree(tmp);
            long files = 0;
            if (Directory.Exists(s))
            {
                try
                {
                    long[] n = CopyTree(s, tmp);
                    long[] check = Count(tmp);
                    if (check[0] != n[0] || check[1] != n[1]) throw new Exception("Copying " + rel + " back didn't match; it stays shared.");
                    files = n[0];
                }
                catch { try { if (Directory.Exists(tmp)) DeleteTree(tmp); } catch { } throw; }
            }
            else Directory.CreateDirectory(tmp);
            RemoveJunction(b);
            try { Directory.Move(tmp, b); }
            catch { try { CreateJunction(b, s); DeleteTree(tmp); } catch { } throw; }
            log(e.Name + ": " + rel + " is its own again (" + files + " files copied).");
        }
    }

    // New builds with Config.ShareNew: link those items right after the empty user folder is made (no data to move,
    // so other emulators may stay open). Returns the items that couldn't be shared.
    public static List<string> ShareForNewBuild(EmuDef e, Action<string> log)
    {
        List<string> failed = new List<string>();
        if (Core.Cfg.ShareNew == null) return failed;
        foreach (string k in Core.Cfg.ShareNew)
        {
            Item it = Find(k);
            if (it == null) continue;
            try { Share(e, it, "shared", !OwnData(e, it), log); }
            catch (Exception ex) { log("Could not share " + it.Label.ToLower() + ": " + ex.Message); failed.Add(it.Label); }
        }
        return failed;
    }

    // Before launching e: another open emulator that uses the same saves (or mods) would write the same files.
    public static string LaunchProblem(EmuDef e)
    {
        foreach (Item it in Items.Where(i => i.Key == "saves" || i.Key == "mods"))
            foreach (EmuDef x in Core.Cfg.Emulators)
                if (x != e && SameData(e, x, it) && Core.Running(x).Count > 0)
                    return x.Name + " is open and uses the same saves. Close it first.";
        return null;
    }

    // Does e hold its own (not shared) data for the item?
    public static bool OwnData(EmuDef e, Item it)
    {
        if (!IsPortable(e)) return false;
        foreach (string rel in it.Paths)
        {
            string b = Path.Combine(UserDir(e), rel);
            if (JunctionTarget(b) == null && HasFiles(b)) return true;
        }
        return false;
    }

    // When e's copy of the item (null: the shared one) was last changed, for "newest first".
    public static DateTime LastChanged(Item it, EmuDef e)
    {
        DateTime t = DateTime.MinValue;
        foreach (string rel in it.Paths)
        {
            string d = e == null ? Path.Combine(SharedRoot, rel) : Path.Combine(UserDir(e), rel);
            if (e != null && JunctionTarget(d) != null) continue;
            try { foreach (string f in Files(d).Values) { DateTime w = File.GetLastWriteTimeUtc(f); if (w > t) t = w; } } catch { }
        }
        return t;
    }

    public static bool SharedHasData(Item it) { return it.Paths.Any(rel => HasFiles(Path.Combine(SharedRoot, rel))); }

    // One build per distinct copy of the item among holders, leaving out copies identical to what the shared folder
    // already has. Used to ask "whose saves should all of them use?" only when the copies really differ.
    public static List<EmuDef> DistinctHolders(Item it, List<EmuDef> holders)
    {
        List<EmuDef> o = new List<EmuDef>();
        foreach (EmuDef h in holders)
        {
            if (SameAll(it, Path.Combine(UserDir(h), "{0}"), Path.Combine(SharedRoot, "{0}"))) continue;
            if (o.Any(x => SameAll(it, Path.Combine(UserDir(h), "{0}"), Path.Combine(UserDir(x), "{0}")))) continue;
            o.Add(h);
        }
        return o;
    }
    static bool SameAll(Item it, string a, string b)
    {
        foreach (string rel in it.Paths)
        {
            string pa = a.Replace("{0}", rel), pb = b.Replace("{0}", rel);
            bool ha = HasFiles(pa), hb = HasFiles(pb);
            if (ha != hb) return false;
            if (ha && !SameTree(pa, pb)) return false;
        }
        return true;
    }

    // Deletes a folder tree; links inside it are removed as links (what they point at is never touched).
    public static void DeleteTree(string d)
    {
        if (JunctionTarget(d) != null || IsReparse(d)) { if (!RemoveDirectory(d)) throw new Exception("Can't remove the link " + d + "."); return; }
        foreach (string s in Directory.GetDirectories(d)) DeleteTree(s);
        foreach (string f in Directory.GetFiles(d)) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); }
        Directory.Delete(d, false);
    }

    // Builds that use an item from the shared folder (for the Shared data card / dialog).
    public static List<EmuDef> Users(Item it) { return Core.Cfg.Emulators.Where(x => StateOf(x, it) != State.Off).ToList(); }

    static void CheckFolders(EmuDef e)
    {
        if (string.IsNullOrEmpty(e.Dir) || !IsPortable(e)) throw new Exception(e.Name + " doesn't keep its own data yet.");
        string bad = Core.FolderProblem(e, e.Dir);
        if (bad != null) throw new Exception(bad);
        Directory.CreateDirectory(SharedRoot);
    }

    // e itself, and when an item is given every other build using the same data for it, must be closed.
    static void BlockIfRunning(EmuDef e, Item it)
    {
        List<string> run = Core.Running(e);
        if (run.Count > 0) throw new Exception("Close " + e.Name + " first.");
        if (it == null) return;
        foreach (EmuDef x in Core.Cfg.Emulators)
            if (x != e && (StateOf(x, it) != State.Off || SameData(e, x, it)) && Core.Running(x).Count > 0) throw new Exception("Close " + x.Name + " first.");
    }

    // ---- file helpers (never follow junctions)
    static bool IsReparse(string p)
    {
        try { return (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0; } catch { return false; }
    }

    public static bool HasFiles(string d)
    {
        if (!Directory.Exists(d)) return false;
        if (Directory.GetFiles(d).Length > 0) return true;
        foreach (string s in Directory.GetDirectories(d)) if (!IsReparse(s) && HasFiles(s)) return true;
        return false;
    }

    static long[] Count(string d)
    {
        long files = 0, bytes = 0;
        foreach (string f in Directory.GetFiles(d)) { files++; bytes += new FileInfo(f).Length; }
        foreach (string s in Directory.GetDirectories(d)) if (!IsReparse(s)) { long[] n = Count(s); files += n[0]; bytes += n[1]; }
        return new[] { files, bytes };
    }

    // Copies a folder tree (files and folders, not junctions inside it). Returns { files, bytes }.
    static long[] CopyTree(string src, string dst)
    {
        long files = 0, bytes = 0;
        Directory.CreateDirectory(dst);
        foreach (string f in Directory.GetFiles(src))
        {
            string to = Path.Combine(dst, Path.GetFileName(f));
            File.Copy(f, to, false);
            try { File.SetAttributes(to, File.GetAttributes(f) & ~FileAttributes.ReadOnly); } catch { }
            files++; bytes += new FileInfo(f).Length;
        }
        foreach (string s in Directory.GetDirectories(src))
        {
            if (IsReparse(s)) continue;
            long[] n = CopyTree(s, Path.Combine(dst, Path.GetFileName(s)));
            files += n[0]; bytes += n[1];
        }
        return new[] { files, bytes };
    }

    // Same files (names, sizes, SHA-256) in both trees.
    static bool SameTree(string a, string b)
    {
        Dictionary<string, string> fa = Files(a), fb = Files(b);
        if (fa.Count != fb.Count) return false;
        foreach (KeyValuePair<string, string> kv in fa)
        {
            string other; if (!fb.TryGetValue(kv.Key, out other)) return false;
            if (new FileInfo(kv.Value).Length != new FileInfo(other).Length) return false;
        }
        foreach (KeyValuePair<string, string> kv in fa) if (Sha(kv.Value) != Sha(fb[kv.Key])) return false;
        return true;
    }
    static Dictionary<string, string> Files(string root)
    {
        Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Action<string> walk = null;
        walk = dir =>
        {
            foreach (string f in Directory.GetFiles(dir)) d[f.Substring(root.Length)] = f;
            foreach (string s in Directory.GetDirectories(dir)) if (!IsReparse(s)) walk(s);
        };
        walk(Full(root));
        return d;
    }
    static string Sha(string f)
    {
        using (SHA256 h = SHA256.Create()) using (FileStream s = File.OpenRead(f)) return Convert.ToBase64String(h.ComputeHash(s));
    }

    // Moves a tree: a rename on the same drive; otherwise copy, check, rename the original out of the way (fails cleanly
    // if a file in it is open), then remove it. The data exists at the destination before anything is removed.
    static void MoveTree(string src, string dst, Action<string> log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        if (string.Equals(Path.GetPathRoot(Full(src)), Path.GetPathRoot(Full(dst)), StringComparison.OrdinalIgnoreCase))
        { Directory.Move(src, dst); return; }
        string link = FindReparse(src);
        if (link != null) throw new Exception(link + " is a link; not moving it.");
        if (Exists(dst)) throw new Exception(dst + " already exists.");
        long[] n;
        try
        {
            n = CopyTree(src, dst);
            long[] check = Count(dst);
            if (check[0] != n[0] || check[1] != n[1]) throw new Exception("Copying " + src + " didn't match; nothing was removed.");
        }
        catch { try { if (Directory.Exists(dst)) DeleteTree(dst); } catch { } throw; }   // our own partial copy; src is untouched
        string gone = src + ".moving";
        try { Directory.Move(src, gone); }
        catch { try { DeleteTree(dst); } catch { } throw new Exception("A file in " + src + " is in use; nothing was moved."); }
        try { DeleteTree(gone); } catch (Exception ex) { log("Moved, but could not remove " + gone + ": " + ex.Message); }
        log("Moved " + n[0] + " files to " + dst + ".");
    }

    // A new folder under "Shared data\Set aside" for one item's replaced copy ("<label> <date>", made unique).
    static string NewAside(string label)
    {
        string root = Path.Combine(SharedRoot, SET_ASIDE);
        string name = Core_Safe(label + " " + DateTime.Now.ToString("yyyy-MM-dd HHmm"));
        string p = Path.Combine(root, name);
        for (int i = 2; Directory.Exists(p); i++) p = Path.Combine(root, name + " " + i);
        return p;
    }
    // Moves dir to <aside>\<rel> (so a two-folder item keeps its layout and can be copied back as is). Returns where.
    static string SetAside(string dir, string aside, string rel, Action<string> log)
    {
        string p = Path.Combine(aside, rel);
        MoveTree(dir, p, log);
        log("Kept the old copy in " + p + ".");
        return p;
    }
    static string Core_Safe(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }

    // The first link (reparse point) inside d, or null.
    static string FindReparse(string d)
    {
        if (!Directory.Exists(d)) return null;
        foreach (string s in Directory.GetDirectories(d)) { if (IsReparse(s)) return s; string r = FindReparse(s); if (r != null) return r; }
        return null;
    }

    static void DeleteEmptyTree(string d)
    {
        foreach (string s in Directory.GetDirectories(d)) { if (IsReparse(s)) throw new Exception(s + " is a link; not touching it."); DeleteEmptyTree(s); }
        if (Directory.GetFiles(d).Length > 0) throw new Exception(d + " isn't empty.");
        Directory.Delete(d, false);
    }

    // ---- junctions
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr h, uint code, byte[] inBuf, int inSize, byte[] outBuf, int outSize, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool RemoveDirectory(string path);
    const uint FSCTL_SET_REPARSE_POINT = 0x000900A4, FSCTL_GET_REPARSE_POINT = 0x000900A8, TAG_MOUNT_POINT = 0xA0000003;
    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, OPEN_EXISTING = 3;
    const uint BACKUP_SEMANTICS = 0x02000000, OPEN_REPARSE_POINT = 0x00200000;
    static readonly IntPtr INVALID = new IntPtr(-1);

    static IntPtr Open(string p, uint access)
    {
        IntPtr h = CreateFile(p, access, 7, IntPtr.Zero, OPEN_EXISTING, BACKUP_SEMANTICS | OPEN_REPARSE_POINT, IntPtr.Zero);
        if (h == INVALID) throw new Exception("Can't open " + p + " (error " + Marshal.GetLastWin32Error() + ").");
        return h;
    }

    // Makes link (a new, empty folder) a junction to target.
    public static void CreateJunction(string link, string target)
    {
        string t = Full(target);
        if (Exists(link)) throw new Exception(link + " already exists.");
        Directory.CreateDirectory(link);
        string subst = @"\??\" + t, print = t;
        byte[] names = Encoding.Unicode.GetBytes(subst + "\0" + print + "\0");
        byte[] buf = new byte[8 + 8 + names.Length];
        BitConverter.GetBytes(TAG_MOUNT_POINT).CopyTo(buf, 0);
        BitConverter.GetBytes((ushort)(8 + names.Length)).CopyTo(buf, 4);
        BitConverter.GetBytes((ushort)0).CopyTo(buf, 8);                              // substitute name offset
        BitConverter.GetBytes((ushort)(subst.Length * 2)).CopyTo(buf, 10);
        BitConverter.GetBytes((ushort)((subst.Length + 1) * 2)).CopyTo(buf, 12);      // print name offset
        BitConverter.GetBytes((ushort)(print.Length * 2)).CopyTo(buf, 14);
        names.CopyTo(buf, 16);
        IntPtr h = IntPtr.Zero;
        try
        {
            h = Open(link, GENERIC_WRITE);
            int ret;
            if (!DeviceIoControl(h, FSCTL_SET_REPARSE_POINT, buf, buf.Length, null, 0, out ret, IntPtr.Zero))
                throw new Exception("Can't link " + link + " (error " + Marshal.GetLastWin32Error() + ").");
        }
        catch { if (h != IntPtr.Zero && h != INVALID) { CloseHandle(h); h = IntPtr.Zero; } try { Directory.Delete(link, false); } catch { } throw; }
        finally { if (h != IntPtr.Zero && h != INVALID) CloseHandle(h); }
    }

    // The folder a junction points at, or null when p is not a junction.
    public static string JunctionTarget(string p)
    {
        if (!Directory.Exists(p) || !IsReparse(p)) return null;
        IntPtr h = IntPtr.Zero;
        try
        {
            h = Open(p, GENERIC_READ);
            byte[] buf = new byte[16384]; int ret;
            if (!DeviceIoControl(h, FSCTL_GET_REPARSE_POINT, null, 0, buf, buf.Length, out ret, IntPtr.Zero)) return null;
            if (BitConverter.ToUInt32(buf, 0) != TAG_MOUNT_POINT) return "(another kind of link)";
            int off = BitConverter.ToUInt16(buf, 8), len = BitConverter.ToUInt16(buf, 10);
            string s = Encoding.Unicode.GetString(buf, 16 + off, len);
            return s.StartsWith(@"\??\") ? s.Substring(4) : s;
        }
        catch { return null; }
        finally { if (h != IntPtr.Zero && h != INVALID) CloseHandle(h); }
    }

    // Removes only the link; what it points at is untouched.
    static void RemoveJunction(string p)
    {
        if (JunctionTarget(p) == null) throw new Exception(p + " isn't a link.");
        if (!RemoveDirectory(p)) throw new Exception("Can't remove the link " + p + " (error " + Marshal.GetLastWin32Error() + ").");
    }

    static bool Exists(string p) { return File.Exists(p) || Directory.Exists(p); }
}
