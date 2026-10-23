# Development

Kiwindow captures the position, size, monitor, stacking order and open file/folder of every top-level window, and restores them later, reopening what is closed. It ships two ways from one codebase:

- **Standalone app** (`Kiwindow.exe`, Inno Setup installer, per-user, no UAC).
- **SwissTechKnife module** (`kiwindow-<ver>.zip`, listed in the host catalog).

Both host the same view and use the SwissTechKnife design system, so they look and behave the same.

## Layout

| Path | What |
|---|---|
| `src/Kiwindow.Core` | Everything: `Engine/` (capture, restore, profile store, Win32), `Ui/` (`KiwindowView`, map, dialog, strings), `Theme/Brand.xaml` (logo) and `KiwindowModule` (SwissTechKnife entry point) |
| `src/Kiwindow` | Standalone shell: window, command line, single instance |
| `tests/Kiwindow.Tests` | Console test runner (profile JSON, strings parity, module smoke test with a fake host) |
| `lib/SwissTechKnife.Sdk.dll` | SDK: compile-only for the module, shipped with the standalone app for the theme (`scripts/update-sdk.ps1`) |
| `installer/Kiwindow.iss` | Inno Setup script |
| `assets/brand` | Rasters generated from `Theme/Brand.xaml` (`scripts/render-brand.ps1`) |

## Engine in short

- **Capture** enumerates visible top-level windows in z-order, skipping tool windows, owned popups and cloaked windows, and records the process command line (files it opened) and Explorer folders (Shell COM).
- **Restore** (`Restorer`): windows already open are matched (same process, stable class, title or folder) and moved with `SWP_ASYNCWINDOWPOS` or a single `SetWindowPlacement`; missing programs are launched with `CreateProcess` + `STARTUPINFO` position hints; a `SetWinEventHook` loop places new windows the moment they show and corrects apps that move themselves; z-order is applied in one `DeferWindowPos` batch.
- **Profiles** are one JSON file each in `%LOCALAPPDATA%\Kiwindow\profiles`, named `<name>_<yyyy-MM-dd_HH-mm-ss>.json`. The format (Portuguese keys) is the original LayoutJanelas one and stays compatible. Engine identifiers are Portuguese for the same reason; new code is English.

## Environment variables

| Variable | Effect |
|---|---|
| `KIWINDOW_HOME` | Data folder instead of `%LOCALAPPDATA%\Kiwindow` (isolated testing) |
| `KIWINDOW_LANG` | `en` or `pt-BR`, overrides the Windows language (standalone) |
| `KIWINDOW_SNAPSHOT` | Render the window to this PNG once idle and exit (`docs/preview.png`, visual review) |

## Versioning and releases

- `<Version>` in `Directory.Build.props` is the single source of truth: app, installer, `module.json` and package name.
- Bumping it on `main` runs `.github/workflows/release.yml`: build, tests, installer and module package, then the GitHub release `vX.Y.Z` with `Kiwindow-X.Y.Z-setup.exe` and `kiwindow-X.Y.Z.zip`.
- Add the version's English notes to `releaseNotes` in `src/Kiwindow.Core/module.json` in the same change; they become the release body and the SwissTechKnife catalog notes.
- The SwissTechKnife catalog sync (every 15 minutes) picks the new `kiwindow-X.Y.Z.zip` up on its own: no change in the host repository after the first entry.

## Localization

UI text lives in `src/Kiwindow.Core/Ui/Strings.cs`, English and Brazilian Portuguese. Every key must exist in both tables (the tests fail otherwise). XAML: `{k:Loc Key}`; code: `Strings.Get` / `Strings.Format`. The standalone app follows Windows' language; the module follows the SwissTechKnife setting.
