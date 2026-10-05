// Windows deployment settings: the answer file (unattend.xml) and the first-logon script (pretail.cmd) are built
// from them. Passwords live in memory only: they are never written to a profile, a log or the capture image.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

class WinSettings
{
    // identity and regional
    public string Organization = "P.RETAIL LLC";
    public string Owner = "INSTALLER";
    public string TimeZone = "FLE Standard Time";
    public string InputLocale = "0409:00000409;0419:00000419;0422:00020422";
    public string UiLanguage = "";          // empty = not set in the answer file
    public string SystemLocale = "";
    public string UserLocale = "";
    // network and screen
    public string Dns = "8.8.8.8";          // comma separated, empty = leave as is
    public string Adapter = "Ethernet";
    public int ResX = 1024;                 // 0 = leave as is
    public int ResY = 768;
    // accounts
    public bool EnableAdmin = true;
    public string AdminName = "Администратор";
    public string AdminPassword = "";       // memory only
    public string UserName = "Пользователь";   // empty = no extra account
    public string UserDescription = "Учетная запись пользователя P.RETAIL";
    public string UserPassword = "";        // memory only
    public bool AutoLogon = true;
    public int LogonCount = 1;
    // first-logon script
    public int RdpPort = 4444;              // 0 = do not touch Remote Desktop
    public bool Icmp = true;
    public bool DisableIpv6 = true;
    public bool DisableTelemetry = true;
    public bool DisableHibernate = true;
    public bool DesktopIcons = true;
    public string Kms = "k.motto.ua:9876";  // empty = no activation
    public string Gvlk = "";                // optional KMS client key

    static readonly Regex Ipv4 = new Regex(@"^\d{1,3}(\.\d{1,3}){3}$");
    static readonly Regex KmsRx = new Regex(@"^[A-Za-z0-9.\-]+(:\d{1,5})?$");
    static readonly Regex GvlkRx = new Regex(@"^[A-Za-z0-9]{5}(-[A-Za-z0-9]{5}){4}$");

    public static bool ValidName(string n)
    {
        return n.Length > 0 && n.Length <= 20 && n.IndexOfAny("\"/\\[]:;|=,+*?<>@".ToCharArray()) < 0 && n.Trim().Length == n.Length;
    }

    // Returns the key of the error text, or null when the settings are usable. Passwords are checked when withSecrets.
    public string Validate(bool withSecrets)
    {
        if (TimeZone.Trim().Length == 0) return "win.err.tz";
        if (Dns.Trim().Length > 0)
            foreach (string d in Dns.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (!Ipv4.IsMatch(d)) return "win.err.dns";
        if (RdpPort < 0 || RdpPort > 65535) return "win.err.port";
        if (Kms.Trim().Length > 0 && !KmsRx.IsMatch(Kms.Trim())) return "win.err.kms";
        if (Gvlk.Trim().Length > 0 && !GvlkRx.IsMatch(Gvlk.Trim())) return "win.err.gvlk";
        if (LogonCount < 1 || LogonCount > 999) return "win.err.logon";
        if (ResX < 0 || ResY < 0 || (ResX == 0) != (ResY == 0)) return "win.err.res";
        if (UserName.Length > 0 && !ValidName(UserName)) return "win.err.user";
        if ((EnableAdmin || AutoLogon) && !ValidName(AdminName)) return "win.err.admin";
        if (withSecrets)
        {
            if (AutoLogon && AdminPassword.Length == 0) return "win.err.adminpw";
            if (UserName.Length > 0 && UserPassword.Length == 0) return "win.err.userpw";
        }
        return null;
    }

    // ---- profile file: key=value, everything except the passwords ----------------------------------
    static bool Persisted(FieldInfo f) { return !f.Name.EndsWith("Password"); }

    public void Save(string path)
    {
        List<string> lines = new List<string>();
        foreach (FieldInfo f in typeof(WinSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!Persisted(f)) continue;
            object v = f.GetValue(this);
            string s = v is bool ? ((bool)v ? "1" : "0") : Convert.ToString(v);
            lines.Add(f.Name + "=" + s.Replace("\r", " ").Replace("\n", " "));
        }
        File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
    }

    public static WinSettings Load(string path)
    {
        WinSettings w = new WinSettings();
        foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
        {
            int i = line.IndexOf('=');
            if (i <= 0) continue;
            FieldInfo f = typeof(WinSettings).GetField(line.Substring(0, i).Trim(), BindingFlags.Public | BindingFlags.Instance);
            if (f == null || !Persisted(f)) continue;
            string s = line.Substring(i + 1);
            try
            {
                if (f.FieldType == typeof(bool)) f.SetValue(w, s.Trim() == "1");
                else if (f.FieldType == typeof(int)) f.SetValue(w, int.Parse(s.Trim()));
                else f.SetValue(w, s);
            }
            catch (Exception) { }
        }
        return w;
    }
}

// The settings shared by the Install and Capture tabs; the last used ones are remembered (without passwords).
static class WinProfile
{
    static WinSettings cur;

