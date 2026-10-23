# ERRORS.md — Kiwindow

Known problems and how they were fixed. Check here before debugging something new.

## Build: XAML compiler fails with "Could not find assembly 'System.Web'"
- **Symptom:** `error MC1000 ... Could not find assembly 'System.Web, Version=4.0.0.0'` while compiling `Kiwindow.Core`.
- **Cause:** `System.Web.Extensions` (JavaScriptSerializer, used by the profile store) references `System.Web`; the WPF markup compiler loads every reference's dependencies.
- **Fix:** reference `System.Web` explicitly next to `System.Web.Extensions` in `Kiwindow.Core.csproj`.

## Icon glyph strings turned into plain "E722"
- **Symptom:** menu/toast icons showed the letters "E722" instead of the glyph.
- **Cause:** editing C# with `perl -pi` / shell heredocs: `\u` in a replacement is Perl's "uppercase next char", and the shell layer also eats backslashes.
- **Fix:** glyphs are written as `char.ConvertFromUtf32(0xE722)` in code. Edit sources with the file tools, not shell substitutions.

## Profile names from the standalone app and the module disagree
- **Cause (design):** both read `%LOCALAPPDATA%\Kiwindow\profiles`; the module does not use the host `DataDirectory` on purpose, so profiles are shared.
- **Note:** for isolated tests set `KIWINDOW_HOME`.

---

## Legacy (LayoutJanelas, the PowerShell/WPF predecessor)

## Restauração lenta (~16s) e programa reaberto à toa
- **Sintoma:** "não apareceu a tempo: [claude.exe] Claude"; restauração levou 15s+.
- **Causa:** a busca por janelas já abertas ignorava janelas minimizadas. O script relançava
  o programa; apps de instância única (Electron, etc.) não criam janela nova, então o loop
  esperava até o timeout.
- **Solução:** na restauração, candidatas incluem minimizadas (`Visiveis($true)`); `Posicionar`
  faz SW_RESTORE antes de mover. Timeout reduzido para 10s.

## Restaurar: "não apareceu a tempo" em todas as janelas que precisavam abrir (ex.: MPC-HC)
- **Sintoma:** nenhum processo era iniciado; todas as janelas a abrir davam timeout.
- **Causa:** no PowerShell 5.1, `$x = if (...) { @(...) } else { @() }` desenrola o array vazio para
  `$null`, e o `ConvertTo-Json` gravou `"PastasExplorer": {}`. Ao ler, `{}` vira um objeto "verdadeiro"
  com `Count = 1`, então toda janela era tratada como pasta do Explorer e o programa real nunca era aberto.
- **Solução:** na captura, listas com `[string[]]@(...)` explícito (gera `[]`). Na restauração,
  `Get-PastaSalva` só aceita strings não vazias, o que mantém compatíveis as capturas antigas.
- **Evitar:** nunca testar listas vindas de JSON com `-and .Count -gt 0`; validar o tipo dos itens.

## Terminal capturado junto / relançado com "-Embedding"
- **Causa:** no Windows Terminal, `GetConsoleWindow` não retorna a janela visível, então o próprio
  terminal do script era capturado. A linha de comando dele era `WindowsTerminal.exe -Embedding` (COM),
  que não abre janela, e o exe fica em WindowsApps (não dá para chamar direto).
- **Solução:** a captura ignora a janela em foco se ela for um terminal; ao relançar, remove `-Embedding`
  e usa `wt.exe` para o Windows Terminal.

## Explorer: pasta não abria / "não apareceu a tempo"
- **Causa 1:** lançar `explorer.exe "pasta"` a partir do processo do script às vezes não abre nada.
- **Solução 1:** abrir via `Shell.Application.Open(pasta)` (usa o Explorer já em execução; mais rápido).
- **Causa 2:** no Windows 11 a pasta pode abrir como **aba** de uma janela existente, sem janela nova.
- **Solução 2:** no loop de espera, consultar o mapa COM HWND→pastas; se a pasta surgir numa janela
  pré-existente, marca `VirouAba` (não espera mais).

## Notepad / apps com abas não eram reconhecidos ao reabrir
- **Causa:** Notepad do Win11 abre o arquivo como aba da janela existente (sem janela nova), e
  reposiciona a própria janela com atraso (~1s) depois de abrir. Título do Notepad começa com espaço.
- **Solução:** após 1s, aceita janela já existente do mesmo exe cujo título (normalizado com
  `Format-Titulo`: Trim + remove `\p{Cf}`) ficou igual ao salvo; vigilância de até 2,5s que
  reaplica a posição se o app se mexer sozinho.

## Lentidão geral do script
- **Causa:** `Add-Type` compilando C# a cada execução e `Get-CimInstance` por processo
  (inclusive dentro do loop de polling).
- **Solução:** DLL compilada em cache em `%LOCALAPPDATA%\LayoutJanelas\Nativo_<hash>.dll`;
  caminho do exe via `QueryFullProcessImageName` em C#; WMI só na captura, numa única consulta.

