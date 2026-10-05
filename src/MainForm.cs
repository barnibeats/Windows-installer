// Main window: header with logo, tabs, language and theme switches, and the pages.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Round theme switch drawn as a moon (dark) or a sun (light).
class ThemeButton : Control
{
    bool hover;
    public ThemeButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Size = new Size(36, 36);
        Cursor = Cursors.Hand;
        TabStop = true;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override bool IsInputKey(Keys k) { return k == Keys.Space || k == Keys.Enter || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (SolidBrush b = new SolidBrush(hover ? Theme.CardHover : Theme.Card)) g.FillEllipse(b, r);
        using (Pen p = new Pen(hover || Focused ? Theme.A2 : Theme.Border)) g.DrawEllipse(p, r);
        float cx = Width / 2f, cy = Height / 2f;
        if (Theme.Dark)
        {
            // moon: a disc with a bite taken out
            using (GraphicsPath moon = new GraphicsPath())
            {
                moon.AddEllipse(cx - 7, cy - 7, 14, 14);
                using (GraphicsPath bite = new GraphicsPath())
                {
                    bite.AddEllipse(cx - 2, cy - 9, 13, 13);
                    using (Region reg = new Region(moon))
                    {
                        reg.Exclude(bite);
                        using (SolidBrush mb = new SolidBrush(Theme.Text)) g.FillRegion(mb, reg);
                    }
                }
            }
        }
        else
        {
            using (SolidBrush sb = new SolidBrush(Theme.Warn)) g.FillEllipse(sb, cx - 5, cy - 5, 10, 10);
            using (Pen rp = new Pen(Theme.Warn, 1.6f))
            {
                rp.StartCap = LineCap.Round; rp.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(rp, (float)(cx + Math.Cos(a) * 8), (float)(cy + Math.Sin(a) * 8), (float)(cx + Math.Cos(a) * 11), (float)(cy + Math.Sin(a) * 11));
                }
            }
        }
    }
}

class MainForm : Form
{
    readonly SegTabs tabs = new SegTabs();
    readonly DropPill langPick = new DropPill();
    readonly ThemeButton themeBtn = new ThemeButton();
    readonly PillButton updBtn = new PillButton();
    readonly InstallPage install = new InstallPage();
    readonly CapturePage capture = new CapturePage();
    readonly AboutPage about;
    readonly Page[] pages;
    readonly ToolTip tip = new ToolTip();
    const int HeaderH = 66;

    public MainForm()
    {
        Ui.Main = this;
        Text = S.T("app.title");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 780);
        MinimumSize = new Size(1000, 720);
        BackColor = Theme.Bg;
        Font = Theme.Body;
        DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

        about = new AboutPage(this);
        pages = new Page[] { install, capture, about };
        foreach (Page p in pages) { p.Visible = false; Controls.Add(p); }

        tabs.Location = new Point(300, 14);
        tabs.SelectedChanged += delegate { ShowPage(tabs.SelectedIndex); };
        Controls.Add(tabs);

        langPick.Width = 130;
        langPick.SetItems(new string[] { "Русский", "English", "Українська" });
        langPick.SelectedIndex = S.LangIndex;
        langPick.SelectedIndexChanged += delegate { S.SetLang(langPick.SelectedIndex); Settings.Set("lang", S.Code); ApplyLanguage(); };
        Controls.Add(langPick);

        themeBtn.Click += delegate { Theme.Set(!Theme.Dark); };
        Controls.Add(themeBtn);

        updBtn.Visible = false;
        updBtn.Height = 34;
        updBtn.Click += delegate
        {
            if (Updater.State == "ready") RestartForUpdate();
            else { tabs.SelectedIndex = 2; }
        };
        Controls.Add(updBtn);

        Theme.Changed += OnThemeChanged;
        Updater.Changed += UpdateHeader;
        Resize += delegate { LayoutAll(); };
        ApplyLanguage();
        ShowPage(0);
        LayoutAll();
        FormClosing += OnClosing;
        FormClosed += delegate { install.Release(); };

