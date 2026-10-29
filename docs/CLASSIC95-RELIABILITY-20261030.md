# Classic95 reliability, original resources and UI performance

## Corrections

The Windows 95 apply/restore actions now run outside the UI thread. Native system-color and metric updates can notify other windows synchronously; blocking the same UI thread that receives those notifications can freeze an interactive apply even when a console test passes. The application keeps its message loop responsive during these actions.

The earlier startup dispatcher deadlock and mapped-DLL rollback fixes remain in place. A real UI test now exercises the actual Apply/Restore buttons and checks message-loop activity, native appearance, application visual-style flags and non-client rendering after restore.

## Classic rendering

`winmim-classic-frames` disables application visual-style rendering in the current lab user session and selects native non-client rendering for standard captioned windows. It observes window events for its own process using a blocking message loop, without polling. It supports x86 and x64.

The module retains the process's original theme flags and tracks its non-client policy changes. Stop restores the flags and notifies affected windows. It does not replace custom per-window theme class overrides. Windows 95 restore also posts a theme-change notification after restoring access to the session theme section.

The renderer is restricted to `AEROFORGE-LAB`. Critical processes, VMware integration, recovery CLI/test workers and Chromium renderer/GPU/utility helpers are excluded. It does not inject code into DWM or winlogon. The WinMim UI participates in this renderer; other modules continue to exclude it.

The pinned upstream [Disable Taskbar Thumbnails](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/taskbar-thumbnails.wh.cpp) module is enabled only in the Classic95 preset, with hover previews disabled and list behavior on grouped-item clicks. Standard tooltips remain enabled.

## Original icons

The local lab now has 13 exact ICO resources extracted from the maintainer's Windows 95 Russian retail media (`shell32.dll` file version 4.0.0.950). The desktop's main icons, Start button and main Start-menu entries use these resources. Extraction preserves their original image sizes, masks and palettes.

Only source identity, resource IDs and hashes are included in `content/classic95/original-reference.json`. The Microsoft DLL, ISO and extracted ICO files remain outside this repository and outside distributable archives.

To prepare the supported private pack from your own media:

```powershell
python tools/import_classic95_icons.py --shell32 C:\path\to\shell32.dll --output C:\path\to\a-new-private-directory
```

The output must be new. The importer verifies the exact supported DLL hash before parsing PE resources; it never loads or executes the DLL. Put the verified private output in `C:\AeroForgeLab\PrivateAssets\Windows95Icons` inside the lab. The profile verifies each ICO against the repository's reference before creating a plan. Other Windows versions/localizations are not accepted without a separately verified reference.

Without that private pack, the redistributable GPL homage artwork remains the fallback and the UI labels it as temporary.

## Measured optimization

The recovery list uses `History(false)`, which skips binary backups. Selecting a journal reads only its error details. Batched list updates also avoid repainting after every row.

In a warm-cache local comparison of six journals containing 12 MiB of binary backups:

| Reading method | Managed allocations | Elapsed time |
| --- | ---: | ---: |
| Full receipts | 24.00 MiB | 20.04 ms |
| Metadata | 4.01 MiB | 15.49 ms |

This is about 83% fewer temporary managed allocations for that operation. It is a synthetic journal-reading measurement, not a claim about total application memory or startup speed.

Packaging now omits obsolete generated module DLLs from the portable archive. Source archives omit generated runtime/module binaries and raw local evidence/session notes.

## Validation and remaining scope

Local baseline: 56 Core tests, 9 Python tests and 56 verified native DLLs across 39 catalog modules. Original-resource hashes, invalid-import rejection, journal error reading, recovery exclusions and loaded-DLL rollback have regression coverage.

The final real Apply/Restore UI cycle passed: native appearance, process visual-style flags and the form's non-client rendering state all returned to their pre-apply values. The window processed 54 timer ticks during apply, confirming that its message loop remained responsive in that test. Live metadata reported both classic rendering and taskbar thumbnail modules loaded.

Real UI testing uses a disposable VM and controlled session helper tasks. UAC is not automated. Normal startup still needs the session's privilege phase; unattended startup is not claimed.

The main VM's interrupted mixed state was retained in `Before-Mixed95-Recovery-20261029`, then the main VM was returned to `Bugfix-Baseline-20261029`. Source fixes, private resources and visual trials were kept separately.

Classic95 remains experimental. Modern tray surfaces, app-specific custom drawing, some Explorer content and other system resources still require separate adapters. Complete visual identity with Windows 95 is not claimed.
