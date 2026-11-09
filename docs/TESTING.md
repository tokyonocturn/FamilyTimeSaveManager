# Testing

## Automated tests

```powershell
dotnet test tests/FamilyTimeSaveManager.Core.Tests/FamilyTimeSaveManager.Core.Tests.csproj -c Release
```

Result at release: **Passed! Failed: 0, Passed: 32, Skipped: 0, Total: 32** (xUnit, net8.0).

Coverage by area:

- **Episode 3 container** — parse with release label and sections; RLE decompression with CRC
  verification; checksum mismatch reported as corrupt; truncated payload; trailing data;
  impossible section count; unknown header reported as unsupported.
- **Episode 1/2 container** — parse without claiming checksums; legacy install resolves to the
  shared persistent folder; scanner flags a release label that contradicts the folder.
- **Backup** — primary + sidecar copies and recorded hashes; name sanitising and defaults;
  missing source leaves no partial folder; tampered backup fails verification and blocks restore.
- **Restore** — creates a pre-restore backup of the live file; refuses a missing destination
  folder.
- **Archive** — export/import round trip with hashes; rejects non-zip, parent traversal, absolute
  entry paths, manifest hash mismatch.
- **Retention/scheduling** — only old scheduled backups are pruned; manual backups are kept;
  scheduled backup only runs when the save changes.
- **Storage safety** — traversal and reserved names rejected; deleting a backup moves it to trash.
- **State** — settings, profiles (default protected), searchable history, comparison.

The tests use temporary folders under the system temp directory and never touch installed games or
real save files.

## Manual verification performed for 1.0.0

On Windows 11 with the user's own installations:

1. **Detection** — the app found five installs: Episode 1, Episode 2 DX11/DX12 build 37, Episode 3
   DX11/DX12 build 108, and resolved three save locations (shared LocalLow plus each Ep3
   `Sessions` folder).
2. **Real save** — a game-written Episode 3 save appeared in
   `...\Family_Time_Ep_3_WINDOWS_DX11_b108\FamilyTime_Data\Sessions` (16,477,745 bytes). The
   scanner reported `Valid`, 26 sections, `release.label` episode 3 / build 108, and all section
   CRC32 values matched. SHA-256 was recorded.
3. **Backup** — two manual backups of that save were created through `BackupService`; both
   re-hashed to the recorded SHA-256 and were reported verified. The library displayed them.
4. **UI** — all six pages were rendered and inspected at 1336×879; Overview, Saves, Library,
   History, Profiles and Settings showed real detected data. Layout regressions found during this
   pass (overlapping cards on Overview/Settings) were fixed and re-checked.
5. **Diagnostics** — `--diagnostics` produced the same five installs and locations in JSON.

## Not verified end-to-end

- No game-written Episode 1/2 `.world` save existed on the test machine. Legacy parsing is
  verified against recovered format documentation and fixtures only.
- Restores were exercised in automated tests against fixture saves; no real save was overwritten
  on the test machine.
- The installer was built and smoke-tested, but it is unsigned and was not tested on a clean
  Windows installation without the .NET SDK.
- Game-running detection was not exercised with a live game process during a restore.

Anything not listed above should be treated as unverified rather than assumed working.
