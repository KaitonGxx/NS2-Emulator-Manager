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
    public static bool Dark = true;
    public static Color Window, Sidebar, Card, Stroke, StrokeStrong, Text, Muted, Disabled, Accent, Accent2, Accent3, AccentText, Input,
                        Control, ControlHover, ControlPressed, ControlOff, NavHover, NavSelected, Danger, Success, Warn, Info, Glow1, Glow2;
    public static Font Body, Small, Title, CardTitle, Header, Nav, Icon, Big, Caption, Value, Strong, Name, Large, Glyph;
    public static string Scheme = "purple";
    // The colour schemes the user can pick (Colors... in the "..." menu): id, name, background, tile, raised tile, line,
    // text, muted text, accent, second accent (gradients), two background glows, text on the accent, a third colour.
    public static readonly string[][] Schemes =
    {
        new[] { "purple",   "Purple",   "0e0b16", "1a1626", "211c30", "2d2640", "ece8f6", "9a93b0", "9b6bff", "e66bd6", "3b2266", "3a1d4f", "ffffff", "7fb3ff" },
        new[] { "lavender", "Lavender", "100e17", "1b1826", "221e30", "302a42", "f1edfb", "a59eba", "c4b5fd", "f0abfc", "2e2550", "3a2244", "1b1430", "93c5fd" },
        new[] { "galaxy",   "Galaxy",   "0b0a1a", "15132b", "1b1836", "29244d", "eceaff", "9893c2", "7c6cff", "ff6ec7", "26205e", "4a1747", "ffffff", "5eead4" },
        new[] { "royal",    "Royal",    "0d0a14", "181325", "1f1830", "2e2444", "f5f0ff", "a79dbd", "f4c542", "c79bff", "3d2f0a", "2f1d4f", "1a1204", "7dd3fc" },
        new[] { "midnight", "Midnight", "0a0c1a", "141830", "1a1f3d", "262c52", "e8ebff", "9097c0", "5566ff", "8b5cf6", "1e2766", "241a55", "ffffff", "38bdf8" },
        new[] { "ocean",    "Ocean",    "071118", "0f1d27", "142633", "1f3442", "e6f3f8", "8aa4b2", "2ec5d3", "3b82f6", "0f3b52", "10304a", "ffffff", "a78bfa" },
        new[] { "arctic",   "Arctic",   "0b1016", "141b24", "19222d", "26313f", "eef5fb", "94a3b4", "a5f3fc", "c7d2fe", "163243", "1f2547", "0b1620", "fda4af" },
        new[] { "sky",      "Sky",      "0a1016", "121c26", "17232f", "233243", "eaf4fd", "90a5b8", "7dd3fc", "a5b4fc", "123247", "22284f", "0b1a26", "fbcfe8" },
        new[] { "neon",     "Neon",     "0a0a12", "13131f", "191928", "262638", "f0f0ff", "9a9ab8", "00e5ff", "ff2bd6", "08343d", "3d0838", "0a0a12", "b6ff3b" },
        new[] { "cyber",    "Cyber",    "0b0b0d", "151518", "1b1b20", "2a2a31", "f4f4ec", "a3a39a", "fcee0a", "00f0ff", "3d3a05", "053a3d", "0b0b0d", "ff3864" },
        new[] { "aurora",   "Aurora",   "081214", "101d20", "152629", "21363a", "e6f7f5", "8fb0ac", "34d399", "a78bfa", "0f3d35", "2a1f4f", "06140f", "60a5fa" },
        new[] { "mint",     "Mint",     "0a1110", "121c1a", "172421", "233431", "e9f7f2", "93ada5", "6ee7b7", "99f6e4", "12382c", "133a38", "06231a", "fda4af" },
        new[] { "emerald",  "Emerald",  "08110d", "111d18", "16261f", "22362d", "e7f6ef", "8fae9f", "22c55e", "a3e635", "123d28", "1f3a10", "0b1a10", "2dd4bf" },
        new[] { "matcha",   "Matcha",   "0d100b", "171c14", "1d2319", "2b3324", "eef3e6", "9fab92", "a7c957", "dde5b6", "26331a", "2f3416", "141a0c", "e9c46a" },
        new[] { "retro",    "Retro",    "0e1209", "182012", "1e2817", "2c3a22", "e8f0d8", "9aa883", "9bbc0f", "c4d64b", "27360a", "1f2f0c", "0e1209", "e07a5f" },
        new[] { "amber",    "Amber",    "110e07", "1d1a10", "262114", "383020", "fbf5e6", "b5a888", "f5b301", "ff7a1a", "4a3608", "3d2008", "1a1204", "6ec1ff" },
        new[] { "sunset",   "Sunset",   "120c08", "1f1611", "281c15", "3a2a20", "f8efe8", "b39d8e", "ff8a3d", "ff4f8b", "4a2410", "4a1530", "ffffff", "ffd166" },
        new[] { "peach",    "Peach",    "130e0c", "201815", "2a1f1b", "3c2d27", "fbefe9", "b8a096", "ffb4a2", "ffd6a5", "4a2a20", "4a3418", "2a140c", "a0c4ff" },
        new[] { "coral",    "Coral",    "120c0c", "1f1615", "291c1b", "3b2a28", "faeeed", "b59d9a", "ff6f61", "ff9e7a", "4a1d18", "2e3a36", "2a0d0a", "2dd4bf" },
        new[] { "mocha",    "Mocha",    "120d0a", "1e1712", "271e18", "382b22", "f7efe8", "b09d8f", "d4a373", "e9c46a", "3d2617", "2f2416", "1a120c", "8ecae6" },
        new[] { "ruby",     "Ruby",     "12080a", "1f0f13", "291419", "3c1d24", "fbe9ec", "b6949b", "e11d48", "f43f5e", "4f0f1c", "3a0d22", "ffffff", "fbbf24" },
        new[] { "joycon",   "Joy-Con",  "100b0d", "1c1517", "24191c", "33262a", "f6ecee", "a8979b", "ff4d5e", "2f9bff", "4a1520", "12304f", "ffffff", "ffd23f" },
        new[] { "sakura",   "Sakura",   "140b10", "21141b", "2a1922", "3b2531", "f8eaf0", "b398a6", "ff7eb6", "ffb38a", "4a1a33", "3d1f2a", "2a0f1c", "c4b5fd" },
        new[] { "lychee",   "Lychee",   "120a0c", "1f1215", "29171b", "3b2228", "fbecee", "b3979c", "f0386b", "e8457a", "4d1424", "1d3a17", "ffffff", "7ed957" },
        new[] { "graphite", "Graphite", "0c0c0e", "17171a", "1e1e22", "2b2b31", "f2f2f4", "9a9aa3", "e5e5ea", "a1a1aa", "2a2a30", "202026", "111114", "7fb3ff" },
    };

    public static void Set(bool dark) { Set(Scheme); }   // older callers (Setup): the current scheme
    public static void Set(string name)
    {
        string[] c = Schemes.FirstOrDefault(x => x[0] == name) ?? Schemes[0];
        Scheme = c[0]; Dark = true;
        Func<int, Color> h = i => Rgb(Convert.ToInt32(c[i], 16));
        Window = h(2); Sidebar = Window; Card = h(3); Input = h(4); Stroke = h(5); Text = h(6); Muted = h(7);
        Accent = h(8); Accent2 = h(9); Glow1 = h(10); Glow2 = h(11); AccentText = h(12); Accent3 = h(13);
        StrokeStrong = Blend(Stroke, Muted, 0.45); Disabled = Blend(Muted, Window, 0.45);
        Control = Input; ControlHover = Blend(Input, Accent, 0.14); ControlPressed = Blend(Input, Accent, 0.24); ControlOff = Blend(Card, Window, 0.4);
        NavHover = Color.FromArgb(0x14, 255, 255, 255); NavSelected = Color.FromArgb(0x22, 255, 255, 255);
        Danger = Rgb(0xff7b8a); Success = Rgb(0x5fd39a); Warn = Rgb(0xf5c451); Info = Rgb(0x7fb3ff);
        if (Body == null)
        {
            string text = Family("Segoe UI"), semi = Family("Segoe UI Variable Display Semibold", "Segoe UI Semibold"), icons = Family("Segoe Fluent Icons", "Segoe MDL2 Assets");
            string light = Family("Segoe UI Variable Display Light", "Segoe UI Light", "Segoe UI");
            Body = new Font(text, 9.5f); Small = new Font(text, 9f); Nav = new Font(text, 10f);
            Title = new Font(semi, 10.5f); CardTitle = new Font(semi, 12f); Header = new Font(semi, 16f);
            Big = new Font(light, 28f); Value = new Font(semi, 13f); Caption = new Font(semi, 9f);
            Strong = new Font(semi, 9.5f); Name = new Font(semi, 15f); Large = new Font(text, 11.5f);
            Icon = new Font(icons, 12f); Glyph = new Font(icons, 7.5f);
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

    public static Color Tint(Color c) { return c; }

    // The two soft glows on the window background, drawn so they line up across every panel that shows them.
    // Drawn once per window size and scheme, then copied: every control on the window background shows its part of it.
    static Bitmap glowCache; static Size glowSize; static string glowScheme;
    public static void PaintGlow(Graphics g, Control c)
    {
        Form f = c.FindForm();
        Size sz = f != null ? f.ClientSize : Size.Empty;
        if (sz.Width < 1 || sz.Height < 1) { g.Clear(Window); return; }
        if (glowCache == null || glowSize != sz || glowScheme != Scheme)
        {
            if (glowCache != null) glowCache.Dispose();
            glowCache = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
            using (Graphics b = Graphics.FromImage(glowCache))
            {
                float W = sz.Width, H = sz.Height;
                b.Clear(Window);
                b.SmoothingMode = SmoothingMode.AntiAlias;
                GlowAt(b, Glow1, new RectangleF(W * 0.20f, -H * 0.62f, W * 1.15f, H * 1.05f), 220);
                GlowAt(b, Glow2, new RectangleF(-W * 0.55f, H * 0.55f, W * 0.95f, H * 0.95f), 220);
            }
            glowSize = sz; glowScheme = Scheme;
        }
        Point o = f.PointToClient(c.PointToScreen(Point.Empty));
        g.DrawImage(glowCache, new Rectangle(-o.X, -o.Y, sz.Width, sz.Height), 0, 0, sz.Width, sz.Height, GraphicsUnit.Pixel);
    }
    public static void GlowAt(Graphics g, Color col, RectangleF r, int alpha)
    {
        if (r.Width < 1 || r.Height < 1) return;
        using (GraphicsPath p = new GraphicsPath())
        {
            p.AddEllipse(r);
            using (PathGradientBrush b = new PathGradientBrush(p))
            { b.CenterColor = Color.FromArgb(alpha, col); b.SurroundColors = new[] { Color.FromArgb(0, col) }; g.FillPath(b, p); }
        }
    }
    // What is behind a control: its tile, the window glow, or its parent's colour.
    public static void ParentBack(Graphics g, Control c)
    {
        Control p = c.Parent;
        IBack b = p as IBack;
        if (b != null) b.PaintBack(g, c.Bounds);
        else if (p is GlowPanel) PaintGlow(g, c);
        else g.Clear(p != null && p.BackColor.A == 255 ? p.BackColor : Window);
    }
    // The accent gradient (buttons, switches); light > 0 brightens it (hover), < 0 darkens it (pressed).
    public static LinearGradientBrush AccentBrush(RectangleF r, double light)
    {
        Color a = Accent, b = Accent2;
        if (light > 0) { a = Blend(a, Color.White, light); b = Blend(b, Color.White, light); }
        if (light < 0) { a = Blend(a, Color.Black, -light); b = Blend(b, Color.Black, -light); }
        return new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), a, b, 30f);
    }
    public static Color Hex(string h) { return Rgb(Convert.ToInt32(h, 16)); }

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
        if (Build >= 22000)
        {   // title bar and edge in the colour scheme (Windows 11)
            Attr(h, 35, Ref(Theme.Window)); Attr(h, 36, Ref(Theme.Text)); Attr(h, 34, Ref(Theme.Stroke));
        }
        return on;
    }
    static int Ref(Color c) { return c.R | c.G << 8 | c.B << 16; }
    public static void Raise(IntPtr h) { try { SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); } catch { } }
    public static void Below(IntPtr h, IntPtr after) { try { SetWindowPos(h, after, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); } catch { } }
    public static void RoundPopup(IntPtr h) { if (Build >= 22000) { Attr(h, 33, 3); Attr(h, 34, Ref(Theme.Stroke)); } }

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    // The frame is measured again (after WM_NCCALCSIZE changes what it covers).
    public static void FrameChanged(IntPtr h) { try { SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); } catch { } }

    public static void ControlTheme(IntPtr h, string name) { try { SetWindowTheme(h, name, null); } catch { } }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct COMBOBOXINFO { public int cbSize; public RECT rcItem, rcButton; public int stateButton; public IntPtr hwndCombo, hwndItem, hwndList; }
    [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr h, ref COMBOBOXINFO i);
    // A drop-down's list (its own window) with a dark scroll bar.
    public static void DarkList(ComboBox c)
    {
        try
        {
            COMBOBOXINFO i = new COMBOBOXINFO(); i.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
            if (GetComboBoxInfo(c.Handle, ref i) && i.hwndList != IntPtr.Zero) SetWindowTheme(i.hwndList, "DarkMode_Explorer", null);
        }
        catch { }
    }

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

// Anything children paint over (a tile): draws its own background under a child at "child" (its bounds).
interface IBack { void PaintBack(Graphics g, Rectangle child); }

class Card : Panel, IBack
{
    public int Shift = 10;
    public static float K = 1;        // screen scale (the main window sets it)
    public float Radius = 0;          // 0: the usual 14 (scaled)
    public Color Glow = Color.Empty;  // a soft glow in the top right corner
    public Color Edge = Color.Empty;  // a coloured outline (the manager tile when an update is out)
    public Card() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnControlAdded(ControlEventArgs e) { base.OnControlAdded(e); e.Control.Top += Shift; }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    float Rad { get { return Radius > 0 ? Radius : 14 * K; } }
    public bool Hover;   // lights up under the mouse (the main window's tiles)
    void Body(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (r.Width < 2 || r.Height < 2) return;
        float h = Hover ? Anim.Get(this, 0, 0) : 0;
        Theme.FillRound(g, h > 0 ? Theme.Blend(Theme.Card, Theme.Input, 0.45 * h) : Theme.Card, Color.Empty, r, Rad, 0);
        if (!Glow.IsEmpty)
            using (GraphicsPath clip = Theme.Round(r, Rad))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                float d = 200 * K;
                Theme.GlowAt(g, Glow, new RectangleF(Width - 36 * K - d / 2, 30 * K - d / 2, d, d), (int)(100 + 55 * h));
                g.Restore(st);
            }
        if (!Edge.IsEmpty) { Theme.FillRound(g, Color.Empty, Edge, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), Rad, 2 * K); return; }
        Color edge = h <= 0 ? Theme.Stroke : Theme.Blend(Theme.Stroke, Glow.IsEmpty ? Theme.StrokeStrong : Theme.Blend(Theme.StrokeStrong, Glow, 0.5), h);
        Theme.FillRound(g, Color.Empty, edge, r, Rad, 1);
    }
    public void PaintBack(Graphics g, Rectangle child)
    {
        g.Clear(Hover ? Theme.Blend(Theme.Card, Theme.Input, 0.45 * Anim.Get(this, 0, 0)) : Theme.Card);
        GraphicsState st = g.Save();
        g.TranslateTransform(-child.X, -child.Y);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Body(g);
        g.Restore(st);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Body(g);
        if (Text.Length > 0)
            TextRenderer.DrawText(g, Text, Theme.CardTitle, new Rectangle((int)(18 * K), (int)(12 * K), Width - (int)(36 * K), (int)(28 * K)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
    }
}

// The window background: the scheme's colour with two soft glows.
class GlowPanel : Panel
{
    public GlowPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    Point scrolled;
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme.PaintGlow(e.Graphics, this);
        if (AutoScrollPosition != scrolled)
        {   // moved without OnScroll: the copied pixels carry the glow from the old place
            scrolled = AutoScrollPosition;
            if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { Invalidate(true); });
        }
    }
    public Func<Point, bool> Through;   // true: the window below handles the mouse here (moving / sizing the window)
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84 && Through != null)   // WM_NCHITTEST
        {
            long lp = m.LParam.ToInt64();
            Point p = PointToClient(new Point(unchecked((short)(lp & 0xFFFF)), unchecked((short)((lp >> 16) & 0xFFFF))));
            if (Through(p)) { m.Result = (IntPtr)(-1); return; }   // HTTRANSPARENT
        }
        base.WndProc(ref m);
    }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(true); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(true); }
}

// Smooth transitions: each control has a few 0..1 values (slot 0 hover, slot 1 on/off) that glide to their target;
// painting reads them. One timer drives all of them and stops when nothing moves.
static class Anim
{
    class Item { public float[] V = new float[2], T = new float[2]; public bool[] Has = new bool[2]; public bool Deep; }
    static readonly Dictionary<Control, Item> items = new Dictionary<Control, Item>();
    static readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static long last;
    public static float Ms = 160;   // a full 0 -> 1 change
    static Anim() { timer.Interval = 15; timer.Tick += Tick; }
    static Item Of(Control c)
    {
        Item it;
        if (!items.TryGetValue(c, out it)) { it = new Item(); items[c] = it; c.Disposed += delegate { items.Remove(c); }; }
        return it;
    }
    // The value now (eased); a control seen for the first time starts at "start".
    public static float Get(Control c, int slot, float start)
    {
        Item it = Of(c);
        if (!it.Has[slot]) { it.Has[slot] = true; it.V[slot] = it.T[slot] = start; }
        float v = it.V[slot];
        return v * v * (3 - 2 * v);
    }
    // Glide to target. deep: the control's children are drawn again too (a tile and what is on it).
    public static void To(Control c, int slot, float target, bool deep = false)
    {
        Item it = Of(c);
        if (!it.Has[slot]) { it.Has[slot] = true; it.V[slot] = it.T[slot] = target; c.Invalidate(deep); return; }
        if (it.T[slot] == target) return;
        it.T[slot] = target; it.Deep |= deep;
        if (!c.IsHandleCreated || !c.Visible) { it.V[slot] = target; c.Invalidate(deep); return; }   // nothing to watch: jump
        if (!timer.Enabled) { last = clock.ElapsedMilliseconds; timer.Start(); }
    }
    static void Tick(object s, EventArgs e)
    {
        long now = clock.ElapsedMilliseconds;
        float step = Math.Max(0.02f, Math.Min(0.5f, (now - last) / Ms));
        last = now;
        bool any = false;
        foreach (KeyValuePair<Control, Item> kv in items.ToList())
        {
            Item it = kv.Value; bool moved = false;
            for (int i = 0; i < it.V.Length; i++)
                if (it.Has[i] && it.V[i] != it.T[i])
                {
                    float d = it.T[i] - it.V[i];
                    it.V[i] = Math.Abs(d) <= step ? it.T[i] : it.V[i] + Math.Sign(d) * step;
                    moved = true;
                }
            if (!moved) continue;
            any = true;
            if (!kv.Key.IsDisposed) kv.Key.Invalidate(it.Deep);
        }
        if (!any) timer.Stop();
    }
}

// The picture faded out when the colours change: never takes the focus or the mouse. A layered window that is given
// the picture before it's shown (UpdateLayeredWindow), so its very first frame is the picture: a window that paints
// itself after showing is white for a frame or two, which flashed the whole manager white.
class FadeForm : Form
{
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref Point pos, ref Size size, IntPtr src, ref Point srcPos, int key, ref BLEND blend, int flags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
    IntPtr mem, bmp, old;
    public FadeForm() { }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {   // no activate, tool window, mouse goes through, layered
        get { CreateParams p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80 | 0x20 | 0x80000; return p; }
    }
    // The picture (the window's size), fully shown. Call before Show.
    public void SetPicture(Bitmap pic)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        try
        {
            mem = CreateCompatibleDC(screen);
            bmp = pic.GetHbitmap(Color.FromArgb(0));
            old = SelectObject(mem, bmp);
        }
        finally { ReleaseDC(IntPtr.Zero, screen); }
        Alpha(255);
    }
    // 255 = the picture fully covers the window, 0 = gone
    public void Alpha(int a)
    {
        if (mem == IntPtr.Zero || IsDisposed) return;
        Point pos = Location, zero = Point.Empty; Size size = Size;
        BLEND b = new BLEND { Op = 0, Flags = 0, Alpha = (byte)Math.Max(0, Math.Min(255, a)), Format = 1 };   // AC_SRC_ALPHA: the cut corners are see-through
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref pos, ref size, mem, ref zero, 0, ref b, 2);   // ULW_ALPHA
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void Dispose(bool disposing)
    {
        if (mem != IntPtr.Zero) { SelectObject(mem, old); DeleteDC(mem); mem = IntPtr.Zero; }
        if (bmp != IntPtr.Zero) { DeleteObject(bmp); bmp = IntPtr.Zero; }
        base.Dispose(disposing);
    }
}

// Menus: rounded highlight, and they fade in.
class MenuLook : ToolStripProfessionalRenderer
{
    public MenuLook() : base(new MenuColors()) { RoundedEdges = false; }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float K = Card.K;
        Theme.FillRound(g, Theme.ControlHover, Color.Empty, new RectangleF(4 * K, 1, e.Item.Width - 8 * K, e.Item.Height - 2), 6 * K, 0);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Disabled;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)   // the sub-menu arrow, readable on the dark menu
    {
        e.ArrowColor = e.Item != null && !e.Item.Enabled ? Theme.Disabled : Theme.Muted;
        base.OnRenderArrow(e);
    }
}

// "+ Add emulator fork" in the free part of the last row of emulator cards: a dashed outline, a round plus, the words.
class AddTile : Control
{
    public float Radius = 20;
    public AddTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        Cursor = Cursors.Hand; Text = "Add emulator fork";
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Anim.To(this, 0, 1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Anim.To(this, 0, 0); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        if (Width < 20 || Height < 20) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float K = Card.K, h = Anim.Get(this, 0, 0);
        RectangleF r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
        Theme.FillRound(g, Color.FromArgb((int)(70 + 90 * h), Theme.Card), Color.Empty, r, Radius, 0);
        using (GraphicsPath p = Theme.Round(r, Radius))
        using (Pen pen = new Pen(Theme.Blend(Theme.StrokeStrong, Theme.Accent, 0.55 * h), 1.6f * K))
        { pen.DashPattern = new[] { 4f, 3f }; g.DrawPath(pen, p); }
        // a round plus in the accent, the words under it
        int lh = TextRenderer.MeasureText("Ag", Theme.Title).Height;
        float d = 46 * K, top = (Height - d - 10 * K - lh) / 2f;
        RectangleF c = new RectangleF((Width - d) / 2f, top, d, d);
        using (GraphicsPath cp = new GraphicsPath())
        {
            cp.AddEllipse(c);
            using (LinearGradientBrush b = Theme.AccentBrush(c, 0.10 * h)) g.FillPath(b, cp);
        }
        using (Pen plus = new Pen(Theme.AccentText, 2.6f * K))
        {
            plus.StartCap = plus.EndCap = LineCap.Round;
            float m = d * 0.27f, cx = c.X + d / 2, cy = c.Y + d / 2;
            g.DrawLine(plus, cx - m, cy, cx + m, cy); g.DrawLine(plus, cx, cy - m, cx, cy + m);
        }
        TextRenderer.DrawText(g, Text, Theme.Title, new Rectangle(0, (int)(c.Bottom + 10 * K), Width, lh), Theme.Blend(Theme.Muted, Theme.Text, 0.4 + 0.6 * h),
                              TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

// A control the mouse goes through (the title and logo: dragging them moves the window).
class HitThrough : NativeWindow
{
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) { m.Result = (IntPtr)(-1); return; }   // WM_NCHITTEST -> HTTRANSPARENT
        base.WndProc(ref m);
    }
}

// Minimize / maximize / close, drawn like Windows' own, for the window without a title bar.
class CaptionButton : Control
{
    public int Kind;   // 0 minimize, 1 maximize / restore, 2 close
    bool hot, down;
    public CaptionButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); SetStyle(ControlStyles.Selectable, false); TabStop = false; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Anim.To(this, 0, 1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = down = false; Anim.To(this, 0, 0); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        Color glyph = Theme.Text;
        float h = Anim.Get(this, 0, 0);
        if (h > 0 && Kind == 2)
        {
            Color red = down ? Color.FromArgb(0x94, 0x1f, 0x15) : Color.FromArgb(0xc4, 0x2b, 0x1c);
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * h), red))) g.FillRectangle(b, ClientRectangle);
            glyph = Theme.Blend(Theme.Text, Color.White, h);
        }
        else if (h > 0) using (SolidBrush b = new SolidBrush(Color.FromArgb((int)((down ? 0x12 : 0x1c) * h), 255, 255, 255))) g.FillRectangle(b, ClientRectangle);
        Form f = FindForm();
        bool max = f != null && f.WindowState == FormWindowState.Maximized;
        string t = Kind == 0 ? "\uE921" : Kind == 1 ? (max ? "\uE923" : "\uE922") : "\uE8BB";
        TextRenderer.DrawText(g, t, Theme.Glyph, ClientRectangle, glyph, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}

// A form that doesn't take the focus when it opens for a screenshot (--demo).
class QuietForm : Form
{
    protected override bool ShowWithoutActivation { get { return EmuForm.Demo != null; } }
}

