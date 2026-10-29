# Contributing to WinMim

Start with the [README](README.md) and [development priorities](docs/DEVELOPMENT.md).

## Development workflow

1. Install the prerequisites and run `tools/build.ps1 -PrepareNative`.
2. Work on one adapter, module or behavior at a time.
3. Run `tools/build.ps1`, the Python tests and native verification described in the README.
4. Test desktop changes in a disposable VM. Record the exact Windows build/update revision, dependency versions and WinMim commit.
5. Describe the changed behavior, relevant tests, restore result and remaining limitations in the pull request.

Do not start the shell engine or inject modules into the development host as part of a build or automated test. Do not disable antivirus or add exclusions.

## Changes and recovery

- Record the original state before every managed mutation.
- Compare the expected state before rollback; preserve unrelated changes and report conflicts.
- Keep external installers separate from journaled profile operations.
- Test failure recovery as well as the successful path for substantial changes.
- Verify logon/reboot behavior separately from same-session apply/restore.
- Do not describe a compiled module as visually verified.

## Upstream code and artwork

Preserve author headers, licenses and source provenance. Pin upstream versions/commits and review hash changes together with the corresponding source changes. Do not add proprietary binaries, extracted Windows resource packs, credentials, Microsoft symbol caches or private VM files to Git.

`prepare.py` refreshes the source lock metadata. A newly generated hash is not by itself proof that a download is trusted. Changes to the pinned source revision and dependency download locations require review.

## Bug reports

Include the profile, exact build/UBR, commit, dependency versions, steps to reproduce, expected result and actual result. Screenshots of the same surface at the same resolution and DPI are useful for historical comparisons. Remove personal data and credentials from logs.

## Licensing contributions

Contributions to original WinMim code are provided under the repository's GPL-3.0 license. Separately licensed upstream files keep their original terms and attribution.
