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

    // Tiny JSON field readers: the files are small and flat, and this keeps the exe free of extra assemblies
    // (WinPE often lacks System.Web.Extensions).
    static string JStr(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        return m.Success ? m.Groups[1].Value.Replace("\\/", "/").Replace("\\\"", "\"").Replace("\\\\", "\\") : "";
    }

    static long JNum(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)");
        long v;
        return m.Success && long.TryParse(m.Groups[1].Value, out v) ? v : 0;
    }

    public static UpdateInfo FetchLatest()
    {
        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls; } catch (Exception) { }   // TLS 1.2 for GitHub
        try
        {
            using (WebClient wc = NewClient())
            {
                string text = wc.DownloadString("https://github.com/" + AppInfo.Repo + "/releases/latest/download/latest.json");
                UpdateInfo u = new UpdateInfo();
                u.Tag = JStr(text, "version");
                u.Url = JStr(text, "url");
                u.Size = JNum(text, "size");
                u.Sha256 = JStr(text, "sha256");
                if (u.Tag.Length > 0 && u.Url.Length > 0 && u.Size > 0) return u;
            }
        }
        catch (Exception) { }
        try   // fallback: the GitHub API
        {
            using (WebClient wc = NewClient())
            {
                string rel = wc.DownloadString("https://api.github.com/repos/" + AppInfo.Repo + "/releases/latest");
                Match asset = Regex.Match(rel, "\\{[^{}]*\"name\"\\s*:\\s*\"[^\"]*\\.exe\"[^{}]*\\}");
                if (!asset.Success) return null;
                UpdateInfo u = new UpdateInfo();
                u.Tag = JStr(rel, "tag_name");
                u.Url = JStr(asset.Value, "browser_download_url");
                u.Size = JNum(asset.Value, "size");
                u.Sha256 = "";
                return u.Tag.Length > 0 && u.Url.Length > 0 ? u : null;
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
