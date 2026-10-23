using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kiwindow.Engine;

namespace Kiwindow.Ui
{
    // Paleta das janelas: tons suaves que convivem com o tema escuro quente do design system (laranja fica reservado à ação principal).
    // Mapa dos monitores com as janelas do perfil: clique seleciona, arrasta move, bordas redimensionam.
    public class LayoutPreview : FrameworkElement
    {
        static readonly FontFamily UiFont = new FontFamily("Segoe UI Variable Text, Segoe UI");

        public static readonly Color[] Paleta =
        {
            Color.FromRgb(0x7F, 0xB0, 0xE8), Color.FromRgb(0x8C, 0xC9, 0x6B), Color.FromRgb(0xE0, 0xB8, 0x60),
            Color.FromRgb(0xC4, 0x93, 0xE0), Color.FromRgb(0xE5, 0x8F, 0xA6), Color.FromRgb(0x5F, 0xC4, 0xC0),
            Color.FromRgb(0xD9, 0xA2, 0x7A), Color.FromRgb(0x9A, 0xA6, 0xE8), Color.FromRgb(0xB7, 0xC9, 0x5A),
            Color.FromRgb(0x8F, 0xB3, 0xB0),
        };
        public static Color CorDe(WindowEntry j) => Paleta[Math.Abs(j.Indice) % Paleta.Length];

        [Flags] enum Borda { Nenhuma = 0, Esq = 1, Topo = 2, Dir = 4, Base = 8, Mover = 16 }

        List<MonitorInfo> _monitores = new List<MonitorInfo>();
        ObservableCollection<WindowEntry> _janelas;
        WindowEntry _sel;

        // transformação física <-> tela
        double _escala = 1, _ox, _oy; int _minX, _minY;

        // arraste
        Borda _modo; Point _inicio; Rect32 _rectInicial; bool _arrastou;
        Point? _ponteiroMaximizada;

        public event EventHandler<WindowEntry> SelecaoMudou;
        public event EventHandler<WindowEntry> RemoverPedido;
        public event EventHandler Editado;

        public LayoutPreview()
        {
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
        }

        public void Carregar(List<MonitorInfo> monitores, ObservableCollection<WindowEntry> janelas)
        {
            if (_janelas != null)
            {
                _janelas.CollectionChanged -= ColecaoMudou;
                foreach (var j in _janelas) j.PropertyChanged -= ItemMudou;
            }
            _monitores = monitores ?? new List<MonitorInfo>();
            _janelas = janelas;
            if (_janelas != null)
            {
                _janelas.CollectionChanged += ColecaoMudou;
                foreach (var j in _janelas) j.PropertyChanged += ItemMudou;
            }
            _sel = null;
            InvalidateVisual();
        }

        void ColecaoMudou(object s, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null) foreach (WindowEntry j in e.OldItems) j.PropertyChanged -= ItemMudou;
            if (e.NewItems != null) foreach (WindowEntry j in e.NewItems) j.PropertyChanged += ItemMudou;
            if (_sel != null && !_janelas.Contains(_sel)) _sel = null;
            InvalidateVisual();
        }

        void ItemMudou(object s, PropertyChangedEventArgs e) => InvalidateVisual();

        public WindowEntry Selecionada
        {
            get => _sel;
            set { if (_sel == value) return; _sel = value; InvalidateVisual(); }
        }

        // ---------------------------------------------------------------- geometria

        void CalcularTransformacao()
        {
            if (_monitores.Count == 0) { _escala = 1; return; }
            _minX = _monitores.Min(m => m.Bounds.X); _minY = _monitores.Min(m => m.Bounds.Y);
            int maxX = _monitores.Max(m => m.Bounds.Right), maxY = _monitores.Max(m => m.Bounds.Bottom);
            double pad = 18;
            double w = Math.Max(1, ActualWidth - pad * 2), h = Math.Max(1, ActualHeight - pad * 2);
            _escala = Math.Min(w / (maxX - _minX), h / (maxY - _minY));
            _ox = pad + (w - (maxX - _minX) * _escala) / 2;
            _oy = pad + (h - (maxY - _minY) * _escala) / 2;
        }

