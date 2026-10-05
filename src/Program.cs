// Windows Installer: install Windows from ISO/WIM onto a chosen disk (GPT/UEFI or MBR/BIOS) and
// capture a prepared Windows into a WIM. Runs in Windows and in WinPE (only the .NET Framework is needed).
using System;
using System.IO;
using System.Net;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        S.Init();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--selftest") return SelfTest.Run(i + 1 < args.Length ? args[i + 1] : null);
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--e2e" && i + 6 <= args.Length)
                return SelfTest.RunE2E(args[i + 1], new string[] { args[i + 2], args[i + 3], args[i + 4], args[i + 5], i + 6 < args.Length ? args[i + 6] : "0" });

        try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls; } catch (Exception) { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Theme.Set(Settings.Get("theme", "dark") != "light");
        Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { Fatal(e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Fatal(e.ExceptionObject as Exception); };
        string shots = null;
        for (int i = 0; i < args.Length; i++) if (args[i] == "--shots" && i + 1 < args.Length) shots = args[i + 1];
        try
        {
            MainForm form = new MainForm();
            if (shots != null) Shots.Attach(form, shots);
            Application.Run(form);
        }
        catch (Exception ex) { Fatal(ex); }
        return 0;
    }

    static void Fatal(Exception ex)
    {
        string text = (ex == null ? "Unknown error" : ex.GetType().Name + ": " + ex.Message);
        try { File.WriteAllText(Path.Combine(Paths.DataDir, "crash.log"), DateTime.Now + "\r\n" + (ex == null ? "" : ex.ToString())); } catch (Exception) { }
        MessageBox.Show(text, S.T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
