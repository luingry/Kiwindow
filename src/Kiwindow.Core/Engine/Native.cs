using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Kiwindow.Engine
{
    public static class Native
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        public delegate bool MonitorProc(IntPtr m, IntPtr dc, IntPtr r, IntPtr d);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length, flags, showCmd;
            public POINT ptMin, ptMax;
            public RECT rcNormal;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb; public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING { public ushort Length, MaximumLength; public IntPtr Buffer; }

        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10,
                          SWP_NOOWNERZORDER = 0x200, SWP_ASYNCWINDOWPOS = 0x4000;
        public const int SW_SHOWNORMAL = 1, SW_SHOWMAXIMIZED = 3, SW_SHOWNOACTIVATE = 4, SW_RESTORE = 9;

        public const uint EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
                          EVENT_OBJECT_NAMECHANGE = 0x800C, EVENT_OBJECT_UNCLOAKED = 0x8018;
        public const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorProc cb, IntPtr d);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern IntPtr BeginDeferWindowPos(int n);
        [DllImport("user32.dll")] public static extern IntPtr DeferWindowPos(IntPtr info, IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool EndDeferWindowPos(IntPtr info);
        [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT wp);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromRect(ref RECT r, uint f);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFOEX mi);
        [DllImport("user32.dll")] public static extern bool FlashWindowEx(ref FLASHWINFO f);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc cb, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG msg, IntPtr h, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("user32.dll")] public static extern uint MsgWaitForMultipleObjects(uint n, IntPtr[] handles, bool all, uint ms, uint mask);

        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr p, uint f, StringBuilder s, ref int n);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags,
                                                IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

        [DllImport("ntdll.dll")] public static extern int NtQueryInformationProcess(IntPtr p, int cls, IntPtr buf, int len, out int ret);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT v, int s);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

        public static MONITORINFOEX MonitorInfo(IntPtr mon)
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
            GetMonitorInfo(mon, ref mi);
            return mi;
        }

        public static string ExePath(uint pid)
        {
            IntPtr p = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (p == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024); int n = sb.Capacity;
                return QueryFullProcessImageName(p, 0, sb, ref n) ? sb.ToString() : null;
            }
            finally { CloseHandle(p); }
        }

        // Linha de comando direto do processo (ProcessCommandLineInformation, Win 8.1+): muito mais rápido que WMI
        public static string CommandLine(uint pid)
        {
            IntPtr p = OpenProcess(0x1000, false, pid);
            if (p == IntPtr.Zero) return null;
            IntPtr buf = IntPtr.Zero;
            try
            {
                int len;
                NtQueryInformationProcess(p, 60, IntPtr.Zero, 0, out len);
                if (len <= 0) return null;
                buf = Marshal.AllocHGlobal(len);
                if (NtQueryInformationProcess(p, 60, buf, len, out len) != 0) return null;
                var us = (UNICODE_STRING)Marshal.PtrToStructure(buf, typeof(UNICODE_STRING));
                return us.Buffer == IntPtr.Zero ? null : Marshal.PtrToStringUni(us.Buffer, us.Length / 2);
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
                CloseHandle(p);
            }
        }

        [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_BASIC_INFORMATION { public IntPtr Reserved1, PebBaseAddress, Reserved2_0, Reserved2_1, UniqueProcessId, InheritedFromUniqueProcessId; }

        public static bool ProcessAlive(uint pid)
        {
            IntPtr p = OpenProcess(0x00100000, false, pid);   // SYNCHRONIZE
            if (p == IntPtr.Zero) return false;
            try { return WaitForSingleObject(p, 0) == 0x102; } // WAIT_TIMEOUT = ainda rodando
            finally { CloseHandle(p); }
        }

        public static uint ParentPid(uint pid)
        {
            IntPtr p = OpenProcess(0x1000, false, pid);
            if (p == IntPtr.Zero) return 0;
            int sz = Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION));
            IntPtr buf = Marshal.AllocHGlobal(sz);
            try
            {
                if (NtQueryInformationProcess(p, 0, buf, sz, out _) != 0) return 0;
                var pbi = (PROCESS_BASIC_INFORMATION)Marshal.PtrToStructure(buf, typeof(PROCESS_BASIC_INFORMATION));
                return (uint)pbi.InheritedFromUniqueProcessId.ToInt64();
            }
            finally { Marshal.FreeHGlobal(buf); CloseHandle(p); }
        }

        public static string Title(IntPtr h)
        {
            int len = GetWindowTextLength(h);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string ClassName(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static void UseDarkTitleBar(IntPtr h)
        {
            int on = 1;
            DwmSetWindowAttribute(h, 20, ref on, 4);   // DWMWA_USE_IMMERSIVE_DARK_MODE
            int round = 2;
            DwmSetWindowAttribute(h, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE = arredondado
        }

        // Windows 11: barra de título na cor do app (COLORREF 0x00BBGGRR); o Windows 10 ignora
        public static void SetCaptionColor(IntPtr h, byte r, byte g, byte b)
        {
            int color = r | (g << 8) | (b << 16);
            DwmSetWindowAttribute(h, 35, ref color, 4);   // DWMWA_CAPTION_COLOR
        }
    }
}
