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

    // e's paths (keys, firmware, saves) that are folder links to an existing folder other than this install's shared
    // one: path -> target. Typically a shared folder of an earlier install in another place.
    public static Dictionary<string, string> ForeignLinks(EmuDef e)
    {
        Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!IsPortable(e)) return d;
        foreach (Item it in HostItems)
            foreach (string rel in it.Paths)
            {
                string b = Path.Combine(UserDir(e), rel), t = JunctionTarget(b);
                if (t == null || t == "(another kind of link)" || !Directory.Exists(t)) continue;
                if (string.Equals(Full(t), Full(Path.Combine(SharedRoot, rel)), StringComparison.OrdinalIgnoreCase)) continue;
                d[b] = Full(t);
            }
        return d;
    }

    // A link the manager can't take over: another kind of link, or one whose folder is gone.
    public static bool OddLinks(EmuDef e)
    {
        if (!IsPortable(e)) return false;
        Dictionary<string, string> f = ForeignLinks(e);
        foreach (Item it in HostItems)
            foreach (string rel in it.Paths)
            {
                string b = Path.Combine(UserDir(e), rel), t = JunctionTarget(b);
                if (t == null || f.ContainsKey(b)) continue;
                if (t != "(another kind of link)" && string.Equals(Full(t), Full(Path.Combine(SharedRoot, rel)), StringComparison.OrdinalIgnoreCase)) continue;
                return true;
            }
        return false;
    }
    // The folder (for messages) e's foreign links lead to: "...\Shared data" when they point into one.
    public static string ForeignRoot(EmuDef e)
    {
        string t = ForeignLinks(e).Values.FirstOrDefault();
        if (t == null) return null;
        int i = t.IndexOf("\\" + SHARED + "\\", StringComparison.OrdinalIgnoreCase);
        return i >= 0 ? t.Substring(0, i + SHARED.Length + 1) : t;
    }

    // Replaces e's foreign links with a copy of what they point to, so the data becomes e's own. The copy is made and
    // checked while the link is still there; the other folder is never changed.
    public static void BringHome(EmuDef e, Action<string> log)
    {
        Dictionary<string, string> f = ForeignLinks(e);
        if (f.Count == 0) return;
        CheckFolders(e);
        if (Core.Running(e).Count > 0) throw new Exception("Close " + e.Name + " first.");
        long need = 0;
        foreach (string t in f.Values) { if (FindReparse(t) != null) throw new Exception(FindReparse(t) + " is a link; not copying it."); need += Count(t)[1]; }
        try
        {
            DriveInfo di = new DriveInfo(Path.GetPathRoot(Full(UserDir(e))));
            if (di.AvailableFreeSpace < need + (64L << 20)) throw new Exception("Not enough free space on " + di.Name.TrimEnd('\\') + ".");
        }
        catch (ArgumentException) { }
        foreach (KeyValuePair<string, string> p in f)
        {
            string b = p.Key, t = p.Value, tmp = b + ".copying";
            if (Directory.Exists(tmp)) DeleteTree(tmp);
            try
            {
                long[] n = CopyTree(t, tmp), check = Count(tmp);
                if (check[0] != n[0] || check[1] != n[1]) throw new Exception("Copying " + t + " didn't match; nothing changed.");
            }
            catch { try { if (Directory.Exists(tmp)) DeleteTree(tmp); } catch { } throw; }
            RemoveJunction(b);
            try { Directory.Move(tmp, b); }
            catch { try { CreateJunction(b, t); DeleteTree(tmp); } catch { } throw; }
            log(e.Name + ": " + b.Substring(UserDir(e).Length + 1) + " copied in from " + t + " (that folder isn't changed).");
        }
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
    // Returns the Set aside folder that got e's own copy (null when nothing of e's was set aside).
    public static string Share(EmuDef e, Item it, string prefer, Action<string> log) { return Share(e, it, prefer, false, log); }
    static string Share(EmuDef e, Item it, string prefer, bool othersMayRun, Action<string> log)
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
        List<Action> undo = new List<Action>();   // paths of this item already done: put back too when a later one fails
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
            {   // put back what this step moved, then the paths of this item already done: newest first
                PutBack(b, s, movedB, movedS, bToS, bWasEmpty, sWasEmpty, rel, log);
                for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
                throw;
            }
            {
                string b0 = b, s0 = s, mb = movedB, ms = movedS, r0 = rel; bool bts = bToS, bwe = bWasEmpty, swe = sWasEmpty;
                undo.Add(() => PutBack(b0, s0, mb, ms, bts, bwe, swe, r0, log));
            }
            log(e.Name + ": " + rel + " -> " + s);
        }
        return asideB;
    }

    // Undoes one path of Share: the link goes, and what was moved goes back where it was.
    static void PutBack(string b, string s, string movedB, string movedS, bool bToS, bool bWasEmpty, bool sWasEmpty, string rel, Action<string> log)
    {
        try
        {
            if (JunctionTarget(b) != null) RemoveDirectory(b);
            if (bToS && Directory.Exists(s)) MoveTree(s, b, log);
            if (movedS != null && Directory.Exists(movedS)) { if (Directory.Exists(s) && !HasFiles(s)) DeleteEmptyTree(s); MoveTree(movedS, s, log); }
            if (movedB != null && Directory.Exists(movedB)) MoveTree(movedB, b, log);
            if (bWasEmpty || !Directory.Exists(b)) Directory.CreateDirectory(b);
            if (sWasEmpty || !Directory.Exists(s)) Directory.CreateDirectory(s);   // other builds' links point here
        }
        catch (Exception ex2) { log("Could not put everything back for " + rel + ": " + ex2.Message); }
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
        string[] rels = it.Key == "saves" ? new[] { @"nand\user\save", @"nand\system\save" } : it.Paths;   // game saves, not updates/DLC
        foreach (string rel in rels)
        {
            string d = e == null ? Path.Combine(SharedRoot, rel) : Path.Combine(UserDir(e), rel);
            if (e != null && (JunctionTarget(d) != null || JunctionTarget(Path.GetDirectoryName(d)) != null)) continue;
            try { foreach (string f in Files(d).Values) { DateTime w = File.GetLastWriteTimeUtc(f); if (w > t) t = w; } } catch { }
        }
        return t;
    }

    public static bool SharedHasData(Item it) { return it.Paths.Any(rel => HasFiles(Path.Combine(SharedRoot, rel))); }

    // Why build k can't take part in sharing right now (plain words), or null.
    public static string SharingProblem(EmuDef k)
    {
        if (!IsPortable(k)) return "keeps its saves outside its folder";
        if (Core.Running(k).Count > 0) return "close it first";
        if (OddLinks(k)) return "uses a folder link the manager didn't make";
        try { CheckFolders(k); CheckLinks(k); } catch (Exception ex) { return ex.Message; }
        return null;
    }

    // A failure while sharing, in plain words for the card / message (details stay in the log).
    public static string PlainReason(Exception ex)
    {
        string m = ex.Message ?? "";
        if (m.StartsWith("Close ") && m.EndsWith(" first.")) return "close " + m.Substring(6, m.Length - 13) + " first";
        if (m.StartsWith("Sharing doesn't work") || m.StartsWith("Not enough free space")) return m.TrimEnd('.');
        if (ex is IOException || ex is UnauthorizedAccessException) return "a file is in use; it tries again later";
        return "it didn't work (see the log)";
    }

    // "Share data with other emulators" ticked on host. For keys, firmware and saves, everyone ends up on one shared copy:
    //  - host's own copy; or, when host already uses the shared folder, the shared copy (the latest progress);
    //  - when host has neither, the copy changed last (another build's, or what the shared folder holds).
    // Every other build then joins (JoinOne). Builds that can't join now are left out and listed ("Name: why").
    // host itself must be able to take part; otherwise nothing changes.
    public static List<string> StartSharing(EmuDef host, List<EmuDef> all, Action<string> log)
    {
        string hp = SharingProblem(host);
        if (hp != null) throw new Exception(host.Name + ": " + hp + ".");
        BringHome(host, log);   // data linked to another (older) shared folder becomes its own first
        Dictionary<EmuDef, string> skip = new Dictionary<EmuDef, string>();
        foreach (EmuDef k in all) if (k != host) { string p = SharingProblem(k); if (p != null) skip[k] = p; }
        foreach (Item it in HostItems)
        {
            EmuDef k0 = null;
            if (OwnData(host, it)) k0 = host;
            else if (StateOf(host, it) == State.Off)
            {   // host has none of it: the newest copy among the others and the shared folder
                DateTime best = SharedHasData(it) ? LastChanged(it, null) : DateTime.MinValue;
                foreach (EmuDef k in all)
                    if (k != host && !skip.ContainsKey(k) && OwnData(k, it)) { DateTime t = LastChanged(it, k); if (t > best) { best = t; k0 = k; } }
            }
            if (k0 != null && StateOf(k0, it) != State.On) Share(k0, it, "build", log);
            if (StateOf(host, it) != State.On) JoinItem(host, it, log);
        }
        List<string> left = skip.Select(p => p.Key.Name + ": " + p.Value).ToList();
        foreach (EmuDef k in all)
        {
            if (k == host || skip.ContainsKey(k)) continue;
            try { JoinOne(k, log); }
            catch (Exception ex) { left.Add(k.Name + ": " + PlainReason(ex)); log(k.Name + " didn't join: " + ex.Message); }
        }
        return left;
    }

    // Links what k doesn't fully share yet to the shared copy. Checked first; nothing changes when k can't take part.
    public static void JoinOne(EmuDef k, Action<string> log)
    {
        string p = SharingProblem(k);
        if (p != null) throw new Exception(p.StartsWith("close") ? "Close " + k.Name + " first." : p);
        BringHome(k, log);
        foreach (Item it in HostItems)
        {
            if (StateOf(k, it) == State.On) continue;
            if (LinkedElsewhere(k, it)) throw new Exception(it.Label + " is in a folder linked by hand");
            JoinItem(k, it, log);
        }
    }

    // One item: for saves, k's saves for games the shared saves don't have (and its installed updates/DLC the shared
    // folder lacks) are added to them first; then the rest of k's own copy goes to Set aside and k is linked. If the
    // linking fails, what was added is moved back to k.
    static void JoinItem(EmuDef k, Item it, Action<string> log)
    {
        List<KeyValuePair<string, string>> moved = it.Key == "saves" ? MergeSaves(k, log) : new List<KeyValuePair<string, string>>();
        string aside;
        try { aside = Share(k, it, "shared", log); }
        catch
        {
            for (int i = moved.Count - 1; i >= 0; i--)
                try
                {
                    string from = moved[i].Key, to = moved[i].Value;
                    if (Directory.Exists(to)) MoveTree(to, from, log);
                    else if (File.Exists(to)) { Directory.CreateDirectory(Path.GetDirectoryName(from)); MoveFile(to, from); }
                }
                catch (Exception ex2) { log("Could not move back " + moved[i].Value + ": " + ex2.Message); }
            throw;
        }
        if (it.Key == "saves") Remember(k, aside, moved);
    }

    // What "Go back" needs when sharing stops: the Set aside folder with k's own saves, and what k added to the shared
    // saves (its path | the shared path). Kept in the config (saved by the caller) until sharing stops.
    static void Remember(EmuDef k, string aside, List<KeyValuePair<string, string>> moved)
    {
        lock (Core.Cfg)
        {
            if (Core.Cfg.ShareBackups == null) Core.Cfg.ShareBackups = new Dictionary<string, List<string>>();
            if (Core.Cfg.ShareMerged == null) Core.Cfg.ShareMerged = new Dictionary<string, List<string>>();
            if (!Core.Cfg.ShareBackups.ContainsKey(k.Id)) Core.Cfg.ShareBackups[k.Id] = new List<string>();
            if (!Core.Cfg.ShareMerged.ContainsKey(k.Id)) Core.Cfg.ShareMerged[k.Id] = new List<string>();
            if (aside != null) Core.Cfg.ShareBackups[k.Id].Add(aside);
            string u = UserDir(k);
            foreach (KeyValuePair<string, string> m in moved)
                if (m.Key.StartsWith(u + "\\", StringComparison.OrdinalIgnoreCase) && m.Value.StartsWith(SharedRoot + "\\", StringComparison.OrdinalIgnoreCase))
                    Core.Cfg.ShareMerged[k.Id].Add(m.Key.Substring(u.Length + 1) + "|" + m.Value.Substring(SharedRoot.Length + 1));
        }
    }

    // Can k "go back" to its own saves from before sharing (something was remembered for it)?
    public static bool CanGoBack(EmuDef k)
    {
        List<string> a = null, m = null;
        if (Core.Cfg.ShareBackups != null) Core.Cfg.ShareBackups.TryGetValue(k.Id, out a);
        if (Core.Cfg.ShareMerged != null) Core.Cfg.ShareMerged.TryGetValue(k.Id, out m);
        return (a != null && a.Any(Directory.Exists)) || (m != null && m.Count > 0);
    }

    const string DEVICE = "00000000000000000000000000000000";   // device saves (not a player profile)

    // Moves k's saves for games the shared saves don't have into them: per profile with the same id; when each side
    // has one player profile, into the shared one; when the shared saves have none yet, as they are. Device saves only
    // by name. Saves for a game both have stay with k (they go to Set aside). Installed updates/DLC (nand\user\Contents)
    // the shared folder lacks are added file by file. Returns what was moved (from, to), to undo it.
    static List<KeyValuePair<string, string>> MergeSaves(EmuDef k, Action<string> log)
    {
        List<KeyValuePair<string, string>> moved = new List<KeyValuePair<string, string>>();
        Item saves = Find("saves");
        string bUser = Path.Combine(UserDir(k), @"nand\user"), sUser = Path.Combine(SharedRoot, @"nand\user");
        if (JunctionTarget(bUser) != null || !HasFiles(bUser) || !SharedHasData(saves)) return moved;   // nothing to add, or nothing shared yet
        if (FindReparse(bUser) != null || FindReparse(sUser) != null) return moved;
        BlockIfRunning(k, saves);
        string b = Path.Combine(bUser, @"save\0000000000000000"), s = Path.Combine(sUser, @"save\0000000000000000");
        if (Directory.Exists(b))
        {
            Directory.CreateDirectory(s);
            Func<string, List<string>> players = d => Directory.GetDirectories(d).Where(x => !IsReparse(x) && !string.Equals(Path.GetFileName(x), DEVICE, StringComparison.OrdinalIgnoreCase)).ToList();
            List<string> pb = players(b), ps = players(s);
            foreach (string p in Directory.GetDirectories(b))
            {
                if (IsReparse(p) || !HasFiles(p)) continue;
                string name = Path.GetFileName(p), target;
                if (string.Equals(name, DEVICE, StringComparison.OrdinalIgnoreCase)) target = Path.Combine(s, DEVICE);
                else target = ps.FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.OrdinalIgnoreCase))
                              ?? (ps.Count == 0 ? Path.Combine(s, name) : pb.Count == 1 && ps.Count == 1 ? ps[0] : null);
                if (target == null) continue;   // several profiles on both sides: left with k (Set aside)
                if (!Directory.Exists(target))
                {
                    MoveTree(p, target, log); moved.Add(new KeyValuePair<string, string>(p, target));
                    log(k.Name + ": saves of profile " + name + " added to the shared saves.");
                    continue;
                }
                foreach (string game in Directory.GetDirectories(p))
                {
                    if (IsReparse(game) || !HasFiles(game)) continue;
                    string to = Path.Combine(target, Path.GetFileName(game));
                    if (Directory.Exists(to) || File.Exists(to)) continue;   // both have this game: the shared one is used
                    MoveTree(game, to, log); moved.Add(new KeyValuePair<string, string>(game, to));
                    log(k.Name + ": save " + Path.GetFileName(game) + " added to the shared saves.");
                }
            }
        }
        string bc = Path.Combine(bUser, "Contents"), sc = Path.Combine(sUser, "Contents");
        if (HasFiles(bc))
        {
            int n = 0;
            foreach (KeyValuePair<string, string> f in Files(bc))
            {
                string to = Path.Combine(sc, f.Key.TrimStart('\\'));
                if (File.Exists(to) || Directory.Exists(to)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                MoveFile(f.Value, to); moved.Add(new KeyValuePair<string, string>(f.Value, to)); n++;
            }
            if (n > 0) log(k.Name + ": " + n + " installed update/DLC file(s) added to the shared folder.");
        }
        return moved;
    }

    static void MoveFile(string from, string to)
    {
        if (string.Equals(Path.GetPathRoot(Full(from)), Path.GetPathRoot(Full(to)), StringComparison.OrdinalIgnoreCase)) File.Move(from, to);
        else { File.Copy(from, to, false); if (new FileInfo(to).Length != new FileInfo(from).Length) { File.Delete(to); throw new IOException("Copying " + from + " didn't match."); } File.Delete(from); }
    }
    // Unticked: sharing stops. host keeps (a copy of) the latest data. goBack false: every other build gets a copy of
    // the latest too. goBack true: the others get their own saves from before sharing back (their Set aside copy, plus
    // what they added to the shared saves, as it is now); their keys and firmware stay the latest. The Set aside
    // copies stay where they are.
    public static void StopSharing(EmuDef host, List<EmuDef> all, bool goBack, Action<string> log)
    {
        foreach (EmuDef k in all)
            foreach (Item it in Items)
            {
                if (StateOf(k, it) == State.Off || LinkedElsewhere(k, it)) continue;
                if (goBack && k != host && it.Key == "saves" && CanGoBack(k)) RestoreOwnSaves(k, log);
                else Unshare(k, it, log);
            }
        lock (Core.Cfg) { Core.Cfg.ShareBackups = null; Core.Cfg.ShareMerged = null; }
    }
    public static void StopSharing(List<EmuDef> all, Action<string> log) { StopSharing(null, all, false, log); }

    // Why sharing can't be stopped right now (plain words), or null: an emulator is open, or there's no room for the
    // copies every emulator gets.
    public static string StopProblem(List<EmuDef> all)
    {
        List<string> open = all.Where(k => Core.Running(k).Count > 0).Select(k => k.Name).ToList();
        if (open.Count > 0) return "Close " + string.Join(" and ", open) + " first.";
        long one = 0;
        foreach (Item it in HostItems) foreach (string rel in it.Paths) { string d = Path.Combine(SharedRoot, rel); if (Directory.Exists(d)) one += Count(d)[1]; }
        long need = one * all.Count(k => HostItems.Any(i => StateOf(k, i) != State.Off));
        try
        {
            DriveInfo di = new DriveInfo(Path.GetPathRoot(Full(SharedRoot)));
            if (di.AvailableFreeSpace < need + (64L << 20)) return "There isn't enough free space on " + di.Name.TrimEnd('\\') + " for every emulator's copy.";
        }
        catch { }
        return null;
    }

    // k's saves as they were before it joined: built next to its links, checked, then swapped in for them.
    static void RestoreOwnSaves(EmuDef k, Action<string> log)
    {
        Item saves = Find("saves");
        CheckFolders(k);
        BlockIfRunning(k, saves);
        string u = UserDir(k);
        List<string> asides = Core.Cfg.ShareBackups != null && Core.Cfg.ShareBackups.ContainsKey(k.Id) ? Core.Cfg.ShareBackups[k.Id] : new List<string>();
        List<string> merged = Core.Cfg.ShareMerged != null && Core.Cfg.ShareMerged.ContainsKey(k.Id) ? Core.Cfg.ShareMerged[k.Id] : new List<string>();
        Dictionary<string, string> tmp = new Dictionary<string, string>();
        try
        {
            foreach (string rel in saves.Paths)
            {
                string t = Path.Combine(u, rel) + ".restoring";
                if (Directory.Exists(t)) DeleteTree(t);
                Directory.CreateDirectory(t); tmp[rel] = t;
                foreach (string a in asides)
                    if (Directory.Exists(Path.Combine(a, rel))) CopyNew(Path.Combine(a, rel), t);
            }
            foreach (string m in merged)
            {   // what it added to the shared saves comes back with its latest progress
                string[] p = m.Split('|');
                if (p.Length != 2) continue;
                string rel = saves.Paths.FirstOrDefault(r => p[0].StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase));
                if (rel == null) continue;
                string src = Path.Combine(SharedRoot, p[1]), dst = Path.Combine(tmp[rel], p[0].Substring(rel.Length + 1));
                if (Directory.Exists(src)) CopyNew(src, dst);
                else if (File.Exists(src) && !File.Exists(dst)) { Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(src, dst, false); }
            }
        }
        catch { foreach (string t in tmp.Values) try { DeleteTree(t); } catch { } throw; }
        List<KeyValuePair<string, string>> swapped = new List<KeyValuePair<string, string>>();   // rel -> the link it had
        try
        {
            foreach (string rel in saves.Paths)
            {
                string b = Path.Combine(u, rel);
                string target = JunctionTarget(b);
                if (target != null) RemoveJunction(b);
                else if (Directory.Exists(b)) { string aside = NewAside(k.Name + " - before going back"); SetAside(b, aside, rel, log); }
                try { Directory.Move(tmp[rel], b); }
                catch { try { if (target != null && !Directory.Exists(b)) CreateJunction(b, target); } catch { } throw; }
                swapped.Add(new KeyValuePair<string, string>(rel, target));
            }
        }
        catch
        {   // the paths already swapped get their link back, so the build stays as it was
            foreach (KeyValuePair<string, string> sw in swapped)
                try { string b = Path.Combine(u, sw.Key); Directory.Move(b, tmp[sw.Key]); if (sw.Value != null) CreateJunction(b, sw.Value); }
                catch (Exception ex2) { log("Could not put back " + sw.Key + ": " + ex2.Message); }
            foreach (string t in tmp.Values) try { if (Directory.Exists(t)) DeleteTree(t); } catch { }
            throw;
        }
        log(k.Name + ": its own saves from before sharing are back (the shared copies keep the latest).");
    }

    // Copies src into dst, leaving files dst already has as they are.
    static void CopyNew(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (string f in Directory.GetFiles(src)) { string to = Path.Combine(dst, Path.GetFileName(f)); if (!File.Exists(to)) File.Copy(f, to, false); }
        foreach (string d in Directory.GetDirectories(src)) if (!IsReparse(d)) CopyNew(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    // The items the card's share tick shares.
    public static List<Item> HostItems { get { return Items.Where(i => i.Default).ToList(); } }

    // Copies the files of an item that "to" doesn't have from another build (keys / firmware). Nothing is overwritten;
    // when "to" uses the shared folder for it, the files go there. Returns the number of files copied.
    public static long CopyMissing(EmuDef from, EmuDef to, Item it, Action<string> log)
    {
        CheckFolders(to);
        BlockIfRunning(to, it);
        long files = 0;
        foreach (string rel in it.Paths)
        {
            string src = Path.Combine(UserDir(from), rel), dst = Path.Combine(UserDir(to), rel);
            if (!HasFiles(src)) continue;
            foreach (KeyValuePair<string, string> f in Files(src))
            {
                string target = Path.Combine(dst, f.Key.TrimStart('\\'));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(f.Value, target, false);
                files++;
            }
        }
        log(to.Name + ": " + files + " " + it.Label.ToLower() + " file(s) copied from " + from.Name + ".");
        return files;
    }
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
