// Windows images: finding ISO files, opening an ISO (mount or extract), listing editions with DISM.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

class Edition
{
    public int Index;
    public string Name = "";
    public string Description = "";
    public string Title
    {
        get
        {
            string n = Name.Length > 0 ? Name : (Description.Length > 0 ? Description : S.T("img.noname"));
            return n + "  (" + S.T("img.index") + " " + Index + ")";
        }
    }
}

// An opened image source: a mounted ISO, an extracted install.wim/esd, or a plain wim/esd file.
class ImageSource : IDisposable
{
    public string SourcePath;     // iso / wim / esd chosen by the user
    public string ImageFile;      // install.wim / install.esd to apply
    public string MountLetter;    // "E" when an ISO is mounted
    public string ExtractDir;     // when extracted with 7-Zip
    IntPtr handle = IntPtr.Zero;

    public void SetHandle(IntPtr h) { handle = h; }

    public void Dispose()
    {
        Native.Detach(handle);
        handle = IntPtr.Zero;
        if (MountPath != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Dismount-DiskImage -ImagePath '" + MountPath.Replace("'", "''") + "'\"")
                { CreateNoWindow = true, UseShellExecute = false }).WaitForExit(8000);
            }
            catch (Exception) { }
            MountPath = null;
        }
        if (ExtractDir != null)
        {
            try { Directory.Delete(ExtractDir, true); } catch (Exception) { }
            ExtractDir = null;
        }
    }

    public string MountPath;      // set when mounted through PowerShell (needs a dismount)
}

static class Images
{
    static readonly Regex IndexRx = new Regex(@"^\s*Index\s*:\s*(\d+)", RegexOptions.IgnoreCase);
    static readonly Regex NameRx = new Regex(@"^\s*Name\s*:\s*(.*)$", RegexOptions.IgnoreCase);
    static readonly Regex DescRx = new Regex(@"^\s*Description\s*:\s*(.*)$", RegexOptions.IgnoreCase);

    public static bool IsPlainImage(string path)
    {
        string e = Path.GetExtension(path).ToLowerInvariant();
        return e == ".wim" || e == ".esd";
    }

