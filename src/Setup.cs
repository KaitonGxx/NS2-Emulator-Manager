// NS2 Emulator Manager setup / uninstaller: its own small exe (Setup.exe), built with /main:SetupApp, shipped in
// a zip next to the app (EmuManager.exe). Setup copies that file in; it never carries the app inside itself (antivirus
// models call that a dropper), and the app never installs itself (an exe that copies itself, adds launchers and then
// downloads is what they call a trojan). Uninstall.exe in the install folder is a copy of Setup.exe.
// Per-user install, no admin: files go to the chosen folder, shortcuts to the user's Desktop / Start menu, and an
// Apps & features entry under HKCU. The uninstaller removes only what setup put there (and the settings if asked);
// emulator builds, their saves and downloads are never deleted.
//   Setup.exe                         install (or update an existing install)
//   Setup.exe --uninstall             uninstall (what the Apps & features entry and Uninstall.exe run)
//   Setup.exe --test-install DIR      silent install into DIR: no shortcuts, no registry (for testing)
//   Setup.exe --test-uninstall DIR    silent uninstall from DIR (testing)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

static class SetupApp
{
    public const string APP = "NS2 Emulator Manager", VERSION = "1.2.11", PUBLISHER = "NS2 Emulator Manager";
    public const string KEY = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Switch2EmulatorManager";
    public static string DesktopLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NS2 Emulator Manager.lnk"); } }
    public static string StartLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "NS2 Emulator Manager.lnk"); } }
    // shortcut names before 1.2.5 (the program was called "Switch 2 Emulator Manager"): replaced on install, removed on uninstall
    public static string[] OldLnks { get { return new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Switch 2 Emulators.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Switch 2 Emulator Manager.lnk") }; } }
    public static string DefaultDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Switch 2 Emulators"); } }

    public static string InstalledDir()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KEY))
            {
                string d = k == null ? null : k.GetValue("InstallLocation") as string;
                return !string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, "EmuManager.exe")) ? d : null;
            }
        }
        catch { return null; }
    }

    [STAThread]
    static int Main(string[] args)
    {
        // Uninstall.exe (a copy of this exe) started without flags: uninstall
        if (string.Equals(Path.GetFileNameWithoutExtension(Application.ExecutablePath), "Uninstall", StringComparison.OrdinalIgnoreCase) && !args.Contains("--uninstall"))
            args = args.Concat(new[] { "--uninstall" }).ToArray();
        try
        {
            // silent install, like pressing Install with the default ticks (shortcuts + Apps & features); DIR optional
            if (args.Length >= 1 && args[0] == "--silent-install")
            {
                string w = Install(args.Length >= 2 ? Path.GetFullPath(args[1]) : (InstalledDir() ?? DefaultDir), true, true, true, s => Console.WriteLine(s));
                return w.Length == 0 ? 0 : 2;
            }
            if (args.Length >= 2 && args[0] == "--test-install") { Install(Path.GetFullPath(args[1]), false, false, false, s => Console.WriteLine(s)); return 0; }
            if (args.Length >= 2 && args[0] == "--test-uninstall") { Uninstall(Path.GetFullPath(args[1]), args.Contains("--settings"), false, s => Console.WriteLine(s)); return 0; }
        }
        catch (Exception ex) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "emu-manager-setup.log"), ex + "\r\n"); } catch { } return 1; }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Theme.Set(Theme.SystemDark());
        if (args.Contains("--uninstall"))
        {
            string dir = InstalledDir() ?? Path.GetDirectoryName(Application.ExecutablePath);
            Application.Run(new SetupForm(true, dir));
        }
        else Application.Run(new SetupForm(false, InstalledDir()));
        return 0;
    }


    public static bool ManagerRunning(string dir)
    {
        string exe = Path.Combine(dir, "EmuManager.exe");
        foreach (Process p in Process.GetProcessesByName("EmuManager"))
            try { if (string.Equals(p.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true; } catch { } finally { p.Dispose(); }
        return false;
    }

    // Every step also goes to %TEMP%\emu-manager-setup.log, so a setup that stopped partway can be explained.
    public static readonly string LogFile = Path.Combine(Path.GetTempPath(), "emu-manager-setup.log");
    public static void FileLog(string s) { try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + "\r\n"); } catch { } }

    // Returns warnings ("" when everything worked). The app files and the Apps & features entry must succeed; the
    // shortcuts are optional, so one failing is reported but doesn't stop the rest.
    public static string Install(string dir, bool desktop, bool startMenu, bool registry, Action<string> log0)
    {
        Action<string> log = s => { FileLog(s); log0(s); };
        FileLog("--- setup " + VERSION + " installing into " + dir + " (desktop=" + desktop + ", start=" + startMenu + ", registry=" + registry + ")");
        try { return InstallSteps(dir, desktop, startMenu, registry, log); }
        catch (Exception ex) { FileLog("FAILED: " + ex); throw; }
    }

    static string InstallSteps(string dir, bool desktop, bool startMenu, bool registry, Action<string> log)
    {
        // the app ships next to Setup.exe (same folder of the zip)
        string appFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "EmuManager.exe");
        if (string.Equals(Path.GetFullPath(appFile), Path.GetFullPath(Path.Combine(dir, "EmuManager.exe")), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Run Setup.exe from the folder you extracted, not from the install folder.");
        if (!File.Exists(appFile))
            throw new Exception("EmuManager.exe isn't next to Setup.exe. Extract the whole zip first (right-click it > Extract All), then run Setup.exe from that folder.");
        byte[] payload = File.ReadAllBytes(appFile);
        Directory.CreateDirectory(dir);
        if (ManagerRunning(dir)) throw new Exception("Close the NS2 Emulator Manager first, then press Install again.");
        string exe = Path.Combine(dir, "EmuManager.exe"), tmp = exe + ".new";
        File.WriteAllBytes(tmp, payload);
        if (File.Exists(exe)) File.Delete(exe);
        File.Move(tmp, exe);
        log("Copied EmuManager.exe");
        string un = Path.Combine(dir, "Uninstall.exe");
        if (!string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase))
        { File.Copy(Application.ExecutablePath, un, true); log("Copied Uninstall.exe"); }
        if (registry)   // first, so the app can always be uninstalled from Apps & features
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KEY))
            {
                k.SetValue("DisplayName", APP); k.SetValue("DisplayVersion", VERSION); k.SetValue("Publisher", PUBLISHER);
                k.SetValue("DisplayIcon", exe + ",0"); k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + un + "\" --uninstall");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)Math.Max(1, (payload.Length + new FileInfo(un).Length) / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord); k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            log("Added to Apps & features");
        }
        List<string> warn = new List<string>();
        foreach (KeyValuePair<string, string> sc in new[] { new KeyValuePair<string, string>(desktop ? DesktopLnk : null, "Desktop"), new KeyValuePair<string, string>(startMenu ? StartLnk : null, "Start menu") })
        {
            if (sc.Key == null) continue;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sc.Key));
                Shortcut(sc.Key, exe, dir, "Install, update and launch your Switch 2 emulators");
                if (!File.Exists(sc.Key)) throw new Exception("the shortcut file wasn't created");
                log(sc.Value + " shortcut");
            }
            catch (Exception ex)
            {
                string w = sc.Value + " shortcut failed: " + (ex.InnerException ?? ex).Message;
                FileLog(w + "\r\n" + ex); warn.Add("The " + sc.Value.ToLower() + " shortcut couldn't be made.");
            }
        }
        foreach (string l in OldLnks)   // the same shortcut under the old name
            try { if (File.Exists(l) && LinkTarget(l).Equals(exe, StringComparison.OrdinalIgnoreCase)) { File.Delete(l); log("Removed old shortcut " + Path.GetFileName(l)); } } catch { }
        RefreshIcons();
        FileLog(warn.Count == 0 ? "done" : "done with warnings: " + string.Join("; ", warn));
        return string.Join("  ", warn);
    }

    // Tells Explorer to drop its cached icons, so shortcuts show the app's own icon right away (not an old one).
    [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern void SHChangeNotify(int eventId, int flags, IntPtr a, IntPtr b);
    public static void RefreshIcons() { try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch { } }   // SHCNE_ASSOCCHANGED

    // Removes what setup installed. Builds, saves, downloads and previous builds in dir always stay.
    // Returns true when Uninstall.exe itself is running (so it stays in the folder).
    public static bool Uninstall(string dir, bool settings, bool registryAndShortcuts, Action<string> log)
    {
        if (ManagerRunning(dir)) throw new Exception("Close the NS2 Emulator Manager first.");
        string exe = Path.Combine(dir, "EmuManager.exe");
        if (File.Exists(exe)) { File.Delete(exe); log("Removed EmuManager.exe"); }
        foreach (string f in new[] { "EmuManager.next.exe", "EmuManager.exe.new" }) try { File.Delete(Path.Combine(dir, f)); } catch { }
        if (settings)
            foreach (string f in new[] { "emulators.json", "emulators.json.tmp", "manager.log" })
                if (File.Exists(Path.Combine(dir, f))) { File.Delete(Path.Combine(dir, f)); log("Removed " + f); }
        if (registryAndShortcuts)
        {
            foreach (string l in new[] { DesktopLnk, StartLnk }.Concat(OldLnks))
                if (File.Exists(l) && LinkTarget(l).Equals(exe, StringComparison.OrdinalIgnoreCase)) { File.Delete(l); log("Removed " + Path.GetFileName(l)); }
            try { Registry.CurrentUser.DeleteSubKeyTree(KEY, false); log("Removed from Apps & features"); } catch { }
        }
        // Uninstall.exe can't delete itself while it runs, and a hidden "cmd /c del" afterwards is what malware does
        // (antivirus scanners flag it), so when it is the one running it stays behind: the window says so.
        string un = Path.Combine(dir, "Uninstall.exe");
        bool self = string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase);
        if (!self && File.Exists(un)) { File.Delete(un); log("Removed Uninstall.exe"); }
        try { Directory.Delete(dir, false); log("Removed the empty folder"); } catch { }   // only when nothing else is left
        return self;
    }

    static void Shortcut(string lnk, string target, string dir, string desc)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        object s = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        Type st = s.GetType();
        st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, s, new object[] { target });
        st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, s, new object[] { dir });
        st.InvokeMember("Description", BindingFlags.SetProperty, null, s, new object[] { desc });
        st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, s, new object[] { target + ",0" });
        st.InvokeMember("Save", BindingFlags.InvokeMethod, null, s, null);
    }

    static string LinkTarget(string lnk)
    {
        try
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object s = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            return (string)s.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, s, null) ?? "";
        }
        catch { return ""; }
    }

    public static Bitmap Logo()
    {
        try { using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.png")) return s == null ? null : new Bitmap(s); }
        catch { return null; }
    }
}

