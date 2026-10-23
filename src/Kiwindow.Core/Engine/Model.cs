using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kiwindow.Engine
{
    public struct Rect32
    {
        public int X, Y, W, H;
        public Rect32(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public int Right => X + W;
        public int Bottom => Y + H;
        public bool Contains(int px, int py) => px >= X && px < Right && py >= Y && py < Bottom;
        public override string ToString() => $"{W}x{H} @ {X},{Y}";
    }

    public class MonitorInfo
    {
        public string Dispositivo;
        public bool Principal;
        public Rect32 Bounds;
        public Rect32 Work;
    }

    public abstract class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void ClearHandlers() => PropertyChanged = null;
        protected void Raise([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string p = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; Raise(p); return true;
        }
    }

    public class WindowEntry : Notifier
    {
        int _ordemZ, _x, _y, _w, _h; bool _max; string _titulo;

        public int OrdemZ { get => _ordemZ; set => Set(ref _ordemZ, value); }
        public string Titulo { get => _titulo; set => Set(ref _titulo, value); }
        public string Classe { get; set; }
        public string Processo { get; set; }
        public string Executavel { get; set; }
        public string LinhaComando { get; set; }
        public string Argumentos { get; set; }
        public List<string> Arquivos { get; set; } = new List<string>();
        public List<string> PastasExplorer { get; set; } = new List<string>();
        public bool Maximizada { get => _max; set { if (Set(ref _max, value)) Raise(nameof(Resumo)); } }
        public bool SempreVisivel { get; set; }
        public int X { get => _x; set { if (Set(ref _x, value)) Raise(nameof(Resumo)); } }
        public int Y { get => _y; set { if (Set(ref _y, value)) Raise(nameof(Resumo)); } }
        public int Largura { get => _w; set { if (Set(ref _w, value)) Raise(nameof(Resumo)); } }
        public int Altura { get => _h; set { if (Set(ref _h, value)) Raise(nameof(Resumo)); } }

        string _monDisp; Rect32 _monBounds, _monWork;
        public string MonitorDispositivo { get => _monDisp; set { if (Set(ref _monDisp, value)) Raise(nameof(MonitorNome)); } }
        public Rect32 MonitorBounds { get => _monBounds; set => _monBounds = value; }
        public Rect32 MonitorWork { get => _monWork; set => _monWork = value; }

        public Rect32 Rect => new Rect32(X, Y, Largura, Altura);
        public string Pasta => PastasExplorer.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        public string NomeProcesso => string.IsNullOrEmpty(Processo) ? "?" : Processo;
        public string MonitorNome => Ui.Strings.Format("Preview.Monitor", (MonitorDispositivo ?? "").Replace(@"\\.\", "").Replace("DISPLAY", ""));
        public string Resumo => Maximizada ? Ui.Strings.Get("Maximized") : $"{Largura}×{Altura} · {X}, {Y}";
        public string Detalhe => Pasta ?? Arquivos.FirstOrDefault() ?? Executavel ?? "";

        // Ícone carregado sob demanda pela UI
        object _icone;
        public object Icone { get => _icone; set => Set(ref _icone, value); }
        public int Indice { get; set; }

        public WindowEntry Clone() { var c = (WindowEntry)MemberwiseClone(); c.ClearHandlers(); return c; }
    }

    public class Profile : Notifier
    {
        string _nome;
        public string Arquivo { get; set; }          // caminho completo do .json
        public string Nome { get => _nome; set => Set(ref _nome, value); }
        public string Carimbo { get; set; }          // sufixo yyyy-MM-dd_HH-mm-ss do nome do arquivo
        public DateTime CapturadoEm { get; set; }
        public string Computador { get; set; }
        public List<MonitorInfo> Monitores { get; set; } = new List<MonitorInfo>();
        public List<WindowEntry> Janelas { get; set; } = new List<WindowEntry>();

        public string Descricao => Ui.Strings.Format("ProfileLine", Janelas.Count, CapturadoEm.ToString("d MMM, HH:mm", Ui.Strings.Culture));
        public void Refresh() { Raise(nameof(Descricao)); Raise(nameof(Nome)); }

        // Garante que cada janela saiba a área útil do seu monitor (capturas antigas não salvavam isso em Monitores)
        public MonitorInfo MonitorDe(WindowEntry j)
        {
            var m = Monitores.FirstOrDefault(x => x.Dispositivo == j.MonitorDispositivo);
            if (m != null) return m;
            return Monitores.FirstOrDefault(x => x.Bounds.Contains(j.X + j.Largura / 2, j.Y + j.Altura / 2)) ?? Monitores.FirstOrDefault();
        }
    }
}