        if (!Updater.IsDevBuild) Updater.CheckAsync(true);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTitleBar(Handle, Theme.Dark); }

    void ShowPage(int i)
    {
        for (int k = 0; k < pages.Length; k++) pages[k].Visible = k == i;
        if (tabs.SelectedIndex != i) tabs.SelectedIndex = i;
    }

    void LayoutAll()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        foreach (Page p in pages) p.SetBounds(0, HeaderH, W, H - HeaderH);
        themeBtn.Location = new Point(W - 20 - themeBtn.Width, 15);
        langPick.Location = new Point(themeBtn.Left - 10 - langPick.Width, 17);
        updBtn.Location = new Point(langPick.Left - 12 - updBtn.Width, 16);
        Invalidate();
    }

    public void ApplyLanguage()
    {
        Text = S.T("app.title");
        tabs.SetItems(new string[] { S.T("tab.install"), S.T("tab.capture"), S.T("tab.about") });
        tabs.Width = tabs.PreferredWidth + 4;
        tip.SetToolTip(themeBtn, S.T("hdr.theme"));
        install.ApplyLanguage();
        capture.ApplyLanguage();
        about.ApplyLanguage();
        UpdateHeader();
        LayoutAll();
    }

    void UpdateHeader()
    {
        string st = Updater.State;
        string tag = Updater.Latest == null ? "" : Updater.Latest.Tag;
        if (st == "ready") { updBtn.Text = S.F("hdr.upd.ready", tag); updBtn.Visible = true; }
        else if (st == "available") { updBtn.Text = S.F("hdr.upd.avail", tag); updBtn.Visible = true; }
        else if (st == "downloading") { updBtn.Text = S.F("hdr.upd.dl", Updater.DownloadPercent); updBtn.Visible = true; }
        else updBtn.Visible = false;
        updBtn.Width = TextRenderer.MeasureText(updBtn.Text, updBtn.Font).Width + 34;
        LayoutAll();
    }

    void OnThemeChanged()
    {
        Settings.Set("theme", Theme.Dark ? "dark" : "light");
        BackColor = Theme.Bg;
        Native.DarkTitleBar(Handle, Theme.Dark);
        Retheme(this);
        install.ApplyLanguage();   // re-colors rows
        capture.ApplyLanguage();
        about.UpdateState();
        Invalidate(true);
    }

    static void Retheme(Control c)
    {
        PillBox pb = c as PillBox; if (pb != null) pb.ApplyTheme();
        Stepper st = c as Stepper; if (st != null) st.ApplyTheme();
        ThemedList tl = c as ThemedList; if (tl != null) tl.ApplyTheme();
        LogBox lb = c as LogBox; if (lb != null) lb.ApplyTheme();
        foreach (Control ch in c.Controls) Retheme(ch);
    }

    public bool Busy { get { return install.Busy || capture.Busy; } }

    public void SelectTab(int i) { tabs.SelectedIndex = i; }

    public void RestartForUpdate()
    {
        if (Busy) { Dlg.Msg(this, S.T("main.busy"), DlgKind.Warn); return; }
        if (Updater.ApplyOnExit(true)) Close();
    }

    void OnClosing(object sender, FormClosingEventArgs e)
    {
        if (Busy)
        {
            e.Cancel = true;
            Dlg.Msg(this, S.T("main.busy"), DlgKind.Warn);
            return;
        }
        if (Updater.State == "ready") Updater.ApplyOnExit(false);
    }

    // ---- header painting ----------------------------------------------------------------------------
    protected override void OnPaintBackground(PaintEventArgs e) { Theme.PaintBackdrop(e.Graphics, ClientRectangle); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Theme.Quality(g);
        // logo: four gradient tiles
        Color[][] pairs = new Color[][]
        {
            new Color[] { ColorTranslator.FromHtml("#7c5cff"), ColorTranslator.FromHtml("#00d4ff") },
            new Color[] { ColorTranslator.FromHtml("#00d4ff"), ColorTranslator.FromHtml("#3ddc97") },
            new Color[] { ColorTranslator.FromHtml("#ff4fd8"), ColorTranslator.FromHtml("#7c5cff") },
            new Color[] { ColorTranslator.FromHtml("#ffb84f"), ColorTranslator.FromHtml("#ff4fd8") }
        };
        int t = 16, gap = 3, x0 = 22, y0 = 14;
        for (int i = 0; i < 4; i++)
        {
            Rectangle r = new Rectangle(x0 + (i % 2) * (t + gap), y0 + (i / 2) * (t + gap), t, t);
            using (GraphicsPath p = Theme.Round(r, 4))
            using (LinearGradientBrush b = Theme.Grad(r, pairs[i][0], pairs[i][1], 45f)) g.FillPath(b, p);
        }
        TextRenderer.DrawText(g, S.T("app.title"), Theme.Big, new Point(66, 10), Theme.Text, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "v" + AppInfo.Version, Theme.Small, new Point(68, 38), Theme.Muted, TextFormatFlags.NoPadding);
        using (Pen pen = new Pen(Theme.Alpha(Theme.Border, 160))) g.DrawLine(pen, 0, HeaderH - 1, Width, HeaderH - 1);
    }
}
