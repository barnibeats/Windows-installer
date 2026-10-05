// Runs console tools (diskpart, DISM, bcdboot, ...) and streams their output.
// DISM redraws its progress bar with carriage returns, so output is split on \r as well as \n.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

static class Proc
{
    static readonly Regex Percent = new Regex(@"(\d{1,3})(?:[.,]\d)?\s*%");

    public static Encoding Oem()
    {
        try { return Encoding.GetEncoding(Native.GetOEMCP()); } catch (Exception) { return Encoding.Default; }
    }

    static ProcessStartInfo Info(string exe, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Oem();
        psi.StandardErrorEncoding = Oem();
        return psi;
    }

    // Runs a program, reporting each output line and any percentage found. Returns the exit code.
    public static int Run(string exe, string args, IProgress2 prog)
    {
        using (Process p = Process.Start(Info(exe, args)))
        {
            Thread t1 = new Thread(delegate() { Pump(p.StandardOutput, prog); });
            Thread t2 = new Thread(delegate() { Pump(p.StandardError, prog); });
            t1.IsBackground = true; t2.IsBackground = true;
            t1.Start(); t2.Start();
            p.WaitForExit();
            t1.Join(); t2.Join();
            return p.ExitCode;
        }
    }

    // Runs a program and returns everything it printed.
    public static int Capture(string exe, string args, out string output)
    {
        using (Process p = Process.Start(Info(exe, args)))
        {
            StringBuilder sb = new StringBuilder();
            object gate = new object();
            Thread t2 = new Thread(delegate()
            {
                string e = p.StandardError.ReadToEnd();
                lock (gate) { sb.Append(e); }
            });
            t2.IsBackground = true;
            t2.Start();
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            t2.Join();
            lock (gate) { output = o + sb.ToString(); }
            return p.ExitCode;
        }
    }

    static void Pump(StreamReader r, IProgress2 prog)
    {
        StringBuilder sb = new StringBuilder();
        try
        {
            int ch;
            while ((ch = r.Read()) != -1)
            {
                if (ch == '\r' || ch == '\n') { Flush(sb, prog); }
                else sb.Append((char)ch);
            }
            Flush(sb, prog);
        }
        catch (Exception) { }
    }

    static void Flush(StringBuilder sb, IProgress2 prog)
    {
        string line = sb.ToString().Trim();
        sb.Length = 0;
        if (line.Length == 0 || prog == null) return;
        Match m = Percent.Match(line);
        if (m.Success && (line.IndexOf('[') >= 0 || line.Length < 12 || line.IndexOf('=') >= 0))
        {
            int v;
            if (int.TryParse(m.Groups[1].Value, out v)) prog.Percent(Math.Min(100, v));
            return;   // progress bars are not logged
        }
        prog.Log("  " + line);
    }
}