// Release notes drawn in the app's style: one rounded card per release, its headings in the accent, bullets with an
// accent dot, **bold** kept. Made from GitHub's markdown; emoji are left out (Windows draws them as empty boxes here).
class NotesView : Control
{
    class Run { public string Text; public bool Bold; }
    class Block { public int Kind; public List<Run> Runs = new List<Run>(); }   // 0 paragraph, 1 bullet, 2 heading, 3 version
    readonly List<List<Block>> cards = new List<List<Block>>();
    public NotesView() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); SetStyle(ControlStyles.Selectable, false); }

    static readonly System.Text.RegularExpressions.Regex Link = new System.Text.RegularExpressions.Regex(@"!?\[([^\]]*)\]\([^)]*\)");
    // Emoji and pictographs (and their joiners): GDI has no colour emoji.
    public static string NoEmoji(string s)
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < s.Length) { i++; continue; }
            if (char.IsSurrogate(c) || c == '️' || c == '‍' || (c >= '⌀' && c <= '⏿') || (c >= '☀' && c <= '➿') || (c >= '⬀' && c <= '⯿')) continue;
            b.Append(c);
        }
        return System.Text.RegularExpressions.Regex.Replace(b.ToString(), "  +", " ").Trim();
    }
    static List<Run> Runs(string s)
    {
        s = NoEmoji(Link.Replace(s, "$1").Replace("`", "").Replace("__", "**"));
        List<Run> o = new List<Run>();
        string[] parts = s.Split(new[] { "**" }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++) if (parts[i].Length > 0) o.Add(new Run { Text = parts[i], Bold = i % 2 == 1 });
        return o;
    }
    // One card: "Version X" (when there are several), then the notes. Sections on how to download it are left out:
    // it's already installed.
    public void Add(string version, string markdown)
    {
        List<Block> c = new List<Block>();
        if (version != null) { Block vb = new Block { Kind = 3 }; vb.Runs.Add(new Run { Text = "Version " + version, Bold = true }); c.Add(vb); }
        bool skip = false;
        foreach (string raw in (markdown ?? "").Replace("\r", "").Split('\n'))
        {
            string t = raw.Trim();
            if (t.StartsWith("#"))
            {
                string h = NoEmoji(t.TrimStart('#').Replace("**", "")).Trim();
                string lo = h.ToLowerInvariant();
                skip = lo.StartsWith("how to get") || lo.StartsWith("how to update") || lo.StartsWith("how to download") || lo.StartsWith("how to install")
                    || lo == "download" || lo == "downloads" || lo == "install";
                if (!skip && h.Length > 0) { Block hb = new Block { Kind = 2 }; hb.Runs.Add(new Run { Text = h, Bold = true }); c.Add(hb); }
                continue;
            }
            if (skip || t.Length == 0 || t.StartsWith("|") || t.StartsWith("---") || t.StartsWith("<") || t.StartsWith("```") || t.StartsWith(">")) continue;
            bool bullet = t.StartsWith("- ") || t.StartsWith("* ");
            List<Run> r = Runs(bullet ? t.Substring(2) : t);
            if (r.Count > 0) c.Add(new Block { Kind = bullet ? 1 : 0, Runs = r });
        }
        if (c.Count > (version != null ? 1 : 0)) cards.Add(c);
    }
    public bool Empty { get { return cards.Count == 0; } }

    // Lays everything out at this width; draws it too when g isn't null. Returns the height needed.
    public int Flow(Graphics g, int width)
    {
        float K = Card.K;
        int pad = (int)(18 * K), y = 0;
        Font body = Theme.Body ?? Font, bold = Theme.Strong ?? body, head = Theme.Title ?? bold, ver = Theme.CardTitle ?? head;
        TextFormatFlags tf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        int space = TextRenderer.MeasureText("a a", body, Size.Empty, tf).Width - TextRenderer.MeasureText("aa", body, Size.Empty, tf).Width;
        foreach (List<Block> c in cards)
        {
            int top = y, cy = top + pad;
            List<Action<Graphics>> draws = new List<Action<Graphics>>();
            for (int bi = 0; bi < c.Count; bi++)
            {
                Block b = c[bi];
                if (bi > 0) cy += b.Kind == 2 || b.Kind == 3 ? (int)(14 * K) : (int)(6 * K);
                int indent = b.Kind == 1 ? (int)(18 * K) : 0, x0 = pad + indent, maxX = width - pad;
                Font nf = b.Kind == 3 ? ver : b.Kind == 2 ? head : body, bf = b.Kind >= 2 ? nf : bold;
                Color col = b.Kind == 2 || b.Kind == 3 ? Theme.Accent : Theme.Text;
                int lh = TextRenderer.MeasureText("Ag", nf, Size.Empty, tf).Height + (int)(2 * K);
                if (b.Kind == 1)
                {
                    float d = 6 * K; RectangleF dot = new RectangleF(pad + 4 * K, cy + (lh - d) / 2f, d, d);
                    draws.Add(gg => { using (SolidBrush br = new SolidBrush(Theme.Accent)) gg.FillEllipse(br, dot); });
                }
                // words wrap at the edge; each line is drawn in pieces of one font (measured whole, so the spaces
                // are the font's own)
                int line = cy, sx = x0;           // where the piece being built starts
                string piece = ""; Font pf = nf;
                bool gapNext = false;             // a space before the next word
                Action flush = delegate
                {
                    if (piece.Length == 0) return;
                    Point at = new Point(sx, line); Font ff = pf; string text = piece;
                    draws.Add(gg => TextRenderer.DrawText(gg, text, ff, at, col, tf));
                    sx += TextRenderer.MeasureText(piece + "x", pf, Size.Empty, tf).Width - TextRenderer.MeasureText("x", pf, Size.Empty, tf).Width; piece = "";   // without the measuring's own margin
                };
                foreach (Run r in b.Runs)
                {
                    Font f = r.Bold ? bf : nf;
                    string[] words = r.Text.Split(' ');
                    for (int wi = 0; wi < words.Length; wi++)
                    {
                        string w = words[wi];
                        if (w.Length == 0) { gapNext = true; continue; }
                        bool lineEmpty = piece.Length == 0 && sx == x0;
                        if (f != pf && piece.Length > 0) flush();
                        if (piece.Length == 0) { pf = f; if (gapNext && !lineEmpty) sx += space; }
                        string cand = piece.Length > 0 && gapNext ? piece + " " + w : piece + w;
                        if (!lineEmpty && sx + TextRenderer.MeasureText(cand, f, Size.Empty, tf).Width > maxX)
                        {
                            flush(); line += lh; sx = x0; cand = w; pf = f;
                        }
                        piece = cand; gapNext = wi < words.Length - 1;
                    }
                }
                flush();
                cy = line + lh;
            }
            int bottom = cy + pad;
            if (g != null)
            {
                Theme.FillRound(g, Theme.Card, Theme.Stroke, new RectangleF(0.5f, top + 0.5f, width - 1.5f, bottom - top - 1.5f), 14 * K, 1);
                foreach (Action<Graphics> a in draws) a(g);
            }
            y = bottom + (int)(12 * K);
        }
        return Math.Max(0, y - (int)(12 * K));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Window);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Flow(e.Graphics, Width);
    }
}

// A small rounded label: a coloured dot and the text (ForeColor), like "Up to date".
class StatusChip : Label
{
    public StatusChip() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    public int Needed(int h) { return TextRenderer.MeasureText(Text, Font, new Size(4000, h), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + (int)(h * 0.42f + h * 0.28f + 5) + (int)(h * 0.5f); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (r.Width < 4) return;
        int one = (int)Math.Round(26 * Card.K);   // the height of a one-line chip
        int lh = Math.Min(Height, one);
        Theme.FillRound(g, Theme.Input, Theme.Stroke, r, lh / 2f, 1);
        float d = lh * 0.28f, x = lh * 0.42f;
        using (SolidBrush b = new SolidBrush(ForeColor)) g.FillEllipse(b, x, (lh - d) / 2f, d, d);
        int tx = (int)(x + d + 5), tw = Width - tx - (int)(lh * 0.36f);
        if (Height <= one + 2)
            TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, tw, Height), ForeColor,
                                  TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        else
        {
            int line = TextRenderer.MeasureText("Ag", Font).Height, pad = Math.Max(0, (lh - line) / 2);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, pad, tw, Height - 2 * pad), ForeColor,
                                  TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
}

// A rounded square in the tile's colour with the emulator's logo (or its initials when it has none).
class Badge : Control
{
    public Color Tone = Color.Empty;
    Image picture; Bitmap scaled;   // scaled: the logo at the size it is drawn (made once)
    public Image Picture
    {
        get { return picture; }
        set { picture = value; if (scaled != null) { scaled.Dispose(); scaled = null; } Invalidate(); }
    }
    public Badge() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing && scaled != null) { scaled.Dispose(); scaled = null; } base.Dispose(disposing); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color t = Tone.IsEmpty ? Theme.Accent : Tone;
        Theme.FillRound(g, Theme.Blend(Theme.Card, t, 0.40), Color.Empty, new RectangleF(0, 0, Width - 1, Height - 1), Height * 0.29f, 0);
        if (picture != null && Height > 8)
        {
            int pad = Math.Max(2, Height / 9), size = Height - 2 * pad;
            if (scaled == null || scaled.Width != size) { if (scaled != null) scaled.Dispose(); scaled = EmuForm.Smooth(picture, size); }
            g.DrawImage(scaled, new Rectangle(pad, pad, size, size), 0, 0, size, size, GraphicsUnit.Pixel);
        }
        else TextRenderer.DrawText(g, Text, Theme.Title, ClientRectangle, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

// Small round badges, overlapping: who uses these saves (logo, or initials when there is none).
class Avatars : Control
{
    public List<Tuple<string, Color, Image>> People = new List<Tuple<string, Color, Image>>();
    public Avatars() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float d = Height - 3, x = 1.5f;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        foreach (var p in People)
        {
            using (SolidBrush b = new SolidBrush(Theme.Blend(Theme.Card, p.Item2.IsEmpty ? Theme.Accent : p.Item2, 0.5))) g.FillEllipse(b, x, 1.5f, d, d);
            if (p.Item3 != null)
            {
                float inset = d * 0.16f;
                try { g.DrawImage(p.Item3, new RectangleF(x + inset, 1.5f + inset, d - 2 * inset, d - 2 * inset)); } catch { }   // a picture just replaced: drawn next time
            }
            else TextRenderer.DrawText(g, p.Item1, Theme.Caption, new Rectangle((int)x, 1, (int)d + 1, (int)d + 1), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            using (Pen pen = new Pen(Theme.Card, 2.2f)) g.DrawEllipse(pen, x, 1.5f, d, d);
            x += d * 0.74f;
        }
    }
}

// A ring split into parts (sizes), the total in the middle, and a legend beside it.
class Ring : Control
{
    public List<Tuple<string, long, Color>> Parts = new List<Tuple<string, long, Color>>();
    public string Center = "";
    public Ring() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    public static string Bytes(long b) { return b >= (1L << 30) ? (b / 1073741824.0).ToString(b >= (10L << 30) ? "0" : "0.0") + " GB" : b >= (1L << 20) ? (b >> 20) + " MB" : b > 0 ? "<1 MB" : "0 MB"; }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float K = Card.K, d = Height - 4, th = d * 0.15f;
        RectangleF r = new RectangleF(2 + th / 2, 2 + th / 2, d - th, d - th);
        using (Pen bg = new Pen(Theme.Input, th)) g.DrawEllipse(bg, r);
        long total = Parts.Sum(p => p.Item2);
        float a = -90;
        if (total > 0)
            foreach (var p in Parts)
            {
                float sweep = 360f * p.Item2 / total;
                if (sweep >= 0.5f) using (Pen pen = new Pen(p.Item3, th)) g.DrawArc(pen, r, a, Math.Max(0.8f, sweep - (Parts.Count > 1 ? 2f : 0)));
                a += sweep;
            }
        TextRenderer.DrawText(g, Center, Theme.Title, new Rectangle(2, 2, (int)d, (int)d), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        int row = (int)(22 * K), lx = (int)(d + 18 * K), ly = (int)((Height - Parts.Count * row) / 2f);
        foreach (var p in Parts)
        {
            using (SolidBrush b = new SolidBrush(p.Item3)) Theme.FillRound(g, p.Item3, Color.Empty, new RectangleF(lx, ly + (row - 9 * K) / 2f, 9 * K, 9 * K), 2.5f * K, 0);
            TextRenderer.DrawText(g, p.Item1 + "  " + Bytes(p.Item2), Theme.Small, new Rectangle(lx + (int)(15 * K), ly, Width - lx - (int)(15 * K), row), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            ly += row;
        }
    }
}

// Seven bars (one per day), drawn with the accent gradient.
// This week's new versions, a bar a day from Sunday to Saturday, each with its day under it (today's in the accent;
// the days still to come are faint).
class Bars : Control
{
    public int[] Values = new int[7];
    public int Today = -1;   // 0 = Sunday ... 6 = Saturday
    static readonly string[] Names = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    public Bars() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int n = Values.Length, max = Math.Max(1, Values.Max());
        Font f = Theme.Small ?? Font;
        int lh = TextRenderer.MeasureText("Sun", f).Height, bh = Math.Max(8, Height - lh - (int)(2 * Card.K));
        float gap = 6 * Card.K, bw = (Width - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            bool later = Today >= 0 && i > Today;
            float h = Math.Max(4, (bh - 2) * Values[i] / (float)max), x = i * (bw + gap);
            RectangleF r = new RectangleF(x, bh - h, bw, h);
            if (later) Theme.FillRound(g, Theme.Blend(Theme.Card, Theme.Accent2, 0.15), Color.Empty, r, Math.Min(4 * Card.K, bw / 2), 0);
            else
                using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(x, 0, bw, bh + 1), Theme.Accent, Theme.Blend(Theme.Card, Theme.Accent2, 0.35), 90f))
                using (GraphicsPath p = Theme.Round(r, Math.Min(4 * Card.K, bw / 2)))
                    g.FillPath(b, p);
            if (n == 7)
                TextRenderer.DrawText(g, Names[i], f, new Rectangle((int)x - 4, Height - lh, (int)bw + 8, lh),
                                      i == Today ? Theme.Accent : later ? Theme.Disabled : Theme.Muted,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
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
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float h = Math.Min(6, Height), y = (Height - h) / 2f;
        Theme.FillRound(g, Theme.Input, Color.Empty, new RectangleF(0, y, Width - 1, h), h / 2, 0);
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
        Theme.ParentBack(g, this);
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

// One colour scheme in the Colors window: a small preview in its own colours, its name, and a ring when chosen.
class Swatch : Control
{
    public string[] Scheme;
    public Swatch() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); Cursor = Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Anim.To(this, 0, 1); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Anim.To(this, 0, 0); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space) { e.Handled = true; OnClick(EventArgs.Empty); }   // Enter stays with Done
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnChangeUICues(UICuesEventArgs e) { base.OnChangeUICues(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.ParentBack(g, this);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float K = Card.K;
        Color bg = Theme.Hex(Scheme[2]), tile = Theme.Hex(Scheme[3]), line = Theme.Hex(Scheme[5]), text = Theme.Hex(Scheme[6]),
              acc = Theme.Hex(Scheme[8]), acc2 = Theme.Hex(Scheme[9]), glow = Theme.Hex(Scheme[10]);
        bool on = Scheme[0] == Theme.Scheme;
        float hot = Anim.Get(this, 0, 0);
        RectangleF r = new RectangleF(1.5f, 1.5f, Width - 3.5f, Height - 3.5f);
        Theme.FillRound(g, bg, Color.Empty, r, 14 * K, 0);
        using (GraphicsPath clip = Theme.Round(r, 14 * K))
        {
            GraphicsState st = g.Save(); g.SetClip(clip, CombineMode.Intersect);
            Theme.GlowAt(g, glow, new RectangleF(Width * 0.35f, -Height * 0.6f, Width * 1.1f, Height * 1.2f), 230);
            g.Restore(st);
        }
        // a little tile with a Launch button
        RectangleF t = new RectangleF(12 * K, 12 * K, Width - 24 * K, Height - 52 * K);
        Theme.FillRound(g, tile, line, t, 10 * K, 1);
        RectangleF pill = new RectangleF(t.X + 10 * K, t.Bottom - 22 * K, t.Width * 0.58f, 13 * K);
        using (GraphicsPath pp = Theme.Round(pill, pill.Height / 2))
        using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(pill.X - 1, pill.Y - 1, pill.Width + 2, pill.Height + 2), acc, acc2, 30f))
            g.FillPath(b, pp);
        using (SolidBrush b = new SolidBrush(Theme.Blend(tile, acc, 0.45))) g.FillEllipse(b, t.X + 10 * K, t.Y + 10 * K, 14 * K, 14 * K);
        using (SolidBrush b = new SolidBrush(Theme.Blend(tile, text, 0.55))) g.FillRectangle(b, t.X + 30 * K, t.Y + 13 * K, t.Width * 0.38f, 3 * K);
        using (SolidBrush b = new SolidBrush(Theme.Blend(tile, text, 0.30))) g.FillRectangle(b, t.X + 30 * K, t.Y + 20 * K, t.Width * 0.25f, 3 * K);
        TextRenderer.DrawText(g, Scheme[1], Theme.Strong, new Rectangle((int)(14 * K), Height - (int)(38 * K), Width - (int)(28 * K), (int)(30 * K)), text,
                              TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        if (on)
        {   // a tick in the corner
            float d = 18 * K; RectangleF c = new RectangleF(Width - 14 * K - d, Height - 14 * K - d - 2 * K, d, d);
            using (GraphicsPath cp = new GraphicsPath()) { cp.AddEllipse(c); using (LinearGradientBrush b = new LinearGradientBrush(c, acc, acc2, 30f)) g.FillPath(b, cp); }
            using (Pen pen = new Pen(Theme.Hex(Scheme[12]), 1.8f * K)) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLines(pen, new[] { new PointF(c.X + d * 0.27f, c.Y + d * 0.52f), new PointF(c.X + d * 0.44f, c.Y + d * 0.68f), new PointF(c.X + d * 0.74f, c.Y + d * 0.34f) }); }
        }
        Color edge = on ? acc : Theme.Blend(line, Theme.Blend(line, text, 0.35), hot);
        Theme.FillRound(g, Color.Empty, edge, r, 14 * K, on ? 2.2f * K : 1);
        if (Focused && ShowFocusCues)   // the keyboard is on this one (only shown when using the keyboard)
            Theme.FillRound(g, Color.Empty, Theme.Blend(line, text, 0.8), new RectangleF(r.X + 4 * K, r.Y + 4 * K, r.Width - 8 * K, r.Height - 8 * K), 11 * K, 1.4f * K);
    }
}

// One emulator's card on the Builds page.
class EmuCard
{
    public EmuDef Def;
    public Card Box = new Card();
    public Label Note = new Label(), State = new StatusChip(), Data = new Label(), Ver = new Label();   // State: the status chip; Ver: "Version ..."
    public Label Title = new Label();
    public Badge Badge = new Badge();
    public bool ChipTop;          // the status chip sits in the top right corner (else on a line of its own)
    public bool Joined;           // uses the shared data (or is the one everyone uses)
    // Launch when installed; Install when not; next to Launch, a greyed "Up to date" that becomes "Update" when a newer version is out. "..." holds the rest.
    public Button Update = new Button(), Launch = new Button(), More = new Button();
    public LinkLabel Existing = new LinkLabel(), Next = new LinkLabel();   // "I already have it" / "Next: add your keys..."
    public FlatBar Bar = new FlatBar();
    public CheckBox Share = new CheckBox();   // "Share this emulator's data": ticked on the one whose data all of them use
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
    readonly Panel buildsPage = new GlowPanel(), cardsHost = new GlowPanel();
    readonly Label head = new Label(), footer = new Label();
    readonly PictureBox logoBox = new PictureBox();
    readonly DiscordTag credit = new DiscordTag();   // "made by" on Discord, next to the title
    readonly LinkLabel mgrLink = new LinkLabel();     // "Update the manager to 1.2.14" when GitHub has a newer manager
    Release mgrRel; Version mgrVer;
    readonly Button addEmu = new Button(), menuBtn = new Button(), logCopy = new Button(), saveBtn = new Button();
    readonly LinkLabel checkLink = new LinkLabel(), logLink = new LinkLabel();
    readonly TextBox logBox = new TextBox();
    Form logForm;
    int closedAsking;   // an "X closed" question is open
    readonly Button logHelp = new Button();   // "Copy for help": what someone helping on Discord needs, ready to paste
    readonly Label logHint = new Label();
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
    readonly Label gamesLbl = new Label(), gamesCap = new Label(), games1Lbl = new Label();
    readonly Button gamesPick = new Button(), games1Pick = new Button();
    // the small tiles under the emulators: saves, storage, updates, manager (the games folder tile is gamesCard)
    readonly Card savesTile = new Card(), storeTile = new Card(), updTile = new Card(), mgrTile = new Card();
    readonly Label savesCap = new Label(), savesBig = new Label(), savesUnit = new Label(), savesLine = new Label();
    readonly LinkLabel savesLink = new LinkLabel();
    readonly Avatars savesWho = new Avatars();
    readonly Label storeCap = new Label();
    readonly Ring storeRing = new Ring();
    readonly Label updCap = new Label(), updText = new Label(), updWhen = new Label(), updLine = new Label();
    readonly Bars updBars = new Bars();
    readonly Label mgrCap = new Label(), mgrText = new Label();
    readonly StatusChip mgrChip = new StatusChip();
    readonly Button mgrGo = new Button();   // "Update" on the manager tile when a newer manager is out
    static readonly Dictionary<Control, string> fontRoles = new Dictionary<Control, string>();   // caption / big / value / name / small / large / strong
    static readonly HashSet<Control> toggles = new HashSet<Control>();   // check boxes drawn as on/off switches
    DateTime checkedAt = DateTime.MinValue;   // last update check that finished
    bool checking, mgrChecked, layoutQueued;
    Size laidOut;
    readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
    // no title bar: the header is the top of the window; these are its minimize / maximize / close buttons
    readonly CaptionButton winMin = new CaptionButton(), winMax = new CaptionButton(), winClose = new CaptionButton();
    readonly List<HitThrough> through = new List<HitThrough>();
    int headerBottom;
    readonly System.Windows.Forms.Timer hoverClock = new System.Windows.Forms.Timer();
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    const string APP = "NS2 Emulator Manager";

    // The Switch 1 games folder is shown once there's a Switch 1 emulator on the list (or it's been set).
    static bool ShowSwitch1Games() { return Core.Cfg.Emulators.Any(e => Core.SwitchOf(e) == 1) || !string.IsNullOrEmpty(Core.Cfg.Games1Folder); }

    bool games1Shown;   // the tile has the Switch 1 row (its height follows this, not the label's Visible)
    void GamesText()
    {
        bool two = ShowSwitch1Games();
        games1Shown = two;
        gamesCap.Text = two ? "Games folders" : "Games folder";
        GamesRow(gamesLbl, gamesPick, Core.Cfg.GamesFolder, two ? "Switch 2:  " : "", two ? "Choose the folder with your Switch 2 games." : "Choose the folder with your games.");
        games1Lbl.Visible = games1Pick.Visible = two;
        if (two) GamesRow(games1Lbl, games1Pick, Core.Cfg.Games1Folder, "Switch 1:  ", "Choose the folder with your Switch 1 games.");
    }
    void GamesRow(Label l, Button b, string g, string label, string empty)
    {
        bool set = !string.IsNullOrEmpty(g);
        l.Text = !set ? empty : label + g + (Directory.Exists(g) ? "" : "   (not found)");
        roles[l] = set && Directory.Exists(g) ? "text" : "info";
        l.ForeColor = RoleColor(l);
        b.Text = set ? "Change..." : "Choose...";
        if (set) accents.Remove(b); else Outline(b, Theme.Accent);
        b.Invalidate();
    }

    // sw: 2 = the Switch 2 games folder, 1 = the Switch 1 one
    void PickGames(int sw)
    {
        string cur = Core.GamesFolderOf(sw);
        string picked = FolderPicker.Pick(this, sw == 1 ? "Choose the folder with your Switch 1 games" : ShowSwitch1Games() ? "Choose the folder with your Switch 2 games" : "Choose the folder with your games",
                                          !string.IsNullOrEmpty(cur) && Directory.Exists(cur) ? cur : null);
        if (picked == null) return;
        if (sw == 1) Core.Cfg.Games1Folder = picked; else Core.Cfg.GamesFolder = picked;
        try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save the games folder: " + ex.Message); }
        GamesText();
        Log((sw == 1 ? "Switch 1 games folder" : ShowSwitch1Games() ? "Switch 2 games folder" : "Games folder") + " set to " + picked);
        SyncGames(cur, sw, true);
    }

    // Writes each installed build's games folder into its game list (or just one, before it launches).
    void SyncGames(string previous, int previousFor, bool all, EmuDef only = null)
    {
        int n = 0;
        foreach (EmuCard c in cards)
        {
            if (only != null && c.Def != only) continue;
            try { string m = Core.SyncGamesFolder(c.Def, previous, previousFor); if (m != null) { Log(m); n++; } }
            catch (Exception ex) { Log(c.Def.Name + ": could not set the game folder: " + ex.Message); }
        }
        if (all && n == 0) Log("Every installed build already lists its games folder.");
    }

    // The card's "Switch 1 or Switch 2..." : which games it plays, so it lists the right games folder.
    void ChooseSwitch(EmuCard c)
    {
        int cur = Core.SwitchOf(c.Def);
        int r = Ask("Switch 1 or Switch 2?", c.Def.Name + " is set as a Switch " + cur + " emulator, so it lists your Switch " + cur + " games folder.\r\n\r\nWhich games does it play?",
                    cur == 1 ? "Switch 1 games" : "Switch 2 games", cur == 1 ? "Switch 2 games" : "Switch 1 games", "Cancel");   // its current choice first
        if (r != 0 && r != 1) return;
        int sw = r == 0 ? cur : 3 - cur;
        if (sw == cur && c.Def.SwitchVersion == sw) return;
        c.Def.SwitchVersion = sw;
        try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }
        Log(c.Def.Name + " is now a Switch " + sw + " emulator.");
        GamesText(); DoLayout();
        SyncGames(null, 0, false, c.Def);
        if (sw == 1 && string.IsNullOrEmpty(Core.Cfg.Games1Folder))
            SetFooter("Choose your Switch 1 games folder in the Games folders tile.", Theme.Info);
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
    // Launch only from a mouse click on it (not Space / Enter on a focused button), and never a second copy.
    DateTime activatedAt = DateTime.MinValue, launchDown = DateTime.MinValue;
    bool RealLaunchClick(EmuCard c)
    {
        double sinceDown = (DateTime.Now - launchDown).TotalMilliseconds;
        if (sinceDown > 3000) { Log(c.Def.Name + ": Launch ignored (not a mouse click)."); return false; }
        // already open (a stray click, or a second one): don't start it twice
        if (Core.Running(c.Def).Count > 0)
        {
            Log(c.Def.Name + ": Launch ignored (it's already open).");
            c.State.Text = c.Def.Name + " is already open."; roles[c.State] = "info"; c.State.ForeColor = RoleColor(c.State);
            return false;
        }
        return true;
    }

