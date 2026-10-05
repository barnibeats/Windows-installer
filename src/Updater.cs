// Self-update from the GitHub releases of the repo. A release carries latest.json (version, url, size, sha256);
// the app downloads the new exe, verifies it and swaps it in after the app closes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

class UpdateInfo
{
    public string Tag;
    public string Url;
    public long Size;
    public string Sha256;
}

static class Updater
{
    public static string State = "idle";      // idle | checking | none | available | downloading | ready | error
    public static UpdateInfo Latest;
    public static string DownloadedPath;
    public static int DownloadPercent;
    public static string Error = "";

    public static event Action Changed;

    static void Raise(string state)
    {
        State = state;
        Action a = Changed;
        if (a != null) Ui.Post(a);
    }

    public static bool IsDevBuild { get { return AppInfo.Version.Contains("-"); } }

    public static WebClient NewClient()
    {
        WebClient wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = "WindowsInstaller/" + AppInfo.Version;
        return wc;
    }

    public static Version Parse(string s)
    {
        Match m = Regex.Match(s ?? "", @"(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (!m.Success) return new Version(0, 0, 0);
        int[] g = new int[3];
        for (int i = 0; i < 3; i++) g[i] = m.Groups[i + 1].Success ? int.Parse(m.Groups[i + 1].Value) : 0;
        return new Version(g[0], g[1], g[2]);
    }

    // Checks the latest release; when newer and the exe can be replaced, downloads it in the background.
    public static void CheckAsync(bool download)
    {
        if (State == "checking" || State == "downloading") return;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate { Check(download); });
    }

    static void Check(bool download)
    {
        Raise("checking");
        try
        {
            UpdateInfo info = FetchLatest();
            if (info == null) { Error = S.T("upd.err.net"); Raise("error"); return; }
            Latest = info;
            if (Parse(info.Tag) <= Parse(AppInfo.Version)) { Raise("none"); return; }
            Raise("available");
            if (!download || IsDevBuild) return;
            Download();
        }
        catch (Exception ex) { Error = ex.Message; Raise("error"); }
    }

