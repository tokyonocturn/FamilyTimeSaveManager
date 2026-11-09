# Save formats

Everything here was recovered from the shipped assemblies of the user's own installations
(`Assembly-CSharp.dll`, build 108 for Episode 3 and the Episode 1/2 builds) and checked against a
real Episode 3 save. The formats are undocumented by the game; treat this as a best-effort
reverse-engineering result, not a contract.

All integers are little-endian.

## Episode 3 — `.family` (`WSC`)

```
header:
  char[3]  "WSC"                     87 83 67
  int32    sectionCount

section (repeated sectionCount times):
  int32    idLength
  byte[]   id                        UTF-8
  byte     codec                     0 = stored raw, 1 = RLE
  int32    rawLength                 decoded size
  int32    storedLength              bytes that follow
  uint32   crc32                     CRC-32 of the stored payload
  byte[]   payload                   storedLength bytes
```

- Section ids are names such as `release.label`, session and world sections, and thumbnail
  sections.
- `release.label` carries two int32 values: **build** then **episode**. Build 108 / episode 3 was
  observed on the real test save.
- CRC-32 is the standard IEEE polynomial, initial/final XOR `0xFFFFFFFF`.
- Trailing bytes after the last section, negative/absurd lengths, truncation and checksum
  mismatches are reported as corrupt. The app never attempts to repair a container.

### RLE codec (codec = 1)

The stored stream expands to `rawLength` bytes:

- control byte `< 0x80`: copy `control + 1` literal bytes that follow.
- control byte `>= 0x80`: repeat the next byte `(control & 0x7F) + 3` times.

A stream that would overrun `rawLength` or end early fails validation.

## Episodes 1 and 2 — `.world` (`WSD`)

```
header:
  char[3]  "WSD"                     87 83 68
  int32    sectionCount

section:
  int32    idLength
  byte[]   id                        UTF-8
  int32    payloadLength
  byte[]   payload
```

- No per-section checksum and no embedded episode/build field. Integrity checks are limited to
  structural validation (magic, counts, lengths, exact end of file).
- Because both episodes share `%USERPROFILE%\AppData\LocalLow\sgthale\Family Time`, the app lists
  these saves as **shared** unless you point an episode at a distinct folder in Settings.
- Episode 1/2 support is based on the recovered reader/writer code and automated fixtures. No
  game-written `.world` save existed on the test machine, so this path is not end-to-end verified
  against real output.

## Build-part sidecars (`.bpm`)

Episode 3 keeps build-part data beside the save in Unity's persistent data folder. For a save
`Name.family` the sidecar is `Name.bpm`; for legacy `.world` saves the manager also looks for
`<full file name>.bpm`. Sidecars are copied into backups as `sidecar` entries and restored
alongside the save, but they are never required for a save to be considered valid.

## What the manager will not do

- It does not write, normalise or "fix" a save container.
- It does not invent an episode, build or compatibility for a file that does not carry one.
- Unknown magic, unreadable files and structurally impossible containers are reported as
  **Unsupported**, **Unreadable** or **Corrupt**, with the reason shown in the UI.
