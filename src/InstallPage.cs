// "Install" tab: choose an image, a disk and options, then install Windows.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

class InstallPage : Page
{
    readonly Card cImage = new Card(), cDisk = new Card(), cParams = new Card();
    readonly PillBox tbFolder = new PillBox(), tbDrivers = new PillBox();
    readonly PillButton btnFile = new PillButton(), btnFolder = new PillButton(), btnSearch = new PillButton();
    readonly PillButton btnRefresh = new PillButton(), btnDrivers = new PillButton(), btnInstall = new PillButton();
    readonly ThemedList lvImages = new ThemedList(), lvDisks = new ThemedList();
    readonly Frame frImages = new Frame(), frDisks = new Frame(), frLog = new Frame();
    readonly DropPill ddEdition = new DropPill();
    readonly Txt lblEdition = new Txt(), lblDiskHint = new Txt(), lblFw = new Txt(), lblGb = new Txt(), lblDrivers = new Txt();
    readonly Txt lblSummary = new Txt(), lblStatus = new Txt();
    readonly CheckPill rbGpt = new CheckPill(), rbMbr = new CheckPill(), chkRecovery = new CheckPill();
    readonly CheckPill rbAll = new CheckPill(), rbCustom = new CheckPill(), chkData = new CheckPill(), chkWin = new CheckPill();
    readonly PillButton btnWin = new PillButton();
    readonly Stepper stSize = new Stepper();
    readonly GradientBar bar = new GradientBar();
    readonly LogBox log = new LogBox();
    readonly UiProgress prog;

    ImageSource src;
    List<Edition> editions = new List<Edition>();
    List<DiskInfo> disks = new List<DiskInfo>();
    bool busy, openingImage;

    public bool Busy { get { return busy; } }

