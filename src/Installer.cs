// Installing Windows: partition the disk (diskpart), apply the image (DISM), create the boot files (bcdboot).
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

class InstallOptions
{
    public string ImageFile;
    public int Index;
    public DiskInfo Disk;
    public bool Gpt = true;
    public bool Recovery = true;
    public int WinGb;              // 0 = the whole disk
    public bool DataPartition;     // remaining space as a "Data" volume (only with WinGb > 0)
    public string Drivers = "";
    public WinSettings Win;        // null = no answer file; otherwise written into the installed Windows

    public string ModeText { get { return Gpt ? "GPT / UEFI" : "MBR / BIOS"; } }
}

static class Installer
{
    const int BootMb = 520;        // EFI(260)+MSR(16) or System Reserved(500), rounded up
    const int RecoveryMb = 1000;

    public static long NeedMb(InstallOptions o)
    {
        return (long)o.WinGb * 1024 + BootMb + (o.Recovery ? RecoveryMb : 0) + 16;
    }

    // Returns an error text, or null when the options are fine.
    public static string Validate(InstallOptions o)
    {
        if (o.Disk == null || string.IsNullOrEmpty(o.ImageFile)) return S.T("inst.err.incomplete");
        if (o.WinGb != 0 && o.WinGb < 20) return S.T("inst.err.size");
        if (o.WinGb > 0)
        {
            long diskMb = o.Disk.Size / 1048576;
            long need = NeedMb(o);
            if (need > diskMb)
                return S.F("inst.err.toobig", o.WinGb, diskMb / 1024, (need - (long)o.WinGb * 1024 + 1023) / 1024);
        }
        if (!string.IsNullOrEmpty(o.Drivers) && !Directory.Exists(o.Drivers)) return S.T("inst.err.drivers");
        if (o.Win != null)
        {
            string we = o.Win.Validate(true);
            if (we != null) return S.T(we);
        }
        return null;
    }

    // Free space left for the Data partition, in GB (0 when too small).
    public static long LeftoverGb(InstallOptions o)
    {
        if (o.WinGb <= 0 || o.Disk == null) return 0;
        long left = o.Disk.Size / 1048576 - NeedMb(o);
        return left >= 1024 ? left / 1024 : 0;
    }

    // The diskpart script. Letters: S = system/EFI, W = Windows, R = recovery, D = data.
    public static List<string> DiskpartScript(InstallOptions o, string ls, string lw, string lr, string ld)
    {
        List<string> dp = new List<string>();
        dp.Add("select disk " + o.Disk.Number);
        dp.Add("attributes disk clear readonly noerr");
        dp.Add("online disk noerr");
        dp.Add("clean");
        dp.Add("rescan");
        if (o.Gpt)
        {
            // "convert gpt" already creates the 16 MB MSR on current diskpart versions; an extra explicit
            // "create partition msr" only produced a duplicate. (The MSR is optional for UEFI boot anyway.)
            dp.Add("convert gpt");
            dp.Add("create partition efi size=260");
            dp.Add("format quick fs=fat32 label=\"System\" override");
            dp.Add("assign letter=" + ls);
        }
        else
        {
            dp.Add("convert mbr");
            dp.Add("create partition primary size=500");
            dp.Add("format quick fs=ntfs label=\"System Reserved\" override");
            dp.Add("active");
            dp.Add("assign letter=" + ls);
        }
        dp.Add(o.WinGb > 0 ? "create partition primary size=" + (o.WinGb * 1024) : "create partition primary");
        if (o.Recovery && o.WinGb <= 0) dp.Add("shrink desired=" + RecoveryMb + " minimum=" + RecoveryMb);
        dp.Add("format quick fs=ntfs label=\"Windows\" override");
        dp.Add("assign letter=" + lw);
        if (o.Recovery)
        {
            dp.Add(o.WinGb > 0 ? "create partition primary size=" + RecoveryMb : "create partition primary");
            dp.Add("format quick fs=ntfs label=\"Recovery\" override");
            dp.Add("assign letter=" + lr);
            if (o.Gpt)
            {
                dp.Add("set id=\"de94bba4-06d1-4d40-a16a-bfd50179d6ac\" override");
                dp.Add("gpt attributes=0x8000000000000001");
            }
            else dp.Add("set id=27 override");
        }
        if (o.DataPartition && o.WinGb > 0)
        {
            dp.Add("create partition primary");
            dp.Add("format quick fs=ntfs label=\"Data\" override");
            dp.Add("assign letter=" + ld);
        }
        dp.Add("exit");
        return dp;
    }

