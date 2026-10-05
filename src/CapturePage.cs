// "Capture" tab. In Windows: checks, shortcut safety net, Sysprep. In WinPE: take the WIM of a prepared Windows.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

class CapturePage : Page
{
    readonly bool winpe = Env.IsWinPE;

    readonly Card cChecks = new Card(), cDest = new Card(), cActions = new Card(), cHelp = new Card();
    readonly PillButton btnCheck = new PillButton(), btnBrowse = new PillButton(), btnCmd = new PillButton(), btnRun = new PillButton();
    readonly PillButton btnSrcRefresh = new PillButton(), btnCapture = new PillButton();
    readonly ThemedList lvChecks = new ThemedList();
    readonly Frame frChecks = new Frame(), frLog = new Frame();
    readonly PillBox tbDest = new PillBox(), tbName = new PillBox();
    readonly DropPill ddComp = new DropPill(), ddSrc = new DropPill();
    readonly CheckPill chkAdmin = new CheckPill(), chkXml = new CheckPill(), chkKeep = new CheckPill();
    readonly Txt lblDest = new Txt(), lblName = new Txt(), lblComp = new Txt(), lblSrc = new Txt(), lblState = new Txt(), help = new Txt();
    readonly Txt lblStatus = new Txt();
    readonly ToolTip tips = new ToolTip();
    readonly GradientBar bar = new GradientBar();
    readonly LogBox log = new LogBox();
    readonly UiProgress prog;

    int blockers = 1;
    bool busy, checksStarted;
    List<WinVolume> volumes = new List<WinVolume>();

    public bool Busy { get { return busy; } }

