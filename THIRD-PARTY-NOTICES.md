# Third-party notices

Own WinMim code: GPL-3.0. Each component below retains its own license.

## Windhawk runtime

Version 1.7.3, GPL-3.0. Unmodified official runtime; corresponding source archive: `vendor/sources/windhawk-v1.7.3.zip`.
Upstream: https://github.com/ramensoftware/windhawk/tree/v1.7.3

LLVM runtime components retain their upstream licenses; see `content/licenses/LLVM-LICENSE.txt`. Official Windhawk debug helper libraries retain their respective upstream terms.

## Modules

Upstream sources are pinned at commit `3f11069419fad9103a56cf78b28d293fee6c20cf`. Original WinMim modules are under src/WinMim.Native and retain their stated GPL-3.0 license.
For modules without an explicit license, the upstream contribution policy states MIT. See `vendor/windhawk-mods/UPSTREAM-README.md`.

The icon-resource-redirect binary includes a WinMim build-time Wh_ModAfterInit addition to refresh icons from the hooked Explorer process; see tools/build_modules.py. The pinned upstream file remains unchanged.
The classic-taskbar-properties binary includes original GPL-3.0 WinMim integration with Open-Shell settings and its shutdown-button preference. See src/WinMim.Native/classic-taskbar-openshell.inc.cpp and tools/build_modules.py; the pinned upstream source retains its original license and remains unchanged.
The Aerexplorer binary includes an original GPL-3.0 WinMim read-only diagnostic export; see src/WinMim.Native/aerexplorer-status.inc.cpp. The pinned upstream source remains unchanged.
The optional legacy Microsoft display.dll provider is verified and supplied separately in the private lab. It is not included in either archive and is not relicensed.

