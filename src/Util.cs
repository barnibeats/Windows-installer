// Small shared helpers: UI thread marshalling, data folder, persisted settings, formatting.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

static class Ui
{
    public static Control Main;

    // Runs an action on the UI thread without waiting.
    public static void Post(Action a)
    {
        Control c = Main;
        if (c == null || c.IsDisposed || !c.IsHandleCreated) return;
        try { c.BeginInvoke(a); } catch (Exception) { }
    }
}

static class Paths
{
    static string dir;

    // Per-user data folder (settings, downloaded updates). Falls back to the temp folder in WinPE.
    public static string DataDir
    {
        get
        {
            if (dir != null) return dir;
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "WindowsInstaller"),
                Path.Combine(Path.GetTempPath(), "WindowsInstaller")
            };
            foreach (string c in candidates)
            {
                if (c.StartsWith("\\") || !Path.IsPathRooted(c)) continue;
                try { Directory.CreateDirectory(c); dir = c; return dir; } catch (Exception) { }
            }
            dir = Path.GetTempPath();
            return dir;
        }
    }

    public static string Drive(string path)
    {
        try { return Path.GetPathRoot(path).Substring(0, 2).ToUpperInvariant(); } catch (Exception) { return ""; }
    }
}

// key=value file in the data folder: theme and language.
static class Settings
{
    static Dictionary<string, string> d;

    static string FilePath { get { return Path.Combine(Paths.DataDir, "settings.ini"); } }

    static void Load()
    {
        if (d != null) return;
        d = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                int i = line.IndexOf('=');
                if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
        }
        catch (Exception) { }
    }

    public static string Get(string key, string def)
    {
        Load();
        string v;
        return d.TryGetValue(key, out v) ? v : def;
    }

    public static void Set(string key, string value)
    {
        Load();
        d[key] = value;
        try
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, string> kv in d) lines.Add(kv.Key + "=" + kv.Value);
            File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
        }
        catch (Exception) { }
    }
}

static class Fmt
{
    public static string Size(double bytes)
    {
        if (bytes >= 1099511627776.0) return string.Format(CultureInfo.CurrentCulture, "{0:N2} {1}", bytes / 1099511627776.0, S.T("u.tb"));
        return string.Format(CultureInfo.CurrentCulture, "{0:N1} {1}", bytes / 1073741824.0, S.T("u.gb"));
    }
}

// Receives progress and text from long operations. Implementations marshal to the UI thread themselves.
interface IProgress2
{
    void Log(string line);
    void Percent(int percent);      // 0-100
    void Indeterminate();           // marquee
    void Status(string text);
}
