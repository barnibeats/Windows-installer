// Windows API calls used by the app: dark title bar, OEM code page, ISO mounting (virtdisk), keyboard layout.
using System;
using System.Runtime.InteropServices;
using System.Text;

static class Native
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

    [DllImport("kernel32.dll")]
    public static extern int GetOEMCP();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    static extern bool ShowScrollBar(IntPtr hWnd, int bar, bool show);

    // Gray hint text inside an empty single-line TextBox.
    public static void SetCue(IntPtr textBox, string text)
    {
        try { SendMessage(textBox, 0x1501, (IntPtr)1, text ?? ""); } catch (Exception) { }
    }

    public static void HideHScroll(IntPtr hwnd)
    {
        try { ShowScrollBar(hwnd, 0, false); } catch (Exception) { }
    }

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetKeyboardLayoutName(StringBuilder pwszKLID);

    // Dark title bar (Windows 10 1809+ / 11). Harmless where unsupported.
    public static void DarkTitleBar(IntPtr hwnd, bool dark)
    {
        try
        {
            int v = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref v, 4);
            DwmSetWindowAttribute(hwnd, 19, ref v, 4);
        }
        catch (Exception) { }
    }

    // Dark scroll bars and list chrome for a native control.
    public static void DarkControl(IntPtr hwnd, bool dark)
    {
        try { SetWindowTheme(hwnd, dark ? "DarkMode_Explorer" : null, null); }
        catch (Exception) { }
    }

    // "00000409"-style layout id of the current keyboard layout.
    public static string KeyboardLayoutId()
    {
        try
        {
            StringBuilder sb = new StringBuilder(16);
            if (GetKeyboardLayoutName(sb)) return sb.ToString();
        }
        catch (Exception) { }
        return "00000409";
    }

    // ---- ISO mounting through virtdisk.dll (works without the PowerShell Storage module, e.g. in WinPE) ----
    [StructLayout(LayoutKind.Sequential)]
    struct VirtualStorageType
    {
        public uint DeviceId;
        public Guid VendorId;
    }

    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    static extern int OpenVirtualDisk(ref VirtualStorageType storageType, string path, int accessMask, int flags, IntPtr parameters, out IntPtr handle);

    [DllImport("virtdisk.dll")]
    static extern int AttachVirtualDisk(IntPtr handle, IntPtr securityDescriptor, int flags, int providerSpecificFlags, IntPtr parameters, IntPtr overlapped);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    // Attaches an ISO read-only. The disc stays mounted while the returned handle is open;
    // closing it (or exiting the app) unmounts it. Returns IntPtr.Zero on failure.
    public static IntPtr AttachIso(string path, out int error)
    {
        error = 0;
        VirtualStorageType t = new VirtualStorageType();
        t.DeviceId = 1;   // VIRTUAL_STORAGE_TYPE_DEVICE_ISO
        t.VendorId = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");   // Microsoft
        IntPtr h;
        int rc = OpenVirtualDisk(ref t, path, 0x000D0000, 0, IntPtr.Zero, out h);   // VIRTUAL_DISK_ACCESS_READ
        if (rc != 0) { error = rc; return IntPtr.Zero; }
        rc = AttachVirtualDisk(h, IntPtr.Zero, 1, 0, IntPtr.Zero, IntPtr.Zero);   // ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY
        if (rc != 0) { error = rc; CloseHandle(h); return IntPtr.Zero; }
        return h;
    }

    public static void Detach(IntPtr handle)
    {
        if (handle != IntPtr.Zero) CloseHandle(handle);
    }
}
