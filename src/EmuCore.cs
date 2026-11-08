// Emulator Manager core: release lookup (GitHub / Forgejo-Gitea), download + hash check, staged install that never
// touches a build's `user` or `metadata` folder, previous builds kept, restore. No UI here; EmuForm.cs is the window.
// C# 5 (the .NET Framework csc), so no string interpolation or ?. operators.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

public class EmuDef
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Note { get; set; }
    public string Host { get; set; }            // "github" or "gitea"
    public string ApiBase { get; set; }         // gitea only, e.g. https://git.eden-emu.dev/api/v1
    public string Repo { get; set; }            // owner/name
    public string TagPrefix { get; set; }       // only releases whose tag starts with this ("" = any)
    public List<string> Assets { get; set; }    // regexes, in order of preference
    public string Dir { get; set; }             // install folder
    public string Exe { get; set; }             // exe the Launch button starts
    public bool Added { get; set; }             // the user put it on the list (installed / existing install / new fork)
}

public class Config
{
    public List<EmuDef> Emulators { get; set; }
    public int KeepPrevious { get; set; }
    public string Theme { get; set; }           // system / light / dark
    public string GamesFolder { get; set; }     // one games folder every build lists (written into each qt-config.ini)
    public List<string> NoInstallPrompt { get; set; }   // ids the user told "don't ask again" about installing
    public bool Sandbox { get; set; }           // test environment (--sandbox DIR): shown in the title, can be reset
    public List<string> ShareNew { get; set; }  // UserData item keys new builds share from the start (null = none)
    public string ShareHost { get; set; }
    public Dictionary<string, List<string>> ShareBackups { get; set; }   // id -> Set aside folders with its own saves (this sharing), for "Go back"
    public Dictionary<string, List<string>> ShareMerged { get; set; }    // id -> "its path|shared path" of saves/DLC it added to the shared ones       // id of the emulator whose data all of them use (its card is ticked); null = no sharing
    public List<string> NoSetupReminder { get; set; }   // emulator ids whose card doesn't say "Next: add your keys / firmware"
    public string Window { get; set; }          // main window as the user left it: "x,y,width,height,maximized" (screen pixels)
}

public class Release
{
    public string Tag, Name, Body, Url, Published, Asset, AssetUrl, Sha256;   // Sha256 null when the host gives none
    public string ApiUrl;   // GitHub only: the same file through the API (a second route when the download link fails)
    public long Size;
}

public class Manifest
{
    public string Tag { get; set; }
    public string Name { get; set; }
    public string Asset { get; set; }
    public string Sha256 { get; set; }
    public bool HashFromHost { get; set; }
    public string Published { get; set; }
    public string InstalledAt { get; set; }
    public List<string> Entries { get; set; }   // top-level names this release put in the folder
    public string Repo { get; set; }            // fork it came from (owner/name); older manifests lack it
    public string Id { get; set; }              // card it belongs to (older builds are matched by it)
    public bool Chosen { get; set; }            // the user picked this (older) version by hand: Launch doesn't offer the update
}

public static class Core
{
    public const string MANIFEST = "emu-manager.json";
    public static readonly string[] PROTECTED = { "user", "metadata" };
    public static string Home;                  // folder of the exe (or of --config)
    public static string ConfigPath;
    public static Config Cfg;
    public static bool FirstRun;
    public const string MAIN_REPO = "Djihads80/Eglantine";   // Eglantine main: always listed first
    public const string MANAGER_REPO = "KaitonGxx/NS2-Emulator-Manager";   // where the manager itself is released
    public const string MANAGER_ZIP = "NS2EmulatorManager.zip";