        Rect ParaTela(Rect32 r) => new Rect(_ox + (r.X - _minX) * _escala, _oy + (r.Y - _minY) * _escala, Math.Max(1, r.W * _escala), Math.Max(1, r.H * _escala));
        Point ParaFisico(Point p) => new Point((p.X - _ox) / _escala + _minX, (p.Y - _oy) / _escala + _minY);

        MonitorInfo MonitorDe(WindowEntry j) =>
            _monitores.FirstOrDefault(m => m.Dispositivo == j.MonitorDispositivo && m.Bounds.Equals(j.MonitorBounds))
            ?? _monitores.FirstOrDefault(m => m.Dispositivo == j.MonitorDispositivo)
            ?? _monitores.FirstOrDefault(m => m.Bounds.Contains(j.X + j.Largura / 2, j.Y + j.Altura / 2))
            ?? _monitores.FirstOrDefault();

        MonitorInfo MonitorEm(int x, int y) =>
            _monitores.FirstOrDefault(m => m.Bounds.Contains(x, y))
            ?? _monitores.OrderBy(m => Dist(m.Bounds, x, y)).FirstOrDefault();

        static double Dist(Rect32 r, int x, int y)
        {
            double dx = Math.Max(Math.Max(r.X - x, 0), x - r.Right), dy = Math.Max(Math.Max(r.Y - y, 0), y - r.Bottom);
            return dx * dx + dy * dy;
        }

        // Retângulo exibido: maximizadas ocupam a área útil do monitor
        Rect32 RectVisivel(WindowEntry j)
        {
            if (!j.Maximizada) return j.Rect;
            var m = MonitorDe(j);
            return m != null ? m.Work : j.Rect;
        }

