# Compatibility and detection

## How installations are found

The detector scans common locations (Downloads, Program Files, Program Files (x86), LocalLow,
Saved Games and user-configured extra folders) with a bounded budget: at most 20,000 directories
and 20 seconds. A folder qualifies when it contains `FamilyTime.exe` and a `FamilyTime_Data`
folder.

Episode and renderer are inferred from, in order:

1. Manual overrides saved in Settings.
2. The save-format marker inside the managed assembly (`.family` ⇒ Episode 3, `.world` ⇒ 1/2).
3. Folder-name hints such as `Episode 1`, `Ep_3`, `DX11`, `DX12` or `Build37`.
4. The Unity build version and `app.info` (company `sgthale`, product `Family Time`).

Because the executables of all three episodes share the same Unity version, the executable version
alone cannot distinguish them. When the evidence is ambiguous the app says so (`episode inferred`,
`Unknown renderer`) and lets you assign the episode and renderer in Settings → Installations.

## Save folder rules

- **Episode 3** — `<install>\FamilyTime_Data\Sessions`. This is verified: a real build 108 save was
  written there.
- **Episodes 1 and 2** — `%USERPROFILE%\AppData\LocalLow\sgthale\Family Time`, shared by both
  builds. The container has no episode field, so the app labels these saves **shared** and warns
  about attribution. Use a manual save-folder override if you keep separate copies.
- **Manual overrides** always win for the selected episode.

## Builds exercised

| Build | Renderer | Detected | Save format parsed |
| --- | --- | --- | --- |
| Episode 1 Patreon | unknown | yes | format recovered; no real save available |
| Episode 2 build 37 | DX11 | yes | format recovered; no real save available |
| Episode 2 build 37 | DX12 | yes | format recovered; no real save available |
| Episode 3 build 108 | DX11 | yes | yes, real 26-section save with valid CRC32 |
| Episode 3 build 108 | DX12 | yes | same format; no separate DX12 save available |

## Unsupported or changed builds

If a future build changes the container, the manager reports **Unsupported** or **Corrupt** with a
reason instead of attempting a repair or silently claiming compatibility. Backups already made
remain usable as files; the manager will still copy and verify them, but it will not describe them
as compatible with a build it cannot parse.

## Requirements

- Windows 10 (21H2 or later) or Windows 11, x64.
- No .NET installation required for the released portable/installer builds (self-contained).
- No network, no accounts, no telemetry.
