using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kiwindow.Engine
{
    public class RestoreResult
    {
        public int Total, Posicionadas, JaAbertas, Abertas;
        public double Segundos;
        public List<string> Avisos = new List<string>();
    }

    /*
      Motor de restauração, otimizado para ficar fluido:
       - Janelas já abertas: movidas com SWP_ASYNCWINDOWPOS (cada app processa em paralelo, um app travado não
         segura os outros) e, se estavam minimizadas/maximizadas, um único SetWindowPlacement já restaura direto
         na posição final (sem o "restaura e depois pula" de antes). Janelas já no lugar não são tocadas.
       - Programas lançados com CreateProcess + STARTUPINFO com posição/tamanho/maximizado: apps que respeitam
         isso já nascem no lugar certo.
       - Em vez de polling com Sleep, usa SetWinEventHook: a janela nova é posicionada no instante em que
         aparece (EVENT_OBJECT_SHOW), reavaliada quando o título muda e corrigida na hora se o app tentar se
         reposicionar sozinho (EVENT_OBJECT_LOCATIONCHANGE).
       - Ordem z aplicada num único lote DeferWindowPos.
    */
    public class Restorer
    {
        class Item
        {
            public WindowEntry S;
            public IntPtr H;
            public bool Lancada, VirouAba, Falhou;
            public uint Pid;          // processo que lançamos para esta janela (0 = desconhecido, ex.: Explorer)
            public int Correcoes;
            public string Titulo, Pasta;
            public bool SemConteudo => Pasta == null && S.Arquivos.Count == 0;
        }

        readonly Profile _p;
        readonly List<Item> _itens = new List<Item>();
        readonly HashSet<IntPtr> _usadas = new HashSet<IntPtr>();
        readonly HashSet<IntPtr> _antes = new HashSet<IntPtr>();
        readonly Dictionary<IntPtr, DateTime> _vistoEm = new Dictionary<IntPtr, DateTime>();
        readonly Dictionary<IntPtr, Item> _porHandle = new Dictionary<IntPtr, Item>();
        readonly RestoreResult _r = new RestoreResult();
        readonly uint _proprioPid = (uint)Process.GetCurrentProcess().Id;
        List<MonitorInfo> _monitores;
        Native.WinEventProc _cb;   // mantém o delegate vivo enquanto o hook existir
        DateTime _ultimaMudanca;
        dynamic _shell;
        string _motivo = "";

        Restorer(Profile p) { _p = p; }

        public static Task<RestoreResult> RunAsync(Profile p)
        {
            var tcs = new TaskCompletionSource<RestoreResult>();
            var t = new Thread(() =>
            {
                try { tcs.SetResult(new Restorer(p).Run()); }
                catch (Exception ex) { tcs.SetException(ex); }
            }) { IsBackground = true, Name = "Restauracao" };
            t.SetApartmentState(ApartmentState.STA);   // COM do Shell (pastas do Explorer)
            t.Start();
            return tcs.Task;
        }

        public static RestoreResult Run(Profile p) => new Restorer(p).Run();

        RestoreResult Run()
        {
            var sw = Stopwatch.StartNew();
            _monitores = WindowList.Monitors();
            foreach (var s in _p.Janelas.OrderBy(j => j.OrdemZ))
                _itens.Add(new Item { S = s, Titulo = WindowList.NormalizeTitle(s.Titulo), Pasta = s.Pasta });
            _r.Total = _itens.Count;

            foreach (var m in _p.Monitores)
                if (!_monitores.Any(x => x.Bounds.Equals(m.Bounds)))
                    _r.Avisos.Add($"Monitor {m.Dispositivo.Replace(@"\\.\", "")} ({m.Bounds}) não existe mais com essa configuração.");

            // 1) Reaproveita janelas já abertas (inclusive minimizadas)
            var atuais = WindowList.Visible(true).Where(a => a.Pid != _proprioPid).ToList();
            foreach (var a in atuais) _antes.Add(a.Handle);
            var pastas = _itens.Any(i => i.Pasta != null) ? WindowList.ExplorerFolders() : new Dictionary<IntPtr, List<string>>();

            foreach (var it in _itens)
                foreach (var a in atuais)
                {
                    if (_usadas.Contains(a.Handle)) continue;
                    pastas.TryGetValue(a.Handle, out var pa);
                    if (MesmaJanela(a, pa, it)) { _motivo = "já aberta, mesmo título"; Atribuir(it, a.Handle); break; }
                }

            // 1b) Programas sem arquivo/pasta associado (navegador, chat...): o título muda o tempo todo.
            //     Usa uma janela aberta do mesmo programa em vez de abrir outra, escolhendo a mais próxima da posição salva.
            foreach (var it in _itens.Where(i => i.H == IntPtr.Zero && i.SemConteudo))
            {
                var c = atuais.Where(a => !_usadas.Contains(a.Handle) && a.Processo == it.S.Processo && a.Classe == it.S.Classe
                                          && (pastas.Count == 0 || !pastas.ContainsKey(a.Handle)))
                              .OrderBy(a => Math.Abs(a.Rect.X - it.S.X) + Math.Abs(a.Rect.Y - it.S.Y)).FirstOrDefault();
                if (c != null) { _motivo = "já aberta, mesmo programa (título mudou)"; Atribuir(it, c.Handle); }
            }
            _r.JaAbertas = _itens.Count(i => i.H != IntPtr.Zero);

            // 2) Abre de uma vez tudo que falta (antes de esperar qualquer coisa)
            Native.AllowSetForegroundWindow(-1);
            foreach (var it in _itens.Where(i => i.H == IntPtr.Zero))
            {
                if (Lancar(it)) { it.Lancada = true; _r.Abertas++; Log($"lançado '{it.S.Titulo}' pid={it.Pid}"); }
                else { it.Falhou = true; _r.Avisos.Add($"Não dá para reabrir: [{it.S.Processo}] {it.S.Titulo}"); }
            }

            // 3) Loop de eventos: posiciona janelas novas assim que aparecem e segura as que tentam se mexer sozinhas
            Aguardar();

            foreach (var it in _itens.Where(i => i.Lancada))
            {
                if (it.VirouAba) _r.Avisos.Add($"Aberta como aba de uma janela do Explorer existente: {it.Pasta}");
                else if (it.H == IntPtr.Zero) _r.Avisos.Add($"Não apareceu a tempo: [{it.S.Processo}] {it.S.Titulo}");
            }

            // 4) Ordem z da captura, num lote só
            var handles = _itens.Where(i => i.H != IntPtr.Zero && Native.IsWindow(i.H)).Select(i => i.H).ToList();
            if (handles.Count > 0)
            {
                AplicarOrdemZ(handles);
                PararPiscar(handles);
                if (_r.Abertas > 0) { Thread.Sleep(250); PararPiscar(handles); }
            }
            _r.Posicionadas = handles.Count;
            _r.Segundos = sw.Elapsed.TotalSeconds;
            GravarLog();
            return _r;
        }

        // ---------------------------------------------------------------- correspondência

        bool MesmaJanela(LiveWindow a, List<string> pastasAtual, Item it)
        {
            if (it.Pasta != null && pastasAtual != null && pastasAtual.Count > 0)
                return pastasAtual.Any(p => string.Equals(p, it.Pasta, StringComparison.OrdinalIgnoreCase));
            if (a.Processo != it.S.Processo) return false;
            if (!MesmaClasse(it.S.Classe, a.Classe)) return false;
            if (!string.IsNullOrEmpty(a.Exe) && !string.IsNullOrEmpty(it.S.Executavel) &&
                !string.Equals(a.Exe, it.S.Executavel, StringComparison.OrdinalIgnoreCase)) return false;
            return WindowList.NormalizeTitle(a.Titulo) == it.Titulo;
        }

        // Compara só a parte estável do nome da classe: WinForms ("WindowsForms10.Window.8.app.0.2bf8098_r6"),
        // WPF ("HwndWrapper[App;;guid]") e MFC ("Afx:00400000:...") mudam o sufixo a cada execução
        static bool MesmaClasse(string salva, string atual)
        {
            if (string.IsNullOrEmpty(salva) || string.IsNullOrEmpty(atual)) return true;
            string Base(string c) { int i = c.IndexOfAny(new[] { '.', '[', ':' }); return i > 0 ? c.Substring(0, i) : c; }
            return string.Equals(Base(salva), Base(atual), StringComparison.OrdinalIgnoreCase);
        }

        void Atribuir(Item it, IntPtr h)
        {
            Native.GetWindowThreadProcessId(h, out uint wpid);
            Log($"atribuir '{it.S.Titulo}' -> janela {h} pid {wpid} titulo-atual='{Native.Title(h)}' ({_motivo}) lancada={it.Lancada} pid-lancado={it.Pid}");
            _motivo = "";
            it.H = h; _usadas.Add(h); _porHandle[h] = it;
            Posicionar(it, true);
            _ultimaMudanca = DateTime.UtcNow;
        }

        // ---------------------------------------------------------------- posicionamento

        MonitorInfo MonitorAlvo(WindowEntry s)
        {
            return _monitores.FirstOrDefault(m => m.Dispositivo == s.MonitorDispositivo && m.Bounds.Equals(s.MonitorBounds))
                ?? _monitores.FirstOrDefault(m => m.Bounds.Equals(s.MonitorBounds))
                ?? _monitores.FirstOrDefault(m => m.Dispositivo == s.MonitorDispositivo)
                ?? _monitores.FirstOrDefault(m => m.Bounds.Contains(s.X + s.Largura / 2, s.Y + s.Altura / 2))
                ?? _monitores.FirstOrDefault(m => m.Principal) ?? _monitores[0];
        }

        // Retângulo final para janelas normais; se o monitor sumiu, traz para dentro de algum monitor visível
        Rect32 RetanguloAlvo(WindowEntry s)
        {
            var r = s.Rect;
            if (_monitores.Any(m => m.Bounds.Contains(r.X + r.W / 2, r.Y + Math.Min(20, r.H / 2)))) return r;
            var w = MonitorAlvo(s).Work;
            int lw = Math.Min(r.W, w.W), lh = Math.Min(r.H, w.H);
            return new Rect32(w.X + (w.W - lw) / 2, w.Y + (w.H - lh) / 2, lw, lh);
        }

        bool NoLugar(Item it)
        {
            var h = it.H;
            if (!Native.IsWindow(h)) return true;
            if (it.S.Maximizada)
            {
                if (!Native.IsZoomed(h)) return false;
                var mi = Native.MonitorInfo(Native.MonitorFromWindow(h, 2));
                return mi.szDevice == MonitorAlvo(it.S).Dispositivo;
            }
            if (Native.IsIconic(h) || Native.IsZoomed(h)) return false;
            Native.GetWindowRect(h, out var r);
            var a = RetanguloAlvo(it.S);
            return r.Left == a.X && r.Top == a.Y && r.Right - r.Left == a.W && r.Bottom - r.Top == a.H;
        }

        // Diagnóstico: toda aplicação grava "last-apply.log" na pasta de dados (sobrescrito a cada vez)
        const bool LogAtivo = true;
        readonly Stopwatch _relogio = Stopwatch.StartNew();
        readonly StringBuilder _log = new StringBuilder();
        void Log(string s) => _log.Append($"{_relogio.ElapsedMilliseconds,6} ms  {s}\r\n");

        void GravarLog()
        {
            try
            {
                Log("---- resultado final ----");
                foreach (var it in _itens)
                {
                    string estado = "sem janela";
                    if (it.H != IntPtr.Zero && Native.IsWindow(it.H))
                    {
                        Native.GetWindowRect(it.H, out var r);
                        estado = $"{r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top} zoom={Native.IsZoomed(it.H)} titulo-atual='{Native.Title(it.H)}'";
                    }
                    var alvo = it.S.Maximizada ? "MAX " + MonitorAlvo(it.S).Dispositivo : RetanguloAlvo(it.S).ToString();
                    Log($"{(it.H != IntPtr.Zero && NoLugar(it) ? "OK   " : "ERRO ")} '{it.S.Titulo}' alvo={alvo} atual={estado} pid={it.Pid} corr={it.Correcoes} lancada={it.Lancada}");
                }
                Directory.CreateDirectory(Paths.DataRoot);
                File.WriteAllText(Paths.LastApplyLog, _log.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }

        void Posicionar(Item it, bool assincrono)
        {
            var h = it.H;
            if (!Native.IsWindow(h) || NoLugar(it)) return;
            if (IsHungAppWindow(h)) return;
            if (LogAtivo)
            {
                Native.GetWindowRect(h, out var rr);
                Log($"posicionar '{it.S.Titulo}' de {rr.Left},{rr.Top} {rr.Right - rr.Left}x{rr.Bottom - rr.Top} zoom={Native.IsZoomed(h)} icon={Native.IsIconic(h)} -> {(it.S.Maximizada ? "MAX" : RetanguloAlvo(it.S).ToString())} async={assincrono} corr={it.Correcoes}");
            }

            bool estadoEspecial = Native.IsIconic(h) || Native.IsZoomed(h);
            if (it.S.Maximizada || estadoEspecial)
            {
                // Um único SetWindowPlacement troca de monitor, restaura e maximiza (ou restaura direto no lugar)
                var wp = new Native.WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(Native.WINDOWPLACEMENT)) };
                Native.GetWindowPlacement(h, ref wp);
                Rect32 normal;
                if (it.S.Maximizada)
                {
                    var w = MonitorAlvo(it.S).Work;
                    int lw = Math.Min(Math.Max(400, wp.rcNormal.Right - wp.rcNormal.Left), w.W - 40);
                    int lh = Math.Min(Math.Max(300, wp.rcNormal.Bottom - wp.rcNormal.Top), w.H - 40);
                    normal = new Rect32(w.X + (w.W - lw) / 2, w.Y + (w.H - lh) / 2, lw, lh);
                    wp.showCmd = Native.SW_SHOWMAXIMIZED;
                    // Já maximizada em outro monitor: precisa sair do estado maximizado para trocar de monitor
                    if (Native.IsZoomed(h)) Native.ShowWindow(h, Native.SW_SHOWNOACTIVATE);
                }
                else
                {
                    normal = RetanguloAlvo(it.S);
                    wp.showCmd = Native.SW_SHOWNOACTIVATE;
                }
                var off = OffsetWorkspace();
                wp.rcNormal = new Native.RECT { Left = normal.X - off.X, Top = normal.Y - off.Y, Right = normal.Right - off.X, Bottom = normal.Bottom - off.Y };
                wp.flags = 0;
                Native.SetWindowPlacement(h, ref wp);
                return;
            }

            var a = RetanguloAlvo(it.S);
            uint f = Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER | (assincrono ? Native.SWP_ASYNCWINDOWPOS : 0);
            Native.SetWindowPos(h, IntPtr.Zero, a.X, a.Y, a.W, a.H, f);
        }

        // rcNormalPosition usa coordenadas de "workspace" (relativas à área útil do monitor principal)
        Rect32 OffsetWorkspace()
        {
            var p = _monitores.FirstOrDefault(m => m.Principal);
            return p == null ? new Rect32() : new Rect32(p.Work.X - p.Bounds.X, p.Work.Y - p.Bounds.Y, 0, 0);
        }

        // ---------------------------------------------------------------- lançamento

        bool Lancar(Item it)
        {
            var s = it.S;
            try
            {
                if (it.Pasta != null)
                {
                    // Pede ao Explorer que já está rodando para abrir a pasta (mais rápido e confiável que explorer.exe)
                    if (_shell == null) _shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                    _shell.Open(it.Pasta);
                    return true;
                }
                if (string.IsNullOrEmpty(s.Executavel) || s.Processo == "applicationframehost.exe") return false; // apps da Store
                var args = (s.Argumentos ?? "").Trim();
                if (args == "-Embedding") args = "";   // processo iniciado via COM: relançar assim não abre janela
                if (s.Processo == "windowsterminal.exe") return ShellExecute(it, "wt.exe", args, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                if (!File.Exists(s.Executavel)) return false;
                var dir = Path.GetDirectoryName(s.Executavel);
                return CriarProcesso(it, args, dir) || ShellExecute(it, s.Executavel, args, dir);
            }
            catch { return false; }
        }

        // CreateProcess com dica de posição/tamanho: apps que criam a janela com CW_USEDEFAULT já nascem no lugar
        bool CriarProcesso(Item it, string args, string dir)
        {
            var s = it.S;
            var si = new Native.STARTUPINFO { cb = Marshal.SizeOf(typeof(Native.STARTUPINFO)) };
            if (s.Maximizada)
            {
                var w = MonitorAlvo(s).Work;
                si.dwX = w.X + 40; si.dwY = w.Y + 40; si.dwXSize = Math.Max(400, w.W - 80); si.dwYSize = Math.Max(300, w.H - 80);
                si.wShowWindow = Native.SW_SHOWMAXIMIZED;
                si.dwFlags = 0x1 | 0x2 | 0x4;   // STARTF_USESHOWWINDOW | USESIZE | USEPOSITION
            }
            else
            {
                var a = RetanguloAlvo(s);
                si.dwX = a.X; si.dwY = a.Y; si.dwXSize = a.W; si.dwYSize = a.H;
                si.dwFlags = 0x2 | 0x4;
            }
            var cmd = new StringBuilder("\"" + s.Executavel + "\"" + (args.Length > 0 ? " " + args : ""));
            if (!Native.CreateProcess(s.Executavel, cmd, IntPtr.Zero, IntPtr.Zero, false, 0x00000400 /*CREATE_UNICODE_ENVIRONMENT*/,
                                      IntPtr.Zero, dir, ref si, out var pi))
                return false;   // ex.: precisa de elevação (740) -> ShellExecute
            it.Pid = (uint)pi.dwProcessId;
            Native.CloseHandle(pi.hThread); Native.CloseHandle(pi.hProcess);
            return true;
        }

        static bool ShellExecute(Item it, string file, string args, string dir)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, WorkingDirectory = dir }))
                    if (p != null) try { it.Pid = (uint)p.Id; } catch { }
                return true;
            }
            catch (Win32Exception) { return false; }
        }

        // ---------------------------------------------------------------- espera orientada a eventos

        void Aguardar()
        {
            var inicio = DateTime.UtcNow;
            bool temLancadas = _itens.Any(i => i.Lancada);
            var prazoAparecer = inicio.AddSeconds(10);
            _ultimaMudanca = DateTime.UtcNow;

            _cb = AoEvento;
            var hooks = new[]
            {
                Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, IntPtr.Zero, _cb, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS),
                Native.SetWinEventHook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_NAMECHANGE, IntPtr.Zero, _cb, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS),
                Native.SetWinEventHook(Native.EVENT_OBJECT_UNCLOAKED, Native.EVENT_OBJECT_UNCLOAKED, IntPtr.Zero, _cb, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS),
            };
            try
            {
                var proximaVarredura = DateTime.UtcNow;
                while (true)
                {
                    Native.MsgWaitForMultipleObjects(0, null, false, 30, 0x04FF /*QS_ALLINPUT*/);
                    while (Native.PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
                    {
                        Native.TranslateMessage(ref msg);
                        Native.DispatchMessage(ref msg);
                    }

                    var agora = DateTime.UtcNow;
                    if (agora >= proximaVarredura)
                    {
                        proximaVarredura = agora.AddMilliseconds(250);
                        Varredura(agora, temLancadas && agora - inicio > TimeSpan.FromMilliseconds(1000));
                    }

                    bool pendentes = _itens.Any(i => i.Lancada && i.H == IntPtr.Zero && !i.VirouAba);
                    bool todasNoLugar = _itens.Where(i => i.H != IntPtr.Zero).All(NoLugar);
                    var quieto = agora - _ultimaMudanca;
                    var total = agora - inicio;

                    // Rede de segurança: janela que saiu do lugar sem gerar evento para nós (ex.: o app se
                    // reposicionou antes de o hook existir) é corrigida aqui
                    if (!todasNoLugar && quieto > TimeSpan.FromMilliseconds(120) && CorrigirTodas()) continue;

                    if (!temLancadas)
                    {
                        // Só janelas já abertas: termina assim que todas estiverem no lugar (ou 800ms)
                        if ((todasNoLugar && total > TimeSpan.FromMilliseconds(40)) || total > TimeSpan.FromMilliseconds(800)) break;
                        continue;
                    }
                    if (pendentes && agora < prazoAparecer) continue;
                    // Apps recém-abertos às vezes se reposicionam sozinhos com atraso: vigia até ficar quieto
                    if (todasNoLugar && quieto > TimeSpan.FromMilliseconds(700)) break;
                    if (quieto > TimeSpan.FromMilliseconds(2500)) break;
                    if (agora > prazoAparecer.AddSeconds(3)) break;
                }
            }
            finally
            {
                foreach (var h in hooks) if (h != IntPtr.Zero) Native.UnhookWinEvent(h);
            }
        }

        // Retorna true se corrigiu alguma (cada janela tem um limite de correções, para não brigar com apps teimosos)
        bool CorrigirTodas()
        {
            bool algum = false;
            foreach (var it in _itens.Where(i => i.H != IntPtr.Zero && i.Correcoes < 6))
                if (!NoLugar(it)) { it.Correcoes++; Posicionar(it, false); algum = true; }
            if (algum) _ultimaMudanca = DateTime.UtcNow;
            return algum;
        }

        void AoEvento(IntPtr hook, uint ev, IntPtr h, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != 0 || idChild != 0 || h == IntPtr.Zero) return;   // só a própria janela
            try
            {
                if (ev == Native.EVENT_OBJECT_LOCATIONCHANGE)
                {
                    // Janela nossa que saiu do lugar (app se reposicionando, DPI de outro monitor...): corrige na hora
                    if (_porHandle.TryGetValue(h, out var it) && it.Correcoes < 6 && !NoLugar(it))
                    {
                        it.Correcoes++;
                        Posicionar(it, false);
                        _ultimaMudanca = DateTime.UtcNow;
                    }
                    return;
                }
                if (_usadas.Contains(h)) return;
                if (!_itens.Any(i => i.Lancada && i.H == IntPtr.Zero && !i.VirouAba)) return;
                if (ev == Native.EVENT_OBJECT_NAMECHANGE && Native.GetWindow(h, 4) != IntPtr.Zero) return;
                TentarAtribuir(h, DateTime.UtcNow, false);
            }
            catch { }
        }

        void TentarAtribuir(IntPtr h, DateTime agora, bool aceitarAntigas)
        {
            if (_usadas.Contains(h)) return;
            bool antiga = _antes.Contains(h);
            if (antiga && !aceitarAntigas) return;
            if (!WindowList.IsAppWindow(h, false)) return;
            var a = WindowList.Describe(h);
            if (a.Pid == _proprioPid) return;
            // Janelas auxiliares sem tamanho (ex.: painéis que o MPC-HC cria escondidos com altura 0) não são a janela do app
            if (a.Rect.W < 60 || a.Rect.H < 40) return;
            if (!_vistoEm.ContainsKey(h)) _vistoEm[h] = agora;

            // Mesmo programa e mesma classe de janela da captura: evita pegar um diálogo/painel secundário do mesmo processo
            var cands = _itens.Where(i => i.Lancada && i.H == IntPtr.Zero && !i.VirouAba && i.S.Processo == a.Processo
                                          && MesmaClasse(i.S.Classe, a.Classe)).ToList();
            if (cands.Count == 0) return;
            var titulo = WindowList.NormalizeTitle(a.Titulo);

            if (!antiga)
            {
                // 1) Identificação certa: a janela pertence ao processo que lançamos para o item (ou a um filho dele)
                uint pai = Native.ParentPid(a.Pid);
                var dono = cands.FirstOrDefault(c => c.Pid != 0 && (c.Pid == a.Pid || c.Pid == pai));
                if (dono != null) { _motivo = dono.Pid == a.Pid ? "pid" : "pid do pai"; Atribuir(dono, h); return; }
                // 2) Itens cujo processo ainda está rodando vão receber a própria janela: ficam fora do chute.
                //    Sobram os sem PID (Explorer) ou cujo processo repassou o arquivo para uma instância já aberta e saiu.
                var livres = cands.Where(c => c.Pid == 0 || !Native.ProcessAlive(c.Pid)).ToList();
                if (livres.Count == 0 && (agora - _vistoEm[h]).TotalMilliseconds < 4000) return;   // depois de 4s aceita qualquer um
                if (livres.Count > 0) cands = livres;
            }

            var alvo = cands.FirstOrDefault(c => c.Titulo == titulo);
            if (antiga)
            {
                // Apps com abas (Notepad, Terminal, navegadores) abrem o arquivo numa janela existente:
                // só vale se ela passou a mostrar exatamente o título salvo
                if (alvo != null) { _motivo = "janela antiga passou a ter o título"; Atribuir(alvo, h); }
                return;
            }
            if (alvo == null && a.Classe == "CabinetWClass" && cands.Count(c => c.Pasta != null) > 1) return;  // espera o mapa de pastas
            // Vários pendentes do mesmo programa: espera o título se definir antes de chutar
            if (alvo != null) _motivo = "título";
            else if (cands.Count == 1 || (agora - _vistoEm[h]).TotalMilliseconds > 1500) { alvo = cands[0]; _motivo = $"chute ({cands.Count} candidatos)"; }
            if (alvo != null) Atribuir(alvo, h);
        }

        void Varredura(DateTime agora, bool aceitarAntigas)
        {
            // Janela atribuída que sumiu (ex.: splash screen) volta a ser procurada
            foreach (var it in _itens.Where(i => i.Lancada && i.H != IntPtr.Zero && !Native.IsWindowVisible(i.H)))
            {
                _usadas.Remove(it.H); _porHandle.Remove(it.H); it.H = IntPtr.Zero;
            }

            var pendentes = _itens.Where(i => i.Lancada && i.H == IntPtr.Zero && !i.VirouAba).ToList();
            if (pendentes.Count == 0) return;

            // Explorer do Windows 11 pode abrir a pasta como aba de uma janela existente em vez de janela nova
            if (pendentes.Any(p => p.Pasta != null))
            {
                var mapa = WindowList.ExplorerFolders();
                foreach (var it in pendentes.Where(p => p.Pasta != null))
                    foreach (var kv in mapa)
                    {
                        if (!kv.Value.Any(p => string.Equals(p, it.Pasta, StringComparison.OrdinalIgnoreCase))) continue;
                        if (_antes.Contains(kv.Key)) { it.VirouAba = true; break; }
                        if (!_usadas.Contains(kv.Key)) { _motivo = "pasta do Explorer"; Atribuir(it, kv.Key); break; }
                    }
            }

            // Rede de segurança para eventos perdidos: reavalia janelas visíveis
            Native.EnumWindows((h, l) =>
            {
                if (!_usadas.Contains(h)) TentarAtribuir(h, agora, aceitarAntigas);
                return true;
            }, IntPtr.Zero);
        }

        // ---------------------------------------------------------------- ordem z e foco

        static void AplicarOrdemZ(List<IntPtr> handles)
        {
            Ativar(handles[0]);
            var normais = handles.Where(h => (Native.GetWindowLong(h, -20) & 0x8) == 0 && !IsHungAppWindow(h)).ToList();  // preserva "sempre visível"
            const uint f = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER;
            var info = Native.BeginDeferWindowPos(normais.Count);
            IntPtr anterior = IntPtr.Zero;   // HWND_TOP
            foreach (var h in normais)
            {
                if (info != IntPtr.Zero) info = Native.DeferWindowPos(info, h, anterior, 0, 0, 0, 0, f);
                anterior = h;
            }
            if (info == IntPtr.Zero || !Native.EndDeferWindowPos(info))
            {
                anterior = IntPtr.Zero;
                foreach (var h in normais) { Native.SetWindowPos(h, anterior, 0, 0, 0, 0, f); anterior = h; }
            }
        }

        static void Ativar(IntPtr h)
        {
            uint fg = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
            uint eu = Native.GetCurrentThreadId();
            bool anexou = fg != eu && Native.AttachThreadInput(eu, fg, true);
            Native.BringWindowToTop(h);
            bool ok = Native.SetForegroundWindow(h);
            if (anexou) Native.AttachThreadInput(eu, fg, false);
            if (!ok || Native.GetForegroundWindow() != h)
            {
                // O Windows só libera o foco para quem gerou a última entrada: um movimento de mouse nulo resolve
                Native.mouse_event(0x1, 0, 0, 0, UIntPtr.Zero);
                Native.SetForegroundWindow(h);
            }
        }

        static void PararPiscar(List<IntPtr> handles)
        {
            foreach (var h in handles)
            {
                var f = new Native.FLASHWINFO { cbSize = (uint)Marshal.SizeOf(typeof(Native.FLASHWINFO)), hwnd = h, dwFlags = 0 };
                if (Native.IsWindow(h)) Native.FlashWindowEx(ref f);
            }
        }

        [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr h);
    }
}
