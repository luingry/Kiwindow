using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Markup;

namespace Kiwindow.Ui
{
    /// <summary>
    /// UI text in English (default) and Brazilian Portuguese. The language is picked once at startup:
    /// the standalone app follows Windows, the SwissTechKnife module follows the host's language setting.
    /// Every key must exist in both tables (the tests check it); a missing key falls back to English.
    /// </summary>
    public static class Strings
    {
        public const string English = "en";
        public const string PortugueseBrazil = "pt-BR";

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["App.Tagline"] = "Window layouts, back in place",
            ["Capture"] = "Capture layout",
            ["Capture.Tip"] = "Save the position and size of every open window as a new profile",
            ["Profiles"] = "Profiles",
            ["OpenFolder"] = "Open profiles folder",
            ["Apply"] = "Apply layout",
            ["Apply.Tip"] = "Reopen what is closed and put every window back in its saved place",
            ["Apply.Busy"] = "Applying…",
            ["ApplyThis"] = "Apply this profile",
            ["Save"] = "Save",
            ["Discard"] = "Discard",
            ["Unsaved"] = "Unsaved changes",
            ["More"] = "More options",
            ["Rename.Tip"] = "Click to rename",
            ["Menu.Replace"] = "Replace with the windows open now",
            ["Menu.Duplicate"] = "Duplicate",
            ["Menu.ShowFile"] = "Show file in Explorer",
            ["Menu.CopyCommand"] = "Copy shortcut command",
            ["Menu.Delete"] = "Delete profile",
            ["Menu.Front"] = "Bring to front",
            ["Menu.Up"] = "Move up one",
            ["Menu.Down"] = "Move down one",
            ["Menu.Back"] = "Send to back",
            ["Menu.UseCurrent"] = "Use the open window's current position",
            ["Menu.ToggleMax"] = "Maximize / restore",
            ["Menu.Remove"] = "Remove from profile",
            ["Preview"] = "Preview",
            ["Preview.Hint"] = "Drag to move · edges resize · Shift turns snapping off · arrows nudge (Ctrl resizes)",
            ["Preview.Empty"] = "No profile selected",
            ["Preview.Primary"] = "primary",
            ["Preview.Monitor"] = "Monitor {0}",
            ["Windows"] = "Windows",
            ["Windows.Hint"] = "Top of the list is the front-most window",
            ["Col.Window"] = "Window",
            ["Col.Monitor"] = "Monitor",
            ["Col.Width"] = "Width",
            ["Col.Height"] = "Height",
            ["Col.Maximized"] = "Maximized",
            ["Maximized"] = "Maximized",
            ["Remove.Tip"] = "Remove from profile",
            ["Empty.Title"] = "No profiles yet",
            ["Empty.Text"] = "Arrange your windows the way you like them and capture the layout. Kiwindow reopens what is closed and puts everything back with one click.",
            ["Meta"] = "{0} windows · {1} monitors · captured {2}",
            ["Meta.One"] = "{0} windows · 1 monitor · captured {1}",
            ["ProfileLine"] = "{0} windows · {1}",
            ["Untitled"] = "Untitled",
            ["Copy.Suffix"] = "{0} (copy)",
            ["Dialog.Cancel"] = "Cancel",
            ["Dialog.SaveTitle"] = "Save changes?",
            ["Dialog.SaveText"] = "“{0}” has unsaved changes.",
            ["Dialog.ReplaceTitle"] = "Replace windows?",
            ["Dialog.ReplaceText"] = "The windows in “{0}” will be replaced by the windows open now. You can review them before saving.",
            ["Dialog.Replace"] = "Replace",
            ["Dialog.DeleteTitle"] = "Delete profile?",
            ["Dialog.DeleteText"] = "“{0}” will be deleted. This cannot be undone.",
            ["Dialog.Delete"] = "Delete",
            ["Toast.Captured"] = "Layout captured",
            ["Toast.CapturedText"] = "{0} windows saved in “{1}”.",
            ["Toast.CaptureFailed"] = "Could not capture",
            ["Toast.SaveFailed"] = "Could not save",
            ["Toast.Saved"] = "Profile saved",
            ["Toast.Replaced"] = "Windows replaced",
            ["Toast.ReplacedText"] = "{0} windows captured. Save to keep them.",
            ["Toast.DuplicateFailed"] = "Could not duplicate",
            ["Toast.DeleteFailed"] = "Could not delete",
            ["Toast.ApplyFailed"] = "Could not apply",
            ["Toast.Applied"] = "{0} of {1} windows in {2:0.0} s",
            ["Toast.AlreadyOpen"] = "{0} already open",
            ["Toast.Opened"] = "{0} opened",
            ["Toast.CommandCopied"] = "Command copied",
            ["Toast.CommandCopiedText"] = "Paste it in a Windows shortcut to apply this profile without opening Kiwindow.",
            ["Toast.NotFound"] = "Window not found",
            ["Toast.NotFoundMany"] = "{0} windows of {1} are open and none has the same title.",
            ["Toast.NotRunning"] = "{0} is not running.",
            ["Tray.Capture"] = "Capture layout",
            ["Tray.Apply"] = "Apply “{0}”",
            ["Tray.Applied"] = "{0}: {1} of {2} windows in place.",
            ["Tray.Captured"] = "Captured “{0}” with {1} windows.",
        };

