# Work register and integration gates

Status date: 2026-11-11. Operational coordination is accepted by the existing project coordinator. This register records preservation and reported implementation checkpoints; it does not certify release readiness. Main baseline at publication is `1a2d9a94d488d01de2a9ed659474109290d46709`.

| Workstream / owner | Checkpoint and publication | Remaining work / gate |
| --- | --- | --- |
| Research / source researcher | [Draft PR #1](https://github.com/Bu11t0N/WinMim/pull/1), `35ad93501b62272456e6cea8f66e9c297c8a7ce8` | Late Luna10 2.1.5, Seven/W2K static findings preserved. Video coverage is partial; 19044 compatibility and artwork redistribution remain unverified. |
| Windows 2000 / theme implementer | [Draft PR #2](https://github.com/Bu11t0N/WinMim/pull/2), `69266ec2c26978def80972205d10746cec28c276` | Historical November 5 apply/repeat/switch/restore cycle reported successful. Quick Access and round navigation buttons remain; Explorer, DPI, dialogs and logon acceptance incomplete. |
| XP / theme implementer | Local implementation checkpoint `47d3116`; publication assigned separately, PR confirmation pending when this register was written | **Do not merge XP before cancellation/Restore and Explorer crashes are fixed.** An interrupted lifecycle experiment reports monitor timeout and 72.02-second apply. Ordinary-cycle success does not close this blocker. |
| Coordination / integrator | Separate documentation branch; no product or VM changes in this stage | Review publication history and source provenance, then own shared lifecycle integration. |
| Seven / coordinator after lifecycle | Existing main implementation and research | Taskbar texture and Start tint comparison pending; one rendering layer per experiment. |

Publication, review, integration and release acceptance are distinct states. PR presence does not establish passing CI or runtime acceptance. Check the linked PRs for subsequent changes; checkpoint hashes above are not moving-branch promises.

## Immediate next stages

1. Review preserved research and product draft PRs, their complete diffs/history and private-resource exclusions. Confirm XP publication without duplicating the already assigned publication task. Review these sanitized assignments: [video/Telegram](tasks/VIDEO-TELEGRAM-RESEARCH.md), [Windows 2000](tasks/WINDOWS-2000.md), [XP](tasks/XP.md).
2. XP implementer owns a narrow P0 cancellation/Restore investigation: ShellHost desktop-stop scope, monitor exit, automatic restart and callback/subclass teardown. Require targeted regression tests, completion of missing-pack/cancellation/foreign-edit/Restore cases, restored baseline, no new attributable Explorer Application Error, and explanation of the apply-time regression. Do not expand into new XP variants during this fix.
3. Integrator owns shared lifecycle decisions after XP P0. XP and 2000 overlap in eleven files: catalog, NativeMainForm, Classic95Profile/Setup/Themes, DesktopBackend/Profile, Models, classic-frame native module, Core tests and own_modules.py. Review semantics, not only textual conflicts. Preserve journals, guards, locks, theme/session ACL handling and user changes; regenerate catalogs/hashes from the chosen sources.
4. Use a separate integration branch before any main merge. For affected 95/2000/XP/Seven transitions verify apply, repeat, resume, switch and restore; failure/cancellation, missing/changed assets, user edits, Explorer restart and actual logon/cold start. Record exact commit, host build and dependency versions. Product merge remains gated by review and evidence.
5. Continue the Windows 2000 Explorer milestone with working Back/Forward/Up, address entry, views, folder options and test-file operations, then 100/125% DPI and rollback. Seven follows stable lifecycle with matched wallpaper/DPI/resolution and controlled texture/skin comparisons. Longer-term dialogs, resource completeness and other host versions remain planned.

## Evidence limits and references

The November 11 audit independently built main with no errors and 43 warnings and ran nine Python tests. Its clean checkout lacked prepared Windhawk/RetroBar dependencies: Core reported 58 passes and two preparation-related failures. That is not a fully passing Core result or a demonstrated new product defect. No new native compilation or VM acceptance was performed by this documentation publication.

Revert8Plus already has a sufficient public mechanism map in [R8P-MODULE-MAP.md](R8P-MODULE-MAP.md) and an evidence-qualified [comparison](R8P-COMPARISON.md). They describe historical static findings and validation limits; they are not certification of feature parity. Private evidence references may be unavailable in a public checkout. No extracted installer script, payload or private resource bundle is needed for this register.

Private deployment credentials, machine identities, diagnostic captures, original resource packs and local chat routing stay outside public Git. Progress reports must state the task, branch/full SHA/PR, actual checks, unmet criteria and next bounded action. Current broader architecture and state documents need a later reconciliation after lifecycle integration, not speculative rewriting during this publication stage.
