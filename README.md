# Carrier Command 2 — Cheat GUI

A Windows desktop tool for editing **Carrier Command 2** single-player saves: change your
**currency / budget**, and the **item quantities** stored in island warehouses, the carrier
hold, and deployed units (Walrus / Albatross / etc.).

> ⚠️ **Single-player only.** Editing saves for online/co-op sessions where another player
> hosts can desync or be rejected. Always let the tool make its automatic `.bak` backup, and
> **close the game before editing** so it doesn't overwrite your changes on autosave.

## Download

Grab the latest **`CC2CheatGUI.exe`** from the
[**Releases**](https://github.com/Zrionix/CarrierCommand2-Cheat-GUI/releases) page and run it.
It's a self-contained single file — no .NET install needed. The first launch may show a
one-time Windows SmartScreen warning ("More info → Run anyway"), which is expected for an
unsigned hobby tool.

## What it does (v1)

- **Auto-detects your saves** under `%APPDATA%\Carrier Command 2\saved_games\slot_*`,
  or open any `save.xml` manually.
- **Inventory editor** — discovers every inventory container in the save (carrier hold,
  each deployed unit, and each island warehouse) and lets you edit item counts in a grid,
  grouped by category (Ammo, Munitions, Turrets, Components, Vehicles, Fuel).
- **Currency editor** — Carrier Command 2 has no fixed XML tag for money, so the tool uses
  the community-proven method: you enter your **current in-game money**, it finds the matching
  value(s) in the save (flagging the ones near your team data), and writes the new amount.
- **Safe writes** — a timestamped `.bak` of the original is created before every save, and
  the tool only ever writes well-formed XML.

## Roadmap

- **v2 — Live RAM editing.** Carrier Command 2 (`carrier_command.exe`, 64-bit) ships with no
  anti-cheat, so a live "trainer" mode (unlimited credit / fuel / ammo via signature scanning)
  is feasible. It is intentionally **not** in v1 because memory addresses change on every game
  patch; the save editor is the durable feature. Full design & implementation spec:
  [docs/V2-RAM-Editing.md](docs/V2-RAM-Editing.md).

## How the save format works (notes)

Carrier Command 2 saves are plain XML. Two different inventory encodings exist:

- **Island / warehouse stock:** sparse `<q i="ITEM_ID" q="QUANTITY"/>` entries.
- **Vehicle holds (carrier & units):** a **positional** `<item_quantities>` list whose
  children carry a `value="..."` attribute (child position = item ID). This list is stored as
  an **escaped, nested XML document inside the `state` attribute** of the vehicle's state node,
  so it must be un-nested, edited, and re-nested — the tool handles this automatically.

Item IDs are positional and can shift between game versions; unknown IDs are shown as
`Item <id>` rather than guessed.

## Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build -c Release
dotnet run --project src/CC2CheatGUI
```

### Produce a standalone .exe (no runtime needed)

```powershell
dotnet publish src/CC2CheatGUI -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

The single-file `.exe` lands in `src/CC2CheatGUI/bin/Release/net8.0-windows/win-x64/publish/`.
An unsigned executable may trigger a one-time SmartScreen warning — that is expected for
unsigned hobby tools.

### Automated releases (CI)

Two GitHub Actions workflows live in [`.github/workflows`](.github/workflows):

- **build.yml** — compiles the solution on every push/PR to `main`.
- **release.yml** — builds the single-file `.exe` and publishes it. To cut a release:

  ```powershell
  # bump <Version> in CC2CheatGUI.csproj first, then:
  git tag v1.0.0
  git push origin v1.0.0
  ```

  The tag push triggers the workflow, which attaches `CC2CheatGUI.exe` to a new
  [GitHub Release](https://github.com/Zrionix/CarrierCommand2-Cheat-GUI/releases). You can
  also run it manually from the **Actions** tab (it uploads the `.exe` as a build artifact
  instead of creating a Release).

## Disclaimer

For personal, single-player use. Back up your saves. Use at your own risk.
