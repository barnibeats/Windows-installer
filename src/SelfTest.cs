// "WindowsInstaller.exe --selftest <file>": checks the non-destructive logic and writes a report.
// Nothing is changed on the machine. Used by the build to catch regressions.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

static class SelfTest
{
    static StringBuilder sb = new StringBuilder();
    static int fails;

    static void Check(string name, bool ok, string detail)
    {
        if (!ok) fails++;
        sb.AppendLine((ok ? "PASS  " : "FAIL  ") + name + (detail != null && detail.Length > 0 ? "  - " + detail : ""));
    }

    static bool Has(List<string> l, string line) { return l.Contains(line); }

    // "--e2e <diskNumber> <imageFile> <index> <gpt|mbr> [winGb]": a REAL install, allowed only onto a virtual disk
    // (a VHD/VHDX created for the test). Refuses any other disk. Writes the log to the given self-test file.
    public static int RunE2E(string outFile, string[] a)
    {
        int diskNo = int.Parse(a[0]);
        InstallOptions o = new InstallOptions();
        o.ImageFile = a[1]; o.Index = int.Parse(a[2]); o.Gpt = a[3] == "gpt";
        o.WinGb = a.Length > 4 ? int.Parse(a[4]) : 0;
        o.Recovery = true; o.DataPartition = o.WinGb > 0;
        StringBuilder log = new StringBuilder();
        int code = 1;
        try
        {
            DiskInfo target = null;
            foreach (DiskInfo d in Disks.List(null)) if (d.Number == diskNo) target = d;
            if (target == null) throw new InvalidOperationException("disk " + diskNo + " not found");
            if (target.PnpId.ToUpperInvariant().IndexOf("PROD_VIRTUAL_DISK") < 0)
                throw new InvalidOperationException("REFUSED: disk " + diskNo + " (" + target.Model + ", " + target.PnpId + ") is not a virtual disk");
            o.Disk = target;
            log.AppendLine("target: disk " + diskNo + " " + target.Model + " " + Fmt.Size(target.Size));
            Installer.Run(o, new LogProgress(log));
            log.AppendLine("E2E OK");
            code = 0;
        }
        catch (Exception ex) { log.AppendLine("E2E FAILED: " + ex.Message); }
        try { File.WriteAllText(outFile, log.ToString(), Encoding.UTF8); } catch (Exception) { }
        return code;
    }

    sealed class LogProgress : IProgress2
    {
        readonly StringBuilder sb;
        public LogProgress(StringBuilder sb) { this.sb = sb; }
        public void Log(string line) { lock (sb) { sb.AppendLine(line); } }
        public void Percent(int percent) { }
        public void Indeterminate() { }
        public void Status(string text) { lock (sb) { sb.AppendLine("[status] " + text); } }
    }