        // ---------------------------------------------------------------- desenho

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            CalcularTransformacao();
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var fonte = new Typeface(UiFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var fonteBold = new Typeface(UiFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

            if (_monitores.Count == 0)
            {
                var ft = Texto(Strings.Get("Preview.Empty"), fonte, 13, TextoTerciario, dpi);
                dc.DrawText(ft, new Point((ActualWidth - ft.Width) / 2, (ActualHeight - ft.Height) / 2));
                return;
            }

            // Monitores
            var fundoMon = Congelar(new SolidColorBrush(FundoMonitor));
            var barra = Congelar(new SolidColorBrush(Color.FromRgb(0x0E, 0x0D, 0x0C)));
            foreach (var m in _monitores)
            {
                var r = ParaTela(m.Bounds);
                dc.DrawRoundedRectangle(fundoMon, null, r, 6, 6);
                // faixa da barra de tarefas (diferença entre monitor e área útil)
                if (!m.Work.Equals(m.Bounds))
                {
                    var w = ParaTela(m.Work);
                    dc.PushClip(new RectangleGeometry(r, 6, 6));
                    if (m.Work.Bottom < m.Bounds.Bottom) dc.DrawRectangle(barra, null, new Rect(r.X, w.Bottom, r.Width, r.Bottom - w.Bottom));
                    if (m.Work.Y > m.Bounds.Y) dc.DrawRectangle(barra, null, new Rect(r.X, r.Y, r.Width, w.Y - r.Y));
                    if (m.Work.X > m.Bounds.X) dc.DrawRectangle(barra, null, new Rect(r.X, r.Y, w.X - r.X, r.Height));
                    if (m.Work.Right < m.Bounds.Right) dc.DrawRectangle(barra, null, new Rect(w.Right, r.Y, r.Right - w.Right, r.Height));
                    dc.Pop();
                }
                var nome = Strings.Format("Preview.Monitor", m.Dispositivo.Replace(@"\\.\", "").Replace("DISPLAY", ""));
                var principal = m.Principal ? "  ·  " + Strings.Get("Preview.Primary") : "";
                var ft = Texto($"{nome}  ·  {m.Bounds.W}×{m.Bounds.H}{principal}", fonte, 11, TextoTerciario, dpi);
                dc.DrawText(ft, new Point(r.X + 10, r.Bottom - ft.Height - 7));
            }

            if (_janelas == null) return;

            // Janelas: de baixo para cima na ordem z; a selecionada por último (por cima)
            var ordem = _janelas.Reverse().Where(j => j != _sel).ToList();
            if (_sel != null && _janelas.Contains(_sel)) ordem.Add(_sel);
            foreach (var j in ordem)
            {
                bool sel = j == _sel;
                var c = CorDe(j);
                var rt = ParaTela(RectVisivel(j));
                var fill = new SolidColorBrush(Color.FromArgb((byte)(sel ? 0x55 : 0x2C), c.R, c.G, c.B));
                var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(sel ? 0xFF : 0xA0), c.R, c.G, c.B)), sel ? 2 : 1);
                if (j.Maximizada) pen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);

                // fundo opaco por baixo, para janelas sobrepostas não virarem uma mistura ilegível
                dc.DrawRoundedRectangle(Congelar(new SolidColorBrush(FundoMonitor)), null, rt, 4, 4);
                dc.DrawRoundedRectangle(fill, pen, rt, 4, 4);

                dc.PushClip(new RectangleGeometry(new Rect(rt.X + 1, rt.Y + 1, Math.Max(0, rt.Width - 2), Math.Max(0, rt.Height - 2)), 4, 4));
                double barH = Math.Min(20, rt.Height);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x50, c.R, c.G, c.B)), null, new Rect(rt.X, rt.Y, rt.Width, barH));
                double tx = rt.X + 6;
                if (j.Icone is ImageSource img && barH >= 14)
                {
                    dc.DrawImage(img, new Rect(tx, rt.Y + (barH - 14) / 2, 14, 14));
                    tx += 18;
                }
                var nomeProc = (j.Processo ?? "").Replace(".exe", "");
                var t1 = Texto(nomeProc, fonteBold, 11, TextoPrimario, dpi);
                t1.MaxTextWidth = Math.Max(1, rt.Right - tx - 4); t1.MaxLineCount = 1; t1.Trimming = TextTrimming.CharacterEllipsis;
                if (barH >= 13) dc.DrawText(t1, new Point(tx, rt.Y + (barH - t1.Height) / 2));
                if (rt.Height > 36)
                {
                    var t2 = Texto(j.Titulo ?? "", fonte, 11, Color.FromArgb(0xC8, 0xF3, 0xF0, 0xEA), dpi);
                    t2.MaxTextWidth = Math.Max(1, rt.Width - 12); t2.MaxTextHeight = Math.Max(1, rt.Height - barH - 22);
                    t2.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(t2, new Point(rt.X + 6, rt.Y + barH + 4));
                }
                if (rt.Height > 56 && rt.Width > 90)
                {
                    var info = j.Maximizada ? Strings.Get("Maximized") : $"{j.Largura}×{j.Altura}";
                    var t3 = Texto(info, fonte, 10, Color.FromArgb(0x99, 0xF3, 0xF0, 0xEA), dpi);
                    dc.DrawText(t3, new Point(rt.Right - t3.Width - 6, rt.Bottom - t3.Height - 4));
                }
                dc.Pop();

                if (sel && !j.Maximizada) DesenharAlcas(dc, rt, c);
            }

            // Arrastando uma maximizada: mostra em qual monitor ela vai cair
            if (_ponteiroMaximizada.HasValue && _sel != null)
            {
                var f = ParaFisico(_ponteiroMaximizada.Value);
                var m = MonitorEm((int)f.X, (int)f.Y);
                if (m != null)
                {
                    var c = CorDe(_sel);
                    var pen = new Pen(new SolidColorBrush(c), 2) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x30, c.R, c.G, c.B)), pen, ParaTela(m.Work), 6, 6);
                }
            }

            // Etiqueta com posição/tamanho durante o arraste
            if (_modo != Borda.Nenhuma && _arrastou && _sel != null && !_sel.Maximizada)
            {
                var txt = _modo == Borda.Mover ? $"{_sel.X}, {_sel.Y}" : $"{_sel.Largura} × {_sel.Altura}";
                var ft = Texto(txt, fonteBold, 11, TextoPrimario, dpi);
                var rt = ParaTela(_sel.Rect);
                var pos = new Point(rt.X + (rt.Width - ft.Width) / 2, rt.Y + (rt.Height - ft.Height) / 2);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x13)), null,
                    new Rect(pos.X - 8, pos.Y - 4, ft.Width + 16, ft.Height + 8), 6, 6);
                dc.DrawText(ft, pos);
            }
        }

        static void DesenharAlcas(DrawingContext dc, Rect r, Color c)
        {
            var fill = Brushes.White; var pen = new Pen(new SolidColorBrush(c), 1.5);
            const double s = 7;
            foreach (var p in new[]
            {
                r.TopLeft, r.TopRight, r.BottomLeft, r.BottomRight,
                new Point(r.X + r.Width / 2, r.Y), new Point(r.X + r.Width / 2, r.Bottom),
                new Point(r.X, r.Y + r.Height / 2), new Point(r.Right, r.Y + r.Height / 2),
            })
                dc.DrawRoundedRectangle(fill, pen, new Rect(p.X - s / 2, p.Y - s / 2, s, s), 2, 2);
        }

        // Tons do design system (Tokens.xaml do SDK): Text.Primary, Text.Tertiary e um fundo abaixo de Surface2
        static readonly Color TextoPrimario = Color.FromRgb(0xF3, 0xF0, 0xEA);
        static readonly Color TextoTerciario = Color.FromRgb(0x8C, 0x86, 0x7D);
        static readonly Color FundoMonitor = Color.FromRgb(0x15, 0x14, 0x13);

        static SolidColorBrush Congelar(SolidColorBrush b) { b.Freeze(); return b; }

        static FormattedText Texto(string s, Typeface tf, double size, Color c, double dpi) =>
            new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, size, new SolidColorBrush(c), dpi);

        // ---------------------------------------------------------------- interação

        Borda Acerto(Point p, out WindowEntry alvo)
        {
            alvo = null;
            if (_janelas == null) return Borda.Nenhuma;
            var candidatos = new List<WindowEntry>();
            if (_sel != null && _janelas.Contains(_sel)) candidatos.Add(_sel);
            candidatos.AddRange(_janelas.Where(j => j != _sel));
            const double tol = 6;
            foreach (var j in candidatos)
            {
                var r = ParaTela(RectVisivel(j));
                var ext = r; ext.Inflate(tol, tol);
                if (!ext.Contains(p)) continue;
                if (j != _sel && !r.Contains(p)) continue;
                alvo = j;
                if (j.Maximizada || j != _sel) return Borda.Mover;
                var b = Borda.Nenhuma;
                if (Math.Abs(p.X - r.Left) <= tol) b |= Borda.Esq;
                else if (Math.Abs(p.X - r.Right) <= tol) b |= Borda.Dir;
                if (Math.Abs(p.Y - r.Top) <= tol) b |= Borda.Topo;
                else if (Math.Abs(p.Y - r.Bottom) <= tol) b |= Borda.Base;
                return b == Borda.Nenhuma ? Borda.Mover : b;
            }
            return Borda.Nenhuma;
        }

        static Cursor CursorPara(Borda b)
        {
            switch (b)
            {
                case Borda.Esq: case Borda.Dir: return Cursors.SizeWE;
                case Borda.Topo: case Borda.Base: return Cursors.SizeNS;
                case Borda.Esq | Borda.Topo: case Borda.Dir | Borda.Base: return Cursors.SizeNWSE;
                case Borda.Dir | Borda.Topo: case Borda.Esq | Borda.Base: return Cursors.SizeNESW;
                case Borda.Mover: return Cursors.SizeAll;
                default: return Cursors.Arrow;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            Pressionar(e.GetPosition(this));
            e.Handled = true;
        }

        void Pressionar(Point p)
        {
            var b = Acerto(p, out var alvo);
            if (alvo == null)
            {
                // Clicou numa janela que não é a selecionada, ou no vazio
                foreach (var j in _janelas ?? Enumerable.Empty<WindowEntry>())
                    if (ParaTela(RectVisivel(j)).Contains(p)) { alvo = j; b = Borda.Mover; break; }
            }
            if (alvo != _sel) { _sel = alvo; SelecaoMudou?.Invoke(this, alvo); }
            if (alvo != null)
            {
                _modo = b == Borda.Nenhuma ? Borda.Mover : b;
                _inicio = p; _rectInicial = alvo.Rect; _arrastou = false;
                CaptureMouse();
            }
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            if (_modo == Borda.Nenhuma || _sel == null || !IsMouseCaptured)
            {
                Cursor = CursorPara(Acerto(p, out _));
                return;
            }
            Arrastar(p, Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
        }

        void Arrastar(Point p, bool semEncaixe)
        {
            var d = p - _inicio;
            if (!_arrastou && Math.Abs(d.X) < 3 && Math.Abs(d.Y) < 3) return;
            _arrastou = true;

            if (_sel.Maximizada)
            {
                _ponteiroMaximizada = p;
                InvalidateVisual();
                return;
            }

            int dx = (int)Math.Round(d.X / _escala), dy = (int)Math.Round(d.Y / _escala);
            var r = _rectInicial;
            int x = r.X, y = r.Y, right = r.Right, bottom = r.Bottom;
            const int min = 120;
            if (_modo == Borda.Mover) { x += dx; y += dy; right += dx; bottom += dy; }
            if ((_modo & Borda.Esq) != 0) x = Math.Min(x + dx, right - min);
            if ((_modo & Borda.Dir) != 0) right = Math.Max(right + dx, x + min);
            if ((_modo & Borda.Topo) != 0) y = Math.Min(y + dy, bottom - min);
            if ((_modo & Borda.Base) != 0) bottom = Math.Max(bottom + dy, y + min);

            if (!semEncaixe) Encaixar(ref x, ref y, ref right, ref bottom);

            _sel.X = x; _sel.Y = y; _sel.Largura = right - x; _sel.Altura = bottom - y;
        }

        // Encaixe magnético nas bordas dos monitores e das outras janelas (Shift desliga)
        void Encaixar(ref int x, ref int y, ref int right, ref int bottom)
        {
            int tol = (int)Math.Ceiling(10 / _escala);
            var xs = new List<int>(); var ys = new List<int>();
            foreach (var m in _monitores)
            {
                xs.Add(m.Work.X); xs.Add(m.Work.Right); xs.Add(m.Bounds.X); xs.Add(m.Bounds.Right);
                ys.Add(m.Work.Y); ys.Add(m.Work.Bottom); ys.Add(m.Bounds.Y); ys.Add(m.Bounds.Bottom);
            }
            foreach (var j in _janelas.Where(j => j != _sel && !j.Maximizada))
            {
                xs.Add(j.X); xs.Add(j.X + j.Largura); ys.Add(j.Y); ys.Add(j.Y + j.Altura);
            }
            int w = right - x, h = bottom - y;
            if (_modo == Borda.Mover)
            {
                int ax = Ajuste(x, xs, tol), bx = Ajuste(right, xs, tol);
                int sx = Math.Abs(ax) <= Math.Abs(bx) ? ax : bx;
                if (sx != int.MaxValue) { x += sx; right = x + w; }
                int ay = Ajuste(y, ys, tol), by = Ajuste(bottom, ys, tol);
                int sy = Math.Abs(ay) <= Math.Abs(by) ? ay : by;
                if (sy != int.MaxValue) { y += sy; bottom = y + h; }
                return;
            }
            int a;
            if ((_modo & Borda.Esq) != 0 && (a = Ajuste(x, xs, tol)) != int.MaxValue) x += a;
            if ((_modo & Borda.Dir) != 0 && (a = Ajuste(right, xs, tol)) != int.MaxValue) right += a;
            if ((_modo & Borda.Topo) != 0 && (a = Ajuste(y, ys, tol)) != int.MaxValue) y += a;
            if ((_modo & Borda.Base) != 0 && (a = Ajuste(bottom, ys, tol)) != int.MaxValue) bottom += a;
        }

        static int Ajuste(int v, List<int> alvos, int tol)
        {
            int melhor = int.MaxValue;
            foreach (var t in alvos)
            {
                int d = t - v;
                if (Math.Abs(d) <= tol && Math.Abs(d) < Math.Abs(melhor)) melhor = d;
            }
            return melhor;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!IsMouseCaptured) return;
            ReleaseMouseCapture();
            Soltar();
        }

        // Usado pelo autoteste: mesmo caminho do mouse, com coordenadas físicas da tela
        internal void SimularArraste(int fx1, int fy1, int fx2, int fy2, bool semEncaixe = true)
        {
            CalcularTransformacao();
            Point T(int x, int y) => new Point(_ox + (x - _minX) * _escala, _oy + (y - _minY) * _escala);
            Pressionar(T(fx1, fy1));
            ReleaseMouseCapture();
            if (_modo == Borda.Nenhuma) return;
            for (int i = 1; i <= 5; i++)
                Arrastar(T(fx1 + (fx2 - fx1) * i / 5, fy1 + (fy2 - fy1) * i / 5), semEncaixe);
            Soltar();
        }

        void Soltar()
        {
            if (_sel != null && _arrastou)
            {
                if (_sel.Maximizada && _ponteiroMaximizada.HasValue)
                {
                    var f = ParaFisico(_ponteiroMaximizada.Value);
                    var m = MonitorEm((int)f.X, (int)f.Y);
                    if (m != null) AtribuirMonitor(_sel, m, true);
                }
                else
                {
                    var m = MonitorEm(_sel.X + _sel.Largura / 2, _sel.Y + Math.Min(20, _sel.Altura / 2));
                    if (m != null) AtribuirMonitor(_sel, m, false);
                }
                Editado?.Invoke(this, EventArgs.Empty);
            }
            _modo = Borda.Nenhuma; _arrastou = false; _ponteiroMaximizada = null;
            InvalidateVisual();
        }

        public static void AtribuirMonitor(WindowEntry j, MonitorInfo m, bool moverRetangulo)
        {
            if (moverRetangulo && j.MonitorDispositivo != m.Dispositivo)
            {
                // Leva junto o tamanho "restaurado" para o novo monitor, mantendo a posição relativa
                var antigo = j.MonitorWork.W > 0 ? j.MonitorWork : m.Work;
                int nx = m.Work.X + (j.X - antigo.X), ny = m.Work.Y + (j.Y - antigo.Y);
                j.X = nx; j.Y = ny;
            }
            j.MonitorDispositivo = m.Dispositivo; j.MonitorBounds = m.Bounds; j.MonitorWork = m.Work;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this);
            Acerto(p, out var alvo);
            if (alvo == null)
                foreach (var j in _janelas ?? Enumerable.Empty<WindowEntry>())
                    if (ParaTela(RectVisivel(j)).Contains(p)) { alvo = j; break; }
            if (alvo != _sel) { _sel = alvo; SelecaoMudou?.Invoke(this, alvo); InvalidateVisual(); }
            base.OnMouseRightButtonDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_sel == null) return;
            int passo = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);   // Ctrl + setas = redimensiona
            switch (e.Key)
            {
                case Key.Left: if (ctrl) _sel.Largura = Math.Max(120, _sel.Largura - passo); else _sel.X -= passo; break;
                case Key.Right: if (ctrl) _sel.Largura += passo; else _sel.X += passo; break;
                case Key.Up: if (ctrl) _sel.Altura = Math.Max(120, _sel.Altura - passo); else _sel.Y -= passo; break;
                case Key.Down: if (ctrl) _sel.Altura += passo; else _sel.Y += passo; break;
                case Key.Delete: RemoverPedido?.Invoke(this, _sel); e.Handled = true; return;
                default: return;
            }
            Editado?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }
    }
}
