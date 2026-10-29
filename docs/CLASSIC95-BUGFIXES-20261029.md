# Classic95 bug fixes — 2026-10-29

## Changes

- **Startup deadlock:** `OnStartup` blocked the WPF dispatcher with `GetAwaiter().GetResult()` while classic resume awaited an elevated child process. The continuation could require the blocked dispatcher. Startup now awaits resume asynchronously and keeps application lifetime explicit until it completes or fails.
- **Session callback bypass:** resume ignored the configured session-switch callback and created its own elevation helper. It now uses the same asynchronous callback as apply/restore.
- **Validation ordering:** the saved palette file and its journal hash are checked before changing the session's ThemeSection access. A damaged saved file must not leave a newly switched session behind.
- **Guest recovery processes:** generated module configuration excludes VMware Tools, graphics service, authentication service and user integration processes, including for modules whose upstream include list contains `*`. Existing module-specific exclusions are retained.
- **Mapped DLL rollback:** Windows can deny deletion of a module that remains mapped after engine shutdown. Rollback now moves such managed DLLs out of their active path into `Data/Engine/Retired`. Later engine start/stop removes released copies only if their recorded SHA-256 still matches; edited files are preserved. No unrelated process is terminated to release a DLL.

The deadlock is a concrete code defect. It is not proof that it caused every symptom of the earlier black-screen incident.

## Validation

Local build: 53 Core tests, including wildcard module exclusions and rollback with a genuinely loaded DLL; 8 Python tests; 53 native DLLs pass integrity/ABI verification. File staging/rollback smoke also passed.

Live testing uses a linked disposable VM from a powered-off Seven baseline. The original VM and reference VMs are preserved. The lab harness controls only the named Classic session helper tasks; it does not automate UAC. A journaled, test-only startup override allows explicit resume testing after logon without a competing automatic elevation request.

The first cold-boot test reached resume, but module readiness required the exact missing `applicationframe.pdb`. Its upstream GUID/age was verified before adding it to the local lab cache. The test then exposed mapped-DLL deletion failures during restore. After the rollback fix, retrying that same incomplete receipt completed successfully and Classic95 became inactive.

The final cold-boot test passed: resume completed using the asynchronous session callback, a tampered palette was rejected before session mutation, Classic95 restored successfully, and native appearance matched the pre-apply snapshot. The test VM's guest integration remained available. The original earlier black-screen failure is not fully attributed to one cause by these results.

Unattended elevation during normal Classic95 startup remains a separate integration limitation. The test used the controlled helper tasks described above, not automated UAC interaction.
