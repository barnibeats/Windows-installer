// "About" tab: version, environment and updates.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

class AboutPage : Page
{
    readonly Card cApp = new Card(), cUpd = new Card();
    readonly Txt lblVersion = new Txt(), lblInfo = new Txt(), lblState = new Txt(), lblNote = new Txt();
    readonly PillButton btnRepo = new PillButton(), btnReleases = new PillButton(), btnCheck = new PillButton(), btnRestart = new PillButton(), btnDownload = new PillButton();
    readonly GradientBar bar = new GradientBar();
    readonly MainForm main;

    public AboutPage(MainForm main)
    {
        this.main = main;
        Controls.Add(cApp); Controls.Add(cUpd);
        cApp.Step = "i"; cUpd.Step = "↑";

        lblVersion.Font = Theme.Title;
        lblInfo.IsMuted = true;
        lblNote.IsMuted = true;
        btnRepo.Kind = BtnKind.Ghost; btnReleases.Kind = BtnKind.Ghost; btnCheck.Kind = BtnKind.Ghost; btnDownload.Kind = BtnKind.Ghost;
        cApp.Controls.Add(lblVersion); cApp.Controls.Add(lblInfo); cApp.Controls.Add(btnRepo); cApp.Controls.Add(btnReleases);
        cUpd.Controls.Add(lblState); cUpd.Controls.Add(lblNote); cUpd.Controls.Add(bar); cUpd.Controls.Add(btnCheck); cUpd.Controls.Add(btnDownload); cUpd.Controls.Add(btnRestart);

        btnRepo.Click += delegate { Open("https://github.com/" + AppInfo.Repo); };
        btnReleases.Click += delegate { Open("https://github.com/" + AppInfo.Repo + "/releases"); };
        btnCheck.Click += delegate { Updater.CheckAsync(true); };
        btnDownload.Click += delegate { Open("https://github.com/" + AppInfo.Repo + "/releases/latest"); };
        btnRestart.Click += delegate { main.RestartForUpdate(); };
        Updater.Changed += delegate { UpdateState(); };
        ApplyLanguage();
    }

    static void Open(string url)
    {
        try { Process.Start(url); } catch (Exception) { }
    }

    public void ApplyLanguage()
    {
        cApp.Title = S.T("about.card.app"); cUpd.Title = S.T("about.card.upd");
        btnRepo.Text = S.T("about.repo"); btnReleases.Text = S.T("about.releases");
        btnCheck.Text = S.T("about.check"); btnRestart.Text = S.T("about.restart"); btnDownload.Text = S.T("about.download");
        lblVersion.Text = S.T("app.title") + "  v" + AppInfo.Version;
        lblInfo.Text = S.T("about.desc") + "\r\n\r\n" +
            S.T("about.env.os") + " " + Environment.OSVersion.VersionString + "\r\n" +
            S.T("about.env.mode") + " " + (Env.IsWinPE ? "WinPE" : "Windows") + ", " + Env.Firmware() + "\r\n" +
            ".NET Framework (CLR " + Environment.Version + ")" + "\r\n" +
            S.T("about.env.exe") + " " + Application.ExecutablePath + "\r\n" +
            S.T("about.env.data") + " " + Paths.DataDir;
        UpdateState();
        DoLayout();
    }

    public void UpdateState()
    {
        string st = Updater.State;
        string tag = Updater.Latest == null ? "" : Updater.Latest.Tag;
        bar.Visible = st == "downloading";
        if (st == "downloading") bar.Value = Updater.DownloadPercent;
        lblState.Tone = null;
        switch (st)
        {
            case "checking": lblState.Text = S.T("upd.checking"); break;
            case "none": lblState.Text = S.T("upd.none"); lblState.Tone = Theme.Ok; break;
            case "available": lblState.Text = S.F("upd.available", tag); lblState.Tone = Theme.Warn; break;
            case "downloading": lblState.Text = S.F("upd.downloading", tag, Updater.DownloadPercent); break;
            case "ready": lblState.Text = S.F("upd.ready", tag); lblState.Tone = Theme.Ok; break;
            case "error": lblState.Text = S.T("upd.error") + " " + Updater.Error; lblState.Tone = Theme.Bad; break;
            default: lblState.Text = S.T("upd.idle"); break;
        }
        lblNote.Text = Updater.IsDevBuild ? S.T("upd.dev") : S.T("upd.auto");
        btnRestart.Visible = st == "ready";
        btnDownload.Visible = st == "available" && !Updater.IsDevBuild || (st == "available" && Updater.IsDevBuild);
        btnCheck.Enabled = st != "checking" && st != "downloading";
        DoLayout();
    }

    protected override void OnResize(EventArgs eventargs) { base.OnResize(eventargs); DoLayout(); }

    void DoLayout()
    {
        int W = ClientSize.Width;
        if (W < 300) return;
        int m = 16, gap = 12, colW = (W - 2 * m - gap) / 2;
        cApp.SetBounds(m, 4, colW, 330);
        cUpd.SetBounds(m + colW + gap, 4, colW, 330);
        lblVersion.SetBounds(16, 50, colW - 32, 24);
        lblInfo.SetBounds(16, 82, colW - 32, 180);
        btnRepo.SetBounds(16, 280, 130, 34);
        btnReleases.SetBounds(154, 280, 130, 34);
        lblState.SetBounds(16, 50, colW - 32, 48);
        bar.SetBounds(16, 104, colW - 32, 10);
        lblNote.SetBounds(16, 124, colW - 32, 90);
        btnCheck.SetBounds(16, 280, 190, 34);
        btnDownload.SetBounds(214, 280, 190, 34);
        btnRestart.SetBounds(16, 232, colW - 32, 38);
        btnRestart.Kind = BtnKind.Primary;
    }
}