    public static int Run(string outFile)
    {
        sb.AppendLine("Windows Installer " + AppInfo.Version + " self-test");
        try
        {
            Strings();
            Scripts();
            WinFiles();
            Parsing();
            Environment_();
            OpenImages();
        }
        catch (Exception ex) { Check("unexpected exception", false, ex.ToString()); }
        sb.AppendLine(fails == 0 ? "ALL OK" : fails + " FAILED");
        try { if (!string.IsNullOrEmpty(outFile)) File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8); } catch (Exception) { }
        return fails == 0 ? 0 : 1;
    }

    static void Strings()
    {
        int n = 0, bad = 0;
        StringBuilder why = new StringBuilder();
        foreach (string key in S.Keys)
        {
            n++;
            string[] ph = new string[3];
            for (int l = 0; l < 3; l++)
            {
                string v = S.Raw(key, l);
                if (string.IsNullOrEmpty(v)) { bad++; why.Append(key + "[" + l + "] empty; "); continue; }
                List<string> found = new List<string>();
                foreach (Match m in Regex.Matches(v, @"\{(\d+)\}")) if (!found.Contains(m.Value)) found.Add(m.Value);
                found.Sort();
                ph[l] = string.Join(",", found.ToArray());
            }
            if (ph[0] != ph[1] || ph[1] != ph[2]) { bad++; why.Append(key + " placeholders differ (" + ph[0] + " | " + ph[1] + " | " + ph[2] + "); "); }
        }
        Check("strings: " + n + " keys, all languages filled, placeholders equal", bad == 0, why.ToString());
    }

    static void Scripts()
    {
        DiskInfo d = new DiskInfo();
        d.Number = 3; d.Size = 250L * 1073741824L;
        InstallOptions o = new InstallOptions();
        o.Disk = d; o.ImageFile = "x.wim"; o.Index = 1;

        // GPT, whole disk, recovery
        o.Gpt = true; o.Recovery = true; o.WinGb = 0; o.DataPartition = false;
        List<string> s = Installer.DiskpartScript(o, "S", "W", "R", "D");
        Check("diskpart GPT whole disk: starts with select disk 3 / clean", s[0] == "select disk 3" && Has(s, "clean"), null);
        Check("diskpart GPT: convert gpt + efi, no duplicate msr", Has(s, "convert gpt") && Has(s, "create partition efi size=260") && !Has(s, "create partition msr size=16"), null);
        Check("diskpart GPT whole disk: shrink for recovery", Has(s, "shrink desired=1000 minimum=1000"), null);
        Check("diskpart GPT: recovery type id set with override", Has(s, "set id=\"de94bba4-06d1-4d40-a16a-bfd50179d6ac\" override"), null);
        Check("diskpart: every format has override", s.FindAll(delegate(string x) { return x.StartsWith("format "); }).TrueForAll(delegate(string x) { return x.EndsWith("override"); }), null);

        // GPT, custom size + data
        o.WinGb = 60; o.DataPartition = true;
        s = Installer.DiskpartScript(o, "S", "W", "R", "D");
        Check("diskpart custom size: Windows partition 61440 MB", Has(s, "create partition primary size=61440"), null);
        Check("diskpart custom size: no shrink, recovery has fixed size", !Has(s, "shrink desired=1000 minimum=1000") && Has(s, "create partition primary size=1000"), null);
        Check("diskpart custom size: Data volume", Has(s, "format quick fs=ntfs label=\"Data\" override") && Has(s, "assign letter=D"), null);

        // MBR
        o.Gpt = false; o.WinGb = 0; o.DataPartition = false;
        s = Installer.DiskpartScript(o, "S", "W", "R", "D");
        Check("diskpart MBR: convert mbr, active system partition, recovery id 27", Has(s, "convert mbr") && Has(s, "active") && Has(s, "set id=27 override"), null);
        Check("diskpart MBR: no efi/msr", !Has(s, "create partition efi size=260") && !Has(s, "create partition msr size=16"), null);

        // validation
        o.Gpt = true; o.WinGb = 250; o.Recovery = true;
        Check("validate: 250 GB partition on a 250 GB disk is rejected", Installer.Validate(o) != null, null);
        o.WinGb = 100;
        Check("validate: 100 GB on a 250 GB disk is accepted", Installer.Validate(o) == null, Installer.Validate(o));
        o.WinGb = 15;
        Check("validate: 15 GB is rejected", Installer.Validate(o) != null, null);
        o.WinGb = 100;
        Check("leftover for Data: about 148 GB", Installer.LeftoverGb(o) >= 145 && Installer.LeftoverGb(o) <= 150, Installer.LeftoverGb(o).ToString());
    }

    // Answer file and first-logon script built from the Windows settings.
    static void WinFiles()
    {
        WinSettings w = new WinSettings();
        w.AdminPassword = "Sekret1"; w.UserPassword = "Sekret2";
        string full = WinDeploy.BuildXml(w, true, "amd64");
        string cap = WinDeploy.BuildXml(w, false, "amd64");
        string pre = WinDeploy.BuildPretail(w);
        string b1 = Convert.ToBase64String(Encoding.Unicode.GetBytes("Sekret1AdministratorPassword"));
        string b2 = Convert.ToBase64String(Encoding.Unicode.GetBytes("Sekret2Password"));
        try
        {
            System.Xml.XmlDocument d = new System.Xml.XmlDocument(); d.LoadXml(full);
            System.Xml.XmlDocument c = new System.Xml.XmlDocument(); c.LoadXml(cap);
            Check("win xml: well-formed, CopyProfile false, first-logon pretail, SkipRearm",
                full.Contains("<CopyProfile>false</CopyProfile>") && full.Contains("C:\\WINDOWS\\system32\\pretail.cmd") && full.Contains("<SkipRearm>1</SkipRearm>"), null);
        }
        catch (Exception ex) { Check("win xml: well-formed", false, ex.Message); }
        Check("win xml: accounts, auto logon (1 logon) and passwords as the answer file expects",
            full.Contains(b1) && full.Contains(b2) && full.Contains("<LogonCount>1</LogonCount>") && full.Contains("<Name>Пользователь</Name>"), null);
        Check("win xml for the capture image has no passwords, accounts or auto logon",
            !cap.Contains(b1) && !cap.Contains(b2) && !cap.Contains("Sekret") && !cap.Contains("<AutoLogon>") && !cap.Contains("<LocalAccount ") && cap.Contains("<FirstLogonCommands>"), null);
        Check("pretail.cmd has no passwords", !pre.Contains("Sekret") && !pre.Contains(b1) && !pre.Contains(b2), null);
        Check("pretail.cmd: RDP 4444, ICMP, KMS retry, answer file cleanup, self-delete is the last line",
            pre.Contains("/d 4444") && pre.Contains("P_RETAIL_ICMPv4_Echo_In") && pre.Contains(":kms") && pre.Contains("Panther\\Unattend\\unattend.xml") &&
            pre.TrimEnd().EndsWith("del /f /q \"%~f0\""), null);
        w.RdpPort = 0; w.Kms = ""; w.UserName = "";
        string pre2 = WinDeploy.BuildPretail(w);
        Check("pretail.cmd: RDP and KMS are skipped when switched off", !pre2.Contains("Terminal Server") && !pre2.Contains(":kms") && !pre2.Contains("slmgr"), null);

        WinSettings v = new WinSettings(); v.AdminPassword = "a"; v.UserPassword = "b";
        Check("win settings: defaults are valid", v.Validate(true) == null, v.Validate(true));
        v.Kms = "bad host;calc"; Check("win settings: KMS with shell characters is rejected", v.Validate(true) == "win.err.kms", null);
        v.Kms = "k.motto.ua:9876"; v.RdpPort = 70000; Check("win settings: RDP port 70000 is rejected", v.Validate(true) == "win.err.port", null);
        v.RdpPort = 4444; v.UserName = "bad\"name"; Check("win settings: quote in the user name is rejected", v.Validate(true) == "win.err.user", null);
        v.UserName = "Пользователь"; v.UserPassword = ""; Check("win settings: empty user password is rejected for an install", v.Validate(true) == "win.err.userpw" && v.Validate(false) == null, null);

        string tmp = Path.Combine(Path.GetTempPath(), "wi_selftest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(tmp, "Windows\\System32"));
            WinSettings s = new WinSettings(); s.AdminPassword = "Sekret1"; s.UserPassword = "Sekret2";
            string prof = Path.Combine(tmp, "p.ini");
            s.Save(prof);
            Check("win settings: profile file has no passwords", !File.ReadAllText(prof, Encoding.UTF8).Contains("Sekret") && !File.ReadAllText(prof, Encoding.UTF8).ToLowerInvariant().Contains("password"), null);
            WinSettings l = WinSettings.Load(prof);
            Check("win settings: profile round trip", l.RdpPort == 4444 && l.UserName == "Пользователь" && l.DesktopIcons && l.AdminPassword.Length == 0, null);
            WinDeploy.ApplyToTarget(tmp, s, new NullProgress());
            Check("install: answer file and pretail.cmd written to the target", File.Exists(Path.Combine(tmp, "Windows\\Panther\\Unattend\\unattend.xml")) && File.Exists(Path.Combine(tmp, "Windows\\System32\\pretail.cmd")), null);
        }
        catch (Exception ex) { Check("install: files written to the target", false, ex.Message); }
        finally { try { Directory.Delete(tmp, true); } catch (Exception) { } }
    }

    static void Parsing()
    {
        string sample =
            "Deployment Image Servicing and Management tool\r\nVersion: 10.0.19041.3636\r\n\r\nDetails for image : E:\\sources\\install.wim\r\n\r\n" +
            "Index : 1\r\nName : Windows 10 Home\r\nDescription : Windows 10 Home\r\nSize : 15,000,000,000 bytes\r\n\r\n" +
            "Index : 2\r\nName : Windows 10 Pro\r\nDescription : Windows 10 Pro\r\nSize : 15,100,000,000 bytes\r\n\r\nThe operation completed successfully.\r\n";
        List<Edition> eds = Images.ParseEditions(sample);
        Check("DISM output: 2 editions parsed", eds.Count == 2 && eds[1].Index == 2 && eds[1].Name == "Windows 10 Pro", eds.Count.ToString());
        Check("edition choice prefers Pro", Images.PreferPro(eds).Index == 2, null);
        Check("version compare: v1.10.0 > 1.9.3", Updater.Parse("v1.10.0") > Updater.Parse("1.9.3"), null);
        Check("version compare: 1.2.0-dev equals 1.2.0", Updater.Parse("1.2.0-dev") == Updater.Parse("1.2.0"), null);
        Check("safe image name is ASCII", Regex.IsMatch(WinPeCapture.SafeName("Моя сборка 1"), "^[A-Za-z0-9_.\\-]+$"), WinPeCapture.SafeName("Моя сборка 1"));
    }

    // Optional, read-only: "--iso <file.iso|wim|esd>" opens the image (mounts an ISO read-only) and lists its editions.
    sealed class NullProgress : IProgress2
    {
        public void Log(string line) { }
        public void Percent(int percent) { }
        public void Indeterminate() { }
        public void Status(string text) { }
    }

    static void OpenImages()
    {
        string[] a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
        {
            if (a[i] != "--iso") continue;
            string path = a[i + 1];
            ImageSource src = null;
            try
            {
                src = Images.Open(path, new NullProgress());
                List<Edition> eds = Images.ListEditions(src.ImageFile);
                string how = src.MountLetter != null ? "mounted as " + src.MountLetter + ":" : (src.ExtractDir != null ? "extracted" : "plain image");
                Check("open " + Path.GetFileName(path) + " (" + how + ")", eds.Count > 0, eds.Count + " editions: " + string.Join("; ", eds.ConvertAll<string>(delegate(Edition e) { return e.Index + "=" + e.Name; }).ToArray()));
            }
            catch (Exception ex) { Check("open " + Path.GetFileName(path), false, ex.Message); }
            finally { if (src != null) src.Dispose(); }
        }
    }

    static void Environment_()
    {
        sb.AppendLine("INFO  WinPE=" + Env.IsWinPE + ", firmware=" + Env.Firmware() + ", OS=" + Environment.OSVersion.VersionString + ", .NET=" + Environment.Version);
        try
        {
            List<DiskInfo> list = Disks.List(null);
            Check("WMI disk list", list.Count > 0, list.Count + " disks");
            foreach (DiskInfo di in list)
                sb.AppendLine("INFO  disk " + di.Number + ": " + di.Model + ", " + Fmt.Size(di.Size) + ", " + di.Bus + ", " + di.Style + ", volumes=[" + di.VolumesText + "]" + (di.Blocked ? ", BLOCKED(" + di.BlockReason + ")" : ""));
            int blockedCount = 0;
            foreach (DiskInfo di in list) if (di.Blocked) blockedCount++;
            if (!Env.IsWinPE) Check("system disk is blocked", blockedCount >= 1, null);
        }
        catch (Exception ex) { Check("WMI disk list", false, ex.Message); }
        try
        {
            UpdateInfo u = Updater.FetchLatest();
            if (u == null) sb.AppendLine("INFO  update check skipped (no network?)");
            else Check("updater reads latest.json from GitHub", u.Tag.StartsWith("v") && u.Url.EndsWith(".exe") && u.Size > 100000 && u.Sha256.Length == 64, u.Tag + ", " + u.Size + " bytes, sha " + u.Sha256.Substring(0, 8) + "...");
        }
        catch (Exception ex) { Check("updater reads latest.json from GitHub", false, ex.Message); }
        try
        {
            RawData a = Disks.ReadWithManagement(), b = Disks.ReadWithPowerShell();
            bool same = a.Drives.Count == b.Drives.Count && a.Partitions.Count == b.Partitions.Count && a.Letters.Count == b.Letters.Count;
            for (int i = 0; same && i < a.Drives.Count; i++)
                same = a.Drives[i].Index == b.Drives[i].Index && a.Drives[i].Size == b.Drives[i].Size && a.Drives[i].Pnp == b.Drives[i].Pnp;
            foreach (KeyValuePair<string, int> kv in a.Letters) { int n; if (!b.Letters.TryGetValue(kv.Key, out n) || n != kv.Value) same = false; }
            Check("disk list: PowerShell fallback equals System.Management", same, a.Drives.Count + " drives, " + a.Letters.Count + " letters");
        }
        catch (Exception ex) { Check("disk list: PowerShell fallback equals System.Management", false, ex.Message); }
        List<string> free = Disks.FreeLetters();
        Check("free drive letters available", free.Count >= 4, free.Count + " free");
        try
        {
            string c = CaptureCmd.Build("E:\\Images", "Test_1", "fast");
            Check("capture.cmd text generated", c.Contains("/Compress:fast") && c.Contains("Test_1.wim") && c.Contains("%~d0\\Images") && !c.Contains("@@"), null);
        }
        catch (Exception ex) { Check("capture.cmd text generated", false, ex.Message); }
    }
}
