using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Kiwindow.Engine
{
    // Lê e grava os perfis (um .json cada) no formato original do LayoutJanelas, mantido por compatibilidade
    public static class ProfileStore
    {
        static readonly Regex Sufixo = new Regex(@"^(?:(.*)_)?(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})$");

        public static string Pasta => Paths.Profiles;

        public static List<Profile> LoadAll()
        {
            var res = new List<Profile>();
            if (!Directory.Exists(Pasta)) return res;
            foreach (var f in new DirectoryInfo(Pasta).GetFiles("*.json").OrderByDescending(f => f.LastWriteTime))
            {
                try { res.Add(Load(f.FullName)); } catch { /* arquivo corrompido: ignora */ }
            }
            return res;
        }

        public static Profile Load(string path)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = js.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>
                       ?? throw new InvalidDataException("JSON inválido");

            var p = new Profile { Arquivo = path };
            var baseName = Path.GetFileNameWithoutExtension(path);
            var m = Sufixo.Match(baseName);
            if (m.Success) { p.Nome = m.Groups[1].Success && m.Groups[1].Value != "" ? m.Groups[1].Value : "Sem nome"; p.Carimbo = m.Groups[2].Value; }
            else { p.Nome = baseName; p.Carimbo = ""; }

            DateTime dt;
            p.CapturadoEm = DateTime.TryParse(Str(root, "CapturadoEm"), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ? dt : File.GetLastWriteTime(path);
            p.Computador = Str(root, "Computador");

            foreach (var mo in Lista(root, "Monitores").OfType<Dictionary<string, object>>())
            {
                var mi = new MonitorInfo
                {
                    Dispositivo = Str(mo, "Dispositivo"),
                    Principal = Bool(mo, "Principal"),
                    Bounds = new Rect32(Int(mo, "X"), Int(mo, "Y"), Int(mo, "Largura"), Int(mo, "Altura")),
                };
                var au = Obj(mo, "AreaUtil");
                mi.Work = au != null ? new Rect32(Int(au, "X"), Int(au, "Y"), Int(au, "Largura"), Int(au, "Altura")) : mi.Bounds;
                p.Monitores.Add(mi);
            }

            int i = 0;
            foreach (var j in Lista(root, "Janelas").OfType<Dictionary<string, object>>())
            {
                var w = new WindowEntry
                {
                    OrdemZ = j.ContainsKey("OrdemZ") ? Int(j, "OrdemZ") : i,
                    Titulo = Str(j, "Titulo"),
                    Classe = Str(j, "Classe"),
                    Executavel = Str(j, "Executavel"),
                    LinhaComando = Str(j, "LinhaComando"),
                    Argumentos = Str(j, "Argumentos"),
                    Arquivos = Strings(j, "Arquivos"),
                    PastasExplorer = Strings(j, "PastasExplorer"),
                    Maximizada = Bool(j, "Maximizada"),
                    SempreVisivel = Bool(j, "SempreVisivel"),
                    X = Int(j, "X"), Y = Int(j, "Y"), Largura = Int(j, "Largura"), Altura = Int(j, "Altura"),
                };
                var proc = Str(j, "Processo");
                w.Processo = string.IsNullOrEmpty(proc) ? (string.IsNullOrEmpty(w.Executavel) ? "" : Path.GetFileName(w.Executavel)).ToLowerInvariant() : proc.ToLowerInvariant();
                var mon = Obj(j, "Monitor");
                if (mon != null)
                {
                    w.MonitorDispositivo = Str(mon, "Dispositivo");
                    w.MonitorBounds = new Rect32(Int(mon, "X"), Int(mon, "Y"), Int(mon, "Largura"), Int(mon, "Altura"));
                    var au = Obj(mon, "AreaUtil");
                    w.MonitorWork = au != null ? new Rect32(Int(au, "X"), Int(au, "Y"), Int(au, "Largura"), Int(au, "Altura")) : w.MonitorBounds;
                    // Completa a área útil do monitor em capturas antigas
                    var mi = p.Monitores.FirstOrDefault(x => x.Dispositivo == w.MonitorDispositivo);
                    if (mi != null && au != null && Obj(mon, "AreaUtil") != null && mi.Work.Equals(mi.Bounds)) mi.Work = w.MonitorWork;
                }
                p.Janelas.Add(w);
                i++;
            }
            p.Janelas = p.Janelas.OrderBy(w => w.OrdemZ).ToList();
            return p;
        }

        public static void Save(Profile p)
        {
            if (!Directory.Exists(Pasta)) Directory.CreateDirectory(Pasta);
            if (string.IsNullOrEmpty(p.Carimbo)) p.Carimbo = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var destino = Path.Combine(Pasta, NomeArquivo(p.Nome, p.Carimbo));
            if (p.Arquivo != null && !string.Equals(p.Arquivo, destino, StringComparison.OrdinalIgnoreCase) && File.Exists(destino))
                destino = Path.Combine(Pasta, NomeArquivo(p.Nome, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")));

            // Renumera a ordem z de acordo com a posição na lista
            for (int k = 0; k < p.Janelas.Count; k++) p.Janelas[k].OrdemZ = k;

            var root = new Dictionary<string, object>
            {
                ["CapturadoEm"] = p.CapturadoEm.ToString("s"),
                ["Computador"] = p.Computador ?? Environment.MachineName,
                ["Monitores"] = p.Monitores.Select(m => (object)new Dictionary<string, object>
                {
                    ["Dispositivo"] = m.Dispositivo, ["Principal"] = m.Principal,
                    ["X"] = m.Bounds.X, ["Y"] = m.Bounds.Y, ["Largura"] = m.Bounds.W, ["Altura"] = m.Bounds.H,
                    ["AreaUtil"] = RectObj(m.Work),
                }).ToList(),
                ["Janelas"] = p.Janelas.Select(j => (object)new Dictionary<string, object>
                {
                    ["OrdemZ"] = j.OrdemZ, ["Titulo"] = j.Titulo, ["Classe"] = j.Classe, ["Processo"] = j.Processo,
                    ["Executavel"] = j.Executavel, ["LinhaComando"] = j.LinhaComando, ["Argumentos"] = j.Argumentos,
                    ["Arquivos"] = j.Arquivos.Cast<object>().ToList(), ["PastasExplorer"] = j.PastasExplorer.Cast<object>().ToList(),
                    ["Maximizada"] = j.Maximizada, ["SempreVisivel"] = j.SempreVisivel,
                    ["X"] = j.X, ["Y"] = j.Y, ["Largura"] = j.Largura, ["Altura"] = j.Altura,
                    ["Monitor"] = new Dictionary<string, object>
                    {
                        ["Dispositivo"] = j.MonitorDispositivo,
                        ["X"] = j.MonitorBounds.X, ["Y"] = j.MonitorBounds.Y, ["Largura"] = j.MonitorBounds.W, ["Altura"] = j.MonitorBounds.H,
                        ["AreaUtil"] = RectObj(j.MonitorWork),
                    },
                }).ToList(),
            };

            var sb = new StringBuilder();
            Write(sb, root, 0);
            var tmp = destino + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(true));
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(tmp, destino);
            if (p.Arquivo != null && !string.Equals(p.Arquivo, destino, StringComparison.OrdinalIgnoreCase) && File.Exists(p.Arquivo))
                File.Delete(p.Arquivo);
            p.Arquivo = destino;
        }

        public static void Delete(Profile p)
        {
            if (p.Arquivo != null && File.Exists(p.Arquivo)) File.Delete(p.Arquivo);
        }

        public static string NomeArquivo(string nome, string carimbo)
        {
            nome = Regex.Replace((nome ?? "").Trim(), @"[\\/:*?""<>|]", "_");
            return (nome.Length > 0 ? nome + "_" : "") + carimbo + ".json";
        }

        static Dictionary<string, object> RectObj(Rect32 r) =>
            new Dictionary<string, object> { ["X"] = r.X, ["Y"] = r.Y, ["Largura"] = r.W, ["Altura"] = r.H };

        // ---------------------------------------------------------------- leitura tolerante

        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        static bool Bool(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool b && b;
        static int Int(Dictionary<string, object> d, string k)
        {
            if (!d.TryGetValue(k, out var v) || v == null) return 0;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }
        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        static IEnumerable<object> Lista(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is IEnumerable e && !(v is string) && !(v is IDictionary) ? e.Cast<object>() : Enumerable.Empty<object>();
        // Capturas antigas gravaram listas vazias como "{}": só aceita strings não vazias
        static List<string> Strings(Dictionary<string, object> d, string k) =>
            Lista(d, k).OfType<string>().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        // ---------------------------------------------------------------- escrita indentada

        static void Write(StringBuilder sb, object v, int ind)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> d:
                    if (d.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    int i = 0;
                    foreach (var kv in d)
                    {
                        sb.Append(' ', (ind + 1) * 2); WriteString(sb, kv.Key); sb.Append(": ");
                        Write(sb, kv.Value, ind + 1);
                        if (++i < d.Count) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', ind * 2).Append('}');
                    break;
                case IList l:
                    if (l.Count == 0) { sb.Append("[]"); break; }
                    sb.Append("[\n");
                    for (int k = 0; k < l.Count; k++)
                    {
                        sb.Append(' ', (ind + 1) * 2);
                        Write(sb, l[k], ind + 1);
                        if (k < l.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', ind * 2).Append(']');
                    break;
                default: WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
