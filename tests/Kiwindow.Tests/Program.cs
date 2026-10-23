using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kiwindow.Engine;
using Kiwindow.Ui;

namespace Kiwindow.Tests
{
    /// <summary>Console test runner: prints one line per test and "N passed, M failed"; exit code = failures.</summary>
    internal static class Program
    {
        static int _passed, _failed;

        [STAThread]
        static int Main()
        {
            Run("every string exists in both languages", StringsParity);
            Run("profile round-trips through JSON", ProfileRoundTrip);
            Run("legacy captures with {} lists still load", LegacyEmptyLists);
            Run("default names skip taken ones", DefaultNames);
            Run("module builds its page and tray menu like the host", ModuleSmoke.Run);
            Console.WriteLine($"{_passed} passed, {_failed} failed");
            return _failed;
        }

        static void Run(string name, Action test)
        {
            try { test(); _passed++; Console.WriteLine("ok    " + name); }
            catch (Exception ex) { _failed++; Console.WriteLine("FAIL  " + name + ": " + ex.Message); }
        }

        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

        static void StringsParity()
        {
            var en = Strings.EnglishTable.Keys.ToList();
            var pt = Strings.PortugueseTable.Keys.ToList();
            var missing = en.Except(pt).Concat(pt.Except(en)).ToList();
            Check(missing.Count == 0, "missing: " + string.Join(", ", missing));
        }

        static string TempFile(string json)
        {
            var dir = Path.Combine(Path.GetTempPath(), "kiwindow-tests");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Test_2026-01-02_03-04-05.json");
            File.WriteAllText(path, json);
            return path;
        }

        static void ProfileRoundTrip()
        {
            var path = TempFile(@"{ ""CapturadoEm"": ""2026-01-02T03:04:05"", ""Monitores"": [ { ""Dispositivo"": ""DISPLAY1"", ""Principal"": true, ""X"": 0, ""Y"": 0, ""Largura"": 1920, ""Altura"": 1080 } ],
                ""Janelas"": [ { ""OrdemZ"": 1, ""Titulo"": ""B"", ""Processo"": ""b.exe"", ""X"": 10, ""Y"": 20, ""Largura"": 300, ""Altura"": 200, ""Arquivos"": [], ""PastasExplorer"": [] },
                               { ""OrdemZ"": 0, ""Titulo"": ""A"", ""Processo"": ""a.exe"", ""Maximizada"": true, ""Arquivos"": [""C:/x.txt""], ""PastasExplorer"": [] } ] }");
            var p = ProfileStore.Load(path);
            Check(p.Nome == "Test", "name from file: " + p.Nome);
            Check(p.Janelas.Select(j => j.Titulo).SequenceEqual(new[] { "A", "B" }), "z-order");
            Check(p.Janelas[0].Maximizada && p.Janelas[1].Largura == 300, "fields");
            Check(p.Monitores.Count == 1 && p.Monitores[0].Work.W == 1920, "work area falls back to bounds");
        }

        static void LegacyEmptyLists()
        {
            var path = TempFile(@"{ ""Janelas"": [ { ""Titulo"": ""A"", ""Processo"": ""a.exe"", ""Arquivos"": {}, ""PastasExplorer"": {} } ] }");
            var p = ProfileStore.Load(path);
            Check(p.Janelas[0].Arquivos.Count == 0 && p.Janelas[0].Pasta == null, "{} must read as an empty list");
        }

        static void DefaultNames()
        {
            Strings.SetLanguage("en");
            Check(KiwindowView.DefaultName(new List<string>()) == "Layout 1", "first");
            Check(KiwindowView.DefaultName(new[] { "Layout 2", "Work" }) == "Layout 3", "skips to a free number");
            Check(KiwindowView.DefaultName(new[] { "Layout 3", "x" }) == "Layout 4", "taken 3");
        }
    }
}