    public CapturePage()
    {
        prog = new UiProgress(bar, lblStatus, log);
        lblStatus.IsMuted = true;
        foreach (PillButton b in new PillButton[] { btnCheck, btnBrowse, btnCmd, btnSrcRefresh }) b.Kind = BtnKind.Ghost;
        btnRun.Kind = BtnKind.Danger; btnCapture.Kind = BtnKind.Danger;
        btnRun.Font = new Font("Segoe UI Semibold", 10.5f); btnCapture.Font = new Font("Segoe UI Semibold", 11f);
        btnRun.Enabled = false;

        lblDest.IsMuted = true; lblName.IsMuted = true; lblComp.IsMuted = true; lblSrc.IsMuted = true; help.IsMuted = true;
        help.Font = Theme.Small;

        cChecks.Step = "1"; cDest.Step = winpe ? "1" : "2"; cActions.Step = "3"; cHelp.Step = "?";
        Controls.Add(cDest); Controls.Add(cHelp);

        // destination (both modes)
        cDest.Controls.Add(lblDest); cDest.Controls.Add(tbDest); cDest.Controls.Add(btnBrowse);
        cDest.Controls.Add(lblName); cDest.Controls.Add(tbName); cDest.Controls.Add(lblComp); cDest.Controls.Add(ddComp);
        tbName.Text = "MyWindows_" + DateTime.Now.ToString("yyyyMMdd");
        cHelp.Controls.Add(help);

        if (winpe)
        {
            cDest.Controls.Add(lblSrc); cDest.Controls.Add(ddSrc); cDest.Controls.Add(btnSrcRefresh); cDest.Controls.Add(lblState); cDest.Controls.Add(btnCapture);
            btnSrcRefresh.Click += delegate { LoadVolumes(); };
            ddSrc.SelectedIndexChanged += delegate { ShowState(); };
            btnCapture.Click += delegate { StartWinPeCapture(); };
        }
        else
        {
            Controls.Add(cChecks); Controls.Add(cActions);
            cChecks.Controls.Add(btnCheck);
            lvChecks.Columns.Add("", 60); lvChecks.Columns.Add("", 300);
            lvChecks.SetColumnPercents(11, 89);
            lvChecks.HeaderStyle = ColumnHeaderStyle.None;
            lvChecks.Dock = DockStyle.Fill;
            lvChecks.RowHeight = 26;
            frChecks.Controls.Add(lvChecks);
            cChecks.Controls.Add(frChecks);
            cDest.Controls.Add(chkAdmin); cDest.Controls.Add(chkXml); cDest.Controls.Add(chkKeep);
            chkAdmin.Checked = true; chkKeep.Checked = true; chkXml.Checked = false;
            cActions.Controls.Add(btnCmd); cActions.Controls.Add(btnRun);
            btnCheck.Click += delegate { RunChecks(); };
            btnCmd.Click += delegate { MakeCmd(); };
            btnRun.Click += delegate { StartSysprep(); };
        }
        btnBrowse.Click += delegate
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = S.T("cap.pickfolder");
                if (d.ShowDialog(FindForm()) == DialogResult.OK) tbDest.Text = d.SelectedPath;
            }
        };

        Controls.Add(bar); Controls.Add(lblStatus); frLog.Controls.Add(log); log.Dock = DockStyle.Fill; Controls.Add(frLog);

        ddComp.SetItems(new string[] { S.T("cap.comp.max"), S.T("cap.comp.fast"), S.T("cap.comp.none") });
        ddComp.SelectedIndex = 1;
        ApplyLanguage();
        VisibleChanged += delegate { if (Visible) OnShown(); };
    }

    void OnShown()
    {
        if (checksStarted) return;
        checksStarted = true;
        if (winpe) LoadVolumes(); else RunChecks();
        GuessDestination();
    }

    // ---- texts --------------------------------------------------------------------------------------
    public void ApplyLanguage()
    {
        cChecks.Title = S.T("cap.card.checks"); cActions.Title = S.T("cap.card.actions");
        cDest.Title = winpe ? S.T("cap.card.capture") : S.T("cap.card.dest");
        cHelp.Title = S.T("cap.card.help");
        btnCheck.Text = S.T("cap.btn.check"); btnBrowse.Text = S.T("cap.btn.browse");
        lblDest.Text = S.T("cap.dest"); lblName.Text = S.T("cap.name"); lblComp.Text = S.T("cap.comp");
        int sel = ddComp.SelectedIndex;
        ddComp.UpdateItems(new string[] { S.T("cap.comp.max"), S.T("cap.comp.fast"), S.T("cap.comp.none") });
        chkAdmin.Text = S.T("cap.opt.admin"); chkXml.Text = S.T("cap.opt.xml"); chkKeep.Text = S.T("cap.opt.keep");
        tips.SetToolTip(chkAdmin, S.T("cap.opt.admin.tip")); tips.SetToolTip(chkXml, S.T("cap.opt.xml.tip")); tips.SetToolTip(chkKeep, S.T("cap.opt.keep.tip"));
        btnCmd.Text = S.T("cap.btn.cmd"); btnRun.Text = S.T("cap.btn.run");
        lblSrc.Text = S.T("cap.src"); btnSrcRefresh.Text = S.T("cap.btn.refresh"); btnCapture.Text = S.T("cap.btn.capture");
        help.Text = S.T(winpe ? "cap.help.winpe" : "cap.help.win");
        if (!busy) lblStatus.Text = S.T("cap.ready");
        if (winpe) { List<string> t = new List<string>(); foreach (WinVolume v in volumes) t.Add(v.Title); if (t.Count > 0) ddSrc.UpdateItems(t); }
        ddSrc.Placeholder = S.T("cap.src.none");
        if (lastChecks != null) FillChecks(lastChecks);
        DoLayout();
    }

    // ---- layout -------------------------------------------------------------------------------------
    protected override void OnResize(EventArgs eventargs) { base.OnResize(eventargs); DoLayout(); }

    void DoLayout()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        if (W < 300 || H < 300) return;
        int m = 16, gap = 12, y0 = 4;
        int bottom = 150;                         // bar + status + log
        int topH = H - bottom - y0 - gap;
        int leftW = (W - 2 * m - gap) * 55 / 100, rightW = W - 2 * m - gap - leftW;
        int rx = m + leftW + gap;

        if (!winpe)
        {
            cChecks.SetBounds(m, y0, leftW, topH);
            btnCheck.SetBounds(14, 44, 130, 30);
            frChecks.SetBounds(14, 82, leftW - 28, topH - 82 - 14);
            cDest.SetBounds(rx, y0, rightW, 218);
            cActions.SetBounds(rx, y0 + 218 + gap, rightW, 104);
            cHelp.SetBounds(rx, y0 + 218 + gap + 104 + gap, rightW, Math.Max(80, topH - 218 - 104 - 2 * gap));
            int w = rightW;
            lblDest.SetBounds(16, 55, 70, 20);
            tbDest.SetBounds(86, 48, w - 86 - 14 - 104 - 8, 32);
            btnBrowse.SetBounds(w - 14 - 104, 48, 104, 32);
            lblName.SetBounds(16, 93, 70, 20);
            tbName.SetBounds(86, 86, (w - 86 - 14) / 2 - 20, 32);
            lblComp.SetBounds(tbName.Right + 10, 93, 56, 20);
            ddComp.SetBounds(lblComp.Right, 86, w - lblComp.Right - 14, 32);
            chkAdmin.SetBounds(16, 126, w - 32, 24);
            chkXml.SetBounds(16, 154, w - 32, 24);
            chkKeep.SetBounds(16, 182, w - 32, 24);
            // actions: two buttons side by side keeps the card compact
            int half = (rightW - 28 - 8) / 2;
            btnCmd.SetBounds(14, 46, half, 44);
            btnRun.SetBounds(14 + half + 8, 46, half, 44);
        }
        else
        {
            cDest.SetBounds(m, y0, leftW, Math.Min(topH, 320));
            cHelp.SetBounds(rx, y0, rightW, topH);
            int w = leftW;
            lblSrc.SetBounds(16, 55, 90, 20);
            ddSrc.SetBounds(110, 48, w - 110 - 14 - 100 - 8, 32);
            btnSrcRefresh.SetBounds(w - 14 - 100, 48, 100, 32);
            lblState.SetBounds(110, 84, w - 124, 36);
            lblDest.SetBounds(16, 131, 90, 20);
            tbDest.SetBounds(110, 124, w - 110 - 14 - 104 - 8, 32);
            btnBrowse.SetBounds(w - 14 - 104, 124, 104, 32);
            lblName.SetBounds(16, 169, 90, 20);
            tbName.SetBounds(110, 162, (w - 110 - 14) / 2 - 20, 32);
            lblComp.SetBounds(tbName.Right + 10, 169, 56, 20);
            ddComp.SetBounds(lblComp.Right, 162, w - lblComp.Right - 14, 32);
            btnCapture.SetBounds(14, 214, w - 28, 46);
        }
        help.SetBounds(16, 46, cHelp.Width - 32, Math.Max(20, cHelp.Height - 56));
        int y = y0 + topH + gap;
        bar.SetBounds(m, y, W - 2 * m, 10);
        lblStatus.SetBounds(m, y + 14, W - 2 * m, 20);
        frLog.SetBounds(m, y + 38, W - 2 * m, Math.Max(40, H - y - 38 - 12));
    }

    // ---- common -------------------------------------------------------------------------------------
    void GuessDestination()
    {
        if (tbDest.Text.Length > 0) return;
        Thread t = new Thread(delegate()
        {
            string guess = "";
            try
            {
                Dictionary<string, int> map = Disks.LetterMap();
                List<DiskInfo> list = Disks.List(null);
                foreach (DiskInfo d in list)
                    if (d.Bus == "USB" && !d.Blocked)
                        foreach (KeyValuePair<string, int> kv in map)
                            if (kv.Value == d.Number && guess.Length == 0) guess = kv.Key + "\\Images";
            }
            catch (Exception) { }
            if (guess.Length > 0) Ui.Post(delegate { if (tbDest.Text.Length == 0) tbDest.Text = guess; });
        });
        t.IsBackground = true;
        t.Start();
    }

    string CompressArg { get { return new string[] { "max", "fast", "none" }[Math.Max(0, ddComp.SelectedIndex)]; } }

    // Validates the destination folder; returns an error text or null.
    string CheckDest()
    {
        string dest = tbDest.Text.Trim().Trim('"').TrimEnd('\\');
        if (!System.Text.RegularExpressions.Regex.IsMatch(dest, @"^[A-Za-z]:(\\.*)?$")) return S.T("cap.err.dest");
        string sys = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").ToUpperInvariant();
        if (!winpe && dest.Substring(0, 2).ToUpperInvariant() == sys) return S.F("cap.err.sysdrive", sys);
        if (!Directory.Exists(dest.Substring(0, 3))) return S.F("cap.err.nodrive", dest.Substring(0, 2));
        return null;
    }

    void SetBusy(bool b)
    {
        busy = b;
        cChecks.Enabled = !b; cDest.Enabled = !b; cActions.Enabled = !b;
    }

    // ---- Windows mode -------------------------------------------------------------------------------
    CheckResult lastChecks;

    void RunChecks()
    {
        btnCheck.Enabled = false;
        btnRun.Enabled = false;
        lvChecks.Items.Clear();
        lblStatus.Text = S.T("cap.checking");
        bar.Marquee = true;
        Thread t = new Thread(delegate()
        {
            CheckResult r = null;
            string err = null;
            try { r = CaptureChecks.Run(); } catch (Exception ex) { err = ex.Message; }
            Ui.Post(delegate
            {
                bar.Marquee = false; bar.Value = 0;
                btnCheck.Enabled = true;
                lblStatus.Text = S.T("cap.ready");
                if (err != null) { Dlg.Msg(FindForm(), err, DlgKind.Error); return; }
                lastChecks = r;
                blockers = r.Blockers;
                chkAdmin.Enabled = r.BuiltinAdminEnabled;
                if (!r.BuiltinAdminEnabled) chkAdmin.Checked = false;
                FillChecks(r);
                btnRun.Enabled = blockers == 0 && !busy;
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    void FillChecks(CheckResult r)
    {
        lvChecks.BeginUpdate();
        lvChecks.Items.Clear();
        foreach (CheckItem c in r.Items)
        {
            string tag; Color col;
            switch (c.Kind)
            {
                case "ok": tag = S.T("cap.tag.ok"); col = Theme.Ok; break;
                case "warn": tag = S.T("cap.tag.warn"); col = Theme.Warn; break;
                case "bad": tag = S.T("cap.tag.bad"); col = Theme.Bad; break;
                default: tag = ""; col = Theme.Muted; break;
            }
            ListViewItem it = new ListViewItem(tag);
            it.ToolTipText = c.Text.Trim();
            it.ForeColor = col;
            ListViewItem.ListViewSubItem sub = it.SubItems.Add(c.Text);
            sub.ForeColor = c.Kind == "info" ? Theme.Muted : Theme.Text;
            lvChecks.Items.Add(it);
        }
        lvChecks.EndUpdate();
        lvChecks.Refit();
    }

    void MakeCmd()
    {
        string err = CheckDest();
        if (err != null) { Dlg.Msg(FindForm(), err, DlgKind.Warn); return; }
        try
        {
            string dest = tbDest.Text.Trim().Trim('"').TrimEnd('\\');
            Directory.CreateDirectory(dest);
            string p = CaptureCmd.Write(dest, WinPeCapture.SafeName(tbName.Text), CompressArg);
            Dlg.Msg(FindForm(), S.F("cap.cmd.done", p), DlgKind.Info);
        }
        catch (Exception ex) { Dlg.Msg(FindForm(), ex.Message, DlgKind.Error); }
    }

    void StartSysprep()
    {
        if (blockers > 0) { Dlg.Msg(FindForm(), S.T("cap.err.blockers"), DlgKind.Warn); return; }
        string err = CheckDest();
        if (err != null) { Dlg.Msg(FindForm(), err, DlgKind.Warn); return; }
        string dest = tbDest.Text.Trim().Trim('"').TrimEnd('\\');
        string name = WinPeCapture.SafeName(tbName.Text);
        string cmdPath;
        try
        {
            Directory.CreateDirectory(dest);
            cmdPath = CaptureCmd.Write(dest, name, CompressArg);
        }
        catch (Exception ex) { Dlg.Msg(FindForm(), ex.Message, DlgKind.Error); return; }
        log.AppendText(S.F("cap.log.cmd", cmdPath) + "\r\n");
        if (!Dlg.ConfirmTyped(FindForm(), S.T("cap.confirm.title"), S.T("cap.confirm.body"), "SYSPREP", S.T("cap.confirm.ok"))) return;

        bool keepAdmin = chkAdmin.Checked, useXml = chkXml.Checked, keepLnk = chkKeep.Checked;
        SetBusy(true);
        btnRun.Enabled = false;
        Thread t = new Thread(delegate()
        {
            string error = null;
            try
            {
                if (keepAdmin) { Unattend.KeepBuiltinAdmin(); prog.Log(S.T("cap.log.admin")); }
                if (keepLnk)
                {
                    prog.Status(S.T("cap.log.keep.run"));
                    int n = ProfileKeeper.Backup(prog);
                    prog.Log(S.F("cap.log.keep", n));
                }
                string xml = null;
                if (useXml) { xml = Unattend.Write(); prog.Log(S.F("cap.log.xml", xml)); }
                SysprepRunner.Run(xml, prog, delegate(List<string> bad)
                {
                    bool yes = false;
                    FindForm().Invoke(new Action(delegate { yes = Dlg.Ask(FindForm(), S.F("cap.apps.ask", string.Join("\r\n", bad.ToArray())), DlgKind.Warn); }));
                    return yes;
                });
            }
            catch (Exception ex) { error = ex.Message; }
            Ui.Post(delegate
            {
                SetBusy(false);
                bar.Marquee = false; bar.Value = 0;
                btnRun.Enabled = blockers == 0;
                lblStatus.Text = S.T("cap.ready");
                if (error != null) { log.AppendText(S.T("inst.log.error") + " " + error + "\r\n"); Dlg.Msg(FindForm(), error, DlgKind.Error); }
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    // ---- WinPE mode ---------------------------------------------------------------------------------
    void LoadVolumes()
    {
        btnSrcRefresh.Enabled = false;
        Thread t = new Thread(delegate()
        {
            List<WinVolume> v = null;
            try { v = WinPeCapture.FindWindows(); } catch (Exception) { v = new List<WinVolume>(); }
            Ui.Post(delegate
            {
                btnSrcRefresh.Enabled = true;
                volumes = v;
                List<string> titles = new List<string>();
                foreach (WinVolume x in v) titles.Add(x.Title);
                ddSrc.SetItems(titles);
                if (v.Count > 0) ddSrc.SelectedIndex = 0;
                else { lblState.Tone = Theme.Bad; lblState.Text = S.T("cap.nowindows"); }
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    void ShowState()
    {
        int i = ddSrc.SelectedIndex;
        if (i < 0 || i >= volumes.Count) return;
        string drive = volumes[i].Drive;
        lblState.Tone = null; lblState.Text = S.T("cap.state.reading");
        Thread t = new Thread(delegate()
        {
            int st = WinPeCapture.GeneralizationState(drive);
            Ui.Post(delegate
            {
                if (ddSrc.SelectedIndex != i) return;
                if (st == 7) { lblState.Tone = Theme.Ok; lblState.Text = S.T("cap.state.ok"); }
                else if (st >= 0) { lblState.Tone = Theme.Warn; lblState.Text = S.F("cap.state.no", st); }
                else { lblState.Tone = null; lblState.Text = S.T("cap.state.unk"); }
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    void StartWinPeCapture()
    {
        int i = ddSrc.SelectedIndex;
        if (i < 0 || i >= volumes.Count) { Dlg.Msg(FindForm(), S.T("cap.err.nosrc"), DlgKind.Warn); return; }
        string err = CheckDest();
        if (err != null) { Dlg.Msg(FindForm(), err, DlgKind.Warn); return; }
        WinVolume v = volumes[i];
        string dest = tbDest.Text.Trim().Trim('"').TrimEnd('\\');
        string dd = dest.Substring(0, 2).ToUpperInvariant();
        if (dd == v.Drive.ToUpperInvariant()) { Dlg.Msg(FindForm(), S.F("cap.err.samedrive", v.Drive), DlgKind.Warn); return; }
        try
        {
            DriveInfo di = new DriveInfo(dd);
            if (di.AvailableFreeSpace < v.Used / 2 && !Dlg.Ask(FindForm(), S.F("cap.warn.space", dd, Fmt.Size(di.AvailableFreeSpace), v.Drive, Fmt.Size(v.Used)), DlgKind.Warn)) return;
        }
        catch (Exception) { }
        string name = WinPeCapture.SafeName(tbName.Text);
        string comp = CompressArg;
        SetBusy(true);
        log.Clear();
        Thread t = new Thread(delegate()
        {
            string error = null, img = null;
            try { img = WinPeCapture.Capture(v.Drive, dest, name, comp, prog); }
            catch (Exception ex) { error = ex.Message; }
            Ui.Post(delegate
            {
                SetBusy(false);
                bar.Marquee = false;
                if (error != null) { bar.Value = 0; log.AppendText(S.T("inst.log.error") + " " + error + "\r\n"); lblStatus.Text = S.T("cap.failed"); Dlg.Msg(FindForm(), error, DlgKind.Error); }
                else { bar.Value = 100; lblStatus.Text = S.T("cap.done"); Dlg.Msg(FindForm(), S.F("cap.done.msg", img), DlgKind.Info); }
            });
        });
        t.IsBackground = true;
        t.Start();
    }
}