// One window for both: install (folder, shortcuts, start when done) or uninstall (keep / remove settings).
class SetupForm : Form
{
    readonly bool uninstall;
    readonly string existing;
    readonly TextBox dir = new TextBox();
    readonly CheckBox desk = new CheckBox(), start = new CheckBox(), run = new CheckBox(), settings = new CheckBox();
    readonly Button go = new Button(), cancel = new Button(), browse = new Button();
    readonly Label status = new Label(), info = new Label();
    bool done;

    public SetupForm(bool uninstall, string existing)
    {
        this.uninstall = uninstall; this.existing = existing;
        Text = uninstall ? "Uninstall " + SetupApp.APP : SetupApp.APP + " Setup";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Font = Theme.Body; BackColor = Theme.Window; ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(600, 400);

        PictureBox logo = new PictureBox(); logo.Image = SetupApp.Logo(); logo.SizeMode = PictureBoxSizeMode.Zoom; logo.SetBounds(24, 22, 64, 64); Controls.Add(logo);
        Label title = new Label(); title.Font = Theme.Header; title.SetBounds(100, 26, 480, 34); Controls.Add(title);
        Label sub = new Label(); sub.ForeColor = Theme.Muted; sub.SetBounds(100, 60, 480, 22); Controls.Add(sub);
        Card box = new Card(); box.Shift = 0; box.SetBounds(24, 104, 552, 220); Controls.Add(box);
        info.SetBounds(18, 14, 516, 64); info.BackColor = Theme.Card; box.Controls.Add(info);
        status.SetBounds(24, 336, 360, 44); status.ForeColor = Theme.Muted; Controls.Add(status);
        go.SetBounds(392, 344, 100, 34); cancel.SetBounds(500, 344, 76, 34); cancel.Text = "Cancel";
        Controls.Add(go); Controls.Add(cancel);
        cancel.Click += delegate { Close(); };

        if (!uninstall)
        {
            title.Text = existing != null ? "Update " + SetupApp.APP : SetupApp.APP;
            sub.Text = "Version " + SetupApp.VERSION;
            info.Text = existing != null
                ? "Updates the manager. Your emulators and saves are kept."
                : "Installs the manager. You pick your emulators when it opens.";
            Label dl = new Label(); dl.Text = "Install folder (your emulators go here too)"; dl.SetBounds(18, 82, 516, 20); dl.BackColor = Theme.Card; box.Controls.Add(dl);
            dir.Text = existing ?? SetupApp.DefaultDir; dir.SetBounds(18, 104, 410, 26); dir.BackColor = Theme.Input; dir.ForeColor = Theme.Text; dir.BorderStyle = BorderStyle.FixedSingle; box.Controls.Add(dir);
            dir.Enabled = existing == null;
            browse.Text = "Browse..."; browse.SetBounds(436, 102, 98, 30); browse.Enabled = existing == null; box.Controls.Add(browse);
            browse.Click += delegate
            {
                string picked = FolderPicker.Pick(this, "Choose the install folder (a drive with free space)", Directory.Exists(dir.Text) ? dir.Text : Path.GetDirectoryName(dir.Text));
                if (picked != null) dir.Text = picked;   // exactly the chosen folder: builds go right inside it, no extra level
            };
            desk.Text = "Desktop shortcut"; desk.Checked = true; desk.SetBounds(18, 146, 240, 26);
            start.Text = "Start menu shortcut"; start.Checked = true; start.SetBounds(18, 176, 240, 26);
            run.Text = "Open the manager when setup finishes"; run.Checked = true; run.SetBounds(270, 146, 270, 26);
            foreach (CheckBox c in new[] { desk, start, run }) { c.BackColor = Theme.Card; c.ForeColor = Theme.Text; box.Controls.Add(c); EmuForm.StyleCheck(c); }
            go.Text = existing != null ? "Update" : "Install";
            go.Click += delegate { DoInstall(); };
        }
        else
        {
            title.Text = "Uninstall";
            sub.Text = SetupApp.APP + " in " + existing;
            info.Text = "Removes the manager. Your emulators and saves are kept.";
            settings.Text = "Also remove the manager's settings"; settings.SetBounds(18, 92, 500, 26);
            settings.BackColor = Theme.Card; settings.ForeColor = Theme.Text; box.Controls.Add(settings); EmuForm.StyleCheck(settings);
            go.Text = "Uninstall";
            go.Click += delegate { DoUninstall(); };
        }
        foreach (Button b in new[] { go, cancel, browse }) { EmuForm.StyleButton(b); b.BackColor = b == browse ? Theme.Card : Theme.Window; b.ForeColor = Theme.Text; }
        EmuForm.Outline(go, uninstall ? Color.Crimson : Color.ForestGreen);
        info.ForeColor = Theme.Text;
        foreach (Control k in Controls.Cast<Control>().Concat(box.Controls.Cast<Control>())) { Label l = k as Label; if (l != null) { l.UseMnemonic = false; l.AutoEllipsis = true; } }   // "Apps & features" shows its &
        HandleCreated += delegate { Native.Apply(Handle, Theme.Dark, 0); Native.ControlTheme(dir.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer"); };
    }

    void Status(string s, bool bad) { status.Text = s; status.ForeColor = bad ? Theme.Danger : Theme.Muted; status.Refresh(); }

    void DoInstall()
    {
        if (done) { Finish(); return; }
        string d = dir.Text.Trim();
        if (d.Length == 0 || !Path.IsPathRooted(d)) { Status("Choose a folder.", true); return; }
        try { d = Path.GetFullPath(d).TrimEnd('\\'); } catch { Status("This folder can't be used. Choose another one.", true); return; }
        if (Path.GetPathRoot(d).TrimEnd('\\') == d) { Status("Choose a folder, not a whole drive.", true); return; }
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (d.StartsWith(win, StringComparison.OrdinalIgnoreCase) || d.StartsWith(pf, StringComparison.OrdinalIgnoreCase)) { Status("This folder can't be used. Choose another one.", true); return; }
        go.Enabled = cancel.Enabled = false;
        try
        {
            string warn = SetupApp.Install(d, desk.Checked, start.Checked, true, s => Status("Installing...", false));
            done = true;
            Status(warn.Length > 0 ? "Done. " + warn : "Done.", warn.Length > 0);
            go.Text = "Finish"; go.Enabled = true; cancel.Visible = false;
            dir.Text = d;
        }
        catch (Exception ex) { Status(ex.Message, true); go.Enabled = cancel.Enabled = true; }
    }

    void Finish()
    {
        if (run.Checked)
            try { ProcessStartInfo si = new ProcessStartInfo(Path.Combine(dir.Text, "EmuManager.exe")); si.WorkingDirectory = dir.Text; Process.Start(si); } catch { }
        Close();
    }

    void DoUninstall()
    {
        if (done) { Close(); return; }
        go.Enabled = cancel.Enabled = false;
        try
        {
            bool left = SetupApp.Uninstall(existing, settings.Checked, true, s => Status("Uninstalling...", false));
            done = true;
            Status("Uninstalled. Your emulators and saves are still in " + existing + ".", false);
            go.Text = "Close"; go.Enabled = true; cancel.Visible = false;
        }
        catch (Exception ex) { Status(ex.Message, true); go.Enabled = cancel.Enabled = true; }
    }
}