    static string FilePath { get { return Path.Combine(Paths.DataDir, "windows-settings.ini"); } }

    public static WinSettings Current
    {
        get
        {
            if (cur == null)
            {
                try { cur = File.Exists(FilePath) ? WinSettings.Load(FilePath) : new WinSettings(); } catch (Exception) { cur = new WinSettings(); }
            }
            return cur;
        }
        set
        {
            cur = value;
            try { cur.Save(FilePath); } catch (Exception) { }
        }
    }
}

static class WinDeploy
{
    const string FirstLogonPath = @"C:\WINDOWS\system32\pretail.cmd";

    static string WinDir { get { return Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows"; } }

    static string Esc(string s) { return SecurityElement.Escape(s); }

    static string Enc(string ps) { return Convert.ToBase64String(Encoding.Unicode.GetBytes(ps)); }

    static string Q(string s) { return s.Replace("'", "''"); }

    // unattend password encoding: Base64 of UTF-16 (password + suffix). It is an encoding, not encryption.
    static string Pw(string pw, string suffix) { return Convert.ToBase64String(Encoding.Unicode.GetBytes(pw + suffix)); }

    static void El(StringBuilder x, int ind, string name, string val)
    {
        x.AppendLine(new string(' ', ind) + "<" + name + ">" + Esc(val) + "</" + name + ">");
    }

    // ---- answer file --------------------------------------------------------------------------------
    // withSecrets = false: no passwords, no accounts, no auto logon (used for the capture image).
    public static string BuildXml(WinSettings w, bool withSecrets, string arch)
    {
        string ns = "xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"";
        string head = "processorArchitecture=\"" + arch + "\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" " + ns;
        StringBuilder x = new StringBuilder();
        x.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        x.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

        // specialize
        x.AppendLine("  <settings pass=\"specialize\">");
        x.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" " + head + ">");
        x.AppendLine("      <CopyProfile>false</CopyProfile>");
        if (w.Organization.Length > 0) El(x, 6, "RegisteredOrganization", w.Organization);
        if (w.Owner.Length > 0) El(x, 6, "RegisteredOwner", w.Owner);
        if (w.TimeZone.Length > 0) El(x, 6, "TimeZone", w.TimeZone);
        x.AppendLine("    </component>");
        if (w.Dns.Trim().Length > 0)
        {
            x.AppendLine("    <component name=\"Microsoft-Windows-DNS-Client\" " + head + ">");
            x.AppendLine("      <Interfaces>");
            x.AppendLine("        <Interface wcm:action=\"add\">");
            x.AppendLine("          <DNSServerSearchOrder>");
            int n = 0;
            foreach (string d in w.Dns.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                n++;
                x.AppendLine("            <IpAddress wcm:action=\"add\" wcm:keyValue=\"" + n + "\">" + Esc(d) + "</IpAddress>");
            }
            x.AppendLine("          </DNSServerSearchOrder>");
            x.AppendLine("          <DNSDomain></DNSDomain>");
            x.AppendLine("          <EnableAdapterDomainNameRegistration>false</EnableAdapterDomainNameRegistration>");
            El(x, 10, "Identifier", w.Adapter);
            x.AppendLine("        </Interface>");
            x.AppendLine("      </Interfaces>");
            x.AppendLine("    </component>");
        }
        x.AppendLine("    <component name=\"Microsoft-Windows-ErrorReportingCore\" " + head + ">");
        x.AppendLine("      <DisableWER>1</DisableWER>");
        x.AppendLine("    </component>");
        if (w.EnableAdmin)
        {
            // the built-in Administrator is found by its SID (-500), so the account name does not depend on the language
            // Path is limited to 259 characters by the answer file schema, so the command is kept short
            string ps = "Get-LocalUser|?{$_.SID -like '*-500'}|Enable-LocalUser";
            x.AppendLine("    <component name=\"Microsoft-Windows-Deployment\" " + head + ">");
            x.AppendLine("      <RunSynchronous>");
            x.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
            x.AppendLine("          <Order>1</Order>");
            El(x, 10, "Path", "powershell.exe -NoP -EP Bypass -EC " + Enc(ps));
            x.AppendLine("        </RunSynchronousCommand>");
            x.AppendLine("      </RunSynchronous>");
            x.AppendLine("    </component>");
        }
        x.AppendLine("  </settings>");

        // generalize
        x.AppendLine("  <settings pass=\"generalize\">");
        x.AppendLine("    <component name=\"Microsoft-Windows-Security-SPP\" " + head + ">");
        x.AppendLine("      <SkipRearm>1</SkipRearm>");
        x.AppendLine("    </component>");
        x.AppendLine("  </settings>");

        // oobeSystem
        bool logon = withSecrets && w.AutoLogon && w.AdminPassword.Length > 0;
        x.AppendLine("  <settings pass=\"oobeSystem\">");
        x.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" " + head + ">");
        x.AppendLine("      <OOBE>");
        x.AppendLine("        <HideEULAPage>true</HideEULAPage>");
        x.AppendLine("        <NetworkLocation>Work</NetworkLocation>");
        x.AppendLine("        <ProtectYourPC>3</ProtectYourPC>");
        x.AppendLine("        <HideLocalAccountScreen>true</HideLocalAccountScreen>");
        x.AppendLine("        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>");
        x.AppendLine("        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
        x.AppendLine("        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>");
        x.AppendLine("      </OOBE>");
        x.AppendLine("      <BluetoothTaskbarIconEnabled>false</BluetoothTaskbarIconEnabled>");
        x.AppendLine("      <ShowWindowsLive>false</ShowWindowsLive>");
        if (w.TimeZone.Length > 0) El(x, 6, "TimeZone", w.TimeZone);
        if (logon)
        {
            x.AppendLine("      <AutoLogon>");
            x.AppendLine("        <Password>");
            x.AppendLine("          <Value>" + Pw(w.AdminPassword, "Password") + "</Value>");
            x.AppendLine("          <PlainText>false</PlainText>");
            x.AppendLine("        </Password>");
            El(x, 8, "Username", w.AdminName);
            x.AppendLine("        <Enabled>true</Enabled>");
            x.AppendLine("        <LogonCount>" + w.LogonCount + "</LogonCount>");
            x.AppendLine("      </AutoLogon>");
        }
        if (w.ResX > 0 && w.ResY > 0)
        {
            x.AppendLine("      <Display>");
            x.AppendLine("        <HorizontalResolution>" + w.ResX + "</HorizontalResolution>");
            x.AppendLine("        <VerticalResolution>" + w.ResY + "</VerticalResolution>");
            x.AppendLine("        <ColorDepth>32</ColorDepth>");
            x.AppendLine("      </Display>");
        }
        bool admPw = withSecrets && w.AdminPassword.Length > 0;
        bool user = withSecrets && w.UserName.Length > 0;
        if (admPw || user)
        {
            x.AppendLine("      <UserAccounts>");
            if (admPw)
            {
                x.AppendLine("        <AdministratorPassword>");
                x.AppendLine("          <PlainText>false</PlainText>");
                x.AppendLine("          <Value>" + Pw(w.AdminPassword, "AdministratorPassword") + "</Value>");
                x.AppendLine("        </AdministratorPassword>");
            }
            if (user)
            {
                x.AppendLine("        <LocalAccounts>");
                x.AppendLine("          <LocalAccount wcm:action=\"add\">");
                x.AppendLine("            <Password>");
                x.AppendLine("              <Value>" + Pw(w.UserPassword, "Password") + "</Value>");
                x.AppendLine("              <PlainText>false</PlainText>");
                x.AppendLine("            </Password>");
                El(x, 12, "Description", w.UserDescription);
                El(x, 12, "DisplayName", w.UserName);
                x.AppendLine("            <Group>Users</Group>");
                El(x, 12, "Name", w.UserName);
                x.AppendLine("          </LocalAccount>");
                x.AppendLine("        </LocalAccounts>");
            }
            x.AppendLine("      </UserAccounts>");
        }
        x.AppendLine("      <FirstLogonCommands>");
        x.AppendLine("        <SynchronousCommand wcm:action=\"add\">");
        x.AppendLine("          <CommandLine>" + Esc(FirstLogonPath) + "</CommandLine>");
        x.AppendLine("          <Order>1</Order>");
        x.AppendLine("          <RequiresUserInput>false</RequiresUserInput>");
        x.AppendLine("        </SynchronousCommand>");
        x.AppendLine("      </FirstLogonCommands>");
        x.AppendLine("      <DesktopOptimization>");
        x.AppendLine("        <ShowWindowsStoreAppsOnTaskbar>false</ShowWindowsStoreAppsOnTaskbar>");
        x.AppendLine("      </DesktopOptimization>");
        x.AppendLine("    </component>");
        if (w.InputLocale.Length > 0 || w.UiLanguage.Length > 0 || w.SystemLocale.Length > 0 || w.UserLocale.Length > 0)
        {
            x.AppendLine("    <component name=\"Microsoft-Windows-International-Core\" " + head + ">");
            if (w.InputLocale.Length > 0) El(x, 6, "InputLocale", w.InputLocale);
            if (w.SystemLocale.Length > 0) El(x, 6, "SystemLocale", w.SystemLocale);
            if (w.UiLanguage.Length > 0) El(x, 6, "UILanguage", w.UiLanguage);
            if (w.UserLocale.Length > 0) El(x, 6, "UserLocale", w.UserLocale);
            x.AppendLine("    </component>");
        }
        x.AppendLine("  </settings>");
        x.AppendLine("</unattend>");
        return x.ToString();
    }

    // ---- first-logon script ------------------------------------------------------------------------
    public static string BuildPretail(WinSettings w)
    {
        List<string> c = new List<string>();
        string log = ">> \"%LOG%\" 2>&1";
        c.Add("@echo off");
        c.Add("setlocal EnableExtensions");
        c.Add("chcp 65001 >nul");
        c.Add("set \"LOG=%WINDIR%\\Temp\\pretail.log\"");
        c.Add("echo ========================================================= > \"%LOG%\"");
        c.Add("echo pretail started %DATE% %TIME% >> \"%LOG%\"");
        c.Add("echo ========================================================= >> \"%LOG%\"");
        c.Add("");

        if (w.UserName.Length > 0)
        {
            string ps = "Set-StrictMode -Version Latest\n$user = '" + Q(w.UserName) + "'\ntry {\n  $lu = Get-LocalUser -Name $user -ErrorAction Stop\n" +
                "  Set-LocalUser -Name $user -PasswordNeverExpires $true -UserMayChangePassword $false\n" +
                "  wmic USERACCOUNT WHERE NAME=\"$user\" SET PasswordExpires=FALSE,PasswordChangeable=FALSE,PasswordRequired=FALSE | Out-Null\n} catch {}\n";
            c.Add("rem Local account policy (the name goes through an encoded PowerShell command, so any language works).");
            c.Add("powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Enc(ps) + " " + log);
            c.Add("");
        }

        if (w.DesktopIcons)
        {
            string[] guids = new string[] { "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}" };
            string np = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\HideDesktopIcons\\NewStartPanel";
            string adv = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced";
            c.Add("rem Standard desktop icons: for the current account and for the Default profile (every account created later).");
            foreach (string g in guids) c.Add("reg add \"HKCU\\" + np + "\" /v \"" + g + "\" /t REG_DWORD /d 0 /f " + log);
            c.Add("reg add \"HKCU\\" + adv + "\" /v HideIcons /t REG_DWORD /d 0 /f " + log);
            c.Add("reg load HKU\\WI_DEFAULT \"%SystemDrive%\\Users\\Default\\NTUSER.DAT\" " + log);
            c.Add("if not errorlevel 1 (");
            foreach (string g in guids) c.Add("  reg add \"HKU\\WI_DEFAULT\\" + np + "\" /v \"" + g + "\" /t REG_DWORD /d 0 /f " + log);
            c.Add("  reg add \"HKU\\WI_DEFAULT\\" + adv + "\" /v HideIcons /t REG_DWORD /d 0 /f " + log);
            c.Add("  reg unload HKU\\WI_DEFAULT " + log);
            c.Add(")");
            c.Add("echo Desktop shortcuts are not managed here. >> \"%LOG%\"");
            c.Add("");
        }

        c.Add("rem Network and system settings.");
        if (w.DisableIpv6)
        {
            c.Add("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\services\\TCPIP6\\Parameters\" /v DisabledComponents /t REG_DWORD /d 0xffffffff /f " + log);
            c.Add("netsh interface teredo set state disable " + log);
        }
        c.Add("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched\" /v NonBestEffortLimit /t REG_DWORD /d 0 /f " + log);
        c.Add("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\PasswordLess\\Device\" /v DevicePasswordLessBuildVersion /t REG_DWORD /d 0 /f " + log);
        if (w.Icmp)
        {
            c.Add("netsh advfirewall firewall delete rule name=\"P_RETAIL_ICMPv4_Echo_In\" " + log);
            c.Add("netsh advfirewall firewall add rule name=\"P_RETAIL_ICMPv4_Echo_In\" protocol=icmpv4:8,any dir=in action=allow enable=yes profile=any " + log);
        }
        if (w.RdpPort > 0)
        {
            c.Add("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\" /v fDenyTSConnections /t REG_DWORD /d 0 /f " + log);
            c.Add("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp\" /v PortNumber /t REG_DWORD /d " + w.RdpPort + " /f " + log);
            c.Add("netsh advfirewall firewall delete rule name=\"P_RETAIL_RDP_TCP_" + w.RdpPort + "\" " + log);
            c.Add("netsh advfirewall firewall add rule name=\"P_RETAIL_RDP_TCP_" + w.RdpPort + "\" dir=in action=allow enable=yes profile=any localport=" + w.RdpPort + " protocol=tcp " + log);
        }
        if (w.DisableHibernate) c.Add("powercfg.exe -H off " + log);
        if (w.DisableTelemetry)
        {
            c.Add("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f " + log);
            c.Add("sc stop DiagTrack " + log);
            c.Add("sc config DiagTrack start= disabled " + log);
        }
        c.Add("");

        if (w.Kms.Trim().Length > 0)
        {
            string chk = "$p = Get-CimInstance SoftwareLicensingProduct -Filter \"PartialProductKey IS NOT NULL AND Name LIKE 'Windows%'\" | Where-Object { $_.LicenseStatus -eq 1 }\nif ($p) { exit 0 } else { exit 1 }\n";
            c.Add("rem Activation through the corporate KMS server; retried while the network comes up.");
            if (w.Gvlk.Trim().Length > 0) c.Add("cscript //nologo \"%SystemRoot%\\system32\\slmgr.vbs\" /ipk " + w.Gvlk.Trim() + " " + log);
            c.Add("cscript //nologo \"%SystemRoot%\\system32\\slmgr.vbs\" /skms " + w.Kms.Trim() + " " + log);
            c.Add("set \"TRY=0\"");
            c.Add(":kms");
            c.Add("set /a TRY+=1");
            c.Add("cscript //nologo \"%SystemRoot%\\system32\\slmgr.vbs\" /ato " + log);
            c.Add("powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Enc(chk));
            c.Add("if errorlevel 1 if %TRY% LSS 5 (");
            c.Add("  echo Not activated yet, retry %TRY% >> \"%LOG%\"");
            c.Add("  ping -n 31 127.0.0.1 >nul");
            c.Add("  goto :kms");
            c.Add(")");
            c.Add("");
        }

        if (w.DesktopIcons)
        {
            c.Add("rem Restart Explorer only for the current first-logon session.");
            c.Add("taskkill /f /im explorer.exe " + log);
            c.Add("start explorer.exe");
            c.Add("");
        }

        c.Add("rem Remove every copy of the answer file and the stored auto logon password.");
        c.Add("del /f /q \"%WINDIR%\\Panther\\unattend.xml\" \"%WINDIR%\\Panther\\Unattend\\unattend.xml\" \"%WINDIR%\\Temp\\capture_unattend.xml\" >nul 2>&1");
        c.Add("del /f /q \"%WINDIR%\\System32\\Sysprep\\unattend*.xml\" >nul 2>&1");
        if (w.LogonCount == 1)
        {
            c.Add("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\" /v AutoAdminLogon /t REG_SZ /d 0 /f >nul 2>&1");
            c.Add("reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon\" /v DefaultPassword /f >nul 2>&1");
        }
        c.Add("echo pretail finished %DATE% %TIME% >> \"%LOG%\"");
        c.Add("rem The script removes itself; this line must stay last.");
        c.Add("(goto) 2>nul & del /f /q \"%~f0\"");
        return string.Join("\r\n", c.ToArray()) + "\r\n";
    }

    // ---- installing: write both files into the freshly applied Windows ------------------------------
    // winRoot is the target volume root, e.g. "W:\". The XML goes to Windows\Panther\Unattend, the first place
    // Windows looks for it on the first start (it wins over a copy left by Sysprep).
    public static void ApplyToTarget(string winRoot, WinSettings w, IProgress2 prog)
    {
        string win = Path.Combine(winRoot, "Windows");
        string dir = Path.Combine(win, "Panther\\Unattend");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "unattend.xml"), BuildXml(w, true, Unattend.Arch()), new UTF8Encoding(false));
        try { File.Delete(Path.Combine(win, "Panther\\unattend.xml")); } catch (Exception) { }
        prog.Log(S.T("win.log.xml"));
        File.WriteAllText(Path.Combine(win, "System32\\pretail.cmd"), BuildPretail(w), Encoding.ASCII);
        prog.Log(S.T("win.log.pretail"));
    }

    // ---- capture: an answer file without any secrets plus the script inside the image ----------------
    public static string PrepareCapture(WinSettings w, IProgress2 prog)
    {
        string path = Path.Combine(WinDir, "Temp\\capture_unattend.xml");
        File.WriteAllText(path, BuildXml(w, false, Unattend.Arch()), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(WinDir, "System32\\pretail.cmd"), BuildPretail(w), Encoding.ASCII);
        prog.Log(S.T("win.log.cap"));
        return path;
    }

    // ---- capture: cleanup before Sysprep ------------------------------------------------------------
    public static void PreSysprepCleanup(IProgress2 prog)
    {
        prog.Log(S.T("cap.clean.dism"));
        Proc.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", prog);

        prog.Log(S.T("cap.clean.temp"));
        foreach (string d in new string[] { Path.GetTempPath(), Path.Combine(WinDir, "Temp") })
        {
            try
            {
                foreach (string f in Directory.GetFiles(d)) { try { File.Delete(f); } catch (Exception) { } }
                foreach (string s in Directory.GetDirectories(d)) { try { Directory.Delete(s, true); } catch (Exception) { } }
            }
            catch (Exception) { }
        }

        prog.Log(S.T("cap.clean.events"));
        string names;
        Proc.Capture("wevtutil.exe", "el", out names);
        foreach (string n in names.Split('\n'))
        {
            string name = n.Trim();
            if (name.Length == 0) continue;
            string o;
            try { Proc.Capture("wevtutil.exe", "cl \"" + name + "\"", out o); } catch (Exception) { }
        }

        prog.Log(S.T("cap.clean.panther"));
        foreach (string d in new string[] { Path.Combine(WinDir, "Panther"), Path.Combine(WinDir, "System32\\Sysprep\\Panther") })
        {
            try { foreach (string f in Directory.GetFiles(d, "*.log")) { try { File.Delete(f); } catch (Exception) { } } }
            catch (Exception) { }
        }
    }
}
