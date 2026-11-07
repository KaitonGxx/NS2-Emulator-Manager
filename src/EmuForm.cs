// Emulator Manager window. Look and painting follow the PSU Offline Lab trainer (lab\gui\LabForm.Layout.cs): Windows 11
// light / dark theme, rounded cards, flat rounded buttons. One window: header, games folder, a card per emulator
// (Launch / Install / Update and a "..." menu), status line; the log opens in its own window.
// Command line (for tests): EmuManager.exe [--config FILE] [--cli check|update ID|restore ID ...] [--log FILE]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Theme
{
    public static bool Dark;
    public static Color Window, Sidebar, Card, Stroke, StrokeStrong, Text, Muted, Disabled, Accent, AccentText, Input,
                        Control, ControlHover, ControlPressed, ControlOff, NavHover, NavSelected, Danger, Success, Warn, Info;
    public static Font Body, Small, Title, CardTitle, Header, Nav, Icon;

    public static void Set(bool dark)
    {
        Dark = dark;
        if (dark)
        {
            Window = Rgb(0x202020); Sidebar = Rgb(0x1a1a1a); Card = Rgb(0x2b2b2b); Stroke = Rgb(0x3a3a3a); StrokeStrong = Rgb(0x8a8a8a);
            Text = Rgb(0xf3f3f3); Muted = Rgb(0xa8a8a8); Disabled = Rgb(0x6e6e6e); Accent = Rgb(0x60cdff); AccentText = Rgb(0x0a1a24);
            Input = Rgb(0x1f1f1f); Control = Rgb(0x373737); ControlHover = Rgb(0x404040); ControlPressed = Rgb(0x313131); ControlOff = Rgb(0x2f2f2f);
            NavHover = Color.FromArgb(0x10, 255, 255, 255); NavSelected = Color.FromArgb(0x1c, 255, 255, 255);
            Danger = Rgb(0xff99a4); Success = Rgb(0x6ccb5f); Warn = Rgb(0xfce100); Info = Rgb(0x99ebff);
        }
        else
        {
            Window = Rgb(0xf3f3f3); Sidebar = Rgb(0xe9e9e9); Card = Rgb(0xffffff); Stroke = Rgb(0xe3e3e3); StrokeStrong = Rgb(0x8a8a8a);
            Text = Rgb(0x1b1b1b); Muted = Rgb(0x5f5f5f); Disabled = Rgb(0xa0a0a0); Accent = Rgb(0x005fb8); AccentText = Rgb(0xffffff);
            Input = Rgb(0xffffff); Control = Rgb(0xfbfbfb); ControlHover = Rgb(0xf3f3f3); ControlPressed = Rgb(0xebebeb); ControlOff = Rgb(0xf5f5f5);
            NavHover = Color.FromArgb(0x0c, 0, 0, 0); NavSelected = Color.FromArgb(0x14, 0, 0, 0);
            Danger = Rgb(0xc42b1c); Success = Rgb(0x0f7b0f); Warn = Rgb(0x9d5d00); Info = Rgb(0x005fb8);
        }
        if (Body == null)
        {
            string text = Family("Segoe UI"), semi = Family("Segoe UI Variable Display Semibold", "Segoe UI Semibold"), icons = Family("Segoe Fluent Icons", "Segoe MDL2 Assets");
            Body = new Font(text, 9.5f); Small = new Font(text, 9f); Nav = new Font(text, 10f);
            Title = new Font(semi, 10.5f); CardTitle = new Font(semi, 12f); Header = new Font(semi, 16f);
            Icon = new Font(icons, 12f);
        }
    }

    static Color Rgb(int v) { return Color.FromArgb(255, (v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff); }

    static string Family(params string[] names)
    {
        foreach (string n in names)
            try { using (Font f = new Font(n, 9f)) if (string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase)) return n; } catch { }
        return names[names.Length - 1];
    }

    public static Color Blend(Color a, Color b, double t)
    {
        return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static Color Tint(Color c) { return Dark ? Blend(c, Color.White, 0.28) : c; }

    public static bool SystemDark()
    {
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            return v is int && (int)v == 0;
        }
        catch { return false; }
    }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Color fill, Color border, RectangleF r, float rad, float bw)
    {
        using (GraphicsPath p = Round(r, rad))
        {
            if (fill.A > 0) using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
            if (bw > 0 && border.A > 0) using (Pen pen = new Pen(border, bw)) g.DrawPath(pen, p);
        }
    }
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int L, R, T, B; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [DllImport("dwmapi.dll")] static extern int DwmIsCompositionEnabled(out bool on);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string ids);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
    [DllImport("kernel32.dll")] public static extern bool AttachConsole(int pid);

    static int build = -1;
    public static int Build
    {
        get
        {
            if (build < 0)
                try { build = int.Parse((string)Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", "0")); }
                catch { build = 0; }
            return build;
        }
    }

    static int Attr(IntPtr h, int attr, int v) { try { return DwmSetWindowAttribute(h, attr, ref v, 4); } catch { return -1; } }

    public static bool Apply(IntPtr h, bool dark, int glassWidth)
    {
        Attr(h, Build >= 18985 ? 20 : 19, dark ? 1 : 0);
        Attr(h, 33, 2);
        bool comp = false;
        try { DwmIsCompositionEnabled(out comp); } catch { }
        bool on = false;
        if (Build >= 22621 && comp) on = Attr(h, 38, 2) == 0;          // Mica
        else if (Build >= 22000 && comp) on = Attr(h, 1029, 1) == 0;
        MARGINS m = new MARGINS(); m.L = on ? glassWidth : 0;
        try { DwmExtendFrameIntoClientArea(h, ref m); } catch { on = false; }
        return on;
    }

    public static void ControlTheme(IntPtr h, string name) { try { SetWindowTheme(h, name, null); } catch { } }

    public static void BlitAlpha(Graphics g, Bitmap bmp)
    {
        IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr hdc = g.GetHdc();
        try
        {
            IntPtr mem = CreateCompatibleDC(hdc), old = SelectObject(mem, hb);
            BitBlt(hdc, 0, 0, bmp.Width, bmp.Height, mem, 0, 0, 0x00CC0020);
            SelectObject(mem, old); DeleteDC(mem);
        }
        finally { g.ReleaseHdc(hdc); DeleteObject(hb); }
    }
}

class Card : Panel
{
    public int Shift = 10;
    public Card() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); e.Control.Top += Shift; }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Theme.FillRound(g, Theme.Card, Theme.Stroke, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 8, 1);
        if (Text.Length > 0)
        {
            Theme.FillRound(g, Theme.Accent, Color.Empty, new RectangleF(14, 10, 3.5f, 17), 1.75f, 0);
            TextRenderer.DrawText(g, Text, Theme.CardTitle, new Rectangle(24, 7, Width - 38, 24), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }
    }
}

class FlatBar : Control
{
    long max = 100, val;
    public FlatBar() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    public void Set(long v, long m) { max = Math.Max(1, m); val = Math.Max(0, Math.Min(max, v)); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float h = Math.Min(6, Height), y = (Height - h) / 2f;
        Theme.FillRound(g, Theme.Dark ? Color.FromArgb(255, 0x45, 0x45, 0x45) : Color.FromArgb(255, 0xdd, 0xdd, 0xdd), Color.Empty, new RectangleF(0, y, Width - 1, h), h / 2, 0);
        float w = (Width - 1) * (float)val / max;
        if (w >= 1) Theme.FillRound(g, Theme.Accent, Color.Empty, new RectangleF(0, y, Math.Max(w, h), h), h / 2, 0);
    }
}

// The author's credit in the header: a small Discord logo and the Discord name. Click copies the name.
class DiscordTag : Control
{
    public const string NAME = "Riskbreak";
    // Discord's logo (24 x 24 units, as an SVG path); arcs in it are tiny, so they're drawn as straight lines
    const string LOGO = "M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189Z";
    static readonly Color Blurple = Color.FromArgb(0x58, 0x65, 0xF2);
    static GraphicsPath logo;
    string shown = NAME;
    readonly System.Windows.Forms.Timer back = new System.Windows.Forms.Timer();
    public DiscordTag()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
        back.Interval = 1500; back.Tick += delegate { back.Stop(); shown = NAME; Invalidate(); };
        Click += delegate { try { Clipboard.SetText(NAME); shown = "Copied"; Invalidate(); back.Stop(); back.Start(); } catch { } };
    }
    public int PreferredWidth(Graphics g) { return Height + 6 + TextRenderer.MeasureText(g, NAME, Font).Width + 2; }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = Math.Min(Height, Width) * 0.72f, y0 = (Height - s) / 2f;
        using (GraphicsPath p = (GraphicsPath)Logo().Clone())
        using (Matrix m = new Matrix())
        using (SolidBrush b = new SolidBrush(Blurple))
        {
            m.Translate(1, y0); m.Scale(s / 24f, s / 24f);
            p.Transform(m); g.FillPath(b, p);
        }
        Rectangle tr = new Rectangle((int)(s + 7), 0, Width - (int)(s + 7), Height);
        TextRenderer.DrawText(g, shown, Font, tr, Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
    protected override void Dispose(bool disposing) { if (disposing) back.Dispose(); base.Dispose(disposing); }

    static GraphicsPath Logo()
    {
        if (logo != null) return logo;
        GraphicsPath p = new GraphicsPath(FillMode.Alternate);
        string d = LOGO; int i = 0; char cmd = 'M';
        PointF cur = PointF.Empty, start = PointF.Empty;
        Func<float> num = () =>
        {
            while (i < d.Length && (d[i] == ' ' || d[i] == ',')) i++;
            int b = i;
            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
            bool dot = false;
            while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot))) { if (d[i] == '.') dot = true; i++; }
            return float.Parse(d.Substring(b, i - b), System.Globalization.CultureInfo.InvariantCulture);
        };
        Func<float> flag = () => { while (i < d.Length && (d[i] == ' ' || d[i] == ',')) i++; return d[i++] - '0'; };
        Func<bool> more = () => { int j = i; while (j < d.Length && (d[j] == ' ' || d[j] == ',')) j++; return j < d.Length && (char.IsDigit(d[j]) || d[j] == '-' || d[j] == '.' || d[j] == '+'); };
        while (i < d.Length)
        {
            if (char.IsLetter(d[i])) cmd = d[i++];
            else if (!more()) { i++; continue; }
            bool rel = char.IsLower(cmd);
            float ox = rel ? cur.X : 0, oy = rel ? cur.Y : 0;
            switch (char.ToUpper(cmd))
            {
                case 'M':
                    p.StartFigure(); cur = start = new PointF(ox + num(), oy + num());
                    cmd = rel ? 'l' : 'L';   // more pairs after M are line-tos
                    break;
                case 'L': { PointF n = new PointF(ox + num(), oy + num()); p.AddLine(cur, n); cur = n; break; }
                case 'H': { PointF n = new PointF(ox + num(), cur.Y); p.AddLine(cur, n); cur = n; break; }
                case 'V': { PointF n = new PointF(cur.X, oy + num()); p.AddLine(cur, n); cur = n; break; }
                case 'C':
                {
                    PointF a = new PointF(ox + num(), oy + num()), b = new PointF(ox + num(), oy + num()), n = new PointF(ox + num(), oy + num());
                    p.AddBezier(cur, a, b, n); cur = n; break;
                }
                case 'A':
                {
                    num(); num(); num(); flag(); flag();
                    PointF n = new PointF(ox + num(), oy + num()); p.AddLine(cur, n); cur = n; break;
                }
                case 'Z': p.CloseFigure(); cur = start; break;
                default: i++; break;
            }
        }
        logo = p;
        return p;
    }
}

// One emulator's card on the Builds page.
class EmuCard
{
    public EmuDef Def;
    public Card Box = new Card();
    public Label Note = new Label(), State = new Label(), Data = new Label(), Ver = new Label();   // Ver: installed version, on the title row
    // Launch when installed; Install when not; next to Launch, a greyed "Up to date" that becomes "Update" when a newer version is out. "..." holds the rest.
    public Button Update = new Button(), Launch = new Button(), More = new Button();
    public LinkLabel Existing = new LinkLabel(), Next = new LinkLabel();   // "I already have it" / "Next: add your keys..."
    public FlatBar Bar = new FlatBar();
    public Release Rel;
    public string Err;            // short, plain message for the card after a failed job or launch (details go to the log)
    public string CheckErr;       // the update check failed
    public bool Busy;
    public string NextKind;       // what Next offers: "keys", "firmware", "portable" or null
}

// Dark / light colours for the "..." menus.
class MenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
    public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
    public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    public override Color MenuBorder { get { return Theme.Stroke; } }
    public override Color MenuItemBorder { get { return Theme.ControlHover; } }
    public override Color MenuItemSelected { get { return Theme.ControlHover; } }
    public override Color SeparatorDark { get { return Theme.Stroke; } }
    public override Color SeparatorLight { get { return Theme.Card; } }
}

class EmuForm : Form
{
    // One window: a header (logo, title, Add emulator, "..." menu), the games folder line, one card per emulator and a
    // status line at the bottom. The log opens in its own window. Text rules: plain words, only what the user needs;
    // details (versions, paths, reasons) go to the log.
    readonly Panel buildsPage = new Panel(), cardsHost = new Panel();
    readonly Label head = new Label(), footer = new Label();
    readonly PictureBox logoBox = new PictureBox();
    readonly DiscordTag credit = new DiscordTag();   // "made by" on Discord, next to the title
    readonly Button addEmu = new Button(), menuBtn = new Button(), logCopy = new Button();
    readonly LinkLabel checkLink = new LinkLabel(), logLink = new LinkLabel();
    readonly TextBox logBox = new TextBox();
    Form logForm;
    readonly List<EmuCard> cards = new List<EmuCard>();
    readonly ToolTip tips = new ToolTip();
    static readonly Dictionary<Control, Color> accents = new Dictionary<Control, Color>();
    static readonly Dictionary<Control, string> roles = new Dictionary<Control, string>();
    static readonly HashSet<Control> styled = new HashSet<Control>();
    static readonly HashSet<Control> strong = new HashSet<Control>();   // labels drawn in the semibold title font
    bool glassOn, anyBusy;   // anyBusy: a Work job runs (only Work sets it)
    int identifying;           // "I already have it" checks running
    int asking;                // ApplyShared questions open
    bool IsBusy { get { return anyBusy || identifying > 0 || asking > 0; } }
    void PleaseWait() { Tell(APP, "Please wait for the current task to finish."); }
    readonly Card gamesCard = new Card();
    readonly Label gamesLbl = new Label();
    readonly Button gamesPick = new Button();
    const string APP = "NS2 Emulator Manager";

    void GamesText()
    {
        string g = Core.Cfg.GamesFolder;
        bool set = !string.IsNullOrEmpty(g);
        gamesLbl.Text = !set ? "Choose the folder with your games." : "Games: " + g + (Directory.Exists(g) ? "" : "   (not found)");
        roles[gamesLbl] = set && Directory.Exists(g) ? "text" : "info";
        gamesLbl.ForeColor = RoleColor(gamesLbl);
        gamesPick.Text = set ? "Change..." : "Choose...";
        if (set) accents.Remove(gamesPick); else accents[gamesPick] = Theme.Accent;
        gamesPick.Invalidate();
    }

    void PickGames()
    {
        string picked = FolderPicker.Pick(this, "Choose the folder with your games",
                                          !string.IsNullOrEmpty(Core.Cfg.GamesFolder) && Directory.Exists(Core.Cfg.GamesFolder) ? Core.Cfg.GamesFolder : null);
        if (picked == null) return;
        string prev = Core.Cfg.GamesFolder;
        Core.Cfg.GamesFolder = picked;
        try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save the games folder: " + ex.Message); }
        GamesText();
        Log("Games folder set to " + picked);
        SyncGames(prev, true);
    }

    // Writes the games folder into each installed build's game list (or just one, before it launches).
    void SyncGames(string previous, bool all, EmuDef only = null)
    {
        int n = 0;
        foreach (EmuCard c in cards)
        {
            if (only != null && c.Def != only) continue;
            try { string m = Core.SyncGamesFolder(c.Def, previous); if (m != null) { Log(m); n++; } }
            catch (Exception ex) { Log(c.Def.Name + ": could not set the game folder: " + ex.Message); }
        }
        if (all && n == 0) Log("Every installed build already lists " + Core.Cfg.GamesFolder + ".");
    }

