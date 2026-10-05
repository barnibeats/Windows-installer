// Physical disk enumeration through WMI (works in WinPE, where the PowerShell Storage module is missing).
// Primary path: System.Management. If that assembly is missing in the environment (some WinPE builds),
// the same data is read through PowerShell (Get-CimInstance / Get-WmiObject).
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

class DiskInfo
{
    public int Number;
    public string Model;
    public long Size;
    public string Bus;
    public string Style;                 // GPT / MBR / RAW
    public string PnpId = "";            // language-neutral device id, e.g. SCSI\DISK&VEN_MSFT&PROD_VIRTUAL_DISK\...
    public List<string> Volumes = new List<string>();
    public bool Blocked;
    public string BlockReason = "";

    public string VolumesText { get { return string.Join(",  ", Volumes.ToArray()); } }
}

// Raw rows, independent of how they were read.
class RawDrive { public int Index; public long Size; public string Model = "", Interface = "", Pnp = ""; }
class RawPartition { public int DiskIndex; public string Type = ""; }
class RawData
{
    public List<RawDrive> Drives = new List<RawDrive>();
    public List<RawPartition> Partitions = new List<RawPartition>();
    public Dictionary<string, int> Letters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // "C:" -> disk
}

static class Disks
{
    static readonly Regex DiskNo = new Regex(@"Disk #(\d+)");
    static readonly Regex Letter = new Regex(@"([A-Za-z]:)""?$");

    static void AddMap(RawData d, string antecedent, string dependent)
    {
        Match m1 = DiskNo.Match(antecedent ?? "");
        Match m2 = Letter.Match(dependent ?? "");
        if (m1.Success && m2.Success) d.Letters[m2.Groups[1].Value.ToUpperInvariant()] = int.Parse(m1.Groups[1].Value);
    }

    // ---- reading: System.Management -------------------------------------------------------------
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static RawData ReadWithManagement()
    {
        RawData d = new RawData();
        foreach (ManagementObject o in Query("SELECT * FROM Win32_DiskDrive"))
        {
            RawDrive r = new RawDrive();
            r.Index = Convert.ToInt32(o["Index"]);
            try { r.Size = Convert.ToInt64(o["Size"]); } catch (Exception) { }
            r.Model = Convert.ToString(o["Model"]) ?? "";
            r.Interface = Convert.ToString(o["InterfaceType"]) ?? "";
            r.Pnp = Convert.ToString(o["PNPDeviceID"]) ?? "";
            d.Drives.Add(r);
        }
        foreach (ManagementObject o in Query("SELECT DiskIndex, Type FROM Win32_DiskPartition"))
        {
            RawPartition p = new RawPartition();
            p.DiskIndex = Convert.ToInt32(o["DiskIndex"]);
            p.Type = Convert.ToString(o["Type"]) ?? "";
            d.Partitions.Add(p);
        }
        try
        {
            foreach (ManagementObject a in Query("SELECT * FROM Win32_LogicalDiskToPartition"))
                AddMap(d, Convert.ToString(a["Antecedent"]), Convert.ToString(a["Dependent"]));
        }
        catch (Exception) { }
        return d;
    }

    static List<ManagementObject> Query(string wql)
    {
        List<ManagementObject> res = new List<ManagementObject>();
        using (ManagementObjectSearcher s = new ManagementObjectSearcher(wql))
            foreach (ManagementBaseObject o in s.Get()) res.Add((ManagementObject)o);
        return res;
    }

    // ---- reading: PowerShell fallback ---------------------------------------------------------------
    const string PsScript =
@"function G($c) { try { Get-CimInstance -ClassName $c -ErrorAction Stop } catch { Get-WmiObject -Class $c } }
G Win32_DiskDrive | ForEach-Object { 'D|' + $_.Index + '|' + $_.Size + '|' + $_.InterfaceType + '|' + $_.PNPDeviceID + '|' + $_.Model }
G Win32_DiskPartition | ForEach-Object { 'P|' + $_.DiskIndex + '|' + $_.Type }
G Win32_LogicalDiskToPartition | ForEach-Object { $a = $_.Antecedent; $b = $_.Dependent; if ($a -isnot [string]) { $a = $a.DeviceID }; if ($b -isnot [string]) { $b = $b.DeviceID }; 'M|' + $a + '|' + $b }
";

