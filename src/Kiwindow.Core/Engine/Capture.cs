using System;
using System.Diagnostics;
using System.Linq;

namespace Kiwindow.Engine
{
    public static class Capture
    {
        // Captura todas as janelas visíveis agora (exceto as deste app)
        public static Profile Take(string nome)
        {
            var proprio = (uint)Process.GetCurrentProcess().Id;
            var monitores = WindowList.Monitors();
            var visiveis = WindowList.Visible(false).Where(v => v.Pid != proprio).ToList();
            var pastas = visiveis.Any(v => v.Classe == "CabinetWClass") ? WindowList.ExplorerFolders() : null;
            var linhas = visiveis.Select(v => v.Pid).Distinct().ToDictionary(pid => pid, pid => Native.CommandLine(pid));

            var p = new Profile
            {
                Nome = string.IsNullOrWhiteSpace(nome) ? "Sem nome" : nome.Trim(),
                CapturadoEm = DateTime.Now,
                Carimbo = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"),
                Computador = Environment.MachineName,
                Monitores = monitores,
            };

            int ordem = 0;
            foreach (var v in visiveis)
            {
                linhas.TryGetValue(v.Pid, out var linha);
                WindowList.SplitCommandLine(linha, out _, out var args);
                var mi = Native.MonitorInfo(Native.MonitorFromWindow(v.Handle, 2));
                var mon = monitores.FirstOrDefault(m => m.Dispositivo == mi.szDevice) ?? monitores.First();
                var j = new WindowEntry
                {
                    OrdemZ = ordem++,
                    Titulo = v.Titulo, Classe = v.Classe, Processo = v.Processo, Executavel = v.Exe,
                    LinhaComando = linha, Argumentos = args,
                    Arquivos = WindowList.FilesInArgs(args),
                    PastasExplorer = pastas != null && pastas.TryGetValue(v.Handle, out var l) ? l.ToList() : new System.Collections.Generic.List<string>(),
                    Maximizada = v.Maximizada, SempreVisivel = v.Topmost,
                    X = v.Rect.X, Y = v.Rect.Y, Largura = v.Rect.W, Altura = v.Rect.H,
                    MonitorDispositivo = mon.Dispositivo, MonitorBounds = mon.Bounds, MonitorWork = mon.Work,
                };
                p.Janelas.Add(j);
            }
            return p;
        }
    }
}