    // Release notes as plain text: markdown links -> their text, no tables, headings or code ticks, and the sections
    // for other platforms (Linux, macOS, Android...) left out. maxLines > 0: a short summary (for the hover tip).
    static readonly System.Text.RegularExpressions.Regex MdLink = new System.Text.RegularExpressions.Regex(@"!?\[([^\]]*)\]\([^)]*\)");
    static readonly System.Text.RegularExpressions.Regex OtherOs = new System.Text.RegularExpressions.Regex(@"(?i)\b(linux|mac ?os|macos|android|aarch64|arm64|appimage|flatpak)\b");
    static string ReadableNotes(string body, int maxLines)
    {
        if (string.IsNullOrEmpty(body)) return "";
        List<string> o = new List<string>();
        bool skip = false, blank = false;
        foreach (string raw in body.Replace("\r\n", "\n").Split('\n'))
        {
            string l = raw.TrimEnd();
            string tl = l.TrimStart();
            if (tl.StartsWith("#"))
            {
                string h = tl.TrimStart('#').Trim();
                skip = OtherOs.IsMatch(h);
                if (skip || h.Length == 0) continue;
                if (maxLines > 0) continue;               // the summary leaves headings out
                l = h.ToUpperInvariant() == h ? h : h + ":";
            }
            if (skip) continue;
            if (tl.StartsWith("|") || tl.StartsWith("---") || tl.StartsWith("<!--") || tl.StartsWith("```")) continue;
            l = MdLink.Replace(l, "$1").Replace("`", "").Replace("**", "").Replace("__", "");
            if (tl.StartsWith("* ")) l = l.Replace("* ", "- ");
            if (l.Trim().Length == 0)
            {
                if (maxLines > 0 || blank || o.Count == 0) continue;
                blank = true; o.Add(""); continue;
            }
            blank = false;
            if (maxLines > 0 && l.Length > 110) l = l.Substring(0, 107).TrimEnd() + "...";
            o.Add(l);
            if (maxLines > 0 && o.Count >= maxLines) { o.Add("..."); break; }
        }
        while (o.Count > 0 && o[o.Count - 1] == "") o.RemoveAt(o.Count - 1);
        return string.Join("\r\n", o);
    }

    void ShowNotes(EmuCard c)
    {
        if (c.Rel == null) return;
        bool na;
        ReleaseDialog("What's new in " + c.Def.Name, null, c.Rel, null, null, false, out na);
    }

    // Release notes window with up to two action buttons. Returns 1 = primary, 2 = secondary, 0 = closed / cancelled.
    int ReleaseDialog(string title, string intro, Release r, string primary, string secondary, bool offerNoAsk, out bool noAsk)
    {
        noAsk = false;
        using (Form f = new Form())
        {
            f.Text = title; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(D(600), D(440)); f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            int y = D(14);
            Label h = new Label(); h.Text = intro ?? ""; h.SetBounds(D(16), y, D(568), D(40)); h.ForeColor = Theme.Text; h.Visible = intro != null;
            if (intro != null) y += D(44);
            Label t = new Label(); t.Font = Theme.Title; t.ForeColor = Theme.Accent; t.SetBounds(D(16), y, D(568), D(22)); t.AutoEllipsis = true;
            t.Text = intro != null ? "What's new" : "Latest version";
            y += D(26);
            TextBox body = new TextBox(); body.Multiline = true; body.ReadOnly = true; body.ScrollBars = ScrollBars.Vertical; body.WordWrap = true;
            body.SetBounds(D(16), y, D(568), D(380) - y); body.BackColor = Theme.Input; body.ForeColor = Theme.Text; body.BorderStyle = BorderStyle.FixedSingle;
            string notes = ReadableNotes(r.Body, 0);
            body.Text = notes.Length == 0 ? "No notes for this version." : notes;
            CheckBox na = new CheckBox(); na.Text = "Don't ask again"; na.SetBounds(D(16), D(394), D(200), D(26)); na.Visible = offerNoAsk; na.ForeColor = Theme.Text;
            int res = 0;
            Button p = new Button(); p.Text = primary ?? ""; p.SetBounds(secondary != null ? D(250) : D(370), D(390), D(150), D(36)); p.Click += delegate { res = 1; f.Close(); };
            Button s2 = new Button(); s2.Text = secondary ?? ""; s2.SetBounds(D(406), D(390), D(110), D(36)); s2.Visible = secondary != null; s2.Click += delegate { res = 2; f.Close(); };
            Button x = new Button(); x.Text = "Cancel"; x.SetBounds(D(522), D(390), D(62), D(36)); x.Click += delegate { res = 0; f.Close(); };
            if (secondary == null) x.SetBounds(D(526), D(390), D(58), D(36));
            if (primary == null) { p.Visible = false; x.Text = "Close"; x.SetBounds(D(494), D(390), D(90), D(36)); }   // notes viewer only
            f.CancelButton = x;
            f.Controls.AddRange(new Control[] { h, t, body, na, p, s2, x });
            foreach (Button b in new[] { p, s2, x }) { StyleButton(b); b.BackColor = Theme.Window; }
            Outline(p, Theme.Accent);
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); Native.ControlTheme(body.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer"); };
            f.Shown += delegate { if (p.Visible) p.Focus(); else x.Focus(); };
            f.ShowDialog(this);
            noAsk = na.Checked;
            return res;
        }
    }