        static readonly Dictionary<string, string> PtBr = new Dictionary<string, string>
        {
            ["App.Tagline"] = "Layouts de janelas, de volta no lugar",
            ["Capture"] = "Capturar layout",
            ["Capture.Tip"] = "Salva a posição e o tamanho de todas as janelas abertas como um novo perfil",
            ["Profiles"] = "Perfis",
            ["OpenFolder"] = "Abrir pasta de perfis",
            ["Apply"] = "Aplicar layout",
            ["Apply.Tip"] = "Reabre o que estiver fechado e coloca cada janela na posição salva",
            ["Apply.Busy"] = "Aplicando…",
            ["ApplyThis"] = "Aplicar este perfil",
            ["Save"] = "Salvar",
            ["Discard"] = "Descartar",
            ["Unsaved"] = "Alterações não salvas",
            ["More"] = "Mais opções",
            ["Rename.Tip"] = "Clique para renomear",
            ["Menu.Replace"] = "Substituir pelas janelas abertas agora",
            ["Menu.Duplicate"] = "Duplicar",
            ["Menu.ShowFile"] = "Mostrar arquivo no Explorer",
            ["Menu.CopyCommand"] = "Copiar comando de atalho",
            ["Menu.Delete"] = "Excluir perfil",
            ["Menu.Front"] = "Trazer para frente",
            ["Menu.Up"] = "Subir uma posição",
            ["Menu.Down"] = "Descer uma posição",
            ["Menu.Back"] = "Enviar para trás",
            ["Menu.UseCurrent"] = "Usar a posição atual da janela aberta",
            ["Menu.ToggleMax"] = "Maximizar / restaurar",
            ["Menu.Remove"] = "Remover do perfil",
            ["Preview"] = "Pré-visualização",
            ["Preview.Hint"] = "Arraste para mover · bordas redimensionam · Shift desliga o encaixe · setas ajustam (Ctrl = tamanho)",
            ["Preview.Empty"] = "Nenhum perfil selecionado",
            ["Preview.Primary"] = "principal",
            ["Preview.Monitor"] = "Monitor {0}",
            ["Windows"] = "Janelas",
            ["Windows.Hint"] = "O topo da lista é a janela da frente",
            ["Col.Window"] = "Janela",
            ["Col.Monitor"] = "Monitor",
            ["Col.Width"] = "Largura",
            ["Col.Height"] = "Altura",
            ["Col.Maximized"] = "Maximizada",
            ["Maximized"] = "Maximizada",
            ["Remove.Tip"] = "Remover do perfil",
            ["Empty.Title"] = "Nenhum perfil ainda",
            ["Empty.Text"] = "Arrume as janelas do jeito que você gosta e capture o layout. O Kiwindow reabre o que estiver fechado e coloca tudo de volta no lugar com um clique.",
            ["Meta"] = "{0} janelas · {1} monitores · capturado em {2}",
            ["Meta.One"] = "{0} janelas · 1 monitor · capturado em {1}",
            ["ProfileLine"] = "{0} janelas · {1}",
            ["Untitled"] = "Sem nome",
            ["Copy.Suffix"] = "{0} (cópia)",
            ["Dialog.Cancel"] = "Cancelar",
            ["Dialog.SaveTitle"] = "Salvar alterações?",
            ["Dialog.SaveText"] = "“{0}” tem alterações não salvas.",
            ["Dialog.ReplaceTitle"] = "Substituir janelas?",
            ["Dialog.ReplaceText"] = "As janelas de “{0}” serão trocadas pelas janelas abertas agora. Você pode revisar antes de salvar.",
            ["Dialog.Replace"] = "Substituir",
            ["Dialog.DeleteTitle"] = "Excluir perfil?",
            ["Dialog.DeleteText"] = "“{0}” será excluído. Não dá para desfazer.",
            ["Dialog.Delete"] = "Excluir",
            ["Toast.Captured"] = "Layout capturado",
            ["Toast.CapturedText"] = "{0} janelas salvas em “{1}”.",
            ["Toast.CaptureFailed"] = "Não foi possível capturar",
            ["Toast.SaveFailed"] = "Não foi possível salvar",
            ["Toast.Saved"] = "Perfil salvo",
            ["Toast.Replaced"] = "Janelas substituídas",
            ["Toast.ReplacedText"] = "{0} janelas capturadas. Salve para manter.",
            ["Toast.DuplicateFailed"] = "Não foi possível duplicar",
            ["Toast.DeleteFailed"] = "Não foi possível excluir",
            ["Toast.ApplyFailed"] = "Não foi possível aplicar",
            ["Toast.Applied"] = "{0} de {1} janelas em {2:0.0} s",
            ["Toast.AlreadyOpen"] = "{0} já abertas",
            ["Toast.Opened"] = "{0} abertas agora",
            ["Toast.CommandCopied"] = "Comando copiado",
            ["Toast.CommandCopiedText"] = "Cole num atalho do Windows para aplicar este perfil sem abrir o Kiwindow.",
            ["Toast.NotFound"] = "Janela não encontrada",
            ["Toast.NotFoundMany"] = "Há {0} janelas de {1} abertas e nenhuma com o mesmo título.",
            ["Toast.NotRunning"] = "{0} não está aberto.",
            ["Tray.Capture"] = "Capturar layout",
            ["Tray.Apply"] = "Aplicar “{0}”",
            ["Tray.Applied"] = "{0}: {1} de {2} janelas no lugar.",
            ["Tray.Captured"] = "“{0}” capturado com {1} janelas.",
        };