## App: uma janela assume a posição/tamanho de outra (a que abre mais rápido "rouba" o lugar)
- **Sintoma:** dois itens do mesmo programa (ex.: dois MPC-HC) relançados; a janela que aparece primeiro
  vai para a posição da outra, e a outra fica com a que sobrou.
- **Causa:** a janela nova era associada só por nome do exe + título. Apps como o MPC-HC abrem com título
  provisório; após 1,5s sem título final, o código "chutava" o primeiro pendente da lista (ordem z), que
  podia ser o item da janela que ainda nem tinha aberto.
- **Solução:** guardar o PID de cada processo lançado (CreateProcess / Process.Start) e associar a janela
  ao item cujo PID é o dono da janela (ou o pai dele). Itens cujo processo ainda está vivo ficam fora do
  "chute"; o chute por título/ordem só vale para quem não tem PID (Explorer) ou cujo processo repassou o
  arquivo para uma instância já aberta e saiu. Reproduzido/verificado com `app\_teste\rodar-troca.ps1`.

## App: MPC-HC ainda abria fora do lugar / "no lugar de outra" (mesmo com PID)
- **Sintoma:** com várias janelas do MPC-HC, de vez em quando algumas ficavam empilhadas no meio da tela,
  com o tamanho automático do vídeo/imagem.
- **Causa:** o MPC-HC cria às vezes uma janela auxiliar do mesmo processo ("Re-sincronização das Legendas",
  altura 0) antes da principal. A associação por PID aceitava qualquer janela do processo, então
  posicionava a auxiliar e a janela real ficava onde o MPC quisesse.
- **Solução:** exigir a mesma classe de janela da captura (comparando só a parte estável do nome:
  `WindowsForms10.*`, `HwndWrapper[...]` e `Afx:...` mudam o sufixo a cada execução) e ignorar janelas sem
  tamanho. Verificado com `app\_teste\rodar-mpc.ps1` (MPC-HC real, 10 janelas, várias rodadas).
- **Evitar:** nunca comparar nome de classe de janela por igualdade exata.
- **Diagnóstico:** toda aplicação grava `ultimo-aplicar.log` ao lado do .exe (como cada janela foi
  reconhecida e o resultado final de cada uma).

## App: janela que estava maximizada às vezes não ia para a posição salva
- **Causa:** apps (ex.: WinForms) reaplicam o próprio tamanho "restaurado" logo depois do SetWindowPlacement.
  Se isso acontecia antes do SetWinEventHook ser instalado, nenhum evento chegava e, com programas sendo
  lançados, o laço só corrigia via eventos.
- **Solução:** o laço de espera também verifica todas as janelas a cada volta e corrige as fora do lugar
  (limite de 6 correções por janela).

## App (LayoutJanelas.exe): build falha com "Could not find assembly 'System.Web'" (MC1000)
- **Causa:** `JavaScriptSerializer` (System.Web.Extensions) depende de System.Web; o compilador de XAML
  do WPF precisa resolver as duas referências.
- **Solução:** `<Reference Include="System.Web" />` junto de `System.Web.Extensions` no `.csproj`.

## App: build quebra com "O nome de tipo ou namespace Forms não existe" vindo de `_teste\`
- **Causa:** projetos SDK incluem todos os `.cs` das subpastas; o `JanelaTeste.cs` (WinForms) entrava no build.
- **Solução:** `<Compile Remove="_teste\**" />` no `.csproj`.

## Testes de restauração: script travava com `Start-Process -Wait`
- **Causa:** `-Wait` espera também os processos filhos; os programas abertos pela restauração nunca fecham.
- **Solução:** `Start-Process -PassThru` + `$p.WaitForExit()` (espera só o próprio processo).
- **Também:** passar `$null` para um parâmetro `string` de P/Invoke no PowerShell vira `""`
  (`FindWindow` não acha nada); usar `[NullString]::Value`.

## Testes de UI: cliques sintéticos podem cair em outra janela
- **Causa:** o Windows bloqueia `SetForegroundWindow` de processos em segundo plano; o app ficava atrás
  de outras janelas e o `mouse_event` atingia o que estivesse por cima.
- **Solução:** não usar mouse real. O app tem autoteste interno (`LAYOUTJANELAS_AUTOTESTE=<arquivo>`) que
  chama o mesmo código de arrastar/redimensionar; prints com `PrintWindow` (só a janela do app).

## Botão piscando / laranja na barra de tarefas após restaurar
- **Causa:** `SetForegroundWindow` bloqueado pelo foreground lock do Windows → o Windows
  sinaliza a janela (flash) em vez de ativá-la; apps recém-abertos também pedem atenção.
- **Solução:** `AllowSetForegroundWindow(ASFW_ANY)` antes de lançar, `AttachThreadInput` +
  fallback com `mouse_event` nulo para ativar, e `FlashWindowEx(FLASHW_STOP)` em todas as
  janelas no fim (repetido após 300ms).