    static UpdateInfo FetchLatest()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;   // TLS 1.2 for GitHub
        JavaScriptSerializer json = new JavaScriptSerializer();
        try
        {
            using (WebClient wc = NewClient())
            {
                string text = wc.DownloadString("https://github.com/" + AppInfo.Repo + "/releases/latest/download/latest.json");
                Dictionary<string, object> j = json.Deserialize<Dictionary<string, object>>(text);
                UpdateInfo u = new UpdateInfo();
                u.Tag = Convert.ToString(j["version"]);
                u.Url = Convert.ToString(j["url"]);
                u.Size = Convert.ToInt64(j["size"]);
                u.Sha256 = j.ContainsKey("sha256") ? Convert.ToString(j["sha256"]) : "";
                return u;
            }
        }
        catch (Exception) { }
        try   // fallback: the GitHub API
        {
            using (WebClient wc = NewClient())
            {
                Dictionary<string, object> rel = json.Deserialize<Dictionary<string, object>>(wc.DownloadString("https://api.github.com/repos/" + AppInfo.Repo + "/releases/latest"));
                Dictionary<string, object> asset = ((ArrayList)rel["assets"]).OfType<Dictionary<string, object>>()
                    .FirstOrDefault(delegate(Dictionary<string, object> a) { return Convert.ToString(a["name"]).EndsWith(".exe", StringComparison.OrdinalIgnoreCase); });
                if (asset == null) return null;
                UpdateInfo u = new UpdateInfo();
                u.Tag = Convert.ToString(rel["tag_name"]);
                u.Url = Convert.ToString(asset["browser_download_url"]);
                u.Size = Convert.ToInt64(asset["size"]);
                u.Sha256 = "";
                return u;
            }
        }
        catch (Exception) { return null; }
    }

    public static bool CanWriteNextTo(string exe)
    {
        try
        {
            string probe = Path.Combine(Path.GetDirectoryName(exe), ".installer-write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception) { return false; }
    }

    public static void Download()
    {
        UpdateInfo u = Latest;
        if (u == null) return;
        if (!CanWriteNextTo(Application.ExecutablePath)) { Raise("available"); return; }
        Raise("downloading");
        try
        {
            string dir = Path.Combine(Paths.DataDir, "update");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "WindowsInstaller-" + Regex.Replace(u.Tag, @"[^\w.-]", "") + ".exe");
            if (!File.Exists(path) || new FileInfo(path).Length != u.Size)
            {
                using (WebClient wc = NewClient())
                {
                    System.Threading.ManualResetEvent done = new System.Threading.ManualResetEvent(false);
                    Exception failure = null;
                    wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e) { DownloadPercent = e.ProgressPercentage; Action a = Changed; if (a != null) Ui.Post(a); };
                    wc.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e) { failure = e.Error; done.Set(); };
                    wc.DownloadFileAsync(new Uri(u.Url), path);
                    done.WaitOne();
                    if (failure != null) throw failure;
                }
            }
            if (!IsTrusted(path, u))
            {
                try { File.Delete(path); } catch (Exception) { }
                Error = S.T("upd.err.verify");
                Raise("error");
                return;
            }
            DownloadedPath = path;
            Raise("ready");
        }
        catch (Exception ex) { Error = ex.Message; Raise("error"); }
    }

    // The new exe must be complete, match the published SHA-256, report the advertised version and,
    // if this exe is signed, carry the same signature.
    static bool IsTrusted(string path, UpdateInfo u)
    {
        try
        {
            if (new FileInfo(path).Length != u.Size) return false;
            using (FileStream fs = File.OpenRead(path))
                if (fs.ReadByte() != 'M' || fs.ReadByte() != 'Z') return false;
            if (!string.IsNullOrEmpty(u.Sha256))
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(path))
                {
                    string h = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
                    if (!h.Equals(u.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            if (Parse(FileVersionInfo.GetVersionInfo(path).ProductVersion) != Parse(u.Tag)) return false;
            string mine = SignerOf(Application.ExecutablePath);
            return mine == null || mine == SignerOf(path);
        }
        catch (Exception) { return false; }
    }

    static string SignerOf(string file)
    {
        try { return X509Certificate.CreateFromSignedFile(file).Subject; } catch (Exception) { return null; }
    }

    // Swaps the exe once this process has exited (a running exe cannot be overwritten). Uses cmd, which exists in WinPE too.
    public static bool ApplyOnExit(bool relaunch)
    {
        if (DownloadedPath == null || !File.Exists(DownloadedPath)) return false;
        string exe = Application.ExecutablePath;
        string script = Path.Combine(Paths.DataDir, "update", "apply.cmd");
        StringBuilder b = new StringBuilder();
        b.AppendLine("@echo off");
        b.AppendLine("set N=0");
        b.AppendLine(":retry");
        b.AppendLine("ping 127.0.0.1 -n 2 >nul");
        b.AppendLine("copy /y \"" + DownloadedPath + "\" \"" + exe + "\" >nul 2>&1");
        b.AppendLine("if errorlevel 1 (");
        b.AppendLine("  set /a N+=1");
        b.AppendLine("  if %N% LSS 40 goto retry");
        b.AppendLine("  goto done");
        b.AppendLine(")");
        b.AppendLine("del \"" + DownloadedPath + "\" >nul 2>&1");
        if (relaunch) b.AppendLine("start \"\" \"" + exe + "\"");
        b.AppendLine(":done");
        b.AppendLine("del \"%~f0\" >nul 2>&1");
        try
        {
            File.WriteAllText(script, b.ToString(), Proc.Oem());
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            Process.Start(psi);
            return true;
        }
        catch (Exception) { return false; }
    }
}