    void DoLaunch(EmuCard c)
    {
        if (!RealLaunchClick(c)) return;
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
        if (Core.Portable && Installed(c.Def) && !UserData.IsPortable(c.Def)) { MakePortable(c, true); return; }
        c.Err = null;
        string block = UserData.LaunchProblem(c.Def);
        if (block != null) { Tell("Can't launch yet", block); return; }
        if (!WarnOtherEmulators(c)) return;
        try { string gm = Core.SyncGamesFolder(c.Def, null, 0, false); if (gm != null) Log(gm); }   // a Switch 1 emulator: not the Switch 2 games (only takes out; only writes on a change)
        catch (Exception ex) { Log(c.Def.Name + ": could not check its game list: " + ex.Message); }
        try
        {
            DateTime started = DateTime.Now;
            Process p = Core.Launch(c.Def);
            Log("Launched " + Path.Combine(c.Def.Dir, c.Def.Exe));
            if (p != null)
                ThreadPool.QueueUserWorkItem(delegate
                {   // closed again within 8 s: tell the user instead of failing silently
                    bool quit = false; int code = 0;
                    try { quit = p.WaitForExit(8000); if (quit) code = p.ExitCode; } catch { }
                    if (quit) UI(delegate
                    {
                        Log(c.Def.Name + " closed right after starting (exit code " + code + ").");
                        // Eden-based emulators can restart themselves (the first process ends, a new window opens): fine
                        if (Core.Running(c.Def).Count > 0) { Log(c.Def.Name + " is running again (it restarted itself)."); return; }
                        bool others = cards.Any(k => k != c && Installed(k.Def) && Core.Running(k.Def).Count > 0);
                        int kind; List<string> errs;
                        string why = EarlyClose(c.Def, code, started, others, out kind, out errs);
                        foreach (string e in errs) Log("  " + c.Def.Name + "'s log: " + e);
                        if (why == null) return;   // closed normally, nothing wrong in its log: most likely the user closed it
                        int cut = why.IndexOf(": "), dot = why.IndexOf(". ");   // the card: the first sentence; the window: all of it
                        Fail(c, cut > 0 ? why.Substring(0, cut) + "." : dot > 0 ? why.Substring(0, dot + 1) : why, why);
                        // the card says it already: no question over another window (or another of these)
                        if (IsBusy || closedAsking > 0 || OwnedForms.Any(o => o.Visible && o != logForm) || (Form.ActiveForm != null && Form.ActiveForm != this)) return;
                        string[] buttons = kind == 1 ? new[] { "Set up keys and firmware", "Show log", "OK" }
                                         : kind == 3 ? new[] { "Open Microsoft's page", "OK" }
                                         : new[] { "Show log", "OK" };
                        int a;
                        closedAsking++;
                        try { a = Ask(c.Def.Name + " closed", why, buttons); } finally { closedAsking--; }
                        if (a < 0 || buttons[a] == "OK") return;
                        if (buttons[a] == "Show log") ShowLog();
                        else if (kind == 1) SetupHelp(c);
                        else if (kind == 3) try { Process.Start("https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist"); } catch { }
                    });
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
    readonly AddTile addTile = new AddTile();   // fills the free part of the last row of emulator cards
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
        Text = APP + (Core.Portable ? " (portable)" : "") + (Core.Cfg.Sandbox && Demo == null ? "  -  TEST" : "");   // --demo: screenshots, without the test label
        Icon = AppIcon();
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.None;   // DoLayout scales by the screen DPI itself
        using (Graphics g = CreateGraphics()) k = Math.Max(1f, g.DpiX / 96f);
        Card.K = k;
        ClientSize = new Size(D(1040), D(780));
        MinimumSize = new Size(D(480), D(420));
        StartPosition = FormStartPosition.CenterScreen;
        RestoreWindow();
        if (Demo != null && Environment.GetEnvironmentVariable("NS2_DEMO_OFFSCREEN") == "1") { StartPosition = FormStartPosition.Manual; Location = new Point(-30000, -30000); }
        DoubleBuffered = true;

        Controls.Add(buildsPage);
        // header
        try { using (Stream s = typeof(EmuForm).Assembly.GetManifestResourceStream("app.png")) if (s != null) using (Bitmap b = new Bitmap(s)) logoBox.Image = Smooth(b, D(56)); } catch { }
        try
        {   // screenshots of other logos (test environment only)
            string lf = Demo != null ? Environment.GetEnvironmentVariable("NS2_DEMO_LOGO") : null;
            if (!string.IsNullOrEmpty(lf) && File.Exists(lf)) using (Bitmap b = new Bitmap(lf)) logoBox.Image = Smooth(b, D(56));
        }
        catch { }
        logoBox.SizeMode = PictureBoxSizeMode.CenterImage;
        head.Text = Core.Cfg.Sandbox && Demo == null ? APP + " (test)" : APP; head.Font = Theme.Header ?? new Font("Segoe UI Semibold", 16f); head.AutoEllipsis = true;
        addEmu.Text = "Add emulator fork"; addEmu.Click += delegate { ShowRecommended(false); };
        Outline(addEmu, Theme.Accent);
        menuBtn.Text = "···"; menuBtn.Click += delegate { MainMenu().Show(menuBtn, new Point(0, menuBtn.Height)); };
        saveBtn.Text = "Themes"; saveBtn.Click += delegate { ShowColors(); }; dotted.Add(saveBtn);
        tips.SetToolTip(saveBtn, "Choose the colours");
        tips.SetToolTip(menuBtn, "More");
        buildsPage.Controls.AddRange(new Control[] { logoBox, head, addEmu, menuBtn, credit, saveBtn });
        winMin.Kind = 0; winMax.Kind = 1; winClose.Kind = 2;
        winMin.Click += delegate { WindowState = FormWindowState.Minimized; };
        winMax.Click += delegate { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
        winClose.Click += delegate { Close(); };
        tips.SetToolTip(winMin, "Minimize"); tips.SetToolTip(winClose, "Close");
        buildsPage.Controls.AddRange(new Control[] { winMin, winMax, winClose });
        // the empty part of the header, the title and the logo move the window (double-click: maximize), like a title bar
        ((GlowPanel)buildsPage).Through = p => p.Y < headerBottom;
        foreach (Control c in new Control[] { head, logoBox })
        {
            Control cc = c;
            cc.HandleCreated += delegate { HitThrough h = new HitThrough(); h.AssignHandle(cc.Handle); through.Add(h); };
        }
        tips.SetToolTip(credit, "Made by " + DiscordTag.NAME + " on Discord. Click to copy the name.");
        mgrLink.Visible = false; mgrLink.TextAlign = ContentAlignment.MiddleRight; mgrLink.LinkBehavior = LinkBehavior.HoverUnderline;
        mgrLink.LinkColor = mgrLink.ActiveLinkColor = mgrLink.VisitedLinkColor = Theme.Accent;
        mgrLink.LinkClicked += delegate { UpdateManager(); };
        buildsPage.Controls.Add(mgrLink);
        cardsHost.AutoScroll = true;
        buildsPage.Controls.Add(cardsHost);
        // the small tiles
        foreach (Card t in new[] { gamesCard, savesTile, storeTile, updTile, mgrTile }) { t.Text = ""; t.Shift = 0; t.Radius = D(18); t.Hover = true; }
        gamesCap.Text = "Games folder"; storeCap.Text = "Storage"; updCap.Text = "Updates"; mgrCap.Text = "Manager";
        foreach (Label l in new[] { gamesCap, savesCap, storeCap, updCap, mgrCap }) { fontRoles[l] = "caption"; roles[l] = "muted"; l.AutoEllipsis = true; }
        gamesLbl.AutoEllipsis = true; fontRoles[gamesLbl] = "large";
        games1Lbl.AutoEllipsis = true; fontRoles[games1Lbl] = "large";
        gamesLbl.UseMnemonic = games1Lbl.UseMnemonic = false;
        gamesPick.Click += delegate { PickGames(2); };
        games1Pick.Click += delegate { PickGames(1); };
        gamesCard.Controls.AddRange(new Control[] { gamesCap, gamesLbl, gamesPick, games1Lbl, games1Pick });
        savesLink.Text = "Save Helper ›"; savesLink.LinkClicked += delegate { SaveHelper(); }; savesLink.TextAlign = ContentAlignment.MiddleRight;
        fontRoles[savesBig] = "big"; fontRoles[savesUnit] = "value"; roles[savesUnit] = "muted"; roles[savesLine] = "muted"; savesLine.AutoEllipsis = true;
        savesTile.Controls.AddRange(new Control[] { savesCap, savesLink, savesBig, savesUnit, savesLine, savesWho });
        storeTile.Controls.AddRange(new Control[] { storeCap, storeRing });
        fontRoles[updText] = "value"; updText.AutoEllipsis = true; roles[updWhen] = "muted"; roles[updLine] = "muted"; updLine.AutoEllipsis = true; updWhen.AutoEllipsis = true;
        updTile.Controls.AddRange(new Control[] { updCap, updText, updWhen, updBars, updLine });
        fontRoles[mgrText] = "value"; mgrText.AutoEllipsis = true; fontRoles[mgrChip] = "small";
        mgrChip.Click += delegate { if (mgrRel != null) UpdateManager(); };
        mgrGo.Text = "Update"; mgrGo.Visible = false; mgrGo.Click += delegate { UpdateManager(); };
        mgrTile.Controls.AddRange(new Control[] { mgrCap, mgrText, mgrChip, mgrGo });
        cardsHost.Controls.AddRange(new Control[] { savesTile, storeTile, updTile, gamesCard, mgrTile });
        clock.Interval = 30000; clock.Tick += delegate { if (WindowState != FormWindowState.Minimized) InfoText(); };
        clock.Start();
        // which tile is under the mouse (MouseEnter/Leave can't tell: a tile's own buttons take the mouse)
        hoverClock.Interval = 50;
        hoverClock.Tick += delegate
        {
            if (WindowState == FormWindowState.Minimized) return;
            Point mp = Control.MousePosition;
            Control under = null;
            try { under = Control.FromChildHandle(WindowFromPoint(mp)); } catch { }
            Control hit = under;
            while (hit != null && hit.Parent != cardsHost) hit = hit.Parent;   // the tile the pointer is over (null: none)
            foreach (Control c in cardsHost.Controls)
            {
                Card t = c as Card;
                if (t == null || !t.Hover) continue;
                Anim.To(t, 0, t.Visible && hit == t ? 1 : 0, true);
            }
        };
        hoverClock.Start();
        emptyCard.Shift = 0; emptyCard.Radius = D(18);
        addTile.Radius = D(20); addTile.Visible = false;
        addTile.Click += delegate { ShowRecommended(false); };
        tips.SetToolTip(addTile, "Add another emulator, from the list or from a link");
        cardsHost.Controls.Add(addTile);
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
        SharingFromOlderVersions();

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
        Activated += delegate { activatedAt = DateTime.Now; if (!IsBusy) { RefreshAll(); ScanInfo(); } if (dupRetry && !IsBusy) BeginInvoke((MethodInvoker)AskDuplicates); if ((joinPending || joinWhy.Count > 0) && !IsBusy) { joinPending = false; joinWhy.Clear(); JoinSharing(null); } };
        // first start: the welcome list (nothing is on the list yet); afterwards the usual update check
        Shown += delegate
        {
            Version before = null;
            try { before = Demo == null && !Core.Cfg.Sandbox ? PreviousVersion() : null; } catch { }
            Log("--- NS2 Emulator Manager " + Core.AppVersion + " started (" + Application.ExecutablePath + ")");
            if (before != null) WhatsNew(before);
            else if (Demo == null && !Core.Cfg.Sandbox)   // a new install (or an unreadable file): this version is the one to compare with next time
                try { File.WriteAllText(LastVersionFile, Core.AppVersion.ToString(3)); } catch { }
            if (Demo == null) OnScreen();
            FitHeight();
            ScanInfo();   // Activated usually started it already
            if (Core.RestoredFromBackup) { Log("emulators.json was damaged: the backup copy (emulators.json.bak) was used."); SetFooter("Your settings were restored from a backup. Your emulators and saves are untouched.", Theme.Warn); }
            if (Core.Cfg.Sandbox && Demo != null) { BeginInvoke((MethodInvoker)RunDemo); CheckAll(); return; }
            if (Core.FirstRun) { Core.FirstRun = false; ShowRecommended(true); }
            CheckAll();
            JoinSharing(null);   // one that didn't join before (the manager was closed or restarted) tries again
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
    // ---- "What's new" once after the manager was updated
    // DataDir\last-version.txt holds the version that last ran (and saw its notes). Without it (1.3.8 and older never
    // wrote it), the last start line in the log says which version ran before. Null: nothing to show (a new install).
    static string LastVersionFile { get { return Path.Combine(Core.DataDir, "last-version.txt"); } }
    static readonly System.Text.RegularExpressions.Regex StartLine = new System.Text.RegularExpressions.Regex(@"--- NS2 Emulator Manager (\d+\.\d+(?:\.\d+)?) started");
    static Version PreviousVersion()
    {
        Version v;
        if (File.Exists(LastVersionFile)) return Version.TryParse(File.ReadAllText(LastVersionFile).Trim(), out v) ? v : null;
        foreach (string f in new[] { LogPath, Path.Combine(Core.DataDir, "manager.old.log") })
        {
            if (!File.Exists(f)) continue;
            System.Text.RegularExpressions.MatchCollection m = StartLine.Matches(File.ReadAllText(f));
            if (m.Count == 0) continue;
            if (!Version.TryParse(m[m.Count - 1].Groups[1].Value, out v)) return null;
            // from now on the file says it, so an update started with no internet still shows its notes next time
            try { File.WriteAllText(LastVersionFile, v.ToString()); } catch { }
            return v;
        }
        return null;
    }

    // An older version ran last: show the notes of every manager release since, newest first. Asked in the background;
    // without an answer (no internet) the file stays as it is, so it's tried again next start.
    void WhatsNew(Version before)
    {
        Version now = new Version(Core.AppVersion.Major, Core.AppVersion.Minor, Math.Max(0, Core.AppVersion.Build));
        bool demo = Demo != null;
        Action seen = delegate { if (!demo) try { File.WriteAllText(LastVersionFile, now.ToString()); } catch { } };
        if (before >= now) { if (before != now) seen(); return; }
        ThreadPool.QueueUserWorkItem(delegate
        {
            List<KeyValuePair<Version, Release>> notes = null; string err = null;
            try { notes = Core.ManagerNotes(before, now); } catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                if (err != null) { FileLog("Couldn't get the manager's release notes: " + err); return; }
                if (demo ? notes.Count == 0 : !notes.Any(k => k.Key == now)) { FileLog("The notes for " + now + " aren't on GitHub (yet): what's new is shown on a later start."); return; }
                Action show = null;
                show = delegate
                {
                    if (IsDisposed) return;
                    // after any other window (Welcome, a question) has closed
                    if (!Enabled || Application.OpenForms.Cast<Form>().Any(f => f != this && f.Visible && f.Modal) || WindowState == FormWindowState.Minimized)
                    {
                        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 1500;
                        t.Tick += delegate { t.Stop(); t.Dispose(); show(); }; t.Start(); return;
                    }
                    seen();
                    Log("Showing what's new since " + before + ".");
                    WhatsNewWindow(before, now, notes);
                };
                show();
            });
        });
    }

    void WhatsNewWindow(Version before, Version now, List<KeyValuePair<Version, Release>> notes)
    {
        NotesView nv = new NotesView();
        foreach (KeyValuePair<Version, Release> k in notes) nv.Add(notes.Count > 1 ? k.Key.ToString() : null, k.Value.Body);
        if (nv.Empty) { nv.Dispose(); return; }
        QuietForm f = new QuietForm();
        f.Text = "What's new"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
        f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
        int W = D(620), pad = D(22);
        Label t = new Label(); t.Text = "The manager was updated to " + now; t.Font = Theme.Name ?? Theme.Header; t.ForeColor = Theme.Text; t.AutoEllipsis = true;
        t.SetBounds(pad, D(18), W - 2 * pad, D(34));
        Label s = new Label(); s.ForeColor = Theme.Muted; s.AutoEllipsis = true; s.SetBounds(pad, D(54), W - 2 * pad, D(22));
        s.Text = notes.Count > 1 ? "Here's what changed since " + before + "." : "Here's what changed.";
        Panel box = new Panel(); box.AutoScroll = true; box.BackColor = Theme.Window;
        int maxBox = D(430), full = W - 2 * pad, inner = full - SystemInformation.VerticalScrollBarWidth - D(4);
        int need = nv.Flow(null, full), bw = full;
        if (need > maxBox) { bw = inner; need = nv.Flow(null, inner); }
        int bh = Math.Min(need, maxBox);
        box.SetBounds(pad, D(88), full, bh);
        nv.SetBounds(0, 0, bw, need);
        box.Controls.Add(nv);
        Button ok = new Button(); ok.Text = "Got it"; ok.SetBounds(W - pad - D(120), D(88) + bh + D(18), D(120), D(36)); ok.Click += delegate { f.Close(); };
        f.Controls.AddRange(new Control[] { t, s, box, ok });
        StyleButton(ok); ok.BackColor = Theme.Window; Outline(ok, Theme.Accent);
        f.AcceptButton = ok; f.CancelButton = ok;
        f.ClientSize = new Size(W, ok.Bottom + D(18));
        f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); Native.ControlTheme(box.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer"); };
        f.Shown += delegate { ok.Focus(); };
        if (Demo != null)
        {   // screenshots: left open, over the window
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(Left + (Width - f.Width) / 2, Top + (Height - f.Height) / 2);
            f.FormClosed += delegate { f.Dispose(); };
            f.Show(this); return;
        }
        f.ShowDialog(this); f.Dispose();
    }

    void RunDemo()
    {
        EmuCard c = cards.FirstOrDefault(k => Demo.EndsWith(":" + k.Def.Id)) ?? cards.FirstOrDefault();
        string n = Demo.Split(':')[0];
        if (n == "welcome") ShowRecommended(true);
        else if (n == "whatsnew") { string[] wp = Demo.Split(':'); Version b; if (wp.Length > 1 && Version.TryParse(wp[1], out b)) WhatsNew(b); }   // whatsnew:1.3.7
        else if (n == "add") ShowRecommended(false);
        else if (n == "link") AddFork();
        else if (n == "shared") SharedDialog();
        else if (n == "menu") MainMenu().Show(menuBtn, new Point(0, menuBtn.Height));
        else if (n == "cardmenu" && c != null) CardMenu(c).Show(c.More, new Point(0, c.More.Height));
        else if (n == "cardfolders" && c != null)   // the card menu with "Open folder" open
        {
            ContextMenuStrip m = CardMenu(c); m.Show(c.More, new Point(0, c.More.Height));
            ToolStripMenuItem f = m.Items.OfType<ToolStripMenuItem>().FirstOrDefault(x => x.Text == "Open folder");
            if (f != null) f.ShowDropDown();
        }
        else if (n == "setup" && c != null) SetupHelp(c);
        else if (n == "portable" && c != null) MakePortable(c);
        else if (n == "remove" && c != null) RemoveFork(c);
        else if (n == "rename" && c != null) RenameEmu(c);
        else if (n == "switch" && c != null) ChooseSwitch(c);
        else if (n == "update" && c != null) CheckOne(c, delegate { if (c.Rel != null) DoUpdate(c); });
        else if (n == "log") ShowLog();
        else if (n == "launch" && c != null) LaunchNow(c);   // the check after a launch (a test emulator that closes)
        else if (n == "loghelp") { ShowLog(); File.WriteAllText(Path.Combine(Core.Home, "help.txt"), HelpText()); }   // what Copy for help copies
        else if (n == "main") { }   // just the main window
        else if ((n == "versions" || n == "versionsopen") && c != null) ChooseVersion(c);
        else if (n == "share" && c != null) ToggleShare(c);
        else if (n == "saves" || n == "savestall") SaveHelper();
        else if (n == "colors") ShowColors();
    }
    protected override bool ShowWithoutActivation { get { return Demo != null; } }

    // ---- Save Helper: where this setup's saves are, in plain words, with a button to open each place
    Form saveForm;
    void SaveHelper()
    {
        if (saveForm != null && !saveForm.IsDisposed) { saveForm.Activate(); return; }
        Form f = new Form(); saveForm = f;
        f.Text = "Save Helper"; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text; f.Icon = Icon; f.ShowInTaskbar = false;
        f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(D(700), D(640)); f.MinimumSize = new Size(D(520), D(360));
        if (Demo == "savestall") f.ClientSize = new Size(D(700), D(1500));   // test screenshots: everything at once
        Panel host = new Panel(); host.AutoScroll = true; host.Dock = DockStyle.Fill; host.BackColor = Theme.Window; f.Controls.Add(host);
        Action fill = null;
        fill = () =>
        {
            int keep = host.VerticalScroll.Value;
            host.SuspendLayout();
            foreach (Control old in host.Controls.Cast<Control>().ToList()) old.Dispose();
            host.Controls.Clear();
            try { BuildSaveHelper(host, Math.Max(D(400), host.ClientSize.Width - D(8)), fill); }
            catch (Exception ex) { Log("Save Helper: " + ex.Message); }
            host.ResumeLayout();
            try { host.AutoScrollPosition = new Point(0, keep); } catch { }
        };
        f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
        f.Shown += delegate { fill(); };
        f.ResizeEnd += delegate { fill(); };
        f.Show(this);
    }

    // Where removed things go, in plain words: the Recycle Bin, or the portable folder's own "Removed items" (USB sticks have no bin).
    static string Bin { get { return Core.Portable ? "the " + Core.REMOVED + " folder" : "the Recycle Bin"; } }

    // Save Helper's "Remove duplicate backups": firmware backups and exact copies go to the Recycle Bin.
    void RemoveDuplicateBackups(Action refill)
    {
        if (IsBusy) { PleaseWait(); return; }
        anyBusy = true; RefreshAll(); SetFooter("Looking for duplicate backups...", Theme.Info);
        ThreadPool.QueueUserWorkItem(delegate
        {
            List<KeyValuePair<string, long>> drop = null; string err = null;
            try { drop = UserData.DuplicateBackups(); } catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                anyBusy = false; RefreshAll(); SetFooter("", Theme.Muted);
                if (err != null) { Log("Duplicate backups: " + err); Tell("Save Helper", "That didn't work. Try again."); return; }
                if (drop.Count == 0) { Tell("Save Helper", "There are no duplicate backups."); return; }
                long bytes = drop.Sum(p => p.Value);
                string size = bytes >= (1L << 30) ? (bytes / 1073741824.0).ToString("0.0") + " GB" : Math.Max(1, bytes >> 20) + " MB";
                if (Ask("Remove duplicate backups", "Move " + drop.Count + " backup" + (drop.Count == 1 ? "" : "s") + " you don't need (" + size + ") to " + Bin + "?\r\n\r\nThese are firmware copies and exact copies of other backups. Backups with saves you might want are kept.", "Remove", "Cancel") != 0) return;
                anyBusy = true; RefreshAll(); SetFooter("Removing duplicate backups...", Theme.Info);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    int done = 0; string err2 = null;
                    foreach (KeyValuePair<string, long> p in drop)
                        try { UserData.Recycle(p.Key, s => UI(delegate { Log(s); })); done++; }
                        catch (Exception ex) { err2 = ex.Message; UI(delegate { Log("Could not remove " + p.Key + ": " + ex.Message); }); }
                    UI(delegate
                    {
                        anyBusy = false; RefreshAll();
                        SetFooter(err2 == null ? "Removed " + done + " duplicate backup" + (done == 1 ? "" : "s") + " (" + size + ")." : "Some backups couldn't be removed. See the log.", err2 == null ? Theme.Success : Theme.Danger);
                        if (refill != null) refill();
                    });
                });
            });
        });
    }

    // A folder path that fits: the start and the end stay, the middle becomes "...".
    static string ShortPath(string p, Font font, int width)
    {
        if (TextRenderer.MeasureText(p, font).Width <= width) return p;
        string[] parts = p.Split('\\');
        if (parts.Length < 4) return p;
        string head = parts[0] + "\\" + parts[1];
        for (int keep = parts.Length - 2; keep >= 1; keep--)
        {
            string t = head + "\\...\\" + string.Join("\\", parts.Skip(parts.Length - keep));
            if (TextRenderer.MeasureText(t, font).Width <= width) return t;
        }
        return "...\\" + parts[parts.Length - 1];
    }

