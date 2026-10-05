// Dialog with the Windows deployment settings (answer file and first-logon script).
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

class WinSettingsDialog : DlgForm
{
    readonly bool secrets;
    readonly WinSettings baseSettings;
    WinSettings result;

    PillBox tbOrg, tbOwner, tbTz, tbInput, tbUi, tbSys, tbUserLoc, tbResX, tbResY;
    PillBox tbAdmName, tbAdmPw, tbUser, tbUserDesc, tbUserPw, tbLogons, tbDns, tbAdapter;
    PillBox tbRdp, tbKms, tbGvlk;
    CheckPill chkAdmin, chkLogon, chkIcmp, chkIpv6, chkTele, chkHib, chkIcons;

    // Returns the edited settings, or null when the dialog was cancelled. secrets = ask for the passwords too.
    public static WinSettings Edit(IWin32Window owner, WinSettings cur, bool secrets)
    {
        using (WinSettingsDialog d = new WinSettingsDialog(cur, secrets))
        {
            DialogResult r = owner == null ? d.ShowDialog() : d.ShowDialog(owner);
            return r == DialogResult.OK ? d.result : null;
        }
    }

    WinSettingsDialog(WinSettings cur, bool secrets) : base(S.T("win.title"))
    {
        this.secrets = secrets;
        baseSettings = cur;
        ClientSize = new Size(800, 604);

        int lx = 22, rx = 412, lw = 150, bw = 210, y0 = 20, step = 38;

        // left column: identity, language, screen
        tbOrg = Field("win.org", lx, y0, lw, bw, false);
        tbOwner = Field("win.owner", lx, y0 + step, lw, bw, false);
        tbTz = Field("win.tz", lx, y0 + 2 * step, lw, bw, false);
        tbInput = Field("win.input", lx, y0 + 3 * step, lw, bw, false);
        tbUi = Field("win.ui", lx, y0 + 4 * step, lw, bw, false);
        tbSys = Field("win.sysloc", lx, y0 + 5 * step, lw, bw, false);
        tbUserLoc = Field("win.userloc", lx, y0 + 6 * step, lw, bw, false);
        tbDns = Field("win.dns", lx, y0 + 7 * step, lw, bw, false);
        tbAdapter = Field("win.adapter", lx, y0 + 8 * step, lw, bw, false);
        Txt lres = Label("win.res", lx, y0 + 9 * step, lw);
        tbResX = new PillBox(); tbResX.SetBounds(lx + lw, y0 + 9 * step, 90, 32); Controls.Add(tbResX);
        Txt times = new Txt("×"); times.SetBounds(lx + lw + 96, y0 + 9 * step + 6, 18, 20); Controls.Add(times);
        tbResY = new PillBox(); tbResY.SetBounds(lx + lw + 116, y0 + 9 * step, 90, 32); Controls.Add(tbResY);

        // right column: accounts
        chkAdmin = Check("win.admin.enable", rx, y0 + 4, 370);
        tbAdmName = Field("win.admin.name", rx, y0 + step, lw, bw, false);
        tbAdmPw = Field("win.admin.pw", rx, y0 + 2 * step, lw, bw, true);
        tbUser = Field("win.user.name", rx, y0 + 3 * step, lw, bw, false);
        tbUserDesc = Field("win.user.desc", rx, y0 + 4 * step, lw, bw, false);
        tbUserPw = Field("win.user.pw", rx, y0 + 5 * step, lw, bw, true);
        chkLogon = Check("win.autologon", rx, y0 + 6 * step + 4, 250);
        tbLogons = new PillBox(); tbLogons.SetBounds(rx + 280, y0 + 6 * step, 70, 32); Controls.Add(tbLogons);

        tbRdp = Field("win.rdp", rx, y0 + 7 * step, lw, 90, false);
        tbKms = Field("win.kms", rx, y0 + 8 * step, lw, bw, false);
        tbGvlk = Field("win.gvlk", rx, y0 + 9 * step, lw, bw, false);

        // options
        int oy = y0 + 10 * step + 8;
        chkIcmp = Check("win.icmp", lx, oy, 370);
        chkIpv6 = Check("win.ipv6", rx, oy, 370);
        chkTele = Check("win.telemetry", lx, oy + 28, 370);
        chkHib = Check("win.hibernate", rx, oy + 28, 370);
        chkIcons = Check("win.icons", lx, oy + 56, 370);

        Txt note = new Txt(S.T(secrets ? "win.note.install" : "win.note.capture"));
        note.IsMuted = true; note.Font = Theme.Small;
        note.SetBounds(lx, oy + 92, 760, 36);
        Controls.Add(note);

        PillButton save = new PillButton(), load = new PillButton(), ok = new PillButton(), no = new PillButton();
        save.Kind = BtnKind.Ghost; load.Kind = BtnKind.Ghost; no.Kind = BtnKind.Ghost;
        save.Text = S.T("win.save"); load.Text = S.T("win.load"); ok.Text = S.T("dlg.ok"); no.Text = S.T("dlg.cancel");
        save.SetBounds(lx, 604 - 52, 130, 34);
        load.SetBounds(lx + 138, 604 - 52, 130, 34);
        ok.SetBounds(800 - 22 - 110 - 8 - 110, 604 - 52, 110, 34);
        no.SetBounds(800 - 22 - 110, 604 - 52, 110, 34);
        Controls.Add(save); Controls.Add(load); Controls.Add(ok); Controls.Add(no);

        tbAdmPw.Enabled = secrets; tbUserPw.Enabled = secrets;
        if (!secrets) { tbAdmPw.Placeholder = S.T("win.pw.later"); tbUserPw.Placeholder = S.T("win.pw.later"); }

        Fill(cur);

        save.Click += delegate { SaveProfile(); };
        load.Click += delegate { LoadProfile(); };
        no.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        ok.Click += delegate
        {
            string err;
            WinSettings w = Collect(out err);
            if (w != null) err = w.Validate(secrets) == null ? null : S.T(w.Validate(secrets));
            if (err != null) { Dlg.Msg(this, err, DlgKind.Warn); return; }
            result = w;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    Txt Label(string key, int x, int y, int w)
    {
        Txt l = new Txt(S.T(key));
        l.IsMuted = true;
        l.SetBounds(x, y + 7, w, 20);
        Controls.Add(l);
        return l;
    }

    PillBox Field(string key, int x, int y, int lw, int bw, bool pw)
    {
        Label(key, x, y, lw - 6);
        PillBox b = new PillBox();
        b.SetBounds(x + lw, y, bw, 32);
        if (pw) b.Edit.UseSystemPasswordChar = true;
        Controls.Add(b);
        return b;
    }

    CheckPill Check(string key, int x, int y, int w)
    {
        CheckPill c = new CheckPill();
        c.Text = S.T(key);
        c.SetBounds(x, y, w, 24);
        Controls.Add(c);
        return c;
    }

    void Fill(WinSettings w)
    {
        tbOrg.Text = w.Organization; tbOwner.Text = w.Owner; tbTz.Text = w.TimeZone; tbInput.Text = w.InputLocale;
        tbUi.Text = w.UiLanguage; tbSys.Text = w.SystemLocale; tbUserLoc.Text = w.UserLocale;
        tbDns.Text = w.Dns; tbAdapter.Text = w.Adapter;
        tbResX.Text = w.ResX.ToString(CultureInfo.InvariantCulture); tbResY.Text = w.ResY.ToString(CultureInfo.InvariantCulture);
        chkAdmin.Checked = w.EnableAdmin; tbAdmName.Text = w.AdminName;
        tbUser.Text = w.UserName; tbUserDesc.Text = w.UserDescription;
        chkLogon.Checked = w.AutoLogon; tbLogons.Text = w.LogonCount.ToString(CultureInfo.InvariantCulture);
        tbRdp.Text = w.RdpPort.ToString(CultureInfo.InvariantCulture); tbKms.Text = w.Kms; tbGvlk.Text = w.Gvlk;
        chkIcmp.Checked = w.Icmp; chkIpv6.Checked = w.DisableIpv6; chkTele.Checked = w.DisableTelemetry;
        chkHib.Checked = w.DisableHibernate; chkIcons.Checked = w.DesktopIcons;
        if (secrets) { tbAdmPw.Text = baseSettings.AdminPassword; tbUserPw.Text = baseSettings.UserPassword; }
    }

    static bool Num(PillBox b, out int v)
    {
        string t = b.Text.Trim();
        if (t.Length == 0) { v = 0; return true; }
        return int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out v);
    }

    // Reads the fields. Returns null and an error text when a number cannot be read.
    WinSettings Collect(out string err)
    {
        err = null;
        WinSettings w = new WinSettings();
        int rx, ry, logons, rdp;
        if (!Num(tbResX, out rx) || !Num(tbResY, out ry) || !Num(tbLogons, out logons) || !Num(tbRdp, out rdp)) { err = S.T("win.err.num"); return null; }
        w.Organization = tbOrg.Text.Trim(); w.Owner = tbOwner.Text.Trim(); w.TimeZone = tbTz.Text.Trim(); w.InputLocale = tbInput.Text.Trim();
        w.UiLanguage = tbUi.Text.Trim(); w.SystemLocale = tbSys.Text.Trim(); w.UserLocale = tbUserLoc.Text.Trim();
        w.Dns = tbDns.Text.Trim(); w.Adapter = tbAdapter.Text.Trim();
        w.ResX = rx; w.ResY = ry;
        w.EnableAdmin = chkAdmin.Checked; w.AdminName = tbAdmName.Text.Trim();
        w.UserName = tbUser.Text.Trim(); w.UserDescription = tbUserDesc.Text.Trim();
        w.AutoLogon = chkLogon.Checked; w.LogonCount = logons;
        w.RdpPort = rdp; w.Kms = tbKms.Text.Trim(); w.Gvlk = tbGvlk.Text.Trim();
        w.Icmp = chkIcmp.Checked; w.DisableIpv6 = chkIpv6.Checked; w.DisableTelemetry = chkTele.Checked;
        w.DisableHibernate = chkHib.Checked; w.DesktopIcons = chkIcons.Checked;
        if (secrets) { w.AdminPassword = tbAdmPw.Text; w.UserPassword = tbUserPw.Text; }
        return w;
    }

    void SaveProfile()
    {
        string err;
        WinSettings w = Collect(out err);
        if (w == null) { Dlg.Msg(this, err, DlgKind.Warn); return; }
        using (SaveFileDialog d = new SaveFileDialog())
        {
            d.Filter = S.T("win.filter") + "|*.ini";
            d.FileName = "windows-settings.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { w.Save(d.FileName); } catch (Exception ex) { Dlg.Msg(this, ex.Message, DlgKind.Error); }
        }
    }

    void LoadProfile()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Filter = S.T("win.filter") + "|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                WinSettings w = WinSettings.Load(d.FileName);
                if (secrets) { w.AdminPassword = tbAdmPw.Text; w.UserPassword = tbUserPw.Text; }
                Fill(w);
                if (secrets) { tbAdmPw.Text = w.AdminPassword; tbUserPw.Text = w.UserPassword; }
            }
            catch (Exception ex) { Dlg.Msg(this, ex.Message, DlgKind.Error); }
        }
    }
}
