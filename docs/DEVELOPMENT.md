# Development priorities

## Goal

Recreate the appearance and behavior of selected Windows generations, including surfaces reached through normal desktop use. The long-term host targets are Windows 10 and Windows 11. Current full-desktop work is restricted to a Windows 10 LTSC 2021 lab.

Initial classic and XP profiles are now [implemented](CLASSIC-FAMILY-20261030.md); improve their historical fidelity using the [classic plan](CLASSIC-PRE-XP-PLAN.md). XP currently has a Luna taskbar/menu and classic frames. Windows 1.x–3.x and NT 3.x are excluded from current implementation.

## Immediate work

1. Correct the Windows 7 taskbar texture and Start-menu color using the [appearance research and comparison plan](SEVEN-APPEARANCE-RESEARCH-20261030.md). Compare one layer at a time before changing backends.
2. Preserve the tested Classic95 recovery baseline. UI apply/restore and controlled reboot/resume have passed; ordinary startup still needs the privilege phase, and the original black-screen cause is not fully established.
3. Keep one coherent controller/catalog version in the lab and retire stale launch paths. Use the [current project handoff](PROJECT-STATE.md) rather than outdated historical counts.
4. Complete the Windows 7 gap list: Explorer, icons, tray, system dialogs and classic destinations. Compare matching surfaces against a Windows 7 reference VM.
5. Refine the integrated RetroBar classic/XP backend: real Start routing, tray behavior, DPI, multi-monitor operation and cold-start recovery. Complete Luna window styling separately; do not enable overlapping taskbar owners.
6. Define a redistributable resource pack with per-file provenance instead of depending on private reference assets.

## Next milestones

| Milestone | Acceptance criteria |
| --- | --- |
| Reliable lab baseline | Clean boot and logon, correct engine/module versions, no unexplained black screen, tested restore from the UI and after an interrupted apply. |
| Windows 7 completeness | Tracked visual and behavioral gaps covered by repeatable comparisons; missing or external components clearly identified. |
| Windows 95 foundation | Repeatable apply/restore and reboot tests, classic taskbar/Start/Explorer behavior, complete licensed resource coverage for agreed surfaces. |
| Classic/XP fidelity | Refine the initial NT 4.0, 98/SE, 2000, Me and XP profiles with version-specific resources, Explorer/dialog behavior and full Luna window styling. 1.x–3.x/NT 3.x excluded from current implementation. |
| Additional appearances | Per-appearance resource manifest, shell behavior adapter, host compatibility matrix, visual references and restore tests. |
| Windows 11 host | Dedicated adapter and exact-build verification; do not infer compatibility from a Windows 10 result. |

Remaining catalog entries are placeholders until implementation and verification are complete. A profile should choose one owner for each desktop surface rather than enable overlapping shell patchers.

## References

- [Open-source landscape](OPEN-SOURCE-LANDSCAPE.md)
- [Architecture](ARCHITECTURE.md)
- [Windows 7 visual gaps](WIN7-VISUAL-GAPS.md)
- [Classic95 implementation](CLASSIC95-20261029.md)
- [Feature inventory](FEATURES.md)

Historical notes can refer to raw evidence retained only in the private local workspace; those captures are intentionally not part of the source repository.