    void BuildSaveHelper(Panel host, int W, Action refill)
    {
        int y = D(16), x = D(20), w = W - D(40);
        Func<string, Font, Color, Label> text = (t, font, color) =>
        {
            Label l = new Label(); l.Text = t; l.Font = font ?? Font; l.ForeColor = color; l.BackColor = Theme.Window;
            l.MaximumSize = new Size(w, 0); l.AutoSize = true; l.Location = new Point(x, y);
            host.Controls.Add(l); y += l.PreferredHeight + D(6); return l;
        };
        Action<string> heading = t => { y += D(10); text(t, Theme.Title, Theme.Text); y += D(2); };
        // one place: a card with what it is, the folder, and Open
        Action<string, string, string> place = (title, note, path) =>
        {
            Card box = new Card(); box.Text = ""; box.Shift = 0; box.SetBounds(x, y, w, D(70)); host.Controls.Add(box);
            Label t = new Label(); t.Text = title; t.Font = Theme.CardTitle; t.AutoEllipsis = true; t.SetBounds(D(14), D(8), w - D(130), D(22)); t.BackColor = Theme.Card; t.ForeColor = Theme.Text;
            Label n = new Label(); n.Text = note; n.AutoEllipsis = true; n.SetBounds(D(14), D(30), w - D(130), D(18)); n.BackColor = Theme.Card; n.ForeColor = Theme.Muted;
            Label pth = new Label(); pth.Text = ShortPath(path ?? "", Font, w - D(134)); pth.SetBounds(D(14), D(48), w - D(130), D(18)); pth.BackColor = Theme.Card; pth.ForeColor = Theme.Muted;
            tips.SetToolTip(pth, path);
            Button open = new Button(); open.Text = "Open"; open.SetBounds(w - D(104), D(18), D(90), D(34)); open.Enabled = path != null && Directory.Exists(path);
            string target = path;
            open.Click += delegate { Explore(target); };
            box.Controls.AddRange(new Control[] { t, n, pth, open });
            StyleButton(open); open.BackColor = Theme.Card;
            y += D(78);
        };
        Func<int, string> games = n => n == 0 ? "no game saves yet" : n == 1 ? "saves for 1 game" : "saves for " + n + " games";

        text("Save Helper", Theme.Header ?? Theme.Title, Theme.Text);
        text("Where your saves are, and what happens to them. Nothing here changes anything; Open only shows the folder.", null, Theme.Muted);

        // what each emulator uses now
        heading("Your saves right now");
        List<EmuCard> inst = cards.Where(k => Installed(k.Def)).ToList();
        if (inst.Count == 0) text("No emulators yet.", null, Theme.Muted);
        EmuDef hostDef = Core.Cfg.ShareHost == null ? null : Core.Cfg.Emulators.FirstOrDefault(e => e.Id == Core.Cfg.ShareHost);
        bool anyShared = false;
        foreach (EmuCard k in inst)
        {
            UserData.SaveKind kind; string where = UserData.SavesOf(k.Def, out kind);
            int n = UserData.CountGames(where);
            string note;
            if (kind == UserData.SaveKind.Shared) { anyShared = true; note = (hostDef == k.Def ? "Shares its saves with your other emulators" : "Uses the shared saves") + " (" + games(n) + ")."; }
            else if (kind == UserData.SaveKind.Own) note = "Keeps its own saves (" + games(n) + ").";
            else if (kind == UserData.SaveKind.OtherFolder) note = "Uses saves in another folder, probably from an earlier install (" + games(n) + ").";
            else if (kind == UserData.SaveKind.AppData) note = "Keeps its saves outside its folder (" + games(n) + "). \"Keep its saves in its own folder\" in its ... menu moves a copy in.";
            else note = "No saves yet.";
            place(k.Def.Name, note, where);
        }
        if (anyShared)
            text("Emulators that share all use the same saves: progress in one shows up in all of them.", null, Theme.Muted);

        // saves that were put aside
        List<Tuple<string, string, DateTime>> aside = UserData.SavedAside();
        heading("Backups");
        if (aside.Count == 0) text("No saves have been put aside.", null, Theme.Muted);
        else
        {
            text("When sharing starts, saves that aren't used are kept here instead of being deleted. Restore (when you stop sharing) uses them too.", null, Theme.Muted);
            Button dedupe = new Button(); dedupe.Text = "Remove duplicate backups"; dedupe.SetBounds(x, y, D(220), D(34)); host.Controls.Add(dedupe);
            StyleButton(dedupe); dedupe.BackColor = Theme.Window;
            tips.SetToolTip(dedupe, "Moves backups you don't need (firmware, and exact copies) to " + Bin);
            dedupe.Click += delegate { RemoveDuplicateBackups(refill); };
            y += D(44);
            foreach (var a in aside.Take(20))
                place(a.Item1 + "'s saves", "Put aside on " + a.Item3.ToString("d MMM yyyy, HH:mm") + " (" + games(UserData.CountGames(a.Item2)) + ").", a.Item2);
            if (aside.Count > 20) text("And " + (aside.Count - 20) + " older ones in the same folder.", null, Theme.Muted);
        }

        // other places worth knowing
        List<KeyValuePair<string, string>> other = new List<KeyValuePair<string, string>>();
        foreach (EmuCard k in inst)
        {
            if (!UserData.IsPortable(k.Def)) continue;
            string a = UserData.AppDataSource(k.Def);   // its saves from before it kept its own (copied in; the originals stay)
            if (a != null && UserData.CountGames(Path.Combine(a, @"nand\user\save")) > 0)
                other.Add(new KeyValuePair<string, string>("Saves in the Windows app data folder (" + Path.GetFileName(a.TrimEnd('\\')) + ")", Path.Combine(a, @"nand\user\save")));
        }
        if (other.Count > 0)
        {
            heading("Other places");
            foreach (var o in other.GroupBy(o => o.Value, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
                place(o.Key, "Used when an emulator has no folder of its own. Never changed by the manager (" + games(UserData.CountGames(o.Value)) + ").", o.Value);
        }

        // the situations, plainly
        heading("How saves work");
        foreach (string line in new[]
        {
            "Not sharing: each emulator keeps its own saves in its own folder.",
            "Sharing (\"Share data with other emulators\" on a card): all your emulators use one set of saves, so progress in one shows up in all of them.",
            "When sharing starts: saves for games the shared saves don't have are added to them. If two emulators have a save for the same game, the ticked one's is used and the other goes to Backups.",
            "When sharing stops: \"Keep latest\" gives every emulator a copy of the latest saves. \"Restore\" gives the others back the saves they had before. The ticked one always keeps the latest.",
            "Updating an emulator never touches its saves. Older versions (in Older builds) don't contain saves.",
            Core.Portable ? "Everything is in the manager's folder: copy the whole folder to back it up or take it to another PC. Anything the manager removes goes to its " + Core.REMOVED + " folder."
                          : "Uninstalling the manager keeps your emulators and saves. Anything the manager removes goes to the Recycle Bin.",
        })
            text("- " + line, null, Theme.Text);

        heading("Back up your saves");
        text("Close your emulators, then copy the folders under \"Your saves right now\" (the shared one is enough when you share) to a USB stick or a cloud folder. To bring them back, copy them back the same way.", null, Theme.Text);

        y += D(6);
        Button again = new Button(); again.Text = "Check again"; again.SetBounds(x, y, D(130), D(34)); host.Controls.Add(again);
        StyleButton(again); again.BackColor = Theme.Window;
        again.Click += delegate { refill(); };
        LinkLabel bin = new LinkLabel(); bin.Text = Core.Portable ? "Open " + Core.REMOVED : "Open the Recycle Bin"; bin.SetBounds(x + D(146), y + D(8), D(220), D(22)); bin.BackColor = Theme.Window;
        bin.LinkColor = bin.ActiveLinkColor = bin.VisitedLinkColor = Theme.Accent; bin.LinkBehavior = LinkBehavior.HoverUnderline;
        bin.LinkClicked += delegate
        {
            try
            {
                if (Core.Portable) { Directory.CreateDirectory(UserData.RemovedRoot); Explore(UserData.RemovedRoot); }
                else Process.Start("explorer.exe", "shell:RecycleBinFolder");
            }
            catch { }
        };
        host.Controls.Add(bin);
        y += D(50);
        Label pad = new Label(); pad.SetBounds(x, y, 1, 1); host.Controls.Add(pad);   // room at the bottom when scrolling
    }

    // ---- why an emulator closed within seconds of starting, in plain words (from its exit code and its own log)
    // Null when it looks like a normal close (exit code 0, no errors in its log, nothing else open): most likely the
    // user closed it. kind: 0 anything else, 1 keys or firmware missing, 2 graphics driver, 3 a Windows part missing.
    // errs: the error lines its log wrote during this start (for the manager's log).
    static string EarlyClose(EmuDef d, int code, DateTime started, bool others, out int kind, out List<string> errs)
    {
        kind = 0;
        List<string> tail = EmuLogTail(d, started, 60);
        errs = tail.Where(l => l.Contains("<Critical>") || l.Contains("<Error>")).Select(l => l.Trim()).ToList();
        if (errs.Count > 8) errs = errs.Skip(errs.Count - 8).ToList();
        uint uc = unchecked((uint)code);
        string name = d.Name, hex = "0x" + uc.ToString("X8");
        List<string> crit = errs.Where(l => l.Contains("<Critical>")).ToList();
        // exit code 0: it closed normally (most likely the user closed it), unless its log says something critical
        // went wrong; ordinary <Error> lines are common and harmless then
        if (code == 0 && crit.Count == 0) return null;
        // only the messages ("Class <Level> file:line:Func: message"), not the class or file names ("Render_Vulkan")
        string low = string.Join("\n", (code == 0 ? crit : errs).Select(Message)).ToLowerInvariant();
        Func<string[], bool> says = words => words.Any(w => low.Contains(w));
        if (uc == 0xC0000135 || uc == 0xC0000139 || uc == 0xC000007B)
        {
            kind = 3;
            return name + " couldn't start because a part of Windows it needs is missing (" + hex + "). Install the latest "
                 + "\"Microsoft Visual C++ Redistributable\" (X64) from Microsoft's page, restart your PC, then try again.";
        }
        if (!HasKeys(d) || says(new[] { "prod.keys", "title.keys", "missing keys", "key area key", "keys are missing" }))
        {
            kind = 1;
            return name + " closed right away. It doesn't have its keys yet: it needs the keys from your own Switch.";
        }
        if (!HasFirmware(d) || says(new[] { "firmware is not installed", "firmware not found", "missing firmware" }))
        {
            kind = 1;
            return name + " closed right away. It doesn't have its firmware yet: it needs the firmware from your own Switch.";
        }
        if (says(new[] { "vk_error", "failed to create vulkan", "vkcreateinstance", "vkcreatedevice", "failed to initialize vulkan",
                         "vulkan is not supported", "vulkan not supported", "vulkan-1.dll", "no suitable gpu", "gpu not supported", "graphics driver" }))
        {
            kind = 2;
            return name + " closed because it couldn't use your graphics card. Update your graphics driver (from the NVIDIA, "
                 + "AMD or Intel website), restart your PC, then try again.";
        }
        if (others) return name + " closed right away. Close the other emulators and try again.";
        if (uc == 0xC0000017 || says(new[] { "bad_alloc", "out of memory" }))
            return name + " ran out of memory. Close other programs and try again.";
        return name + " closed right away" + (code != 0 ? " (error " + hex + ")" : "") + ". Try again. If it keeps happening, "
             + "click Show log, then Copy for help, and paste it on Discord.";
    }

    // The message part of an emulator log line: "[ 0.4] Render_Vulkan <Critical> vulkan_device.cpp:12:Create: Failed..."
    // -> "Failed...".
    static string Message(string line)
    {
        int lv = line.IndexOf("<Critical>"); if (lv < 0) lv = line.IndexOf("<Error>");
        if (lv < 0) return line;
        int m = line.IndexOf(": ", lv);
        return m < 0 ? line.Substring(lv) : line.Substring(m + 2);
    }

    // The last lines of an emulator's own log (user\log, Eden-based emulators: eden_log.txt), only if it was written
    // since "since" (DateTime.MinValue: any). Read without locking it: the emulator may still be writing.
    static List<string> EmuLogTail(EmuDef d, DateTime since, int lines)
    {
        try
        {
            string dir = Path.Combine(d.Dir, "user", "log");
            if (!Directory.Exists(dir)) return new List<string>();
            FileInfo f = new DirectoryInfo(dir).GetFiles().Where(x => !x.Name.Contains(".old")).OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
            if (f == null || f.LastWriteTime < since.AddSeconds(-2)) return new List<string>();
            string text;
            using (FileStream fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length > 256 * 1024) fs.Seek(-256 * 1024, SeekOrigin.End);
                using (StreamReader r = new StreamReader(fs)) text = r.ReadToEnd();
            }
            List<string> all = text.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
            return all.Skip(Math.Max(0, all.Count - lines)).ToList();
        }
        catch { return new List<string>(); }
    }

    // ---- "Copy for help": the manager, Windows, the PC and each emulator in a few lines, then the end of the log.
    // The Windows user folder is replaced by %USERPROFILE% (the user name stays private when it's pasted on Discord).
    string HelpText()
    {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        Action<string> L = s => b.Append(s).Append("\r\n");
        L("NS2 Emulator Manager " + Core.AppVersion.ToString(3) + (Core.Portable ? " (portable)" : " (installed)"));
        L("Windows: " + WindowsName());
        L("Processor: " + (Reg(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "?").Trim());
        try { L("Memory: " + Math.Round(new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / 1073741824.0) + " GB"); } catch { }
        foreach (string g in GraphicsCards()) L("Graphics: " + g);
        try { DriveInfo di = new DriveInfo(Path.GetPathRoot(Core.Home)); L("Drive " + di.Name.TrimEnd('\\') + " " + di.DriveFormat + ", " + di.AvailableFreeSpace / 1073741824 + " GB free"); } catch { }
        L("");
        L("Emulators:");
        foreach (EmuCard c in cards)
        {
            EmuDef d = c.Def;
            Manifest m = null; try { m = Core.LoadManifest(d.Dir); } catch { }
            bool inst = Installed(d);
            L("- " + d.Name + " (Switch " + Core.SwitchOf(d) + ", " + (d.Repo ?? "?") + "): "
              + (inst ? "version " + (m != null ? m.Tag : "?") + ", keys " + (HasKeys(d) ? "yes" : "no") + ", firmware " + (HasFirmware(d) ? "yes" : "no")
                      + (c.Joined ? ", shares data" : "") + (Core.Running(d).Count > 0 ? ", open now" : "") : "not installed")
              + (c.Err != null ? "\r\n    Problem: " + c.Err : ""));
        }
        foreach (EmuCard c in cards.Where(k => k.Err != null && Installed(k.Def)))
        {
            List<string> t = EmuLogTail(c.Def, DateTime.MinValue, 25);
            if (t.Count == 0) continue;
            L("");
            L("End of " + c.Def.Name + "'s own log:");
            foreach (string l in t) L(l);
        }
        L("");
        L("End of the manager's log:");
        try
        {
            string text;
            using (FileStream fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader r = new StreamReader(fs)) text = r.ReadToEnd();
            List<string> all = text.Replace("\r", "").Split('\n').Where(l => l.Length > 0).ToList();
            foreach (string l in all.Skip(Math.Max(0, all.Count - 60))) L(l);
        }
        catch { L("(couldn't read it)"); }
        string s2 = b.ToString();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            s2 = System.Text.RegularExpressions.Regex.Replace(s2, System.Text.RegularExpressions.Regex.Escape(home), "%USERPROFILE%", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s2 = System.Text.RegularExpressions.Regex.Replace(s2, System.Text.RegularExpressions.Regex.Escape(home.Replace('\\', '/')), "%USERPROFILE%", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return s2;
    }
    static string Reg(string key, string name) { try { object v = Microsoft.Win32.Registry.GetValue(key, name, null); return v == null ? null : v.ToString(); } catch { return null; } }
    static string WindowsName()
    {
        const string k = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        string build = Reg(k, "CurrentBuildNumber") ?? Reg(k, "CurrentBuild") ?? "?", ubr = Reg(k, "UBR"), ed = Reg(k, "EditionID") ?? "", ver = Reg(k, "DisplayVersion") ?? Reg(k, "ReleaseId") ?? "";
        int bn; int.TryParse(build, out bn);
        ed = ed == "Core" || ed == "CoreSingleLanguage" || ed == "CoreCountrySpecific" ? "Home" : ed == "Professional" ? "Pro" : ed;   // the names Windows shows
        return (bn >= 22000 ? "Windows 11" : "Windows 10") + (ed.Length > 0 ? " " + ed : "") + (ver.Length > 0 ? " " + ver : "") + " (build " + build + (ubr != null ? "." + ubr : "") + ")"
             + (Environment.Is64BitOperatingSystem ? ", 64-bit" : ", 32-bit");
    }
    // The graphics cards in the PC (Windows' device list), with their driver version and date. Without that list, the
    // installed graphics drivers (which may include a card that was taken out).
    static List<string> GraphicsCards()
    {
        List<string> o = new List<string>();
        try
        {
            using (System.Management.ManagementObjectSearcher q = new System.Management.ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController"))
            using (System.Management.ManagementObjectCollection all = q.Get())
                foreach (System.Management.ManagementBaseObject v in all)
                {
                    string desc = v["Name"] as string, ver = v["DriverVersion"] as string, date = v["DriverDate"] as string;
                    if (string.IsNullOrEmpty(desc) || desc.Contains("Basic Display") || desc.Contains("Remote")) continue;
                    try { if (date != null) date = System.Management.ManagementDateTimeConverter.ToDateTime(date).ToString("M-d-yyyy"); } catch { date = null; }
                    string line = desc + (ver != null ? ", driver " + ver : "") + (date != null ? " (" + date + ")" : "");
                    if (!o.Contains(line)) o.Add(line);
                }
        }
        catch { }
        if (o.Count > 0) return o;
        try
        {
            using (Microsoft.Win32.RegistryKey cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                if (cls != null)
                    foreach (string sub in cls.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
                        try
                        {
                            using (Microsoft.Win32.RegistryKey k = cls.OpenSubKey(sub))
                            {
                                string desc = k == null ? null : k.GetValue("DriverDesc") as string;
                                if (string.IsNullOrEmpty(desc) || desc.Contains("Basic Display") || desc.Contains("Remote")) continue;
                                string ver = k.GetValue("DriverVersion") as string, date = k.GetValue("DriverDate") as string;
                                string line = "driver for " + desc + (ver != null ? ", " + ver : "") + (date != null ? " (" + date + ")" : "");
                                if (!o.Contains(line)) o.Add(line);
                            }
                        }
                        catch { }
        }
        catch { }
        if (o.Count == 0) o.Add("?");
        return o;
    }

    void ShowLog()
    {
        if (logForm == null || logForm.IsDisposed)
        {
            logForm = new Form();
            logForm.Text = "Log"; logForm.Font = Font; logForm.BackColor = Theme.Window; logForm.ForeColor = Theme.Text; logForm.Icon = Icon;
            logForm.ClientSize = new Size(D(760), D(480)); logForm.StartPosition = FormStartPosition.CenterParent; logForm.ShowInTaskbar = false;
            logForm.Controls.Add(logBox); logForm.Controls.Add(logCopy); logForm.Controls.Add(logHelp); logForm.Controls.Add(logHint);
            StyleButton(logCopy); logCopy.BackColor = Theme.Window;
            logHelp.Text = "Copy for help"; StyleButton(logHelp); logHelp.BackColor = Theme.Window; Outline(logHelp, Theme.Accent);
            logHint.Text = "Asking for help on Discord? Click Copy for help, then paste it in your message.";
            logHint.ForeColor = Theme.Muted; logHint.AutoEllipsis = true; logHint.TextAlign = ContentAlignment.MiddleLeft;
            logHelp.Click += delegate
            {
                string t = HelpText();
                bool ok = false;
                for (int i = 0; i < 3 && !ok; i++) try { Clipboard.SetText(t); ok = true; } catch { Thread.Sleep(100); }   // another program may hold the clipboard
                logHelp.Text = ok ? "Copied" : "Try again";
                if (ok) Log("Copied the help info (" + t.Length + " characters).");
                System.Windows.Forms.Timer back = new System.Windows.Forms.Timer(); back.Interval = 2000;
                back.Tick += delegate { back.Stop(); back.Dispose(); if (!logHelp.IsDisposed) logHelp.Text = "Copy for help"; };
                back.Start();
            };
            Action lay = () =>
            {
                int cw = logForm.ClientSize.Width, ch = logForm.ClientSize.Height;
                logBox.SetBounds(D(12), D(12), cw - D(24), ch - D(64));
                logCopy.SetBounds(cw - D(112), ch - D(44), D(100), D(32));
                logHelp.SetBounds(cw - D(112) - D(8) - D(140), ch - D(44), D(140), D(32));
                logHint.SetBounds(D(12), ch - D(44), Math.Max(D(40), logHelp.Left - D(24)), D(32));
            };
            logForm.Resize += delegate { lay(); }; lay();
            logForm.FormClosing += delegate (object s, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; logForm.Hide(); } };
            logForm.HandleCreated += delegate { Native.Apply(logForm.Handle, Theme.Dark, 0); Native.ControlTheme(logBox.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer"); };
        }
        logBox.BackColor = Theme.Input; logBox.ForeColor = Theme.Text; logBox.BorderStyle = BorderStyle.FixedSingle;
        logHint.ForeColor = Theme.Muted; logHint.BackColor = Theme.Window; logHelp.BackColor = logCopy.BackColor = Theme.Window;
        if (!logForm.Visible) logForm.Show(this);
        logForm.Activate();
        logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret();
    }

    ContextMenuStrip MakeMenu(params object[] items)   // items: "text", Action, ... ; null = separator
    {
        ContextMenuStrip m = new ContextMenuStrip();
        m.Renderer = new MenuLook();
        m.Padding = new Padding(0, D(4), 0, D(4));
        m.HandleCreated += delegate { Native.RoundPopup(m.Handle); };
        FadeIn(m);
        m.Font = Font; m.ShowImageMargin = false;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) { m.Items.Add(new ToolStripSeparator()); continue; }
            m.Items.Add(MenuItem((string)items[i], items[++i]));
        }
        m.Closed += delegate { BeginInvoke((MethodInvoker)delegate { m.Dispose(); }); };
        return m;
    }

    // One menu item: an Action, or object[] for a sub-menu (the same "text", Action ... ; null = separator).
    ToolStripItem MenuItem(string t, object what)
    {
        ToolStripMenuItem it = new ToolStripMenuItem(t); it.ForeColor = Theme.Text; it.Padding = new Padding(D(4), D(3), D(4), D(3));
        object[] sub = what as object[];
        if (sub == null) { Action a = (Action)what; it.Click += delegate { a(); }; return it; }
        ToolStripDropDownMenu dd = new ToolStripDropDownMenu();
        dd.Renderer = new MenuLook(); dd.Padding = new Padding(0, D(4), 0, D(4)); dd.Font = Font; dd.ShowImageMargin = false;
        dd.HandleCreated += delegate { Native.RoundPopup(dd.Handle); };
        for (int i = 0; i < sub.Length; i++)
        {
            if (sub[i] == null) { dd.Items.Add(new ToolStripSeparator()); continue; }
            dd.Items.Add(MenuItem((string)sub[i], sub[++i]));
        }
        it.DropDown = dd;
        it.Disposed += delegate { dd.Dispose(); };   // a drop-down set by hand isn't disposed with its item
        return it;
    }

    // A menu fades in over a few frames when it opens.
    static void FadeIn(ToolStripDropDown m)
    {
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 15;
        Stopwatch sw = new Stopwatch();
        m.Opening += delegate { m.Opacity = 0.01; };
        m.Opened += delegate { sw.Restart(); t.Start(); };
        t.Tick += delegate
        {
            double v = Math.Min(1, sw.ElapsedMilliseconds / 130.0);
            if (m.IsDisposed) { t.Stop(); t.Dispose(); return; }
            m.Opacity = 1 - (1 - v) * (1 - v);
            if (v >= 1) t.Stop();
        };
        m.Disposed += delegate { t.Stop(); t.Dispose(); };
    }

    ContextMenuStrip MainMenu()
    {
        List<object> items = IsBusy ? new List<object>() : new List<object> { "Add from link...", (Action)AddFork, "Save Helper", (Action)SaveHelper, null };
        items.AddRange(new object[] {
            "Themes...", (Action)ShowColors, "Check for updates", (Action)delegate { CheckAll(true); }, "Show log", (Action)ShowLog, "Open install folder", (Action)delegate { Explore(Core.Home); } });
        if (mgrRel != null && !IsBusy) { items.Insert(0, (Action)UpdateManager); items.Insert(0, "Update the manager to " + mgrVer); }
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
        // versions
        if (c.Rel != null) { items.Add("What's new"); items.Add((Action)delegate { ShowNotes(c); }); }
        if (free) { items.Add("Choose version..."); items.Add((Action)delegate { ChooseVersion(c); }); }
        if (free && Core.Backups(d).Count > 0) { items.Add("Go back to previous version"); items.Add((Action)delegate { DoRestore(c); }); }
        if (free && !inst) { items.Add("I already have it"); items.Add((Action)delegate { UseExisting(c); }); }
        // its data
        if (free && inst && UserData.IsPortable(d)) { items.Add(null); items.Add("Set up keys and firmware"); items.Add((Action)delegate { SetupHelp(c); }); }
        if (free && inst && !UserData.IsPortable(d)) { items.Add(null); items.Add("Keep its saves in its own folder"); items.Add((Action)delegate { MakePortable(c); }); }
        // folders and its page
        items.Add(null);
        items.Add("Open folder"); items.Add(FolderItems(c));
        string site = SiteName(d);
        items.Add("Open on " + site); items.Add((Action)delegate { try { Process.Start(ProjectPage(d)); } catch { } });
        // name and look
        items.Add(null);
        items.Add("Rename..."); items.Add((Action)delegate { RenameEmu(c); });
        items.Add("Edit note"); items.Add((Action)delegate { EditNote(c); });
        items.Add("Change icon..."); items.Add((Action)delegate { ChangeIcon(c); });
        if (File.Exists(IconFile(d))) { items.Add("Reset icon"); items.Add((Action)delegate { ResetIcon(c); }); }
        items.Add("Switch 1 or Switch 2..."); items.Add((Action)delegate { ChooseSwitch(c); });
        if (free) { items.Add(null); items.Add("Remove from list"); items.Add((Action)delegate { RemoveFork(c); }); }
        return MakeMenu(items.ToArray());
    }

    // "GitHub", or the name of the Gitea/Forgejo server (git.eden-emu.dev -> "Gitea") for the menu.
    static string SiteName(EmuDef d) { return d.Host == "gitea" ? "Gitea" : "GitHub"; }
    // The project's own page (its code, releases and issues).
    static string ProjectPage(EmuDef d)
    {
        if (d.Host == "gitea" && !string.IsNullOrEmpty(d.ApiBase))
        {
            string b = d.ApiBase.TrimEnd('/');
            if (b.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase)) b = b.Substring(0, b.Length - 7);
            return b + "/" + d.Repo;
        }
        return "https://github.com/" + d.Repo;
    }

    // "Open folder" > the emulator's folders. Its data is in its own "user" folder (or, for a build that doesn't keep its
    // own, in %APPDATA%). Keys, firmware and mods folders are made on the click when the emulator has a "user" folder,
    // since that's where the user puts them; the others open the nearest folder that exists.
    object[] FolderItems(EmuCard c)
    {
        EmuDef d = c.Def;
        List<object> o = new List<object>();
        string emuDir = Directory.Exists(d.Dir) ? d.Dir : Path.GetDirectoryName(d.Dir.TrimEnd('\\'));
        o.Add("Emulator"); o.Add((Action)delegate { Explore(emuDir); });
        bool own = Installed(d) && UserData.IsPortable(d);
        string data = own ? Path.Combine(d.Dir, "user") : Installed(d) ? UserData.AppDataSource(d) : null;
        if (data != null)
        {
            o.Add(null);
            Action<string, string, bool> add = (label, rel, make) =>
            {
                string full = Path.Combine(data, rel);
                o.Add(label); o.Add((Action)delegate { OpenDataFolder(data, full, make && own); });
            };
            add("Keys", "keys", true);
            add("Firmware", @"nand\system\Contents\registered", true);
            add("Saves", @"nand\user\save", false);
            add("Mods", "load", true);
            add("Screenshots", "screenshots", false);
            add("Log files", "log", false);
        }
        if (c.Joined && Directory.Exists(UserData.SharedRoot)) { o.Add(null); o.Add("Shared data"); o.Add((Action)delegate { Explore(UserData.SharedRoot); }); }
        List<string> older = Core.Backups(d);
        if (older.Count > 0) { if (!(c.Joined && Directory.Exists(UserData.SharedRoot))) o.Add(null); o.Add("Older versions"); o.Add((Action)delegate { Explore(Path.GetDirectoryName(older[0].TrimEnd('\\'))); }); }
        return o.ToArray();
    }
    void OpenDataFolder(string data, string full, bool make)
    {
        if (!Directory.Exists(full) && make) try { Directory.CreateDirectory(full); } catch { }
        string p = full;
        while (!Directory.Exists(p) && p.Length > data.Length) p = Path.GetDirectoryName(p);   // not made yet: the nearest one there is
        Explore(Directory.Exists(p) ? p : data);
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
        int px = D(22);
        int pw = Math.Max(D(260), cw - 2 * px);   // the tiles' width (the scroll bar fits in the right margin)
        buildsPage.SetBounds(0, 0, cw, ch);

        // minimize / maximize / close in the top right corner (there is no title bar)
        int cbw = D(46), cbh = D(32);
        winClose.SetBounds(cw - cbw, 0, cbw, cbh); winMax.SetBounds(cw - 2 * cbw, 0, cbw, cbh); winMin.SetBounds(cw - 3 * cbw, 0, cbw, cbh);
        tips.SetToolTip(winMax, WindowState == FormWindowState.Maximized ? "Restore Down" : "Maximize");
        winMax.Invalidate();
        // header under them: logo, title and credit left; Save Helper, Add emulator fork and "..." right
        int hy = D(30);
        logoBox.SetBounds(px - D(2), hy - D(6), D(56), D(56));   // the console logo, as wide as it can be
        int right = px + pw;
        menuBtn.SetBounds(right - D(48), hy + D(3), D(48), D(38));
        addEmu.SetBounds(menuBtn.Left - D(10) - D(176), hy + D(3), D(176), D(38));
        saveBtn.Visible = pw >= D(620);   // narrow window: it's still in the "..." menu
        saveBtn.SetBounds(addEmu.Left - D(10) - D(124), hy + D(3), D(124), D(38));
        int hx = px + D(64);
        head.SetBounds(hx, hy - D(4), Math.Max(D(60), (saveBtn.Visible ? saveBtn.Left : addEmu.Left) - D(12) - hx), D(32));
        {
            credit.Font = Font; credit.Height = D(20);
            int crw; using (Graphics g = CreateGraphics()) crw = credit.PreferredWidth(g);
            credit.SetBounds(hx + D(2), hy + D(28), crw, D(20));
            int mw = TextRenderer.MeasureText(mgrLink.Text, Font).Width + D(8);
            mgrLink.SetBounds(right - mw, hy + D(44), mw, D(20));
        }
        int top = hy + D(70);
        headerBottom = top - D(4);

        // status line at the bottom
        int fy = ch - D(34);
        int lw = TextRenderer.MeasureText(logLink.Text, Font).Width + D(6), cwid = TextRenderer.MeasureText(checkLink.Text, Font).Width + D(6);
        logLink.SetBounds(right - lw, fy, lw, D(22));
        checkLink.SetBounds(right - lw - D(14) - cwid, fy, cwid, D(22));
        footer.SetBounds(px, fy, Math.Max(D(40), pw - lw - cwid - D(30)), D(22));

        cardsHost.SetBounds(0, top, cw, Math.Max(D(60), fy - D(10) - top));
        int gap = D(14);
        int cols = pw >= D(960) ? 3 : pw >= D(620) ? 2 : 1;
        int tw = (pw - gap * (cols - 1)) / cols;
        int cy = cardsHost.AutoScrollPosition.Y;
        Color[] tones = { Theme.Accent, Theme.Accent2, Theme.Accent3 };
        for (int i = 0; i < cards.Count; i++)
        {
            EmuCard c = cards[i];
            Color t = tones[i % tones.Length];
            if (c.Box.Glow != t) { c.Box.Glow = t; c.Badge.Tone = t; c.Box.Invalidate(true); }
        }
        // emulator tiles, a row at a time; a row's tiles share its height (buttons line up at the bottom)
        int lastY = cy, lastH = 0;
        for (int i = 0; i < cards.Count; i += cols)
        {
            List<EmuCard> row = cards.Skip(i).Take(cols).ToList();
            int rh = 0;
            for (int j = 0; j < row.Count; j++) rh = Math.Max(rh, LayoutCard(row[j], px + j * (tw + gap), cy, tw, 0));
            for (int j = 0; j < row.Count; j++) LayoutCard(row[j], px + j * (tw + gap), cy, tw, rh);
            lastY = cy; lastH = rh;
            cy += rh + gap;
        }
        // the last row not full (4, 5, 7... emulators in three columns): "+ Add emulator fork" takes the rest of it
        int inLast = cards.Count % cols;
        addTile.Visible = cards.Count > 0 && inLast != 0;
        if (addTile.Visible)
        {
            int free = cols - inLast;
            addTile.SetBounds(px + inLast * (tw + gap), lastY, free * tw + (free - 1) * gap, lastH);
            addTile.Invalidate();
        }
        emptyCard.Visible = cards.Count == 0;
        checkLink.Visible = cards.Count > 0;
        if (cards.Count == 0)
        {
            int P = D(18);
            emptyText.SetBounds(P, D(48), pw - 2 * P, D(24));
            emptyRec.SetBounds(P, D(84), D(176), D(38));
            int fw = TextRenderer.MeasureText(emptyFork.Text, Font).Width + D(6);
            emptyFork.SetBounds(P + D(176) + D(16), D(92), fw, D(22));
            emptyCard.SetBounds(px, cy, pw, D(140)); emptyCard.Invalidate();
            cy += D(140) + gap;
        }

        // the small tiles: saves, storage and updates once something is installed; the games folder and the manager always
        InfoText();
        if (games1Shown != ShowSwitch1Games()) GamesText();
        bool any = cards.Any(k => Installed(k.Def));
        savesTile.Visible = storeTile.Visible = updTile.Visible = any;
        List<KeyValuePair<Card, int>> tiles = new List<KeyValuePair<Card, int>>();
        Action<Card, int> add = (t, span) => tiles.Add(new KeyValuePair<Card, int>(t, Math.Min(span, cols)));
        if (any) { add(savesTile, 1); add(storeTile, 1); add(updTile, 1); }
        if (cols == 3) { add(gamesCard, 2); add(mgrTile, 1); }
        else if (cols == 2 && any) { add(mgrTile, 1); add(gamesCard, 2); }
        else { add(gamesCard, 1); add(mgrTile, 1); }
        int k0 = 0;
        while (k0 < tiles.Count)
        {
            int used = 0, k1 = k0, rh = 0;
            while (k1 < tiles.Count && used + tiles[k1].Value <= cols) { rh = Math.Max(rh, InfoHeight(tiles[k1].Key)); used += tiles[k1].Value; k1++; }
            int x = px;
            for (int j = k0; j < k1; j++)
            {
                int span = tiles[j].Value, w = span * tw + (span - 1) * gap;
                LayoutInfo(tiles[j].Key, x, cy, w, rh);
                x += w + gap;
            }
            cy += rh + gap;
            k0 = k1;
        }
        ResumeLayout();
        // the glow is drawn for the window size: everything again when that changed
        if (laidOut != ClientSize) { laidOut = ClientSize; buildsPage.Invalidate(true); }
        contentHeight = cy - cardsHost.AutoScrollPosition.Y - gap;
        // an emulator added or removed, or the first one installed (the small tiles appear): the window fits again
        string key = cards.Count + "|" + any + "|" + games1Shown;
        if (contentKey != null && key != contentKey && IsHandleCreated) BeginInvoke((MethodInvoker)delegate { FitHeight(); });
        contentKey = key;
    }

    // ---- emulator icons: the user's own picture (Change icon...), else the built-in emulators' logos, else initials
    static readonly Dictionary<string, Bitmap> builtIn = new Dictionary<string, Bitmap>();
    static Bitmap BuiltInPicture(string id)
    {
        Bitmap b;
        if (id == null) return null;
        if (builtIn.TryGetValue(id, out b)) return b;
        try
        {
            using (Stream st = typeof(EmuForm).Assembly.GetManifestResourceStream("badge-" + id + ".png"))
                if (st != null) using (Bitmap t = new Bitmap(st)) b = new Bitmap(t);   // a copy: it outlives the stream
        }
        catch { b = null; }
        builtIn[id] = b;
        return b;
    }
    // Which built-in emulator this is: by id, or by repository (lists saved by older versions).
    static string BuiltInId(EmuDef d)
    {
        foreach (EmuDef r in Core.Recommended(Core.Home))
            if (r.Id == d.Id || Core.SameRepo(d.Host, d.Repo, r.Host, r.Repo)) return r.Id;
        return null;
    }
    static string IconFile(EmuDef d) { return Path.Combine(Core.DataDir, "icons", SafeName(d.Id ?? d.Name ?? "emulator") + ".png"); }
    static Image PictureOf(EmuDef d)
    {
        string f = IconFile(d);
        if (File.Exists(f))
            try { using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(f))) using (Image im = Image.FromStream(ms)) return new Bitmap(im); }
            catch (Exception ex) { FileLog("Icon " + f + " can't be read: " + ex.Message); }
        return BuiltInPicture(BuiltInId(d));
    }
    void SetBadge(EmuCard c)
    {
        Image old = c.Badge.Picture;
        c.Badge.Picture = PictureOf(c.Def);
        InfoText();   // the saves tile shows the badges too
        Bitmap ob = old as Bitmap;
        if (old != null && old != c.Badge.Picture && !(ob != null && builtIn.ContainsValue(ob))) old.Dispose();
    }

    void ChangeIcon(EmuCard c)
    {
        string file;
        using (OpenFileDialog o = new OpenFileDialog())
        {
            o.Title = "Choose a picture for " + c.Def.Name;
            o.Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|All files|*.*";
            if (o.ShowDialog(this) != DialogResult.OK) return;
            file = o.FileName;
        }
        try
        {
            if (new FileInfo(file).Length > 30L << 20) throw new Exception("over 30 MB");
            Bitmap pic;
            if (file.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) using (Icon ic = new Icon(file, 256, 256)) pic = ic.ToBitmap();
            else using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(file))) using (Image im = Image.FromStream(ms)) pic = new Bitmap(im);
            using (pic)
            using (Bitmap fitted = Smooth(pic, 256))
            {
                string to = IconFile(c.Def);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                fitted.Save(to, ImageFormat.Png);   // a copy: the picture the user chose stays where it is
            }
            SetBadge(c);
            Log(c.Def.Name + ": new icon from " + file + ".");
        }
        catch (Exception ex)
        {
            Log(c.Def.Name + ": that picture couldn't be used (" + ex.Message + ").");
            Tell("Change icon", "That picture couldn't be used. Try a PNG or JPG file.");
        }
    }

    void ResetIcon(EmuCard c)
    {
        try { File.Delete(IconFile(c.Def)); }   // the manager's own copy; the user's picture is untouched
        catch (Exception ex) { Log("Could not reset the icon: " + ex.Message); return; }
        SetBadge(c);
        Log(c.Def.Name + ": icon reset.");
    }

    // The window's icon, from the copy of app.ico inside the app. The taskbar shows the window's big icon stretched to
    // 24 px x the screen scale, so that size is loaded: at 100 % the hand-drawn 24 px picture (the title bar still gets
    // the 16 px one, Windows makes it from the same icon). The exe's own icon if the copy can't be read.
    internal static Icon AppIcon()
    {
        int n = 24;
        try
        {   // the real screen scale (this app isn't DPI-aware, so SystemInformation always says 100 %)
            object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "AppliedDPI", 96);
            if (v is int && (int)v > 96) n = 24 * (int)v / 96;
        }
        catch { }
        try { using (Stream s = typeof(EmuForm).Assembly.GetManifestResourceStream("app.ico")) if (s != null) return new Icon(s, n, n); } catch { }
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { return null; }
    }

