// Capturing Windows into a WIM: pre-checks, shortcut safety net, answer file, Sysprep (in Windows),
// and the DISM capture itself (in WinPE).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

class CheckItem
{
    public string Kind;   // ok | info | warn | bad
    public string Text;
    public CheckItem(string kind, string text) { Kind = kind; Text = text; }
}

static class Env
{
    public static bool IsWinPE
    {
        get
        {
            if (Environment.GetEnvironmentVariable("WI_FORCE_WINPE") == "1") return true;   // developer switch: preview the WinPE screens in Windows
            if ((Environment.GetEnvironmentVariable("SystemDrive") ?? "").ToUpperInvariant() == "X:") return true;
            try { using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\MiniNT")) { return k != null; } }
            catch (Exception) { return false; }
        }
    }

    // UEFI or Bios (Legacy) of the current machine
    public static string Firmware()
    {
        string t = Environment.GetEnvironmentVariable("firmware_type");
        if (!string.IsNullOrEmpty(t)) return t.Equals("Legacy", StringComparison.OrdinalIgnoreCase) ? "BIOS" : "UEFI";
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control"))
            {
                object v = k == null ? null : k.GetValue("PEFirmwareType");
                if (v != null) return Convert.ToInt32(v) == 1 ? "BIOS" : "UEFI";
            }
        }
        catch (Exception) { }
        return Directory.Exists(Environment.GetEnvironmentVariable("SystemDrive") + "\\EFI") ? "UEFI" : "UEFI";
    }
}