    public static RawData ReadWithPowerShell()
    {
        string output;
        Ps.Run(PsScript, out output);
        RawData d = new RawData();
        foreach (string raw in output.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length < 3 || line[1] != '|') continue;
            string[] f = line.Split('|');
            try
            {
                if (f[0] == "D")
                {
                    RawDrive r = new RawDrive();
                    r.Index = int.Parse(f[1]);
                    long sz; long.TryParse(f[2], out sz); r.Size = sz;
                    r.Interface = f[3]; r.Pnp = f[4];
                    r.Model = string.Join("|", f, 5, f.Length - 5);
                    d.Drives.Add(r);
                }
                else if (f[0] == "P") { RawPartition p = new RawPartition(); p.DiskIndex = int.Parse(f[1]); p.Type = f[2]; d.Partitions.Add(p); }
                else if (f[0] == "M") AddMap(d, f[1], f[2]);
            }
            catch (Exception) { }
        }
        if (d.Drives.Count == 0) throw new InvalidOperationException("no disks reported by WMI");
        return d;
    }

    static RawData Read()
    {
        try { return ReadWithManagement(); }
        catch (FileNotFoundException) { }       // System.Management is not available here
        catch (TypeLoadException) { }
        catch (BadImageFormatException) { }
        return ReadWithPowerShell();
    }

    // "C:" -> physical disk number
    public static Dictionary<string, int> LetterMap()
    {
        try { return Read().Letters; } catch (Exception) { return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); }
    }

    public static int DiskOfDrive(string drive)   // "E:" or "E:\..." ; -1 if unknown
    {
        if (string.IsNullOrEmpty(drive) || drive.Length < 2) return -1;
        int n;
        return LetterMap().TryGetValue(drive.Substring(0, 2), out n) ? n : -1;
    }

    static string Bus(RawDrive d)
    {
        string pnp = d.Pnp.ToUpperInvariant();
        string model = d.Model.ToUpperInvariant();
        string itf = d.Interface.ToUpperInvariant();
        if (pnp.StartsWith("USBSTOR") || itf == "USB") return "USB";
        if (pnp.Contains("NVME") || model.Contains("NVME")) return "NVMe";
        if (itf == "IDE") return "SATA";
        return d.Interface;
    }

    // blockedDrives: drive letters (e.g. "E:") whose physical disks must not be offered as a target
    // (the image file, the running program).
    public static List<DiskInfo> List(IEnumerable<string> blockedDrives)
    {
        RawData raw = Read();
        Dictionary<string, int> map = raw.Letters;

        HashSet<int> blocked = new HashSet<int>();
        if (blockedDrives != null)
            foreach (string b in blockedDrives)
            {
                int n;
                if (!string.IsNullOrEmpty(b) && b.Length >= 2 && map.TryGetValue(b.Substring(0, 2), out n)) blocked.Add(n);
            }
        int sysDisk = -1;
        string sysDrive = (Environment.GetEnvironmentVariable("SystemDrive") ?? "").ToUpperInvariant();
        if (sysDrive != "X:" && sysDrive.Length == 2) { int n; if (map.TryGetValue(sysDrive, out n)) sysDisk = n; }

        List<DiskInfo> res = new List<DiskInfo>();
        foreach (RawDrive d in raw.Drives)
        {
            if (d.Size <= 0) continue;   // empty card reader
            DiskInfo di = new DiskInfo();
            di.Number = d.Index;
            di.Model = d.Model;
            di.PnpId = d.Pnp;
            di.Size = d.Size;
            di.Bus = Bus(d);

            int count = 0; bool gpt = false;
            foreach (RawPartition p in raw.Partitions)
            {
                if (p.DiskIndex != di.Number) continue;
                count++;
                if (p.Type.StartsWith("GPT")) gpt = true;
            }
            di.Style = count == 0 ? "RAW" : (gpt ? "GPT" : "MBR");

            foreach (KeyValuePair<string, int> kv in map)
                if (kv.Value == di.Number)
                {
                    string label = "";
                    try { label = new DriveInfo(kv.Key).VolumeLabel; } catch (Exception) { }
                    di.Volumes.Add((kv.Key + " " + label).Trim());
                }
            di.Volumes.Sort();

            if (di.Number == sysDisk) { di.Blocked = true; di.BlockReason = S.T("disk.system"); }
            else if (blocked.Contains(di.Number)) { di.Blocked = true; di.BlockReason = S.T("disk.source"); }
            else if (di.Bus == "USB") di.BlockReason = S.T("disk.usb");
            res.Add(di);
        }
        res.Sort(delegate(DiskInfo a, DiskInfo b) { return a.Number.CompareTo(b.Number); });
        return res;
    }

    public static List<string> FreeLetters()
    {
        HashSet<char> used = new HashSet<char>();
        foreach (DriveInfo d in DriveInfo.GetDrives()) used.Add(char.ToUpperInvariant(d.Name[0]));
        List<string> free = new List<string>();
        for (char c = 'Z'; c >= 'D'; c--) if (!used.Contains(c)) free.Add(c.ToString());
        return free;
    }
}