    // A picture shrunk to size x size with smooth edges (a plain zoom looks jagged).
    internal static Bitmap Smooth(Image src, int size)
    {
        Bitmap b = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.SmoothingMode = SmoothingMode.AntiAlias;
            float r = Math.Min((float)size / src.Width, (float)size / src.Height);
            float w = src.Width * r, h = src.Height * r;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);   // no grey fringe at the edges
                g.DrawImage(src, new Rectangle((int)((size - w) / 2), (int)((size - h) / 2), (int)w, (int)h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
        }
        return b;
    }

    // The window as tall as its tiles need: no empty space under them, and no scrolling when the screen has room.
    // On opening, and when emulators are added or removed; the user's own resizing is left alone otherwise.
    int contentHeight; string contentKey;
    void OnScreen()
    {
        if (WindowState != FormWindowState.Normal) return;
        Rectangle wa = Screen.FromControl(this).WorkingArea;
        int w = Math.Min(Width, wa.Width), h = Math.Min(Height, wa.Height);
        Rectangle b = new Rectangle(Math.Max(wa.Left, Math.Min(Left, wa.Right - w)), Math.Max(wa.Top, Math.Min(Top, wa.Bottom - h)), w, h);
        if (b != Bounds) Bounds = b;
    }
    void FitHeight()
    {
        if (WindowState != FormWindowState.Normal || !IsHandleCreated || contentHeight <= 0) return;
        int need = cardsHost.Top + contentHeight + D(32) + D(34);   // tiles, a little room above the status line, the status line
        int diff = need - ClientSize.Height;
        if (diff > 0) diff = Math.Min(diff, Math.Max(0, Screen.FromControl(this).WorkingArea.Bottom - Bottom));   // grow down, staying on the screen
        if (Math.Abs(diff) < D(4)) return;
        int h = Math.Max(MinimumSize.Height, Height + diff);
        if (h != Height) Height = h;
    }

    // Lays out once more after the current event (a status that no longer fits its place, and the like).
    void LayoutSoon()
    {
        if (layoutQueued || !IsHandleCreated) return;
        layoutQueued = true;
        BeginInvoke((MethodInvoker)delegate { layoutQueued = false; DoLayout(); });
    }

    // The height a text needs at width w, up to maxLines lines.
    static int TextHeight(string t, Font f, int w, int maxLines)
    {
        int line = TextRenderer.MeasureText("Ag", f).Height;
        int h = TextRenderer.MeasureText(t ?? "", f, new Size(Math.Max(10, w), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        return Math.Max(1, Math.Min(maxLines, (h + line / 2) / line)) * line;
    }

    // One emulator tile at (x, y), width w. h > 0: that height (the row's), with the buttons at the bottom.
    // h == 0: only measures (nothing at the bottom moves, the tile keeps its size). Returns the height the tile needs.
    int LayoutCard(EmuCard c, int x, int y, int w, int h)
    {
        int P = D(18), iw = w - 2 * P, chipH = D(26), bh = D(40), L = P - D(3);   // L: labels (their text starts a little in)
        c.Badge.SetBounds(P, P, D(40), D(40));
        int need = ((StatusChip)c.State).Needed(chipH);
        c.ChipTop = !c.Busy && need <= iw - D(40) - D(12);   // a job's progress gets a line of its own, above its bar
        if (c.ChipTop) c.State.SetBounds(w - P - need, P + (D(40) - chipH) / 2, need, chipH);
        int ly = P + D(40) + D(10);
        c.Title.SetBounds(P - D(4), ly, iw + D(4), D(30)); ly += D(32);
        if (!c.ChipTop)
        {
            int hh = chipH;
            if (need > iw)
            {   // too long for one line: the chip grows downward (up to three lines)
                int textW = iw - (need - TextRenderer.MeasureText(c.State.Text, c.State.Font, new Size(4000, chipH), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width);
                int line = TextRenderer.MeasureText("Ag", c.State.Font).Height;
                hh = Math.Max(chipH, TextHeight(c.State.Text, c.State.Font, Math.Max(D(40), textW), 3) + (chipH - line));
            }
            c.State.SetBounds(P, ly, Math.Min(need, iw), hh); ly += hh + D(8);
        }
        c.Note.Visible = c.Note.Text.Length > 0;
        if (c.Note.Visible) { int nh = TextHeight(c.Note.Text, c.Note.Font, iw, 2); c.Note.SetBounds(L, ly, iw + D(3), nh); ly += nh + D(6); }
        c.Ver.Visible = c.Ver.Text.Length > 0;
        if (c.Ver.Visible) { c.Ver.SetBounds(L, ly, iw + D(3), D(20)); ly += D(22); }
        c.Data.Visible = c.Data.Text.Length > 0;
        if (c.Data.Visible) { int dh = TextHeight(c.Data.Text, c.Data.Font, iw, 2); c.Data.SetBounds(L, ly, iw + D(3), dh); ly += dh + D(2); }
        c.Next.Visible = c.NextKind != null && !c.Busy && !IsBusy;
        if (c.Next.Visible) { ly += D(2); c.Next.SetBounds(L, ly, Math.Min(iw, TextRenderer.MeasureText(c.Next.Text, Font).Width + D(8)), D(22)); ly += D(24); }
        if (c.Bar.Visible) { ly += D(6); c.Bar.SetBounds(P, ly, iw, D(8)); ly += D(10); }
        c.Share.Visible = Installed(c.Def) && UserData.IsPortable(c.Def) && (UserData.CanJoin(c.Def) || Core.Cfg.ShareHost == c.Def.Id);
        int low = bh + (c.Share.Visible ? D(30) + D(10) : 0) + (c.Existing.Visible ? D(22) + D(10) : 0);
        int natural = ly + D(14) + low + P;
        // measuring only: placing the buttons at this height and then at the row's would make them jump on every
        // layout (a download's progress lays out again many times a second)
        if (h <= 0) return natural;
        h = Math.Max(h, natural);

        // bottom: [Launch        ][Update][...]   or   [Install          ][...]
        int by = h - P - bh;
        c.More.SetBounds(w - P - D(46), by, D(46), bh);
        int r = c.More.Left - D(8);
        if (c.Launch.Visible)
        {
            if (c.Update.Visible)
            {
                int uw = Math.Max(D(96), TextRenderer.MeasureText(c.Update.Text, c.Update.Font).Width + D(30));
                c.Update.SetBounds(r - uw, by, uw, bh); r -= uw + D(8);
            }
            c.Launch.SetBounds(P, by, Math.Max(D(60), r - P), bh);
        }
        else if (c.Update.Visible) c.Update.SetBounds(P, by, Math.Max(D(60), r - P), bh);
        int sy = by - D(10);
        if (c.Existing.Visible)
        {
            int ew = TextRenderer.MeasureText(c.Existing.Text, Font).Width + D(6);
            sy -= D(22); c.Existing.SetBounds(L, sy, Math.Min(iw, ew), D(22)); sy -= D(10);
        }
        if (c.Share.Visible) { sy -= D(30); c.Share.SetBounds(P, sy, iw, D(30)); }
        c.Box.SetBounds(x, y, w, h);
        return natural;
    }

    // A status that changed length (download progress...): moved in place, or a new layout when it changes rows.
    void ChipMoved(EmuCard c)
    {
        int w = c.Box.Width, P = D(18), chipH = D(26);
        if (w <= 0) return;
        int need = ((StatusChip)c.State).Needed(chipH);
        bool top = !c.Busy && need <= w - 2 * P - D(40) - D(12);
        if (top != c.ChipTop || !top) { LayoutSoon(); return; }   // on its own line it may need more lines: lay out again
        c.State.SetBounds(w - P - need, c.State.Top, need, chipH);
        c.State.Invalidate();
    }

    int InfoHeight(Card t)
    {
        return t == savesTile ? D(178) : t == storeTile ? D(178) : t == updTile ? D(206) : t == gamesCard && games1Shown ? D(150) : D(104);
    }

    // The small tiles' insides.
    void LayoutInfo(Card t, int x, int y, int w, int h)
    {
        int P = D(18), iw = w - 2 * P;
        t.SetBounds(x, y, w, h);
        if (t == savesTile)
        {
            int lw = TextRenderer.MeasureText(savesLink.Text, Font).Width + D(6);
            savesCap.SetBounds(P, P, Math.Max(D(20), iw - lw - D(8)), D(20));
            savesLink.SetBounds(w - P - lw, P, lw, D(20));
            int bw = TextRenderer.MeasureText(savesBig.Text, Theme.Big).Width;
            savesBig.SetBounds(P - D(4), P + D(24), bw, D(52));
            int uw = TextRenderer.MeasureText(savesUnit.Text, Theme.Value).Width + D(6);
            savesUnit.SetBounds(savesBig.Right, P + D(46), uw, D(26));
            savesLine.SetBounds(P, P + D(82), iw, D(20));
            savesWho.SetBounds(P, P + D(110), iw, D(32));
        }
        else if (t == storeTile)
        {
            storeCap.SetBounds(P, P, iw, D(20));
            storeRing.SetBounds(P, P + D(30), iw, Math.Min(D(108), h - P - D(30) - D(10)));
        }
        else if (t == updTile)
        {
            updCap.SetBounds(P, P, iw, D(20));
            updText.SetBounds(P, P + D(26), iw, D(28));
            updWhen.SetBounds(P, P + D(56), iw, D(20));
            updBars.SetBounds(P, P + D(82), iw, D(62));   // bars and the day names under them
            updLine.SetBounds(P, P + D(150), iw, D(20));
        }
        else if (t == gamesCard)
        {
            gamesCap.SetBounds(P, P, iw, D(20));
            gamesPick.SetBounds(w - P - D(112), P + D(28), D(112), D(38));
            gamesLbl.SetBounds(P, P + D(32), Math.Max(D(40), iw - D(124)), D(30));
            games1Pick.SetBounds(w - P - D(112), P + D(74), D(112), D(38));
            games1Lbl.SetBounds(P, P + D(78), Math.Max(D(40), iw - D(124)), D(30));
        }
        else if (t == mgrTile)
        {
            mgrCap.SetBounds(P, P, iw, D(20));
            int cw = mgrGo.Visible ? D(112) : mgrChip.Visible ? mgrChip.Needed(D(26)) : 0;
            mgrChip.SetBounds(w - P - cw, P + D(34), cw, D(26));
            mgrGo.SetBounds(w - P - D(112), P + D(28), D(112), D(38));
            mgrText.SetBounds(P, P + D(32), Math.Max(D(40), iw - cw - D(10)), D(30));
        }
        t.Invalidate();
    }

    // ---- the small tiles' contents: saves, storage, update activity (read from disk on a worker thread)
    class InfoData
    {
        public int SharedGames, AllGames;                 // games with saves in the shared folder / in any emulator's saves
        public long Emus, Shared, Older, Aside;
        public bool HasSizes;                             // sizes were read (a quick read keeps the last ones)
        public int[] Days = new int[7];                   // new versions per day this week, Sunday first
        public string Key;                                // the installed emulators it was read for
        public DateTime Day;                              // the day it was read (Days is that day's week)
    }
    string InstalledKey() { return string.Join("|", cards.Where(k => Installed(k.Def)).Select(k => k.Def.Id).OrderBy(x => x)); }
    InfoData info;
    bool scanning, scanAgain, againSizes, rescanQueued;
    DateTime scannedAt = DateTime.MinValue;

    static long DirBytes(string dir)
    {
        long n = 0;
        try
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            Stack<DirectoryInfo> st = new Stack<DirectoryInfo>();
            st.Push(new DirectoryInfo(dir));
            while (st.Count > 0)
            {
                DirectoryInfo d = st.Pop();
                try
                {
                    foreach (FileInfo f in d.EnumerateFiles()) n += f.Length;
                    foreach (DirectoryInfo sub in d.EnumerateDirectories())
                        if ((sub.Attributes & FileAttributes.ReparsePoint) == 0) st.Push(sub);   // shared folders are counted once, as Shared data
                }
                catch { }
            }
        }
        catch { }
        return n;
    }

    // now: read again even if it was read a moment ago (else at most every 20 seconds); sizes: walk the folders too
    // (slow), else only the save counts and the update dates. Each part that fails keeps what it showed before.
    void ScanInfo(bool now = false, bool sizes = true)
    {
        if (scanning) { if (now) { scanAgain = true; againSizes |= sizes; } return; }
        if (!now && DateTime.Now - scannedAt < TimeSpan.FromSeconds(20)) return;
        scanning = true;
        List<EmuDef> defs = cards.Select(k => k.Def).ToList();
        string key = InstalledKey();
        InfoData old = info;
        bool walk = sizes || old == null || !old.HasSizes;
        ThreadPool.QueueUserWorkItem(delegate
        {
            InfoData r = new InfoData(); r.Key = key; r.Day = DateTime.Today;
            List<EmuDef> inst = new List<EmuDef>();
            try { inst = defs.Where(Installed).ToList(); } catch { }
            try
            {
                r.SharedGames = UserData.CountGames(Path.Combine(UserData.SharedRoot, @"nand\user\save"));
                HashSet<string> games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (EmuDef d in inst)
                {
                    UserData.SaveKind kind; string saves = UserData.SavesOf(d, out kind);
                    if (saves != null) games.UnionWith(UserData.GamesIn(saves));
                }
                r.AllGames = games.Count;
            }
            catch (Exception ex) { FileLog("Counting saves failed: " + ex.Message); if (old != null) { r.SharedGames = old.SharedGames; r.AllGames = old.AllGames; } }
            if (walk)
                try
                {
                    foreach (EmuDef d in inst) r.Emus += DirBytes(d.Dir);
                    string aside = Path.Combine(UserData.SharedRoot, UserData.SET_ASIDE);
                    r.Aside = DirBytes(aside);
                    r.Shared = Math.Max(0, DirBytes(UserData.SharedRoot) - r.Aside);
                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (EmuDef d in defs)
                        foreach (string b in Core.Backups(d))
                            if (seen.Add(Path.GetFullPath(b).TrimEnd('\\'))) r.Older += DirBytes(b);
                    r.HasSizes = true;
                }
                catch (Exception ex) { FileLog("Reading sizes failed: " + ex.Message); walk = false; }
            if (!walk && old != null) { r.Emus = old.Emus; r.Shared = old.Shared; r.Older = old.Older; r.Aside = old.Aside; r.HasSizes = old.HasSizes; }
            try
            {
                // this week, Sunday to Saturday: Days[0] is Sunday
                DateTime today = DateTime.Today, sunday = today.AddDays(-(int)today.DayOfWeek);
                foreach (EmuDef d in inst)
                    foreach (DateTime t in Core.RecentReleaseDates(d))
                        if (t.Date >= sunday && t.Date <= today) r.Days[(int)t.Date.DayOfWeek]++;
            }
            catch (Exception ex) { FileLog("Reading update dates failed: " + ex.Message); if (old != null) r.Days = old.Days; }
            UI(delegate
            {
                scanning = false; scannedAt = DateTime.Now; info = r;
                DoLayout();
                if (scanAgain) { bool full = againSizes; scanAgain = againSizes = false; ScanInfo(true, full); }
            });
        });
    }

    static void Txt(Control c, string t) { if (c.Text != t) c.Text = t; }

    void InfoText()
    {
        List<EmuCard> inst = cards.Where(k => Installed(k.Def)).ToList();
        InfoData r = info;
        if (r != null && !scanning && !rescanQueued && r.Key != InstalledKey() && IsHandleCreated)
        {   // an emulator was added, removed or found: read again (once)
            rescanQueued = true;
            BeginInvoke((MethodInvoker)delegate { rescanQueued = false; ScanInfo(true); });
        }
        else if (r != null && !scanning && !rescanQueued && r.Day != DateTime.Today && IsHandleCreated)
        {   // past midnight: today's bar (and on Sunday a new week) needs the dates read again
            rescanQueued = true;
            BeginInvoke((MethodInvoker)delegate { rescanQueued = false; ScanInfo(true, false); });
        }
        DateTime thisSunday = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        int[] weekDays = r != null && r.Day.AddDays(-(int)r.Day.DayOfWeek) == thisSunday ? r.Days : new int[7];   // last week's: not shown
        // saves: who shares comes from the cards (Refresh keeps it current), the counts from the last scan
        EmuCard hostCard = Core.Cfg.ShareHost == null ? null : inst.FirstOrDefault(k => k.Def.Id == Core.Cfg.ShareHost && k.Joined);
        bool sharing = hostCard != null;
        List<EmuCard> users = sharing ? inst.Where(k => k.Joined).ToList() : inst;
        int games = r == null ? 0 : sharing ? r.SharedGames : r.AllGames;
        Txt(savesCap, sharing ? "Shared saves" : "Saves");
        Txt(savesBig, r == null ? "-" : games.ToString());
        Txt(savesUnit, games == 1 ? "game" : "games");
        string host = sharing ? hostCard.Def.Name : "";
        Txt(savesLine, sharing ? (users.Count >= 2 && users.Count == inst.Count ? "All " + users.Count + " emulators use " + host + "'s saves"
                                  : users.Count >= 2 ? users.Count + " emulators use " + host + "'s saves" : "Shared from " + host)
                     : inst.Count > 1 ? "Each emulator keeps its own saves" : inst.Count == 1 ? "Saved in " + inst[0].Def.Name : "");
        savesWho.People = users.Select(k => Tuple.Create(k.Badge.Text, k.Badge.Tone, k.Badge.Picture)).ToList();
        savesWho.Invalidate();
        // storage
        List<Tuple<string, long, Color>> parts = new List<Tuple<string, long, Color>>();
        if (r != null)
        {
            parts.Add(Tuple.Create("Emulators", r.Emus, Theme.Accent));
            if (r.Shared > 0) parts.Add(Tuple.Create("Shared data", r.Shared, Theme.Accent2));
            if (r.Older > 0) parts.Add(Tuple.Create("Older builds", r.Older, Theme.Accent3));
            if (r.Aside > 0) parts.Add(Tuple.Create("Backups", r.Aside, Theme.StrokeStrong));
        }
        storeRing.Parts = parts;
        storeRing.Center = r == null ? "..." : Ring.Bytes(parts.Sum(p => p.Item2));
        storeRing.Invalidate();
        // updates
        int upd = cards.Count(k => k.Rel != null && Installed(k.Def) && !Current(k));
        bool failed = cards.Any(k => k.CheckErr != null && Installed(k.Def));
        bool busyCheck = checking || checksRunning > 0, never = checkedAt == DateTime.MinValue;
        Txt(updText, busyCheck ? "Checking..." : upd == 1 ? "1 update available" : upd > 1 ? upd + " updates available"
                   : failed ? "Couldn't check" : never ? "Not checked yet" : "Everything is up to date");
        roles[updText] = busyCheck || (never && !failed && upd == 0) ? "muted" : upd > 0 ? "info" : failed ? "danger" : "success";
        updText.ForeColor = RoleColor(updText);
        TimeSpan ago = DateTime.Now - checkedAt;
        Txt(updWhen, checkedAt == DateTime.MinValue ? "" : ago.TotalMinutes < 1 ? "Checked just now" : ago.TotalMinutes < 2 ? "Checked 1 minute ago"
                   : ago.TotalMinutes < 60 ? "Checked " + (int)ago.TotalMinutes + " minutes ago" : "Checked at " + checkedAt.ToString("t"));
        updBars.Values = weekDays;
        updBars.Today = (int)DateTime.Today.DayOfWeek;
        updBars.Invalidate();
        int week = weekDays.Sum();
        Txt(updLine, r == null ? "" : week == 0 ? "No new versions this week" : week == 1 ? "1 new version this week" : week + " new versions this week");
        // manager
        // a newer manager: the tile lights up in the accent and gets a real Update button
        bool newer = mgrRel != null;
        Txt(mgrCap, newer ? "Manager update ready" : "Manager");
        roles[mgrCap] = newer ? "accent" : "muted"; mgrCap.ForeColor = newer ? Theme.Accent : RoleColor(mgrCap);
        Txt(mgrText, newer ? "Version " + mgrVer + " is out" : "Version " + Core.AppVersion + (Core.Portable ? " (portable)" : ""));
        tips.SetToolTip(mgrText, newer ? "You have " + Core.AppVersion + (Core.Portable ? " (portable)" : "") : null);
        bool goShown = newer && !anyBusy;
        if (mgrGo.Visible != goShown) { mgrGo.Visible = goShown; LayoutSoon(); }
        mgrGo.Enabled = !IsBusy;
        Outline(mgrGo, Theme.Accent);
        mgrChip.Visible = mgrChecked && !newer;
        Txt(mgrChip, "Latest");
        roles[mgrChip] = "success";
        mgrChip.ForeColor = RoleColor(mgrChip);
        Color edge = newer ? Theme.Accent : Color.Empty;
        if (mgrTile.Edge != edge || mgrTile.Glow != edge) { mgrTile.Edge = edge; mgrTile.Glow = edge; mgrTile.Invalidate(true); }
    }

    // Two letters for an emulator: "Eglantine Main" -> EM, "PokeDinner" -> PD, "Lichii" -> LI.
    static string Initials(string name)
    {
        string[] w = System.Text.RegularExpressions.Regex.Replace(name ?? "?", @"\([^)]*\)", " ").Split(new[] { ' ', '-', '_', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        if (w.Length == 0) return "?";
        if (w.Length > 1) return (w[0].Substring(0, 1) + w[1].Substring(0, 1)).ToUpper();
        string one = w[0];
        for (int i = 1; i < one.Length; i++) if (char.IsUpper(one[i])) return (one.Substring(0, 1) + one[i]).ToUpper();
        return (one.Length > 1 ? one.Substring(0, 2) : one).ToUpper();
    }

    // ---- a picture of the window laid over it, then faded out: the colours change smoothly
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    Form Snapshot(Form keepOnTop)
    {
        try
        {
            if (!Visible || WindowState == FormWindowState.Minimized || ClientSize.Width < 10 || ClientSize.Height < 10) return null;
            Rectangle wr = Bounds;
            Bitmap whole = new Bitmap(wr.Width, wr.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(whole))
            {
                IntPtr hdc = g.GetHdc();
                bool ok;
                try { ok = PrintWindow(Handle, hdc, 2); } finally { g.ReleaseHdc(hdc); }
                if (!ok) { whole.Dispose(); return null; }
            }
            Point c0 = PointToScreen(Point.Empty);
            Rectangle cr = new Rectangle(c0.X - wr.X, c0.Y - wr.Y, ClientSize.Width, ClientSize.Height);
            Bitmap pic = whole.Clone(cr, PixelFormat.Format32bppArgb);
            whole.Dispose();
            Opaque(pic, Native.Build >= 22000 && WindowState == FormWindowState.Normal ? (int)Math.Round(8 * Card.K) : 0);
            FadeForm f = new FadeForm();
            f.FormBorderStyle = FormBorderStyle.None; f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual;
            f.Bounds = new Rectangle(c0, ClientSize);
            f.BackColor = Theme.Window;
            f.SetPicture(pic);   // before it shows: its first frame is the picture
            pic.Dispose();
            IntPtr above = GetWindow(Handle, 3);   // GW_HWNDPREV: the window just above the main window, before the picture exists
            f.Show(this);
            if (above != IntPtr.Zero) Native.Below(f.Handle, above);   // right on the main window: its other windows stay on top
            else if (keepOnTop != null && !keepOnTop.IsDisposed) Native.Raise(keepOnTop.Handle);
            return f;
        }
        catch (Exception ex) { FileLog("No colour fade: " + ex.Message); return null; }
    }
    // The picture fully opaque (the window's own alpha can be anything), with its corners cut round like the window's
    // on Windows 11 (radius r, 0 = square).
    static void Opaque(Bitmap b, int r)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            byte[] row = new byte[d.Stride];
            for (int y = 0; y < b.Height; y++)
            {
                IntPtr at = d.Scan0 + y * d.Stride;
                Marshal.Copy(at, row, 0, d.Stride);
                for (int x = 0; x < b.Width; x++)
                {
                    bool cut = false;
                    if (r > 0)
                    {
                        int cx = x < r ? r : x >= b.Width - r ? b.Width - r - 1 : -1, cy = y < r ? r : y >= b.Height - r ? b.Height - r - 1 : -1;
                        if (cx >= 0 && cy >= 0) { double dx = x - cx, dy = y - cy; cut = dx * dx + dy * dy > r * r; }
                    }
                    int i = x * 4;
                    if (cut) { row[i] = row[i + 1] = row[i + 2] = row[i + 3] = 0; } else row[i + 3] = 255;
                }
                Marshal.Copy(row, 0, at, d.Stride);
            }
        }
        finally { b.UnlockBits(d); }
    }
    static void CrossFade(Form f)
    {
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 15;
        Stopwatch sw = new Stopwatch();
        t.Tick += delegate
        {
            if (!sw.IsRunning) { sw.Start(); return; }   // the first tick comes after the new look has painted
            double v = Math.Min(1, sw.ElapsedMilliseconds / 260.0);
            if (v >= 1 || f.IsDisposed) { t.Stop(); t.Dispose(); if (!f.IsDisposed) f.Close(); return; }
            FadeForm ff = f as FadeForm;
            if (ff != null) ff.Alpha((int)Math.Round(255 * (1 - v * v * (3 - 2 * v))));
        };
        t.Start();
    }

    // ---- Colors: the six schemes; a click applies one at once and the manager remembers it
    Form colorsForm;
    void ShowColors()
    {
        if (colorsForm != null && !colorsForm.IsDisposed) { colorsForm.Activate(); return; }
        QuietForm f = new QuietForm(); colorsForm = f;
        f.Text = "Themes"; f.Font = Font; f.Icon = Icon; f.ShowInTaskbar = false;
        f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false; f.MinimizeBox = false;
        f.StartPosition = FormStartPosition.CenterParent;
        const int cols = 5;   // a row per colour family
        int sw = D(150), sh = D(104), g = D(12), W = D(24) * 2 + sw * cols + g * (cols - 1);
        // the swatches scroll when they don't all fit on the screen (a small screen or a big text size)
        Panel grid = new Panel(); grid.AutoScroll = true;
        int rowsN = (Theme.Schemes.Length + cols - 1) / cols, gridH = rowsN * (sh + g) - g + D(4);
        int room = Screen.FromControl(this).WorkingArea.Height - D(54) - D(38) - D(20) - D(30) - SystemInformation.CaptionHeight - D(40);
        if (Demo != null && Environment.GetEnvironmentVariable("NS2_DEMO_SMALLSCREEN") == "1") room = D(330);   // test: a small screen
        bool scroll = gridH > room;
        int gh = scroll ? Math.Max(sh + g, room) : gridH;
        grid.SetBounds(D(24), D(54), sw * cols + g * (cols - 1) + (scroll ? SystemInformation.VerticalScrollBarWidth + D(6) : 0), gh);
        if (scroll) W += SystemInformation.VerticalScrollBarWidth + D(6);
        f.Controls.Add(grid);
        ToolTip ftips = new ToolTip(); f.Disposed += delegate { ftips.Dispose(); };
        Label t = new Label(); t.Text = "Pick the colors you like."; t.SetBounds(D(24), D(18), W - D(48), D(24)); f.Controls.Add(t);
        List<Swatch> all = new List<Swatch>();
        Button done = new Button(); done.Text = "Done";
        Action paint = () =>
        {
            f.BackColor = Theme.Window; t.ForeColor = Theme.Muted; t.BackColor = Theme.Window; grid.BackColor = Theme.Window;
            done.BackColor = Theme.Window; done.ForeColor = Theme.Text;
            foreach (Swatch x in all) x.Invalidate();
            done.Invalidate();
            if (f.IsHandleCreated) Native.Apply(f.Handle, true, 0);
        };
        Action<Swatch> choose = x =>
        {
            if (Theme.Scheme == x.Scheme[0]) return;
            Core.Cfg.Colors = x.Scheme[0];
            try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save the colors: " + ex.Message); }
            Form fade = Snapshot(f);
            ApplyStyle(); DoLayout(); paint();
            if (fade != null) CrossFade(fade);
            if (logForm != null && !logForm.IsDisposed) { logForm.BackColor = Theme.Window; logForm.Invalidate(true); }
        };
        for (int i = 0; i < Theme.Schemes.Length; i++)
        {
            Swatch x = new Swatch(); x.Scheme = Theme.Schemes[i];
            x.SetBounds((i % cols) * (sw + g), D(2) + (i / cols) * (sh + g), sw, sh);
            ftips.SetToolTip(x, x.Scheme[1]);
            x.Click += delegate { choose(x); };
            all.Add(x); grid.Controls.Add(x);
        }
        if (Demo != null && Demo.StartsWith("colors:"))
        {   // test: pick a scheme by itself a moment after opening (the fade from one look to the other)
            System.Windows.Forms.Timer dt = new System.Windows.Forms.Timer(); dt.Interval = 1500;
            dt.Tick += delegate { dt.Stop(); Swatch pick = all.FirstOrDefault(k => k.Scheme[0] == Demo.Substring(7)); if (pick != null) choose(pick); };
            dt.Start();
        }
        Swatch cur = all.FirstOrDefault(k => k.Scheme[0] == Theme.Scheme);
        if (cur != null) { f.ActiveControl = cur; f.Shown += delegate { grid.ScrollControlIntoView(cur); }; }   // the keyboard starts on the colours in use
        int by = grid.Bottom + D(18);
        done.SetBounds(W - D(24) - D(110), by, D(110), D(38));
        StyleButton(done); Outline(done, Theme.Accent);
        done.Click += delegate { f.Close(); };
        f.Controls.Add(done);
        f.AcceptButton = done; f.CancelButton = done;
        f.ClientSize = new Size(W, by + D(38) + D(20));
        f.HandleCreated += delegate { Native.Apply(f.Handle, true, 0); Native.ControlTheme(grid.Handle, "DarkMode_Explorer"); };
        paint();
        if (Demo == null) { f.ShowDialog(this); f.Dispose(); colorsForm = null; return; }
        f.StartPosition = FormStartPosition.Manual;   // a screenshot: over the main window, wherever that is
        f.Location = new Point(Left + (Width - f.Width) / 2, Top + (Height - f.Height) / 2);
        f.Show(this);
    }

    EmuCard MakeCard(EmuDef d)
    {
        EmuCard c = new EmuCard(); c.Def = d;
        c.Box.Text = ""; c.Box.Shift = 0; c.Box.Radius = D(20); c.Box.Hover = true;
        c.Title.Text = d.Name; c.Title.AutoEllipsis = true; fontRoles[c.Title] = "name";
        c.Title.UseMnemonic = c.Data.UseMnemonic = c.State.UseMnemonic = c.Note.UseMnemonic = c.Ver.UseMnemonic = false;   // "R&D" shows as typed
        c.Badge.Text = Initials(d.Name);
        c.Badge.Picture = PictureOf(d);
        c.Note.AutoEllipsis = true; roles[c.Note] = "muted";
        fontRoles[c.State] = "small";
        c.State.TextChanged += delegate { ChipMoved(c); };
        c.Data.AutoEllipsis = true; roles[c.Data] = "muted";
        c.Ver.AutoEllipsis = true; roles[c.Ver] = "muted";
        c.Bar.Visible = false;
        c.Update.Text = "Install"; c.Launch.Text = "Launch"; c.More.Text = "···";
        c.Launch.TabStop = false;   // keyboard focus never rests on Launch
        c.Launch.MouseDown += delegate { launchDown = DateTime.Now; };
        Outline(c.Launch, Theme.Accent);
        tips.SetToolTip(c.More, "More");
        c.Existing.Text = "I already have it";
        c.Share.Text = "Share data with other emulators"; c.Share.AutoCheck = false;   // its state comes from Refresh
        c.Share.Click += delegate { ToggleShare(c); };
        tips.SetToolTip(c.Share, "Your other emulators use this one's keys, firmware and saves, so progress shows up in all of them. Their own data is kept as a backup.");
        StyleCheck(c.Share); toggles.Add(c.Share);
        c.Box.Controls.AddRange(new Control[] { c.Badge, c.Title, c.Note, c.More, c.State, c.Data, c.Next, c.Bar, c.Launch, c.Update, c.Existing, c.Ver, c.Share });
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
        bool current = installed && m != null && c.Rel != null && m.Tag == c.Rel.Tag;
        bool update = installed && c.Rel != null && !current;
        c.Ver.Text = installed ? "Version " + (m != null && !string.IsNullOrEmpty(m.Tag) ? m.Tag : "unknown") + (update && !string.IsNullOrEmpty(c.Rel.Tag) ? "  →  " + c.Rel.Tag : "") : "";
        c.Title.Text = d.Name; c.Badge.Text = Initials(d.Name);
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
        tips.SetToolTip(c.State, (c.Err ?? c.CheckErr) != null ? (c.Err ?? c.CheckErr) + "\r\nClick to see details." : installed ? "Version: " + (m != null ? m.Tag : "unknown") : null);

        bool portable = installed && UserData.IsPortable(d);
        c.Joined = false;
        List<UserData.Item> shared = portable ? UserData.SharedItems(d) : new List<UserData.Item>();
        EmuDef hostDef = Core.Cfg.ShareHost == null ? null : Core.Cfg.Emulators.FirstOrDefault(x => x.Id == Core.Cfg.ShareHost);
        bool hostHere = hostDef != null && cards.Any(k => k.Def == hostDef && Installed(k.Def) && UserData.IsPortable(k.Def));
        if (hostDef != null && installed && !portable) c.Data.Text = "Not sharing: it keeps its saves outside its folder.";
        else if (hostDef != null && portable)
        {
            bool all = UserData.HostItems.All(i => UserData.StateOf(d, i) == UserData.State.On);
            c.Joined = hostDef == d || all;
            if (hostDef == d) c.Data.Text = "Your other emulators use this data.";
            else if (all) c.Data.Text = "Uses " + hostDef.Name + "'s data.";
            else if (!UserData.CanJoin(d)) c.Data.Text = "Not sharing: Switch 1 emulators keep their own data.";
            else
            {   // not joined yet: say what it waits for
                string why; joinWhy.TryGetValue(d.Id, out why);
                List<string> open = cards.Where(k => Installed(k.Def) && Core.Running(k.Def).Count > 0 &&
                                                     (k == c || UserData.HostItems.Any(i => UserData.StateOf(k.Def, i) != UserData.State.Off))).Select(k => k.Def.Name).ToList();
                c.Data.Text = UserData.OddLinks(d) ? "Not sharing: it uses a folder link the manager didn't make."
                            : open.Count > 0 ? "Not sharing yet: close " + string.Join(" and ", open) + " first."
                            : why != null ? "Not sharing yet: " + why + "."
                            : "Not sharing yet.";
            }
        }
        else c.Data.Text = shared.Count > 0 ? "Shared with your other emulators: " + string.Join(", ", shared.Select(i => i.Label.ToLower())) : "";
        c.Share.Checked = hostDef == d;
        c.Share.Enabled = !c.Busy && !IsBusy && (hostDef == null || hostDef == d || !hostHere) && UserData.CanShare;   // one at a time: the others are greyed out
        if (!UserData.CanShare && portable) { c.Share.Checked = false; c.Data.Text = "Not sharing: this drive can't share saves (most USB sticks can't)."; }
        tips.SetToolTip(c.Share, UserData.CanShare ? "Your other emulators use this one's keys, firmware and saves, so progress shows up in all of them. Their own data is kept as a backup." : UserData.NO_SHARE);
        c.NextKind = !installed || c.Busy ? null : !portable ? "portable" : NoReminder(d) ? null : !HasKeys(d) ? "keys" : !HasFirmware(d) ? "firmware" : null;
        c.Next.Text = c.NextKind == "portable" ? "Keep its saves in its own folder" : c.NextKind == "keys" ? "Next: add your keys" : c.NextKind == "firmware" ? "Next: add firmware" : "";

        c.Launch.Visible = installed;
        c.Launch.Enabled = !c.Busy && !IsBusy;
        // installed: always next to Launch; greyed "Up to date" until a newer version is out, then "Update"
        c.Update.Visible = true;
        c.Update.Text = !installed ? "Install" : current ? "Up to date" : "Update";
        c.Update.Enabled = !IsBusy && !c.Busy && c.Rel != null && (!installed || update);
        if (installed) accents.Remove(c.Update); else Outline(c.Update, Theme.Accent);   // one accent: Launch when installed
        c.Update.Invalidate();
        c.Existing.Visible = !installed && !c.Busy;
        c.Existing.Text = other.Count > 0 ? "Use it" : "I already have it";
        c.Existing.Enabled = !IsBusy;
        c.More.Enabled = !c.Busy;
        LayoutSoon();   // once, after all the cards changed
    }

    void RefreshAll() { foreach (EmuCard c in cards) Refresh(c); if (IsHandleCreated) BeginInvoke((MethodInvoker)InfoText); }

    void SetFooter(string text, Color color) { footer.Text = text; footer.ForeColor = color; }

    // What a failed release check tells the user: GitHub's hourly limit gets its own plain message.
    static string CheckFailed(string err, string otherwise = "Couldn't check for updates.")
    {
        return err != null && (err.Contains("hourly limit") || err.Contains("rate limit")) ? "GitHub is busy. Try again in a few minutes." : otherwise;
    }

    // ---- actions (one at a time, on a worker thread)
    // Is a newer manager on GitHub? Shows "Update the manager to X" under the header buttons.
    void CheckManager(bool fresh)
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            Release r = null; Version v = null; string err = null;
            try { r = Core.ManagerUpdate(fresh, out v); } catch (Exception ex) { err = ex.Message; }
            string fake = Core.Cfg.Sandbox ? Environment.GetEnvironmentVariable("NS2_DEMO_MGRUPDATE") : null;
            if (!string.IsNullOrEmpty(fake) && Version.TryParse(fake, out v)) { r = new Release { Tag = "v" + fake, Name = fake }; err = null; }
            UI(delegate
            {
                if (err != null) { FileLog("Manager update check failed: " + err); return; }
                mgrRel = r; mgrVer = v; mgrChecked = true;
                mgrLink.Text = r != null ? "Update the manager to " + v : "";
                mgrLink.Visible = r != null && !anyBusy;
                if (r != null) Log("A newer manager is out: " + v + " (this one is " + Core.AppVersion + ").");
                InfoText(); DoLayout();
            });
        });
    }

