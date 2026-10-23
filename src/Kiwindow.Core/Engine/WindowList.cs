using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kiwindow.Engine
{
    public class LiveWindow
    {
        public IntPtr Handle;
        public uint Pid;
        public string Titulo, Classe, Exe;
        public bool Maximizada, Minimizada, Topmost;
        public Rect32 Rect;
        public string Processo => string.IsNullOrEmpty(Exe) ? "" : Path.GetFileName(Exe).ToLowerInvariant();
    }

    public static class WindowList
    {
        static readonly HashSet<string> ClassesIgnoradas = new HashSet<string>
            { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

        static readonly Dictionary<uint, string> CacheExe = new Dictionary<uint, string>();

        // Filtro barato usado também pelos eventos: janela "de usuário" de nível superior
        public static bool IsAppWindow(IntPtr h, bool incluirMinimizadas)
        {
            if (!Native.IsWindowVisible(h)) return false;
            if (!incluirMinimizadas && Native.IsIconic(h)) return false;
            if (Native.GetWindow(h, 4) != IntPtr.Zero) return false;                 // GW_OWNER: diálogos/popups
            if ((Native.GetWindowLong(h, -20) & 0x80) != 0) return false;            // WS_EX_TOOLWINDOW
            if (Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return false; // DWMWA_CLOAKED
            if (Native.GetWindowTextLength(h) == 0) return false;
            return !ClassesIgnoradas.Contains(Native.ClassName(h));
        }

        public static LiveWindow Describe(IntPtr h)
        {
            Native.GetWindowThreadProcessId(h, out uint pid);
            string exe;
            lock (CacheExe)
            {
                if (!CacheExe.TryGetValue(pid, out exe) || exe == null) { exe = Native.ExePath(pid); CacheExe[pid] = exe; }
            }
            Native.GetWindowRect(h, out var r);
            return new LiveWindow
            {
                Handle = h, Pid = pid, Exe = exe,
                Titulo = Native.Title(h), Classe = Native.ClassName(h),
                Maximizada = Native.IsZoomed(h), Minimizada = Native.IsIconic(h),
                Topmost = (Native.GetWindowLong(h, -20) & 0x8) != 0,
                Rect = new Rect32(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
            };
        }

        // Janelas de nível superior na ordem z (primeira = topo)
        public static List<LiveWindow> Visible(bool incluirMinimizadas)
        {
            var handles = new List<IntPtr>(256);
            Native.EnumWindows((h, l) => { handles.Add(h); return true; }, IntPtr.Zero);
            var res = new List<LiveWindow>();
            foreach (var h in handles)
                if (IsAppWindow(h, incluirMinimizadas)) res.Add(Describe(h));
            return res;
        }

        public static List<MonitorInfo> Monitors()
        {
            var res = new List<MonitorInfo>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, dc, r, d) =>
            {
                var mi = Native.MonitorInfo(m);
                res.Add(new MonitorInfo
                {
                    Dispositivo = mi.szDevice, Principal = (mi.dwFlags & 1) != 0,
                    Bounds = new Rect32(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top),
                    Work = new Rect32(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top),
                });
                return true;
            }, IntPtr.Zero);
            return res;
        }

        // HWND -> pastas abertas (no Windows 11 cada aba do Explorer aparece com o mesmo HWND)
        public static Dictionary<IntPtr, List<string>> ExplorerFolders()
        {
            var m = new Dictionary<IntPtr, List<string>>();
            try
            {
                var t = Type.GetTypeFromProgID("Shell.Application");
                dynamic shell = Activator.CreateInstance(t);
                foreach (dynamic w in shell.Windows())
                {
                    try
                    {
                        string p = w.Document.Folder.Self.Path;
                        var h = new IntPtr((long)w.HWND);
                        if (!m.TryGetValue(h, out var l)) m[h] = l = new List<string>();
                        if (!string.IsNullOrEmpty(p)) l.Add(p);
                    }
                    catch { }
                }
            }
            catch { }
            return m;
        }

        public static void ForgetPid(uint pid) { lock (CacheExe) CacheExe.Remove(pid); }

        // ---------------------------------------------------------------- utilidades de texto

        public static string NormalizeTitle(string t) => string.IsNullOrEmpty(t) ? "" : Regex.Replace(t, @"\p{Cf}", "").Trim();

        public static void SplitCommandLine(string linha, out string exe, out string args)
        {
            exe = ""; args = "";
            if (string.IsNullOrEmpty(linha)) return;
            linha = linha.Trim();
            if (linha.StartsWith("\""))
            {
                int fim = linha.IndexOf('"', 1);
                if (fim < 0) { exe = linha.Trim('"'); return; }
                exe = linha.Substring(1, fim - 1); args = linha.Substring(fim + 1).Trim(); return;
            }
            int esp = linha.IndexOf(' ');
            if (esp < 0) { exe = linha; return; }
            exe = linha.Substring(0, esp); args = linha.Substring(esp + 1).Trim();
        }

        public static List<string> FilesInArgs(string args)
        {
            var res = new List<string>();
            if (string.IsNullOrEmpty(args)) return res;
            foreach (Match m in Regex.Matches(args, "\"([^\"]+)\"|(\\S+)"))
            {
                var t = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (!Regex.IsMatch(t, @"^[a-zA-Z]:\\|^\\\\")) continue;
                try { if (File.Exists(t) || Directory.Exists(t)) res.Add(t); } catch { }
            }
            return res;
        }
    }
}