    // A short question with buttons that say what happens. Returns the index of the button pressed, -1 when closed.
    int Ask(string title, string text, params string[] buttons) { return AskOn(this, title, text, buttons); }
    int AskOn(IWin32Window owner, string title, string text, params string[] buttons)
    {
        using (Form f = new Form())
        {
            f.Text = title; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            int W = Math.Max(D(460), buttons.Sum(t => Math.Max(D(92), TextRenderer.MeasureText(t, Font).Width + D(28)) + D(8)) + D(32));
            Label l = new Label(); l.Text = text; l.ForeColor = Theme.Text; l.MaximumSize = new Size(W - D(40), 0); l.AutoSize = true; l.Location = new Point(D(20), D(18));
            f.Controls.Add(l);
            int th = l.PreferredHeight;
            int y = D(18) + th + D(22);
            int res = -1;
            int x = W - D(20);
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                int idx = i;
                Button b = new Button(); b.Text = buttons[i];
                int bw = Math.Max(D(92), TextRenderer.MeasureText(buttons[i], Font).Width + D(28));
                x -= bw; b.SetBounds(x, y, bw, D(34)); x -= D(8);
                b.Click += delegate { res = idx; f.Close(); };
                f.Controls.Add(b); StyleButton(b); b.BackColor = Theme.Window;
                if (i == 0) { Outline(b, Theme.Accent); f.AcceptButton = b; }
                if (i == buttons.Length - 1 && buttons.Length > 1) f.CancelButton = b;
            }
            f.ClientSize = new Size(W, y + D(34) + D(16));
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
            f.ShowDialog(owner);
            return res;
        }
    }
    void Tell(string title, string text) { Ask(title, text, "OK"); }

    bool Installed(EmuDef d) { return !string.IsNullOrEmpty(d.Exe) && File.Exists(Path.Combine(d.Dir, d.Exe)); }
    bool Current(EmuCard c) { Manifest m = Core.LoadManifest(c.Def.Dir); return Installed(c.Def) && m != null && c.Rel != null && m.Tag == c.Rel.Tag; }
    static bool HasKeys(EmuDef d) { return File.Exists(Path.Combine(d.Dir, "user", "keys", "prod.keys")); }
    static bool HasFirmware(EmuDef d)
    {
        string r = Path.Combine(d.Dir, "user", "nand", "system", "Contents", "registered");
        try { return Directory.Exists(r) && Directory.EnumerateFileSystemEntries(r).Any(); } catch { return false; }
    }

    // Launch: the first time per session that an update is known, offer it.
    readonly HashSet<string> launchAsked = new HashSet<string>();
    void DoLaunch(EmuCard c)
    {
        Manifest im = Core.LoadManifest(c.Def.Dir);
        bool chosen = im != null && im.Chosen;   // an older version the user picked: don't push the update
        if (!chosen && c.Rel != null && Installed(c.Def) && !Current(c) && !IsBusy && launchAsked.Add(c.Def.Id))
        {
            bool na;
            int r = ReleaseDialog("Update " + c.Def.Name + "?", "A newer version of " + c.Def.Name + " is available. Your saves and settings are kept.",
                c.Rel, "Update and launch", "Launch now", false, out na);
            if (r == 0) return;
            if (r == 1) { StartUpdate(c, delegate { LaunchNow(c); }); return; }
        }
        LaunchNow(c);
    }

    // Eden-based emulators may close a few seconds after starting while another one runs. Returns false to cancel.
    bool WarnOtherEmulators(EmuCard c)
    {
        List<string> running = new List<string>();
        foreach (EmuCard k in cards)
            if (k != c && Installed(k.Def) && Core.Running(k.Def).Count > 0) running.Add(k.Def.Name);
        HashSet<string> mine = new HashSet<string>(cards.Select(k => Path.GetFileNameWithoutExtension(k.Def.Exe ?? "")), StringComparer.OrdinalIgnoreCase);
        foreach (string n in Core.FamilyExes.Select(Path.GetFileNameWithoutExtension).Concat(mine).Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (Process p in Process.GetProcessesByName(n))
            {
                try
                {
                    string path = p.MainModule.FileName;
                    if (!path.StartsWith(Path.GetFullPath(c.Def.Dir).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) &&
                        !cards.Any(k => k != c && !string.IsNullOrEmpty(k.Def.Dir) && path.StartsWith(Path.GetFullPath(k.Def.Dir).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))
                        running.Add(Path.GetFileNameWithoutExtension(path));
                }
                catch { }
                finally { p.Dispose(); }
            }
        running = running.Distinct().ToList();
        if (running.Count == 0) return true;
        return Ask("Another emulator is open", "Close " + string.Join(" and ", running) + " first, or " + c.Def.Name + " may close by itself.", "Launch anyway", "Cancel") == 0;
    }

    void LaunchNow(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        c.Err = null;
        string block = UserData.LaunchProblem(c.Def);
        if (block != null) { Tell("Can't launch yet", block); return; }
        if (!WarnOtherEmulators(c)) return;
        try
        {
            Process p = Core.Launch(c.Def);
            Log("Launched " + Path.Combine(c.Def.Dir, c.Def.Exe));
            if (p != null)
                ThreadPool.QueueUserWorkItem(delegate
                {   // closed again within 8 s: tell the user instead of failing silently
                    bool quit = false; int code = 0;
                    try { quit = p.WaitForExit(8000); if (quit) code = p.ExitCode; } catch { }
                    if (quit) UI(delegate { Log(c.Def.Name + " closed right after starting (exit code " + code + ")."); Fail(c, c.Def.Name + " closed right away. Close other emulators and try again."); });
                });
        }
        catch (Exception ex) { Log(c.Def.Name + ": " + ex.Message); Fail(c, "Couldn't start " + c.Def.Name + "."); }
    }

    // ---- layout. Everything is placed by DoLayout (on every resize), so the window can be made small.
    const int S = 10;   // Card.Shift (title room)
    float k = 1f;
    int D(int v) { return (int)Math.Round(v * k); }
    // shown instead of cards until the user adds one
    readonly Card emptyCard = new Card();
    readonly Label emptyText = new Label();
    readonly Button emptyRec = new Button();
    readonly LinkLabel emptyFork = new LinkLabel();

    const string SANDBOX_MARK = ".emu-manager-sandbox";

    // Test environment only: wipe its folder and start again as a new user would.
    void ResetSandbox()
    {
        string home = Path.GetFullPath(Core.Home).TrimEnd('\\');
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath).TrimEnd('\\');
        if (!Core.Cfg.Sandbox || IsBusy || string.Equals(home, exeDir, StringComparison.OrdinalIgnoreCase) || Path.GetPathRoot(home).TrimEnd('\\') == home) return;
        // only a folder that --sandbox made itself (it leaves a marker when it creates an empty one), and never one
        // that holds the manager: "--sandbox D:\Games" must not let Reset wipe D:\Games
        if (!File.Exists(Path.Combine(home, SANDBOX_MARK)) || exeDir.StartsWith(home + "\\", StringComparison.OrdinalIgnoreCase))
        {
            Tell("Reset test", "Reset only works on a test folder the manager made itself. Nothing was deleted.");
            return;
        }
        List<string> run = Core.Cfg.Emulators.SelectMany(d => Core.Running(d)).ToList();
        if (run.Count > 0) { Tell("Reset test", "Close the test emulators first."); return; }
        if (Ask("Reset test", "Delete everything in the test folder and start again as a new user?\r\n" + home, "Reset", "Cancel") != 0) return;
        try { UserData.DeleteTree(home); }
        catch (Exception ex) { Tell("Reset test", "Could not delete everything: " + ex.Message); return; }
        Process.Start(Application.ExecutablePath, "--sandbox \"" + home + "\"");
        Close();
    }

    public EmuForm()
    {
        Text = APP + (Core.Cfg.Sandbox && Demo == null ? "  -  TEST" : "");   // --demo: screenshots, without the test label
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.None;   // DoLayout scales by the screen DPI itself
        using (Graphics g = CreateGraphics()) k = Math.Max(1f, g.DpiX / 96f);
        ClientSize = new Size(D(800), D(720));
        MinimumSize = new Size(D(480), D(420));
        StartPosition = FormStartPosition.CenterScreen;
        RestoreWindow();
        DoubleBuffered = true;

        Controls.Add(buildsPage);
        // header
        try { using (Stream s = typeof(EmuForm).Assembly.GetManifestResourceStream("app.png")) if (s != null) logoBox.Image = new Bitmap(s); } catch { }
        logoBox.SizeMode = PictureBoxSizeMode.Zoom;
        head.Text = Core.Cfg.Sandbox && Demo == null ? APP + " (test)" : APP; head.Font = Theme.Header ?? new Font("Segoe UI Semibold", 16f); head.AutoEllipsis = true;
        addEmu.Text = "Add emulator fork"; addEmu.Click += delegate { ShowRecommended(false); };
        Outline(addEmu, Theme.Accent);
        menuBtn.Text = "..."; menuBtn.Click += delegate { MainMenu().Show(menuBtn, new Point(0, menuBtn.Height)); };
        tips.SetToolTip(menuBtn, "More");
        buildsPage.Controls.AddRange(new Control[] { logoBox, head, addEmu, menuBtn, credit });
        tips.SetToolTip(credit, "Made by " + DiscordTag.NAME + " on Discord. Click to copy the name.");
        // games folder: one line
        gamesCard.Text = ""; gamesCard.Shift = 0;
        gamesLbl.AutoEllipsis = true;
        gamesPick.Click += delegate { PickGames(); };
        gamesCard.Controls.AddRange(new Control[] { gamesLbl, gamesPick });
        buildsPage.Controls.Add(gamesCard);
        cardsHost.AutoScroll = true;
        buildsPage.Controls.Add(cardsHost);
        emptyCard.Text = "No emulators yet";
        emptyText.Text = "Choose one to install.";
        emptyText.ForeColor = Color.DimGray;
        emptyRec.Text = "Choose emulator"; emptyRec.Click += delegate { ShowRecommended(false); };
        emptyFork.Text = "Add from link..."; emptyFork.LinkClicked += delegate { AddFork(); };
        Outline(emptyRec, Theme.Accent);
        emptyCard.Controls.AddRange(new Control[] { emptyText, emptyRec, emptyFork });
        cardsHost.Controls.Add(emptyCard);
        // status line
        footer.AutoEllipsis = true; footer.ForeColor = Color.DimGray;
        checkLink.Text = "Check for updates"; checkLink.LinkClicked += delegate { CheckAll(true); };   // by hand: always ask the hosts now
        logLink.Text = "Show log"; logLink.LinkClicked += delegate { ShowLog(); };
        buildsPage.Controls.AddRange(new Control[] { footer, checkLink, logLink });
        // Eglantine main (the upstream repo) always comes first; the rest keep their order
        foreach (EmuDef d in Core.Cfg.Emulators.OrderBy(x => Core.IsMainRepo(x) ? 0 : 1)) cards.Add(MakeCard(d));

        // log (its own window, made on first use)
        logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical; logBox.WordWrap = true;
        logBox.Font = new Font("Cascadia Mono", 9f); if (logBox.Font.Name != "Cascadia Mono") logBox.Font = new Font("Consolas", 9f);
        logCopy.Text = "Copy"; logCopy.Click += delegate { if (logBox.Text.Length > 0) Clipboard.SetText(logBox.Text); };
        logBox.CreateControl();

        GamesText();
        ApplyStyle();
        DoLayout();
        Resize += delegate { DoLayout(); };
        // back from Explorer (keys copied in...): every card re-checks; a postponed question is asked now
        Activated += delegate { if (!IsBusy) RefreshAll(); if (dupRetry && !IsBusy) BeginInvoke((MethodInvoker)AskDuplicates); };
        // first start: the welcome list (nothing is on the list yet); afterwards the usual update check
        Shown += delegate
        {
            if (Core.Cfg.Sandbox && Demo != null) { BeginInvoke((MethodInvoker)RunDemo); CheckAll(); return; }
            if (Core.FirstRun) { Core.FirstRun = false; ShowRecommended(true); }
            CheckAll();
        };
        FormClosing += delegate (object s, FormClosingEventArgs e)
        {
            // closing would kill the worker mid-move, with no rollback: wait instead (Windows shutting down still wins)
            if (anyBusy && e.CloseReason == CloseReason.UserClosing)
            {
                Tell(APP, "Please wait for the current task to finish.");
                e.Cancel = true;
            }
        };
        FormClosed += delegate { SaveWindow(); };
    }

    // The window opens where and how big the user left it (if that spot is still on a screen), else centred.
    void RestoreWindow()
    {
        try
        {
            if (string.IsNullOrEmpty(Core.Cfg.Window)) return;
            int[] v = Core.Cfg.Window.Split(',').Select(int.Parse).ToArray();
            if (v.Length < 5) return;
            Rectangle r = new Rectangle(v[0], v[1], Math.Max(v[2], MinimumSize.Width), Math.Max(v[3], MinimumSize.Height));
            // its title bar must be on some screen, so it can be moved
            Rectangle bar = new Rectangle(r.X + 40, r.Y, Math.Max(1, r.Width - 80), 30);
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bar))) { Size = r.Size; return; }
            StartPosition = FormStartPosition.Manual;
            Bounds = r;
            if (v[4] == 1) WindowState = FormWindowState.Maximized;
        }
        catch { }   // a hand-edited or old value: the default size
    }
    void SaveWindow()
    {
        try
        {
            Rectangle r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;   // maximized / minimized: the normal size
            if (r.Width < 100 || r.Height < 100) return;
            Core.Cfg.Window = r.X + "," + r.Y + "," + r.Width + "," + r.Height + "," + (WindowState == FormWindowState.Maximized ? 1 : 0);
            Core.SaveConfig();
        }
        catch { }
    }

    // --demo NAME[:ID] (test environment only): opens one screen right away, for screenshots.
    public static string Demo;
    void RunDemo()
    {
        EmuCard c = cards.FirstOrDefault(k => Demo.EndsWith(":" + k.Def.Id)) ?? cards.FirstOrDefault();
        string n = Demo.Split(':')[0];
        if (n == "welcome") ShowRecommended(true);
        else if (n == "add") ShowRecommended(false);
        else if (n == "link") AddFork();
        else if (n == "shared") SharedDialog();
        else if (n == "menu") MainMenu().Show(menuBtn, new Point(0, menuBtn.Height));
        else if (n == "cardmenu" && c != null) CardMenu(c).Show(c.More, new Point(0, c.More.Height));
        else if (n == "setup" && c != null) SetupHelp(c);
        else if (n == "portable" && c != null) MakePortable(c);
        else if (n == "remove" && c != null) RemoveFork(c);
        else if (n == "update" && c != null) CheckOne(c, delegate { if (c.Rel != null) DoUpdate(c); });
        else if (n == "log") ShowLog();
        else if (n == "main") { }   // just the main window
        else if ((n == "versions" || n == "versionsopen") && c != null) ChooseVersion(c);
    }

    void ShowLog()
    {
        if (logForm == null || logForm.IsDisposed)
        {
            logForm = new Form();
            logForm.Text = "Log"; logForm.Font = Font; logForm.BackColor = Theme.Window; logForm.ForeColor = Theme.Text; logForm.Icon = Icon;
            logForm.ClientSize = new Size(D(760), D(480)); logForm.StartPosition = FormStartPosition.CenterParent; logForm.ShowInTaskbar = false;
            logForm.Controls.Add(logBox); logForm.Controls.Add(logCopy);
            StyleButton(logCopy); logCopy.BackColor = Theme.Window;
            Action lay = () => { logBox.SetBounds(D(12), D(12), logForm.ClientSize.Width - D(24), logForm.ClientSize.Height - D(64)); logCopy.SetBounds(logForm.ClientSize.Width - D(112), logForm.ClientSize.Height - D(44), D(100), D(32)); };
            logForm.Resize += delegate { lay(); }; lay();
            logForm.FormClosing += delegate (object s, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; logForm.Hide(); } };
            logForm.HandleCreated += delegate { Native.Apply(logForm.Handle, Theme.Dark, 0); Native.ControlTheme(logBox.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer"); };
        }
        logBox.BackColor = Theme.Input; logBox.ForeColor = Theme.Text; logBox.BorderStyle = BorderStyle.FixedSingle;
        if (!logForm.Visible) logForm.Show(this);
        logForm.Activate();
        logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret();
    }

    ContextMenuStrip MakeMenu(params object[] items)   // items: "text", Action, ... ; null = separator
    {
        ContextMenuStrip m = new ContextMenuStrip();
        m.Renderer = new ToolStripProfessionalRenderer(new MenuColors());
        m.Font = Font; m.ShowImageMargin = false;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) { m.Items.Add(new ToolStripSeparator()); continue; }
            string t = (string)items[i]; Action a = (Action)items[++i];
            ToolStripMenuItem it = new ToolStripMenuItem(t); it.ForeColor = Theme.Text; it.Padding = new Padding(D(4), D(3), D(4), D(3));
            it.Click += delegate { a(); };
            m.Items.Add(it);
        }
        m.Closed += delegate { BeginInvoke((MethodInvoker)delegate { m.Dispose(); }); };
        return m;
    }

    ContextMenuStrip MainMenu()
    {
        List<object> items = IsBusy ? new List<object>() : new List<object> { "Add from link...", (Action)AddFork, "Shared data...", (Action)SharedDialog, null };
        items.AddRange(new object[] {
            "Check for updates", (Action)delegate { CheckAll(true); }, "Show log", (Action)ShowLog, "Open install folder", (Action)delegate { Explore(Core.Home); } });
        if (Core.Cfg.Sandbox) { items.Add(null); items.Add("Reset test"); items.Add((Action)ResetSandbox); }
        ContextMenuStrip m = MakeMenu(items.ToArray());
        // which manager this is (the same number Setup and Settings > Apps show)
        Version v = typeof(EmuForm).Assembly.GetName().Version;
        ToolStripMenuItem ver = new ToolStripMenuItem("Version " + v.Major + "." + v.Minor + "." + v.Build);
        ver.Enabled = false; ver.Padding = new Padding(D(4), D(3), D(4), D(3));
        m.Items.Add(new ToolStripSeparator()); m.Items.Add(ver);
        return m;
    }

    ContextMenuStrip CardMenu(EmuCard c)
    {
        EmuDef d = c.Def;
        bool inst = Installed(d);
        List<object> items = new List<object>();
        bool free = !IsBusy;
        if (c.Rel != null) { items.Add("What's new"); items.Add((Action)delegate { ShowNotes(c); }); }
        if (free) { items.Add("Choose version..."); items.Add((Action)delegate { ChooseVersion(c); }); }
        if (free && inst && UserData.IsPortable(d)) { items.Add("Set up keys and firmware"); items.Add((Action)delegate { SetupHelp(c); }); }
        if (free && inst && !UserData.IsPortable(d)) { items.Add("Keep its saves in its own folder"); items.Add((Action)delegate { MakePortable(c); }); }
        if (free && inst) { items.Add("Shared data..."); items.Add((Action)SharedDialog); }
        if (free && Core.Backups(d).Count > 0) { items.Add("Go back to previous version"); items.Add((Action)delegate { DoRestore(c); }); }
        if (free && !inst) { items.Add("I already have it"); items.Add((Action)delegate { UseExisting(c); }); }
        items.Add(null);
        items.Add("Open folder"); items.Add((Action)delegate { Explore(Directory.Exists(d.Dir) ? d.Dir : Path.GetDirectoryName(d.Dir.TrimEnd('\\'))); });
        items.Add("Open website"); items.Add((Action)delegate
        {
            string u = c.Rel != null && c.Rel.Url != null ? c.Rel.Url : d.Host == "gitea" ? d.ApiBase.Replace("/api/v1", "") + "/" + d.Repo + "/releases" : "https://github.com/" + d.Repo + "/releases";
            try { Process.Start(u); } catch { }
        });
        items.Add("Edit note"); items.Add((Action)delegate { EditNote(c); });
        if (free) { items.Add(null); items.Add("Remove from list"); items.Add((Action)delegate { RemoveFork(c); }); }
        return MakeMenu(items.ToArray());
    }

    // Places buttons left to right from x0, wrapping at maxX; returns the y of the last row.
    int Flow(Control[] bs, int[] widths, int x0, int y, int maxX)
    {
        int x = x0;
        for (int i = 0; i < bs.Length; i++)
        {
            int w = D(widths[i]);
            if (x > x0 && x + w > maxX) { x = x0; y += D(42); }
            bs[i].SetBounds(x, y, w, D(34));
            x += w + D(8);
        }
        return y;
    }

    void DoLayout()
    {
        if (WindowState == FormWindowState.Minimized) return;
        int cw = ClientSize.Width, ch = ClientSize.Height;
        SuspendLayout();
        int px = D(20);
        int pw = Math.Max(D(260), cw - px - D(38));   // page width (room kept for the cards' scroll bar)
        buildsPage.SetBounds(px, 0, cw - px, ch);

        // header: logo + title left, Add emulator and "..." right
        logoBox.SetBounds(0, D(16), D(36), D(36));
        addEmu.SetBounds(pw - D(226), D(18), D(170), D(34));
        menuBtn.SetBounds(pw - D(48), D(18), D(48), D(34));
        head.SetBounds(D(46), D(16), Math.Max(D(60), pw - D(280)), D(36));
        {   // credit on its own small line under the title
            credit.Font = Font; credit.Height = D(20);
            int crw; using (Graphics g = CreateGraphics()) crw = credit.PreferredWidth(g);
            credit.SetBounds(D(48), D(52), crw, D(20));
            credit.BackColor = buildsPage.BackColor; credit.Invalidate();
        }
        int y = D(82);

        // games folder: one line
        gamesLbl.SetBounds(D(16), D(12), pw - D(160), D(26));
        gamesPick.SetBounds(pw - D(128), D(8), D(112), D(34));
        gamesCard.SetBounds(0, y, pw, D(50)); gamesCard.Invalidate();
        y += D(50) + D(12);

        // status line at the bottom
        int fy = ch - D(34);
        int lw = TextRenderer.MeasureText(logLink.Text, Font).Width + D(6), cwid = TextRenderer.MeasureText(checkLink.Text, Font).Width + D(6);
        logLink.SetBounds(pw - lw, fy, lw, D(22));
        checkLink.SetBounds(pw - lw - D(14) - cwid, fy, cwid, D(22));
        footer.SetBounds(0, fy, Math.Max(D(40), pw - lw - cwid - D(30)), D(22));

        cardsHost.SetBounds(0, y, pw + D(18), Math.Max(D(60), fy - D(8) - y));
        int cy = cardsHost.AutoScrollPosition.Y;
        foreach (EmuCard c in cards) cy += LayoutCard(c, cy, pw) + D(12);
        emptyCard.Visible = cards.Count == 0;
        checkLink.Visible = cards.Count > 0;
        if (cards.Count == 0)
        {
            emptyText.SetBounds(D(24), D(26 + S), pw - D(48), D(24));
            emptyRec.SetBounds(D(24), D(58 + S), D(170), D(36));
            int fw = TextRenderer.MeasureText(emptyFork.Text, Font).Width + D(6);
            emptyFork.SetBounds(D(210), D(66 + S), fw, D(22));
            emptyCard.SetBounds(0, cy, pw, D(116)); emptyCard.Invalidate();
        }
        ResumeLayout();
    }

    // One card at width w; returns its height. The card's children are given Card.Shift (S) by hand here.
    int LayoutCard(EmuCard c, int y, int w)
    {
        c.More.SetBounds(w - D(60), D(8), D(44), D(30));
        {   // installed version, right of the title, left of "..."; hidden when the title needs the room
            int vw = TextRenderer.MeasureText(c.Ver.Text, c.Ver.Font).Width + D(4);
            int titleEnd = 24 + TextRenderer.MeasureText(c.Def.Name ?? "", Theme.CardTitle).Width;
            int vx = c.More.Left - D(10) - vw;
            c.Ver.Visible = c.Ver.Text.Length > 0 && vx > titleEnd + D(12);
            c.Ver.SetBounds(vx, D(8), vw, D(22));
        }
        int ly = D(22 + S);
        Action<Control, bool> row = (ctl, show) =>
        {
            ctl.Visible = show;
            if (show) { ctl.SetBounds(D(24), ly, w - D(48), D(22)); ly += D(24); }
        };
        row(c.Note, c.Note.Text.Length > 0);
        if (c.Note.Visible) c.Note.Width = w - D(96);   // leave room for the "..." button
        row(c.State, true);
        row(c.Data, c.Data.Text.Length > 0);
        c.Next.Visible = c.NextKind != null && !c.Busy && !IsBusy;
        if (c.Next.Visible) { c.Next.SetBounds(D(24), ly, Math.Min(w - D(48), TextRenderer.MeasureText(c.Next.Text, Font).Width + D(8)), D(22)); ly += D(24); }
        if (c.Bar.Visible) { c.Bar.SetBounds(D(24), ly + D(4), w - D(48), D(8)); ly += D(16); }
        ly += D(8);
        List<Control> bs = new List<Control>(); List<int> ws = new List<int>();
        if (c.Launch.Visible) { bs.Add(c.Launch); ws.Add(130); }
        if (c.Update.Visible) { bs.Add(c.Update); ws.Add(130); }
        int last = Flow(bs.ToArray(), ws.ToArray(), D(24), ly, w - D(16));
        if (c.Existing.Visible)
        {
            int ew = TextRenderer.MeasureText(c.Existing.Text, Font).Width + D(6);
            int ex = bs.Count > 0 ? bs[bs.Count - 1].Right + D(16) : D(24);
            if (ex + ew > w - D(16)) { ex = D(24); last += D(42); }
            c.Existing.SetBounds(ex, last + D(8), ew, D(22));
        }
        int h = last + D(34) + D(18);
        c.Box.SetBounds(0, y, w, h);
        c.Box.Invalidate();
        return h;
    }

    EmuCard MakeCard(EmuDef d)
    {
        EmuCard c = new EmuCard(); c.Def = d;
        c.Box.Text = d.Name;
        c.Note.AutoEllipsis = true; roles[c.Note] = "muted";
        c.State.AutoEllipsis = true; strong.Add(c.State);
        c.Data.AutoEllipsis = true; roles[c.Data] = "muted";
        c.Ver.AutoEllipsis = true; c.Ver.TextAlign = ContentAlignment.MiddleRight; roles[c.Ver] = "muted";
        c.Bar.Visible = false;
        c.Update.Text = "Install"; c.Launch.Text = "Launch"; c.More.Text = "...";
        Outline(c.Update, Theme.Accent); Outline(c.Launch, Theme.Accent);
        tips.SetToolTip(c.More, "More");
        c.Existing.Text = "I already have it";
        c.Box.Controls.AddRange(new Control[] { c.Note, c.More, c.State, c.Data, c.Next, c.Bar, c.Launch, c.Update, c.Existing, c.Ver });
        cardsHost.Controls.Add(c.Box);

        c.Update.Click += delegate { DoUpdate(c); };
        c.Launch.Click += delegate { DoLaunch(c); };
        c.More.Click += delegate { CardMenu(c).Show(c.More, new Point(0, c.More.Height)); };
        c.Existing.LinkClicked += delegate { UseExisting(c); };
        c.Next.LinkClicked += delegate { if (c.NextKind == "portable") MakePortable(c); else SetupHelp(c); };
        c.State.Click += delegate { if ((c.Err ?? c.CheckErr) != null) ShowLog(); };
        Refresh(c);
        return c;
    }

    static void Explore(string dir) { try { Process.Start("explorer.exe", "\"" + dir + "\""); } catch { } }

    // ---- state shown on a card: one status line, plus a line about shared data and one "next step" link when needed
    void Refresh(EmuCard c)
    {
        EmuDef d = c.Def;
        c.Note.Text = string.IsNullOrEmpty(d.Note) ? "" : "Best for: " + d.Note;
        Manifest m = Core.LoadManifest(d.Dir);
        bool installed = Installed(d);
        c.Ver.Text = installed ? "Version " + (m != null && !string.IsNullOrEmpty(m.Tag) ? m.Tag : "unknown") : "";
        bool current = installed && m != null && c.Rel != null && m.Tag == c.Rel.Tag;
        bool update = installed && c.Rel != null && !current;
        List<KeyValuePair<string, string>> other = c.Busy ? new List<KeyValuePair<string, string>>() : Core.OtherInstalls(d);
        if (c.Busy) { }
        else if ((c.Err ?? c.CheckErr) != null) { c.State.Text = c.Err ?? c.CheckErr; roles[c.State] = "danger"; }
        else if (!installed && other.Count > 0) { c.State.Text = "Already on this PC."; roles[c.State] = "info"; }
        else if (!installed) { c.State.Text = "Not installed"; roles[c.State] = "muted"; }
        else if (other.Count > 0) { c.State.Text = "Installed in two places."; roles[c.State] = "danger"; }
        else if (c.Rel == null) { c.State.Text = "Installed"; roles[c.State] = "muted"; }
        else if (current) { c.State.Text = "Up to date"; roles[c.State] = "success"; }
        else { c.State.Text = "Update available"; roles[c.State] = "info"; }
        c.State.Cursor = (c.Err ?? c.CheckErr) != null ? Cursors.Hand : Cursors.Default;
        c.State.ForeColor = RoleColor(c.State);
        tips.SetToolTip(c.State, (c.Err ?? c.CheckErr) != null ? "Click to see details." : installed ? "Version: " + (m != null ? m.Tag : "unknown") : null);

        bool portable = installed && UserData.IsPortable(d);
        List<UserData.Item> shared = portable ? UserData.SharedItems(d) : new List<UserData.Item>();
        c.Data.Text = shared.Count > 0 ? "Shared with your other emulators: " + string.Join(", ", shared.Select(i => i.Label.ToLower())) : "";
        c.NextKind = !installed || c.Busy ? null : !portable ? "portable" : NoReminder(d) ? null : !HasKeys(d) ? "keys" : !HasFirmware(d) ? "firmware" : null;
        c.Next.Text = c.NextKind == "portable" ? "Keep its saves in its own folder" : c.NextKind == "keys" ? "Next: add your keys" : c.NextKind == "firmware" ? "Next: add firmware" : "";

        c.Launch.Visible = installed;
        c.Launch.Enabled = !c.Busy && !IsBusy;
        // installed: always next to Launch; greyed "Up to date" until a newer version is out, then "Update"
        c.Update.Visible = true;
        c.Update.Text = !installed ? "Install" : current ? "Up to date" : "Update";
        c.Update.Enabled = !IsBusy && !c.Busy && c.Rel != null && (!installed || update);
        if (installed) accents.Remove(c.Update); else accents[c.Update] = Theme.Accent;   // one accent: Launch when installed
        c.Update.Invalidate();
        c.Existing.Visible = !installed && !c.Busy;
        c.Existing.Text = other.Count > 0 ? "Use it" : "I already have it";
        c.Existing.Enabled = !IsBusy;
        c.More.Enabled = !c.Busy;
        if (IsHandleCreated) BeginInvoke((MethodInvoker)DoLayout);
    }

    void RefreshAll() { foreach (EmuCard c in cards) Refresh(c); }

    void SetFooter(string text, Color color) { footer.Text = text; footer.ForeColor = color; }

    // What a failed release check tells the user: GitHub's hourly limit gets its own plain message.
    static string CheckFailed(string err, string otherwise = "Couldn't check for updates.")
    {
        return err != null && (err.Contains("hourly limit") || err.Contains("rate limit")) ? "GitHub is busy. Try again in a few minutes." : otherwise;
    }

    // ---- actions (one at a time, on a worker thread)
    // fresh: ask the hosts now (the user clicked Check for updates); otherwise lists from the last 10 minutes are used
    void CheckAll(bool fresh = false)
    {
        if (cards.Count == 0) { SetFooter("", Theme.Muted); return; }
        SetFooter("Checking for updates...", Theme.Muted);
        foreach (EmuCard c in cards) { c.CheckErr = null; c.Rel = null; Refresh(c); }
        int left = cards.Count;
        foreach (EmuCard c0 in cards)
        {
            EmuCard c = c0;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Release r = null; string err = null;
                try { r = Core.Latest(c.Def, fresh); } catch (Exception ex) { err = ex.Message; }
                UI(delegate
                {
                    c.Rel = r; c.CheckErr = err != null ? CheckFailed(err) : null; Refresh(c);
                    if (err != null) Log(c.Def.Name + ": update check failed: " + err);
                    if (--left == 0)
                    {
                        int upd = cards.Count(k => k.Rel != null && Installed(k.Def) && !Current(k));
                        if (cards.Any(k => k.CheckErr != null)) SetFooter(cards.First(k => k.CheckErr != null).CheckErr, Theme.Danger);
                        else if (upd > 0) SetFooter(upd == 1 ? "1 update available" : upd + " updates available", Theme.Info);
                        else if (cards.Any(k => Installed(k.Def))) SetFooter("Everything is up to date", Theme.Success);
                        else SetFooter("", Theme.Muted);
                        AskDuplicates();
                    }
                });
            });
        }
    }

    // Two installs of the same emulator: ask which one to keep using (once per session). Nothing is deleted.
    readonly HashSet<string> dupAsked = new HashSet<string>();
    bool dupRetry;
    void AskDuplicates()
    {
        if (IsBusy || !Enabled || OwnedForms.Any(o => o.Visible && o != logForm) || (Form.ActiveForm != null && Form.ActiveForm != this)) { dupRetry = true; return; }
        dupRetry = false;
        foreach (EmuCard c in cards.ToList())
        {
            if (!cards.Contains(c) || IsBusy || !Installed(c.Def) || !dupAsked.Add(c.Def.Id)) continue;
            List<KeyValuePair<string, string>> other = Core.OtherInstalls(c.Def);
            if (other.Count == 0) continue;
            KeyValuePair<string, string> o = other[0];
            string here = Path.GetFullPath(c.Def.Dir).TrimEnd('\\');
            Log(c.Def.Name + " is installed twice: " + here + " and " + o.Key + " (" + o.Value + ").");
            int r = Ask(c.Def.Name + " is installed twice", c.Def.Name + " is installed in two folders. Which one should the manager use?\r\n\r\n1. " + here + "\r\n2. " + o.Key +
                "\r\n\r\nThe other copy stays on your PC.", "Use 1", "Use 2", "Decide later");
            if (r != 0 && r != 1) continue;
            EmuCard twin = cards.FirstOrDefault(k => k != c && string.Equals(Path.GetFullPath(k.Def.Dir).TrimEnd('\\'), o.Key, StringComparison.OrdinalIgnoreCase));
            string drop = r == 0 ? o.Key : here;
            if (r == 1)
            {
                string bad = Core.FolderProblem(c.Def, o.Key);
                if (bad != null && twin == null) { Log("Can't switch " + c.Def.Name + " to " + o.Key + ": " + bad); Tell(APP, "That folder can't be used."); continue; }
                if (twin != null) { RemoveCardOnly(c); Log(c.Def.Name + ": kept " + o.Key + " (the other card)."); }
                else { c.Def.Dir = o.Key; try { Core.SaveConfig(); } catch { } Core.RememberInstall(c.Def, o.Value); Log(c.Def.Name + " now uses " + o.Key + "."); }
            }
            else if (twin != null) { RemoveCardOnly(twin); Log(c.Def.Name + ": kept " + here + "; the other card was taken off the list."); }
            else Log(c.Def.Name + ": kept " + here + "; " + o.Key + " is no longer tracked.");
            Core.ForgetInstall(drop);
            RefreshAll();
        }
    }

    // ---- keys / firmware / saves
    // Simple steps for keys and firmware. If another emulator has what this one is missing, offer to share it instead.
    static bool NoReminder(EmuDef d) { return Core.Cfg.NoSetupReminder != null && Core.Cfg.NoSetupReminder.Contains(d.Id); }

    void SetupHelp(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        EmuDef d = c.Def;
        bool keys = HasKeys(d), fw = HasFirmware(d);
        // a donor must have everything this one is missing (and it only ever fills those gaps)
        EmuCard donor = cards.Where(k => k != c && Installed(k.Def) && UserData.IsPortable(k.Def) && (keys || HasKeys(k.Def)) && (fw || HasFirmware(k.Def)))
                             .FirstOrDefault();
        string steps = "";
        if (!keys)
            steps += "Keys\r\n1. Get your key files (prod.keys and title.keys) from your own Switch.\r\n2. Click \"Open keys folder\".\r\n3. Copy the key files into that folder.\r\n\r\n";
        steps += "Firmware\r\n1. Get the firmware from your own Switch.\r\n2. Click Launch to open " + d.Name + ".\r\n3. In " + d.Name + ", open the Tools menu, choose Install Firmware, and pick the firmware." +
                 (fw ? "\r\n\r\nFirmware is already installed." : "");
        List<string> btn = new List<string>();
        if (donor != null && (!keys || !fw)) btn.Add("Use the ones from " + donor.Def.Name);
        if (!keys) btn.Add("Open keys folder");
        bool quiet = NoReminder(d);
        if (!keys || !fw) btn.Add(quiet ? "Remind me again" : "Don't remind me");
        btn.Add("Close");
        int r = Ask("Set up " + d.Name, steps.TrimEnd(), btn.ToArray());
        if (r < 0 || btn[r] == "Close") { RefreshAll(); return; }
        if (btn[r] == "Don't remind me" || btn[r] == "Remind me again")
        {   // only the line on the card; this window stays in the "..." menu
            List<string> ids = Core.Cfg.NoSetupReminder ?? new List<string>();
            ids.RemoveAll(x => x == d.Id);
            if (!quiet) ids.Add(d.Id);
            Core.Cfg.NoSetupReminder = ids.Count > 0 ? ids : null;
            try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save the reminder choice: " + ex.Message); }
            Log(d.Name + ": keys/firmware reminder " + (quiet ? "on" : "off") + ".");
            Refresh(c);
            return;
        }
        if (btn[r] == "Open keys folder")
        {
            string kd = Path.Combine(d.Dir, "user", "keys");
            try { Directory.CreateDirectory(kd); } catch { }
            Explore(kd);
            return;
        }
        // share what this one is missing; everything already shared stays shared (other items are asked about as usual)
        List<string> want = UserData.Items.Where(i => cards.Any(k => Installed(k.Def) && UserData.StateOf(k.Def, i) == UserData.State.On)).Select(i => i.Key).ToList();
        List<string> fill = new List<string>();
        if (!keys) fill.Add("keys");
        if (!fw) fill.Add("firmware");
        foreach (string kk in fill) if (!want.Contains(kk)) want.Add(kk);
        ApplyShared(want, donor, fill);
    }

    void MakePortable(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        EmuDef d = c.Def;
        string from = UserData.AppDataSource(d);
        if (from != null)
        {
            string q = "Copy " + d.Name + "'s saves and settings into its own folder? The originals stay where they are." +
                       (UserData.SourceIsShared(d, from) ? "\r\n\r\nThey are in the \"" + Path.GetFileName(from) + "\" folder, which other emulators may use too." : "");
            if (Ask("Keep saves in its own folder", q, "Copy", "Cancel") != 0) return;
        }
        else if (Ask("Keep saves in its own folder", "No saves or settings were found for " + d.Name + ". Give it an empty folder of its own? It will start like a new install.", "Continue", "Cancel") != 0) return;
        Work(c, "Copying saves and settings", (log, prog) => { log(UserData.MakePortable(d, from, log)); }, delegate { JoinSharing(c); });
    }

    // An emulator that now keeps its own data joins what the others share (the user is asked when data differs).
    // c == null: check every emulator. If something is running, it waits and runs when the work is done.
    bool joinPending;
    void JoinSharing(EmuCard c)
    {
        if (Core.Cfg.ShareNew == null || Core.Cfg.ShareNew.Count == 0) return;
        List<UserData.Item> want = UserData.Items.Where(i => Core.Cfg.ShareNew.Contains(i.Key)).ToList();
        bool needed = cards.Where(k => c == null || k == c).Any(k => Installed(k.Def) && UserData.IsPortable(k.Def) && want.Any(i => UserData.StateOf(k.Def, i) == UserData.State.Off));
        if (!needed) return;
        if (IsBusy || installQueue.Count > 0 || addingFound > 0) { joinPending = true; return; }
        BeginInvoke((MethodInvoker)delegate
        {
            if (IsBusy || installQueue.Count > 0 || addingFound > 0) { joinPending = true; return; }
            ApplyShared(Core.Cfg.ShareNew.ToList(), null, null);
        });
    }

    // Shared data: which items all installed emulators share. Ticking links each emulator's copy to one shared folder;
    // unticking gives each emulator its own copy again. Nothing is deleted.
    void SharedDialog()
    {
        if (IsBusy) { PleaseWait(); return; }
        List<EmuCard> inst = cards.Where(k => Installed(k.Def)).ToList();
        using (Form f = new Form())
        {
            f.Text = "Shared data"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            int W = D(440), y = D(16);
            Label l = new Label(); l.Text = "Ticked items are the same in all your emulators. Settings stay separate."; l.SetBounds(D(20), y, W - D(40), D(40)); f.Controls.Add(l); y += D(44);
            List<KeyValuePair<UserData.Item, CheckBox>> boxes = new List<KeyValuePair<UserData.Item, CheckBox>>();
            foreach (UserData.Item it in UserData.Items)
            {
                CheckBox cb = new CheckBox(); cb.Text = it.Label; cb.SetBounds(D(20), y, W - D(40), D(28)); cb.ForeColor = Theme.Text; cb.BackColor = Theme.Window;
                List<EmuCard> port = inst.Where(k => UserData.IsPortable(k.Def)).ToList();
                bool on = port.Count > 0 ? port.Any(k => UserData.StateOf(k.Def, it) != UserData.State.Off)
                                         : Core.Cfg.ShareNew != null && Core.Cfg.ShareNew.Contains(it.Key);
                cb.Checked = on;
                f.Controls.Add(cb); StyleCheck(cb);
                boxes.Add(new KeyValuePair<UserData.Item, CheckBox>(it, cb));
                y += D(32);
            }
            y += D(8);
            Label note = new Label(); note.ForeColor = Theme.Muted; note.SetBounds(D(20), y, W - D(40), D(40)); f.Controls.Add(note);
            List<EmuCard> skipped = inst.Where(k => !UserData.IsPortable(k.Def)).ToList();
            note.Text = skipped.Count > 0 ? string.Join(", ", skipped.Select(k => k.Def.Name)) + (skipped.Count == 1 ? " keeps its" : " keep their") + " saves outside its folder, so it isn't included."
                                          : "Only one emulator can be open at a time when saves are shared.";
            if (skipped.Count > 1) note.Text = string.Join(", ", skipped.Select(k => k.Def.Name)) + " keep their saves outside their folders, so they aren't included.";
            y += D(48);
            Button ok = new Button(); ok.Text = "Save"; ok.SetBounds(W - D(212), y, D(96), D(34)); ok.DialogResult = DialogResult.OK; f.Controls.Add(ok);
            Button no = new Button(); no.Text = "Cancel"; no.SetBounds(W - D(108), y, D(88), D(34)); no.DialogResult = DialogResult.Cancel; f.Controls.Add(no);
            f.AcceptButton = ok; f.CancelButton = no;
            StyleButton(ok); StyleButton(no); ok.BackColor = no.BackColor = Theme.Window; Outline(ok, Theme.Accent);
            f.ClientSize = new Size(W, y + D(34) + D(16));
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
            if (f.ShowDialog(this) != DialogResult.OK) return;
            ApplyShared(boxes.Where(b => b.Value.Checked).Select(b => b.Key.Key).ToList(), null, null);
        }
    }

    // Makes every installed (portable) emulator share exactly the items in want, and new emulators too. When emulators
    // hold different data for an item, the user picks whose to keep, newest first; for the items in preferFor, prefer's
    // copy is used without asking (it fills what another emulator is missing). The choice is saved only when it goes ahead.
    void ApplyShared(List<string> want, EmuCard prefer, ICollection<string> preferFor)
    {
        if (IsBusy) { PleaseWait(); return; }
        List<EmuCard> inst = cards.Where(k => Installed(k.Def) && UserData.IsPortable(k.Def)).ToList();
        Action saveChoice = () =>
        {
            Core.Cfg.ShareNew = want.Count > 0 ? want.ToList() : null;
            try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save the shared data choice: " + ex.Message); }
            Log("Shared data: " + (want.Count > 0 ? string.Join(", ", want) : "nothing"));
        };
        if (inst.Count == 0) { saveChoice(); RefreshAll(); return; }
        List<string> open = inst.Where(k => Core.Running(k.Def).Count > 0).Select(k => k.Def.Name).ToList();
        if (open.Count > 0) { Tell("Shared data", "Close " + string.Join(" and ", open) + " first."); return; }
        // whose copy to keep, per item, decided up front on the UI thread (nothing else starts meanwhile)
        Dictionary<string, EmuCard> keep = new Dictionary<string, EmuCard>();
        asking++;
        try
        {
        foreach (UserData.Item it in UserData.Items.Where(i => want.Contains(i.Key)))
        {
            List<EmuCard> holders = inst.Where(k => UserData.StateOf(k.Def, it) != UserData.State.On && UserData.OwnData(k.Def, it)).ToList();
            bool sharedHas = UserData.SharedHasData(it);
            List<EmuCard> distinct = UserData.DistinctHolders(it, holders.Select(h => h.Def).ToList()).Select(def => holders.First(h => h.Def == def)).ToList();
            if (distinct.Count == 0) continue;
            if (distinct.Count == 1 && !sharedHas) { keep[it.Key] = distinct[0]; continue; }
            if (prefer != null && preferFor != null && preferFor.Contains(it.Key) && distinct.Contains(prefer)) { keep[it.Key] = prefer; continue; }
            // newest first, so the default button is the copy changed last
            List<KeyValuePair<string, EmuCard>> cand = distinct.Select(h => new KeyValuePair<string, EmuCard>(h.Def.Name + "'s", h)).ToList();
            if (sharedHas) cand.Add(new KeyValuePair<string, EmuCard>("the shared ones", null));
            cand = cand.OrderByDescending(p => UserData.LastChanged(it, p.Value != null ? p.Value.Def : null)).ToList();
            List<string> opts = cand.Select((p, i) => "Use " + p.Key + (i == 0 ? " (newest)" : "")).ToList();
            opts.Add("Cancel");
            int r = Ask("Shared data", "Your emulators have different " + it.Label.ToLower() + ". Which should all of them use?\r\n\r\nThe others are kept in the Shared data folder, not deleted.", opts.ToArray());
            if (r < 0 || r == opts.Count - 1) return;
            keep[it.Key] = cand[r].Value;
        }
        }
        finally { asking--; }
        if (IsBusy) { PleaseWait(); return; }
        EmuCard first = inst[0];
        List<string> skipped = new List<string>();
        Work(first, "Setting up shared data", (log, prog) =>
        {
            foreach (UserData.Item it in UserData.Items)
            {
                bool on = want.Contains(it.Key);
                EmuCard k0; keep.TryGetValue(it.Key, out k0);
                IEnumerable<EmuCard> order = k0 != null ? new[] { k0 }.Concat(inst.Where(k => k != k0)) : inst;
                foreach (EmuCard k in order)
                {
                    UserData.State st = UserData.StateOf(k.Def, it);
                    if (UserData.LinkedElsewhere(k.Def, it)) { log(k.Def.Name + ": " + it.Label + " is linked somewhere else by hand; left as it is."); skipped.Add(k.Def.Name); continue; }
                    if (on && st != UserData.State.On) UserData.Share(k.Def, it, k == k0 ? "build" : "shared", log);
                    else if (!on && st != UserData.State.Off) UserData.Unshare(k.Def, it, log);
                }
            }
        }, delegate
        {
            saveChoice();
            first.State.Text = skipped.Count > 0 ? "Shared data is set up, except " + string.Join(", ", skipped.Distinct()) + "." : "Shared data is set up.";
            roles[first.State] = "success"; first.State.ForeColor = RoleColor(first.State);
        });
    }

    void RemoveCardOnly(EmuCard c)
    {
        Core.Cfg.Emulators.Remove(c.Def);
        try { Core.SaveConfig(); } catch { }
        cards.Remove(c); cardsHost.Controls.Remove(c.Box); c.Box.Dispose();
        DoLayout();
    }

    // ---- forks: add one from a repository link, or take one off the list
    void AddFork()
    {
        if (IsBusy) { PleaseWait(); return; }
        Core.RepoInfo info = null;
        using (Form f = new Form())
        {
            f.Text = "Add from link"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            Func<string, int, Label> lab = (t, yy) => { Label l = new Label(); l.Text = t; l.SetBounds(D(16), yy, D(568), D(20)); l.ForeColor = Theme.Text; f.Controls.Add(l); return l; };
            Func<int, int, int, TextBox> box = (xx, yy, ww) => { TextBox t = new TextBox(); t.SetBounds(xx, yy, ww, D(24)); t.BackColor = Theme.Input; t.ForeColor = Theme.Text; t.BorderStyle = BorderStyle.FixedSingle; f.Controls.Add(t); return t; };
            Func<int, ComboBox> combo = yy => { ComboBox c = new ComboBox(); c.DropDownStyle = ComboBoxStyle.DropDownList; c.SetBounds(D(16), yy, D(568), D(26)); c.BackColor = Theme.Input; c.ForeColor = Theme.Text; c.Enabled = false; f.Controls.Add(c); return c; };

            lab("Paste the emulator's web link:", D(12));
            TextBox url = box(D(16), D(34), D(458));
            Button look = new Button(); look.Text = "Find"; look.SetBounds(D(482), D(32), D(102), D(30)); f.Controls.Add(look);
            Label status = lab("", D(64)); status.ForeColor = Theme.Muted; status.AutoEllipsis = true;
            lab("Name", D(92)); TextBox name = box(D(16), D(112), D(568));
            lab("Best for (optional)", D(142)); TextBox note = box(D(16), D(162), D(568));
            LinkLabel adv = new LinkLabel(); adv.Text = "Advanced"; adv.SetBounds(D(16), D(196), D(120), D(20)); adv.LinkColor = adv.ActiveLinkColor = adv.VisitedLinkColor = Theme.Accent; adv.LinkBehavior = LinkBehavior.HoverUnderline; f.Controls.Add(adv);
            List<Control> advanced = new List<Control>();
            int c0 = f.Controls.Count;
            lab("Which releases", D(224)); ComboBox chan = combo(D(244));
            lab("Windows version", D(276)); ComboBox kind = combo(D(296));
            lab("Install folder", D(328)); TextBox dir = box(D(16), D(348), D(458));
            Button browse = new Button(); browse.Text = "Browse..."; browse.SetBounds(D(482), D(346), D(102), D(30)); f.Controls.Add(browse);
            for (int i = c0; i < f.Controls.Count; i++) advanced.Add(f.Controls[i]);
            Label warn = lab("", D(224)); warn.SetBounds(D(16), D(224), D(568), D(36)); warn.ForeColor = Theme.Muted;
            Button ok = new Button(); ok.Text = "Install"; ok.Enabled = false; f.Controls.Add(ok);
            Button no = new Button(); no.Text = "Cancel"; no.DialogResult = DialogResult.Cancel; f.Controls.Add(no);
            Action place = () =>
            {
                bool a = advanced[0].Visible;
                int wy = a ? D(380) : D(224);
                warn.SetBounds(D(16), wy, D(568), D(36));
                ok.SetBounds(D(388), wy + D(42), D(96), D(34)); no.SetBounds(D(490), wy + D(42), D(94), D(34));
                f.ClientSize = new Size(D(600), wy + D(42) + D(34) + D(16));
            };
            foreach (Control a0 in advanced) a0.Visible = false;
            place();
            adv.LinkClicked += delegate { bool v = !advanced[0].Visible; foreach (Control a0 in advanced) a0.Visible = v; adv.Text = v ? "Hide advanced" : "Advanced"; place(); };
            f.AcceptButton = look; f.CancelButton = no;
            foreach (Button b in new[] { look, browse, ok, no }) { StyleButton(b); b.BackColor = Theme.Window; }
            Outline(ok, Theme.Accent);
            f.HandleCreated += delegate
            {
                Native.Apply(f.Handle, Theme.Dark, 0);
                foreach (Control c in f.Controls) if (c is ComboBox || c is TextBox) Native.ControlTheme(c.Handle, Theme.Dark ? (c is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer") : "Explorer");
            };

            string root = Core.Home;   // like the recommended builds: <install folder>\<name> (always inside a test environment)
            bool dirTouched = false;
            dir.TextChanged += delegate { if (dir.Focused) dirTouched = true; };
            EventHandler checkDir = delegate
            {
                string d0 = dir.Text.Trim(), why = null;
                warn.Text = "";
                if (d0.Length == 0 || !Path.IsPathRooted(d0)) why = "Choose an install folder (Advanced).";
                else
                {
                    string full = "";
                    try { full = Path.GetFullPath(d0).TrimEnd('\\'); } catch { why = "This folder can't be used. Choose another one (Advanced)."; }
                    if (why == null)
                    {
                        string bad = Core.FolderProblem(new EmuDef(), full);   // other builds, manager folders, test-environment bounds
                        if (bad == null && string.Equals(full, Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) bad = "the install folder itself";
                        if (bad != null) { why = "This folder can't be used. Choose another one (Advanced)."; tips.SetToolTip(warn, bad); }
                        else if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) warn.Text = "This folder isn't empty.";
                    }
                }
                if (why != null) warn.Text = why;
                warn.ForeColor = why != null ? Theme.Danger : Theme.Muted;
                ok.Enabled = info != null && why == null && name.Text.Trim().Length > 0;
            };
            dir.TextChanged += checkDir; name.TextChanged += checkDir;
            name.TextChanged += delegate { if (!dirTouched && info != null) dir.Text = Path.Combine(root, SafeName(Core.DisplayName(new EmuDef { Name = name.Text.Trim(), Host = info.Host, Repo = info.Repo }))); };
            browse.Click += delegate
            {
                string picked = FolderPicker.Pick(f, "Choose an empty folder for this emulator", Directory.Exists(dir.Text) ? dir.Text : (Directory.Exists(root) ? root : Core.Home));
                if (picked != null) { dirTouched = true; dir.Text = picked; }
            };
            look.Click += delegate
            {
                string link = url.Text.Trim();
                if (link.Length == 0) return;
                look.Enabled = false; ok.Enabled = false; info = null;
                status.ForeColor = Theme.Muted; status.Text = "Looking...";
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Core.RepoInfo ri = null; string err = null;
                    try { ri = Core.Lookup(link); } catch (Exception ex) { err = ex.Message; }
                    try
                    {
                        f.BeginInvoke((MethodInvoker)delegate
                        {
                            look.Enabled = true;
                            if (err != null) { Log("Add from link: " + link + ": " + err); status.ForeColor = Theme.Danger; status.Text = "Nothing found at that link. Check it and try again."; tips.SetToolTip(status, err); return; }
                            EmuDef dup = Core.Cfg.Emulators.FirstOrDefault(x => string.Equals(x.Repo, ri.Repo, StringComparison.OrdinalIgnoreCase) && x.Host == ri.Host);
                            info = ri;
                            Log("Add from link: " + ri.Repo + ", " + ri.Channels.Sum(c => c.Count) + " Windows releases.");
                            status.ForeColor = dup != null ? Theme.Warn : Theme.Success;
                            status.Text = dup != null ? "Already on your list as " + dup.Name + "." : "Found " + ri.Name + ".";
                            chan.Items.Clear();
                            chan.Items.Add("All releases (newest: " + ri.Channels[0].Newest + ")");
                            foreach (Core.Channel c in ri.Channels)
                                if (c.Prefix.Length > 0)
                                    chan.Items.Add(c.Prefix + "*   " + c.Count + " release" + (c.Count == 1 ? "" : "s") + ", newest " + c.Newest + " (" + Core.ParseDate(c.NewestDate).ToLocalTime().ToString("yyyy-MM-dd") + ")");
                            List<Core.Channel> named = ri.Channels.Where(c => c.Prefix.Length > 0).ToList();
                            int sel = named.FindIndex(c => c.Prefix == ri.DefaultPrefix);
                            chan.SelectedIndex = ri.DefaultPrefix.Length > 0 && sel >= 0 ? sel + 1 : 0;
                            chan.Tag = named;
                            kind.Items.Clear();
                            if (ri.HasPgo) kind.Items.Add("Faster version (recommended)");
                            kind.Items.Add("Standard version");
                            kind.SelectedIndex = 0;
                            Log("Add from link: Windows files in " + ri.ZipsFrom + ": " + string.Join(", ", ri.Zips));
                            chan.Enabled = kind.Enabled = true;
                            if (name.Text.Trim().Length == 0 || !dirTouched) name.Text = ri.Name;
                            if (!dirTouched) dir.Text = Path.Combine(root, SafeName(Core.DisplayName(new EmuDef { Name = name.Text.Trim(), Host = ri.Host, Repo = ri.Repo })));
                            checkDir(null, EventArgs.Empty);
                            f.AcceptButton = ok;
                        });
                    }
                    catch { }   // dialog closed meanwhile
                });
            };
            ok.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
            if (f.ShowDialog(this) != DialogResult.OK || info == null) return;

            List<Core.Channel> chans = (List<Core.Channel>)chan.Tag;
            EmuDef e = new EmuDef();
            e.Name = name.Text.Trim(); e.Note = note.Text.Trim();
            e.Host = info.Host; e.ApiBase = info.ApiBase; e.Repo = info.Repo;
            e.Name = Core.DisplayName(e);   // "<name> (fork)", or "Eglantine Main" for the upstream repo
            e.TagPrefix = chan.SelectedIndex > 0 ? chans[chan.SelectedIndex - 1].Prefix : "";
            e.Assets = Core.AssetPatterns(info.HasPgo && kind.SelectedIndex == 0);
            e.Dir = Path.GetFullPath(dir.Text.Trim()).TrimEnd('\\');
            e.Exe = ""; e.Added = true;
            string id = SafeName(info.Repo.Replace('/', '-')).ToLowerInvariant().Replace(' ', '-');
            string baseId = id; int n = 2;
            // a removed fork's previous builds / downloads stay under its id: never reuse one
            while (Core.IdTaken(id)) id = baseId + "-" + n++;
            e.Id = id;
            Core.Cfg.Emulators.Add(e);
            try { Core.SaveConfig(); }
            catch (Exception ex) { Core.Cfg.Emulators.Remove(e); Log("Could not save the new fork: " + ex.Message); return; }
            Log("Added " + e.Name + " (" + info.WebUrl + ", channel " + (e.TagPrefix.Length > 0 ? e.TagPrefix + "*" : "all") + ") -> " + e.Dir);
            EmuCard card = MakeCard(e);
            cards.Add(card);
            SortCards();
            ApplyStyle(); DoLayout();
            cardsHost.ScrollControlIntoView(card.Box);
            CheckOne(card, delegate { if (card.Rel != null) StartUpdate(card, null); });   // the user pressed Install (queued if busy)
        }
    }

    // ---- existing installs: pick the folder, find out which release it is, confirm, adopt
    readonly Queue<EmuCard> installQueue = new Queue<EmuCard>();

    // Picks a folder for d, identifies it on a worker thread (progress -> status), then asks and adopts on the UI
    // thread. c is d's card, or null for a recommended build that isn't on the list yet (adopting adds it).
    // done(true) when d now uses that folder.
    // confirm false: the user already chose this folder (Welcome found the build in place); asked only when its
    // version isn't recognised.
    void PickExisting(IWin32Window owner, EmuDef d, EmuCard c, Action<string> status, Action<bool> done, string preset = null, bool confirm = true)
    {
        string pick;
        {
            string start = Core.Cfg.Sandbox ? Core.Home : Path.GetDirectoryName(Core.Home.TrimEnd('\\'));
            string picked = preset ?? FolderPicker.Pick(owner, "Choose the folder where " + d.Name + " is (the one with " + (string.IsNullOrEmpty(d.Exe) ? "its .exe" : d.Exe) + " in it)", start);
            if (picked == null) { done(false); return; }
            pick = Path.GetFullPath(picked).TrimEnd('\\');
        }
        // test-environment bounds, another build's folder (or inside one), previous-build / download folders
        string bad = Core.FolderProblem(d, pick);
        if (bad != null)
        {
            Log(d.Name + ": can't use " + pick + ": " + bad);
            AskOn(owner, d.Name, "This folder can't be used. Choose another one.", "OK");
            done(false); return;
        }
        string exe = d.Exe;
        if (string.IsNullOrEmpty(exe)) exe = Core.DetectExe(pick);   // fork added by link, never installed: take the folder's exe
        if (exe == null || !File.Exists(Path.Combine(pick, exe)))
        {
            Log(d.Name + ": no " + (exe ?? "emulator .exe") + " in " + pick);
            AskOn(owner, d.Name, d.Name + " wasn't found in this folder. Choose the folder that has " + (exe ?? "the emulator") + " in it.", "OK");
            done(false); return;
        }
        EmuDef probe = new EmuDef { Id = d.Id, Name = d.Name, Host = d.Host, ApiBase = d.ApiBase, Repo = d.Repo, TagPrefix = d.TagPrefix, Assets = d.Assets, Dir = pick, Exe = exe };
        // nothing else may start (an install on this card, another check) until this is answered
        if (IsBusy) { Log(d.Name + ": another task is running"); AskOn(owner, d.Name, "Please wait for the current task to finish.", "OK"); done(false); return; }
        identifying++; RefreshAll();
        Action<string> st = s => { try { status(s); } catch { } };    // the Recommended window may be gone by then
        st("Checking which version this is...");
        ThreadPool.QueueUserWorkItem(delegate
        {
            Core.Identified id = null; string err = null;
            try { id = Core.Identify(probe, pick, s => FileLog("identify: " + s)); }
            catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                Form of = owner as Form;
                IWin32Window own = of != null && (of.IsDisposed || !of.Visible) ? (IWin32Window)this : owner;
                try { PickExistingAnswer(own, d, c, pick, exe, id, err, st, done, confirm); }
                finally { identifying--; RefreshAll(); BeginInvoke((MethodInvoker)RunQueue); }
            });
        });
    }

    void PickExistingAnswer(IWin32Window owner, EmuDef d, EmuCard c, string pick, string exe, Core.Identified id, string err, Action<string> status, Action<bool> done0, bool confirm = true)
    {
        Action<bool> done = ok => { try { done0(ok); } catch { } };
        if (err != null)
        {   // no network etc.: the folder can still be used, just untracked
            id = new Core.Identified { Dir = pick, Def = d, Exe = exe, HasUser = Directory.Exists(Path.Combine(pick, "user")), Why = "couldn't check the version (" + err + ")" };
        }
        Log(d.Name + " in " + pick + ": " + (id.Rel != null ? id.Rel.Tag + " (" + id.Why + ")" : "version not recognised: " + id.Why) + (id.HasUser ? "; has its own user folder" : "; no user folder (uses %APPDATA%)"));
        string q = "Found " + d.Name + ". Use this folder?" + (id.Rel == null ? "\r\n\r\nIts version isn't known, so the next update replaces it with the latest one. Your saves are kept." : "");
        if ((confirm || id.Rel == null) && AskOn(owner, d.Name, q, "Use this folder", "Cancel") != 0) { status(""); done(false); return; }
        string oldExe = d.Exe, oldDir = d.Dir;
        EmuCard card;
        try
        {
            if (string.IsNullOrEmpty(d.Exe)) d.Exe = exe;
            d.Added = true;
            string msg = Core.Adopt(d, id);
            Log(msg); status("Added.");
            card = c ?? AddDef(d, null);   // recommended build: on the list from now on
            if (c != null) { Refresh(c); CheckOne(c, null); }
        }
        catch (Exception ex)
        {
            d.Exe = oldExe; d.Dir = oldDir;
            Log(d.Name + ": could not use that folder: " + ex.Message); status("That folder can't be used."); done(false);
            return;
        }
        done(true);   // once; what follows is extra and never undoes the adopt
        try
        {
            if (card != null && id.HasUser) JoinSharing(card);
            // it keeps its saves in %APPDATA%: offer to keep them in its own folder (copied, originals stay)
            if (card != null && !id.HasUser && Installed(d) && !UserData.IsPortable(d))
            {
                string from = UserData.AppDataSource(d);
                if (from != null && AskOn(owner, d.Name, d.Name + " keeps its saves and settings outside its folder, where other emulators can change them.\r\n\r\nCopy them into its own folder? The originals stay where they are.", "Copy", "Not now") == 0)
                    BeginInvoke((MethodInvoker)delegate { Work(card, "Copying saves and settings", (log, prog) => { log(UserData.MakePortable(d, from, log)); }, delegate { JoinSharing(card); }); });   // after IsBusy clears
            }
        }
        catch (Exception ex) { Log(d.Name + ": added, but its saves couldn't be checked: " + ex.Message); }
    }

    void UseExisting(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        string before = c.State.Text;
        PickExisting(this, c.Def, c, s => { c.State.Text = s.Length > 0 ? s : before; roles[c.State] = "info"; c.State.ForeColor = RoleColor(c.State); }, ok => { if (!ok) Refresh(c); });
    }

    // Puts a build on the list (a recommended one the user picked, or a new fork) and gives it a card.
    EmuCard AddDef(EmuDef d, Release rel)
    {
        d.Added = true;
        if (!Core.Cfg.Emulators.Contains(d)) Core.Cfg.Emulators.Add(d);
        try { Core.SaveConfig(); }
        catch (Exception ex) { Core.Cfg.Emulators.Remove(d); Log("Could not save " + d.Name + ": " + ex.Message); return null; }
        EmuCard card = MakeCard(d);
        card.Rel = rel;
        cards.Add(card);
        SortCards();
        ApplyStyle(); DoLayout(); Refresh(card);
        if (rel == null) CheckOne(card, null);
        Log("Added " + d.Name + " (" + d.Dir + ").");
        return card;
    }

    // The recommended builds that aren't on the list yet: tick to download, or point one at a build you already have.
    // First start shows it as the welcome; afterwards the Recommended button opens it. Only what the user picks gets a
    // card in the main window. Ticked builds install one after another.
    void ShowRecommended(bool first)
    {
        if (IsBusy) { PleaseWait(); return; }
        List<EmuDef> recs = Core.RecommendedNotAdded();
        if (recs.Count == 0 && !first)
        {
            if (Ask("Add emulator", "You already have all the recommended emulators.", "Add from link...", "Close") == 0) AddFork();
            return;
        }
        bool fromLink = false;
        using (Form f = new Form())
        {
            f.Text = first ? "Welcome" : "Add emulator"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            int W = D(600), y = D(14);
            Label t = new Label(); t.Text = first ? "Welcome" : "Add emulator"; t.Font = Theme.CardTitle; t.SetBounds(D(16), y, W - D(32), D(28)); f.Controls.Add(t); y += D(32);
            Label intro = new Label(); intro.SetBounds(D(16), y, W - D(32), D(22)); intro.ForeColor = Theme.Muted; f.Controls.Add(intro);
            intro.Text = "Tick the emulators you want, then click Install.";
            y += D(32);
            List<KeyValuePair<EmuDef, CheckBox>> picks = new List<KeyValuePair<EmuDef, CheckBox>>();
            Dictionary<EmuDef, Release> rels = new Dictionary<EmuDef, Release>();
            List<EmuDef> found = new List<EmuDef>();   // already installed in their own folder
            Button goRef = new Button();
            foreach (EmuDef d0 in recs)
            {
                EmuDef d = d0;
                Card row = new Card(); row.Text = ""; row.Shift = 0; row.SetBounds(D(16), y, W - D(32), D(70)); f.Controls.Add(row);
                CheckBox cb = new CheckBox(); cb.Text = d.Name; cb.Font = Theme.Title; cb.Checked = false; cb.Enabled = false;
                cb.SetBounds(D(14), D(10), D(330), D(26)); row.Controls.Add(cb);
                Label note = new Label(); note.SetBounds(D(40), D(38), W - D(90), D(20)); note.AutoEllipsis = true; note.ForeColor = Theme.Muted; row.Controls.Add(note);
                string best = string.IsNullOrEmpty(d.Note) ? "" : "Best for: " + d.Note;
                List<KeyValuePair<string, string>> known = Core.OtherInstalls(d);
                string knownDir = known.Count > 0 ? known[0].Key : null;
                // the build is already in the folder this one would install into: ticked means "add it", no download
                bool here = Installed(d) && Core.FolderProblem(d, d.Dir) == null;
                if (here) knownDir = null;
                note.Text = here ? "Already installed. It won't be downloaded again." : knownDir != null ? "Already on this PC." : best;
                if (here) { tips.SetToolTip(note, d.Dir); cb.Enabled = true; cb.Checked = first; }
                if (knownDir != null) { note.ForeColor = Theme.Warn; tips.SetToolTip(note, knownDir); }
                bool[] adopted = { false };   // not ex.Visible: that reads false until the window is on screen
                LinkLabel ex = new LinkLabel(); ex.Text = knownDir != null ? "Use it" : "I already have it"; ex.SetBounds(W - D(232), D(13), D(190), D(22)); ex.TextAlign = ContentAlignment.MiddleRight;
                ex.LinkColor = Theme.Accent; ex.ActiveLinkColor = Theme.Accent; ex.VisitedLinkColor = Theme.Accent; ex.LinkBehavior = LinkBehavior.HoverUnderline; row.Controls.Add(ex);
                ex.Visible = !here;
                if (here) found.Add(d);
                ex.LinkClicked += delegate
                {
                    ex.Enabled = false; f.Tag = (f.Tag as int? ?? 0) + 1; goRef.Enabled = false;   // no Install while a check runs
                    PickExisting(f, d, null, s => { if (s.Length > 0) { note.Text = s; note.ForeColor = Theme.Muted; } }, ok =>
                    {
                        f.Tag = (f.Tag as int? ?? 1) - 1; if ((int)f.Tag == 0) goRef.Enabled = true;
                        ex.Enabled = true;
                        if (ok) { adopted[0] = true; cb.Checked = false; cb.Enabled = false; ex.Visible = false; note.ForeColor = Theme.Success; note.Text = "Added."; }
                        else if (!adopted[0]) { note.Text = knownDir != null ? "Already on this PC." : best; note.ForeColor = knownDir != null ? Theme.Warn : Theme.Muted; }
                    }, knownDir);
                };
                foreach (Control k in row.Controls) k.BackColor = Theme.Card;
                cb.ForeColor = Theme.Text; StyleCheck(cb);
                picks.Add(new KeyValuePair<EmuDef, CheckBox>(d, cb));
                f.Shown += delegate { ThreadPool.QueueUserWorkItem(delegate
                {
                    Release r = null; string err = null;
                    try { r = Core.Latest(d); } catch (Exception e2) { err = e2.Message; }
                    try
                    {
                        if (!f.IsDisposed) f.BeginInvoke((MethodInvoker)delegate
                        {
                            if (here) { if (r != null) rels[d] = r; else Log(d.Name + ": update check failed: " + err); }   // added either way; the tick is the user's
                            else if (r != null) { rels[d] = r; if (!adopted[0]) { cb.Enabled = true; cb.Checked = first && knownDir == null; } }   // already on this PC: offer that, don't tick a second copy
                            else { Log(d.Name + ": update check failed: " + err); if (!adopted[0]) { note.Text = CheckFailed(err, "Can't connect right now."); note.ForeColor = Theme.Danger; } }
                        });
                    }
                    catch (InvalidOperationException) { }   // window closed meanwhile (ObjectDisposedException is one too)
                }); };
                y += D(78);
            }
            // games folder
            Card gr = new Card(); gr.Text = ""; gr.Shift = 0; gr.SetBounds(D(16), y, W - D(32), D(50)); f.Controls.Add(gr);
            Label gv = new Label(); gv.SetBounds(D(14), D(14), W - D(190), D(22)); gv.AutoEllipsis = true; gr.Controls.Add(gv);
            Button gb = new Button(); gb.SetBounds(W - D(152), D(8), D(104), D(34)); gr.Controls.Add(gb);
            Action showG = () =>
            {
                bool set = !string.IsNullOrEmpty(Core.Cfg.GamesFolder);
                gv.Text = set ? "Games: " + Core.Cfg.GamesFolder : "Choose the folder with your games (optional).";
                gb.Text = set ? "Change..." : "Choose...";
            };
            showG();
            gb.Click += delegate { PickGames(); showG(); };
            foreach (Control k in gr.Controls) { k.BackColor = Theme.Card; k.ForeColor = Theme.Text; }
            y += D(58);
            // first start: one tick for sharing keys, firmware and saves between the emulators it installs
            CheckBox share = new CheckBox(); share.Text = "Use the same keys, firmware and saves in all of them"; share.SetBounds(D(20), y, W - D(40), D(28));
            share.ForeColor = Theme.Text; share.BackColor = Theme.Window; share.Checked = true; share.Visible = first;
            f.Controls.Add(share); StyleCheck(share);
            Action setShare = () =>
            {
                if (!first) return;
                Core.Cfg.ShareNew = share.Checked ? UserData.Items.Where(i => i.Default).Select(i => i.Key).ToList() : null;
                try { Core.SaveConfig(); } catch { }
            };
            share.CheckedChanged += delegate { setShare(); };
            setShare();
            if (first) y += D(36);
            y += D(6);
            LinkLabel link = new LinkLabel(); link.Text = "Add from link..."; link.SetBounds(D(16), y + D(8), D(160), D(22));
            link.LinkColor = link.ActiveLinkColor = link.VisitedLinkColor = Theme.Accent; link.LinkBehavior = LinkBehavior.HoverUnderline; f.Controls.Add(link);
            link.LinkClicked += delegate { fromLink = true; f.DialogResult = DialogResult.Cancel; f.Close(); };
            Button go = goRef; go.Text = "Install"; go.SetBounds(W - D(250), y, D(118), D(36)); f.Controls.Add(go);
            Button later = new Button(); later.Text = first ? "Not now" : "Close"; later.SetBounds(W - D(124), y, D(108), D(36)); f.Controls.Add(later);
            go.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
            later.DialogResult = DialogResult.Cancel; f.CancelButton = later;
            foreach (Button b in new[] { go, later, gb }) { StyleButton(b); b.BackColor = b == gb ? Theme.Card : Theme.Window; }
            Outline(go, Theme.Accent);
            f.ClientSize = new Size(W, y + D(52));
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
            DialogResult dr = f.ShowDialog(this);
            if (first)
            {   // what new emulators share from the start (Shared data in the menu changes it later)
                setShare();
                Log("New emulators share: " + (share.Checked ? string.Join(", ", Core.Cfg.ShareNew) : "nothing"));
                JoinSharing(null);   // emulators added with "I already have it" in this window
            }
            if (fromLink) { BeginInvoke((MethodInvoker)AddFork); return; }
            if (dr != DialogResult.OK) { if (first) Log("Welcome: nothing installed for now."); return; }
            List<EmuDef> keep = picks.Where(p => p.Value.Checked && p.Value.Enabled && found.Contains(p.Key)).Select(p => p.Key).ToList();
            List<EmuDef> todo = picks.Where(p => p.Value.Checked && p.Value.Enabled && !found.Contains(p.Key) && rels.ContainsKey(p.Key)).Select(p => p.Key).ToList();
            if (keep.Count > 0) Log("Already installed, added without downloading: " + string.Join(", ", keep.Select(x => x.Name)) + ".");
            // the ones already in place first (one at a time: each checks its version), then the downloads
            AddFound(keep, delegate
            {
                if (todo.Count == 0) return;
                List<EmuCard> added = todo.Select(d => AddDef(d, rels[d])).Where(k => k != null).ToList();
                if (added.Count == 0) return;
                Log("Installing " + string.Join(", ", added.Select(x => x.Def.Name)) + ".");
                foreach (EmuCard c in added) installQueue.Enqueue(c);
                RunQueue();
            });
        }
    }

    // Puts builds that are already in their folder on the list, as "I already have it" would (no folder question
    // when the version is recognised). They join shared data when everything is done.
    int addingFound;   // AddFound running: shared data waits until all of them are on the list
    void AddFound(List<EmuDef> list, MethodInvoker then)
    {
        if (list.Count == 0) { then(); return; }
        addingFound++;
        AddFound(list, 0, then);
    }
    void AddFound(List<EmuDef> list, int i, MethodInvoker then)
    {
        if (i >= list.Count)
        {
            addingFound--;
            then();
            JoinSharing(null);   // waits for the downloads, if any
            dupRetry = true; BeginInvoke((MethodInvoker)AskDuplicates);   // one of them may also be in another folder
            return;
        }
        if (IsBusy) { BeginInvoke((MethodInvoker)delegate { WhenIdle(() => AddFound(list, i, then)); }); return; }
        EmuDef d = list[i];
        if (Core.Cfg.Emulators.Any(x => string.Equals(x.Repo, d.Repo, StringComparison.OrdinalIgnoreCase) && x.Host == d.Host)) { AddFound(list, i + 1, then); return; }
        // its card shows right away, with the version check on it; taken off again if the user says no
        EmuCard card = AddDef(d, null);
        if (card == null) { AddFound(list, i + 1, then); return; }
        card.Busy = true; Refresh(card);   // keeps the status line and hides its buttons until the check is done
        string[] last = { "" };   // the last status: "" after Cancel, a message after a failure
        bool[] fired = { false };
        PickExisting(this, d, card, s =>
        {
            last[0] = s;
            if (!cards.Contains(card)) return;
            if (s.Length > 0 && s != "Added.") { card.State.Text = s; roles[card.State] = "info"; card.State.ForeColor = RoleColor(card.State); }
        }, ok =>
        {
            if (fired[0]) return;
            fired[0] = true;
            card.Busy = false;
            if (!ok && cards.Contains(card))
            {
                Log(d.Name + ": not added.");
                RemoveCardOnly(card);
                if (last[0].Length > 0) SetFooter(d.Name + " couldn't be added. See the log.", Theme.Danger);   // not shown after Cancel
            }
            else if (cards.Contains(card)) Refresh(card);
            BeginInvoke((MethodInvoker)delegate { AddFound(list, i + 1, then); });   // after this check has finished
        }, d.Dir, false);
    }

    // Runs a step once nothing is busy (checked every half second; the window stays usable).
    void WhenIdle(MethodInvoker step)
    {
        if (!IsBusy) { step(); return; }
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 500;
        t.Tick += delegate { if (IsBusy || IsDisposed) { if (IsDisposed) t.Dispose(); return; } t.Stop(); t.Dispose(); step(); };
        t.Start();
    }

    // Eglantine Main first, the others in the order they were added (OrderBy is stable).
    void SortCards()
    {
        List<EmuCard> sorted = cards.OrderBy(k => Core.IsMainRepo(k.Def) ? 0 : 1).ToList();
        cards.Clear(); cards.AddRange(sorted);
    }

    static string SafeName(string s)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
        s = s.Trim().Trim('.');
        return s.Length == 0 ? "New fork" : s;
    }

    void CheckOne(EmuCard c, MethodInvoker after)
    {
        c.CheckErr = null; c.Rel = null; Refresh(c);
        ThreadPool.QueueUserWorkItem(delegate
        {
            Release r = null; string err = null;
            try { r = Core.Latest(c.Def); } catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                c.Rel = r; c.CheckErr = err != null ? CheckFailed(err) : null; Refresh(c);
                if (err != null) Log(c.Def.Name + ": update check failed: " + err);
                if (after != null) after();
            });
        });
    }

    void RemoveFork(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        if (Ask("Remove " + c.Def.Name, "Remove " + c.Def.Name + " from the list? Its files stay on your PC.", "Remove", "Cancel") != 0) return;
        Core.Cfg.Emulators.Remove(c.Def);
        try { Core.SaveConfig(); }
        catch (Exception ex) { Core.Cfg.Emulators.Add(c.Def); Log("Could not save: " + ex.Message); return; }
        cards.Remove(c);
        cardsHost.Controls.Remove(c.Box);
        c.Box.Dispose();
        Log("Removed " + c.Def.Name + " from the list (files kept in " + c.Def.Dir + ").");
        DoLayout();
    }

    // Runs job on a worker thread, one at a time. The card shows only short progress ("Downloading 40 of 85 MB",
    // "Installing..."); every detail goes to the log. On failure the card says so in plain words; the reason is in the log.
    void Work(EmuCard c, string what, Action<Action<string>, Action<long, long>> job, MethodInvoker after = null)
    {
        if (IsBusy) { PleaseWait(); return; }
        anyBusy = true; c.Busy = true; c.Err = null;
        c.State.Text = what + "..."; roles[c.State] = "info"; c.State.ForeColor = RoleColor(c.State);
        c.Bar.Set(0, 1); c.Bar.Visible = true;
        RefreshAll();
        Action<string> log = s => UI(delegate { Log(s); });
        Action<long, long> prog = (got, tot) => UI(delegate
        {
            c.Bar.Set(got, tot);
            c.State.Text = got >= tot && tot > 0 ? what + "..." : "Downloading " + (got / 1048576) + " of " + Math.Max(1, tot / 1048576) + " MB";
        });
        Thread t = new Thread(delegate ()
        {
            string err = null;
            try { job(log, prog); } catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                anyBusy = false; c.Busy = false; c.Bar.Visible = false;
                if (err != null)
                {
                    Log(c.Def.Name + ": " + what + " failed: " + err);
                    c.Err = err.StartsWith("Close the emulator first") ? "Close " + c.Def.Name + ", then try again."
                          : err.StartsWith("Close ") && err.EndsWith(" first.") ? err.Substring(0, err.Length - 7) + ", then try again."
                          : err.StartsWith("Not enough free space") || err.StartsWith("Sharing doesn't work") ? err.Split(new[] { " (" }, StringSplitOptions.None)[0]
                          : what == "Installing" ? "Install failed. Try again."
                          : what == "Updating" ? "Update failed. Try again."
                          : "That didn't work. Try again.";
                }
                RefreshAll();
                if (err == null && after != null) after();
                BeginInvoke((MethodInvoker)RunQueue);
                if (dupRetry) BeginInvoke((MethodInvoker)AskDuplicates);
            });
        });
        t.IsBackground = true; t.Start();
    }

    // Install: no question (the user pressed Install). Update: a short confirm with what's new.
    void DoUpdate(EmuCard c)
    {
        EmuDef d = c.Def;
        if (!Installed(d)) { StartUpdate(c, null); return; }
        bool na;
        int r = ReleaseDialog("Update " + d.Name, "Update " + d.Name + " to the newest version? Your saves and settings are kept.", c.Rel, "Update", null, false, out na);
        if (r == 1) StartUpdate(c, null);
    }

    // Installs waiting for their turn (Welcome picks several; or Install pressed while something else runs).
    void RunQueue()
    {
        while (!IsBusy && installQueue.Count > 0)
        {
            EmuCard next = installQueue.Dequeue();
            if (!cards.Contains(next)) continue;
            if (next.Rel != null) { StartUpdate(next, null); if (IsBusy) return; continue; }   // not started (it's open): next one
            identifying++;   // hold the queue while its release is fetched again
            CheckOne(next, delegate
            {
                identifying--;
                if (next.Rel == null) { next.Err = "Install failed. Try again."; Refresh(next); }
                else if (IsBusy) installQueue.Enqueue(next);
                else StartUpdate(next, null);
                if (!IsBusy) BeginInvoke((MethodInvoker)RunQueue);
            });
            return;
        }
        if (!IsBusy && installQueue.Count == 0 && joinPending) { joinPending = false; BeginInvoke((MethodInvoker)delegate { JoinSharing(null); }); }
    }

    void StartUpdate(EmuCard c, MethodInvoker after)
    {
        EmuDef d = c.Def;
        if (IsBusy)
        {   // never drop an install the user asked for
            if (!installQueue.Contains(c)) installQueue.Enqueue(c);
            c.State.Text = "Waiting..."; roles[c.State] = "info"; c.State.ForeColor = RoleColor(c.State);
            Log(d.Name + ": waiting for the current task.");
            return;
        }
        if (Core.Running(d).Count > 0) { Log(d.Name + ": not updating, it is open."); Fail(c, "Close " + d.Name + ", then try again."); return; }
        bool installed = Installed(d);
        Work(c, installed ? "Updating" : "Installing", (log, prog) => Core.InstallOrUpdate(d, log, prog, CancellationToken.None),
             delegate { CheckOne(c, null); if (!installed) JoinSharing(c); if (after != null) after(); });   // re-check: a newer release may be out already
    }

    // Every version of the emulator in a drop-down; the one picked is installed (saves and settings are kept, the
    // version it replaces goes to Older builds like an update's).
    void ChooseVersion(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        EmuDef d = c.Def;
        bool inst = Installed(d);
        Manifest m = inst ? Core.LoadManifest(d.Dir) : null;
        string have = m != null ? m.Tag : null;
        Release pick = null; bool newest = false; string recTag = null;   // recTag: the version Update installs
        using (Form f = new Form())
        {
            f.Text = "Choose version"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            int W = D(500);
            Label t = new Label(); t.Text = d.Name; t.Font = Theme.Title; t.SetBounds(D(16), D(14), W - D(32), D(24)); t.AutoEllipsis = true;
            Label now = new Label(); now.ForeColor = Theme.Muted; now.SetBounds(D(16), D(40), W - D(32), D(20)); now.AutoEllipsis = true;
            now.Text = !inst ? "Not installed" : "Installed: " + (have ?? "unknown version");
            ComboBox list = new ComboBox(); list.DropDownStyle = ComboBoxStyle.DropDownList; list.SetBounds(D(16), D(70), W - D(32), D(28));
            list.BackColor = Theme.Input; list.ForeColor = Theme.Text; list.Enabled = false;
            list.MaxDropDownItems = 10; list.IntegralHeight = true;   // 10 rows, then scroll
            Label status = new Label(); status.Text = "Loading versions..."; status.ForeColor = Theme.Muted; status.SetBounds(D(16), D(106), W - D(140), D(22)); status.AutoEllipsis = true;
            LinkLabel notes = new LinkLabel(); notes.Text = "What's new"; notes.SetBounds(W - D(116), D(106), D(100), D(22)); notes.TextAlign = ContentAlignment.MiddleRight;
            notes.LinkColor = notes.ActiveLinkColor = notes.VisitedLinkColor = Theme.Accent; notes.LinkBehavior = LinkBehavior.HoverUnderline; notes.Visible = false;
            Button go = new Button(); go.Text = inst ? "Install this version" : "Install"; go.Enabled = false;
            Button no = new Button(); no.Text = "Cancel"; no.DialogResult = DialogResult.Cancel;
            int bw = Math.Max(D(150), TextRenderer.MeasureText(go.Text, Font).Width + D(28));
            no.SetBounds(W - D(16) - D(96), D(140), D(96), D(34)); go.SetBounds(no.Left - D(8) - bw, D(140), bw, D(34));
            f.Controls.AddRange(new Control[] { t, now, list, status, notes, go, no });
            f.CancelButton = no; f.AcceptButton = go;
            foreach (Button b in new[] { go, no }) { StyleButton(b); b.BackColor = Theme.Window; }
            Outline(go, Theme.Accent);
            f.ClientSize = new Size(W, D(190));
            List<Release> rels = new List<Release>();
            Action pickChanged = () =>
            {
                Release r = list.SelectedIndex >= 0 && list.SelectedIndex < rels.Count ? rels[list.SelectedIndex] : null;
                bool same = r != null && have != null && string.Equals(r.Tag, have, StringComparison.OrdinalIgnoreCase);
                go.Enabled = r != null && !same;
                status.Text = r == null ? "" : same ? "This is the version you have." : "Your saves and settings are kept.";
                notes.Visible = r != null;
            };
            list.SelectedIndexChanged += delegate { pickChanged(); };
            notes.LinkClicked += delegate
            {
                if (list.SelectedIndex < 0 || list.SelectedIndex >= rels.Count) return;
                bool na; ReleaseDialog("What's new in " + rels[list.SelectedIndex].Tag, null, rels[list.SelectedIndex], null, null, false, out na);
            };
            go.Click += delegate
            {
                if (list.SelectedIndex < 0 || list.SelectedIndex >= rels.Count) return;
                pick = rels[list.SelectedIndex]; newest = recTag != null && string.Equals(pick.Tag, recTag, StringComparison.OrdinalIgnoreCase);
                f.DialogResult = DialogResult.OK; f.Close();
            };
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); Native.ControlTheme(list.Handle, Theme.Dark ? "DarkMode_CFD" : "Explorer"); };
            f.Shown += delegate
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    List<Release> got = null; string err = null, rec = null;
                    try { got = Core.AllReleases(d, out rec); } catch (Exception ex) { err = ex.Message; }
                    try
                    {
                        if (!f.IsDisposed) f.BeginInvoke((MethodInvoker)delegate
                        {
                            if (got == null) { Log(d.Name + ": couldn't list versions: " + err); status.Text = CheckFailed(err, "Can't connect right now. Try again later."); status.ForeColor = Theme.Danger; return; }
                            rels = got; recTag = rec;
                            int sel = Math.Max(0, rels.FindIndex(x => rec != null && string.Equals(x.Tag, rec, StringComparison.OrdinalIgnoreCase)));
                            for (int i = 0; i < rels.Count; i++)
                            {
                                Release r = rels[i];
                                DateTime dt = Core.ParseDate(r.Published);
                                string label = r.Tag + (dt > DateTime.MinValue ? "   " + dt.ToLocalTime().ToString("d MMM yyyy") : "");
                                if (rec != null && string.Equals(r.Tag, rec, StringComparison.OrdinalIgnoreCase)) label += "   (recommended)";
                                if (have != null && string.Equals(r.Tag, have, StringComparison.OrdinalIgnoreCase)) { label += "   (installed)"; sel = i; }
                                list.Items.Add(label);
                            }
                            list.DropDownHeight = list.ItemHeight * Math.Min(10, Math.Max(1, rels.Count)) + 2;   // 10 rows, then scroll
                            list.Enabled = true; list.SelectedIndex = sel; pickChanged(); list.Focus();   // Enter = Install, not Cancel
                            if (Demo != null && Demo.StartsWith("versionsopen")) list.DroppedDown = true;   // test screenshots only
                            Log(d.Name + ": " + rels.Count + " versions listed.");
                        });
                    }
                    catch (InvalidOperationException) { }   // window closed meanwhile
                });
            };
            if (f.ShowDialog(this) != DialogResult.OK || pick == null) return;
        }
        if (IsBusy) { PleaseWait(); return; }
        if (Core.Running(d).Count > 0) { Fail(c, "Close " + d.Name + ", then try again."); return; }
        Log(d.Name + ": installing " + pick.Tag + (newest ? " (recommended)" : " (chosen)") + ".");
        // exactly the release shown; picked the recommended one: later updates are offered again on Launch
        Work(c, inst ? "Changing version" : "Installing", (log, prog) => Core.InstallOrUpdate(d, log, prog, CancellationToken.None, pick, !newest),
             delegate { CheckOne(c, null); if (!inst) JoinSharing(c); });
    }

    void DoRestore(EmuCard c)
    {
        List<string> b = Core.Backups(c.Def);
        if (b.Count == 0) return;
        if (Core.Running(c.Def).Count > 0) { Fail(c, "Close " + c.Def.Name + ", then try again."); return; }
        if (Ask("Go back", "Go back to the previous version of " + c.Def.Name + "? Your saves are kept.", "Go back", "Cancel") != 0) return;
        Work(c, "Going back", (log, prog) => Core.Restore(c.Def, log));
    }

    void EditNote(EmuCard c)
    {
        using (Form f = new Form())
        {
            f.Text = "Note for " + c.Def.Name; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(D(460), D(118)); f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            Label l = new Label(); l.Text = "Best for:"; l.SetBounds(D(14), D(12), D(430), D(20));
            TextBox tb = new TextBox(); tb.Text = c.Def.Note ?? ""; tb.SetBounds(D(14), D(36), D(432), D(24)); tb.BackColor = Theme.Input; tb.ForeColor = Theme.Text; tb.BorderStyle = BorderStyle.FixedSingle;
            Button ok = new Button(); ok.Text = "Save"; ok.SetBounds(D(262), D(72), D(88), D(34)); ok.DialogResult = DialogResult.OK;
            Button no = new Button(); no.Text = "Cancel"; no.SetBounds(D(358), D(72), D(88), D(34)); no.DialogResult = DialogResult.Cancel;
            f.AcceptButton = ok; f.CancelButton = no;
            f.Controls.AddRange(new Control[] { l, tb, ok, no });
            StyleButton(ok); StyleButton(no); ok.BackColor = no.BackColor = Theme.Window; Outline(ok, Theme.Accent);
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
            if (f.ShowDialog(this) != DialogResult.OK) return;
            c.Def.Note = tb.Text.Trim();
            try { Core.SaveConfig(); Log("Note for " + c.Def.Name + " saved."); } catch (Exception ex) { Log("Could not save the note: " + ex.Message); Fail(c, "Couldn't save the note."); }
            Refresh(c);
        }
    }

    void Fail(EmuCard c, string msg) { Log(c.Def.Name + ": " + msg); c.Err = msg; c.State.Text = msg; roles[c.State] = "danger"; c.State.ForeColor = RoleColor(c.State); }

    // ---- log
    public static string LogPath { get { try { Directory.CreateDirectory(Core.DataDir); } catch { } return Path.Combine(Core.DataDir, "manager.log"); } }
    public static void FileLog(string s)
    {
        try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + "\r\n"); } catch { }
    }
    void Log(string s)
    {
        FileLog(s);
        logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
    }
    void UI(MethodInvoker m) { try { if (IsHandleCreated && !IsDisposed) BeginInvoke(m); } catch { } }

    // ---- style (same rules as the PSU lab)
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyWindow(); }
    void ApplyWindow()
    {
        glassOn = Native.Apply(Handle, Theme.Dark, 0);
    }

    void ApplyStyle()
    {
        string t = Core.Cfg.Theme;
        Theme.Set(t == "dark" || (t == "system" && Theme.SystemDark()));
        head.Font = Theme.Header;
        if (IsHandleCreated) ApplyWindow();
        SuspendLayout();
        BackColor = Theme.Window; ForeColor = Theme.Text;
        Style(this, Theme.Window);
        logBox.BackColor = Theme.Input; logBox.ForeColor = Theme.Text; logBox.BorderStyle = BorderStyle.FixedSingle;
        Native.ControlTheme(logBox.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer");
        Native.ControlTheme(cardsHost.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer");
        ResumeLayout();
        Invalidate(true);
    }

    void Style(Control parent, Color bg)
    {
        foreach (Control c in parent.Controls)
        {
            Color inner = bg;
            if (c is Card) { c.BackColor = Theme.Card; inner = Theme.Card; }
            else if (c is FlatBar) c.BackColor = bg;
            else if (c is Button) { StyleButton((Button)c); c.BackColor = bg; c.ForeColor = Theme.Text; }
            else if (c is Label)
            {
                c.BackColor = bg; c.ForeColor = RoleColor(c);
                c.Font = c == head || c.Font.Size > 12 ? c.Font : strong.Contains(c) ? Theme.Title : Theme.Body;
                LinkLabel ll = c as LinkLabel;
                if (ll != null) { ll.LinkColor = ll.ActiveLinkColor = ll.VisitedLinkColor = Theme.Accent; ll.DisabledLinkColor = Theme.Disabled; ll.LinkBehavior = LinkBehavior.HoverUnderline; }
            }
            else if (c is Panel) c.BackColor = bg;
            if (c.Controls.Count > 0) Style(c, inner);
            c.Invalidate();
        }
    }

    static Color RoleColor(Control c)
    {
        string r;
        if (!roles.TryGetValue(c, out r)) { r = c.ForeColor == Color.DimGray ? "muted" : "text"; roles[c] = r; }
        return r == "muted" ? Theme.Muted : r == "info" ? Theme.Info : r == "danger" ? Theme.Danger : r == "success" ? Theme.Success : r == "accent" ? Theme.Accent : Theme.Text;
    }

    internal static void Outline(Button b, Color c) { accents[b] = c; }

    internal static void StyleButton(Button b)
    {
        if (!styled.Add(b)) return;
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.UseVisualStyleBackColor = false;
        b.Paint += PaintButton;
        EventHandler inv = delegate { b.Invalidate(); };
        b.MouseEnter += inv; b.MouseLeave += inv; b.EnabledChanged += inv; b.TextChanged += inv;
        b.MouseDown += delegate { b.Invalidate(); }; b.MouseUp += delegate { b.Invalidate(); };
    }

    // Check boxes drawn like the PSU lab's: rounded box, accent fill + tick when checked, text after it.
    internal static void StyleCheck(CheckBox c)
    {
        if (!styled.Add(c)) return;
        c.Paint += PaintCheck;
        EventHandler inv = delegate { c.Invalidate(); };
        c.MouseEnter += inv; c.MouseLeave += inv; c.EnabledChanged += inv; c.CheckedChanged += inv;
    }

    static void PaintCheck(object s, PaintEventArgs e)
    {
        CheckBox c = (CheckBox)s; Graphics g = e.Graphics;
        g.Clear(c.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool hot = c.Enabled && c.ClientRectangle.Contains(c.PointToClient(Control.MousePosition));
        const int box = 18;
        RectangleF r = new RectangleF(0.5f, (c.Height - box) / 2f, box - 1, box - 1);
        if (c.Checked)
        {
            Color a = c.Enabled ? (hot ? Theme.Blend(Theme.Accent, Theme.Card, 0.15) : Theme.Accent) : Theme.Disabled;
            Theme.FillRound(g, a, Color.Empty, r, 4, 0);
            using (Pen p = new Pen(Theme.AccentText, 1.8f))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                float x = r.X, y = r.Y;
                g.DrawLines(p, new[] { new PointF(x + 4.5f, y + 9f), new PointF(x + 7.5f, y + 12f), new PointF(x + 13f, y + 5.5f) });
            }
        }
        else Theme.FillRound(g, hot ? Theme.ControlHover : Theme.Input, c.Enabled ? Theme.StrokeStrong : Theme.Disabled, r, 4, 1);
        TextRenderer.DrawText(g, c.Text, c.Font, new Rectangle(box + 8, 0, c.Width - box - 8, c.Height), c.Enabled ? c.ForeColor : Theme.Disabled,
                              TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    static void PaintButton(object s, PaintEventArgs e)
    {
        Button b = (Button)s; Graphics g = e.Graphics;
        g.Clear(b.Parent != null ? b.Parent.BackColor : Theme.Window);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool hot = b.Enabled && b.ClientRectangle.Contains(b.PointToClient(Control.MousePosition));
        bool down = hot && (Control.MouseButtons & MouseButtons.Left) != 0;
        Color acc, fill, border, text = Theme.Text;
        float bw = 1;
        if (!b.Enabled) { fill = Theme.ControlOff; border = Theme.Stroke; text = Theme.Disabled; }
        else if (accents.TryGetValue(b, out acc))
        {
            acc = Theme.Tint(acc);
            fill = Theme.Blend(Theme.Control, acc, down ? 0.30 : hot ? 0.22 : 0.12); border = acc; bw = 1.6f;
        }
        else { fill = down ? Theme.ControlPressed : hot ? Theme.ControlHover : Theme.Control; border = Theme.Dark ? Theme.Stroke : Color.FromArgb(255, 0xd6, 0xd6, 0xd6); }
        RectangleF r = new RectangleF(bw / 2, bw / 2, b.Width - bw - 0.5f, b.Height - bw - 0.5f);
        if (down) r.Inflate(-0.5f, -0.5f);
        Theme.FillRound(g, fill, border, r, 5, bw);
        TextRenderer.DrawText(g, b.Text, b.Font, new Rectangle(4, 0, b.Width - 8, b.Height - (down ? 0 : 1)), text,
                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    // ---- entry
    [STAThread]
    static int Main(string[] args)
    {
        string cfg = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "emulators.json");
        List<string> cli = null; string logFile = null;
        bool sandbox = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length) cfg = args[++i];
            else if (args[i] == "--sandbox" && i + 1 < args.Length)
            {   // test environment: a folder of its own, starting like a brand-new user's (no builds, no games folder)
                string dir = Path.GetFullPath(args[++i]);
                bool fresh = !Directory.Exists(dir) || !Directory.EnumerateFileSystemEntries(dir).Any();
                Directory.CreateDirectory(dir);
                if (fresh) File.WriteAllText(Path.Combine(dir, SANDBOX_MARK), "Test environment made by EmuManager.exe --sandbox. Reset test may delete this whole folder.\r\n");
                cfg = Path.Combine(dir, "emulators.json"); sandbox = true;
            }
            else if (args[i] == "--log" && i + 1 < args.Length) logFile = args[++i];
            else if (args[i] == "--demo" && i + 1 < args.Length) EmuForm.Demo = args[++i];
            else if (args[i] == "--cli") cli = new List<string>();
            else if (cli != null) cli.Add(args[i]);
        }
        try
        {
            Core.LoadConfig(cfg);
            if (sandbox && !Core.Cfg.Sandbox) { Core.Cfg.Sandbox = true; Core.SaveConfig(); }
        }
        catch (Exception ex)
        {
            if (cli != null) { if (logFile != null) File.AppendAllText(logFile, "config: " + ex.Message + "\r\n"); return 2; }
            MessageBox.Show("Could not read " + cfg + ":\r\n" + ex.Message, "NS2 Emulator Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
        if (cli != null) return Cli(cli, logFile);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Theme.Set(Theme.SystemDark());
        Application.Run(new EmuForm());
        return 0;
    }

    // --cli check | update ID | restore ID   (several in a row are fine). Output to --log FILE and manager.log.
    static int Cli(List<string> a, string logFile)
    {
        Action<string> log = s => { FileLog(s); if (logFile != null) File.AppendAllText(logFile, s + "\r\n"); };
        int rc = 0;
        for (int i = 0; i < a.Count; i++)
        {
            try
            {
                if (a[i] == "check" || a[i] == "check-fresh")   // check-fresh: ask the hosts now, like the Check for updates link
                    foreach (EmuDef d in Core.Cfg.Emulators)
                    {
                        Release r; try { r = Core.Latest(d, a[i] == "check-fresh"); } catch (Exception ex) { log(d.Id + ": ERROR " + ex.Message + " -> shown as: " + CheckFailed(ex.Message)); continue; }
                        Manifest m = Core.LoadManifest(d.Dir);
                        log(d.Id + ": installed=" + (Core.Describe(d) ?? "none") + " latest=" + r.Tag + " asset=" + r.Asset + " size=" + r.Size + " sha256=" + (r.Sha256 ?? "none") +
                            " current=" + (m != null && m.Tag == r.Tag));
                    }
                else if (a[i] == "add-recommended")   // add-recommended ID: put a recommended build on the list (tests)
                {
                    string id = a[++i];
                    EmuDef r = Core.RecommendedNotAdded().First(x => x.Id == id);
                    r.Added = true; Core.Cfg.Emulators.Add(r); Core.SaveConfig();
                    log("added " + r.Name + " -> " + r.Dir);
                }
                else if (a[i] == "remember")   // remember ID DIR: record DIR as a known install of that card's fork (tests)
                {
                    string id = a[++i], dir = a[++i];
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == id);
                    EmuDef t = new EmuDef { Id = d.Id, Repo = d.Repo, Host = d.Host, Dir = dir, Exe = d.Exe };
                    Core.RememberInstall(t, null); log("remembered " + dir);
                }
                else if (a[i] == "dupes")      // other installs of each card's emulator
                    foreach (EmuDef d in Core.Cfg.Emulators)
                        log(d.Id + ": " + string.Join("; ", Core.OtherInstalls(d).Select(p => p.Key + " (" + p.Value + ")")) + (Core.OtherInstalls(d).Count == 0 ? "(none)" : ""));
                else if (a[i] == "older")      // each card's older builds
                    foreach (EmuDef d in Core.Cfg.Emulators)
                        log(d.Id + " older: " + string.Join(" | ", Core.Backups(d).Select(Path.GetFileName)));
                else if (a[i] == "identify")   // identify ID DIR: which release of that card's fork is in DIR (read-only)
                {
                    string id = a[++i], dir = a[++i];
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == id);
                    EmuDef probe = new EmuDef { Id = d.Id, Name = d.Name, Host = d.Host, ApiBase = d.ApiBase, Repo = d.Repo, TagPrefix = d.TagPrefix, Assets = d.Assets, Dir = dir, Exe = d.Exe };
                    DateTime t0 = DateTime.Now; int n = 0;
                    Core.Identified r = Core.Identify(probe, dir, s => n++);
                    log("identify " + id + " " + dir + " -> " + (r.Rel != null ? r.Rel.Tag + " / " + r.Rel.Asset : "not recognised") + " | " + r.Why +
                        " | user=" + r.HasUser + " entries=" + (r.Entries != null ? r.Entries.Count : 0) + " | " + n + " zips, " + (DateTime.Now - t0).TotalSeconds.ToString("0.0") + " s");
                }
                else if (a[i] == "lookup")     // what Add new fork would find for a link
                {
                    string link = a[++i];
                    try
                    {
                        Core.RepoInfo ri = Core.Lookup(link);
                        log("lookup " + link + " -> host=" + ri.Host + " api=" + (ri.ApiBase ?? "github") + " repo=" + ri.Repo + " default=" + (ri.DefaultPrefix.Length > 0 ? ri.DefaultPrefix : "(all)") +
                            " pgo=" + ri.HasPgo + " zipsFrom=" + ri.ZipsFrom + " zips=" + string.Join(",", ri.Zips) + "\r\n  channels: " +
                            string.Join("; ", ri.Channels.Select(c => (c.Prefix.Length > 0 ? c.Prefix : "(none)") + " x" + c.Count + " newest " + c.Newest)));
                        foreach (bool pgo in new[] { true, false })
                        {
                            EmuDef t = new EmuDef { Id = "t", Name = "t", Host = ri.Host, ApiBase = ri.ApiBase, Repo = ri.Repo, TagPrefix = ri.DefaultPrefix, Assets = Core.AssetPatterns(pgo && ri.HasPgo) };
                            Release r = Core.Latest(t);
                            log("  latest (" + (pgo && ri.HasPgo ? "pgo" : "standard") + "): " + r.Tag + " " + r.Asset + " sha256=" + (r.Sha256 != null));
                        }
                    }
                    catch (Exception ex) { log("lookup " + link + " -> ERROR " + ex.Message); }
                }
                else if (a[i] == "notes")      // hover summary + full readable notes of each build's latest release
                    foreach (EmuDef d in Core.Cfg.Emulators)
                    {
                        Release r = Core.Latest(d);
                        log("=== " + d.Id + " " + r.Tag + " hover:\r\n" + ReadableNotes(r.Body, 6) + "\r\n--- full:\r\n" + ReadableNotes(r.Body, 0));
                    }
                else if (a[i] == "data")       // each card's portable / shared state
                    foreach (EmuDef d in Core.Cfg.Emulators)
                        log(d.Id + ": portable=" + UserData.IsPortable(d) + " appdata=" + (UserData.AppDataSource(d) ?? "none") + " | " +
                            string.Join(" ", UserData.Items.Select(it => it.Key + "=" + UserData.StateOf(d, it))));
                else if (a[i] == "portable")   // portable ID FROM|-: give the build its own user folder (copy FROM, or empty)
                {
                    string id = a[++i], from = a[++i];
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == id);
                    log(UserData.MakePortable(d, from == "-" ? null : from, log));
                }
                else if (a[i] == "share" || a[i] == "unshare")   // share ID KEY [ask|shared|build] / unshare ID KEY
                {
                    bool on = a[i] == "share";
                    string id = a[++i], key = a[++i];
                    string prefer = on && i + 1 < a.Count && (a[i + 1] == "ask" || a[i + 1] == "shared" || a[i + 1] == "build") ? a[++i] : "ask";
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == id);
                    UserData.Item it = UserData.Find(key);
                    try
                    {
                        if (on) UserData.Share(d, it, prefer == "ask" ? null : prefer, log); else UserData.Unshare(d, it, log);
                        log(id + " " + key + " -> " + UserData.StateOf(d, it));
                    }
                    catch (UserData.Conflict c) { log(id + " " + key + " -> CONFLICT " + c.Message); rc = 1; }
                }
                else if (a[i] == "share-new")  // share-new KEY,KEY|-: what new builds share from the start
                {
                    string v = a[++i];
                    Core.Cfg.ShareNew = v == "-" ? null : v.Split(',').ToList(); Core.SaveConfig();
                    log("share-new = " + v);
                }
                else if (a[i] == "deltree")    // deltree DIR: Reset's delete, test environment only (links are removed, not followed)
                {
                    string dir = Path.GetFullPath(a[++i]).TrimEnd('\\');
                    if (!Core.Cfg.Sandbox || !dir.StartsWith(Path.GetFullPath(Core.Home).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new Exception("deltree only works inside a test environment.");
                    UserData.DeleteTree(dir); log("deleted " + dir);
                }
                else if (a[i] == "launch-check") // launch-check ID: why it may not launch now
                {
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == a[i + 1]); i++;
                    log(d.Id + " launch: " + (UserData.LaunchProblem(d) ?? "ok"));
                }
                else if (a[i] == "games")
                    foreach (EmuDef d in Core.Cfg.Emulators) log(d.Id + ": " + (Core.SyncGamesFolder(d, null) ?? "no change"));
                else if (a[i] == "update" || a[i] == "restore")
                {
                    string id = a[++i];
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == id);
                    if (a[i - 1] == "update") Core.InstallOrUpdate(d, log, (g, t) => { }, CancellationToken.None);
                    else Core.Restore(d, log);
                }
                else { log("unknown command " + a[i]); rc = 1; }
            }
            catch (Exception ex) { log("ERROR: " + ex.Message); rc = 1; }
        }
        return rc;
    }
}
