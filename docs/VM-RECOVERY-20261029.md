# Lab recovery — 2026-10-29

## Observations

After the interrupted Windows 95 experiment and host power loss, the main VMware guest showed a black screen. VMware reported Tools as running, but guest commands failed with `VIX_E_UNRECOGNIZED_COMMAND_IN_GUEST` / `Unknown Command`; file operations and guest screenshots were unavailable. A hard restart of that state did not restore usable guest control.

A read-only extraction from the snapshot disk chain found an older WinMim autostart error: a controller expected a different staged Aerexplorer DLL hash. This establishes a controller/engine version mismatch in the saved history, **not the root cause of the black screen**. The exact original failure remains unresolved.

## Recovery performed

1. Preserved the affected state as `Before-Black-Screen-Recovery-20261029` and verified it appeared in the snapshot list.
2. Restored `Before-WinMim-Windows95-20261029` in the main lab only. Reference VMs were not changed.
3. Confirmed the Windows desktop, Explorer, Open-Shell and guest RPC were available again.
4. Updated the main WinMim application/CLI/catalog and classic artwork. The executable contains the new maintainer-supplied icon.
5. Verified all content hashes and all 43 runtime/module files used by the active Seven engine; no mismatch was found.
6. Performed a normal guest reboot. Windows completed logon and displayed the desktop. Explorer, Windhawk and the updated WinMim ran; the resume journal recorded `StartRequested` and no resume error remained.

## Limits and next action

The active lab profile is **Seven**. Windows 95 is not currently applied. Its code and the failed VM state have been retained. This recovery verifies the updated Seven baseline after reboot; it does not validate Windows 95 cold boot or establish full visual fidelity.

Investigate the Windows 95 snapshot on a separate disposable clone. Isolate session theme switching, automatic elevation, wildcard modules and controller versioning before another full cold-boot attempt. Keep guest integration processes available for recovery and record exact boot/logon events.

Raw reports and screenshots remain in the private local workspace and are excluded from Git.