    // Downloads the newer manager, then opens its Setup on this install and closes, so Setup can replace it.
    void UpdateManager()
    {
        if (IsBusy) { PleaseWait(); return; }
        Release r = mgrRel; Version v = mgrVer;
        if (r == null) return;
        if (Ask("Update the manager", "Download version " + v + " of the manager and open its setup? The manager closes so it can be updated. Your emulators and saves are kept.", "Update", "Cancel") != 0) return;
        anyBusy = true; mgrLink.Visible = false; RefreshAll();
        SetFooter("Downloading the manager update...", Theme.Info);
        Log("Updating the manager to " + v + ".");
        ThreadPool.QueueUserWorkItem(delegate
        {
            string setup = null, err = null;
            try
            {
                setup = Core.DownloadManagerUpdate(r, s => UI(delegate { Log(s); }),
                    (got, total) => UI(delegate
                    {
                        if (got == Core.SLOW) SetFooter(total == 1 ? "GitHub is slow, trying again..." : "The download server is slow, trying again...", Theme.Info);
                        else if (total > 0) SetFooter("Downloading the manager update... " + (got * 100 / total) + "%", Theme.Info);
                    }));
            }
            catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                anyBusy = false; RefreshAll();
                if (err != null)
                {
                    Log("Manager update failed: " + err);
                    SetFooter(err.StartsWith("The download server isn't responding") ? "The download server isn't responding. Try again later." : "The manager update didn't download. Try again later.", Theme.Danger);
                    mgrLink.Visible = true;
                    return;
                }
                if (Core.Cfg.Sandbox) { SetFooter("Test copy: the update was downloaded, Setup was not started.", Theme.Muted); Explore(Path.GetDirectoryName(setup)); return; }
                try
                {
                    Process.Start(new ProcessStartInfo(setup, "--update \"" + (Core.Home.TrimEnd('\\').EndsWith(":") ? Core.Home.TrimEnd('\\') + "\\." : Core.Home.TrimEnd('\\')) + "\"") { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(setup) });
                    Log("Started the manager's setup; closing so it can update.");
                    Close();
                }
                catch (Exception ex) { Log("Could not start the setup: " + ex.Message); SetFooter("The setup didn't start. Try again.", Theme.Danger); }
            });
        });
    }
    // fresh: ask the hosts now (the user clicked Check for updates); otherwise lists from the last 10 minutes are used
    int checkGen;   // which round of CheckAll is the latest
    void CheckAll(bool fresh = false)
    {
        CheckManager(fresh);
        if (cards.Count == 0) { SetFooter("", Theme.Muted); return; }
        int gen = ++checkGen;   // a newer round makes this one's answers moot
        SetFooter("Checking for updates...", Theme.Muted);
        checking = true;
        foreach (EmuCard c in cards) { c.CheckErr = null; c.Rel = null; Refresh(c); }
        int left = cards.Count, answered = 0;
        foreach (EmuCard c0 in cards)
        {
            EmuCard c = c0;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Release r = null; string err = null;
                try { r = Core.Latest(c.Def, fresh); } catch (Exception ex) { err = ex.Message; }
                UI(delegate
                {
                    if (gen != checkGen) return;
                    c.Rel = r; c.CheckErr = err != null ? CheckFailed(err) : null; Refresh(c);
                    if (err != null) Log(c.Def.Name + ": update check failed: " + err); else answered++;
                    if (--left == 0)
                    {
                        checking = false; if (answered > 0) checkedAt = DateTime.Now; ScanInfo(true, false);
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
        // copy what this one is missing from the other emulator (only into this one; nothing else changes)
        if (Core.Running(d).Count > 0) { Fail(c, "Close " + d.Name + ", then try again."); return; }
        List<UserData.Item> fill = UserData.Items.Where(i => (i.Key == "keys" && !keys) || (i.Key == "firmware" && !fw)).ToList();
        EmuDef from = donor.Def;
        Work(c, "Copying keys and firmware", (log, prog) => { foreach (UserData.Item it in fill) UserData.CopyMissing(from, d, it, log); },
             delegate { RefreshAll(); if ((!keys && !HasKeys(d)) || (!fw && !HasFirmware(d))) Fail(c, "Nothing could be copied. Add them by hand."); });
    }

    // thenLaunch: asked by Launch in the portable edition, where an emulator must keep its saves in its own folder (else
    // they'd go to this PC's %APPDATA% and stay behind): it launches once that's done.
    void MakePortable(EmuCard c, bool thenLaunch = false)
    {
        if (IsBusy) { PleaseWait(); return; }
        EmuDef d = c.Def;
        string from = UserData.AppDataSource(d);
        string why = thenLaunch ? "The portable manager keeps " + d.Name + "'s saves in its own folder, so they go wherever this folder goes.\r\n\r\n" : "";
        if (from != null)
        {
            string q = why + "Copy " + d.Name + "'s saves and settings from this PC into its own folder? The originals stay where they are." +
                       (UserData.SourceIsShared(d, from) ? "\r\n\r\nThey are in the \"" + Path.GetFileName(from) + "\" folder, which other emulators may use too." : "");
            if (Ask("Keep saves in its own folder", q, "Copy", "Cancel") != 0) return;
        }
        else if (Ask("Keep saves in its own folder", why + "No saves or settings were found for " + d.Name + ". Give it an empty folder of its own? It will start like a new install.", "Continue", "Cancel") != 0) return;
        Work(c, "Copying saves and settings", (log, prog) => { log(UserData.MakePortable(d, from, log)); }, delegate { JoinSharing(c); if (thenLaunch) BeginInvoke((MethodInvoker)delegate { LaunchNow(c); }); });
    }

    // While sharing is on, an emulator that isn't linked yet (just added, or now keeping its own data) joins it without
    // questions: it uses the shared data, and its own goes to the backup folder (Shared data\Set aside).
    // c == null: check every emulator. If something is running, it waits and runs when the work is done.
    bool joinPending;
    readonly Dictionary<string, string> joinWhy = new Dictionary<string, string>();   // id -> why it didn't join (plain words); retried on Activated
    void JoinSharing(EmuCard c)
    {
        if (Core.Cfg.ShareHost == null || !UserData.CanShare) return;   // a drive without links (FAT32 / exFAT): sharing waits for one that has them
        List<EmuCard> need = cards.Where(k => (c == null || k == c) && Installed(k.Def) && UserData.IsPortable(k.Def) && UserData.CanJoin(k.Def) && !joinWhy.ContainsKey(k.Def.Id) &&
                                              UserData.HostItems.Any(i => UserData.StateOf(k.Def, i) != UserData.State.On)).ToList();
        if (need.Count == 0) return;
        if (IsBusy || installQueue.Count > 0 || addingFound > 0) { joinPending = true; return; }
        BeginInvoke((MethodInvoker)delegate
        {
            if (IsBusy || installQueue.Count > 0 || addingFound > 0) { joinPending = true; return; }
            // any open emulator that uses the shared data, or that would join, blocks it: try again later (Activated)
            if (cards.Any(k => Installed(k.Def) && Core.Running(k.Def).Count > 0 && (need.Contains(k) || UserData.HostItems.Any(i => UserData.StateOf(k.Def, i) != UserData.State.Off))))
            { joinPending = true; Log("Joining shared data waits until the emulators are closed."); RefreshAll(); return; }
            List<EmuCard> todo = new List<EmuCard>();
            foreach (EmuCard k in need)
            {   // a lasting problem (linked by hand, a drive that can't share...) is shown on the card, not retried in a loop
                string why = UserData.SharingProblem(k.Def);
                if (why == null) todo.Add(k); else if (!why.StartsWith("close")) joinWhy[k.Def.Id] = why;
            }
            if (todo.Count == 0) { RefreshAll(); return; }
            Dictionary<string, string> failed = new Dictionary<string, string>();
            Work(todo[0], "Sharing data", (log, prog) =>
            {
                foreach (EmuCard k in todo)
                    try { UserData.JoinOne(k.Def, log); }
                    catch (Exception ex) { failed[k.Def.Id] = UserData.PlainReason(ex); log(k.Def.Name + " didn't join the shared data yet: " + ex.Message); }
            }, delegate
            {
                foreach (var f in failed) joinWhy[f.Key] = f.Value;
                try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }   // what "Go back" needs
                RefreshAll();
            });
        });
    }

    // Settings from before the share tick: sharing that is fully on (keys, firmware and saves linked) gets a ticked card;
    // partial sharing from the old Shared data window stays as it is (nothing joins it automatically any more).
    void SharingFromOlderVersions()
    {
        try
        {
            bool changed = false;
            if (Core.Cfg.ShareHost != null && !Core.Cfg.Emulators.Any(x => x.Id == Core.Cfg.ShareHost)) { Core.Cfg.ShareHost = null; changed = true; }
            bool stopping = Core.Cfg.ShareBackups != null || Core.Cfg.ShareMerged != null;   // a stop that didn't finish: don't turn sharing back on
            if (Core.Cfg.ShareHost == null && !stopping)
            {
                EmuCard full = cards.FirstOrDefault(k => Installed(k.Def) && UserData.IsPortable(k.Def) && UserData.CanJoin(k.Def) && UserData.HostItems.All(i => UserData.StateOf(k.Def, i) == UserData.State.On));
                if (full != null) { Core.Cfg.ShareHost = full.Def.Id; Core.Cfg.ShareNew = UserData.HostItems.Select(i => i.Key).ToList(); changed = true; Log("Sharing was on: " + full.Def.Name + " is ticked."); }
                else if (Core.Cfg.ShareNew != null) { Core.Cfg.ShareNew = null; changed = true; }
            }
            if (changed) Core.SaveConfig();
        }
        catch (Exception ex) { Log("Could not read the sharing setup: " + ex.Message); }
    }

    // The card's "Share this emulator's data" tick.
    //  - Nothing shared yet: this emulator's keys, firmware and saves become the ones all of them use. Each other
    //    emulator's own data goes to the backup folder first. (An item this one doesn't have comes from the emulator that
    //    changed it last.)
    //  - Another one is ticked: this one is ticked instead; everyone keeps playing from the same, latest data.
    //  - Unticking: sharing stops; every emulator keeps its own copy of the latest data.
    void ToggleShare(EmuCard c)
    {
        if (IsBusy) { PleaseWait(); return; }
        if (!UserData.CanShare) { Tell("Share data", UserData.NO_SHARE); RefreshAll(); return; }
        EmuDef d = c.Def;
        if (!UserData.CanJoin(d) && Core.Cfg.ShareHost != d.Id) { Tell("Share data", UserData.SWITCH1_OWN); RefreshAll(); return; }
        List<EmuCard> inst = cards.Where(k => Installed(k.Def) && UserData.IsPortable(k.Def)).ToList();
        EmuDef host = Core.Cfg.ShareHost == null ? null : Core.Cfg.Emulators.FirstOrDefault(x => x.Id == Core.Cfg.ShareHost);
        List<string> open = inst.Where(k => Core.Running(k.Def).Count > 0).Select(k => k.Def.Name).ToList();
        if (host == d)
        {
            // the ticked one always keeps the latest; the others keep the latest too, or go back to their own saves
            List<EmuCard> others = inst.Where(k => k.Def != d && UserData.HostItems.Any(i => UserData.StateOf(k.Def, i) != UserData.State.Off)).ToList();
            List<EmuCard> can = others.Where(k => UserData.CanGoBack(k.Def)).ToList(), cannot = others.Where(k => !can.Contains(k)).ToList();
            bool canBack = can.Count > 0;
            string only = can.Count > 0 && cannot.Count > 0   // shared since an older version: not every one has its old saves recorded
                ? "\r\n\r\nOnly " + string.Join(" and ", can.Select(k => k.Def.Name)) + " can restore. " + string.Join(" and ", cannot.Select(k => k.Def.Name)) + (cannot.Count == 1 ? " keeps" : " keep") + " the latest saves."
                : "";
            int r = canBack
                ? Ask("Stop sharing", "Stop sharing data? " + d.Name + " keeps the latest saves.\r\n\r\nShould the other emulators keep the latest saves too, or restore the saves they had before sharing?" + only, "Keep latest", "Restore", "Cancel")
                : (Ask("Stop sharing", "Stop sharing data? Each emulator keeps its own copy of the current keys, firmware and saves.", "Stop sharing", "Cancel") == 0 ? 0 : -1);
            if (r != 0 && r != 1) return;
            bool goBack = r == 1;
            if (open.Count > 0) { Tell("Stop sharing", "Close " + string.Join(" and ", open) + " first."); return; }
            List<EmuDef> defs = inst.Select(k => k.Def).ToList();
            string sp = UserData.StopProblem(defs);
            if (sp != null) { Tell("Stop sharing", sp); return; }
            // sharing counts as off from now on, so nothing joins again if stopping fails partway (the Restore records
            // stay until it succeeds)
            Core.Cfg.ShareHost = null; Core.Cfg.ShareNew = null;
            try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }
            Work(c, "Stopping sharing", (log, prog) => UserData.StopSharing(d, defs, goBack, log), delegate
            {
                Core.Cfg.ShareHost = null; Core.Cfg.ShareNew = null;
                try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }
                Log("Sharing stopped; every emulator has its own copy.");
                RefreshAll();
            });
            return;
        }
        if (host != null && cards.Any(k => k.Def == host))
        {   // switching which one is ticked: the data everyone uses stays the same (the latest progress)
            Core.Cfg.ShareHost = d.Id;
            try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }
            Log(d.Name + " is now the one that shares (was " + host.Name + "); everyone keeps the same data.");
            RefreshAll();
            JoinSharing(null);
            return;
        }
        if (open.Count > 0) { Tell("Share data", "Close " + string.Join(" and ", open) + " first."); return; }
        if (UserData.OddLinks(d)) { Tell("Share data", d.Name + "'s data uses a folder link the manager didn't make, so it can't be shared."); return; }
        // data linked to another shared folder (an earlier install in another place) is copied in first; say so here
        List<EmuCard> foreign = cards.Where(k => Installed(k.Def) && UserData.IsPortable(k.Def) && UserData.ForeignLinks(k.Def).Count > 0).ToList();
        string copyNote = "";
        if (foreign.Count > 0)
        {
            string names = string.Join(" and ", foreign.Select(k => k.Def.Name)), root = UserData.ForeignRoot(foreign[0].Def);
            copyNote = "\r\n\r\n" + names + (foreign.Count == 1 ? " uses" : " use") + " data from another folder (" + root + "), probably from an earlier install. It's copied in first; that folder isn't changed.";
        }
        if (Ask("Share " + d.Name + "'s data", "All your emulators will use " + d.Name + "'s keys, firmware and saves, so progress shows up in all of them. Saves for other games are kept.\r\n\r\nIf two emulators have a save for the same game, " + d.Name + "'s is used and the other is kept as a backup." + copyNote, "Share", "Cancel") != 0) return;
        List<EmuDef> all = cards.Where(k => Installed(k.Def)).Select(k => k.Def).ToList();   // not-portable ones are named as left out
        if (!all.Contains(d)) all.Insert(0, d);
        List<string> left = new List<string>();
        Work(c, "Sharing data", (log, prog) => { left = UserData.StartSharing(d, all, log); }, delegate
        {
            Core.Cfg.ShareHost = d.Id; Core.Cfg.ShareNew = UserData.HostItems.Select(i => i.Key).ToList();
            try { Core.SaveConfig(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }
            Log("Sharing " + d.Name + "'s keys, firmware and saves with every emulator" + (left.Count > 0 ? ", except " + string.Join("; ", left) : "") + ".");
            RefreshAll();
            if (left.Count > 0)
            {
                joinPending = true;
                bool selfFix = left.All(x => x.Contains(": close ") || x.Contains("in use"));
                Tell("Share data", "Shared. These aren't sharing yet:\r\n" + string.Join("\r\n", left.Select(x => "- " + x)) + (selfFix ? "\r\n\r\nThey join by themselves when they can." : ""));
            }
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
            int r = Ask("Shared data", "Your emulators have different " + it.Label.ToLower() + ". Which should all of them use?\r\n\r\nThe others are kept in the Shared data folder (firmware goes to " + Bin + ").", opts.ToArray());
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
        if (Core.Cfg.ShareHost == c.Def.Id) { SharingFromOlderVersions(); RefreshAll(); }   // another linked one is ticked, or none
        DoLayout();
    }

    // ---- forks: add one from a repository link, or take one off the list
    // The link is looked up as soon as it's pasted (or typed and Enter pressed). Everything is on one page: the name,
    // which releases, the Windows file (by its name on the release page) and the folder.
    void AddFork()
    {
        if (IsBusy) { PleaseWait(); return; }
        Core.RepoInfo info = null;
        using (Form f = new Form())
        using (System.Windows.Forms.Timer soon = new System.Windows.Forms.Timer())
        {
            f.Text = "Add from link"; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            int W = D(600), pad = D(16), cw = W - 2 * pad, iw = cw - D(28);   // card width, width inside a card
            Label title = new Label(); title.Text = "Add from link"; title.Font = Theme.CardTitle; title.SetBounds(pad, D(14), cw, D(28)); f.Controls.Add(title);
            Label intro = new Label(); intro.Text = "Paste a GitHub or Gitea link to an emulator. It's found right away."; intro.ForeColor = Theme.Muted; intro.SetBounds(pad, D(46), cw, D(22)); f.Controls.Add(intro);
            Func<int, int, Card> card = (yy, hh) => { Card c = new Card(); c.Text = ""; c.Shift = 0; c.SetBounds(pad, yy, cw, hh); f.Controls.Add(c); return c; };
            Func<Control, string, int, Label> lab = (p, t, yy) => { Label l = new Label(); l.Text = t; l.SetBounds(D(14), yy, iw, D(20)); l.ForeColor = Theme.Muted; l.BackColor = Theme.Card; p.Controls.Add(l); return l; };
            Func<Control, int, int, TextBox> box = (p, yy, ww) => { TextBox t = new TextBox(); t.SetBounds(D(14), yy, ww, D(26)); t.BackColor = Theme.Input; t.ForeColor = Theme.Text; t.BorderStyle = BorderStyle.FixedSingle; p.Controls.Add(t); return t; };
            Func<Control, int, ComboBox> combo = (p, yy) => { ComboBox c = new ComboBox(); c.DropDownStyle = ComboBoxStyle.DropDownList; c.SetBounds(D(14), yy, iw, D(26)); c.BackColor = Theme.Input; c.ForeColor = Theme.Text; c.Enabled = false; DarkCombo(c); p.Controls.Add(c); return c; };

            int y = D(78);
            Card linkCard = card(y, D(94));
            lab(linkCard, "Link", D(10));
            TextBox url = box(linkCard, D(32), iw);
            const string HINT = "For example https://github.com/owner/emulator";
            Label status = new Label(); status.SetBounds(D(14), D(64), iw, D(22)); status.AutoEllipsis = true; status.BackColor = Theme.Card; status.ForeColor = Theme.Muted; status.Text = HINT; linkCard.Controls.Add(status);
            y += D(94) + D(10);
            Card nameCard = card(y, D(130));
            lab(nameCard, "Name", D(10)); TextBox name = box(nameCard, D(32), iw);
            lab(nameCard, "Best for (optional)", D(68)); TextBox note = box(nameCard, D(90), iw);
            y += D(130) + D(10);
            Card moreCard = card(y, D(194));
            lab(moreCard, "Which releases", D(10)); ComboBox chan = combo(moreCard, D(32));
            lab(moreCard, "Windows version", D(68)); ComboBox kind = combo(moreCard, D(90));
            lab(moreCard, "Install folder", D(126)); TextBox dir = box(moreCard, D(148), iw - D(110));
            Button browse = new Button(); browse.Text = "Browse..."; browse.SetBounds(D(14) + iw - D(102), D(146), D(102), D(30)); moreCard.Controls.Add(browse);
            y += D(194) + D(8);
            Label warn = new Label(); warn.SetBounds(pad, y, cw, D(36)); warn.ForeColor = Theme.Muted; f.Controls.Add(warn);
            y += D(40);
            Button ok = new Button(); ok.Text = "Install"; ok.Enabled = false; ok.SetBounds(W - D(250), y, D(118), D(36)); f.Controls.Add(ok);
            Button no = new Button(); no.Text = "Cancel"; no.DialogResult = DialogResult.Cancel; no.SetBounds(W - D(124), y, D(108), D(36)); f.Controls.Add(no);
            f.ClientSize = new Size(W, y + D(52));
            f.CancelButton = no;
            foreach (Button b in new[] { browse, ok, no }) { StyleButton(b); b.BackColor = b == browse ? Theme.Card : Theme.Window; }
            Outline(ok, Theme.Accent);
            f.HandleCreated += delegate
            {
                Native.Apply(f.Handle, Theme.Dark, 0);
                foreach (Control c in new Control[] { url, name, note, dir, chan, kind }) Native.ControlTheme(c.Handle, Theme.Dark ? (c is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer") : "Explorer");
            };

            string root = Core.Home;   // like the recommended builds: <install folder>\<name> (always inside a test environment)
            bool dirTouched = false, nameTouched = false;
            string autoNote = "";   // the note filled in for a recommended emulator: replaced when the link changes, a typed one never
            dir.TextChanged += delegate { if (dir.Focused) dirTouched = true; };
            name.TextChanged += delegate { if (name.Focused) nameTouched = true; };
            // a name no other emulator has ("Eglantine Main 2" for a second Eglantine)
            Func<string, string> freeName = n0 =>
            {
                string n = n0;
                for (int k = 2; Core.Cfg.Emulators.Any(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase)); k++) n = n0 + " " + k;
                return n;
            };
            // <install folder>\<name>, or "<name> 2"... when that folder belongs to another emulator or already has files
            Func<string, string> freeDir = n0 =>
            {
                string b0 = Path.Combine(root, SafeName(n0)), d = b0;
                for (int k = 2; k < 100; k++)
                {
                    string full = Path.GetFullPath(d).TrimEnd('\\');
                    if (Core.FolderProblem(new EmuDef(), full) == null && !(Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())) break;
                    d = b0 + " " + k;
                }
                return d;
            };
            EventHandler checkDir = delegate
            {
                string d0 = dir.Text.Trim(), why = null, n = Core.DisplayName(new EmuDef { Name = name.Text.Trim() });
                warn.Text = "";
                if (info != null && Core.Cfg.Emulators.Any(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase)))
                    why = "Another emulator is already called " + n + ". Choose another name.";
                else if (d0.Length == 0 || !Path.IsPathRooted(d0)) why = "Choose an install folder.";
                else
                {
                    string full = "";
                    try { full = Path.GetFullPath(d0).TrimEnd('\\'); } catch { why = "This folder can't be used. Choose another one."; }
                    if (why == null)
                    {
                        string bad = Core.FolderProblem(new EmuDef(), full);   // other builds, manager folders, test-environment bounds
                        if (bad == null && string.Equals(full, Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) bad = "the install folder itself";
                        if (bad != null) { why = "This folder can't be used. Choose another one."; tips.SetToolTip(warn, bad); }
                        else if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) warn.Text = "This folder isn't empty.";
                    }
                }
                if (why != null) warn.Text = why;
                warn.ForeColor = why != null ? Theme.Danger : Theme.Muted;
                ok.Enabled = info != null && why == null && name.Text.Trim().Length > 0 && kind.SelectedIndex >= 0;
            };
            dir.TextChanged += checkDir; name.TextChanged += checkDir;
            name.TextChanged += delegate { if (!dirTouched && info != null && name.Text.Trim().Length > 0) dir.Text = freeDir(Core.DisplayName(new EmuDef { Name = name.Text.Trim() })); };
            browse.Click += delegate
            {
                string picked = FolderPicker.Pick(f, "Choose an empty folder for this emulator", Directory.Exists(dir.Text) ? dir.Text : (Directory.Exists(root) ? root : Core.Home));
                if (picked != null) { dirTouched = true; dir.Text = picked; }
            };

            // looking the link up: each change starts over, and only the answer for the current text counts
            int gen = 0;
            Action find = () =>
            {
                string link = url.Text.Trim();
                if (link.Length == 0) return;
                int my = ++gen;
                status.ForeColor = Theme.Muted; status.Text = "Looking..."; tips.SetToolTip(status, null);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Core.RepoInfo ri = null; string err = null;
                    try { ri = Core.Lookup(link); } catch (Exception ex) { err = ex.Message; }
                    try
                    {
                        f.BeginInvoke((MethodInvoker)delegate
                        {
                            if (my != gen) return;   // the link changed meanwhile
                            if (err != null) { Log("Add from link: " + link + ": " + err); status.ForeColor = Theme.Danger; status.Text = "Nothing found at that link. Check it and try again."; tips.SetToolTip(status, err); return; }
                            EmuDef dup = Core.Cfg.Emulators.FirstOrDefault(x => Core.SameRepo(x.Host, x.Repo, ri.Host, ri.Repo));
                            EmuDef rec = Core.Recommended(Core.Home).FirstOrDefault(r => Core.SameRepo(r.Host, r.Repo, ri.Host, ri.Repo));
                            info = ri;
                            Log("Add from link: " + ri.Repo + ", " + ri.Channels.Sum(c => c.Count) + " Windows releases.");
                            status.ForeColor = dup != null ? Theme.Warn : Theme.Success;
                            status.Text = dup != null ? "Already on your list as " + dup.Name + ". Choose another Windows version below." : "Found " + ri.Name + ".";
                            chan.Items.Clear();
                            chan.Items.Add("All releases (newest: " + ri.Channels[0].Newest + ")");
                            foreach (Core.Channel c in ri.Channels)
                                if (c.Prefix.Length > 0)
                                    chan.Items.Add(c.Prefix + "*   " + c.Count + " release" + (c.Count == 1 ? "" : "s") + ", newest " + c.Newest + " (" + Core.ParseDate(c.NewestDate).ToLocalTime().ToString("yyyy-MM-dd") + ")");
                            List<Core.Channel> named = ri.Channels.Where(c => c.Prefix.Length > 0).ToList();
                            // the channel the one already on the list (or the recommended one) follows, else the busiest
                            string want = dup != null ? dup.TagPrefix ?? "" : rec != null ? rec.TagPrefix ?? "" : ri.DefaultPrefix;
                            if ((dup != null || rec != null) && want.Length > 0 && !named.Any(c => string.Equals(c.Prefix, want, StringComparison.OrdinalIgnoreCase))) want = ri.DefaultPrefix;
                            int sel = named.FindIndex(c => string.Equals(c.Prefix, want, StringComparison.OrdinalIgnoreCase));
                            chan.SelectedIndex = want.Length > 0 && sel >= 0 ? sel + 1 : 0;
                            chan.Tag = named;
                            // the Windows files of the newest release, named as on its release page; the PGO one first
                            // when there is one (faster), unless that one is already on the list
                            kind.Items.Clear();
                            foreach (string z in ri.Zips) kind.Items.Add(z);
                            int pick = ri.Zips.FindIndex(z => z.IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0);
                            if (dup != null)
                            {   // the file the one on the list uses (its installed build, else its newest release)
                                Manifest mf = string.IsNullOrEmpty(dup.Dir) ? null : Core.LoadManifest(dup.Dir);
                                EmuCard dc = cards.FirstOrDefault(k => k.Def == dup);
                                string have = mf != null && !string.IsNullOrEmpty(mf.Asset) ? mf.Asset : dc != null && dc.Rel != null ? dc.Rel.Asset : null;
                                // a second copy is usually the other kind (DLSS needs the non-PGO build): PGO <-> not
                                bool havePgo = have != null ? have.IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0
                                             : dup.Assets != null && dup.Assets.Count > 0 && (dup.Assets[0].IndexOf("(?=.*pgo)", StringComparison.OrdinalIgnoreCase) >= 0   // older versions' PGO pattern
                                                 || dup.Assets[0].IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0 && dup.Assets[0].IndexOf("(?!.*pgo)", StringComparison.OrdinalIgnoreCase) < 0 && dup.Assets[0].IndexOf("?!", StringComparison.Ordinal) < 0);
                                int other = ri.Zips.FindIndex(z => (z.IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0) != havePgo);
                                if (other >= 0) pick = other;
                            }
                            kind.SelectedIndex = pick >= 0 ? pick : 0;
                            Log("Add from link: Windows files in " + ri.ZipsFrom + ": " + string.Join(", ", ri.Zips));
                            chan.Enabled = kind.Enabled = true;
                            if (!nameTouched || name.Text.Trim().Length == 0)
                            {
                                name.Text = freeName(dup != null ? dup.Name : rec != null ? rec.Name : ri.Name);
                            }
                            string cur = note.Text.Trim();
                            if (cur.Length == 0 || cur == autoNote) { autoNote = rec != null && dup == null ? (rec.Note ?? "") : ""; note.Text = autoNote; }
                            if (!dirTouched) dir.Text = freeDir(Core.DisplayName(new EmuDef { Name = name.Text.Trim() }));
                            checkDir(null, EventArgs.Empty);
                            f.AcceptButton = ok;
                        });
                    }
                    catch { }   // dialog closed meanwhile
                });
            };
            soon.Interval = 500;
            soon.Tick += delegate { soon.Stop(); find(); };
            url.TextChanged += delegate
            {
                soon.Stop(); gen++;   // an answer for the old text no longer counts
                info = null; ok.Enabled = false; f.AcceptButton = null; chan.Enabled = kind.Enabled = false;
                string t = url.Text.Trim();
                // a link (host/owner/repo) is looked up once typing stops; anything else waits
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^(?:[a-z+]+://|git@)?[^\s/:]+\.[^\s/:]+(?::\d+)?[/:][^\s/]+/[^\s/#?]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { status.ForeColor = Theme.Muted; status.Text = "Looking..."; soon.Start(); }
                else { status.ForeColor = Theme.Muted; status.Text = t.Length == 0 ? HINT : "Paste the whole link, like " + HINT.Substring(12) + "."; tips.SetToolTip(status, null); }
                checkDir(null, EventArgs.Empty);
            };
            url.KeyDown += delegate (object s0, KeyEventArgs k) { if (k.KeyCode == Keys.Enter && info == null) { k.SuppressKeyPress = true; soon.Stop(); find(); } };
            kind.SelectedIndexChanged += checkDir;
            ok.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
            f.Shown += delegate
            {
                url.Focus();
                // a link already copied: fill it in, it's looked up at once
                try { string clip = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : ""; if (clip.Length < 300 && System.Text.RegularExpressions.Regex.IsMatch(clip, @"^(https?://)?(www\.)?github\.com/[^\s/]+/[^\s/]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { url.Text = clip; url.SelectAll(); } } catch { }
            };
            if (f.ShowDialog(this) != DialogResult.OK || info == null) return;

            List<Core.Channel> chans = (List<Core.Channel>)chan.Tag;
            string zip = kind.SelectedItem as string ?? "";
            EmuDef e = new EmuDef();
            e.Name = Core.DisplayName(new EmuDef { Name = name.Text.Trim(), Repo = info.Repo }); e.Note = note.Text.Trim();
            e.Host = info.Host; e.ApiBase = info.ApiBase; e.Repo = info.Repo;
            e.TagPrefix = chan.SelectedIndex > 0 ? chans[chan.SelectedIndex - 1].Prefix : "";
            // the chosen file by name (its version parts may change), then the same kind of file if a release names it differently
            bool pgo = zip.IndexOf("pgo", StringComparison.OrdinalIgnoreCase) >= 0;
            e.Assets = new List<string>();
            if (zip.Length > 0) e.Assets.Add(Core.AssetPattern(zip));
            e.Assets.AddRange(Core.AssetPatterns(pgo));
            e.Dir = Path.GetFullPath(dir.Text.Trim()).TrimEnd('\\');
            e.Exe = ""; e.Added = true;
            string id = SafeName(info.Repo.Replace('/', '-')).ToLowerInvariant().Replace(' ', '-');
            string baseId = id; int idn = 2;
            // a removed fork's previous builds / downloads stay under its id: never reuse one
            while (Core.IdTaken(id)) id = baseId + "-" + idn++;
            e.Id = id;
            Core.Cfg.Emulators.Add(e);
            try { Core.SaveConfig(); }
            catch (Exception ex) { Core.Cfg.Emulators.Remove(e); Log("Could not save the new fork: " + ex.Message); return; }
            Log("Added " + e.Name + " (" + info.WebUrl + ", channel " + (e.TagPrefix.Length > 0 ? e.TagPrefix + "*" : "all") + ", " + (zip.Length > 0 ? zip : "any Windows zip") + ") -> " + e.Dir);
            EmuCard card2 = MakeCard(e);
            cards.Add(card2);
            SortCards();
            ApplyStyle(); DoLayout();
            cardsHost.ScrollControlIntoView(card2.Box);
            CheckOne(card2, delegate { if (card2.Rel != null) StartUpdate(card2, null); });   // the user pressed Install (queued if busy)
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
            if (Core.Cfg.ShareHost == null) { SharingFromOlderVersions(); RefreshAll(); }   // it already shares (an earlier install): tick a card
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
        if (recs.Count == 0 && !first) { AddFork(); return; }   // all the recommended ones are on the list: straight to a link
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
            bool two = ShowSwitch1Games();
            Action showG = () =>
            {
                bool set = !string.IsNullOrEmpty(Core.Cfg.GamesFolder);
                gv.Text = set ? (two ? "Switch 2 games: " : "Games: ") + Core.Cfg.GamesFolder : two ? "Choose the folder with your Switch 2 games (optional)." : "Choose the folder with your games (optional).";
                gb.Text = set ? "Change..." : "Choose...";
            };
            showG();
            gv.UseMnemonic = false;
            gb.Click += delegate { PickGames(2); showG(); };
            foreach (Control k in gr.Controls) { k.BackColor = Theme.Card; k.ForeColor = Theme.Text; }
            y += D(58);
            Button gb1 = null;
            if (two)
            {   // a Switch 1 emulator is on the list: its games folder too
                Card gr1 = new Card(); gr1.Text = ""; gr1.Shift = 0; gr1.SetBounds(D(16), y, W - D(32), D(50)); f.Controls.Add(gr1);
                Label gv1 = new Label(); gv1.SetBounds(D(14), D(14), W - D(190), D(22)); gv1.AutoEllipsis = true; gv1.UseMnemonic = false; gr1.Controls.Add(gv1);
                gb1 = new Button(); gb1.SetBounds(W - D(152), D(8), D(104), D(34)); gr1.Controls.Add(gb1);
                Action showG1 = () =>
                {
                    bool set = !string.IsNullOrEmpty(Core.Cfg.Games1Folder);
                    gv1.Text = set ? "Switch 1 games: " + Core.Cfg.Games1Folder : "Choose the folder with your Switch 1 games (optional).";
                    gb1.Text = set ? "Change..." : "Choose...";
                };
                showG1();
                gb1.Click += delegate { PickGames(1); showG1(); };
                foreach (Control k in gr1.Controls) { k.BackColor = Theme.Card; k.ForeColor = Theme.Text; }
                y += D(58);
            }
            y += D(6);   // sharing is a tick on each emulator's card
            LinkLabel link = new LinkLabel(); link.Text = "Add from link..."; link.SetBounds(D(16), y + D(8), D(160), D(22));
            link.LinkColor = link.ActiveLinkColor = link.VisitedLinkColor = Theme.Accent; link.LinkBehavior = LinkBehavior.HoverUnderline; f.Controls.Add(link);
            link.LinkClicked += delegate { fromLink = true; f.DialogResult = DialogResult.Cancel; f.Close(); };
            Button go = goRef; go.Text = "Install"; go.SetBounds(W - D(250), y, D(118), D(36)); f.Controls.Add(go);
            Button later = new Button(); later.Text = first ? "Not now" : "Close"; later.SetBounds(W - D(124), y, D(108), D(36)); f.Controls.Add(later);
            go.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
            later.DialogResult = DialogResult.Cancel; f.CancelButton = later;
            foreach (Button b in new[] { go, later, gb, gb1 }) { if (b == null) continue; StyleButton(b); b.BackColor = b == gb || b == gb1 ? Theme.Card : Theme.Window; }
            Outline(go, Theme.Accent);
            f.ClientSize = new Size(W, y + D(52));
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
            DialogResult dr = f.ShowDialog(this);
            if (first) JoinSharing(null);   // emulators added with "I already have it" join sharing, if it's on
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
            SharingFromOlderVersions(); RefreshAll();   // found emulators that already share: one card shows the tick
            then();
            JoinSharing(null);   // waits for the downloads, if any
            dupRetry = true; BeginInvoke((MethodInvoker)AskDuplicates);   // one of them may also be in another folder
            return;
        }
        if (IsBusy) { BeginInvoke((MethodInvoker)delegate { WhenIdle(() => AddFound(list, i, then)); }); return; }
        EmuDef d = list[i];
        if (Core.Cfg.Emulators.Any(x => Core.SameRepo(x.Host, x.Repo, d.Host, d.Repo))) { AddFound(list, i + 1, then); return; }
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

    int checksRunning;   // single-card checks (CheckOne) still out
    void CheckOne(EmuCard c, MethodInvoker after)
    {
        c.CheckErr = null; c.Rel = null; checksRunning++; Refresh(c);
        ThreadPool.QueueUserWorkItem(delegate
        {
            Release r = null; string err = null;
            try { r = Core.Latest(c.Def); } catch (Exception ex) { err = ex.Message; }
            UI(delegate
            {
                checksRunning = Math.Max(0, checksRunning - 1);
                if (err == null) checkedAt = DateTime.Now;
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
            if (got == Core.SLOW) { c.State.Text = tot == 1 ? "GitHub is slow, trying again..." : "The download server is slow, trying again..."; return; }
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
                          : err.StartsWith("The download server isn't responding") ? "The download server isn't responding. Try again later."
                          : what == "Installing" ? "Install failed. Try again."
                          : what == "Updating" ? "Update failed. Try again."
                          : "That didn't work. Try again.";
                }
                RefreshAll();
                if (err == null && after != null) after();
                ScanInfo(true);   // after the follow-up: sharing settings it saves are counted too
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
            DarkCombo(list);
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

    // The name on the card (and in "Uses ...'s data", the log and the Save Helper). Only the name: its folder,
    // saves and icon stay as they are.
    void RenameEmu(EmuCard c)
    {
        string old = c.Def.Name ?? "";
        using (Form f = new Form())
        {
            f.Text = "Rename " + old; f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MinimizeBox = f.MaximizeBox = false; f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent; f.ClientSize = new Size(D(460), D(140)); f.Font = Font; f.BackColor = Theme.Window; f.ForeColor = Theme.Text;
            Label l = new Label(); l.Text = "Name:"; l.SetBounds(D(14), D(12), D(430), D(20));
            TextBox tb = new TextBox(); tb.Text = old; tb.MaxLength = 40; tb.SetBounds(D(14), D(36), D(432), D(24)); tb.BackColor = Theme.Input; tb.ForeColor = Theme.Text; tb.BorderStyle = BorderStyle.FixedSingle;
            Label msg = new Label(); msg.UseMnemonic = false; msg.Text = "Its folder and saves stay the same."; msg.ForeColor = Theme.Muted; msg.SetBounds(D(14), D(66), D(432), D(20));
            Button ok = new Button(); ok.Text = "Save"; ok.SetBounds(D(262), D(94), D(88), D(34));
            Button no = new Button(); no.Text = "Cancel"; no.SetBounds(D(358), D(94), D(88), D(34)); no.DialogResult = DialogResult.Cancel;
            string chosen = null;
            ok.Click += delegate
            {
                string n = Core.DisplayName(new EmuDef { Name = tb.Text, Repo = c.Def.Repo });
                if (tb.Text.Trim().Length == 0) { msg.Text = "Type a name."; msg.ForeColor = Theme.Danger; tb.Focus(); return; }
                EmuDef same = Core.Cfg.Emulators.FirstOrDefault(x => x != c.Def && string.Equals((x.Name ?? "").Trim(), n, StringComparison.OrdinalIgnoreCase));
                if (same != null) { msg.Text = "Another emulator is already called " + same.Name + "."; msg.ForeColor = Theme.Danger; tb.Focus(); tb.SelectAll(); return; }
                chosen = n; f.DialogResult = DialogResult.OK;
            };
            tb.TextChanged += delegate { msg.Text = "Its folder and saves stay the same."; msg.ForeColor = Theme.Muted; };
            f.AcceptButton = ok; f.CancelButton = no;
            f.Controls.AddRange(new Control[] { l, tb, msg, ok, no });
            StyleButton(ok); StyleButton(no); ok.BackColor = no.BackColor = Theme.Window; Outline(ok, Theme.Accent);
            f.HandleCreated += delegate { Native.Apply(f.Handle, Theme.Dark, 0); };
            f.Shown += delegate { tb.Focus(); tb.SelectAll(); };
            if (f.ShowDialog(this) != DialogResult.OK || chosen == null || chosen == old) return;
            try { Core.ClaimBackups(c.Def); } catch (Exception ex) { FileLog("Rename: older builds not marked: " + ex.Message); }
            c.Def.Name = chosen;
            try { Core.SaveConfig(); Log("Renamed " + old + " to " + chosen + "."); }
            catch (Exception ex) { c.Def.Name = old; Log("Could not save the new name: " + ex.Message); Fail(c, "Couldn't save the new name."); return; }
            foreach (EmuCard k in cards) if (k.Err != null && k.Err.Contains(old)) k.Err = null;   // old messages named it by its old name
            RefreshAll();   // other cards say "Uses <name>'s data"
        }
    }

    void Fail(EmuCard c, string msg, string logMsg = null)   // logMsg: a longer text for the log
    {
        Log(c.Def.Name + ": " + (logMsg ?? msg)); c.Err = msg; c.State.Text = msg; roles[c.State] = "danger"; c.State.ForeColor = RoleColor(c.State);
        tips.SetToolTip(c.State, msg + "\r\nClick to see details.");
    }

    // ---- log
    public static string LogPath { get { try { Directory.CreateDirectory(Core.DataDir); } catch { } return Path.Combine(Core.DataDir, "manager.log"); } }
    public static void FileLog(string s)
    {
        try
        {
            string f = LogPath;
            if (File.Exists(f) && new FileInfo(f).Length > (2 << 20))
            {
                string old = Path.Combine(Path.GetDirectoryName(f), "manager.old.log");
                try { if (File.Exists(old)) File.Delete(old); File.Move(f, old); } catch { }
            }
            File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + "\r\n");
        }
        catch { }
    }
    void Log(string s)
    {
        FileLog(s);
        logBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
    }
    void UI(MethodInvoker m) { try { if (IsHandleCreated && !IsDisposed) BeginInvoke(m); } catch { } }

    // ---- style (same rules as the PSU lab)
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyWindow(); Native.FrameChanged(Handle); }

    // No title bar: the client area takes the caption's place (the sides and bottom keep Windows' sizing edges, the
    // shadow and the rounded corners). The header acts as the title bar: dragging moves the window, a double-click
    // maximizes, the top edge sizes it.
    [StructLayout(LayoutKind.Sequential)] struct NcRect { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct AppBarData { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public NcRect rc; public IntPtr lParam; }
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref AppBarData d);
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x83 && m.WParam != IntPtr.Zero)   // WM_NCCALCSIZE
        {
            NcRect before = (NcRect)Marshal.PtrToStructure(m.LParam, typeof(NcRect));
            base.WndProc(ref m);
            NcRect after = (NcRect)Marshal.PtrToStructure(m.LParam, typeof(NcRect));
            // maximized, the frame hangs over the screen edge: keep the top inside, as thick as the side frame
            after.T = before.T + (Native.IsZoomed(m.HWnd) ? Math.Max(0, after.L - before.L) : 0);
            if (Native.IsZoomed(m.HWnd))
            {   // an auto-hiding taskbar only comes up if the window doesn't cover the whole screen: keep 2 px free on its side
                Rectangle mon = Screen.FromRectangle(Rectangle.FromLTRB(before.L, before.T, before.R, before.B)).Bounds;
                AppBarData q = new AppBarData(); q.cbSize = Marshal.SizeOf(typeof(AppBarData));
                if ((SHAppBarMessage(4, ref q).ToInt64() & 1) != 0)   // ABM_GETSTATE: ABS_AUTOHIDE
                    for (uint edge = 0; edge < 4; edge++)
                    {
                        AppBarData d = new AppBarData(); d.cbSize = q.cbSize; d.uEdge = edge;
                        d.rc.L = mon.Left; d.rc.T = mon.Top; d.rc.R = mon.Right; d.rc.B = mon.Bottom;
                        if (SHAppBarMessage(0x0B, ref d) == IntPtr.Zero) continue;   // ABM_GETAUTOHIDEBAREX: none on this side
                        if (edge == 0) after.L += 2; else if (edge == 1) after.T += 2; else if (edge == 2) after.R -= 2; else after.B -= 2;
                    }
            }
            Marshal.StructureToPtr(after, m.LParam, false);
            m.Result = IntPtr.Zero;
            return;
        }
        if (m.Msg == 0x214 && contentHeight > 0)   // WM_SIZING: an edge dragged no further down than the tiles need
        {
            int side = m.WParam.ToInt32();   // WMSZ_TOP 3, TOPLEFT 4, TOPRIGHT 5, BOTTOM 6, BOTTOMLEFT 7, BOTTOMRIGHT 8
            if (side >= 3 && side <= 8)
            {
                NcRect r = (NcRect)Marshal.PtrToStructure(m.LParam, typeof(NcRect));
                int most = Math.Max(MinimumSize.Height, cardsHost.Top + contentHeight + D(32) + D(34) + (Height - ClientSize.Height));   // as FitHeight
                // (a corner drag that changes the width uses the tiles' height at the last layout: it catches up as the drag goes on)
                if (r.B - r.T > most)
                {
                    if (side >= 6) r.B = r.T + most; else r.T = r.B - most;
                    Marshal.StructureToPtr(r, m.LParam, false);
                }
            }
            base.WndProc(ref m);
            return;
        }
        if (m.Msg == 0x84)   // WM_NCHITTEST
        {
            base.WndProc(ref m);
            if (m.Result.ToInt64() == 1)   // HTCLIENT
            {
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point(unchecked((short)(lp & 0xFFFF)), unchecked((short)((lp >> 16) & 0xFFFF))));
                bool max = Native.IsZoomed(m.HWnd);
                int edge = D(5);
                if (!max && p.Y < edge) m.Result = (IntPtr)(p.X < D(14) ? 13 : p.X >= ClientSize.Width - D(14) ? 14 : 12);   // HTTOPLEFT / HTTOPRIGHT / HTTOP
                else if (p.Y < headerBottom) m.Result = (IntPtr)2;   // HTCAPTION
            }
            return;
        }
        base.WndProc(ref m);
    }
    void ApplyWindow()
    {
        glassOn = Native.Apply(Handle, Theme.Dark, 0);
    }

    void ApplyStyle()
    {
        Theme.Set(Core.Cfg.Colors);
        head.Font = Theme.Header;
        if (IsHandleCreated) ApplyWindow();
        SuspendLayout();
        BackColor = Theme.Window; ForeColor = Theme.Text;
        Style(this, Theme.Window);
        logBox.BackColor = Theme.Input; logBox.ForeColor = Theme.Text; logBox.BorderStyle = BorderStyle.FixedSingle;
        Native.ControlTheme(logBox.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer");
        Native.ControlTheme(cardsHost.Handle, Theme.Dark ? "DarkMode_Explorer" : "Explorer");
        foreach (EmuCard c in cards) c.Box.Glow = Color.Empty;   // DoLayout gives them the new scheme's tones
        InfoText();
        ResumeLayout();
        Invalidate(true);
    }

    void Style(Control parent, Color bg)
    {
        bool over = parent is Card || parent is GlowPanel;   // drawn over a tile or the window background
        foreach (Control c in parent.Controls)
        {
            Color inner = bg;
            if (c is Card) { c.BackColor = Theme.Card; inner = Theme.Card; }
            else if (c is GlowPanel) { c.BackColor = Theme.Window; inner = Theme.Window; }
            else if (c is FlatBar || c is StatusChip || c is Badge || c is Avatars || c is Ring || c is Bars || c is DiscordTag)
            {
                c.BackColor = bg;
                string fr; if (fontRoles.TryGetValue(c, out fr)) c.Font = RoleFont(fr);
                if (c is StatusChip) c.ForeColor = RoleColor(c);
            }
            else if (c is PictureBox) c.BackColor = over ? Color.Transparent : bg;
            else if (c is Button) { StyleButton((Button)c); c.BackColor = bg; c.ForeColor = Theme.Text; }
            else if (c is CheckBox) { c.BackColor = bg; c.ForeColor = Theme.Text; }
            else if (c is Label)
            {
                c.BackColor = over ? Color.Transparent : bg; c.ForeColor = RoleColor(c);
                string fr;
                c.Font = fontRoles.TryGetValue(c, out fr) ? RoleFont(fr) : c == head || c.Font.Size > 12 ? c.Font : strong.Contains(c) ? Theme.Title : Theme.Body;
                LinkLabel ll = c as LinkLabel;
                if (ll != null) { ll.LinkColor = ll.ActiveLinkColor = ll.VisitedLinkColor = Theme.Accent; ll.DisabledLinkColor = Theme.Disabled; ll.LinkBehavior = LinkBehavior.HoverUnderline; }
            }
            else if (c is Panel) c.BackColor = bg;
            if (c.Controls.Count > 0) Style(c, inner);
            c.Invalidate();
        }
    }

    static Font RoleFont(string r)
    {
        return r == "caption" ? Theme.Caption : r == "big" ? Theme.Big : r == "value" ? Theme.Value : r == "name" ? Theme.Name
             : r == "small" ? Theme.Small : r == "large" ? Theme.Large : r == "strong" ? Theme.Strong : Theme.Body;
    }

    static Color RoleColor(Control c)
    {
        string r;
        if (!roles.TryGetValue(c, out r)) { r = c.ForeColor == Color.DimGray ? "muted" : "text"; roles[c] = r; }
        return r == "muted" ? Theme.Muted : r == "info" ? Theme.Info : r == "danger" ? Theme.Danger : r == "success" ? Theme.Success : r == "accent" ? Theme.Accent : Theme.Text;
    }

    // An accent button: Theme.Accent = the scheme's gradient (kept when the colors change); another colour = outlined in it.
    internal static void Outline(Button b, Color c) { accents[b] = c == Theme.Accent ? Color.Empty : c; b.Invalidate(); }

    internal static void StyleButton(Button b)
    {
        if (!styled.Add(b)) return;
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.UseVisualStyleBackColor = false;
        b.Paint += PaintButton;
        EventHandler inv = delegate { b.Invalidate(); };
        b.MouseEnter += delegate { Anim.To(b, 0, 1); }; b.MouseLeave += delegate { Anim.To(b, 0, 0); };
        b.EnabledChanged += inv; b.TextChanged += inv;
        b.MouseDown += delegate { b.Invalidate(); }; b.MouseUp += delegate { b.Invalidate(); };
    }

    // Drop-down lists drawn in the scheme's colours (Windows draws a focused one white).
    internal static void DarkCombo(ComboBox c)
    {
        c.DrawMode = DrawMode.OwnerDrawFixed;
        c.ItemHeight = (int)Math.Round(20 * Card.K);
        c.DrawItem += delegate (object s, DrawItemEventArgs e)
        {
            ComboBox cb = (ComboBox)s;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0, sel = (e.State & DrawItemState.Selected) != 0;
            Color back = edit ? (cb.Focused ? Theme.Blend(Theme.Input, Theme.Accent, 0.16) : Theme.Input) : sel ? Theme.ControlHover : Theme.Input;
            using (SolidBrush b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Index < 0) return;
            TextRenderer.DrawText(e.Graphics, cb.GetItemText(cb.Items[e.Index]), cb.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                                  cb.Enabled ? Theme.Text : Theme.Disabled, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        c.GotFocus += delegate { c.Invalidate(); }; c.LostFocus += delegate { c.Invalidate(); };
        c.DropDown += delegate { Native.DarkList(c); };
    }

    // Check boxes drawn like the PSU lab's: rounded box, accent fill + tick when checked, text after it.
    internal static void StyleCheck(CheckBox c)
    {
        if (!styled.Add(c)) return;
        c.Paint += PaintCheck;
        EventHandler inv = delegate { c.Invalidate(); };
        c.MouseEnter += delegate { Anim.To(c, 0, 1); }; c.MouseLeave += delegate { Anim.To(c, 0, 0); };
        c.EnabledChanged += inv; c.CheckedChanged += delegate { Anim.To(c, 1, c.Checked ? 1 : 0); c.Invalidate(); };
    }

    static void PaintCheck(object s, PaintEventArgs e)
    {
        CheckBox c = (CheckBox)s; Graphics g = e.Graphics;
        Theme.ParentBack(g, c);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float hv = c.Enabled ? Anim.Get(c, 0, 0) : 0;
        bool hot = hv > 0.5f;
        float K = Card.K;
        if (toggles.Contains(c))
        {   // a switch on the right, its text on the left; the knob slides and the colour fades in
            float on = Anim.Get(c, 1, c.Checked ? 1 : 0);
            float th = Math.Min(c.Height - 4, 20 * K), tw = th * 1.8f;
            RectangleF tr = new RectangleF(c.Width - tw - 1.5f, (c.Height - th) / 2f, tw, th);
            Theme.FillRound(g, Theme.Blend(Theme.Blend(Theme.Input, Theme.Stroke, 0.7), Theme.ControlHover, hv), Theme.Stroke, tr, th / 2, 1);
            if (on > 0)
                using (GraphicsPath p = Theme.Round(tr, th / 2))
                using (LinearGradientBrush b = Theme.AccentBrush(tr, 0.10 * hv))
                {
                    if (on < 1) b.LinearColors = new[] { Color.FromArgb((int)(255 * on), b.LinearColors[0]), Color.FromArgb((int)(255 * on), b.LinearColors[1]) };
                    g.FillPath(b, p);
                    if (!c.Enabled) using (SolidBrush veil = new SolidBrush(Color.FromArgb((int)(140 * on), Theme.Card))) g.FillPath(veil, p);
                }
            float d = th - 6 * K, x0 = tr.X + 3 * K, x1 = tr.Right - 3 * K - d, kx = x0 + (x1 - x0) * on;
            Color knob = !c.Enabled ? Theme.Disabled : Theme.Blend(Theme.Muted, Theme.AccentText, on);
            using (SolidBrush b = new SolidBrush(knob)) g.FillEllipse(b, kx, tr.Y + 3 * K, d, d);
            TextRenderer.DrawText(g, c.Text, c.Font, new Rectangle(0, 0, (int)(c.Width - tw - 10 * K), c.Height),
                                  !c.Enabled ? Theme.Disabled : Theme.Blend(Theme.Muted, Theme.Text, Math.Max(on, hv * 0.6f)),
                                  TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return;
        }
        float box = 18 * K;
        RectangleF r = new RectangleF(0.5f, (c.Height - box) / 2f, box - 1, box - 1);
        if (c.Checked)
        {
            if (c.Enabled) using (GraphicsPath p = Theme.Round(r, 5 * K)) using (LinearGradientBrush b = Theme.AccentBrush(r, hot ? 0.10 : 0)) g.FillPath(b, p);
            else Theme.FillRound(g, Theme.Disabled, Color.Empty, r, 5 * K, 0);
            using (Pen p = new Pen(Theme.AccentText, 1.8f * K))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                float x = r.X, y = r.Y, u = r.Width / 17f;
                g.DrawLines(p, new[] { new PointF(x + 4.5f * u, y + 9f * u), new PointF(x + 7.5f * u, y + 12f * u), new PointF(x + 13f * u, y + 5.5f * u) });
            }
        }
        else Theme.FillRound(g, hot ? Theme.ControlHover : Theme.Input, c.Enabled ? Theme.StrokeStrong : Theme.Disabled, r, 5 * K, 1);
        TextRenderer.DrawText(g, c.Text, c.Font, new Rectangle((int)(box + 8 * K), 0, c.Width - (int)(box + 8 * K), c.Height), c.Enabled ? c.ForeColor : Theme.Disabled,
                              TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    // Buttons with three little dots in the scheme's colours before the text (Themes): they spread a bit on hover.
    static readonly HashSet<Button> dotted = new HashSet<Button>();

    // Buttons: rounded; the accent one filled with the scheme's gradient, the others raised tiles.
    static void PaintButton(object s, PaintEventArgs e)
    {
        Button b = (Button)s; Graphics g = e.Graphics;
        Theme.ParentBack(g, b);
        if (b.Width < 4 || b.Height < 4) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float hv = b.Enabled ? Anim.Get(b, 0, 0) : 0;
        bool down = b.Enabled && b.ClientRectangle.Contains(b.PointToClient(Control.MousePosition)) && (Control.MouseButtons & MouseButtons.Left) != 0;
        float rad = Math.Min(b.Height / 2f - 1, 12 * Card.K);
        RectangleF r = new RectangleF(0.5f, 0.5f, b.Width - 1.5f, b.Height - 1.5f);
        if (down) r.Inflate(-0.5f, -0.5f);
        Color acc, text = Theme.Text;
        Font font = b.Font;
        bool isAcc = accents.TryGetValue(b, out acc);
        if (!b.Enabled) { Theme.FillRound(g, Theme.ControlOff, Theme.Stroke, r, rad, 1); text = Theme.Disabled; }
        else if (isAcc && acc.IsEmpty)
        {
            using (GraphicsPath p = Theme.Round(r, rad))
            using (LinearGradientBrush br = Theme.AccentBrush(r, down ? -0.12 : 0.12 * hv))
                g.FillPath(br, p);
            text = Theme.AccentText; font = Theme.Strong ?? b.Font;
        }
        else if (isAcc)
        {
            Theme.FillRound(g, Theme.Blend(Theme.Control, acc, down ? 0.30 : 0.12 + 0.10 * hv), acc, r, rad, 1.6f);
        }
        else Theme.FillRound(g, down ? Theme.ControlPressed : Theme.Blend(Theme.Control, Theme.ControlHover, hv), Theme.Blend(Theme.Stroke, Theme.StrokeStrong, hv), r, rad, 1);
        Rectangle tr = new Rectangle(4, 0, b.Width - 8, b.Height - (down ? 0 : 1));
        if (dotted.Contains(b))
        {
            float K = Card.K, d = 9 * K, step = (5.5f + 1.5f * hv) * K, w = d + 2 * step, gap = 7 * K;
            int tw = TextRenderer.MeasureText(b.Text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            float x = (b.Width - (w + gap + tw)) / 2f, y = (b.Height - d) / 2f - (down ? 0 : 0.5f);
            Color[] tones = { Theme.Accent3, Theme.Accent2, Theme.Accent };   // drawn back to front: the accent on top
            Color edge = down ? Theme.ControlPressed : Theme.Blend(Theme.Control, Theme.ControlHover, hv);
            for (int i = 0; i < 3; i++)
            {
                RectangleF dr = new RectangleF(x + (2 - i) * step, y, d, d);
                using (SolidBrush br = new SolidBrush(b.Enabled ? tones[i] : Theme.Disabled)) g.FillEllipse(br, dr);
                using (Pen ring = new Pen(edge, 1.4f * K)) g.DrawEllipse(ring, dr);   // a thin edge so overlapping dots stay apart
            }
            TextRenderer.DrawText(g, b.Text, font, new Rectangle((int)(x + w + gap), 0, tw + 4, tr.Height), text,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return;
        }
        TextRenderer.DrawText(g, b.Text, font, tr, text,
                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    // ---- entry
    [STAThread]
    static int Main(string[] args)
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        string cfg = Path.Combine(exeDir, "emulators.json");
        List<string> cli = null; string logFile = null;
        bool sandbox = false, custom = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length) { cfg = args[++i]; custom = true; }
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
        Mutex one = null;
        if (sandbox || custom) Core.PortableAllowed = false;
        else
        {
            // opened from inside the zip (Windows runs it from a temp folder): everything, saves too, would land in a
            // folder Windows cleans up
            string tmp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\') + "\\";
            if ((exeDir + "\\").StartsWith(tmp, StringComparison.OrdinalIgnoreCase) || exeDir.IndexOf(".zip\\", StringComparison.OrdinalIgnoreCase) >= 0 || exeDir.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if (cli != null) return 2;
                MessageBox.Show("Extract the whole zip first (right-click it, then Extract All), then open EmuManager.exe from the extracted folder.", APP, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }
            Core.Portable = File.Exists(Path.Combine(exeDir, Core.PORTABLE_MARK));
            if (Core.Portable && Path.GetPathRoot(exeDir + "\\").TrimEnd('\\') == exeDir.TrimEnd('\\') && cli == null)
            {
                MessageBox.Show("Put the portable manager in a folder of its own (for example " + exeDir.TrimEnd('\\') + "\\NS2 Emulator Manager), not straight on the drive, then open it from there.", APP, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }
            // a folder Setup installed into stays the installed manager, even with a portable.txt in it
            if (InstalledHere(exeDir)) { Core.Portable = false; Core.PortableAllowed = false; }
        }
        if (cli == null)
        {
            string home = Path.GetDirectoryName(Path.GetFullPath(cfg));
            try
            {   // can it save here? (Program Files, a CD, a write-protected stick)
                Directory.CreateDirectory(home);
                string probe = Path.Combine(home, ".write-test");
                File.WriteAllText(probe, ""); File.Delete(probe);
            }
            catch
            {
                MessageBox.Show("The manager can't save files in " + home + ".\r\n\r\nMove its folder somewhere you can save files (not Program Files or a write-protected drive), then open it again.", APP, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }
            // one window per folder: two copies working on the same emulators would get in each other's way
            string id;
            using (System.Security.Cryptography.SHA1 h = System.Security.Cryptography.SHA1.Create())
                id = BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(home.TrimEnd('\\').ToLowerInvariant()))).Replace("-", "");
            one = new Mutex(false, @"Local\NS2EmulatorManager-" + id);
            bool mine;
            try { mine = one.WaitOne(10000); } catch (AbandonedMutexException) { mine = true; }   // a copy that crashed
            if (!mine)
            {   // the one that's open: to the front
                foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Application.ExecutablePath)))
                    try { if (p.Id != Process.GetCurrentProcess().Id && p.MainWindowHandle != IntPtr.Zero) Native.Raise(p.MainWindowHandle); } catch { } finally { p.Dispose(); }
                return 3;
            }
        }
        try
        {
            Core.LoadConfig(cfg);
            if (sandbox && !Core.Cfg.Sandbox) { Core.Cfg.Sandbox = true; Core.SaveConfig(); }
            // the installed manager (only that one: a copy elsewhere leaves it for the installed one) takes its files
            // out of AppData into its own folder
            if (!sandbox && !custom && !Core.Portable && InstalledHere(exeDir))
                try { string mv = Core.MoveOldDataDir(); if (mv != null) FileLog(mv); } catch (Exception ex) { FileLog("Couldn't move the old AppData folder: " + ex.Message); }
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
        Theme.Set(Core.Cfg.Colors);
        Application.Run(new EmuForm());
        GC.KeepAlive(one);
        return 0;
    }

    // Did Setup install the manager into this folder (its Apps & features entry points here)?
    static bool InstalledHere(string dir)
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Switch2EmulatorManager"))
            {
                string d = k == null ? null : k.GetValue("InstallLocation") as string;
                return !string.IsNullOrEmpty(d) && string.Equals(Path.GetFullPath(d).TrimEnd('\\'), Path.GetFullPath(dir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { return false; }
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
                if (a[i] == "manager-check" || a[i] == "manager-download")   // tests: newest manager on GitHub; download + unpack it (any version)
                {
                    Version nv; Release mr = Core.ManagerUpdate(true, out nv);
                    log("this manager " + Core.AppVersion + ", newest on GitHub " + (nv != null ? nv.ToString() : "none") + ", update offered: " + (mr != null));
                    if (a[i] == "manager-download")
                    {
                        if (mr == null)
                        {   // test: take the newest release even if it isn't newer
                            log("(no newer release; downloading v" + nv + " for the test)");
                            mr = Core.ManagerRelease(nv);
                        }
                        log("setup: " + Core.DownloadManagerUpdate(mr, log, (g, t) => { }));
                    }
                    continue;
                }
                if (a[i] == "host" && i + 1 < a.Count)   // test: tick "Share this emulator's data" on ID
                {
                    string hid = a[++i]; EmuDef h = Core.Cfg.Emulators.First(x => x.Id == hid);
                    List<EmuDef> all = Core.Cfg.Emulators.Where(x => !string.IsNullOrEmpty(x.Exe) && File.Exists(Path.Combine(x.Dir, x.Exe)) && UserData.IsPortable(x)).ToList();
                    if (Core.Cfg.ShareHost == null) UserData.StartSharing(h, all, log);
                    Core.Cfg.ShareHost = h.Id; Core.Cfg.ShareNew = UserData.HostItems.Select(it => it.Key).ToList(); Core.SaveConfig();
                    log("host -> " + h.Id);
                    continue;
                }
                if (a[i] == "dupes-backups" || a[i] == "dupes-backups-remove")   // test: list (and remove) duplicate backups
                {
                    foreach (KeyValuePair<string, long> p in UserData.DuplicateBackups())
                    {
                        log("duplicate backup: " + Path.GetFileName(p.Key) + " (" + (p.Value >> 20) + " MB)");
                        if (a[i] == "dupes-backups-remove") UserData.Recycle(p.Key, log);
                    }
                    continue;
                }
                if (a[i] == "host-stop" || a[i] == "host-stop-back")   // test: untick; -back = the others go back to their own saves
                {
                    EmuDef hd = Core.Cfg.Emulators.FirstOrDefault(x => x.Id == Core.Cfg.ShareHost);
                    UserData.StopSharing(hd, Core.Cfg.Emulators.Where(x => UserData.IsPortable(x)).ToList(), a[i] == "host-stop-back", log);
                    Core.Cfg.ShareHost = null; Core.Cfg.ShareNew = null; Core.SaveConfig();
                    log("sharing stopped");
                    continue;
                }
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
                    foreach (EmuDef d in Core.Cfg.Emulators) log(d.Id + " (Switch " + Core.SwitchOf(d) + "): " + (Core.SyncGamesFolder(d, null, 0) ?? "no change"));
                else if (a[i] == "previous-version")   // test: which version ran before (for "What's new"); test environment only
                    log(Core.Cfg.Sandbox ? "previous: " + (PreviousVersion() == null ? "none" : PreviousVersion().ToString()) : "test environment only");
                else if (a[i] == "move-old-data")   // test: the first-start move out of AppData (test environment, NS2_TEST_OLD_DATADIR)
                    log(Core.Cfg.Sandbox ? (Core.MoveOldDataDir() ?? "nothing to move") : "test environment only");
                else if (a[i] == "games-launch")   // what the check before each launch does (only takes the other Switch's folder out)
                    foreach (EmuDef d in Core.Cfg.Emulators) log(d.Id + " (Switch " + Core.SwitchOf(d) + "): " + (Core.SyncGamesFolder(d, null, 0, false) ?? "no change"));
                else if (a[i] == "update" || a[i] == "restore")
                {
                    string id = a[++i];
                    EmuDef d = Core.Cfg.Emulators.First(x => x.Id == id);
                    if (a[i - 1] == "update") Core.InstallOrUpdate(d, log, (g, t) => { }, CancellationToken.None);
                    else Core.Restore(d, log);
                }
                else { log("unknown command " + a[i]); rc = 1; }
            }
            catch (Exception ex) { log("ERROR: " + ex.Message); FileLog(ex.ToString()); rc = 1; }
        }
        return rc;
    }
}