    // ---- searching ----------------------------------------------------------------------------------
    public static List<string> FindIsos(string where, Action<string> progress)
    {
        List<string> roots = new List<string>();
        if (!string.IsNullOrEmpty(where) && Directory.Exists(where)) roots.Add(where);
        else
        {
            string sys = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:").ToUpperInvariant();
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType == DriveType.CDRom) continue;
                    if (d.Name.Substring(0, 2).ToUpperInvariant() == sys) continue;
                    roots.Add(d.Name);
                }
                catch (Exception) { }
            }
        }
        List<string> found = new List<string>();
        foreach (string r in roots)
        {
            if (progress != null) progress(r);
            Walk(r, found, 0);
        }
        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    static void Walk(string dir, List<string> found, int depth)
    {
        if (depth > 12) return;
        try
        {
            foreach (string f in Directory.GetFiles(dir, "*.iso")) found.Add(f);
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string n = Path.GetFileName(sub);
                if (n.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) || n.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(sub, found, depth + 1);
            }
        }
        catch (Exception) { }
    }

    // ---- opening ------------------------------------------------------------------------------------
    static List<string> Letters()
    {
        List<string> l = new List<string>();
        foreach (DriveInfo d in DriveInfo.GetDrives()) l.Add(d.Name.Substring(0, 1).ToUpperInvariant());
        return l;
    }

    static string FindImageOn(string root)
    {
        foreach (string n in new string[] { "install.wim", "install.esd" })
        {
            string p = Path.Combine(root, "sources", n);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    static string WaitForNewLetter(List<string> before, int seconds)
    {
        for (int i = 0; i < seconds * 2; i++)
        {
            foreach (string l in Letters())
                if (!before.Contains(l))
                {
                    try { if (new DriveInfo(l).IsReady) return l; } catch (Exception) { }
                }
            Thread.Sleep(500);
        }
        return null;
    }

    public static string Find7Zip()
    {
        List<string> c = new List<string>();
        c.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe"));
        c.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools\\7z.exe"));
        foreach (string pf in new string[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "X:\\Program Files", "X:\\Program Files (x86)" })
            if (!string.IsNullOrEmpty(pf)) c.Add(Path.Combine(pf, "7-Zip\\7z.exe"));
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in path.Split(';'))
            if (dir.Trim().Length > 0) c.Add(Path.Combine(dir.Trim(), "7z.exe"));
        foreach (string f in c) { try { if (File.Exists(f)) return f; } catch (Exception) { } }
        return null;
    }

    // Opens a chosen file: wim/esd directly; iso by mounting (virtdisk, then PowerShell) or extracting with 7-Zip.
    public static ImageSource Open(string path, IProgress2 prog)
    {
        ImageSource src = new ImageSource();
        src.SourcePath = path;
        if (IsPlainImage(path)) { src.ImageFile = path; return src; }

        List<string> before = Letters();
        string letter = null;
        string err = "";

        // 1) virtdisk API
        int rc;
        IntPtr h = Native.AttachIso(path, out rc);
        if (h != IntPtr.Zero)
        {
            src.SetHandle(h);
            letter = WaitForNewLetter(before, 12);
            if (letter == null) { src.Dispose(); src = new ImageSource(); src.SourcePath = path; }
        }
        else err = "virtdisk error " + rc;

        // 2) PowerShell Mount-DiskImage
        if (letter == null)
        {
            try
            {
                string o;
                int code = Proc.Capture("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Mount-DiskImage -ImagePath '" + path.Replace("'", "''") + "'\"", out o);
                if (code == 0)
                {
                    src.MountPath = path;
                    letter = WaitForNewLetter(before, 12);
                }
                else err += "; " + o.Trim();
            }
            catch (Exception ex) { err += "; " + ex.Message; }
        }

        if (letter != null)
        {
            src.MountLetter = letter;
            src.ImageFile = FindImageOn(letter + ":\\");
            if (src.ImageFile == null) { src.Dispose(); throw new InvalidOperationException(S.T("img.noinstall")); }
            return src;
        }

        // 3) extract install.wim/esd with 7-Zip
        string sz = Find7Zip();
        if (sz == null) throw new InvalidOperationException(S.T("img.cantmount") + "\r\n\r\n" + err);
        string root = Path.GetPathRoot(path);
        long free = new DriveInfo(root).AvailableFreeSpace;
        if (free < new FileInfo(path).Length) throw new InvalidOperationException(S.F("img.nospace", root));
        string dest = Path.Combine(root, "_wininst_tmp");
        src.ExtractDir = dest;
        prog.Status(S.T("img.extracting"));
        prog.Indeterminate();
        Proc.Run(sz, "e \"" + path + "\" -o\"" + dest + "\" sources\\install.wim sources\\install.esd -y", prog);
        foreach (string n in new string[] { "install.wim", "install.esd" })
            if (File.Exists(Path.Combine(dest, n))) { src.ImageFile = Path.Combine(dest, n); break; }
        if (src.ImageFile == null) { src.Dispose(); throw new InvalidOperationException(S.T("img.noinstall")); }
        return src;
    }

    // ---- editions -----------------------------------------------------------------------------------
    public static List<Edition> ListEditions(string imageFile)
    {
        string output;
        Proc.Capture("dism.exe", "/English /Get-WimInfo /WimFile:\"" + imageFile + "\"", out output);
        return ParseEditions(output);
    }

    // Parses the text of "dism /English /Get-WimInfo".
    public static List<Edition> ParseEditions(string output)
    {
        List<Edition> list = new List<Edition>();
        Edition cur = null;
        foreach (string raw in output.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            Match m = IndexRx.Match(line);
            if (m.Success) { cur = new Edition(); cur.Index = int.Parse(m.Groups[1].Value); list.Add(cur); continue; }
            if (cur == null) continue;
            m = NameRx.Match(line);
            if (m.Success && cur.Name.Length == 0) { cur.Name = m.Groups[1].Value.Trim(); continue; }
            m = DescRx.Match(line);
            if (m.Success && cur.Description.Length == 0) cur.Description = m.Groups[1].Value.Trim();
        }
        if (list.Count == 0)
        {
            string[] lines = output.Trim().Split('\n');
            string tail = string.Join(" ", lines, Math.Max(0, lines.Length - 3), Math.Min(3, lines.Length));
            throw new InvalidOperationException(S.T("img.dismfail") + " " + tail.Trim());
        }
        return list;
    }

    public static Edition PreferPro(List<Edition> list)
    {
        foreach (Edition e in list) if (Regex.IsMatch(e.Name, @"\bPro$", RegexOptions.IgnoreCase) || Regex.IsMatch(e.Name, @"\bPro\b", RegexOptions.IgnoreCase)) return e;
        return list[0];
    }
}