| Module | Author | Version | License | Source |
|---|---|---|---|---|
| Aerexplorer | aubymori | 1.8.2 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/aerexplorer.wh.cpp) |
| Aero Flyout Fix | aubymori | 1.1.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/aero-flyout-fix.wh.cpp) |
| Aero Tray | aubymori | 1.0.2 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/aero-tray.wh.cpp) |
| Basic Themer | aubymori | 1.1.1 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/basic-themer.wh.cpp) |
| Classic Explorer Status Bar | Anixx | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-explorer-statusbar.wh.cpp) |
| Classic Explorer Treeview | Waldemar | 1.1.3 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-explorer-treeview.wh.cpp) |
| Classic Menus | aubymori | 1.1.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-menus.wh.cpp) |
| Classic Theme | Anixx | 1.2.8 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-theme-enable.wh.cpp) |
| Classic UWP Fix | Ingan121 | 0.3 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-uwp-fix.wh.cpp) |
| Control Panel Revival | AdmXP8 | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/control-panel-revival.wh.cpp) |
| Disable Office Hotkeys | ItsProfessional | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/disable-office-hotkeys.wh.cpp) |
| Eradicate Immersive Menus | aubymori | 1.1.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/eradicate-immersive-menus.wh.cpp) |
| Resource Redirect | m417z | 1.3 | GNU General Public License v3.0 | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/icon-resource-redirect.wh.cpp) |
| Legacy File Copy | rounk-ctrl | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/legacy-file-copy.wh.cpp) |
| Message Box Fix | aubymori | 2.3.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/msg-box-font-fix.wh.cpp) |
| Old This PC Commands | aubymori | 1.0.1 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/old-this-pc-commands.wh.cpp) |
| Aero Peek on "Show desktop" button hover | m417z | 1.0.2 | GNU General Public License v3.0 | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/taskbar-show-desktop-button-aero-peek.wh.cpp) |
| Transparent Screensaver Fix | aubymori | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/transparent-screensaver-fix.wh.cpp) |
| UIFILE Override | xalejandro | 1.0.3 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/uifile-override.wh.cpp) |
| Win32 Tray Clock Experience | aubymori | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win32-tray-clock-experience.wh.cpp) |
| Windows 7/8.x Alt+Tab Loader | aubymori | 2.1.1 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-alttab-loader.wh.cpp) |
| Windows 7 Style UAC Dim | aubymori | 1.0.1 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-style-uac-dim.wh.cpp) |
| Windows 7 Clock Spacing | aubymori | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/windows-7-clock-spacing.wh.cpp) |
| Classic Taskbar and Start Menu Properties | babamohammed | 3.5.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-taskbar-properties.wh.cpp) |
| Redirect Settings to Control Panel | babamohammed | 10.0.36 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/settings-to-control-panel.wh.cpp) |
| Windows 7/8.1 Action Center Recreation | babamohammed | 2.2.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-action-center-recreation.wh.cpp) |
| Windows Vista/7 Legacy Applet Restorer | babamohammed | 3.2.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-legacy-applet-restorer.wh.cpp) |
| Windows 7 Network Flyout Recreation | babamohammed | 5.2.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-network-flyout-recreation.wh.cpp) |
| Windows Vista/7 "Open with" Dialog Restorer | babamohammed | 1.1.0 | MIT | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-open-with-dialog.wh.cpp) |
| Classic Display Control Panel Restorer | babamohammed | 1.1.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/win7-display-control-panel-restorer.wh.cpp) |
| Classic Theme Explorer Lite | Anixx | 1.3 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-theme-explorer-lite.wh.cpp) |
| Classic theme transparency fix | Anixx | 1.5 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-theme-transparency-fix.wh.cpp) |
| Disable Navigation Bar | ItsProfessional | 1.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/disable-navigation-bar.wh.cpp) |
| Remove Command Bar | Waldemar | 1.0.1 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/remove-command-bar.wh.cpp) |
| Enable SyslistView32 | anixx | 2.0.0 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/syslistview32-enabler.wh.cpp) |
| Classic Taskbar 3D buttons Lite | Anixx | 1.4 | MIT (Windhawk mods contribution policy) | [source](https://github.com/ramensoftware/windhawk-mods/blob/3f11069419fad9103a56cf78b28d293fee6c20cf/mods/classic-taskbar-buttons-lite.wh.cpp) |
| WinMim: классические переходы оболочки | WinMim contributors | 0.1.0 | GPL-3.0 | [source](src/WinMim.Native/winmim-shell-behavior.wh.cpp) |

## Optional installers

These installers are downloaded directly from upstream on a user action; they are not bundled in this release.
- Open-Shell 4.4.198: MIT. https://github.com/Open-Shell/Open-Shell-Menu
- SecureUxTheme 4.0.0: LGPL-2.1. https://github.com/namazso/SecureUxTheme
- OpenGlass 3.0.2.3749: GPL-3.0. https://github.com/ALTaleX531/OpenGlass

## Optional lab visual style

Aero10 Seven by vaporvance is a separate CC BY-NC-SA 4.0 package; Microsoft assets retain their rights. The theme, wallpaper and start-button images are not bundled or relicensed as GPL. The explicit tools/fetch_desktop_packages.py --with-aero10 option obtains the external upstream package for personal lab use. See https://github.com/vaporvance/aero10 and docs/SEVEN-DESKTOP-LAB.md.

## Private reference resources and extras

The optional private W7Aero-Redstone theme, 56 resource-only icon libraries, games, Desktop Gadgets, Media Center, Windows 7 WAV sounds and DVD Appx/license are supplied separately for the private lab. None is bundled or relicensed. The original WinMim coordinator validates pinned files and retains native uninstallers. See docs/SEVEN-PROGRESS-20261024.md.

## Classic95 artwork

Five original homage icons by ke2083 are included under GPL-3.0, pinned at d1cb96c9841c9ce321d7c95c0ee266e0d483c702. See content/classic95/LICENSE, UPSTREAM-README.md and sources.json. They are not extracted Microsoft Windows 95 binaries. The Start button in the Classic95 profile uses the existing system icon and the native Open-Shell classic renderer.

## Original tools and artwork

The system-native Windows Forms interface, remaining WPF utilities, transaction engine, Mines game, gadgets, media player, Alt+Tab implementation and generated taskbar texture are original WinMim code. No Revert8Plus visual resource packs or proprietary StartIsBack binaries are included.