    static void RunDiskpart(List<string> script, IProgress2 prog)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "wininst_diskpart.txt");
        File.WriteAllLines(tmp, script.ToArray(), System.Text.Encoding.ASCII);
        try
        {
            int code = Proc.Run("diskpart.exe", "/s \"" + tmp + "\"", prog);
            if (code != 0) throw new InvalidOperationException(S.F("inst.err.diskpart", code));
        }
        finally { try { File.Delete(tmp); } catch (Exception) { } }
    }

    // Partitions the disk; letters are re-picked on every attempt (stale ones can stay busy after a failure).
    // Returns { S, W, R, D }.
    static string[] Partition(InstallOptions o, IProgress2 prog)
    {
        string[] letters = null;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                List<string> free = Disks.FreeLetters();
                if (free.Count < 4) throw new InvalidOperationException(S.T("inst.err.letters"));
                letters = new string[] { free[0], free[1], free[2], free[3] };
                RunDiskpart(DiskpartScript(o, letters[0], letters[1], letters[2], letters[3]), prog);
                break;
            }
            catch (InvalidOperationException)
            {
                if (attempt == 2) throw;
                prog.Log(S.T("inst.retry"));
                Thread.Sleep(5000);
            }
        }
        // wait for the volumes to appear
        for (int i = 0; i < 20; i++)
        {
            if (Directory.Exists(letters[0] + ":\\") && Directory.Exists(letters[1] + ":\\")) return letters;
            Thread.Sleep(500);
        }
        throw new InvalidOperationException(S.F("inst.err.novol", letters[1]));
    }

    public static void Run(InstallOptions o, IProgress2 prog)
    {
        string err = Validate(o);
        if (err != null) throw new InvalidOperationException(err);

        // 1. partitions
        prog.Status(S.T("inst.step1")); prog.Log("== " + S.T("inst.log.part") + " ==");
        prog.Indeterminate();
        string[] L = Partition(o, prog);
        string ls = L[0], lw = L[1], lr = L[2];

        // 2. apply the image; scratch space goes to the target volume (the WinPE RAM disk is too small)
        prog.Status(S.T("inst.step2")); prog.Log("== " + S.T("inst.log.apply") + " ==");
        string scratch = lw + ":\\_dism_scratch";
        Directory.CreateDirectory(scratch);
        int code = Proc.Run("dism.exe", "/Apply-Image /ImageFile:\"" + o.ImageFile + "\" /Index:" + o.Index + " /ApplyDir:" + lw + ":\\ /ScratchDir:\"" + scratch + "\"", prog);
        if (code != 0) throw new InvalidOperationException(S.F("inst.err.apply", code));
        if (!Directory.Exists(lw + ":\\Windows\\System32")) throw new InvalidOperationException(S.T("inst.err.nowindows"));

        // 3. drivers
        if (!string.IsNullOrEmpty(o.Drivers))
        {
            prog.Status(S.T("inst.step3")); prog.Log("== " + S.T("inst.log.drivers") + " ==");
            prog.Indeterminate();
            code = Proc.Run("dism.exe", "/Image:" + lw + ":\\ /Add-Driver /Driver:\"" + o.Drivers.TrimEnd('\\') + "\" /Recurse /ScratchDir:\"" + scratch + "\"", prog);
            if (code != 0) prog.Log(S.T("inst.warn.drivers"));
        }
        try { Directory.Delete(scratch, true); } catch (Exception) { }

        // 3b. answer file and first-logon script (passwords are written only here, to the target disk)
        if (o.Win != null)
        {
            prog.Log("== " + S.T("win.log.head") + " ==");
            WinDeploy.ApplyToTarget(lw + ":\\", o.Win, prog);
        }

        // 4. boot files
        prog.Status(S.T("inst.step4")); prog.Log("== " + S.T("inst.log.boot") + " ==");
        prog.Indeterminate();
        string fw = o.Gpt ? "UEFI" : "BIOS";
        bool ok = false;
        string bcd = lw + ":\\Windows\\System32\\bcdboot.exe";
        if (File.Exists(bcd))
        {
            try { ok = Proc.Run(bcd, lw + ":\\Windows /s " + ls + ": /f " + fw, prog) == 0; } catch (Exception) { ok = false; }
        }
        if (!ok && Proc.Run("bcdboot.exe", lw + ":\\Windows /s " + ls + ": /f " + fw, prog) != 0)
            throw new InvalidOperationException(S.T("inst.err.bcd"));

        // 5. WinRE
        if (o.Recovery)
        {
            try
            {
                string re = lw + ":\\Windows\\System32\\Recovery\\Winre.wim";
                if (File.Exists(re))
                {
                    string dst = lr + ":\\Recovery\\WindowsRE";
                    Directory.CreateDirectory(dst);
                    File.SetAttributes(re, FileAttributes.Normal);
                    File.Copy(re, Path.Combine(dst, "Winre.wim"), true);
                    Proc.Run("reagentc.exe", "/setreimage /path " + dst + " /target " + lw + ":\\Windows", prog);
                    File.Delete(re);
                }
            }
            catch (Exception ex) { prog.Log(S.T("inst.warn.winre") + " " + ex.Message); }
        }

        // 6. release the temporary letters
        try
        {
            List<string> rm = new List<string>();
            rm.Add("select volume " + ls); rm.Add("remove letter=" + ls + " noerr");
            if (o.Recovery) { rm.Add("select volume " + lr); rm.Add("remove letter=" + lr + " noerr"); }
            rm.Add("exit");
            RunDiskpart(rm, null);
        }
        catch (Exception) { }
        prog.Percent(100);
        prog.Status(S.T("inst.done"));
        prog.Log("== " + S.T("inst.done") + " ==");
    }
}