static class Ps
{
    // Runs a PowerShell snippet (encoded, so quoting never breaks) and returns its output.
    public static int Run(string script, out string output)
    {
        string enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return Proc.Capture("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + enc, out output);
    }
}

// ---------------------------------------------------------------------------------------------------
// Pre-checks (in a running Windows)
// ---------------------------------------------------------------------------------------------------
class CheckResult
{
    public List<CheckItem> Items = new List<CheckItem>();
    public int Blockers;
    public bool BuiltinAdminEnabled;
    public void Add(string kind, string text)
    {
        Items.Add(new CheckItem(kind, text));
        if (kind == "bad") Blockers++;
    }
}

static class CaptureChecks
{
    static string SysDrive { get { return (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").ToUpperInvariant(); } }

    public static CheckResult Run()
    {
        CheckResult r = new CheckResult();
        try
        {
            foreach (ManagementObject os in new ManagementObjectSearcher("SELECT Caption, BuildNumber FROM Win32_OperatingSystem").Get())
                r.Add("info", Convert.ToString(os["Caption"]).Trim() + ", " + S.T("cap.build") + " " + os["BuildNumber"]);
        }
        catch (Exception) { }

        if (Env.IsWinPE) r.Add("bad", S.T("cap.chk.winpe"));

        // BitLocker
        try
        {
            bool on = false, found = false;
            ManagementScope scope = new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption");
            scope.Connect();
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='" + SysDrive + "'")))
                foreach (ManagementObject v in s.Get())
                {
                    found = true;
                    uint prot = Convert.ToUInt32(v.InvokeMethod("GetProtectionStatus", null, null)["ProtectionStatus"]);
                    uint conv = Convert.ToUInt32(v.InvokeMethod("GetConversionStatus", null, null)["ConversionStatus"]);
                    if (prot != 0 || conv != 0) on = true;
                }
            if (found && on) r.Add("bad", S.F("cap.chk.bitlocker", SysDrive));
            else r.Add("ok", S.T("cap.chk.bitlocker.ok"));
        }
        catch (Exception) { r.Add("info", S.T("cap.chk.bitlocker.unk")); }

        // domain
        try
        {
            bool dom = false;
            foreach (ManagementObject cs in new ManagementObjectSearcher("SELECT PartOfDomain FROM Win32_ComputerSystem").Get()) dom = Convert.ToBoolean(cs["PartOfDomain"]);
            r.Add(dom ? "bad" : "ok", S.T(dom ? "cap.chk.domain" : "cap.chk.domain.ok"));
        }
        catch (Exception) { }

        // pending reboot
        bool pend = RegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") ||
                    RegKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
                if (k != null && k.GetValue("PendingFileRenameOperations") != null) pend = true;
        }
        catch (Exception) { }
        r.Add(pend ? "warn" : "ok", S.T(pend ? "cap.chk.reboot" : "cap.chk.reboot.ok"));

        // accounts
        try
        {
            foreach (ManagementObject u in new ManagementObjectSearcher("SELECT Name, Disabled, SID FROM Win32_UserAccount WHERE LocalAccount=True").Get())
            {
                string sid = Convert.ToString(u["SID"]);
                bool builtin = sid.EndsWith("-500");
                bool enabled = !Convert.ToBoolean(u["Disabled"]);
                if (builtin && enabled) r.BuiltinAdminEnabled = true;
                r.Add("info", S.T("cap.chk.account") + " " + u["Name"] + "  (" + S.T(enabled ? "cap.enabled" : "cap.disabled") + ")" + (builtin ? "  - " + S.T("cap.builtin") : ""));
            }
        }
        catch (Exception) { }

        // user-installed Store apps (Sysprep generalize usually fails on them)
        try
        {
            string o;
            Ps.Run("$p=@((Get-AppxProvisionedPackage -Online).DisplayName); Get-AppxPackage -AllUsers | Where-Object { -not $_.IsFramework -and $_.SignatureKind -ne 'System' -and $p -notcontains $_.Name } | ForEach-Object { $_.Name }", out o);
            List<string> apps = new List<string>();
            foreach (string line in o.Split('\n')) if (line.Trim().Length > 0 && !line.Contains(" ")) apps.Add(line.Trim());
            if (apps.Count > 0)
            {
                r.Add("warn", S.F("cap.chk.apps", apps.Count));
                for (int i = 0; i < Math.Min(8, apps.Count); i++) r.Add("info", "    " + apps[i]);
            }
            else r.Add("ok", S.T("cap.chk.apps.ok"));
        }
        catch (Exception) { r.Add("info", S.T("cap.chk.apps.unk")); }

        // programs installed outside the system drive
        List<string> other = new List<string>();
        foreach (string key in new string[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(key))
                {
                    if (k == null) continue;
                    foreach (string sub in k.GetSubKeyNames())
                        using (RegistryKey a = k.OpenSubKey(sub))
                        {
                            if (a == null) continue;
                            string name = Convert.ToString(a.GetValue("DisplayName"));
                            string loc = (Convert.ToString(a.GetValue("InstallLocation")) ?? "").Trim('"');
                            if (name.Length > 0 && Regex.IsMatch(loc, "^[A-Za-z]:") && !loc.ToUpperInvariant().StartsWith(SysDrive))
                                other.Add(name + "  ->  " + loc);
                        }
                }
            }
            catch (Exception) { }
        }
        if (other.Count > 0)
        {
            r.Add("warn", S.F("cap.chk.other", SysDrive, other.Count));
            for (int i = 0; i < Math.Min(6, other.Count); i++) r.Add("info", "    " + other[i]);
        }
        else r.Add("ok", S.F("cap.chk.other.ok", SysDrive));

        // desktop shortcuts
        try { ShortcutReport(r); } catch (Exception) { r.Add("info", S.T("cap.chk.lnk.unk")); }

        // rearm
        try
        {
            foreach (ManagementObject s in new ManagementObjectSearcher("SELECT RemainingWindowsReArmCount FROM SoftwareLicensingService").Get())
                r.Add("info", S.T("cap.chk.rearm") + " " + s["RemainingWindowsReArmCount"]);
        }
        catch (Exception) { }
        r.Add("info", S.T("cap.chk.activation"));
        return r;
    }

    static bool RegKey(string path)
    {
        try { using (RegistryKey k = Registry.LocalMachine.OpenSubKey(path)) { return k != null; } } catch (Exception) { return false; }
    }

    static void ShortcutReport(CheckResult r)
    {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        string users = Path.Combine(SysDrive + "\\", "Users");
        foreach (string u in Directory.GetDirectories(users))
        {
            string n = Path.GetFileName(u);
            if (n == "Default" || n == "Default User" || n == "All Users") continue;
            foreach (string sub in new string[] { "Desktop", "OneDrive\\Desktop" })
            {
                string d = Path.Combine(u, sub);
                if (!Directory.Exists(d)) continue;
                string[] lnks = Directory.GetFiles(d, "*.lnk");
                r.Add("info", S.T("cap.chk.lnk") + " " + d + ": " + lnks.Length);
                if (sub.StartsWith("OneDrive")) r.Add("warn", "    " + S.T("cap.chk.onedrive"));
                foreach (string l in lnks)
                {
                    string target = "";
                    try
                    {
                        object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { l });
                        target = (string)lnk.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, lnk, null);
                    }
                    catch (Exception) { }
                    if (Regex.IsMatch(target ?? "", "^[A-Za-z]:") && !target.ToUpperInvariant().StartsWith(SysDrive))
                        r.Add("warn", "    " + Path.GetFileName(l) + " -> " + target + "  (" + S.F("cap.chk.lnk.off", SysDrive) + ")");
                }
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------------
// Safety net: shortcuts are copied into the image and put back at the first logon of each user
// ---------------------------------------------------------------------------------------------------
static class ProfileKeeper
{
    const string RestoreScript =
@"$root = Join-Path $env:ProgramData 'ProfileKeeper'
$me = $env:USERNAME
$flag = Join-Path $root ('done_' + $me + '.flag')
if (Test-Path -LiteralPath $flag) { exit }
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$rc = '/E', '/XC', '/XN', '/XO', '/R:1', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP'
$src = Join-Path $root $me
if (Test-Path -LiteralPath $src) {
    $map = @{ 'Desktop' = [Environment]::GetFolderPath('Desktop'); 'StartMenu' = (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs') }
    foreach ($k in $map.Keys) {
        $s = Join-Path $src $k
        if ((Test-Path -LiteralPath $s) -and $map[$k]) { & robocopy.exe $s $map[$k] @rc | Out-Null }
    }
}
$pub = Join-Path $root 'Public\Desktop'
$pubFlag = Join-Path $root 'done_Public.flag'
if ($isAdmin -and (Test-Path -LiteralPath $pub) -and -not (Test-Path -LiteralPath $pubFlag)) {
    & robocopy.exe $pub (Join-Path $env:PUBLIC 'Desktop') @rc | Out-Null
    New-Item -ItemType File -Path $pubFlag -Force | Out-Null
}
New-Item -ItemType File -Path $flag -Force -ErrorAction SilentlyContinue | Out-Null
";

    // Returns the number of folders saved.
    public static int Backup(IProgress2 prog)
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("ProgramData") ?? "C:\\ProgramData", "ProfileKeeper");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);
        int count = 0;
        string sys = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        foreach (string u in Directory.GetDirectories(Path.Combine(sys + "\\", "Users")))
        {
            string n = Path.GetFileName(u);
            if (n == "Default" || n == "Default User" || n == "All Users") continue;
            List<string[]> pairs = new List<string[]>();
            pairs.Add(new string[] { "Desktop", "Desktop" });
            if (n != "Public") pairs.Add(new string[] { "AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs", "StartMenu" });
            foreach (string[] pr in pairs)
            {
                string src = Path.Combine(u, pr[0]);
                if (!Directory.Exists(src)) continue;
                string dst = Path.Combine(Path.Combine(root, n), pr[1]);
                int code = Proc.Run("robocopy.exe", "\"" + src + "\" \"" + dst + "\" *.lnk *.url /S /R:1 /W:1 /NFL /NDL /NJH /NJS /NP", null);
                if (code < 8) count++;
            }
        }
        string rp = Path.Combine(root, "restore.ps1");
        File.WriteAllText(rp, RestoreScript, new UTF8Encoding(true));
        Proc.Run("icacls.exe", "\"" + root + "\" /grant *S-1-5-32-545:(OI)(CI)M /T /C", null);   // Users may write the flags
        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            k.SetValue("ProfileKeeper", "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + rp + "\"");
        return count;
    }
}

// ---------------------------------------------------------------------------------------------------
// Answer file and first-boot tweaks
// ---------------------------------------------------------------------------------------------------
static class Unattend
{
    // Skips OOBE so that, after the first start, the existing accounts can sign in right away.
    // Language, keyboard and time zone are taken from the current system.
    // SkipMachineOOBE/SkipUserOOBE are deprecated; where ignored, the normal OOBE runs.
    public static string Write()
    {
        string arch = "amd64";
        string pa = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "AMD64";
        if (pa.Equals("ARM64", StringComparison.OrdinalIgnoreCase)) arch = "arm64"; else if (pa.Equals("x86", StringComparison.OrdinalIgnoreCase)) arch = "x86";
        string ui = CultureInfo.CurrentUICulture.Name;
        string loc = CultureInfo.CurrentCulture.Name;
        string klid = Native.KeyboardLayoutId();
        string inp = klid.Substring(4).ToLowerInvariant() + ":" + klid.ToLowerInvariant();
        string tz = TimeZoneInfo.Local.Id;
        string ns = "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"";
        string head = "processorArchitecture=\"" + arch + "\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" " + ns;
        StringBuilder x = new StringBuilder();
        x.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        x.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");
        x.AppendLine("  <settings pass=\"oobeSystem\">");
        x.AppendLine("    <component name=\"Microsoft-Windows-International-Core\" " + head + ">");
        x.AppendLine("      <InputLocale>" + inp + "</InputLocale>");
        x.AppendLine("      <SystemLocale>" + loc + "</SystemLocale>");
        x.AppendLine("      <UILanguage>" + ui + "</UILanguage>");
        x.AppendLine("      <UserLocale>" + loc + "</UserLocale>");
        x.AppendLine("    </component>");
        x.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" " + head + ">");
        x.AppendLine("      <TimeZone>" + tz + "</TimeZone>");
        x.AppendLine("      <OOBE>");
        x.AppendLine("        <HideEULAPage>true</HideEULAPage>");
        x.AppendLine("        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
        x.AppendLine("        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>");
        x.AppendLine("        <ProtectYourPC>3</ProtectYourPC>");
        x.AppendLine("        <SkipMachineOOBE>true</SkipMachineOOBE>");
        x.AppendLine("        <SkipUserOOBE>true</SkipUserOOBE>");
        x.AppendLine("      </OOBE>");
        x.AppendLine("    </component>");
        x.AppendLine("  </settings>");
        x.AppendLine("</unattend>");
        string path = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Temp\\capture_unattend.xml");
        File.WriteAllText(path, x.ToString(), new UTF8Encoding(false));
        return path;
    }

    // Windows disables the built-in Administrator after OOBE; this turns it back on.
    public static void KeepBuiltinAdmin()
    {
        string dir = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Setup\\Scripts");
        Directory.CreateDirectory(dir);
        string f = Path.Combine(dir, "SetupComplete.cmd");
        string marker = "REM ::INSTALLER:: enable built-in Administrator";
        string existing = File.Exists(f) ? File.ReadAllText(f) : "";
        if (existing.Contains(marker)) return;
        string add = "\r\n" + marker + "\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"Get-LocalUser | Where-Object { $_.SID.Value -match '-500$' } | Enable-LocalUser\"\r\n";
        File.AppendAllText(f, add, Encoding.ASCII);
    }
}

// ---------------------------------------------------------------------------------------------------
// Sysprep
// ---------------------------------------------------------------------------------------------------
static class SysprepRunner
{
    static string SysprepDir { get { return Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "System32\\Sysprep"); } }

    // Packages named in the Sysprep logs as the reason generalize failed.
    public static List<string> FailedPackages()
    {
        List<string> res = new List<string>();
        Regex rx = new Regex(@"Package\s+(\S+)\s+was installed for a user, but not provisioned");
        foreach (string f in new string[] { "Panther\\setuperr.log", "Panther\\setupact.log" })
        {
            string p = Path.Combine(SysprepDir, f);
            if (!File.Exists(p)) continue;
            try
            {
                string[] lines;
                using (FileStream fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.Default))
                    lines = sr.ReadToEnd().Split('\n');
                for (int i = Math.Max(0, lines.Length - 400); i < lines.Length; i++)
                {
                    Match m = rx.Match(lines[i]);
                    if (m.Success && !res.Contains(m.Groups[1].Value)) res.Add(m.Groups[1].Value);
                }
            }
            catch (Exception) { }
        }
        return res;
    }