    // This manager's version (1.2.13).
    public static Version AppVersion { get { Version v = typeof(Core).Assembly.GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); } }

    // The newest release of the manager on GitHub (tag v1.2.14 with NS2EmulatorManager.zip), or null when there's none
    // newer than this one. fresh: ask GitHub now instead of a list from the last 10 minutes.
    public static Release ManagerUpdate(bool fresh, out Version newest)
    {
        Release best = ManagerRelease(null, fresh, out newest);
        return best != null && newest > AppVersion ? best : null;
    }

    // The newest manager release (want == null) or the one with version want.
    public static Release ManagerRelease(Version want) { Version v; return ManagerRelease(want, false, out v); }
    static Release ManagerRelease(Version want, bool fresh, out Version newest)
    {
        newest = null;
        Release best = null;
        foreach (Dictionary<string, object> r in ReleasesJson("github", null, MANAGER_REPO, 1, fresh).OfType<Dictionary<string, object>>())
        {
            if (S(r, "draft") == "True" || S(r, "prerelease") == "True") continue;
            Version v;
            if (!Version.TryParse((S(r, "tag_name") ?? "").TrimStart('v', 'V'), out v)) continue;
            v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            object[] assets = r.ContainsKey("assets") ? r["assets"] as object[] : null;
            Dictionary<string, object> zip = assets == null ? null : assets.OfType<Dictionary<string, object>>().FirstOrDefault(a => string.Equals(S(a, "name"), MANAGER_ZIP, StringComparison.OrdinalIgnoreCase));
            if (zip == null || (want != null && v != want) || (newest != null && v <= newest)) continue;
            newest = v; best = MakeRelease(r, zip);
        }
        return best;
    }

    // Downloads the manager release (checked against GitHub's SHA-256), unpacks it and returns its Setup.exe.
    public static string DownloadManagerUpdate(Release r, Action<string> log, Action<long, long> progress)
    {
        EmuDef me = new EmuDef { Id = "manager-update", Name = "NS2 Emulator Manager" };
        string zip = Download(me, r, log, progress, CancellationToken.None);
        // a new folder each time: a setup from an earlier try may still be open
        string root = Path.Combine(DataDir, "manager-update"), dst = Path.Combine(root, Safe(r.Tag) + "-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
        try { foreach (string old in Directory.GetDirectories(root)) try { Directory.Delete(old, true); } catch { } } catch { }
        Directory.CreateDirectory(dst);
        Unzip(zip, dst);
        string setup = Directory.GetFiles(dst, "Setup.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (setup == null || !File.Exists(Path.Combine(Path.GetDirectoryName(setup), "EmuManager.exe")))
            throw new Exception("The manager release has no Setup.exe next to EmuManager.exe.");
        log("Manager " + r.Tag + " is ready: " + setup);
        return setup;
    }
    // Naming rule: the upstream repo is "Eglantine Main"; every other build is "<name> (fork)" (an existing
    // "(... fork)" ending is replaced, so "PokeDinner (Eglantine fork)" -> "PokeDinner (fork)").
    public static string DisplayName(EmuDef e)
    {
        if (IsMainRepo(e)) return "Eglantine Main";
        string n = Regex.Replace((e.Name ?? "").Trim(), @"\s*\([^()]*\bfork\)\s*$", "", RegexOptions.IgnoreCase).Trim();
        if (n.Length == 0) n = (e.Repo ?? "Build").Split('/').Last();
        return n + " (fork)";
    }

    public static bool IsMainRepo(EmuDef e) { return e.Host == "github" && string.Equals(e.Repo, MAIN_REPO, StringComparison.OrdinalIgnoreCase); }                // no config existed: show the welcome list
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };

    public static bool IsProtected(string name)
    {
        foreach (string p in PROTECTED) if (string.Equals(p, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ---- config
    public static void LoadConfig(string path)
    {
        ConfigPath = Path.GetFullPath(path);
        Home = Path.GetDirectoryName(ConfigPath);
        if (!File.Exists(ConfigPath))
        {   // first start (new user, or a fresh test environment): the known builds, nothing installed
            Cfg = DefaultConfig(Home);
            SaveConfig();
            FirstRun = true;
            return;
        }
        Cfg = Json.Deserialize<Config>(File.ReadAllText(ConfigPath, Encoding.UTF8));
        if (Cfg.Emulators == null) Cfg.Emulators = new List<EmuDef>();
        if (Cfg.KeepPrevious <= 0) Cfg.KeepPrevious = 2;
        if (string.IsNullOrEmpty(Cfg.Theme)) Cfg.Theme = "system";
        if (Cfg.NoInstallPrompt == null) Cfg.NoInstallPrompt = new List<string>();
        bool renamed = false;
        // 1.0.0 put every recommended build on the list at first start. Drop the ones never installed or pointed at a
        // folder (nothing in their folder, no previous builds); anything really in use stays and counts as added.
        HashSet<string> rec = new HashSet<string>(Recommended(Home).Select(r => r.Repo), StringComparer.OrdinalIgnoreCase);
        foreach (EmuDef e in Cfg.Emulators.ToList())
        {
            if (e.Added) continue;
            bool used = !string.IsNullOrEmpty(e.Dir) && (File.Exists(Path.Combine(e.Dir, e.Exe ?? "")) || LoadManifest(e.Dir) != null);
            bool backups = false;
            try { backups = Backups(e).Count > 0; } catch { }
            if (rec.Contains(e.Repo ?? "") && !used && !backups) Cfg.Emulators.Remove(e);
            else e.Added = true;
            renamed = true;
        }
        foreach (EmuDef e in Cfg.Emulators) { string n = DisplayName(e); if (n != e.Name) { e.Name = n; renamed = true; } }
        foreach (EmuDef e in Cfg.Emulators)   // 1.1.x wording of the recommended note
            if (e.Note == "Newest upstream nx2 build: try new games here first") { e.Note = "Newest version: try new games here first"; renamed = true; }
        if (renamed) try { SaveConfig(); } catch { }
        // remember every installed build, so a later reinstall of the manager can spot a second copy
        foreach (EmuDef e in Cfg.Emulators)
            if (!string.IsNullOrEmpty(e.Exe) && !string.IsNullOrEmpty(e.Dir) && File.Exists(Path.Combine(e.Dir, e.Exe)))
            { Manifest m = LoadManifest(e.Dir); RememberInstall(e, m != null ? m.Tag : null); }
    }

    // What a new user starts with: no builds (the window lists only what they add) and no games folder yet.
    public static Config DefaultConfig(string home)
    {
        Config c = new Config();
        c.KeepPrevious = 2; c.Theme = "system"; c.GamesFolder = ""; c.NoInstallPrompt = new List<string>();
        c.Emulators = new List<EmuDef>();
        return c;
    }

    // The recommended builds, offered on first start and from the Recommended button. A build joins the list (with
    // Added = true) only when the user installs it or points it at an existing install. Fresh objects every call.
    public static List<EmuDef> Recommended(string home)
    {
        string b = home;   // each build gets "<install folder>\<its name>", no extra levels
        return new List<EmuDef>
        {
            new EmuDef { Id = "eglantine", Name = "Eglantine Main", Note = "Newest version: try new games here first", Host = "github",
                         Repo = "Djihads80/Eglantine", TagPrefix = "daily-",
                         Assets = new List<string> { "^eglantine-windows-x86_64-pgo\\.zip$", "^eglantine-windows-x86_64\\.zip$" },
                         Dir = Path.Combine(b, "Eglantine Main"), Exe = "eglantine.exe" },
            new EmuDef { Id = "pokedinner", Name = "PokeDinner (fork)", Note = "Pokopia", Host = "gitea", ApiBase = "https://git.eden-emu.dev/api/v1",
                         Repo = "Franseven/PokeDinner-a-Eglantine-Fork", TagPrefix = "",
                         Assets = new List<string> { "(?i)^(?!.*(mac|linux|android|aarch64|arm64|appimage)).*win.*\\.zip$", "(?i)^(?!.*(mac|linux|android|aarch64|arm64|appimage)).*\\.zip$" },
                         Dir = Path.Combine(b, "PokeDinner (fork)"), Exe = "eglantine.exe" },
            new EmuDef { Id = "lichii", Name = "Lichii (fork)", Note = "Ocarina of Time (OOT)", Host = "github", Repo = "zortos293/Lichii", TagPrefix = "build-",
                         Assets = new List<string> { "^lichii-windows-x86_64-pgo\\.zip$" }, Dir = Path.Combine(b, "Lichii (fork)"), Exe = "lichii.exe" },
        };
    }

    // Recommended builds not on the list yet (matched by fork repo).
    public static List<EmuDef> RecommendedNotAdded()
    {
        return Recommended(Home).Where(r => !Cfg.Emulators.Any(x => string.Equals(x.Repo, r.Repo, StringComparison.OrdinalIgnoreCase) && x.Host == r.Host)).ToList();
    }

    public static void SaveConfig()
    {
        string tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, Pretty(Json.Serialize(Cfg)), new UTF8Encoding(false));
        if (File.Exists(ConfigPath)) File.Replace(tmp, ConfigPath, null); else File.Move(tmp, ConfigPath);
    }

    // Indents JavaScriptSerializer's one-line output so the file stays hand-editable.
    public static string Pretty(string json)
    {
        StringBuilder sb = new StringBuilder(); int ind = 0; bool str = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (str) { sb.Append(c); if (c == '\\') { sb.Append(json[++i]); } else if (c == '"') str = false; continue; }
            switch (c)
            {
                case '"': str = true; sb.Append(c); break;
                case '{': case '[': sb.Append(c).Append("\r\n").Append(' ', ++ind * 2); break;
                case '}': case ']': sb.Append("\r\n").Append(' ', --ind * 2).Append(c); break;
                case ',': sb.Append(c).Append("\r\n").Append(' ', ind * 2); break;
                case ':': sb.Append(": "); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // ---- HTTP
    static Core()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288;  // TLS 1.2 + 1.3
    }

    static HttpWebRequest Req(string url)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
        r.UserAgent = "Switch2-EmulatorManager/1.0";
        r.Accept = "application/json";
        r.Timeout = 20000; r.ReadWriteTimeout = 20000;   // a host that hangs is given up on after 20 s (and tried again)
        r.AllowAutoRedirect = true;
        return r;
    }

    static string GetText(string url)
    {
        try
        {
            using (WebResponse w = Req(url).GetResponse())
            using (StreamReader sr = new StreamReader(w.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }
        catch (WebException ex)
        {
            HttpWebResponse hr = ex.Response as HttpWebResponse;
            string msg = ex.Message;
            if (hr != null)
                try
                {
                    using (StreamReader sr = new StreamReader(hr.GetResponseStream()))
                    {
                        string body = sr.ReadToEnd().Trim();
                        if (body.StartsWith("{")) msg += " " + body;   // API error JSON; web pages (HTML) are left out
                    }
                }
                catch { }
            if (msg.Contains("rate limit")) msg = "GitHub's hourly limit for anonymous checks (60) is used up; try again later.";
            throw new Exception(msg.Length > 300 ? msg.Substring(0, 300) : msg, ex);   // the WebException stays inside (Retryable looks at it)
        }
    }

    // ---- releases
    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v != null ? v.ToString() : null; }

    // fresh: ask the host now instead of using a list remembered in the last 10 minutes
    public static Release Latest(EmuDef e, bool fresh = false)
    {
        // a busy repo can push a quiet channel off the first page: look at up to three pages
        for (int page = 1; page <= 3; page++)
        {
            object[] list = ReleasesJson(e.Host, e.ApiBase, e.Repo, page, fresh);
            Release best = PickLatest(e, list, true) ?? PickLatest(e, list, false);
            if (best != null) return best;
            if (list.Length < (e.Host == "gitea" ? 50 : 100)) break;   // that was the last page
        }
        throw new Exception("No release with a matching Windows asset (channel '" + e.TagPrefix + "').");
    }

    // Channel match: exact (TagChannel(tag) == prefix, so "v" doesn't take "vulkan-1", nor "nightly-" take
    // "nightly-pgo-3"); exact = false is the old starts-with rule, used only when nothing matches exactly.
    static Release PickLatest(EmuDef e, object[] list, bool exact)
    {
        List<Release> found = Matching(e, list, exact);
        return found == null ? null : found.OrderByDescending(x => ParseDate(x.Published)).FirstOrDefault();
    }

    // Every release of the repository that has a Windows zip, newest first (up to five pages of the host's list), as on
    // its releases page: all channels (daily, build, test branches...). A release whose zip names differ from e.Assets
    // (older builds carry a commit id in the name) gets the closest Windows zip. recommended: the tag Update installs
    // (Latest's rule, on the same list), or null.
    public static List<Release> AllReleases(EmuDef e, out string recommended)
    {
        List<object> raw = new List<object>();
        for (int page = 1; page <= 5; page++)
        {
            object[] list = ReleasesJson(e.Host, e.ApiBase, e.Repo, page);
            raw.AddRange(list);
            if (list.Length < (e.Host == "gitea" ? 50 : 100)) break;
        }
        object[] all = raw.ToArray();
        List<Release> chan = Matching(e, all, true);
        if ((chan == null || chan.Count == 0) && !string.IsNullOrEmpty(e.TagPrefix)) chan = Matching(e, all, false);
        Release rec = chan == null ? null : chan.OrderByDescending(x => ParseDate(x.Published)).FirstOrDefault();
        recommended = rec != null ? rec.Tag : null;
        bool pgo = e.Assets != null && e.Assets.Any(p => p.IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0 && p.IndexOf("?!", StringComparison.Ordinal) < 0);
        List<string> loose = AssetPatterns(pgo).Take(2).ToList();   // Windows zips only ("win" in the name)
        List<Release> found = new List<Release>();
        foreach (Dictionary<string, object> r in all.OfType<Dictionary<string, object>>())
        {
            if (S(r, "draft") == "True") continue;
            object[] assets = r.ContainsKey("assets") ? r["assets"] as object[] : null;
            if (assets == null) continue;
            Dictionary<string, object> pick = null;
            foreach (string pat in (e.Assets ?? new List<string>()).Concat(loose))
            {
                pick = assets.OfType<Dictionary<string, object>>().FirstOrDefault(a => Regex.IsMatch(S(a, "name") ?? "", pat));
                if (pick != null) break;
            }
            if (pick != null) found.Add(MakeRelease(r, pick));
        }
        if (found.Count == 0) throw new Exception("No release with a Windows download.");
        return found.GroupBy(r => r.Tag, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                    .OrderByDescending(x => ParseDate(x.Published)).ToList();
    }

    // The releases in list that are in e's channel and have one of e's Windows zips. null: no channel, starts-with rule
    // (doesn't apply).
    static List<Release> Matching(EmuDef e, object[] list, bool exact)
    {
        List<Release> found = new List<Release>();
        foreach (object o in list)
        {
            Dictionary<string, object> r = o as Dictionary<string, object>;
            if (r == null) continue;
            if (S(r, "draft") == "True") continue;
            string tag = S(r, "tag_name") ?? "";
            if (!string.IsNullOrEmpty(e.TagPrefix))
            {
                if (exact ? !string.Equals(TagChannel(tag), e.TagPrefix, StringComparison.OrdinalIgnoreCase)
                          : !tag.StartsWith(e.TagPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            }
            else if (!exact) return null;
            object[] assets = r.ContainsKey("assets") ? r["assets"] as object[] : null;
            if (assets == null) continue;
            Dictionary<string, object> pick = null;
            foreach (string pat in e.Assets)
            {
                foreach (object ao in assets)
                {
                    Dictionary<string, object> a = ao as Dictionary<string, object>;
                    if (a != null && Regex.IsMatch(S(a, "name") ?? "", pat)) { pick = a; break; }
                }
                if (pick != null) break;
            }
            if (pick == null) continue;
            found.Add(MakeRelease(r, pick));
        }
        return found;
    }

    static Release MakeRelease(Dictionary<string, object> r, Dictionary<string, object> asset)
    {
        Release rel = new Release();
        rel.Tag = S(r, "tag_name") ?? ""; rel.Name = S(r, "name") ?? rel.Tag; rel.Body = S(r, "body") ?? ""; rel.Url = S(r, "html_url");
        rel.Published = S(r, "published_at") ?? S(r, "created_at") ?? "";
        rel.Asset = S(asset, "name"); rel.AssetUrl = S(asset, "browser_download_url");
        string au = S(asset, "url"); if (au != null && au.StartsWith("https://api.github.com/", StringComparison.OrdinalIgnoreCase)) rel.ApiUrl = au;
        long sz; long.TryParse(S(asset, "size"), out sz); rel.Size = sz;
        string dg = S(asset, "digest");
        if (dg != null && dg.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) rel.Sha256 = dg.Substring(7).ToLowerInvariant();
        return rel;
    }

    // ---- recognising a build that is already on disk
    // A release zip's central directory (read with two small HTTP range requests, not the whole download) lists every
    // file's size and CRC-32. An exe on disk with the same size and CRC as a release's exe is that release.
    public class ZipEntryInfo { public string Name; public uint Crc; public long Size; }

    static byte[] GetRange(string url, long from, long count, out long total)
    {
        total = -1;
        for (int hop = 0; hop < 6; hop++)
        {
            HttpWebRequest q = (HttpWebRequest)WebRequest.Create(url);
            q.UserAgent = "Switch2-EmulatorManager/1.0"; q.Accept = "application/octet-stream"; q.Timeout = 20000; q.ReadWriteTimeout = 20000;
            q.AllowAutoRedirect = false;                          // follow by hand so the Range header goes along
            if (from < 0) q.AddRange(-count); else q.AddRange(from, from + count - 1);
            using (HttpWebResponse w = (HttpWebResponse)q.GetResponse())
            {
                int code = (int)w.StatusCode;
                if (code >= 300 && code < 400) { url = new Uri(new Uri(url), w.Headers["Location"]).ToString(); continue; }
                if (w.StatusCode != HttpStatusCode.PartialContent) return null;   // no range support: don't pull the whole file
                string cr = w.Headers["Content-Range"] ?? "";
                int slash = cr.LastIndexOf('/');
                if (slash >= 0) long.TryParse(cr.Substring(slash + 1), out total);
                using (Stream s = w.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[65536]; int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > count + 65536) return null; }
                    return ms.ToArray();
                }
            }
        }
        return null;
    }

    // size: the file size the host's API gave (GitHub's CDN refuses "last N bytes" ranges, so ask for exact bytes).
    public static List<ZipEntryInfo> RemoteZipDir(string url, long size)
    {
        long total;
        long want = size > 0 ? Math.Min(65536, size) : 65536;
        byte[] tail = size > 0 ? GetRange(url, size - want, want, out total) : GetRange(url, -1, want, out total);
        if (tail == null || tail.Length < 22 || total <= 0) return null;
        int eocd = -1;
        for (int i = tail.Length - 22; i >= 0; i--)
            if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 5 && tail[i + 3] == 6) { eocd = i; break; }
        if (eocd < 0) return null;
        uint cdSize = BitConverter.ToUInt32(tail, eocd + 12), cdOff = BitConverter.ToUInt32(tail, eocd + 16);
        if (cdSize == 0xFFFFFFFF || cdOff == 0xFFFFFFFF || cdSize > 8 * 1024 * 1024) return null;   // zip64 / absurd: skip
        long tailStart = total - tail.Length;
        byte[] cd; int at;
        if (cdOff >= tailStart && cdOff + cdSize <= total) { cd = tail; at = (int)(cdOff - tailStart); }
        else { long t2; cd = GetRange(url, cdOff, cdSize, out t2); at = 0; if (cd == null || cd.Length < cdSize) return null; }
        List<ZipEntryInfo> list = new List<ZipEntryInfo>();
        int end = at + (int)cdSize;
        while (at + 46 <= end && BitConverter.ToUInt32(cd, at) == 0x02014b50)
        {
            int nameLen = BitConverter.ToUInt16(cd, at + 28), extra = BitConverter.ToUInt16(cd, at + 30), comment = BitConverter.ToUInt16(cd, at + 32);
            if (at + 46 + nameLen > cd.Length) break;
            ZipEntryInfo z = new ZipEntryInfo();
            z.Crc = BitConverter.ToUInt32(cd, at + 16); z.Size = BitConverter.ToUInt32(cd, at + 24);
            z.Name = Encoding.UTF8.GetString(cd, at + 46, nameLen).Replace('\\', '/');
            list.Add(z);
            at += 46 + nameLen + extra + comment;
        }
        return list.Count > 0 ? list : null;
    }

    static uint[] crcTable;
    public static uint Crc32File(string path)
    {
        if (crcTable == null)
        {
            uint[] t = new uint[256];
            for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[i] = c; }
            crcTable = t;
        }
        uint crc = 0xFFFFFFFF;
        using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
        {
            byte[] buf = new byte[1 << 20]; int n;
            while ((n = f.Read(buf, 0, buf.Length)) > 0)
                for (int i = 0; i < n; i++) crc = crcTable[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
        }
        return ~crc;
    }

    public class Identified
    {
        public string Dir, Exe;
        public EmuDef Def;            // the card it belongs to (null: the user picks)
        public Release Rel;           // exact release, or null when not recognised
        public List<string> Entries;  // the release's top-level names (for the manifest)
        public bool Tracked;          // already has an emu-manager.json
        public bool HasUser;          // has a portable user folder (saves, keys, firmware)
        public string Why;            // how it was recognised, or why not
    }

    // A manifest counts only if it's a real one (has the downloaded asset) of this fork: Repo matches, or for older
    // manifests without Repo, its asset name fits the fork's patterns. Placeholders in previous-build folders don't.
    public static bool BelongsTo(Manifest m, EmuDef e)
    {
        if (m == null || string.IsNullOrEmpty(m.Asset)) return false;
        if (!string.IsNullOrEmpty(m.Repo)) return string.Equals(m.Repo, e.Repo, StringComparison.OrdinalIgnoreCase);
        // older manifests: the generic "*win*.zip" patterns would accept any fork's zip, so also require the zip's
        // name to carry the exe's name (lichii-windows-...zip for lichii.exe); otherwise the exe check decides
        string stem = Path.GetFileNameWithoutExtension(e.Exe ?? "");
        return stem.Length > 0 && m.Asset.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0 &&
               e.Assets != null && e.Assets.Any(p => Regex.IsMatch(m.Asset, p));
    }

    // Folders a build must never live in: the manager's own downloads / previous builds / staging, any build's
    // previous-builds folder, and anywhere inside another build (its user folder above all). In a test environment,
    // anything outside the test folder too. Returns why, or null when the folder is fine for e.
    public static string FolderProblem(EmuDef e, string dir)
    {
        string d;
        try { d = Path.GetFullPath(dir).TrimEnd('\\'); } catch { return "That folder path isn't valid."; }
        string home = Path.GetFullPath(Home).TrimEnd('\\');
        Func<string, string, bool> inside = (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase);
        if (Path.GetPathRoot(d).TrimEnd('\\') == d) return "Pick a folder of its own, not the top of a drive.";
        if (Cfg.Sandbox && !d.StartsWith(home + "\\", StringComparison.OrdinalIgnoreCase))
            return "This is the test environment: builds must stay inside " + home + ".";
        if (string.Equals(d, home, StringComparison.OrdinalIgnoreCase) || inside(home, d)) return "Pick a folder of its own for this build (not the manager's folder or one around it).";
        foreach (string own in new[] { "downloads", "previous" })
            if (inside(d, Path.Combine(home, own))) return "That's one of the manager's own folders (" + own + ").";
        if (inside(d, FullDir(DataDir))) return "That's the manager's own data folder.";
        if (inside(d, FullDir(UserData.SharedRoot))) return "That's the shared data folder.";
        if (string.Equals(Path.GetFileName(d), OLDER, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(Path.GetDirectoryName(d) ?? ""), OLDER, StringComparison.OrdinalIgnoreCase))
            return "That's an \"" + OLDER + "\" folder; pick the build's own folder.";
        if (Path.GetFileName(d).EndsWith(".staging", StringComparison.OrdinalIgnoreCase)) return "That's a temporary install folder.";
        foreach (EmuDef x in Cfg.Emulators)
        {
            if (string.IsNullOrEmpty(x.Dir)) continue;
            string xd = Path.GetFullPath(x.Dir).TrimEnd('\\');
            if (inside(d, BackupRoot(x).TrimEnd('\\'))) return "That's where " + x.Name + "'s previous builds are kept.";
            if (x == e) continue;
            if (string.Equals(d, xd, StringComparison.OrdinalIgnoreCase)) return x.Name + " already uses this folder.";
            if (inside(d, xd)) return "That's inside " + x.Name + "'s folder.";
            if (inside(xd, d)) return x.Name + " is inside that folder; pick one of its own.";
        }
        return null;
    }

    // Which release of e is the build in dir? Checks the last 40 Windows zips of the fork (any channel, PGO or not).
    public static Identified Identify(EmuDef e, string dir, Action<string> progress)
    {
        Identified id = new Identified();
        id.Dir = Path.GetFullPath(dir).TrimEnd('\\'); id.Def = e; id.Exe = e.Exe;
        id.HasUser = Directory.Exists(Path.Combine(id.Dir, "user"));
        string exe = Path.Combine(id.Dir, e.Exe ?? "");
        if (string.IsNullOrEmpty(e.Exe) || !File.Exists(exe)) { id.Why = "no " + (e.Exe ?? "exe") + " in this folder"; return id; }
        Manifest m = LoadManifest(id.Dir);
        if (BelongsTo(m, e))
        {
            id.Tracked = true; id.Rel = new Release { Tag = m.Tag, Name = m.Name, Asset = m.Asset, Published = m.Published }; id.Entries = m.Entries;
            id.Why = "already tracked (" + m.Tag + ")"; return id;
        }
        long size = new FileInfo(exe).Length;
        uint? crc = null;
        int checkedZips = 0, noRange = 0;
        object[] list = ReleasesJson(e.Host, e.ApiBase, e.Repo);
        foreach (Dictionary<string, object> r in list.OfType<Dictionary<string, object>>().OrderByDescending(x => ParseDate(S(x, "published_at") ?? S(x, "created_at"))))
        {
            if (S(r, "draft") == "True") continue;
            object[] assets = r.ContainsKey("assets") ? r["assets"] as object[] : null;
            if (assets == null) continue;
            foreach (Dictionary<string, object> a in assets.OfType<Dictionary<string, object>>())
            {
                string an = S(a, "name") ?? "";
                if (!WinZip.IsMatch(an)) continue;
                if (checkedZips >= 40) { if (id.Rel == null) id.Why = "not one of the last 40 releases (an older or self-built copy)"; return id; }
                checkedZips++;
                if (progress != null) progress("checking " + (S(r, "tag_name") ?? "") + " " + an);
                List<ZipEntryInfo> zd;
                long asz; long.TryParse(S(a, "size"), out asz);
                try { zd = RemoteZipDir(S(a, "browser_download_url"), asz); } catch { zd = null; }
                if (zd == null) { noRange++; continue; }
                ZipEntryInfo hit = zd.FirstOrDefault(z => string.Equals(z.Name, e.Exe, StringComparison.OrdinalIgnoreCase) || z.Name.EndsWith("/" + e.Exe, StringComparison.OrdinalIgnoreCase));
                if (hit == null || hit.Size != size) continue;
                if (crc == null) crc = Crc32File(exe);
                if (hit.Crc != crc.Value) continue;
                // a release can ship the same exe in a full package and an "exe-only" one: keep the fullest
                string prefix = hit.Name.Substring(0, hit.Name.Length - e.Exe.Length);   // "" or "topfolder/"
                List<string> entries = zd.Where(z => z.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && z.Name.Length > prefix.Length)
                               .Select(z => z.Name.Substring(prefix.Length).Split('/')[0]).Where(n => n.Length > 0 && !IsProtected(n))
                               .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (id.Entries == null || entries.Count > id.Entries.Count)
                {
                    id.Rel = MakeRelease(r, a); id.Entries = entries;
                    id.Why = "exe matches " + id.Rel.Tag + " (" + an + ") exactly";
                }
            }
            if (id.Rel != null) return id;
        }
        id.Why = checkedZips == 0 ? "the fork has no Windows releases to compare with"
               : noRange == checkedZips ? "the host doesn't allow partial downloads, so the version can't be checked"
               : "not one of the fork's last " + checkedZips + " Windows releases (an older or self-built copy)";
        return id;
    }

    // Adopts an identified folder as e's install: points e at it and, when the release is known exactly, writes the
    // manifest (one small file) so updates know what is installed. Nothing else in the folder changes.
    public static string Adopt(EmuDef e, Identified id)
    {
        string bad = FolderProblem(e, id.Dir);
        if (bad != null) throw new Exception(bad);
        e.Dir = id.Dir;
        string msg = e.Name + " now uses " + id.Dir;
        if (id.Rel != null && !id.Tracked && id.Entries != null)
        {
            Manifest m = new Manifest();
            m.Tag = id.Rel.Tag; m.Name = id.Rel.Name; m.Asset = id.Rel.Asset; m.Published = id.Rel.Published; m.Repo = e.Repo; m.Id = e.Id;
            m.Sha256 = id.Rel.Sha256; m.HashFromHost = id.Rel.Sha256 != null;
            m.InstalledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (found on disk, exe matched)";
            m.Entries = id.Entries;
            SaveManifest(id.Dir, m);
            WriteVersionFile(e, id.Dir, m);
            msg += " (" + m.Tag + ")";
        }
        else if (id.Rel == null) msg += " (version not recognised; Update installs the latest and keeps this one in Older builds)";
        else { Manifest t = LoadManifest(id.Dir); if (t != null) WriteVersionFile(e, id.Dir, t); }
        RememberInstall(e, id.Rel != null ? id.Rel.Tag : null);
        SaveConfig();
        return msg + ".";
    }

    // Raw release list (newest pages first) from GitHub or a Gitea / Forgejo host.
    static object[] ReleasesJson(string host, string apiBase, string repo) { return ReleasesJson(host, apiBase, repo, 1); }
    // Release lists are remembered for 10 minutes (DataDir\cache), so opening the app or a window again doesn't ask the
    // host each time (GitHub allows 60 anonymous requests an hour). fresh: ask the host anyway ("Check for updates").
    // When the host can't be reached, an older remembered list is used rather than failing.
    public static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    // progress(SLOW, 1 = GitHub / 0 = another host): a download attempt failed and is being tried again
    public const long SLOW = -1;
    static object[] ReleasesJson(string host, string apiBase, string repo, int page, bool fresh = false)
    {
        string url = host == "gitea"
            ? apiBase.TrimEnd('/') + "/repos/" + repo + "/releases?limit=50&page=" + page
            : "https://api.github.com/repos/" + repo + "/releases?per_page=100&page=" + page;
        string file = null, text = null;
        try
        {
            using (SHA1 h = SHA1.Create())
                file = Path.Combine(DataDir, "cache", BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "").ToLowerInvariant() + ".json");
            if (!fresh && File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < CacheFor) text = File.ReadAllText(file);
        }
        catch { text = null; }
        if (text == null)
        {
            try
            {
                try { text = GetText(url); }
                catch (Exception ex1) { if (!Retryable(ex1)) throw; Thread.Sleep(2000); text = GetText(url); }   // GitHub's API fails now and then: once more
                if (file != null) try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, text, new UTF8Encoding(false)); } catch { }
            }
            catch
            {
                string old = null;
                try { if (file != null && File.Exists(file)) old = File.ReadAllText(file); } catch { }
                if (old == null) throw;
                text = old;   // offline / host busy: the last list we had
            }
        }
        object[] list = Json.DeserializeObject(text) as object[];
        if (list == null) throw new Exception("Unexpected answer from " + url);
        return list;
    }

    // ---- adding a fork from a repository link
    const string NOT_OTHER_OS = "(?!.*(mac|darwin|osx|ios|linux|android|aarch64|arm64|appimage|flatpak|source))";
    public static readonly Regex WinZip = new Regex("(?i)^" + NOT_OTHER_OS + ".*\\.zip$");

    // Windows zip patterns for a fork: PGO builds or standard ones, each falling back to a plain non-PGO zip when a
    // release lacks that kind (names carrying version numbers or commit hashes match too).
    public static List<string> AssetPatterns(bool pgo)
    {
        List<string> p = new List<string>();
        if (pgo) p.Add("(?i)^" + NOT_OTHER_OS + "(?=.*pgo).*win.*\\.zip$");
        p.Add("(?i)^" + NOT_OTHER_OS.Replace("source", "source|pgo") + ".*win.*\\.zip$");
        p.Add("(?i)^" + NOT_OTHER_OS.Replace("source", "source|pgo") + ".*\\.zip$");
        return p;
    }

    public class Channel { public string Prefix, Newest, NewestDate; public int Count; }
    public class RepoInfo
    {
        public string Host, ApiBase, Repo, Name, WebUrl;
        public List<Channel> Channels = new List<Channel>();
        public List<string> Zips = new List<string>();   // Windows zips of the newest release that has any
        public string ZipsFrom;
        public bool HasPgo;
        public string DefaultPrefix = "";
    }

    // Channel of a tag: its leading word ("build-83" -> "build-", "daily-20261105" -> "daily-", "branch-fix-x-1a2b" ->
    // "branch-", "v1.2" -> "v", "2.7.7" -> "").
    // Words before the first one that starts with a digit (or "v1..."): "nx2-build-34" -> "nx2-build-". Three or more
    // words are per-branch test builds ("branch-fix-oot-blackscreen-17ac56ed"), grouped under the first word.
    public static string TagChannel(string tag)
    {
        tag = tag ?? "";
        if (Regex.IsMatch(tag, @"^[vV]\d")) return tag.Substring(0, 1);
        int pos = 0, words = 0, firstEnd = -1;
        foreach (Match w in Regex.Matches(tag, @"[^-_]+[-_]?"))
        {
            if (char.IsDigit(w.Value[0]) || !Regex.IsMatch(w.Value, "[-_]$")) break;   // version / date / hash, or the last word
            pos = w.Index + w.Length; words++;
            if (firstEnd < 0) firstEnd = pos;
        }
        if (words >= 3) return tag.Substring(0, firstEnd);
        return tag.Substring(0, pos);
    }

    // Accepts https://host/owner/repo[.git][/anything], host/owner/repo, git@host:owner/repo.git.
    public static RepoInfo Lookup(string link)
    {
        string u = (link ?? "").Trim();
        Match m = Regex.Match(u, @"^(?:git@(?<h1>[^:/\s]+):|(?:(?<sch>[a-z+]+)://)?(?:[^@/\s]+@)?(?<h2>[^/:\s]+)(?<port>:\d+)?/)(?<o>[^/\s]+)/(?<r>[^/\s#?]+)", RegexOptions.IgnoreCase);
        if (!m.Success) throw new Exception("That doesn't look like a repository link. Use one like https://github.com/owner/repo.");
        string host = (m.Groups["h1"].Success && m.Groups["h1"].Value.Length > 0 ? m.Groups["h1"].Value : m.Groups["h2"].Value).ToLowerInvariant();
        string owner = m.Groups["o"].Value, name = m.Groups["r"].Value;
        // keep an explicit http:// and a port (self-hosted Gitea / Forgejo); git@ and ssh:// links use https
        string scheme = string.Equals(m.Groups["sch"].Value, "http", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
        string port = m.Groups["h1"].Success && m.Groups["h1"].Value.Length > 0 ? "" : m.Groups["port"].Value;
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
        RepoInfo ri = new RepoInfo();
        ri.Repo = owner + "/" + name; ri.Name = name;
        if (host == "github.com" || host == "www.github.com") { ri.Host = "github"; ri.WebUrl = "https://github.com/" + ri.Repo; }
        else { ri.Host = "gitea"; ri.ApiBase = scheme + "://" + host + port + "/api/v1"; ri.WebUrl = scheme + "://" + host + port + "/" + ri.Repo; }
        object[] list;
        try { list = ReleasesJson(ri.Host, ri.ApiBase, ri.Repo); }
        catch (Exception ex)
        {
            if (ri.Host == "gitea")
                throw new Exception(host + " didn't answer like a Gitea / Forgejo server (" + ex.Message + "). GitHub, Gitea, Forgejo and Codeberg links work; GitLab isn't supported.");
            throw;
        }
        Dictionary<string, Channel> ch = new Dictionary<string, Channel>();
        DateTime newestZip = DateTime.MinValue;
        foreach (object o in list)
        {
            Dictionary<string, object> r = o as Dictionary<string, object>;
            if (r == null || S(r, "draft") == "True") continue;
            object[] assets = r.ContainsKey("assets") ? r["assets"] as object[] : null;
            if (assets == null) continue;
            List<string> zips = assets.OfType<Dictionary<string, object>>().Select(a => S(a, "name") ?? "").Where(n => WinZip.IsMatch(n)).ToList();
            if (zips.Count == 0) continue;
            if (zips.Any(n => n.IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0)) ri.HasPgo = true;
            string tag = S(r, "tag_name") ?? "", when = S(r, "published_at") ?? S(r, "created_at") ?? "";
            DateTime d = ParseDate(when);
            string p = TagChannel(tag);
            Channel c;
            if (!ch.TryGetValue(p, out c)) { c = new Channel(); c.Prefix = p; ch[p] = c; }
            c.Count++;
            if (c.Newest == null || d > ParseDate(c.NewestDate)) { c.Newest = tag; c.NewestDate = when; }
            if (d > newestZip)
            {
                newestZip = d; ri.ZipsFrom = tag;
                List<string> win = zips.Where(n => n.IndexOf("win", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                ri.Zips = win.Count > 0 ? win : zips;
            }
        }
        if (ch.Count == 0) throw new Exception(ri.Repo + " has no release with a Windows .zip to download" + (list.Length == 0 ? " (it has no releases at all)." : "."));
        ri.Channels = ch.Values.OrderByDescending(c => ParseDate(c.NewestDate)).ToList();
        // default channel: the busiest named one that isn't per-branch test builds; "" (all) when there's just one
        List<Channel> named = ri.Channels.Where(c => c.Prefix.Length > 0 && !c.Prefix.StartsWith("branch", StringComparison.OrdinalIgnoreCase)).ToList();
        if (ri.Channels.Count > 1 && named.Count > 0)
            ri.DefaultPrefix = named.OrderByDescending(c => c.Count).ThenByDescending(c => ParseDate(c.NewestDate)).First().Prefix;
        return ri;
    }

    // The emulator's own exe in an unpacked release (largest exe that isn't a CLI / room server / test / installer).
    static readonly Regex NotMainExe = new Regex(@"(?i)(cli|room|test|updater|crash|redist|unins|setup|install|helper)");
    public static string DetectExe(string dir)
    {
        return Directory.GetFiles(dir, "*.exe").Where(f => !NotMainExe.IsMatch(Path.GetFileNameWithoutExtension(f)))
            .OrderByDescending(f => new FileInfo(f).Length).Select(Path.GetFileName).FirstOrDefault();
    }

    public static DateTime ParseDate(string s)
    {
        DateTime d;
        return DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out d) ? d : DateTime.MinValue;
    }

    // ---- installed state
    public static Manifest LoadManifest(string dir)
    {
        try { string p = Path.Combine(dir, MANIFEST); return File.Exists(p) ? Json.Deserialize<Manifest>(File.ReadAllText(p)) : null; }
        catch { return null; }
    }

    static void SaveManifest(string dir, Manifest m) { File.WriteAllText(Path.Combine(dir, MANIFEST), Pretty(Json.Serialize(m)), new UTF8Encoding(false)); }

    // Text for a folder the manager did not install: VERSION.txt's first line, else the exe's date.
    public static string Describe(EmuDef e)
    {
        if (!Directory.Exists(e.Dir)) return null;
        Manifest m = LoadManifest(e.Dir);
        if (m != null) return m.Tag;
        try
        {
            string v = Path.Combine(e.Dir, "VERSION.txt");
            if (File.Exists(v)) { string l = File.ReadAllLines(v).FirstOrDefault(); if (!string.IsNullOrEmpty(l)) return l.Trim() + " (from VERSION.txt)"; }
            string x = Path.Combine(e.Dir, e.Exe);
            if (File.Exists(x)) return "unknown build, exe dated " + File.GetLastWriteTime(x).ToString("yyyy-MM-dd");
        }
        catch { }
        return "folder exists, no " + e.Exe;
    }

    // Processes running an exe from the install folder (top level).
    public static List<string> Running(EmuDef e)
    {
        List<string> hits = new List<string>();
        if (!Directory.Exists(e.Dir)) return hits;
        string root = Path.GetFullPath(e.Dir).TrimEnd('\\') + "\\";
        foreach (string exe in Directory.GetFiles(e.Dir, "*.exe"))
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
            {
                try { if (p.MainModule.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase)) hits.Add(Path.GetFileName(exe) + " (pid " + p.Id + ")"); }
                catch { hits.Add(Path.GetFileName(exe) + " (pid " + p.Id + ", path unreadable)"); }
                finally { p.Dispose(); }
            }
        return hits;
    }

    // ---- download
    public static string Sha256File(string path)
    {
        using (SHA256 h = SHA256.Create())
        using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            return BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
    }

    // ---- where things go
    // Build folders: wherever their card says (recommended ones: <install folder>\<Display name>). Older builds: one
    // "Older builds" folder next to the build folders (same drive, so moves are instant), entries named
    // "<Name> <version> (older)". Downloads, the log and the list of known installs live in the app's own data folder
    // (%LOCALAPPDATA%\Switch2EmulatorManager; inside the test folder for a test environment), never among the builds.
    public const string OLDER = "Older builds";
    public static string DataDir
    {
        get
        {
            return Cfg != null && Cfg.Sandbox ? Path.Combine(Home, ".manager-data")
                 : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Switch2EmulatorManager");
        }
    }
    static string DownloadsDir(EmuDef e) { return Path.Combine(DataDir, "downloads", e.Id ?? "build"); }

    public static string Download(EmuDef e, Release r, Action<string> log, Action<long, long> progress, CancellationToken ct)
    {
        string dir = Path.Combine(DownloadsDir(e), Safe(r.Tag));
        Directory.CreateDirectory(dir);
        string dst = Path.Combine(dir, Safe(Path.GetFileName(r.Asset ?? "")).Length > 0 ? Safe(Path.GetFileName(r.Asset)) : "release.zip");   // the host's name, never a path
        if (File.Exists(dst))
        {
            string h = Sha256File(dst);
            if ((r.Sha256 != null && h == r.Sha256) || (r.Sha256 == null && new FileInfo(dst).Length == r.Size))
            { log("Already downloaded: " + dst); return dst; }
            File.Delete(dst);
        }
        string part = dst + ".part";
        log("Downloading " + r.Asset + " (" + Mb(r.Size) + ") from " + r.AssetUrl);
        // GitHub's download link sometimes fails (502/503/504, or the connection drops): try again, then the API route
        List<string> routes = new List<string> { r.AssetUrl, r.AssetUrl };
        if (r.ApiUrl != null) { routes.Add(r.ApiUrl); routes.Add(r.ApiUrl); }
        for (int t = 0; ; t++)
        {
            ct.ThrowIfCancellationRequested();
            try { Fetch(routes[t], part, r, progress, ct); break; }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException) throw;
                bool server = Retryable(ex);
                log("Download " + (t + 1) + " failed: " + ex.Message);
                try { if (File.Exists(part)) File.Delete(part); } catch { }
                if (!server || t + 1 >= routes.Count)
                    throw new Exception(server ? "The download server isn't responding right now (" + ex.Message + ")." : ex.Message);
                if (routes[t + 1] != routes[t]) log("Trying GitHub's other download route.");
                progress(SLOW, routes[t].IndexOf("github", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0);   // the window says "... is slow, trying again"
                Thread.Sleep(2000 * (t + 1));
            }
        }
        long len = new FileInfo(part).Length;
        if (r.Size > 0 && len != r.Size) { File.Delete(part); throw new Exception("Download size " + len + " does not match the host's " + r.Size + "."); }
        string sha = Sha256File(part);
        if (r.Sha256 != null)
        {
            if (sha != r.Sha256) { File.Delete(part); throw new Exception("SHA-256 mismatch: got " + sha + ", host says " + r.Sha256 + ". Nothing was installed."); }
            log("SHA-256 matches the host's digest: " + sha);
        }
        else log("The host publishes no hash for this file. Size matches (" + len + " bytes); SHA-256 " + sha + ". The zip's own CRCs are checked when it is unpacked.");
        File.Move(part, dst);
        return dst;
    }

    // A server-side or network failure worth another try (not a 404 or a full disk).
    static bool Retryable(Exception ex)
    {
        WebException we = ex as WebException ?? ex.InnerException as WebException;
        if (we != null)
        {
            HttpWebResponse hr = we.Response as HttpWebResponse;
            if (hr != null) { int c = (int)hr.StatusCode; return c >= 500 || c == 408 || c == 429; }
            return true;   // timeout, connection reset, name lookup...
        }
        return ex is IOException && !(ex is PathTooLongException) && !ex.Message.Contains("space");
    }

    // Hosts (GitHub above all) can limit the speed of each connection: big files come in 6 pieces at once when the host
    // allows pieces (HTTP ranges); otherwise in one piece.
    const int PIECES = 6;
    static void Fetch(string url, string part, Release r, Action<long, long> progress, CancellationToken ct)
    {
        if (r.Size >= 8L << 20)
        {
            string final = null;
            try
            {
                HttpWebRequest p = Req(url); p.Accept = "application/octet-stream"; p.AddRange(0L, 0L);
                using (HttpWebResponse w = (HttpWebResponse)p.GetResponse())
                    if (w.StatusCode == HttpStatusCode.PartialContent) final = w.ResponseUri.AbsoluteUri;   // after GitHub's redirect
            }
            catch (WebException) { throw; }
            catch { final = null; }
            if (final != null) { FetchPieces(final, part, r.Size, progress, ct); return; }
        }
        FetchWhole(url, part, r, progress, ct);
    }

    static void FetchPieces(string url, string part, long size, Action<long, long> progress, CancellationToken ct)
    {
        using (FileStream f = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) f.SetLength(size);
        long chunk = (size + PIECES - 1) / PIECES;
        long[] done = new long[1];
        Exception[] errs = new Exception[PIECES];
        List<Thread> ts = new List<Thread>();
        for (int i = 0; i < PIECES; i++)
        {
            int k = i;
            Thread t = new Thread(delegate ()
            {
                long pos = k * chunk, to = Math.Min(size, (k + 1) * chunk) - 1;
                int tries = 0;
                while (pos <= to && !ct.IsCancellationRequested)
                {
                    try
                    {
                        HttpWebRequest q = Req(url); q.Accept = "application/octet-stream"; q.AddRange(pos, to);
                        using (HttpWebResponse w = (HttpWebResponse)q.GetResponse())
                        {
                            if (w.StatusCode != HttpStatusCode.PartialContent) throw new IOException("The host sent the whole file instead of a piece.");
                            using (Stream s = w.GetResponseStream())
                            using (FileStream f = new FileStream(part, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1 << 16))
                            {
                                f.Seek(pos, SeekOrigin.Begin);
                                byte[] buf = new byte[1 << 16]; int n;
                                while (pos <= to && (n = s.Read(buf, 0, (int)Math.Min(buf.Length, to - pos + 1))) > 0)
                                {
                                    if (ct.IsCancellationRequested) return;
                                    f.Write(buf, 0, n); pos += n; Interlocked.Add(ref done[0], n);
                                }
                            }
                        }
                        if (pos <= to) throw new IOException("The connection closed early.");
                    }
                    catch (Exception ex)
                    {
                        if (++tries > 4) { errs[k] = ex; return; }   // the whole download is tried again (fresh link) by Download
                        Thread.Sleep(1500 * tries);
                    }
                }
            });
            t.IsBackground = true; ts.Add(t); t.Start();
        }
        while (ts.Any(t => t.IsAlive))
        {
            Thread.Sleep(200);
            progress(Interlocked.Read(ref done[0]), size);
        }
        ct.ThrowIfCancellationRequested();
        Exception first = errs.FirstOrDefault(x => x != null);
        if (first != null) throw first;
        progress(size, size);
    }

    static void FetchWhole(string url, string part, Release r, Action<long, long> progress, CancellationToken ct)
    {
        HttpWebRequest q = Req(url); q.Accept = "application/octet-stream";
        long got = 0;
        using (WebResponse w = q.GetResponse())
        using (Stream s = w.GetResponseStream())
        using (FileStream f = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        {
            byte[] buf = new byte[1 << 18]; long total = r.Size > 0 ? r.Size : w.ContentLength; int n;
            DateTime last = DateTime.MinValue;
            while ((n = s.Read(buf, 0, buf.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                f.Write(buf, 0, n); got += n;
                if ((DateTime.Now - last).TotalMilliseconds > 150) { progress(got, total); last = DateTime.Now; }
            }
            progress(got, total);
        }
        // the connection closed before the whole file came: worth another try
        if (r.Size > 0 && got < r.Size) throw new IOException("The connection closed early (" + got + " of " + r.Size + " bytes).");
    }

    // ---- install / update / restore
    static string Safe(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s; }
    public static string Mb(long b) { return (b / 1048576.0).ToString("0.0") + " MB"; }

    // The "Older builds" folder for e: next to e's folder.
    public static string BackupRoot(EmuDef e)
    {
        string full = Path.GetFullPath(e.Dir).TrimEnd('\\');
        string parent = Path.GetDirectoryName(full);
        return Path.Combine(parent ?? full, OLDER);
    }

    // e's older builds, newest first: entries of its Older builds folder whose manifest names e (by id, or by fork for
    // entries without one), plus folders left by older manager versions (<home>\previous\<id>\*, "<dir> (previous builds)").
    public static List<string> Backups(EmuDef e)
    {
        List<string> all = new List<string>();
        string live = string.IsNullOrEmpty(e.Dir) ? "" : Path.GetFullPath(e.Dir).TrimEnd('\\');
        try
        {
            string root = BackupRoot(e);
            if (Directory.Exists(root))
                foreach (string d in Directory.GetDirectories(root))
                {
                    Manifest m = LoadManifest(d);
                    if (m == null) continue;
                    bool mine = !string.IsNullOrEmpty(m.Id) ? m.Id == e.Id
                              : !string.IsNullOrEmpty(m.Repo) ? string.Equals(m.Repo, e.Repo, StringComparison.OrdinalIgnoreCase)
                              : Path.GetFileName(d).StartsWith(Stem(e) + " ", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(e.Exe) && File.Exists(Path.Combine(d, e.Exe));
                    if (mine) all.Add(d);
                }
        }
        catch { }
        foreach (string legacy in new[] { Path.Combine(Home, "previous", e.Id ?? "-"), live + " (previous builds)" })
            try { if (Directory.Exists(legacy)) all.AddRange(Directory.GetDirectories(legacy)); } catch { }
        return all.Where(d => !string.Equals(Path.GetFullPath(d).TrimEnd('\\'), live, StringComparison.OrdinalIgnoreCase))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .OrderByDescending(d => Directory.GetCreationTimeUtc(d)).ThenByDescending(d => d).ToList();
    }

    // "PokeDinner (fork) 1.5.0 (older)", made unique with " 2", " 3"... if that version is kept twice.
    public static string Stem(EmuDef e) { return Regex.Replace(e.Name ?? "", @"\s*\((fork|[^()]*\bfork)\)\s*$", "", RegexOptions.IgnoreCase).Trim(); }
    // A version label short enough for a folder name: the tag itself, or the version number inside a long description
    // ("Eglantine - PokeDinner fork by Franseven 1.5.0 (Windows x64...)" -> "1.5.0").
    static string ShortVersion(string label)
    {
        label = (label ?? "").Trim();
        if (label.Length <= 30) return label;
        Match v = Regex.Match(label, @"\bv?\d+(\.\d+)+[a-z]?\b");
        return v.Success ? v.Value : label.Substring(0, 30).Trim();
    }

    static string OlderFolder(EmuDef e, string version)
    {
        string baseName = Safe(Stem(e) + " " + ShortVersion(version) + " (older)").Trim();
        string root = BackupRoot(e), p = Path.Combine(root, baseName);
        for (int i = 2; Exists(p); i++) p = Path.Combine(root, baseName + " " + i);
        return p;
    }

    // One small file per build folder whose name says the version, so it shows at a glance in Explorer.
    public const string VERSION_PREFIX = "VERSION - ";
    static bool IsVersionFile(string name) { return name.StartsWith(VERSION_PREFIX, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase); }
    public static void WriteVersionFile(EmuDef e, string dir, Manifest m)
    {
        try
        {
            foreach (string f in Directory.GetFiles(dir, VERSION_PREFIX + "*.txt")) File.Delete(f);
            string body = e.Name + "\r\nVersion: " + m.Tag + (m.Name != null && m.Name != m.Tag ? " (" + m.Name + ")" : "") +
                          "\r\nInstalled: " + (m.InstalledAt ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm")) +
                          "\r\nFrom: " + (m.Asset ?? "") + (m.Repo != null ? " (" + m.Repo + ")" : "") +
                          "\r\n\r\nWritten by the NS2 Emulator Manager; it is replaced when the build is updated.\r\n";
            File.WriteAllText(Path.Combine(dir, VERSION_PREFIX + Safe(m.Tag) + ".txt"), body, new UTF8Encoding(false));
        }
        catch { }
    }

    static void Unzip(string zip, string dst)
    {
        string tar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");  // bsdtar: checks CRCs, refuses ../ paths
        ProcessStartInfo si = new ProcessStartInfo(tar, "-xf \"" + zip + "\" -C \"" + dst + "\"");
        si.UseShellExecute = false; si.CreateNoWindow = true; si.RedirectStandardError = true; si.RedirectStandardOutput = true;
        using (Process p = Process.Start(si))
        {
            string err = p.StandardError.ReadToEnd(); p.StandardOutput.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception("Unpacking failed (tar exit " + p.ExitCode + "): " + err.Trim());
        }
    }

    static bool IsDir(string p) { return Directory.Exists(p); }
    static bool Exists(string p) { return File.Exists(p) || Directory.Exists(p); }
    static void Move(string a, string b) { if (IsDir(a)) Directory.Move(a, b); else File.Move(a, b); }

    static string HiddenSibling(string full, string what)
    {
        string p = Path.Combine(Path.GetDirectoryName(full), "." + Path.GetFileName(full) + "." + what);
        if (Directory.Exists(p)) Directory.Delete(p, true);
        DirectoryInfo di = Directory.CreateDirectory(p);
        try { di.Attributes |= FileAttributes.Hidden; } catch { }
        return p;
    }

    // want: the release the user picked from the list; null = the latest. chosen: it isn't the newest (Launch then
    // doesn't offer the update).
    public static void InstallOrUpdate(EmuDef e, Action<string> log, Action<long, long> progress, CancellationToken ct, Release want = null, bool chosen = false)
    {
        string bad = FolderProblem(e, e.Dir);
        if (bad != null) throw new Exception("Not installing into " + e.Dir + ": " + bad);
        List<string> run = Running(e);
        if (run.Count > 0) throw new Exception("Close the emulator first: " + string.Join(", ", run));
        Release r = want ?? Latest(e);
        log(e.Name + ": " + (want != null ? "chosen version is " : "latest is ") + r.Tag + " (" + r.Name + ", " + r.Published + ")");
        string full = Path.GetFullPath(e.Dir).TrimEnd('\\');

        // the folder already holds this very release: nothing to download, move or keep
        Manifest cur = LoadManifest(full);
        if (BelongsTo(cur, e) && cur.Tag == r.Tag && !string.IsNullOrEmpty(e.Exe) && File.Exists(Path.Combine(full, e.Exe)))
        {
            if (cur.Chosen != chosen) { cur.Chosen = chosen; SaveManifest(full, cur); }   // picked by hand, or back on the latest
            WriteVersionFile(e, full, cur);
            RememberInstall(e, cur.Tag);
            log(e.Name + " in " + full + " is already " + r.Tag + "; nothing to do.");
            return;
        }

        string zip = Download(e, r, log, progress, ct);
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        string staging = HiddenSibling(full, "staging");
        try
        {
            log("Unpacking into " + staging);
            Unzip(zip, staging);
            string src = staging;
            string[] dirs = Directory.GetDirectories(src), files = Directory.GetFiles(src);
            if (files.Length == 0 && dirs.Length == 1 && (string.IsNullOrEmpty(e.Exe) || !File.Exists(Path.Combine(src, e.Exe)))) src = dirs[0];  // zip with one top folder
            if (string.IsNullOrEmpty(e.Exe))
            {   // fork added from a link: the exe name is known only now
                string exe = DetectExe(src);
                if (exe == null) throw new Exception("The release has no emulator .exe at its top level, so nothing was installed.");
                e.Exe = exe; log("Found the emulator exe: " + exe);
                try { SaveConfig(); } catch (Exception ex) { log("Could not save the exe name to the config: " + ex.Message); }
            }
            if (!File.Exists(Path.Combine(src, e.Exe))) throw new Exception("The release has no " + e.Exe + " at its top level; its layout changed, so nothing was installed.");
            foreach (string p in PROTECTED)
                if (Exists(Path.Combine(src, p))) { log("The release zip has its own '" + p + "' folder; it is ignored (yours is kept)."); DeleteAny(Path.Combine(src, p)); }
            if (File.Exists(Path.Combine(src, MANIFEST))) File.Delete(Path.Combine(src, MANIFEST));

            Manifest m = new Manifest();
            m.Tag = r.Tag; m.Name = r.Name; m.Asset = r.Asset; m.Published = r.Published; m.Repo = e.Repo; m.Id = e.Id;
            m.Sha256 = r.Sha256 ?? Sha256File(zip); m.HashFromHost = r.Sha256 != null;
            m.Chosen = chosen;
            run = Running(e);
            if (run.Count > 0) throw new Exception("Close the emulator first: " + string.Join(", ", run));
            bool fresh = !Directory.Exists(full) || !File.Exists(Path.Combine(full, e.Exe));
            Apply(e, src, m, log);
            if (fresh && !Directory.Exists(Path.Combine(full, "user")))
            {
                Directory.CreateDirectory(Path.Combine(full, "user"));
                log("Made an empty 'user' folder so this build runs portable (its own saves, keys and firmware) and leaves %APPDATA%\\eden alone.");
                SeedGamesIni(e, log);
                UserData.ShareForNewBuild(e, log);
            }
        }
        finally { try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { } }

        // the zip was verified and installed: it isn't needed any more
        try { Directory.Delete(DownloadsDir(e), true); } catch { }
        RememberInstall(e, r.Tag);
        Prune(e, log);
        log(e.Name + " is now " + r.Tag + ".");
    }

    static void DeleteAny(string p) { if (IsDir(p)) Directory.Delete(p, true); else File.Delete(p); }

    // Moves the release's entries from src into the build folder. When the version changes, what they replace (and what
    // the previous release put there but this one doesn't ship) goes to "Older builds\<Name> <old version> (older)"
    // first. The same version is never kept twice: its files are simply replaced. user / metadata are never moved;
    // everything else in the folder (your own files) stays. Any failure puts every moved entry back.
    static void Apply(EmuDef e, string src, Manifest m, Action<string> log)
    {
        string dir = Path.GetFullPath(e.Dir).TrimEnd('\\');
        Directory.CreateDirectory(dir);
        Manifest old = LoadManifest(dir);
        string oldLabel = old != null && !string.IsNullOrEmpty(old.Tag) ? old.Tag : (Describe(e) ?? "none");
        if (old == null) oldLabel = Regex.Replace(oldLabel, @"\s*\(from VERSION\.txt\)$", "");
        bool sameVersion = old != null && old.Tag == m.Tag && (string.IsNullOrEmpty(old.Repo) || string.Equals(old.Repo, m.Repo, StringComparison.OrdinalIgnoreCase));
        List<string> incoming = Directory.GetFileSystemEntries(src).Select(Path.GetFileName)
            .Where(n => !IsProtected(n) && !string.Equals(n, MANIFEST, StringComparison.OrdinalIgnoreCase) && !IsVersionFile(n)).ToList();
        HashSet<string> outgoing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string n in incoming) if (Exists(Path.Combine(dir, n))) outgoing.Add(n);
        if (old != null && old.Entries != null)
            foreach (string n in old.Entries) if (!IsProtected(n) && Exists(Path.Combine(dir, n))) outgoing.Add(n);
        foreach (string f in Directory.GetFiles(dir, VERSION_PREFIX + "*.txt")) outgoing.Add(Path.GetFileName(f));   // the old version's label goes with it
        outgoing.Remove(MANIFEST);

        string bk = null;
        if (outgoing.Count > 0 || File.Exists(Path.Combine(dir, MANIFEST)))
        {
            if (sameVersion) bk = HiddenSibling(dir, "replaced");        // same version: thrown away after the swap
            else { bk = OlderFolder(e, oldLabel); Directory.CreateDirectory(bk); }
        }
        List<KeyValuePair<string, string>> done = new List<KeyValuePair<string, string>>();
        try
        {
            foreach (string n in outgoing)
            {
                if (IsProtected(n)) continue;
                Move(Path.Combine(dir, n), Path.Combine(bk, n)); done.Add(new KeyValuePair<string, string>(Path.Combine(dir, n), Path.Combine(bk, n)));
            }
            if (File.Exists(Path.Combine(dir, MANIFEST)))
            { Move(Path.Combine(dir, MANIFEST), Path.Combine(bk, MANIFEST)); done.Add(new KeyValuePair<string, string>(Path.Combine(dir, MANIFEST), Path.Combine(bk, MANIFEST))); }
            if (bk != null && !sameVersion)
            {   // what the build was, for Restore, and which card it belongs to
                Manifest prev = old ?? new Manifest { Tag = oldLabel, Name = oldLabel, Entries = outgoing.Where(n => !IsVersionFile(n)).ToList(), InstalledAt = "before Emulator Manager" };
                prev.Id = e.Id; if (string.IsNullOrEmpty(prev.Repo)) prev.Repo = e.Repo;
                SaveManifest(bk, prev);
            }
            foreach (string n in incoming)
            {
                Move(Path.Combine(src, n), Path.Combine(dir, n)); done.Add(new KeyValuePair<string, string>(Path.Combine(src, n), Path.Combine(dir, n)));
            }
            m.Entries = incoming; m.Id = e.Id; m.InstalledAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveManifest(dir, m);
        }
        catch (Exception ex)
        {
            log("Install failed (" + ex.Message + "); putting the previous build back...");
            for (int i = done.Count - 1; i >= 0; i--)
                try { Move(done[i].Value, done[i].Key); } catch (Exception ex2) { log("  could not move back " + done[i].Value + ": " + ex2.Message); }
            try { if (File.Exists(Path.Combine(dir, MANIFEST)) && old == null) File.Delete(Path.Combine(dir, MANIFEST)); } catch { }
            try { if (bk != null && Directory.GetFileSystemEntries(bk).All(p => Path.GetFileName(p) == MANIFEST)) Directory.Delete(bk, true); } catch { }
            throw;
        }
        WriteVersionFile(e, dir, m);
        if (bk != null && sameVersion) { try { Directory.Delete(bk, true); } catch { } log("Same version as before (" + m.Tag + "): its files were replaced, nothing kept twice."); }
        else if (bk != null) log("Older build (" + oldLabel + ") kept in " + bk);
        List<string> kept = Directory.GetFileSystemEntries(dir).Select(Path.GetFileName)
            .Where(n => !incoming.Contains(n, StringComparer.OrdinalIgnoreCase) && !string.Equals(n, MANIFEST, StringComparison.OrdinalIgnoreCase) && !IsVersionFile(n)).ToList();
        if (kept.Count > 0) log("Left as they were: " + string.Join(", ", kept));
    }

    public static void Restore(EmuDef e, Action<string> log)
    {
        string bad = FolderProblem(e, e.Dir);
        if (bad != null) throw new Exception("Not restoring into " + e.Dir + ": " + bad);
        List<string> run = Running(e);
        if (run.Count > 0) throw new Exception("Close the emulator first: " + string.Join(", ", run));
        List<string> b = Backups(e);
        if (b.Count == 0) throw new Exception("No older build kept for " + e.Name + ".");
        string src = b[0];
        Manifest m = LoadManifest(src) ?? new Manifest { Tag = Path.GetFileName(src), Name = Path.GetFileName(src) };
        if (!File.Exists(Path.Combine(src, e.Exe))) throw new Exception(src + " has no " + e.Exe + "; not restoring it.");
        log("Restoring " + m.Tag + " from " + src);
        string restoredAt = m.InstalledAt;
        Apply(e, src, m, log);
        if (restoredAt == "before Emulator Manager")
        {   // that build had no manifest; keep it that way so the window shows its own description again
            try { File.Delete(Path.Combine(Path.GetFullPath(e.Dir), MANIFEST)); } catch { }
        }
        try { if (Directory.GetFileSystemEntries(src).All(p => Path.GetFileName(p) == MANIFEST)) Directory.Delete(src, true); } catch { }
        RememberInstall(e, m.Tag);
        DropSameVersionOlder(e, m.Tag, log);
        log(e.Name + " is back on " + m.Tag + ".");
    }

    // An older build of the very version now installed is a duplicate (e.g. after Restore older then Update): drop it.
    static void DropSameVersionOlder(EmuDef e, string tag, Action<string> log)
    {
        foreach (string d in Backups(e))
        {
            Manifest m = LoadManifest(d);
            if (m == null || m.Tag != tag || PROTECTED.Any(p => Exists(Path.Combine(d, p)))) continue;
            try { Directory.Delete(d, true); log("Removed " + Path.GetFileName(d) + ": it's the same version as the one installed."); } catch { }
        }
    }

    static void Prune(EmuDef e, Action<string> log)
    {
        DropSameVersionOlder(e, (LoadManifest(e.Dir) ?? new Manifest()).Tag, log);
        List<string> b = Backups(e);
        for (int i = Cfg.KeepPrevious; i < b.Count; i++)
        {
            // older-build folders only ever hold release files (user / metadata are never moved), so they can go
            if (PROTECTED.Any(p => Exists(Path.Combine(b[i], p)))) { log("Kept " + b[i] + ": it has a protected folder in it."); continue; }
            try { Directory.Delete(b[i], true); log("Removed old build " + Path.GetFileName(b[i])); } catch (Exception ex) { log("Could not remove " + b[i] + ": " + ex.Message); }
        }
    }

    // ---- installs the app knows about (DataDir\installs.json, survives reinstalls of the manager), to spot the same
    // emulator installed twice
    public class KnownInstall { public string Id { get; set; } public string Repo { get; set; } public string Host { get; set; } public string Dir { get; set; } public string Tag { get; set; } public string When { get; set; } }
    static string KnownFile { get { return Path.Combine(DataDir, "installs.json"); } }
    static List<KnownInstall> LoadKnown()
    {
        try { return File.Exists(KnownFile) ? (Json.Deserialize<List<KnownInstall>>(File.ReadAllText(KnownFile)) ?? new List<KnownInstall>()) : new List<KnownInstall>(); }
        catch { return new List<KnownInstall>(); }
    }
    static void SaveKnown(List<KnownInstall> k)
    {
        try { Directory.CreateDirectory(DataDir); File.WriteAllText(KnownFile, Pretty(Json.Serialize(k)), new UTF8Encoding(false)); } catch { }
    }
    static string FullDir(string d) { try { return Path.GetFullPath(d).TrimEnd('\\'); } catch { return d ?? ""; } }

    public static void RememberInstall(EmuDef e, string tag)
    {
        if (string.IsNullOrEmpty(e.Dir)) return;
        List<KnownInstall> k = LoadKnown();
        string d = FullDir(e.Dir);
        k.RemoveAll(x => string.Equals(FullDir(x.Dir), d, StringComparison.OrdinalIgnoreCase));
        k.Add(new KnownInstall { Id = e.Id, Repo = e.Repo, Host = e.Host, Dir = d, Tag = tag, When = DateTime.Now.ToString("yyyy-MM-dd HH:mm") });
        SaveKnown(k);
    }

    public static void ForgetInstall(string dir)
    {
        List<KnownInstall> k = LoadKnown();
        string d = FullDir(dir);
        if (k.RemoveAll(x => string.Equals(FullDir(x.Dir), d, StringComparison.OrdinalIgnoreCase)) > 0) SaveKnown(k);
    }

    // Other installs of e's emulator that still exist: another card of the same fork, or a folder the app installed or
    // adopted before (also before a reinstall of the manager). Key = folder, value = version.
    public static List<KeyValuePair<string, string>> OtherInstalls(EmuDef e)
    {
        List<KeyValuePair<string, string>> o = new List<KeyValuePair<string, string>>();
        string mine = string.IsNullOrEmpty(e.Dir) ? "" : FullDir(e.Dir);
        Func<string, bool> seen = d => string.Equals(d, mine, StringComparison.OrdinalIgnoreCase) || o.Any(p => string.Equals(p.Key, d, StringComparison.OrdinalIgnoreCase));
        foreach (EmuDef x in Cfg.Emulators)
        {
            if (x == e || x.Host != e.Host || !string.Equals(x.Repo, e.Repo, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(x.Dir)) continue;
            string d = FullDir(x.Dir);
            if (!seen(d) && !string.IsNullOrEmpty(x.Exe) && File.Exists(Path.Combine(d, x.Exe))) o.Add(new KeyValuePair<string, string>(d, Describe(x) ?? "?"));
        }
        foreach (KnownInstall k in LoadKnown())
        {
            if (k.Host != e.Host || !string.Equals(k.Repo, e.Repo, StringComparison.OrdinalIgnoreCase)) continue;
            string d = FullDir(k.Dir);
            if (seen(d) || string.IsNullOrEmpty(e.Exe) || !File.Exists(Path.Combine(d, e.Exe))) continue;
            Manifest m = LoadManifest(d);
            if (m != null && !BelongsTo(m, e)) continue;
            o.Add(new KeyValuePair<string, string>(d, m != null ? m.Tag : (k.Tag ?? "?")));
        }
        return o;
    }

    // Ids already used by a card, a download folder or an older build (a re-added fork must not pick up another's).
    public static bool IdTaken(string id)
    {
        if (Cfg.Emulators.Any(x => x.Id == id)) return true;
        if (Directory.Exists(Path.Combine(DataDir, "downloads", id)) || Directory.Exists(Path.Combine(Home, "previous", id))) return true;
        return LoadKnown().Any(k => k.Id == id);
    }

    // Other Eden-family emulator exes (for the "another emulator is running" warning).
    public static readonly string[] FamilyExes = { "eden.exe", "yuzu.exe", "citron.exe", "sudachi.exe", "suyu.exe", "torzu.exe", "eglantine.exe", "lichii.exe" };

    public static Process Launch(EmuDef e)
    {
        string exe = Path.Combine(e.Dir, e.Exe);
        if (!File.Exists(exe)) throw new Exception(exe + " not found.");
        ProcessStartInfo si = new ProcessStartInfo(exe); si.WorkingDirectory = e.Dir; si.UseShellExecute = true;
        return Process.Start(si);
    }

    // ---- shared games folder: the build's own game list (user\config\qt-config.ini, [UI] Paths\gamedirs\N).
    // Only the gamedirs keys are changed; a copy of the file is kept once as qt-config.ini.bak-emu-manager.
    public static string GamesIni(EmuDef e) { return Path.Combine(e.Dir, "user", "config", "qt-config.ini"); }
    static string Norm(string p)
    {
        if (string.IsNullOrEmpty(p)) return null;
        string f = Path.GetFullPath(p);
        f = Path.GetPathRoot(f) == f ? f : f.TrimEnd('\\');   // "D:\" stays "D:/" (a bare "D:" means the drive's current folder)
        return f.Replace('\\', '/');
    }
    static readonly Regex GameDir = new Regex(@"^Paths\\gamedirs\\(\d+)\\path=(.*)$");
    const string GD = "Paths\\gamedirs\\";

    static string[] DirLines(int k, string path, bool deep)
    {
        string b = GD + k + "\\";
        return new[] { b + "path=" + path, b + "deep_scan\\default=" + (deep ? "false" : "true"), b + "deep_scan=" + (deep ? "true" : "false"),
                       b + "expanded\\default=true", b + "expanded=true" };
    }

    // New install: a qt-config.ini holding only the game list (the three built-in entries plus the games folder), so
    // the first start already lists the games instead of asking for a folder. Everything else stays at its default.
    static void SeedGamesIni(EmuDef e, Action<string> log)
    {
        string want = Norm(Cfg.GamesFolder), ini = GamesIni(e);
        if (want == null || File.Exists(ini)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ini));
        List<string> L = new List<string> { "[UI]", GD + "size=4" };
        L.AddRange(DirLines(1, "SDMC", false)); L.AddRange(DirLines(2, "UserNAND", false)); L.AddRange(DirLines(3, "SysNAND", false));
        L.AddRange(DirLines(4, want, true));
        File.WriteAllText(ini, string.Join("\n", L) + "\n", new UTF8Encoding(false));
        log("Set its game folder to " + want + " (new qt-config.ini holding only the game list).");
    }

    // Makes sure the build lists the games folder (deep scan on). Replaces the entry of the previous shared folder if
    // there is one, else adds an entry; other folders the user added stay. Returns a message, or null if nothing changed.
    public static string SyncGamesFolder(EmuDef e, string previous)
    {
        string want = Norm(Cfg.GamesFolder), prev = Norm(previous), ini = GamesIni(e);
        if (want == null || string.IsNullOrEmpty(e.Exe) || !File.Exists(Path.Combine(e.Dir, e.Exe)) || !Directory.Exists(Path.Combine(e.Dir, "user"))) return null;
        if (Cfg.Sandbox && FolderProblem(e, e.Dir) != null) return null;   // test environment: never edit a build outside it
        if (Running(e).Count > 0) return e.Name + " is running; close it, then press Apply to all builds again.";
        if (!File.Exists(ini)) { SeedGamesIni(e, s => { }); return e.Name + ": game folder set to " + want + " (new config)."; }
        string raw = File.ReadAllText(ini, Encoding.UTF8);
        string nl = raw.Contains("\r\n") ? "\r\n" : "\n";
        List<string> L = raw.Replace("\r\n", "\n").Split('\n').ToList();
        bool trailing = L.Count > 0 && L[L.Count - 1] == "";
        if (trailing) L.RemoveAt(L.Count - 1);
        int sizeAt = L.FindIndex(l => l.StartsWith(GD + "size="));
        if (sizeAt < 0)
        {   // no game list yet: add one to [UI] (or a new [UI] section)
            int ui = L.FindIndex(l => l.Trim() == "[UI]");
            List<string> add = new List<string> { GD + "size=4" };
            add.AddRange(DirLines(1, "SDMC", false)); add.AddRange(DirLines(2, "UserNAND", false)); add.AddRange(DirLines(3, "SysNAND", false));
            add.AddRange(DirLines(4, want, true));
            if (ui < 0) { L.Add("[UI]"); L.AddRange(add); } else L.InsertRange(ui + 1, add);
            return Write(e, ini, L, nl, trailing, e.Name + ": game list created with " + want + ".");
        }
        int size; int.TryParse(L[sizeAt].Substring(L[sizeAt].IndexOf('=') + 1), out size);
        int hit = -1, prevHit = -1;
        for (int i = 0; i < L.Count; i++)
        {
            Match m = GameDir.Match(L[i]);
            if (!m.Success) continue;
            int k = int.Parse(m.Groups[1].Value);
            if (k > size) continue;
            string p = m.Groups[2].Value.Trim().TrimEnd('/');
            if (string.Equals(p, want, StringComparison.OrdinalIgnoreCase)) hit = k;
            else if (prev != null && string.Equals(p, prev, StringComparison.OrdinalIgnoreCase)) prevHit = k;
            else if (prevHit < 0 && p.StartsWith(want + "/", StringComparison.OrdinalIgnoreCase))
            { prevHit = k; prev = p; }   // a subfolder of the games folder: widen it instead of listing its games twice
        }
        bool changed = false;
        string msg = e.Name + ": game folder " + want + " added to its game list.";
        if (hit < 0 && prevHit > 0)
        {
            int at = L.FindIndex(l => l.StartsWith(GD + prevHit + "\\path="));
            L[at] = GD + prevHit + "\\path=" + want; hit = prevHit; changed = true;
            msg = e.Name + ": game folder changed from " + prev + " to " + want + ".";
        }
        if (hit < 0)
        {
            int k = size + 1;
            L.RemoveAll(l => l.StartsWith(GD + k + "\\"));       // stale unused slot
            int last = L.FindLastIndex(l => l.StartsWith(GD));
            L.InsertRange(last + 1, DirLines(k, want, true));
            sizeAt = L.FindIndex(l => l.StartsWith(GD + "size="));
            L[sizeAt] = GD + "size=" + k; hit = k; changed = true;
        }
        int ds = L.FindIndex(l => l == GD + hit + "\\deep_scan=false");
        if (ds >= 0)
        {   // Qt only reads the value when its "\default" flag is false
            L[ds] = GD + hit + "\\deep_scan=true"; changed = true;
            int dd = L.FindIndex(l => l == GD + hit + "\\deep_scan\\default=true");
            if (dd >= 0) L[dd] = GD + hit + "\\deep_scan\\default=false";
        }
        return changed ? Write(e, ini, L, nl, trailing, msg) : null;
    }

    static string Write(EmuDef e, string ini, List<string> L, string nl, bool trailing, string msg)
    {
        string bak = ini + ".bak-emu-manager";
        if (!File.Exists(bak)) File.Copy(ini, bak);
        string tmp = ini + ".tmp-emu-manager";
        File.WriteAllText(tmp, string.Join(nl, L) + (trailing ? nl : ""), new UTF8Encoding(false));
        File.Replace(tmp, ini, null);
        return msg;
    }
}
