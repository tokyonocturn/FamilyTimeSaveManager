# User guide

## First run

Open FamilyTime Save Manager. The Overview scans for Family Time installations and save folders.
No game file is modified. If an installation or episode is not recognised, open **Settings →
Installations** and set the episode/renderer, or add the folder that contains `FamilyTime.exe`.

## Saves

**Saves** lists every detected save with episode, format, size, modified time and status.

- Select a row to see its path, section count, embedded release label, SHA-256 and sidecar state.
- **Back up now** creates a verified backup.
- **Compare with…** compares two selected saves by metadata and file hashes. Hold Ctrl to select
  two rows.
- **Open save folder** opens Explorer at the save.
- Status meanings:
  - **Healthy** — container parsed and, for Episode 3, every CRC matched.
  - **Check recommended** — parsed but something is unusual (for example an episode label that
    contradicts the folder).
  - **Corrupt or incomplete** — checksum, length or structure failure. Back it up only if you want
    a forensic copy.
  - **Unsupported / Unreadable / Empty** — the format is unknown or the file cannot be read.

Saves in the shared Episode 1/2 folder are shown as **shared** because the format does not record
an episode.

## Backups

**Backup library** lists backups with kind, episode, profile, creation time, size and integrity.

- **Restore…** verifies the backup, warns about episode/build mismatches and a running game, asks
  for confirmation, creates a pre-restore backup of the live save, then writes and re-verifies.
- **Verify** re-hashes every file against the manifest.
- **Export selected** writes a `.zip` archive you can move to another PC.
- **Import archive** accepts only archives whose paths and hashes validate.
- **Move to trash** moves the backup into `backups\.trash`; it does not delete game saves.
- Backup kinds: Manual, Scheduled, Pre-restore, Imported. Retention only prunes Scheduled backups.

Profiles group backups by playthrough or patch. Deleting a profile moves its backups to Default.

## Scheduled backups

Settings → Scheduled backups:

- Enable checks while the app is running and choose the interval (5–1440 minutes).
- Keep at most N scheduled backups per profile.
- Optionally delete scheduled backups older than N days (0 = never).
- Optionally skip when no save changed.

Manual, pre-restore and imported backups are never pruned. There is no background service: if the
app is closed, no scheduled backup runs.

## Settings

- **Installations** — assign episode/renderer or add an install folder.
- **Save folders** — override Ep1/Ep2/Ep3 folders; empty means automatic.
- **Safety** — warn while the game is running, verify checksums on restore.
- **Data, logs and privacy** — opens the local data folder and logs.

Settings are stored in `%LOCALAPPDATA%\FamilyTimeSaveManager\settings.json`.

## Troubleshooting

- **No saves found** — play the game and save once, then Rescan. For Episodes 1/2 look in
  `%USERPROFILE%\AppData\LocalLow\sgthale\Family Time`.
- **Wrong episode** — set the override in Settings and Rescan.
- **Backup will not restore** — verify it first; a hash mismatch blocks restore. Restore is also
  blocked while `FamilyTime.exe` is running.
- **Reset the app** — close it and rename `%LOCALAPPDATA%\FamilyTimeSaveManager`. This removes
  backups and history, so move them first if needed.

## Uninstall

Use **Settings → Apps → FamilyTime Save Manager → Uninstall**, or the Start Menu uninstaller.
Uninstalling does not delete your backups in `%LOCALAPPDATA%\FamilyTimeSaveManager`; remove that
folder manually if you want them gone.