    // Starts Sysprep; on success the PC shuts down. confirmRemove(list) is asked on failure caused by Store apps.
    public static void Run(string unattend, IProgress2 prog, Func<List<string>, bool> confirmRemove)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            prog.Log(S.F("cap.log.sysprep", attempt));
            prog.Status(S.T("cap.sysprep.running"));
            prog.Indeterminate();
            string args = "/generalize /oobe /shutdown /quiet" + (string.IsNullOrEmpty(unattend) ? "" : " /unattend:\"" + unattend + "\"");
            using (Process p = Process.Start(new ProcessStartInfo(Path.Combine(SysprepDir, "sysprep.exe"), args) { UseShellExecute = false }))
            {
                p.WaitForExit();
            }
            prog.Status(S.T("cap.sysprep.wait"));
            Thread.Sleep(60000);   // on success the PC powers off meanwhile

            List<string> bad = FailedPackages();
            prog.Log(S.T("cap.sysprep.failed"));
            if (bad.Count == 0) throw new InvalidOperationException(S.T("cap.err.sysprep.unknown"));
            prog.Log(S.T("cap.sysprep.apps") + " " + string.Join("; ", bad.ToArray()));
            if (!confirmRemove(bad)) return;
            foreach (string pk in bad)
            {
                string o;
                Ps.Run("Remove-AppxPackage -Package '" + pk.Replace("'", "''") + "' -AllUsers", out o);
                prog.Log(S.T("cap.sysprep.removed") + " " + pk);
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------------
// capture.cmd for a stock Windows Setup WinPE (no .NET there)
// ---------------------------------------------------------------------------------------------------
static class CaptureCmd
{
    const string Template =
@"@echo off
setlocal EnableDelayedExpansion
title Capture Windows image
echo ==== Capture Windows image (WinPE) ====
set ""SRC=""
for %%L in (C D E F G H I J K L M N O P Q R S T U V W Y Z) do (
  if exist ""%%L:\Windows\System32\config\SYSTEM"" if /I not ""%%L:""==""%~d0"" (
    echo Found Windows volume: %%L:
    if not defined SRC set ""SRC=%%L:""
  )
)
if not defined SRC echo Windows volume not found. & goto :end
set ""IN=""
set /p ""IN=Windows volume to capture (Enter = !SRC!): ""
if defined IN set ""SRC=!IN:~0,1!:""
if not exist ""!SRC!\Windows\System32\config\SYSTEM"" echo No Windows on !SRC! & goto :end
if /I ""!SRC!""==""%~d0"" echo Save the image to a different drive than the captured one. & goto :end
set ""DIR=@@DIRLINE@@""
if not exist ""!DIR!"" mkdir ""!DIR!""
set ""IMG=!DIR!\@@FILE@@.wim""
set N=1
:chk
if exist ""!IMG!"" (
  set /a N+=1
  set ""IMG=!DIR!\@@FILE@@_!N!.wim""
  goto :chk
)
set ""SCR=%~d0\_dism_scratch""
mkdir ""!SCR!"" 2>nul
echo Capturing !SRC! to !IMG! ...
dism /Capture-Image /ImageFile:""!IMG!"" /CaptureDir:!SRC!\ /Name:""@@FILE@@"" /Description:""@@FILE@@"" /Compress:@@COMPRESS@@ /Verify /ScratchDir:""!SCR!""
set ""RC=!errorlevel!""
rd /s /q ""!SCR!"" 2>nul
if not ""!RC!""==""0"" echo FAILED, error !RC! & goto :end
echo.
echo DONE: !IMG!
:end
echo.
pause
";

    public static string Build(string destFolder, string name, string compress)
    {
        string dest = destFolder.Trim().Trim('"').TrimEnd('\\');
        string rel = dest.Length > 3 ? dest.Substring(3) : "";
        string dirLine = rel.Length > 0 ? "%~d0\\" + rel : "%~d0";
        string cmd = Template.Replace("@@DIRLINE@@", dirLine).Replace("@@FILE@@", name).Replace("@@COMPRESS@@", compress);
        return Regex.Replace(cmd, "\r?\n", "\r\n");
    }

    // Puts capture.cmd in the root of the destination drive; the images go to the chosen folder.
    public static string Write(string destFolder, string name, string compress)
    {
        string dest = destFolder.Trim().Trim('"').TrimEnd('\\');
        string path = Path.Combine(dest.Substring(0, 3), "capture.cmd");
        File.WriteAllText(path, Build(destFolder, name, compress), Encoding.ASCII);
        return path;
    }
}

// ---------------------------------------------------------------------------------------------------
// WinPE side: find Windows volumes, read Sysprep state, capture with DISM
// ---------------------------------------------------------------------------------------------------
class WinVolume
{
    public string Drive;     // "D:"
    public string Label;
    public long Total;
    public long Used;
    public string Title { get { return Drive + "  " + Label + "  (" + Fmt.Size(Total) + ", " + S.T("cap.used") + " " + Fmt.Size(Used) + ")"; } }
}

static class WinPeCapture
{
    public static List<WinVolume> FindWindows()
    {
        List<WinVolume> res = new List<WinVolume>();
        foreach (DriveInfo d in DriveInfo.GetDrives())
        {
            try
            {
                if (d.DriveType == DriveType.CDRom || !d.IsReady) continue;
                string drive = d.Name.Substring(0, 2).ToUpperInvariant();
                if (drive == "X:") continue;
                if (!File.Exists(d.Name + "Windows\\System32\\config\\SYSTEM")) continue;
                WinVolume v = new WinVolume();
                v.Drive = drive; v.Label = d.VolumeLabel; v.Total = d.TotalSize; v.Used = d.TotalSize - d.AvailableFreeSpace;
                res.Add(v);
            }
            catch (Exception) { }
        }
        return res;
    }

    // GeneralizationState of the offline Windows (7 = Sysprep generalize done), -1 if unknown.
    public static int GeneralizationState(string drive)
    {
        const string h = "HKLM\\OFFSYS_CAP";
        string o;
        try
        {
            if (Proc.Capture("reg.exe", "load " + h + " \"" + drive + "\\Windows\\System32\\config\\SYSTEM\"", out o) != 0) return -1;
            try
            {
                Proc.Capture("reg.exe", "query \"" + h + "\\Setup\\Status\\SysprepStatus\" /v GeneralizationState", out o);
                Match m = Regex.Match(o, @"GeneralizationState\s+REG_DWORD\s+0x([0-9a-fA-F]+)");
                if (m.Success) return Convert.ToInt32(m.Groups[1].Value, 16);
            }
            finally
            {
                GC.Collect();
                Proc.Capture("reg.exe", "unload " + h, out o);
            }
        }
        catch (Exception) { }
        return -1;
    }

    public static string SafeName(string s)
    {
        string n = Regex.Replace((s ?? "").Trim(), "[^A-Za-z0-9_.\\-]", "_");
        return n.Length == 0 ? "Windows_image" : n;
    }

    // Returns the path of the created image.
    public static string Capture(string src, string destFolder, string name, string compress, IProgress2 prog)
    {
        string dest = destFolder.Trim().Trim('"').TrimEnd('\\');
        string dd = dest.Substring(0, 2).ToUpperInvariant();
        if (dd == src.ToUpperInvariant()) throw new InvalidOperationException(S.F("cap.err.samedrive", src));
        if (dd == "X:") throw new InvalidOperationException(S.T("cap.err.ram"));
        Directory.CreateDirectory(dest);
        string img = Path.Combine(dest, name + ".wim");
        int k = 1;
        while (File.Exists(img)) { k++; img = Path.Combine(dest, name + "_" + k + ".wim"); }
        string scratch = Path.Combine(dd + "\\", "_dism_scratch");
        Directory.CreateDirectory(scratch);
        try
        {
            prog.Status(S.F("cap.capturing", src, img));
            prog.Indeterminate();
            int code = Proc.Run("dism.exe", "/Capture-Image /ImageFile:\"" + img + "\" /CaptureDir:" + src + "\\ /Name:\"" + name + "\" /Description:\"" + name + "\" /Compress:" + compress + " /Verify /ScratchDir:\"" + scratch + "\"", prog);
            if (code != 0) throw new InvalidOperationException(S.F("cap.err.dism", code));
        }
        finally { try { Directory.Delete(scratch, true); } catch (Exception) { } }
        prog.Percent(100);
        return img;
    }
}
