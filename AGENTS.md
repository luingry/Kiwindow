## Conventions

- Code, comments, docs, commit messages and release notes in English; UI text in English and Brazilian Portuguese (`src/Kiwindow.Core/Ui/Strings.cs`, every key in both tables). The engine keeps its original Portuguese identifiers and JSON keys (profile file compatibility); new code is English.
- One codebase, two shells: the standalone app (`src/Kiwindow`) and the SwissTechKnife module (`KiwindowModule`) host the same `KiwindowView`. Never fork UI between them; use `ViewOptions` for the differences.
- This is a SwissTechKnife module: follow the host's rules (`../SwissTechKnife/AGENTS.md`, `docs/MODULES.md`, `docs/DESIGN_SYSTEM.md`). Views use SDK tokens and styles only; no hard-coded colors, sizes or durations in XAML (the map in `LayoutPreview` mirrors token values in code).
- Never block the UI thread: capture and restore run on their own STA threads.
- Versioning: `<Version>` in `Directory.Build.props` is the single source of truth. Bumping it on `main` releases `vX.Y.Z` (installer + module package) from `.github/workflows/release.yml`; add the version's English notes to `releaseNotes` in `src/Kiwindow.Core/module.json` in the same change.
- Logo changes: edit `src/Kiwindow.Core/Theme/Brand.xaml`, then `scripts/render-brand.ps1`. Never edit the rasters.
- Edit UTF-8 files with the file tools (or `[IO.File]` with an explicit encoding), never Windows PowerShell `Set-Content`.
- Run `powershell -File scripts/build.ps1` before handing off; check visual changes with `KIWINDOW_SNAPSHOT`; record non-trivial fixes in `ERRORS.md`.