    public InstallPage()
    {
        prog = new UiProgress(bar, lblStatus, log);

        cImage.Step = "1"; cDisk.Step = "2"; cParams.Step = "3";
        Controls.Add(cImage); Controls.Add(cDisk); Controls.Add(cParams);

        // --- card 1: image ---
        tbFolder.TextChanged2 += delegate { };
        btnFile.Kind = BtnKind.Ghost; btnFolder.Kind = BtnKind.Ghost; btnSearch.Kind = BtnKind.Ghost; btnRefresh.Kind = BtnKind.Ghost; btnDrivers.Kind = BtnKind.Ghost;
        cImage.Controls.Add(btnFile); cImage.Controls.Add(tbFolder); cImage.Controls.Add(btnFolder); cImage.Controls.Add(btnSearch);
        lvImages.Columns.Add("", 100); lvImages.Columns.Add("", 100);
        lvImages.SetColumnPercents(82, 18);
        lvImages.HeaderStyle = ColumnHeaderStyle.None;
        lvImages.Dock = DockStyle.Fill;
        frImages.Controls.Add(lvImages);
        cImage.Controls.Add(frImages);
        cImage.Controls.Add(lblEdition); cImage.Controls.Add(ddEdition);

        // --- card 2: disk ---
        for (int i = 0; i < 7; i++) lvDisks.Columns.Add("", 80);
        lvDisks.SetColumnPercents(5, 26, 12, 8, 9, 16, 24);
        lvDisks.Dock = DockStyle.Fill;
        frDisks.Controls.Add(lvDisks);
        cDisk.Controls.Add(frDisks); cDisk.Controls.Add(btnRefresh); cDisk.Controls.Add(lblDiskHint);

        // --- card 3: options ---
        rbGpt.Radio = true; rbGpt.Group = "mode"; rbMbr.Radio = true; rbMbr.Group = "mode";
        rbAll.Radio = true; rbAll.Group = "size"; rbCustom.Radio = true; rbCustom.Group = "size";
        rbAll.Checked = true;
        bool bios = Env.Firmware() == "BIOS";
        if (bios) rbMbr.Checked = true; else rbGpt.Checked = true;
        chkRecovery.Checked = true; chkData.Checked = true;
        stSize.Min = 20; stSize.Max = 100000; stSize.Step = 10; stSize.Value = 60;
        lblFw.IsMuted = true; lblDiskHint.IsMuted = true; lblGb.IsMuted = true; lblDrivers.IsMuted = true; lblEdition.IsMuted = true;
        cParams.Controls.Add(rbGpt); cParams.Controls.Add(rbMbr); cParams.Controls.Add(lblFw); cParams.Controls.Add(chkRecovery);
        cParams.Controls.Add(rbAll); cParams.Controls.Add(rbCustom); cParams.Controls.Add(stSize); cParams.Controls.Add(lblGb); cParams.Controls.Add(chkData);
        cParams.Controls.Add(lblDrivers); cParams.Controls.Add(tbDrivers); cParams.Controls.Add(btnDrivers);
        btnWin.Kind = BtnKind.Ghost; btnWin.Enabled = false;
        cParams.Controls.Add(chkWin); cParams.Controls.Add(btnWin);

        // --- action area ---
        btnInstall.Kind = BtnKind.Danger;
        btnInstall.Font = new Font("Segoe UI Semibold", 11f);
        btnInstall.Enabled = false;
        lblSummary.Font = Theme.Bold;
        lblStatus.IsMuted = true;
        frLog.Controls.Add(log);
        log.Dock = DockStyle.Fill;
        Controls.Add(lblSummary); Controls.Add(btnInstall); Controls.Add(bar); Controls.Add(lblStatus); Controls.Add(frLog);

        // --- events ---
        btnFile.Click += delegate { PickFiles(); };
        btnFolder.Click += delegate
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = S.T("inst.pickfolder");
                if (d.ShowDialog(FindForm()) == DialogResult.OK) tbFolder.Text = d.SelectedPath;
            }
        };
        btnDrivers.Click += delegate
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = S.T("inst.pickdrivers");
                if (d.ShowDialog(FindForm()) == DialogResult.OK) tbDrivers.Text = d.SelectedPath;
            }
        };
        btnWin.Click += delegate { EditWinSettings(); };
        chkWin.CheckedChanged += delegate { btnWin.Enabled = chkWin.Checked && !busy; UpdateSummary(); };
        btnSearch.Click += delegate { SearchIsos(); };
        btnRefresh.Click += delegate { RefreshDisks(); };
        lvImages.SelectedIndexChanged += delegate
        {
            if (lvImages.SelectedItems.Count == 1 && !busy && !openingImage) OpenImage((string)lvImages.SelectedItems[0].Tag);
        };
        lvDisks.SelectedIndexChanged += delegate { OnDiskSelected(); };
        ddEdition.SelectedIndexChanged += delegate { UpdateSummary(); };
        rbGpt.CheckedChanged += delegate { UpdateSummary(); };
        rbMbr.CheckedChanged += delegate { UpdateSummary(); };
        rbAll.CheckedChanged += delegate { UpdateSummary(); };
        rbCustom.CheckedChanged += delegate { UpdateSummary(); };
        stSize.ValueChanged += delegate { UpdateSummary(); };
        chkRecovery.CheckedChanged += delegate { UpdateSummary(); };
        chkData.CheckedChanged += delegate { UpdateSummary(); };
        btnInstall.Click += delegate { StartInstall(); };

        ApplyLanguage();
        RefreshDisks();
    }

    // ---- texts --------------------------------------------------------------------------------------
    public void ApplyLanguage()
    {
        cImage.Title = S.T("inst.card1"); cDisk.Title = S.T("inst.card2"); cParams.Title = S.T("inst.card3");
        btnFile.Text = S.T("inst.btn.file"); btnFolder.Text = S.T("inst.btn.folder"); btnSearch.Text = S.T("inst.btn.search");
        tbFolder.Placeholder = S.T("inst.folder.ph"); tbDrivers.Placeholder = S.T("inst.drivers.ph");
        lblEdition.Text = S.T("inst.edition"); ddEdition.Placeholder = S.T("inst.edition.ph");
        btnRefresh.Text = S.T("inst.btn.refresh");
        string[] heads = new string[] { "#", S.T("disk.col.model"), S.T("disk.col.size"), S.T("disk.col.bus"), S.T("disk.col.style"), S.T("disk.col.volumes"), S.T("disk.col.status") };
        for (int i = 0; i < 7; i++) lvDisks.Columns[i].Text = heads[i];
        rbGpt.Text = "GPT + UEFI"; rbMbr.Text = "MBR + BIOS (Legacy)";
        lblFw.Text = S.T("inst.thispc") + " " + Env.Firmware();
        chkRecovery.Text = S.T("inst.recovery");
        rbAll.Text = S.T("inst.size.all"); rbCustom.Text = S.T("inst.size.custom");
        lblGb.Text = S.T("u.gb"); chkData.Text = S.T("inst.data");
        lblDrivers.Text = S.T("inst.drivers");
        btnDrivers.Text = S.T("inst.btn.browse");
        chkWin.Text = S.T("inst.win.use"); btnWin.Text = S.T("win.btn");
        btnInstall.Text = S.T("inst.btn.install");
        if (!busy) lblStatus.Text = S.T("inst.ready");
        lblDiskHint.Text = S.T("inst.diskhint");
        if (editions.Count > 0) { List<string> t = new List<string>(); foreach (Edition e in editions) t.Add(e.Title); ddEdition.UpdateItems(t); }
        FillDisks();
        FillImageSizes();
        UpdateSummary();
        DoLayout();
    }

    // ---- layout -------------------------------------------------------------------------------------
    protected override void OnResize(EventArgs eventargs) { base.OnResize(eventargs); DoLayout(); }

    void DoLayout()
    {
        int W = ClientSize.Width, H = ClientSize.Height;
        if (W < 300 || H < 300) return;
        int m = 16, gap = 12, colW = (W - 2 * m - gap) * 45 / 100, colW2 = W - 2 * m - gap - colW, y = 4;
        int rowA = 252, rowB = 190;
        cImage.SetBounds(m, y, colW, rowA);
        cDisk.SetBounds(m + colW + gap, y, colW2, rowA);
        y += rowA + gap;
        cParams.SetBounds(m, y, W - 2 * m, rowB);
        y += rowB + gap;

        // card 1
        int cw = colW;
        btnFile.SetBounds(14, 46, 112, 32);
        btnSearch.SetBounds(cw - 14 - 96, 46, 96, 32);
        btnFolder.SetBounds(cw - 14 - 96 - 8 - 84, 46, 84, 32);
        tbFolder.SetBounds(134, 46, Math.Max(40, cw - 14 - 96 - 8 - 84 - 8 - 134), 32);
        frImages.SetBounds(14, 88, cw - 28, 106);
        lblEdition.SetBounds(16, 207, 80, 20);
        ddEdition.SetBounds(92, 200, cw - 14 - 92, 32);

        // card 2
        frDisks.SetBounds(14, 46, colW2 - 28, 148);
        btnRefresh.SetBounds(14, 204, 110, 30);
        lblDiskHint.SetBounds(134, 210, colW2 - 148, 20);

        // card 3
        int pw = W - 2 * m;
        rbGpt.SetBounds(16, 46, 124, 24);
        rbMbr.SetBounds(150, 46, 190, 24);
        lblFw.SetBounds(350, 49, 180, 20);
        chkRecovery.SetBounds(Math.Max(540, pw - 330), 46, 310, 24);
        rbAll.SetBounds(16, 82, 120, 24);
        rbCustom.SetBounds(142, 82, 170, 24);
        stSize.SetBounds(322, 78, 132, 32);
        lblGb.SetBounds(462, 85, 40, 20);
        chkData.SetBounds(510, 82, 330, 24);
        lblDrivers.SetBounds(16, 121, 120, 20);
        btnDrivers.SetBounds(pw - 14 - 110, 114, 110, 32);
        tbDrivers.SetBounds(140, 114, pw - 14 - 110 - 8 - 140, 32);
        chkWin.SetBounds(16, 154, Math.Max(200, pw - 14 - 190 - 8 - 16), 24);
        btnWin.SetBounds(pw - 14 - 190, 150, 190, 32);

        // action row
        lblSummary.SetBounds(m, y, W - 2 * m - 270, 46);
        btnInstall.SetBounds(W - m - 258, y, 258, 46);
        y += 54;
        bar.SetBounds(m, y, W - 2 * m, 10);
        y += 14;
        lblStatus.SetBounds(m, y, W - 2 * m, 20);
        y += 24;
        frLog.SetBounds(m, y, W - 2 * m, Math.Max(40, H - y - 12));
    }

    // ---- images -------------------------------------------------------------------------------------
    void PickFiles()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = S.T("inst.pickimage");
            d.Filter = S.T("inst.filter") + "|*.iso;*.wim;*.esd|*.*|*.*";
            d.Multiselect = true;
            try { d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); } catch (Exception) { }
            if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
            ListViewItem first = null;
            foreach (string f in d.FileNames)
            {
                ListViewItem it = FindImageItem(f);
                if (it == null) it = AddImageItem(f);
                if (first == null) first = it;
            }
            if (first != null) { first.Selected = true; first.EnsureVisible(); }
        }
    }

    ListViewItem FindImageItem(string path)
    {
        foreach (ListViewItem it in lvImages.Items) if (string.Equals((string)it.Tag, path, StringComparison.OrdinalIgnoreCase)) return it;
        return null;
    }

    ListViewItem AddImageItem(string path)
    {
        ListViewItem it = new ListViewItem(path);
        it.Tag = path;
        long len = 0;
        try { len = new FileInfo(path).Length; } catch (Exception) { }
        it.SubItems.Add(len > 0 ? Fmt.Size(len) : "");
        it.ToolTipText = path;
        lvImages.Items.Add(it);
        return it;
    }

    void FillImageSizes()
    {
        foreach (ListViewItem it in lvImages.Items)
        {
            long len = 0;
            try { len = new FileInfo((string)it.Tag).Length; } catch (Exception) { }
            if (it.SubItems.Count > 1) it.SubItems[1].Text = len > 0 ? Fmt.Size(len) : "";
        }
    }

    void SearchIsos()
    {
        string where = tbFolder.Text.Trim().Trim('"');
        btnSearch.Enabled = false;
        lblStatus.Text = S.T("inst.searching");
        Thread t = new Thread(delegate()
        {
            List<string> found = null;
            string error = null;
            try { found = Images.FindIsos(where, delegate(string r) { Ui.Post(delegate { lblStatus.Text = S.T("inst.searching") + " " + r; }); }); }
            catch (Exception ex) { error = ex.Message; }
            Ui.Post(delegate
            {
                btnSearch.Enabled = true;
                if (error != null) { Dlg.Msg(FindForm(), error, DlgKind.Error); return; }
                foreach (string f in found) if (FindImageItem(f) == null) AddImageItem(f);
                lblStatus.Text = S.F("inst.found", found.Count);
                if (found.Count == 0) Dlg.Msg(FindForm(), S.T("inst.noiso"), DlgKind.Warn);
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    void OpenImage(string path)
    {
        openingImage = true;
        SetControlsEnabled(false);
        ddEdition.SetItems(new string[0]);
        lblStatus.Text = S.T("inst.opening");
        bar.Marquee = true;
        Thread t = new Thread(delegate()
        {
            ImageSource ns = null;
            List<Edition> eds = null;
            string err = null;
            try
            {
                ns = Images.Open(path, prog);
                eds = Images.ListEditions(ns.ImageFile);
            }
            catch (Exception ex) { err = ex.Message; if (ns != null) ns.Dispose(); ns = null; }
            Ui.Post(delegate
            {
                openingImage = false;
                bar.Marquee = false; bar.Value = 0;
                SetControlsEnabled(true);
                if (src != null) { src.Dispose(); src = null; }
                editions = new List<Edition>();
                if (err != null)
                {
                    lvImages.SelectedItems.Clear();
                    lblStatus.Text = S.T("inst.openfail");
                    UpdateSummary();
                    Dlg.Msg(FindForm(), err, DlgKind.Error);
                    return;
                }
                src = ns;
                editions = eds;
                List<string> titles = new List<string>();
                foreach (Edition e in eds) titles.Add(e.Title);
                ddEdition.SetItems(titles);
                ddEdition.SelectedIndex = eds.IndexOf(Images.PreferPro(eds));
                lblStatus.Text = S.F("inst.opened", src.ImageFile);
                RefreshDisks();
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    // ---- disks --------------------------------------------------------------------------------------
    List<string> BlockedDrives()
    {
        List<string> l = new List<string>();
        if (src != null) { l.Add(Paths.Drive(src.SourcePath)); if (src.ImageFile != null) l.Add(Paths.Drive(src.ImageFile)); }
        l.Add(Paths.Drive(Application.ExecutablePath));
        return l;
    }

    public void RefreshDisks()
    {
        btnRefresh.Enabled = false;
        List<string> blocked = BlockedDrives();
        Thread t = new Thread(delegate()
        {
            List<DiskInfo> list = null;
            string err = null;
            try { list = Disks.List(blocked); } catch (Exception ex) { err = ex.Message; }
            Ui.Post(delegate
            {
                btnRefresh.Enabled = !busy;
                if (err != null) { Dlg.Msg(FindForm(), S.T("disk.err") + "\r\n" + err, DlgKind.Error); return; }
                int prev = SelectedDisk == null ? -1 : SelectedDisk.Number;
                disks = list;
                FillDisks();
                if (prev >= 0)
                    foreach (ListViewItem it in lvDisks.Items)
                    {
                        DiskInfo d = (DiskInfo)it.Tag;
                        if (d.Number == prev && !d.Blocked) it.Selected = true;
                    }
                UpdateSummary();
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    void FillDisks()
    {
        lvDisks.BeginUpdate();
        lvDisks.Items.Clear();
        foreach (DiskInfo d in disks)
        {
            ListViewItem it = new ListViewItem(d.Number.ToString());
            it.SubItems.Add(d.Model);
            it.SubItems.Add(Fmt.Size(d.Size));
            it.SubItems.Add(d.Bus);
            it.SubItems.Add(d.Style);
            it.SubItems.Add(d.VolumesText);
            it.SubItems.Add(d.BlockReason);
            it.Tag = d;
            it.ToolTipText = d.Model + "  " + d.VolumesText;
            if (d.Blocked)
            {
                it.ForeColor = Theme.Muted;
                it.SubItems[6].ForeColor = Theme.Bad;
            }
            else if (d.Bus == "USB") it.SubItems[6].ForeColor = Theme.Warn;
            lvDisks.Items.Add(it);
        }
        lvDisks.EndUpdate();
        lvDisks.Refit();
    }

    DiskInfo SelectedDisk
    {
        get { return lvDisks.SelectedItems.Count == 1 ? (DiskInfo)lvDisks.SelectedItems[0].Tag : null; }
    }

    void OnDiskSelected()
    {
        DiskInfo d = SelectedDisk;
        if (d != null && d.Blocked)
        {
            lvDisks.SelectedItems[0].Selected = false;
            lblDiskHint.Tone = Theme.Bad;
            lblDiskHint.Text = S.T("inst.diskblocked");
        }
        else
        {
            lblDiskHint.Tone = null;
            lblDiskHint.Text = S.T("inst.diskhint");
            if (d != null)
            {
                if (d.Style == "GPT") rbGpt.Checked = true;
                else if (d.Style == "MBR") rbMbr.Checked = true;
            }
        }
        UpdateSummary();
    }

    // ---- state --------------------------------------------------------------------------------------
    int WinGb { get { return rbAll.Checked ? 0 : stSize.Value; } }

    InstallOptions Options()
    {
        InstallOptions o = new InstallOptions();
        o.ImageFile = src == null ? null : src.ImageFile;
        o.Index = ddEdition.SelectedIndex >= 0 && ddEdition.SelectedIndex < editions.Count ? editions[ddEdition.SelectedIndex].Index : 0;
        o.Disk = SelectedDisk;
        o.Gpt = rbGpt.Checked;
        o.Recovery = chkRecovery.Checked;
        o.WinGb = WinGb;
        o.DataPartition = chkData.Checked && WinGb > 0;
        o.Drivers = tbDrivers.Text.Trim().Trim('"');
        o.Win = chkWin.Checked ? WinProfile.Current : null;
        return o;
    }

    // Opens the settings dialog (passwords included); true when the settings were accepted.
    bool EditWinSettings()
    {
        WinSettings w = WinSettingsDialog.Edit(FindForm(), WinProfile.Current, true);
        if (w == null) return false;
        WinProfile.Current = w;
        return true;
    }

    void UpdateSummary()
    {
        stSize.Enabled = rbCustom.Checked && !busy;
        chkData.Enabled = rbCustom.Checked && !busy;
        InstallOptions o = Options();
        bool ready = o.ImageFile != null && o.Disk != null && ddEdition.SelectedIndex >= 0;
        btnInstall.Enabled = ready && !busy;
        if (ready)
        {
            string size = o.WinGb > 0 ? S.F("inst.sum.size", o.WinGb) : S.T("inst.sum.all");
            lblSummary.Tone = Theme.Bad;
            lblSummary.Text = editions[ddEdition.SelectedIndex].Name + "  →  " + S.F("inst.sum.disk", o.Disk.Number, o.Disk.Model, Fmt.Size(o.Disk.Size)) + ", " + o.ModeText + ", " + size + ".\r\n" + S.T("inst.sum.erase");
        }
        else
        {
            lblSummary.Tone = null;
            List<string> need = new List<string>();
            if (o.ImageFile == null) need.Add(S.T("inst.need.image")); else if (ddEdition.SelectedIndex < 0) need.Add(S.T("inst.need.edition"));
            if (o.Disk == null) need.Add(S.T("inst.need.disk"));
            lblSummary.Text = S.T("inst.need") + " " + string.Join(", ", need.ToArray());
        }
    }

    void SetControlsEnabled(bool on)
    {
        cImage.Enabled = on; cDisk.Enabled = on; cParams.Enabled = on;
        btnWin.Enabled = on && chkWin.Checked;
        if (!on) btnInstall.Enabled = false; else UpdateSummary();
    }

    // ---- installing ---------------------------------------------------------------------------------
    void StartInstall()
    {
        InstallOptions o = Options();
        if (o.Win != null && o.Win.Validate(true) != null)
        {
            // passwords are never remembered, so they are asked for before every installation
            if (!EditWinSettings()) return;
            o.Win = WinProfile.Current;
        }
        string err = Installer.Validate(o);
        if (err != null) { Dlg.Msg(FindForm(), err, DlgKind.Warn); return; }
        if (!o.Gpt && o.Disk.Size > 2199023255552L && !Dlg.Ask(FindForm(), S.T("inst.warn.mbr2tb"), DlgKind.Warn)) return;
        if (o.WinGb > 0 && o.WinGb < 40 && !Dlg.Ask(FindForm(), S.F("inst.warn.small", o.WinGb), DlgKind.Warn)) return;

        string mode = o.ModeText + (o.WinGb > 0 ? ", " + S.F("inst.sum.size", o.WinGb) : "");
        if (o.DataPartition && Installer.LeftoverGb(o) > 0) mode += " + " + S.F("inst.sum.data", Installer.LeftoverGb(o));
        else o.DataPartition = false;
        string body = S.T("inst.confirm.warn") + "\r\n\r\n" +
            S.F("inst.confirm.disk", o.Disk.Number, o.Disk.Model) + "\r\n" +
            S.F("inst.confirm.size", Fmt.Size(o.Disk.Size), o.Disk.Bus) + "\r\n" +
            S.T("inst.confirm.scheme") + " " + mode + "\r\n" +
            S.T("inst.confirm.edition") + " " + editions[ddEdition.SelectedIndex].Name;
        if (o.Win != null) body += "\r\n" + S.T("inst.confirm.win");
        if (!Dlg.ConfirmTyped(FindForm(), S.T("inst.confirm.title"), body, o.Disk.Number.ToString(), S.T("inst.confirm.ok"))) return;

        busy = true;
        SetControlsEnabled(false);
        log.Clear();
        bar.Marquee = true;
        Thread t = new Thread(delegate()
        {
            string error = null;
            try { Installer.Run(o, prog); }
            catch (Exception ex) { error = ex.Message; }
            Ui.Post(delegate
            {
                busy = false;
                bar.Marquee = false;
                if (error != null)
                {
                    bar.Value = 0;
                    lblStatus.Tone = Theme.Bad;
                    lblStatus.Text = S.T("inst.failed");
                    log.AppendText(S.T("inst.log.error") + " " + error + "\r\n");
                    SetControlsEnabled(true);
                    RefreshDisks();
                    Dlg.Msg(FindForm(), error, DlgKind.Error);
                }
                else
                {
                    bar.Value = 100;
                    WinProfile.Current.AdminPassword = ""; WinProfile.Current.UserPassword = "";   // do not keep passwords in memory
                    SetControlsEnabled(true);
                    RefreshDisks();
                    Dlg.Msg(FindForm(), S.F("inst.done.msg", o.Disk.Number), DlgKind.Info);
                }
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    public void Release()
    {
        if (src != null) { src.Dispose(); src = null; }
    }
}
