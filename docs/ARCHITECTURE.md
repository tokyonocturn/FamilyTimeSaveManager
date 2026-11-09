# Architecture

## Projects

| Project | Target | Responsibility |
| --- | --- | --- |
| `FamilyTimeSaveManager.Core` | `net8.0` | Detection, parsing, storage, backup/restore/archive, scheduling, history, settings, profiles. No UI dependencies. |
| `FamilyTimeSaveManager.App` | `net8.0-windows` | WinForms shell: dark theme controls, six pages, dialogs, activity log, diagnostics and screenshot harness. |
| `FamilyTimeSaveManager.Core.Tests` | `net8.0` | xUnit tests for the core. |

The UI talks to the core through `AppServices`, a composition root that owns every service and
the single local data folder. There is no network code anywhere in the solution.

## Core services

- **`GameInstallDetector`** — bounded folder scan (20,000 directories / 20 s) for
  `FamilyTime.exe` + `FamilyTime_Data`, reading `app.info` and Unity version, with folder-name
  hints, assembly save-format markers, and settings overrides.
- **`SaveLocationService`** — maps installs to save folders: Episode 3 Sessions inside the
  install, Episodes 1/2 in the shared LocalLow folder, plus manual overrides.
- **`SaveScanner`** — enumerates saves and sidecars, parses metadata, computes SHA-256, checks
  CRCs and assigns a status (`Valid`, `Suspicious`, `Corrupt`, `Unsupported`, `Empty`,
  `Unreadable`). Read-only.
- **`FamilySaveFormat`** — `WSC`/`WSD` container parser, CRC-32 and RLE decoding.
- **`BackupService` / `BackupRepository`** — copy, re-hash, manifest, verification; deletion moves
  folders into `backups/.trash`.
- **`RestoreService`** — verifies the backup, snapshots the live file as a pre-restore backup, then
  writes with temporary files and atomic replace; verifies the result.
- **`ArchiveService`** — export/import of `.zip` archives with traversal, absolute path, duplicate
  and hash checks.
- **`AutoBackupService` / `RetentionService`** — interval checks while the app runs, only when a
  save changed; retention applies only to scheduled backups.
- **`SettingsStore`, `ProfileStore`, `HistoryStore`, `ActivityLog`** — JSON stores written
  atomically, with corrupt files quarantined rather than overwritten.

## Local data layout

Everything lives under `%LOCALAPPDATA%\FamilyTimeSaveManager`:

```
settings.json
profiles.json
history.json
logs\                 daily activity logs
exports\              scratch space for archive export
backups\<id>\manifest.json
backups\<id>\files\   verified copies of the save and sidecars
backups\.trash\       backups removed from the library
```

Each backup folder is self-describing; the manifest records original paths, roles, lengths and
SHA-256 values. Imported archives are validated before a manifest is written.

## Threading and UI

Scans, copies, hashing and restores run on the thread pool. The UI shows a busy bar and disables
rescan while working; log entries are marshalled back to the UI thread. Long operations never run
on the message loop.

## Safety invariants

1. A save file is only read during scanning, comparison and backup.
2. A write to a save happens only during an explicitly confirmed restore.
3. Before that write, the current destination is copied into a pre-restore backup.
4. Every backup copy and every restore is checked against SHA-256.
5. All paths derived from archives or settings are validated to stay inside their intended root.
6. Deleting a backup moves it to trash; deleting a profile moves its backups to Default.

## Developer helpers

- `FamilyTimeSaveManager.exe --diagnostics <path>` writes a JSON snapshot of installs, locations,
  scans, backups and settings for support.
- `FamilyTimeSaveManager.exe --screenshot <page> <path>` opens the app, scans, navigates and saves
  an offscreen render for documentation.
