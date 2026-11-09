# Changelog

All notable changes to FamilyTime Save Manager are documented here.
The format follows Keep a Changelog; this project uses semantic versioning.

## [1.0.0] - 2026-11-09

### Added

- Automatic detection of Family Time Episodes 1, 2 and 3 installations, including DX11 and DX12
  builds, with manual episode and renderer overrides.
- Read-only save discovery for Episode 3 `.family` (`WSC`) containers and Episode 1/2 `.world`
  (`WSD`) containers, including per-section CRC32 verification, RLE decoding and `release.label`
  metadata.
- Save list with episode, format, size, timestamps, section count, embedded build/episode and
  SHA-256, plus corruption, truncation and episode-mismatch reporting.
- Manual backups with names, descriptions and profiles; copies are re-hashed before being marked
  verified.
- Restore flow with explicit confirmation, block-while-running guard, SHA-256 verification and a
  mandatory pre-restore recovery backup.
- Export/import of backup archives with zip-slip, absolute-path and hash validation.
- Named profiles, searchable history, scheduled backups with retention that never prunes manual
  backups, and a dark resizable WinForms interface with activity log and progress indicator.
- 32 automated core tests covering format parsing, integrity, backup, restore, recovery, archive
  hardening, retention, detection and storage safety.
- Portable self-contained Windows x64 build, per-user Inno Setup installer, SHA-256 checksums and
  GitHub Actions CI/release workflows.

### Verified

- Five real installations detected on the test machine (Ep1, Ep2 DX11/DX12 build 37,
  Ep3 DX11/DX12 build 108).
- A real Episode 3 build 108 save (16,477,745 bytes, 26 sections) parsed with every CRC32 matching
  and `release.label` reporting episode 3 / build 108.
- Backups of that save created and re-verified against their manifests.