        static Dictionary<string, string> _table = En;

        public static string Language { get; private set; } = English;

        public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

        internal static IReadOnlyDictionary<string, string> EnglishTable => En;

        internal static IReadOnlyDictionary<string, string> PortugueseTable => PtBr;

        public static void SetLanguage(string language)
        {
            bool pt = language != null && language.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
            Language = pt ? PortugueseBrazil : English;
            _table = pt ? PtBr : En;
            Culture = CultureInfo.GetCultureInfo(pt ? "pt-BR" : "en-US");
        }

        /// <summary>Follows the Windows display language.</summary>
        public static void UseSystemLanguage() => SetLanguage(CultureInfo.CurrentUICulture.Name);

        /// <summary>Follows SwissTechKnife's <c>language</c> setting (English when missing).</summary>
        public static void UseHostLanguage(string moduleDataDirectory)
        {
            string language = English;
            try
            {
                // DataDirectory is %LOCALAPPDATA%\SwissTechKnife\data\<module>; settings.json sits two levels up.
                var settings = Path.Combine(moduleDataDirectory, "..", "..", "settings.json");
                if (File.Exists(settings))
                {
                    var m = Regex.Match(File.ReadAllText(settings), "\"language\"\\s*:\\s*\"([^\"]*)\"");
                    if (m.Success) language = m.Groups[1].Value;
                }
            }
            catch { }
            SetLanguage(language);
        }

        public static string Get(string key) =>
            _table.TryGetValue(key, out var s) ? s : En.TryGetValue(key, out s) ? s : key;

        public static string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);
    }

    /// <summary>XAML: <c>Text="{k:Loc Capture}"</c>.</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key) { Key = key; }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
    }
}
