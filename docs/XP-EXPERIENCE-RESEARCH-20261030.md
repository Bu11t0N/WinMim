# Windows eXPerience: findings for WinMim XP

Research date: 2026-10-30. WinMim implementation baseline: `42af917315b7840480f20e25a37d6cec1642a957`. This is a source and materials review, not a completed port or a runtime compatibility test. Implementation assignment: [XP task](tasks/XP.md).

## What was examined

Read the official site's About page, six release/Extras pages, their linked Internet Archive descriptions and current file metadata. Inspected the archive's ISO directory listings for Extras Pack 2 and an R2 RC3 community mirror. Retrieved six small text files from Extras Pack 2 and read relevant public component sources. No downloaded program or script was executed; no VM or host appearance was changed. Full Windows images and the R2 `install.esd` were not downloaded or unpacked.

### Builds and materials

| Material | Verified finding | Relevance |
| --- | --- | --- |
| [About](https://experience.noncities.com/about) | The documented original mod uses Windows 10 Enterprise LTSC 2019. | Its resources and hooks cannot be presumed compatible with WinMim's LTSC 2021 / 19044. |
| [Initial Release](https://experience.noncities.com/downloads/initial/) | Original XP transformation; its System Restore recreation is described as nonfunctional. | A visual reference does not prove equivalent behavior. Current linked archive metadata contains screenshots but no ISO. |
| [Lonestar](https://experience.noncities.com/downloads/lonestar/) | Added Silver, Olive Green, Royale/Noir, screensavers and a task-manager selector. | Variant/resource inventory. Current linked archive metadata contains no ISO. |
| [Freestyle](https://experience.noncities.com/downloads/freestyle/) | Added Zune/Embedded and further icon changes. | Broader optional XP variants; not the first implementation priority. |
| [Freestyle SP1](https://experience.noncities.com/downloads/freestylesp1/) | Refines captions, icons, text rendering, sounds and visual styles; removes ClassicLogonShell by default. | Useful comparison cases, not an installer to run on WinMim's guest. |
| [Extras 1](https://experience.noncities.com/downloads/extras/rel1/) | Historical applications, wallpapers and ICOs. | Optional private resource references; not necessary to solve Luna frames. |
| [Extras 2](https://experience.noncities.com/downloads/extras/rel2/) | Plex, Slate, Watercolor, Firefox disguises and associated setup files. The linked archive explicitly excludes R2 compatibility. | Concrete evidence of the coordinator's composition. Extras Pack **Release 2 is not Windows eXPerience R2**. |
| R2 | The [official home page](https://experience.noncities.com/) announces R2 but does not link a complete source repository or an R2 download. A [community RC3 mirror](https://archive.org/details/en-us_windows_experience_r2_rc3) exposes an ISO directory containing `sources/install.esd`. | More relevant candidate reference, but uploader provenance, actual OS build and internal components remain unverified. Do not present it as an authenticated author release. |

Archive file observations, not publisher-authentication checks:

- SP1 currently lists `en-us_windows-experience_freestyle-sp1_hotfix_1.1_x64_dvd_d289cd96.iso`, 3,331,940,352 bytes; [linked item](https://archive.org/details/windows-experience-freestyle-sp1).
- Extras 2 lists `en-us_windows_experience_extras_pack_r2_dvd.iso`, 54,263,808 bytes; [linked item](https://archive.org/details/windows-experience-extras-rel-2).
- The community R2 RC3 ISO is 4,551,278,592 bytes, with `sources/install.esd` of 4,101,781,768 bytes. Only the outer directory was inspected. Claims about its internal DirectUI sidebar or Open-Shell fork still require examination of the actual files.

## How the inspected coordinator works

The [Extras 2 ISO listing](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/) exposes readable installer/configuration files. These are inspected third-party material, not instructions authorizing their execution.

| Inspected file | What it establishes | WinMim adaptation |
| --- | --- | --- |
| [INSTALL.BAT](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/INSTALL.BAT) | Copies visual styles, Open-Shell skins/button images/XML, FindeXer assets and metric presets into different locations. | Treat appearance as a coordinated set of independently journaled components. |
| [THEMES/Set-Theme.bat](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/THEMES%2FSet-Theme.bat) | Applies Open-Shell XML, invokes `SecureUXHelper`, coordinates T-Clock and FindeXer, and provides a metric reset. | Reimplement the sequence using current pinned dependencies, dependency checks and transactional rollback. Fixed sleeps and a warning about a black screen are not a recovery strategy. |
| [THEMES/METRICS/luna.reg](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/THEMES%2FMETRICS%2Fluna.reg) | Uses Trebuchet MS for captions, Tahoma elsewhere; sets caption/menu/scroll dimensions and 96-DPI assumptions. | Separate XP typography/geometry from the current classic metrics. Derive scaling; do not import the entire registry preset. |
| [Plex.theme](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/THEMES%2FMSSTYLES%2FPlex.theme) and [Plex.xml](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/THEMES%2FXMLS%2FPlex.xml) | `.theme` points to an `.msstyles`; Open-Shell has separate menu-skin and Start-image settings. Plex XML uses a Win7 menu layout, so it is not an XP menu preset. | Inspect which values actually affect each surface; do not copy a different appearance's complete XML. |
| [IE/UPGRADE.BAT](https://archive.org/download/windows-experience-extras-rel-2/en-us_windows_experience_extras_pack_r2_dvd.iso/IE%2FUPGRADE.BAT) | Its IE7/8 options replace Firefox customization files and an icon in a hard-coded profile. | Browser theming is optional and separate. It does not install a functional IE7/8 engine. |

The listing also contains `.msstyles`, `shellstyle.dll`/MUI, `.skin7`, button images and FindeXer data. Seeing those files does not establish how every original/R2 feature was implemented. The batch scripts do not contain a complete reversible transformation engine.

Private research copies were hashed with SHA-256:

| File | SHA-256 |
| --- | --- |
| `INSTALL.BAT` | `974928b4503ece13b417ca57a14ddfe652bfaceeee6809ddd2c4ac25c04036f7` |
| `THEMES/Set-Theme.bat` | `7bcb8e4c16f0afa4794ce6bbb20ac13e12296007b5555803a94a62c1c0cade7e` |
| `THEMES/METRICS/luna.reg` | `b18ca4dbbaf5dd6ae195cb1793519b69fec5f914476671ca794637aed57d76fb` |

## Source code: found, useful, and incomplete

The [Windows eXPerience GitHub organization](https://github.com/windows-experience) links back to the supplied website and currently lists two public repositories. A complete build recipe for the Windows image was not located in these sources.

| Component and inspected commit | Source findings | Decision |
| --- | --- | --- |
| [eXPerienceBar](https://github.com/windows-experience/eXPerienceBar/tree/2a79cdbad3f9516c9c08d6ec303fa3ac9d77bf3b) | C# / WPF / .NET 6 COM Explorer Bar. `MainSidebar.cs` describes a BHO that opens the bar, an HWND-hosted WPF control and dependence on restored Explorer-band support. `ShellController.cs` uses Shell COM navigation/events. `UI/Sidebar.xaml.cs` has empty handlers and test actions. | Architectural reference for a Common Tasks sidebar; not a finished drop-in replacement and not evidence for R2's alleged DirectUI implementation. No repository-wide license found in the inspected tree. |
| [eXPeriencefox](https://github.com/windows-experience/eXPeriencefox/tree/8cb323f55c6e95f603f221a0f91cd9e4a5e06b23) | CSS, userChrome JavaScript and Firefox startup configuration. README warns that browser updates break the customization. | Optional later module, outside the first desktop milestone. No repository-wide license found in the inspected tree. |
| [ClassicExplorer](https://github.com/kawapure/ClassicExplorer/tree/350685c4877cf4bae595095b10693cc9afc1f81b) | The [former repository](https://github.com/ephemeralViolette/ClassicExplorer) explicitly redirects here. C++/ATL address bar and throbber extensions, COM installation/uninstallation; source has window subclasses and Explorer integration. README says no prebuilt binaries are supplied. | Strong address-bar/throbber candidate, but first resolve component licensing, build and interference with the existing Explorer modules. No repository-wide license found in the inspected tree. |
| [Luna10 by vaporvance](https://www.deviantart.com/vaporvance/art/Luna10-907548273) | Linked by ClassicExplorer's README as its example window style. The author's page returned HTTP 403 during this review. | Candidate for actual Luna frames and controls. Package, license, supported build and exact contents not verified. No source repository confirmed; do not invent one. |
| [BasicThemer2](https://github.com/Ingan121/BasicThemer2/tree/13deb679e0a1b7d936eaa33ccfcc1823285d4672) | MIT [license](https://github.com/Ingan121/BasicThemer2/blob/13deb679e0a1b7d936eaa33ccfcc1823285d4672/LICENSE). Uses per-window `DwmSetWindowAttribute` non-client policy while keeping DWM, with a polling loop. | Verified alternative implementation reference, **not confirmed as Experience's bundled component**. Prefer adapting WinMim's existing event-driven observer over adding a second polling service. |

Publicly visible source and downloadable artwork are separate from redistribution permission. Existing permissively/GPL-licensed dependencies can be integrated with their notices. For unclear Experience/theme resources, keep private test packs and independent adapters; resolve terms before public vendoring. Archive metadata for Extras 2 labels it CC BY-NC-ND 4.0, which is not a blanket license for an editable GPL resource pack or Microsoft files.

## The concrete WinMim blocker

This conclusion comes from local source inspection, not speculation about Experience:

1. `Classic95Setup.ApplyThemeAsync` routes XP through `switchSession(false)` and the shared `windows95` shell-module plan. `ClassicSession` denies mapping access to the active session's `ThemeSection` to obtain classic rendering.
2. `winmim-classic-frames.wh.cpp` calls `SetThemeAppProperties(0)`, disabling process visual styling, and sets a non-client DWM policy. The [Microsoft API documentation](https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-setthemeappproperties) distinguishes control and non-client styling flags.
3. `ClassicAppearance.Desired` and `PersistentMetrics` retain classic caption geometry. All themes also select the same 95 icon manifest in `Classic95Profile`.

**Consequently an XP `.msstyles` cannot be expected to produce Luna controls while these classic overrides remain active.** XP needs a separate themed application/resume/restore strategy. Retain the proven classic strategy for 95–2000. Non-client policy experiments should preserve control theming and DWM; [DWMNCRENDERINGPOLICY](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmncrenderingpolicy) is a per-window policy, not an instruction to terminate the compositor.

Useful existing pieces: RetroBar XP Blue, Open-Shell Luna menu, hash-pinned runtime acquisition, journals, conflict-preserving rollback, cross-process operation lock, icon/resource adapters and native system-themed WinMim UI. Reuse them before choosing new replacements.

## Reference defects to turn into acceptance tests

SP1's own [issue list](https://experience.noncities.com/downloads/freestylesp1/) identifies failures involving theme transitions, scaling above 100%, non-English resources, expanded tray/volume, maximized Explorer and dead menu commands. Use these as test cases, not as acceptable WinMim behavior. Its improved screenshots also do not prove global fidelity.

Start with Luna Blue: frames **and** controls, actual XP icons, desktop, panel/menu and working Explorer navigation. Then complete dialog/resource coverage and Silver/Olive variants. Royale/Noir/Zune/Embedded and period applications are later optional work. Validate each layer against an original XP reference; Experience is an additional implementation reference, not the definition of XP.

## Still unresolved

- Authenticated R2 package provenance, actual version and internal manifest; bundled hook sources/configuration; exact Open-Shell fork and DirectUI sidebar sources.
- Luna10 package availability, terms, geometry and compatibility with 19044, x86/x64 applications and Russian resources.
- Whether a native themed taskbar can improve on RetroBar enough to justify replacing it. Keep a single taskbar owner during experiments.
- Complete XP resource-ID map, secure desktop/logon compatibility, per-monitor DPI and cold-boot recovery. None were tested in this research turn.
