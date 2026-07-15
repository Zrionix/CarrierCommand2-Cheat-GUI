# Carrier Command 2 — Save Editor / Cheat GUI

A Windows desktop tool for editing **Carrier Command 2** single-player saves, restyled to match the
game's own naval-command-console look. Edit your **credits**, **item quantities** (carrier hold and
island warehouses), **unlocked blueprints**, **island ownership**, and your **fleet's health / fuel /
ammo** — with one-click cheat presets.

> ⚠️ **Single-player only.** Editing saves for online/co-op sessions where another player hosts can
> desync or be rejected. The tool always makes an automatic `.bak` backup, but **close the game
> before saving** so it doesn't overwrite your changes on autosave.

## What it does

- **Auto-detects your saves** under `%APPDATA%\Carrier Command 2\saved_games\slot_*`, or open any
  `save.xml` manually.
- **Overview + Quick Cheats** — one-click presets: max credits, unlock all blueprints, own all
  islands, fill carrier hold, repair/refuel/rearm the fleet, or "Armageddon" (all of them).
- **Credits** — reads and edits the player team's money directly (and every other team's). Handles
  the full `uint32` range (values above two billion are common).
- **Inventory** — every inventory container in the save (carrier hold, each deployed unit, and each
  island warehouse) with per-item editing, bulk-set, and add-by-ID.
- **Blueprints** — unlock every vehicle/attachment blueprint for the player, or clear them.
- **Islands** — list every island with its owner; reassign ownership individually or capture them
  all for the player.
- **Fleet** — list your units with live hitpoints / fuel / weapon counts; edit hitpoints per unit or
  bulk repair / refuel / rearm.
- **Live ▸ Trainer** — edit the *running* game's memory so cheats apply instantly with no reload:
  set/freeze credit (value scan) and toggle Unlimited Ammo / God Mode. CC2 ships with no anti-cheat,
  and the tool finds every address by **signature scanning at attach time** (never hardcoded
  offsets), applying NOP patches it restores on toggle-off/detach. Single-player only; ammo/health
  cheats sit on code shared with the AI, so they affect enemy units too.
- **Safe, faithful writes** — a timestamped `.bak` is created before every save, and the writer
  reproduces the game's exact XML format **byte-for-byte** (verified against real saves), so only
  the values you changed differ from the original.

## The save format (and why v1 couldn't open real saves)

Carrier Command 2 saves are **not** a single well-formed XML document — a `save.xml` is an XML
*fragment*: an `<?xml?>` declaration followed by several sibling top-level elements
(`<meta/>`, `<scene>`, `<vehicles>`, `<missiles>`). The standard `XmlDocument.Load` throws
*"There are multiple root elements"* on every real save, so the original v1 could not open any of
them. v2 wraps the fragment in a synthetic root to parse it and serializes with a custom writer that
matches the game's own formatting exactly:

- single-quoted attributes for values that contain `"` (the escaped `state` blobs),
- no space before `/>`,
- literal tabs/newlines preserved inside attribute values,
- only `& < >` escaped.

Two inventory encodings exist and both are handled automatically:

- **Island / warehouse stock:** sparse `<q i="ITEM_ID" q="QUANTITY"/>` entries.
- **Vehicle holds:** a **positional** `<item_quantities>` list (child index = item ID) stored as an
  **escaped, nested XML document inside the `state` attribute** of the vehicle's state node. Live
  hitpoints, fuel and per-weapon ammo live in the same escaped blob, so the tool parses each
  vehicle's state exactly once and shares it between the inventory and fleet views.

Item IDs are positional and can shift between game versions; unknown IDs are shown as `Item <id>`
rather than guessed.

## Download

Grab the latest **`CC2CheatGUI.exe`** from the
[**Releases**](https://github.com/Zrionix/CarrierCommand2-Cheat-GUI/releases) page and run it. It's a
self-contained single file — no .NET install needed. The first launch may show a one-time Windows
SmartScreen warning ("More info → Run anyway"), expected for an unsigned hobby tool.

## Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build -c Release
dotnet run --project src/CC2CheatGUI
```

### Produce a standalone .exe (no runtime needed)

```powershell
dotnet publish src/CC2CheatGUI -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The single-file `.exe` lands in `src/CC2CheatGUI/bin/Release/net8.0-windows/win-x64/publish/`.

## Credits

- UI font: **LanaPixel** (SIL Open Font License) — the same pixel font Carrier Command 2 uses,
  bundled for an authentic look.

## Disclaimer

For personal, single-player use. Back up your saves. Use at your own risk.
